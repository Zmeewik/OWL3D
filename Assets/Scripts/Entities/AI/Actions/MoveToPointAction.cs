using UnityEngine;

/// <summary>Walk to a world position and stop. Completes on arrival, fails if it stops making progress.</summary>
public class MoveToPointAction : EnemyAction
{
    private readonly Vector3 destination;
    private readonly float arriveDistance;
    private readonly float timeout;

    private float elapsed;

    public MoveToPointAction(Vector3 destination, float arriveDistance = 0f, float timeout = 20f)
    {
        this.destination = destination;
        this.arriveDistance = arriveDistance;
        this.timeout = timeout;
    }

    public override void Begin(Enemy owner)
    {
        base.Begin(owner);
        elapsed = 0f;
        enemy.Rotation?.ClearAim();
        enemy.Movement?.SetDestination(destination);
    }

    public override ActionStatus Tick(float deltaTime)
    {
        var movement = enemy.Movement;
        if (movement == null)
            return ActionStatus.Failed;

        // A timeout rather than a stuck-detector: with no NavMesh baked the entity steers straight
        // at the destination and can end up pressed against a wall forever, and a plan that can
        // never finish would block every queued action behind it.
        elapsed += deltaTime;
        if (timeout > 0f && elapsed >= timeout)
            return ActionStatus.Failed;

        if (movement.ReachedDestination)
            return ActionStatus.Complete;

        if (arriveDistance > 0f && movement.DistanceToDestination <= arriveDistance)
            return ActionStatus.Complete;

        return ActionStatus.Running;
    }

    public override void End()
    {
        enemy.Movement?.Stop();
    }
}
