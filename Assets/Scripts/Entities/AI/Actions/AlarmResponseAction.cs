using UnityEngine;

/// <summary>
/// Rallies to an alarm post: arms the weapon, spreads out around the post rather than stacking on
/// it (via <see cref="SurroundPointAction"/>), then holds combat-ready there for a while before
/// completing. Completing lets the queue empty and the strategy's own fallback (walking back to
/// its post) take over -- there's no separate stand-down step here.
///
/// Never checks for a hostile itself: if vision spots one mid-rally or mid-hold, the strategy's
/// OnTargetSpotted clears this out and replaces it with a chase, the same way it pre-empts anything
/// else that happens to be queued.
/// </summary>
public class AlarmResponseAction : EnemyAction
{
    private readonly Vector3 postPosition;
    private readonly float rallyRadius;
    private readonly float holdDuration;

    private SurroundPointAction rally;
    private bool arrived;
    private float holdElapsed;

    public AlarmResponseAction(Vector3 postPosition, float rallyRadius, float holdDuration)
    {
        this.postPosition = postPosition;
        this.rallyRadius = rallyRadius;
        this.holdDuration = holdDuration;
    }

    public override void Begin(Enemy owner)
    {
        base.Begin(owner);

        enemy.Animation?.SetArmed(true);

        arrived = false;
        holdElapsed = 0f;
        rally = new SurroundPointAction(postPosition, rallyRadius);
        rally.Begin(owner);
    }

    public override ActionStatus Tick(float deltaTime)
    {
        if (!arrived)
        {
            var status = rally.Tick(deltaTime);
            if (status == ActionStatus.Failed)
                return ActionStatus.Failed;

            if (status == ActionStatus.Running)
                return ActionStatus.Running;

            rally.End();
            arrived = true;
            enemy.Movement?.Stop();
        }

        holdElapsed += deltaTime;
        return holdElapsed >= holdDuration ? ActionStatus.Complete : ActionStatus.Running;
    }

    public override void End()
    {
        if (!arrived)
            rally?.End();
        else
            enemy.Movement?.Stop();
    }
}
