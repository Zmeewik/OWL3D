/// <summary>Result of ticking an action, telling the strategy whether to move on.</summary>
public enum ActionStatus
{
    Running,
    Complete,
    Failed,
}

/// <summary>
/// One step in a strategy's plan ("walk here", "engage that", "stand around"). Plain C# objects
/// rather than components so a strategy can build, insert and drop them freely at runtime -- which
/// is the whole point of the action list (add / remove / clear).
///
/// Actions never touch physics or animators directly; they drive the entity's systems.
/// </summary>
public abstract class EnemyAction
{
    protected Enemy enemy;

    /// <summary>Called once when the action reaches the front of the queue.</summary>
    public virtual void Begin(Enemy owner)
    {
        enemy = owner;
    }

    public abstract ActionStatus Tick(float deltaTime);

    /// <summary>Called when the action finishes, fails, or is dropped early. Must leave systems in a neutral state.</summary>
    public virtual void End() { }

    public override string ToString() => GetType().Name;
}
