using UnityEngine;

/// <summary>
/// Close on a target and keep hitting it. Runs until the target dies or disappears -- losing sight
/// of it isn't handled here but by the strategy, which drops this action when vision reports the
/// target lost.
/// </summary>
public class ChaseAndAttackAction : EnemyAction
{
    private readonly TeamMember target;

    /// <summary>Fraction of weapon range the entity actually closes to, so it isn't shooting from the very edge.</summary>
    private const float RangeMargin = 0.8f;

    private Vector3 lastRequestedPosition;

    private readonly float attackChance;
    private readonly float breatherDuration;
    private readonly float repositionDistance;

    private float breatherRemaining;
    private bool repositioning;

    /// <param name="attackChance">
    /// Share of opportunities spent attacking rather than pausing. The remainder become breathers --
    /// a short reposition or a bit of posturing -- so a fight has a rhythm instead of being a
    /// continuous stream of fire.
    /// </param>
    public ChaseAndAttackAction(TeamMember target, float attackChance = 0.7f,
                                float breatherDuration = 1.5f, float repositionDistance = 4f)
    {
        this.target = target;
        this.attackChance = attackChance;
        this.breatherDuration = breatherDuration;
        this.repositionDistance = repositionDistance;
    }

    public TeamMember Target => target;

    public override void Begin(Enemy owner)
    {
        base.Begin(owner);
        lastRequestedPosition = Vector3.positiveInfinity;
        breatherRemaining = 0f;
        repositioning = false;
    }

    public override ActionStatus Tick(float deltaTime)
    {
        if (target == null || !target.IsAlive)
            return ActionStatus.Complete;

        var attack = enemy.Attack;
        var movement = enemy.Movement;
        var rotation = enemy.Rotation;
        var vision = enemy.Vision;

        // Can't see them right now -- go check out where they were last seen instead of chasing and
        // firing at a position read straight off their transform through whatever's blocking sight.
        // Vision keeps scanning every tick regardless of what this action does; the moment it
        // re-spots the target this drops straight back into the live-chase branch below, and if
        // instead its own timeToLose grace period runs out first, the strategy removes this action
        // entirely (see SecurityStrategy.OnTargetLost) -- either way this action never has to decide
        // to give up on its own.
        if (vision != null && !vision.HasVisibleTarget)
            return TickInvestigateLastKnown(movement, rotation, vision);

        Vector3 targetPosition = target.transform.position;
        float distance = Flat(targetPosition - enemy.transform.position).magnitude;
        float engageRange = attack != null ? attack.EffectiveRange * RangeMargin : 2f;

        // Always face the target while engaging: ranged attacks fire along the weapon's forward
        // axis, so shooting before the turn finishes would just spray past them.
        rotation?.AimAt(target.transform);

        if (distance > engageRange)
        {
            // Only re-issue the destination when the target has actually moved, so the movement
            // system's own repath timer governs path cost instead of us resetting it every frame.
            if ((targetPosition - lastRequestedPosition).sqrMagnitude > 0.25f)
            {
                lastRequestedPosition = targetPosition;
                movement?.SetDestination(targetPosition);
            }

            return ActionStatus.Running;
        }

        // In range: either press the attack or take the breather that was rolled for.
        if (breatherRemaining > 0f)
        {
            breatherRemaining -= deltaTime;
            TickBreather(movement, deltaTime);
            return ActionStatus.Running;
        }

        movement?.Stop();
        lastRequestedPosition = Vector3.positiveInfinity;

        if (rotation != null && !rotation.IsFacingAim())
            return ActionStatus.Running;

        // The attack system picks which body part to aim at, so that choice stays with the thing
        // that owns the weapons rather than being duplicated by every action that can attack. It
        // also reports which weapon went off, so the body plays the matching animation instead of
        // one generic attack for both.
        if (attack == null)
            return ActionStatus.Running;

        var result = attack.TryAttack(target);
        if (result != EnemyAttackResult.None)
        {
            enemy.Animation?.PlayAttack(ranged: result == EnemyAttackResult.Ranged);
            RollBreather();
        }

        return ActionStatus.Running;
    }

    /// <summary>
    /// Decides whether to pause after an attack instead of immediately lining up the next one.
    /// Firing without a break reads as a turret rather than a person, so most attacks are followed
    /// by more attacking and the rest by a moment of something else.
    /// </summary>
    private void RollBreather()
    {
        if (Random.value < attackChance)
            return;

        breatherRemaining = Random.Range(breatherDuration * 0.6f, breatherDuration * 1.4f);

        // Half the breathers are spent relocating, the other half standing off and posturing --
        // otherwise every pause looks identical.
        repositioning = Random.value < 0.5f;

        if (repositioning)
        {
            // Sidestep around the target rather than backing off, so the guard stays in the fight
            // while giving the player a moving mark.
            Vector3 toTarget = Flat(target.transform.position - enemy.transform.position);
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector3 sideways = Vector3.Cross(Vector3.up, toTarget.normalized)
                                   * (Random.value < 0.5f ? 1f : -1f);
                enemy.Movement?.SetDestination(enemy.transform.position + sideways * repositionDistance);
            }
        }
        else
        {
            enemy.Movement?.Stop();
            enemy.Animation?.PlayOneShot(EnemyMotion.ShowOff);
        }
    }

    private void TickBreather(EnemyMovementSystem movement, float deltaTime)
    {
        // Keep facing the target throughout: a breather is a pause in shooting, not in attention.
        enemy.Rotation?.AimAt(target.transform);

        if (!repositioning)
            return;

        if (movement != null && movement.ReachedDestination)
            movement.Stop();
    }

    public override void End()
    {
        enemy.Movement?.Stop();
        enemy.Rotation?.ClearAim();
    }

    /// <summary>
    /// Walks to wherever the target was last actually seen and stops -- no aim-lock (there's nothing
    /// to aim at), no attack. Just holds there once arrived; nothing left to decide until vision
    /// either re-spots the target or times out for the strategy to act on.
    /// </summary>
    private ActionStatus TickInvestigateLastKnown(EnemyMovementSystem movement, EnemyRotationSystem rotation, EnemyVisionSystem vision)
    {
        rotation?.ClearAim();

        Vector3 lastKnown = vision.LastKnownPosition;
        if ((lastKnown - lastRequestedPosition).sqrMagnitude > 0.25f)
        {
            lastRequestedPosition = lastKnown;
            movement?.SetDestination(lastKnown);
        }

        if (movement != null && movement.ReachedDestination)
            movement.Stop();

        return ActionStatus.Running;
    }

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
