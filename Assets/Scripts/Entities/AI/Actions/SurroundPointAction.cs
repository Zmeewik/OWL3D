using UnityEngine;

/// <summary>
/// Take up a position on a ring around a point, rather than everyone piling onto the point itself.
///
/// The slot is picked from the entity's own instance id instead of being handed out by the manager:
/// it spreads a squad deterministically around the circle with no central bookkeeping, and each
/// entity keeps its slot across repeated commands. Good enough for "surround that" -- it doesn't
/// guarantee even spacing when only a couple of entities respond.
/// </summary>
public class SurroundPointAction : EnemyAction
{
    private readonly Vector3 center;
    private readonly float radius;

    private MoveToPointAction move;

    public SurroundPointAction(Vector3 center, float radius)
    {
        this.center = center;
        this.radius = Mathf.Max(0.5f, radius);
    }

    public override void Begin(Enemy owner)
    {
        base.Begin(owner);

        // Deterministic pseudo-random angle per entity.
        float angle = Mathf.Abs(owner.GetInstanceID() * 0.6180339f % 1f) * Mathf.PI * 2f;
        Vector3 offset = new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

        move = new MoveToPointAction(center + offset, arriveDistance: 1f);
        move.Begin(owner);
    }

    public override ActionStatus Tick(float deltaTime) => move?.Tick(deltaTime) ?? ActionStatus.Failed;

    public override void End() => move?.End();
}
