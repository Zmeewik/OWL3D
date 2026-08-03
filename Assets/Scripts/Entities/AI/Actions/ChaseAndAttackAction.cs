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

    public ChaseAndAttackAction(TeamMember target)
    {
        this.target = target;
    }

    public TeamMember Target => target;

    public override void Begin(Enemy owner)
    {
        base.Begin(owner);
        lastRequestedPosition = Vector3.positiveInfinity;
    }

    public override ActionStatus Tick(float deltaTime)
    {
        if (target == null || !target.IsAlive)
            return ActionStatus.Complete;

        var attack = enemy.Attack;
        var movement = enemy.Movement;
        var rotation = enemy.Rotation;

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

        movement?.Stop();
        lastRequestedPosition = Vector3.positiveInfinity;

        if (rotation != null && !rotation.IsFacingAim())
            return ActionStatus.Running;

        if (attack != null && attack.TryAttack(targetPosition))
            enemy.Animation?.PlayAttack();

        return ActionStatus.Running;
    }

    public override void End()
    {
        enemy.Movement?.Stop();
        enemy.Rotation?.ClearAim();
    }

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
