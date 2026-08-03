using UnityEngine;

/// <summary>
/// Walk up to a friendly and interact with them. The interaction itself is a debug log for now --
/// the point of the action is that friendlies get a distinct, non-hostile response from the same
/// vision event that would otherwise start a fight.
/// </summary>
public class InteractWithFriendlyAction : EnemyAction
{
    private readonly TeamMember friendly;
    private readonly float interactRange;
    private readonly float timeout;

    private float elapsed;
    private bool interacted;

    public InteractWithFriendlyAction(TeamMember friendly, float interactRange, float timeout = 15f)
    {
        this.friendly = friendly;
        this.interactRange = interactRange;
        this.timeout = timeout;
    }

    public TeamMember Friendly => friendly;

    public override void Begin(Enemy owner)
    {
        base.Begin(owner);
        elapsed = 0f;
        interacted = false;
    }

    public override ActionStatus Tick(float deltaTime)
    {
        if (friendly == null || !friendly.IsAlive)
            return ActionStatus.Complete;

        elapsed += deltaTime;
        if (timeout > 0f && elapsed >= timeout)
            return ActionStatus.Failed;

        Vector3 friendlyPosition = friendly.transform.position;
        float distance = Flat(friendlyPosition - enemy.transform.position).magnitude;

        if (distance > interactRange)
        {
            enemy.Rotation?.AimAt(friendly.transform);
            enemy.Movement?.SetDestination(friendlyPosition);
            return ActionStatus.Running;
        }

        enemy.Movement?.Stop();
        enemy.Rotation?.AimAt(friendly.transform);

        if (!interacted)
        {
            interacted = true;
            Debug.Log($"[AI] {enemy.name} interacts with friendly {friendly.name}.", enemy);
        }

        return ActionStatus.Complete;
    }

    public override void End()
    {
        enemy.Movement?.Stop();
        enemy.Rotation?.ClearAim();
    }

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
