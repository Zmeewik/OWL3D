using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Moves the entity to a destination. Pathing comes from the NavMesh, but the movement itself is
/// hand-rolled on the Rigidbody rather than delegated to a NavMeshAgent -- an agent would take over
/// the transform and fight the physics the rest of the game (knockback, ragdolls, slopes) is built
/// on. So the NavMesh is used purely as a route planner: it hands us a corner list, and we steer
/// along it ourselves.
/// </summary>
public class EnemyMovementSystem : EnemySystem
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float stoppingDistance = 0.6f;

    [Header("Pathing")]
    [Tooltip("How often the route is recalculated while chasing a moving target.")]
    [SerializeField] private float repathInterval = 0.35f;
    [Tooltip("How close the entity must get to a path corner before steering to the next one.")]
    [SerializeField] private float cornerReachDistance = 0.6f;
    [Tooltip("How far off the NavMesh a requested destination may be and still snap onto it.")]
    [SerializeField] private float navSampleDistance = 4f;

    private NavMeshPath path;
    private readonly System.Collections.Generic.List<Vector3> corners = new();
    private int cornerIndex;

    private Vector3 destination;
    private float repathTimer;
    private bool warnedAboutMissingNavMesh;

    public bool HasDestination { get; private set; }

    /// <summary>Horizontal direction currently being steered in, or zero when standing still. Read by <see cref="EnemyRotationSystem"/>.</summary>
    public Vector3 MoveDirection { get; private set; }

    public bool IsMoving => HasDestination && MoveDirection.sqrMagnitude > 0.001f;

    public float DistanceToDestination =>
        HasDestination ? Vector3.Distance(Flat(transform.position), Flat(destination)) : 0f;

    public bool ReachedDestination => HasDestination && DistanceToDestination <= stoppingDistance;

    public override void Initialize(Enemy owner)
    {
        base.Initialize(owner);
        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();
        path = new NavMeshPath();
    }

    public void ConfigureFrom(EnemyConfig config)
    {
        if (config == null) return;
        moveSpeed = config.moveSpeed;
        acceleration = config.acceleration;
        stoppingDistance = config.stoppingDistance;
    }

    public void SetDestination(Vector3 worldPosition)
    {
        destination = worldPosition;
        HasDestination = true;
        repathTimer = 0f;
        RecalculatePath();
    }

    public void Stop()
    {
        HasDestination = false;
        MoveDirection = Vector3.zero;
        corners.Clear();
        cornerIndex = 0;
    }

    public override void FixedTickSystem(float fixedDeltaTime)
    {
        if (rb == null) return;

        if (!HasDestination || ReachedDestination)
        {
            MoveDirection = Vector3.zero;
            Decelerate(fixedDeltaTime);
            return;
        }

        repathTimer -= fixedDeltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            RecalculatePath();
        }

        Vector3 steerTarget = NextSteerTarget();
        Vector3 toTarget = Flat(steerTarget - transform.position);
        MoveDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;

        Vector3 desired = MoveDirection * moveSpeed;
        Vector3 horizontal = Flat(rb.velocity);
        Vector3 next = Vector3.MoveTowards(horizontal, desired, acceleration * fixedDeltaTime);
        rb.velocity = new Vector3(next.x, rb.velocity.y, next.z);
    }

    private void Decelerate(float fixedDeltaTime)
    {
        Vector3 horizontal = Flat(rb.velocity);
        Vector3 next = Vector3.MoveTowards(horizontal, Vector3.zero, acceleration * fixedDeltaTime);
        rb.velocity = new Vector3(next.x, rb.velocity.y, next.z);
    }

    /// <summary>
    /// Current corner to steer at, advancing through the path as corners are reached. With no
    /// usable path this degrades to steering straight at the destination, which keeps the AI
    /// functional in a scene whose NavMesh hasn't been baked (just without obstacle avoidance).
    /// </summary>
    private Vector3 NextSteerTarget()
    {
        while (cornerIndex < corners.Count &&
               Vector3.Distance(Flat(transform.position), Flat(corners[cornerIndex])) <= cornerReachDistance)
        {
            cornerIndex++;
        }

        return cornerIndex < corners.Count ? corners[cornerIndex] : destination;
    }

    private void RecalculatePath()
    {
        corners.Clear();
        cornerIndex = 0;

        if (path == null)
            path = new NavMeshPath();

        // Both ends have to be on the NavMesh for CalculatePath to return anything useful; entities
        // stand slightly above it and destinations are often a live target's exact position, so
        // both get snapped first.
        if (!NavMesh.SamplePosition(transform.position, out var fromHit, navSampleDistance, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(destination, out var toHit, navSampleDistance, NavMesh.AllAreas))
        {
            WarnMissingNavMeshOnce();
            return;
        }

        if (!NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, path) ||
            path.status == NavMeshPathStatus.PathInvalid ||
            path.corners.Length == 0)
        {
            return;
        }

        corners.AddRange(path.corners);

        // corners[0] is the entity's own position; steering at it would stall the first step.
        if (corners.Count > 1)
            cornerIndex = 1;
    }

    private void WarnMissingNavMeshOnce()
    {
        if (warnedAboutMissingNavMesh) return;
        warnedAboutMissingNavMesh = true;
        Debug.LogWarning(
            $"[{name}] No NavMesh under this entity or its destination -- falling back to direct " +
            "steering with no obstacle avoidance. Bake one via Window > AI > Navigation.", this);
    }

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
