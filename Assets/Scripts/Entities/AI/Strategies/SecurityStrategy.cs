using UnityEngine;

/// <summary>
/// Behaviour for a standing guard (the Runner). Idles until it sees something, then reacts by
/// allegiance: hostiles get chased and attacked and called in over the radio, friendlies get walked
/// up to and greeted, neutrals are ignored outright.
///
/// The same class covers a bandit -- what differs between the two is the <see cref="EnemyConfig"/>
/// (team, ranges, reaction times), not the decision-making. Split it only when a class actually
/// needs to decide something differently.
/// </summary>
public class SecurityStrategy : EnemyStrategy
{
    [Header("Reactions")]
    [Tooltip("Shout a spotted hostile to nearby allies as well as over the radio.")]
    [SerializeField] private bool alertNearbyAllies = true;

    [Tooltip("Also relay spotted/lost over the radio frequency, reaching allies at any distance.")]
    [SerializeField] private bool useRadio = true;

    protected override void OnInitialized()
    {
        Enqueue(new IdleAction());
    }

    /// <summary>
    /// Falling back to idle is only right if there's nothing to fight. Vision raises
    /// OnTargetSpotted once, on the transition -- so an entity whose plan gets wiped (a command
    /// arriving mid-fight, an action failing) while the hostile is still standing in front of it
    /// would otherwise stand there idling and never re-engage, because the spot event has already
    /// been and gone. Re-deriving intent from what vision currently holds closes that.
    /// </summary>
    protected override void OnQueueEmpty()
    {
        var target = enemy != null && enemy.Vision != null ? enemy.Vision.CurrentTarget : null;

        if (target != null && target.IsAlive && Teams.IsHostile(Team, target.team))
        {
            Enqueue(new ChaseAndAttackAction(target));
            return;
        }

        // Nothing left to fight or look for, so the weapon goes away. Doing it here rather than the
        // moment a target is lost is what lets the guard keep it drawn for the whole search -- it's
        // still hunting, and re-holstering while walking the area looks like it has forgotten.
        enemy.Animation?.SetArmed(false);

        base.OnQueueEmpty();
    }

    protected override void OnTargetSpotted(TeamMember target)
    {
        if (target == null) return;

        switch (Teams.Relation(Team, target.team))
        {
            case TeamRelation.Hostile:
                Debug.Log($"[AI] {enemy.name} spotted hostile {target.name}.", enemy);
                Announce(target);
                Engage(target);
                break;

            case TeamRelation.Friendly:
                // Don't break off a fight to say hello.
                if (!HasAction<ChaseAndAttackAction>())
                {
                    ClearActions();
                    Enqueue(new InteractWithFriendlyAction(target, InteractRange));
                }
                break;

            case TeamRelation.Neutral:
                // Deliberately nothing.
                break;
        }
    }

    /// <summary>
    /// Already fighting? A stray hit doesn't need to interrupt that -- whoever's attacking gets
    /// found the normal way (vision, or their next hit lands on an idle guard instead). Otherwise
    /// the guard draws its weapon and goes hunting up the line the shot came from.
    /// </summary>
    protected override void OnDamaged(DamagePacket packet)
    {
        if (HasAction<ChaseAndAttackAction>())
            return;

        // Being shot at is reason enough to have the gun out, even before anything is spotted.
        enemy.Animation?.SetArmed(true);

        base.OnDamaged(packet);
    }

    /// <summary>
    /// Losing sight of someone doesn't mean forgetting them. The guard drops the chase but walks on
    /// to wherever it last actually saw them and searches around there, weapon still out, giving up
    /// only when the search runs out of time (see <see cref="OnQueueEmpty"/>, which stows the weapon
    /// once there's genuinely nothing left to do).
    /// </summary>
    protected override void OnTargetLost(TeamMember target)
    {
        if (!HasAction<ChaseAndAttackAction>())
            return;

        Vector3 lastKnown = enemy.Vision != null ? enemy.Vision.LastKnownPosition : transform.position;

        Debug.Log($"[AI] {enemy.name} lost {(target != null ? target.name : "target")}, searching " +
                  $"around {lastKnown}.", enemy);

        RemoveActions<ChaseAndAttackAction>();
        SearchFor(lastKnown);

        if (useRadio)
            EnemyManager.Broadcast(EnemyCommand.TargetLost(transform, target, enemy.RadioFrequency));
    }

    public override void HandleCommand(EnemyCommand command)
    {
        switch (command.type)
        {
            case EnemyCommandType.TargetSpotted:
                // Only act on it if that target is actually our enemy -- a shared radio channel
                // doesn't imply a shared enemy list.
                if (command.target != null && Teams.IsHostile(Team, command.target.team))
                    Engage(command.target);
                break;

            case EnemyCommandType.TargetLost:
                RemoveActions<ChaseAndAttackAction>();
                break;

            case EnemyCommandType.MoveTo:
                ClearActions();
                Enqueue(new MoveToPointAction(command.position));
                break;

            case EnemyCommandType.SurroundPoint:
                ClearActions();
                Enqueue(new SurroundPointAction(command.position, command.radius));
                break;

            case EnemyCommandType.Defend:
                ClearActions();
                // Get inside the guard radius; anything hostile that turns up is picked up by
                // vision from there and handled by OnTargetSpotted.
                if (command.target != null)
                    Enqueue(new MoveToPointAction(command.target.transform.position, command.radius));
                break;

            case EnemyCommandType.HoldPosition:
                ClearActions();
                Enqueue(new IdleAction());
                break;
        }
    }

    private void Engage(TeamMember target)
    {
        if (target == null || !target.IsAlive)
            return;

        // Drawing the weapon belongs here rather than in the action, so an entity ordered into a
        // fight over the radio arms itself exactly like one that spotted the enemy on its own.
        // SetArmed is a no-op when already armed, so re-engaging a new target doesn't re-draw.
        enemy.Animation?.SetArmed(true);

        // Already fighting this one? Leave the running action alone rather than restarting it.
        if (CurrentAction is ChaseAndAttackAction chase && chase.Target == target)
            return;

        ClearActions();
        Enqueue(new ChaseAndAttackAction(target));
    }

    private void Announce(TeamMember target)
    {
        if (alertNearbyAllies)
            EnemyManager.Broadcast(EnemyCommand.TargetSpotted(transform, target, AlertRadius));

        if (useRadio)
            EnemyManager.Broadcast(EnemyCommand.TargetSpottedOnRadio(transform, target, enemy.RadioFrequency));
    }

    private float AlertRadius => enemy != null && enemy.Config != null ? enemy.Config.alertRadius : 20f;
    private float InteractRange => enemy != null && enemy.Config != null ? enemy.Config.interactRange : 2.5f;
}
