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

    [Header("Avoidance")]
    [Tooltip("Personal space from other enemies. Closer than this, a push-away steer blends in on top of the path.")]
    [SerializeField] private float avoidanceRadius = 1.5f;
    [Tooltip("How hard the push-away steer is at zero distance between two entities, tapering to 0 at avoidanceRadius.")]
    [SerializeField] private float avoidanceStrength = 3f;

    [Header("Jumping")]
    [Tooltip("How far a corner-to-corner segment's midpoint may be from real NavMesh surface and " +
             "still count as ground. Beyond this it's treated as a gap the path only crosses via a jump.")]
    [SerializeField] private float jumpGapSampleRadius = 0.35f;
    [Tooltip("Minimum apex height above the takeoff point for a jump arc.")]
    [SerializeField] private float jumpApexHeight = 1.2f;
    [Tooltip("Safety cap on a jump's flight time, in case the computed arc math ever degenerates.")]
    [SerializeField] private float jumpMaxDuration = 3f;

    private NavMeshPath path;
    private readonly System.Collections.Generic.List<Vector3> corners = new();
    private readonly System.Collections.Generic.List<bool> segmentIsJump = new();
    private int cornerIndex;

    private bool isJumping;
    private float jumpElapsed;
    private float jumpDuration;

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
        avoidanceRadius = config.avoidanceRadius;
        avoidanceStrength = config.avoidanceStrength;
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
        segmentIsJump.Clear();
        cornerIndex = 0;
        isJumping = false;
    }

    public override void FixedTickSystem(float fixedDeltaTime)
    {
        if (rb == null) return;

        // A jump commits to its arc once launched -- no steering, no repathing, just watching the
        // clock -- same as a real jump can't change its mind partway through.
        if (isJumping)
        {
            TickJump(fixedDeltaTime);
            return;
        }

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

        // The segment leading to the corner we're now aimed at may not be walkable ground at all --
        // a ledge or a gap the NavMesh only connects via an (auto-generated) off-mesh link. Launch a
        // jump instead of trying to walk it.
        if (cornerIndex < segmentIsJump.Count && segmentIsJump[cornerIndex])
        {
            BeginJump(steerTarget);
            return;
        }

        Vector3 toTarget = Flat(steerTarget - transform.position);
        Vector3 pathDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;

        // Other enemies routed through the same corridor push the heading aside rather than being
        // walked through -- NavMesh carving (see EnemyManager.SetupDynamicObstacles) handles static
        // and dynamic props, but two agents both reading the same bake have no idea about each other.
        Vector3 steer = pathDirection + ComputeSeparation();
        MoveDirection = steer.sqrMagnitude > 0.0001f ? steer.normalized : pathDirection;

        Vector3 desired = MoveDirection * moveSpeed;
        Vector3 horizontal = Flat(rb.velocity);
        Vector3 next = Vector3.MoveTowards(horizontal, desired, acceleration * fixedDeltaTime);
        rb.velocity = new Vector3(next.x, rb.velocity.y, next.z);
    }

    /// <summary>
    /// Launches a physics-driven jump arc toward <paramref name="target"/>: the launch velocity is
    /// solved analytically (apex height -> time up/down -> horizontal speed) and set once, then
    /// gravity (already acting on the Rigidbody every step, same as normal ground movement only
    /// ever touches the horizontal component of velocity) carries it the rest of the way. No
    /// steering during the arc -- a real jump commits the moment it leaves the ground.
    /// </summary>
    private void BeginJump(Vector3 target)
    {
        Vector3 origin = transform.position;
        Vector3 flatDelta = Flat(target - origin);
        float horizontalDistance = flatDelta.magnitude;
        float heightDelta = target.y - origin.y;

        float gravity = Mathf.Abs(Physics.gravity.y);
        // The apex has to clear the higher of the two ends -- for a jump that lands above takeoff,
        // jumpApexHeight alone (measured from takeoff) might not leave any clearance over the ledge.
        float apex = Mathf.Max(jumpApexHeight, heightDelta + 0.3f);

        float timeUp = Mathf.Sqrt(2f * apex / gravity);
        float timeDown = Mathf.Sqrt(2f * Mathf.Max(0.05f, apex - heightDelta) / gravity);
        jumpDuration = timeUp + timeDown;
        jumpElapsed = 0f;

        Vector3 horizontalVelocity = horizontalDistance > 0.0001f
            ? flatDelta.normalized * (horizontalDistance / jumpDuration)
            : Vector3.zero;
        float verticalVelocity = gravity * timeUp;

        rb.velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        isJumping = true;
        MoveDirection = flatDelta.sqrMagnitude > 0.0001f ? flatDelta.normalized : MoveDirection;
    }

    private void TickJump(float fixedDeltaTime)
    {
        jumpElapsed += fixedDeltaTime;
        if (jumpElapsed < jumpDuration && jumpElapsed < jumpMaxDuration)
            return;

        // Landed (by the clock, not a ground raycast -- the arc's own math already targeted the
        // corner's height, which came from a NavMesh sample resting on the ground). Consume the
        // corner just crossed and let the next tick resume normal corner-following (or chain
        // straight into another jump, if the path calls for one).
        isJumping = false;
        cornerIndex++;
    }

    /// <summary>
    /// Push-away steer from every other live enemy within <see cref="avoidanceRadius"/>, strongest
    /// at zero distance and fading to nothing at the radius. Not normalized to a unit vector -- its
    /// magnitude relative to the path direction (always length 1) is what lets a near-collision
    /// dominate the heading while a distant neighbour barely nudges it.
    /// </summary>
    private Vector3 ComputeSeparation()
    {
        if (avoidanceStrength <= 0f || avoidanceRadius <= 0f)
            return Vector3.zero;

        Vector3 push = Vector3.zero;
        var active = EnemyManager.Active;
        for (int i = 0; i < active.Count; i++)
        {
            var other = active[i];
            if (other == null || other == enemy || !other.IsAlive)
                continue;

            Vector3 offset = Flat(transform.position - other.transform.position);
            float distance = offset.magnitude;
            if (distance >= avoidanceRadius || distance < 0.0001f)
                continue;

            float weight = 1f - distance / avoidanceRadius;
            push += offset.normalized * weight;
        }

        return push * avoidanceStrength;
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
        segmentIsJump.Clear();
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

        // A real walkable stretch of ground is continuously on the NavMesh along its whole length;
        // a corner-to-corner segment that crosses an off-mesh link (auto-generated at bake time for
        // drops/gaps within the configured Drop Height / Jump Distance) is not -- its midpoint sits
        // over empty space. That difference is the only signal available without a NavMeshAgent,
        // which is what actually knows it's traversing a link.
        for (int i = 0; i < corners.Count; i++)
        {
            bool isJump = i > 0 && !NavMesh.SamplePosition(
                (corners[i - 1] + corners[i]) * 0.5f, out _, jumpGapSampleRadius, NavMesh.AllAreas);
            segmentIsJump.Add(isJump);
        }

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
