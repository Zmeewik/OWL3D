using UnityEngine;

/// <summary>
/// Turns to face a fixed point, then stops -- no movement, no attack. Used as a startled reaction
/// (e.g. facing where an unseen shot came from): vision keeps scanning the whole time this runs, so
/// a real target caught in view during the turn is picked up the normal way (OnTargetSpotted fires
/// and the strategy engages it); if nothing's there once the turn finishes, the queue empties and
/// the strategy's own OnQueueEmpty falls back to idling.
/// </summary>
public class FaceDirectionAction : EnemyAction
{
    private readonly Vector3 point;
    private readonly float timeout;
    private float elapsed;

    /// <param name="timeout">
    /// Safety net in case facing never resolves (no Rotation system, degenerate direction) so the
    /// entity doesn't stand locked on a stray point forever with nothing there to find.
    /// </param>
    public FaceDirectionAction(Vector3 point, float timeout = 1.5f)
    {
        this.point = point;
        this.timeout = timeout;
    }

    public override void Begin(Enemy owner)
    {
        base.Begin(owner);
        elapsed = 0f;
        enemy.Rotation?.AimAt(point);
    }

    public override ActionStatus Tick(float deltaTime)
    {
        elapsed += deltaTime;

        var rotation = enemy.Rotation;
        if (rotation == null || rotation.IsFacingAim())
            return ActionStatus.Complete;

        return elapsed >= timeout ? ActionStatus.Complete : ActionStatus.Running;
    }

    public override void End()
    {
        enemy.Rotation?.ClearAim();
    }
}
