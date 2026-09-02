using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Moves the entity to a destination. Routes come from <see cref="JumpPathfinder"/> -- the NavMesh
/// plus the baked jump graph -- but the movement itself is hand-rolled on the Rigidbody rather than
/// delegated to a NavMeshAgent: an agent would take over the transform and fight the physics the rest
/// of the game (knockback, ragdolls, slopes) is built on. So pathfinding is used purely as a route
/// planner: it hands us a waypoint list, some of which are launch points, and we steer along it
/// ourselves.
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
    [Tooltip("How close the entity must get to a waypoint before steering to the next one.")]
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
    [Tooltip("Take the jump envelope from this component instead of the EnemyConfig. For tuning one " +
             "instance in a scene -- leave off so a whole class stays tuned in one asset.")]
    [SerializeField] private bool overrideJumpSettings;

    [Tooltip("How high this entity can raise itself, which is also the tallest climb it can make. " +
             "Together with the move speed above it decides everything else about a jump -- impulse, " +
             "flight time, reach -- so there is nothing else here to keep in sync with it.")]
    [SerializeField] private float maxJumpUpHeight = 3f;
    [Tooltip("Furthest down this entity is willing to leap. The one jump limit that is a choice: it " +
             "could always fall further.")]
    [SerializeField] private float maxDropHeight = 5f;
    [Tooltip("Dead time a route is charged for every touchdown, on top of the arc's flight time.")]
    [SerializeField] private float landingRecovery = 0.25f;

    [Tooltip("Safety cap on a jump's flight time, in case the computed arc math ever degenerates.")]
    [SerializeField] private float jumpMaxDuration = 3f;

    [Tooltip("How close to a launch point the entity must be before it commits to the arc. Tighter " +
             "than an ordinary corner because the arc was solved for that exact spot: starting it " +
             "half a metre short lengthens the flight by the same amount, which is enough to land " +
             "short of a spot the bake had cleared comfortably.")]
    [SerializeField] private float jumpTakeoffTolerance = 0.25f;

    [Tooltip("Shortest time an arc stays airborne before ground contact is even tested, so the probe " +
             "doesn't see the ledge it just left and end the jump on the frame it started.")]
    [SerializeField] private float minAirTime = 0.15f;

    [Tooltip("How long past the arc's predicted flight time to keep waiting for real ground contact " +
             "before giving up and calling it landed anyway.")]
    [SerializeField] private float landingGrace = 0.5f;

    [Tooltip("How far from the intended landing the entity may actually touch down before the route " +
             "is considered broken and replanned. An arc deflected by geometry leaves the body " +
             "somewhere the rest of the route knows nothing about.")]
    [SerializeField] private float landingTolerance = 1.5f;

    [Header("Dodging")]
    [Tooltip("How fast the sideways hop off an incoming shot is.")]
    [SerializeField] private float dodgeSpeed = 9f;
    [Tooltip("How long the dodge drives movement before normal steering resumes.")]
    [SerializeField] private float dodgeDuration = 0.35f;

    [Header("Depenetration")]
    [Tooltip("What the body is pushed back out of when it ends up inside something: other entities " +
             "and world geometry. Also what a landing jump probes for ground.")]
    [SerializeField] private LayerMask depenetrationMask = ~0;

    [Tooltip("Cap on how fast overlap is resolved, so a deep intersection doesn't fling the body.")]
    [SerializeField] private float maxDepenetrationSpeed = 6f;

    private readonly Collider[] overlapBuffer = new Collider[16];

    private float dodgeRemaining;
    private Vector3 dodgeVelocity;

    /// <summary>True while a dodge hop is driving movement. Read by the animation system.</summary>
    public bool IsDodging => dodgeRemaining > 0f;

    private readonly JumpRoute route = new();
    private int waypointIndex;

    private bool isJumping;
    private float jumpElapsed;
    private float jumpDuration;
    private Vector3 jumpTarget;

    private JumpCapability capability;

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

    /// <summary>How many jumps the current route plans to make. Zero means it is walking the whole way.</summary>
    public int RouteJumpCount => route.JumpCount;

    /// <summary>Seconds the current route is predicted to take -- the figure it beat the plain walk on.</summary>
    public float RouteEstimatedTime => route.EstimatedTime;

    /// <summary>How far the last touchdown missed its intended landing spot by.</summary>
    public float LastLandingError { get; private set; }

    /// <summary>
    /// Distance still to travel, measured along the route rather than straight to the destination.
    ///
    /// The straight-line version this replaces was flat (XZ only), so a target five metres overhead
    /// read as already arrived, and any detour around a wall was invisible to every caller asking
    /// "am I nearly there". Following the actual waypoints counts the climbs, the drops and the way
    /// round -- which is the point of routing through jumps in the first place.
    /// </summary>
    public float DistanceToDestination
    {
        get
        {
            if (!HasDestination)
                return 0f;

            if (route.IsValid && waypointIndex < route.Count)
                return route.RemainingDistance(transform.position, waypointIndex);

            return Vector3.Distance(Flat(transform.position), Flat(destination));
        }
    }

    public bool ReachedDestination => HasDestination && DistanceToDestination <= stoppingDistance;

    private LocomotionProfile Profile =>
        new(moveSpeed * SpeedMultiplier, acceleration, landingRecovery);

    private static bool jumpAreaCostFixed;

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

        RebuildCapability();
        WarnIfGraphTooSmall();

        // Spread the first repath across entities. Three enemies initialised on the same frame would
        // otherwise stay in lockstep forever, stacking every route solve onto the same physics step.
        repathTimer = Random.Range(0f, repathInterval);

        // The bake's "Jump" area carries a 2x cost multiplier (ProjectSettings/NavMeshAreas.asset),
        // which biases NavMesh.CalculatePath toward walking around a native drop/across link even
        // when the jump is the shorter route. Route choice should come from real travel time -- the
        // basis JumpPathfinder judges everything on -- not an arbitrary per-area multiplier, so it's
        // neutralized here rather than left to silently outvote a genuinely shorter route.
        if (!jumpAreaCostFixed)
        {
            jumpAreaCostFixed = true;
            int jumpArea = NavMesh.GetAreaFromName("Jump");
            if (jumpArea >= 0)
                NavMesh.SetAreaCost(jumpArea, 1f);
        }
    }

    public void ConfigureFrom(EnemyConfig config)
    {
        if (config == null) return;
        moveSpeed = config.moveSpeed;
        acceleration = config.acceleration;
        stoppingDistance = config.stoppingDistance;
        avoidanceMargin = config.avoidanceMargin;
        avoidanceStrength = config.avoidanceStrength;

        if (!overrideJumpSettings)
        {
            maxJumpUpHeight = config.maxJumpUpHeight;
            maxDropHeight = config.maxDropHeight;
            landingRecovery = config.landingRecovery;
        }

        RebuildCapability();
    }

    private void RebuildCapability()
    {
        // Run speed is the jump's horizontal reach, so the capability has to be rebuilt whenever the
        // configured speed changes -- not merely when a jump setting does.
        capability = new JumpCapability(maxJumpUpHeight, maxDropHeight, moveSpeed);
    }

    /// <summary>
    /// Says so when this entity believes it can jump further than the level was baked for. Nothing
    /// breaks -- it simply never finds the links it thinks it could make, and quietly routes the long
    /// way round -- which is precisely the kind of silent under-performance worth a line in the log.
    /// </summary>
    private void WarnIfGraphTooSmall()
    {
        var graph = JumpLinkMap.Instance != null ? JumpLinkMap.Instance.Graph : null;
        if (graph == null || graph.IsEmpty)
            return;

        if (!graph.Covers(capability, out string shortfall))
            Debug.LogWarning($"[{name}] {shortfall}. Rebake the jump graph with a wider envelope.", this);
    }

    public void SetDestination(Vector3 worldPosition)
    {
        destination = worldPosition;
        HasDestination = true;
        repathTimer = repathInterval;
        RecalculatePath();
    }

    public void Stop()
    {
        HasDestination = false;
        MoveDirection = Vector3.zero;
        route.Clear();
        waypointIndex = 0;
        isJumping = false;
    }

    public override void FixedTickSystem(float fixedDeltaTime)
    {
        if (rb == null) return;

        // A jump commits to its arc once launched -- no steering, no repathing -- same as a real jump
        // can't change its mind partway through.
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

        // Committed to an animation that owns the body -- firing, posturing, talking, drawing. The
        // destination is kept, so walking resumes where it left off once the action finishes; only
        // the steering stops. Depenetration still runs via Decelerate, so a locked body can still be
        // pushed out of something it's stuck in.
        if (enemy != null && enemy.Animation != null && enemy.Animation.MovementLocked)
        {
            MoveDirection = Vector3.zero;
            Decelerate(fixedDeltaTime);
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

        // Standing on a launch point? Fly it. Whether this is a climb onto a ledge or a leap down off
        // one is settled by the arc itself -- the mover treats both the same, because both are jumps.
        //
        // The tolerance is tighter than an ordinary corner's on purpose: the arc was solved for the
        // exact launch spot, and starting it from half a metre short stretches every jump by that
        // much, which is enough to fall short of a landing the bake had comfortably cleared.
        if (waypointIndex < route.Count && route[waypointIndex].jumpFromHere &&
            Flat(route[waypointIndex].position - transform.position).magnitude <= jumpTakeoffTolerance)
        {
            BeginJump(route[waypointIndex]);
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
    /// Launches the arc the route is carrying: the velocity was solved when the crossing was baked
    /// and validated against the geometry then, so it is applied here as-is rather than worked out
    /// again. Gravity (already acting on the Rigidbody every step, same as normal ground movement
    /// only ever touching the horizontal component of velocity) carries it the rest of the way. No
    /// steering during the arc -- a real jump commits the moment it leaves the ground.
    ///
    /// Re-solving from wherever the entity happens to stand was the previous behaviour and it could
    /// fail, which meant walking to a ledge, declining to jump, replanning, and walking to the same
    /// ledge again -- forever, with no jump ever made. Flying the stored arc cannot fail; the arrival
    /// tolerance below is what keeps it honest instead.
    /// </summary>
    private void BeginJump(in JumpRoute.Waypoint launchPoint)
    {
        jumpElapsed = 0f;
        jumpDuration = launchPoint.flightTime;
        jumpTarget = launchPoint.landingPoint;

        rb.velocity = launchPoint.launchVelocity;
        isJumping = true;

        // The launch point is behind us now; the landing is the waypoint being flown to.
        waypointIndex++;

        Vector3 flatDelta = Flat(launchPoint.launchVelocity);
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

    /// <summary>
    /// Watches a jump for real ground contact, not just the clock.
    ///
    /// The arc's own math predicts when it should land, but nothing guarantees it does: a clipped
    /// lip, a body knocked aside, a landing surface that isn't quite where the bake sampled it. Ending
    /// the jump on the timer alone meant the route advanced as though the planned landing had been
    /// reached, while the body was somewhere else entirely -- every waypoint after that was then being
    /// steered at from the wrong place. Ground contact ends the jump; the predicted duration (plus
    /// grace) only backs it up, and a touchdown too far from where it was aimed replans instead of
    /// pretending.
    /// </summary>
    private void TickJump(float fixedDeltaTime)
    {
        jumpElapsed += fixedDeltaTime;

        bool grounded = jumpElapsed >= minAirTime && rb.velocity.y <= 0.01f && IsGrounded();
        bool overdue = jumpElapsed >= jumpDuration + landingGrace;
        bool timedOut = jumpElapsed >= jumpMaxDuration;

        if (!grounded && !overdue && !timedOut)
            return;

        isJumping = false;
        LastLandingError = Vector3.Distance(transform.position, jumpTarget);

        if (LastLandingError > landingTolerance)
        {
            // Wherever we ended up, the rest of the route was planned from somewhere else.
            repathTimer = repathInterval;
            RecalculatePath();
            return;
        }

        // Landed where intended: consume the landing waypoint and carry on, possibly straight into
        // the next jump if the route chains them.
        waypointIndex++;
    }

    /// <summary>
    /// Whether something solid is directly underfoot. Probes just below the capsule's bottom tip and
    /// skips this entity's own colliders -- an overlap test centred on the capsule would otherwise
    /// always find itself and report a landing on the frame the jump began.
    /// </summary>
    private bool IsGrounded()
    {
        if (capsule == null)
            return false;

        GetCapsuleEnds(out var point0, out var point1, out float worldRadius);
        Vector3 bottomTip = (point0.y < point1.y ? point0 : point1) + Vector3.down * worldRadius;

        float probeRadius = Mathf.Max(0.1f, worldRadius * 0.35f);
        Vector3 probe = bottomTip + Vector3.down * (probeRadius * 0.5f);

        int count = Physics.OverlapSphereNonAlloc(probe, probeRadius, overlapBuffer,
                                                  depenetrationMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            var other = overlapBuffer[i];
            if (other == null || other == capsule || other.transform.IsChildOf(transform))
                continue;

            return true;
        }

        return false;
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

    /// <summary>
    /// Comes to a stop, while still clearing any overlap. Standing still is exactly when a body most
    /// needs pushing out of whatever it is inside -- with no steering to mask it, an entity wedged
    /// into a wall or a squadmate would otherwise simply stay there.
    /// </summary>
    private void Decelerate(float fixedDeltaTime)
    {
        Vector3 horizontal = Flat(rb.velocity);
        Vector3 next = Vector3.MoveTowards(horizontal, Vector3.zero, acceleration * fixedDeltaTime);
        next += ComputeDepenetration(fixedDeltaTime);
        rb.velocity = new Vector3(next.x, rb.velocity.y, next.z);
    }

    /// <summary>
    /// Current waypoint to steer at, advancing through the route as waypoints are reached. A launch
    /// point is never consumed by proximity -- reaching one is the trigger to jump, and swallowing it
    /// here would leave the entity walking off a ledge it was supposed to leap from. With no usable
    /// route this degrades to steering straight at the destination, which keeps the AI functional in
    /// a scene whose NavMesh hasn't been baked (just without obstacle avoidance).
    /// </summary>
    private Vector3 NextSteerTarget()
    {
        while (waypointIndex < route.Count &&
               !route[waypointIndex].jumpFromHere &&
               Vector3.Distance(Flat(transform.position), Flat(route[waypointIndex].position)) <= cornerReachDistance)
        {
            waypointIndex++;
        }

        return waypointIndex < route.Count ? route[waypointIndex].position : destination;
    }

    private void RecalculatePath()
    {
        route.Clear();
        waypointIndex = 0;

        // Both ends have to be on the NavMesh for pathfinding to return anything useful; entities
        // stand slightly above it and destinations are often a live target's exact position, so both
        // get snapped first.
        if (!NavMesh.SamplePosition(transform.position, out var fromHit, navSampleDistance, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(destination, out var toHit, navSampleDistance, NavMesh.AllAreas))
        {
            WarnMissingNavMeshOnce();
            return;
        }

        var graph = JumpLinkMap.Instance != null ? JumpLinkMap.Instance.Graph : null;
        float carriedSpeed = Flat(rb.velocity).magnitude;

        JumpPathfinder.Shared.TrySolve(graph, fromHit.position, toHit.position,
                                       capability, Profile, carriedSpeed, route);

        // The first waypoint is the entity's own position; steering at it would stall the first step.
        // Unless it is a launch point -- then it is a jump to start immediately, not a corner to skip.
        if (route.Count > 1 && !route[0].jumpFromHere)
            waypointIndex = 1;
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
