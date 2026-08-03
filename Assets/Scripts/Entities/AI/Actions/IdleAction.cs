using UnityEngine;

/// <summary>
/// Stand still and do nothing. The fallback whenever a strategy's queue empties, so an entity
/// always has something to be doing rather than holding whatever half-finished state its last
/// action left behind.
/// </summary>
public class IdleAction : EnemyAction
{
    private readonly float duration;
    private float elapsed;

    /// <param name="duration">Seconds to idle for; anything &lt;= 0 idles indefinitely.</param>
    public IdleAction(float duration = 0f)
    {
        this.duration = duration;
    }

    public override void Begin(Enemy owner)
    {
        base.Begin(owner);
        elapsed = 0f;
        enemy.Movement?.Stop();
        enemy.Rotation?.ClearAim();
    }

    public override ActionStatus Tick(float deltaTime)
    {
        if (duration <= 0f)
            return ActionStatus.Running;

        elapsed += deltaTime;
        return elapsed >= duration ? ActionStatus.Complete : ActionStatus.Running;
    }
}
