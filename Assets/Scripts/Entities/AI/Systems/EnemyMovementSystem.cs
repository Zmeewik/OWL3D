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
    [Tooltip("Extra cushion on top of both entities' actual physical radii. The effective personal " +
             "space is PhysicalRadius + otherEntity.PhysicalRadius + this -- not a flat number -- so " +
             "it stays correct however an entity happens to be scaled.")]
    [SerializeField] private float avoidanceMargin = 2f;
    [Tooltip("How hard the push-away steer is at zero distance between two entities, tapering to 0 at the effective radius.")]
    [SerializeField] private float avoidanceStrength = 3f;

    private CapsuleCollider capsule;

    /// <summary>
    /// This entity's actual world-space contact radius, honouring whatever scale it's placed at.
    /// Avoidance is sized from this rather than a fixed constant so a 2.4x-scaled entity (which
    /// physically touches another one at ~2.4x the "default" contact distance) still gets a real
    /// cushion instead of slamming into contact before the steer has any room to react.
    ///
    /// Includes the capsule's own local X/Z centre offset, not just its radius: this rig's collider
    /// is centred slightly off the character's vertical axis, and since the entity turns to face
    /// whatever it's approaching, that offset rotates into extra real-world reach in whichever
    /// direction it happens to be facing. Folding it into the radius (rather than assuming it's
    /// always zero) is what makes two rotating, off-centre capsules actually stop clear of each
    /// other instead of a hard collision only ~30cm before the number this used to predict.
    /// </summary>
    public float PhysicalRadius { get; private set; } = 0.5f;

    [Header("Jumping")]
    [Tooltip("Minimum height difference a NavMesh.Raycast-blocked segment needs before it counts as " +
             "a jump instead of ordinary ground. Must clear the bake's own agentClimb (step height) " +
             "with real margin -- otherwise every seam between two adjacent, not-quite-welded floor " +
             "or wall pieces (same height, technically separate NavMesh islands) reads as a gap and " +
             "gets a full jump arc for a difference an agent would just step over.")]
    [SerializeField] private float minJumpHeight = 1f;
    [Tooltip("Minimum apex height above the takeoff point for a jump arc.")]
    [SerializeField] private float jumpApexHeight = 1.2f;
    [Tooltip("Safety cap on a jump's flight time, in case the computed arc math ever degenerates.")]
    [SerializeField] private float jumpMaxDuration = 3f;

    [Tooltip("Quiet period after a jump-link attempt before another may be started, so an impossible climb isn't retried on loop.")]
    [SerializeField] private float linkRetryDelay = 1.5f;

    [Header("Dodging")]
    [Tooltip("How fast the sideways hop off an incoming shot is.")]
    [SerializeField] private float dodgeSpeed = 9f;
    [Tooltip("How long the dodge drives movement before normal steering resumes.")]
    [SerializeField] private float dodgeDuration = 0.35f;

    [Header("Depenetration")]
    [Tooltip("What the body is pushed back out of when it ends up inside something: other entities " +
             "and world geometry.")]
    [SerializeField] private LayerMask depenetrationMask = ~0;

    [Tooltip("Cap on how fast overlap is resolved, so a deep intersection doesn't fling the body.")]
    [SerializeField] private float maxDepenetrationSpeed = 6f;

    private readonly Collider[] overlapBuffer = new Collider[16];

    private float dodgeRemaining;
    private Vector3 dodgeVelocity;

    /// <summary>True while a dodge hop is driving movement. Read by the animation system.</summary>
    public bool IsDodging => dodgeRemaining > 0f;

    private NavMeshPath path;
    private readonly System.Collections.Generic.List<Vector3> corners = new();
    private readonly System.Collections.Generic.List<bool> segmentIsJump = new();
    private int cornerIndex;

    private bool isJumping;
    private float jumpElapsed;
    private float jumpDuration;

    private bool hasPendingLink;
    private JumpLinkMap.JumpLink pendingLink;

    /// <summary>
    /// Stops a link whose jump doesn't actually land from being retried every repath. Geometry can
    /// always produce a climb the arc can't really make, and without this the entity would stand at
    /// the same ledge hurling itself at it several times a second.
    /// </summary>
    private float linkCooldownRemaining;

    private Vector3 destination;
    private float repathTimer;
    private bool warnedAboutMissingNavMesh;

    public bool HasDestination { get; private set; }

    /// <summary>Horizontal direction currently being steered in, or zero when standing still. Read by <see cref="EnemyRotationSystem"/>.</summary>
    public Vector3 MoveDirection { get; private set; }

    public bool IsMoving => HasDestination && MoveDirection.sqrMagnitude > 0.001f;

    /// <summary>
    /// Scales <see cref="moveSpeed"/> without overwriting it, so an action can move at a different
    /// pace (a search walks rather than sprints) and hand the entity back at its normal speed
    /// afterwards -- the configured speed stays the one source of truth.
    /// </summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>True for the whole flight of a jump arc. Read by <see cref="EnemyAnimationSystem"/> so
    /// it plays an in-air pose instead of leaving the walk cycle looping while the rigidbody flies a
    /// parabola -- without this the jump was physically correct but looked like sliding through the
    /// air, because IsMoving (MoveDirection stays nonzero mid-arc) kept selecting Walk every tick.</summary>
    public bool IsJumping => isJumping;

    public float DistanceToDestination =>
        HasDestination ? Vector3.Distance(Flat(transform.position), Flat(destination)) : 0f;

    public bool ReachedDestination => HasDestination && DistanceToDestination <= stoppingDistance;

    public override void Initialize(Enemy owner)
    {
        base.Initialize(owner);
        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();
        if (capsule == null)
            capsule = GetComponentInParent<CapsuleCollider>();

        if (capsule != null)
        {
            var scale = capsule.transform.lossyScale;
            float centerOffset = new Vector2(capsule.center.x, capsule.center.z).magnitude;
            PhysicalRadius = (capsule.radius + centerOffset) * Mathf.Max(scale.x, scale.z);
        }

        path = new NavMeshPath();
    }

    public void ConfigureFrom(EnemyConfig config)
    {
        if (config == null) return;
        moveSpeed = config.moveSpeed;
        acceleration = config.acceleration;
        stoppingDistance = config.stoppingDistance;
        avoidanceMargin = config.avoidanceMargin;
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
        hasPendingLink = false;
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

        // A dodge likewise owns movement for its brief duration; steering back toward the target
        // mid-hop would cancel out the sideways displacement that is the entire point.
        if (dodgeRemaining > 0f)
        {
            dodgeRemaining -= fixedDeltaTime;
            rb.velocity = new Vector3(dodgeVelocity.x, rb.velocity.y, dodgeVelocity.z);
            return;
        }

        if (!HasDestination || ReachedDestination)
        {
            MoveDirection = Vector3.zero;
            Decelerate(fixedDeltaTime);
            return;
        }

        if (linkCooldownRemaining > 0f)
            linkCooldownRemaining -= fixedDeltaTime;

        repathTimer -= fixedDeltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            RecalculatePath();
        }

        Vector3 steerTarget = NextSteerTarget();

        // Standing on the takeoff of a climb the walk path can't express? Launch it. This is the
        // only way onto a raised platform -- see JumpLinkMap for why the bake can't provide one.
        if (hasPendingLink && Flat(pendingLink.takeoff - transform.position).magnitude <= cornerReachDistance)
        {
            hasPendingLink = false;
            linkCooldownRemaining = linkRetryDelay;
            BeginJump(pendingLink.landing);
            return;
        }

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

        Vector3 desired = MoveDirection * (moveSpeed * SpeedMultiplier);
        Vector3 horizontal = Flat(rb.velocity);
        Vector3 next = Vector3.MoveTowards(horizontal, desired, acceleration * fixedDeltaTime);

        // Added on top of the steering rather than blended into it, because it has to survive being
        // overwritten -- see ComputeDepenetration.
        next += ComputeDepenetration(fixedDeltaTime);

        rb.velocity = new Vector3(next.x, rb.velocity.y, next.z);
    }

    /// <summary>
    /// Velocity that clears any overlap the body is currently in, against other entities and world
    /// geometry alike.
    ///
    /// This is needed because movement assigns <c>rb.velocity</c> outright every physics step, which
    /// silently throws away the separating velocity PhysX had just computed to push the body out of
    /// whatever it was intersecting. That is what let two entities grind into one another and stand
    /// there overlapping -- walking animation playing, visibly stuck inside each other -- and what
    /// let a body wedge itself into a wall and stay there. Steering alone can't fix it: by the time
    /// bodies are interpenetrating, the thing that resolves it is being cancelled every step.
    ///
    /// Measured from the real capsule via ComputePenetration rather than from centre distances, so
    /// it accounts for this rig's laterally-offset collider and whichever way each body is facing.
    /// </summary>
    private Vector3 ComputeDepenetration(float fixedDeltaTime)
    {
        if (capsule == null || fixedDeltaTime <= 0f)
            return Vector3.zero;

        GetCapsuleEnds(out var point0, out var point1, out float worldRadius);

        int count = Physics.OverlapCapsuleNonAlloc(point0, point1, worldRadius, overlapBuffer,
                                                   depenetrationMask, QueryTriggerInteraction.Ignore);

        Vector3 push = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            var other = overlapBuffer[i];
            if (other == null || other == capsule || other.transform.IsChildOf(transform))
                continue;

            if (!Physics.ComputePenetration(
                    capsule, capsule.transform.position, capsule.transform.rotation,
                    other, other.transform.position, other.transform.rotation,
                    out var direction, out float distance))
            {
                continue;
            }

            // Horizontal only: vertical separation is the ground holding the body up, and cancelling
            // gravity here would leave it hovering.
            push += Flat(direction) * (distance / fixedDeltaTime);
        }

        return Vector3.ClampMagnitude(push, maxDepenetrationSpeed);
    }

    private void GetCapsuleEnds(out Vector3 point0, out Vector3 point1, out float worldRadius)
    {
        var scale = capsule.transform.lossyScale;
        float radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        worldRadius = capsule.radius * radialScale;

        float height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), worldRadius * 2f);
        float halfSpan = Mathf.Max(0f, height * 0.5f - worldRadius);

        Vector3 center = capsule.transform.TransformPoint(capsule.center);
        Vector3 axis = capsule.direction == 0 ? capsule.transform.right
                     : capsule.direction == 2 ? capsule.transform.forward
                     : capsule.transform.up;

        point0 = center + axis * halfSpan;
        point1 = center - axis * halfSpan;
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
        print("jump!!!");
        Vector3 origin = transform.position;

        // Same solver the link bake validated the arc with, so what gets flown is what was approved.
        JumpArc.Solve(origin, target, out var velocity, out jumpDuration);
        jumpElapsed = 0f;

        rb.velocity = velocity;
        isJumping = true;

        Vector3 flatDelta = Flat(target - origin);
        MoveDirection = flatDelta.sqrMagnitude > 0.0001f ? flatDelta.normalized : MoveDirection;
    }

    /// <summary>
    /// Hops sideways, out of the line of an incoming shot. Kept here rather than in the strategy so
    /// it goes through the same rigidbody the rest of movement drives, and so it can take priority
    /// over steering for its duration.
    /// </summary>
    public void Dodge(Vector3 worldDirection)
    {
        Vector3 flat = Flat(worldDirection);
        if (flat.sqrMagnitude < 0.0001f)
            return;

        dodgeVelocity = flat.normalized * dodgeSpeed;
        dodgeRemaining = dodgeDuration;
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
    /// Push-away steer from every other live enemy within the pair's combined physical radius plus
    /// margin, strongest at zero distance and fading to nothing at that effective radius. Not
    /// normalized to a unit vector -- its magnitude relative to the path direction (always length 1)
    /// is what lets a near-collision dominate the heading while a distant neighbour barely nudges it.
    /// </summary>
    private Vector3 ComputeSeparation()
    {
        if (avoidanceStrength <= 0f)
            return Vector3.zero;

        Vector3 push = Vector3.zero;
        var active = EnemyManager.Active;
        for (int i = 0; i < active.Count; i++)
        {
            var other = active[i];
            if (other == null || other == enemy || !other.IsAlive)
                continue;

            var otherMovement = other.Movement;
            if (otherMovement == null)
                continue;

            float radius = PhysicalRadius + otherMovement.PhysicalRadius + avoidanceMargin;
            if (radius <= 0f)
                continue;

            Vector3 offset = Flat(transform.position - other.transform.position);
            float distance = offset.magnitude;
            if (distance >= radius || distance < 0.0001f)
                continue;

            float weight = 1f - distance / radius;
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
        hasPendingLink = false;

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

        // PathPartial means walking gets us as close as the NavMesh allows and no further -- which is
        // exactly what happens when the target has gone somewhere only a climb can reach, since the
        // bake cannot generate an upward link (see JumpLinkMap). Re-route to the takeoff of the
        // nearest link that makes progress, and jump from there.
        if (path.status == NavMeshPathStatus.PathPartial && linkCooldownRemaining <= 0f &&
            JumpLinkMap.Instance != null &&
            JumpLinkMap.Instance.TryFindLink(transform.position, destination, out var link))
        {
            var linkPath = new NavMeshPath();
            if (NavMesh.SamplePosition(link.takeoff, out var takeoffHit, navSampleDistance, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(fromHit.position, takeoffHit.position, NavMesh.AllAreas, linkPath) &&
                linkPath.status == NavMeshPathStatus.PathComplete && linkPath.corners.Length > 0)
            {
                pendingLink = link;
                hasPendingLink = true;
                path = linkPath;
            }
        }

        corners.AddRange(path.corners);

        // A corner-to-corner segment that crosses an off-mesh link (auto-generated at bake time for
        // drops/gaps within the configured Drop Height / Jump Distance) can't actually be walked in
        // a straight line -- NavMesh.Raycast is the tool built specifically to answer "can I walk
        // straight from A to B on this mesh", and it operates on the mesh's own surface rather than
        // Euclidean 3D distance, so it isn't fooled by a slope or an uneven stretch of ground the
        // way sampling a single Lerp'd midpoint was: a long segment's linearly-interpolated Y very
        // often doesn't track the real terrain height beneath it, so a plain sloped path was
        // wrongly flagged as "over empty space" and turned into a phantom jump.
        //
        // A blocked raycast alone isn't sufficient, though: this level is built from many separate
        // floor/wall pieces that sit at the same height but aren't quite welded together, so their
        // shared edge bakes as two disconnected NavMesh islands bridged by a trivial auto-link --
        // technically a "jump" by the same test, but a height difference the agent could just step
        // over (minJumpHeight is set with real margin above the bake's own agentClimb). Requiring a
        // real height difference is what tells an actual gap apart from a seam.
        for (int i = 0; i < corners.Count; i++)
        {
            bool isJump = i > 0
                && Mathf.Abs(corners[i].y - corners[i - 1].y) >= minJumpHeight
                && NavMesh.Raycast(corners[i - 1], corners[i], out _, NavMesh.AllAreas);
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
