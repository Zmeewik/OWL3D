using UnityEngine;

/// <summary>
/// The ballistic arc an entity flies when it jumps. <see cref="JumpLinkMap"/> solves one per link
/// while baking and tests it against geometry; <see cref="EnemyMovementSystem"/> launches the
/// rigidbody along the very same stored arc at runtime. Solving it once and storing it is what stops
/// the bake from approving a curve the mover then flies differently -- which is exactly what happened
/// while the arc was re-derived per class: a graph baked for a ten-metre jumper handed a four-metre
/// one links whose flight time it could never buy.
///
/// There is nothing to tune. A jump is two numbers -- how hard the legs push up and how fast the body
/// is already moving -- and both come from what the entity already has: its maximum jump height and
/// its run speed.
///
/// Arcs are symmetric in time, and that is load-bearing here rather than a curiosity: a descent is
/// not a second kind of jump needing its own rules, it is a climb flown backwards along the identical
/// curve, in the identical flight time. One link therefore serves both directions.
/// </summary>
public static class JumpArc
{
    /// <summary>
    /// The cheapest arc joining the two points: the least upward impulse that still gets there.
    ///
    /// The shortest flight possible is the one flown at full run speed, and a shorter flight needs a
    /// smaller impulse -- so the fastest crossing is also the cheapest one, and the minimum falls
    /// straight out of <c>t = span / runSpeed</c>. Taking the minimum rather than always launching at
    /// full power is what keeps a small step looking like a small step.
    /// </summary>
    /// <param name="requiredJumpHeight">
    /// How high the jumper has to be able to leap for this arc, <c>v↑²/2g</c>. The caller compares it
    /// to what a class can actually do; it is also where a bake sweep starts when the minimum arc
    /// turns out to clip geometry.
    /// </param>
    public static bool TrySolveMinimum(Vector3 takeoff, Vector3 landing, float maxRunSpeed,
                                       out float launchUp, out float launchForward, out float duration,
                                       out float requiredJumpHeight)
    {
        launchUp = 0f;
        launchForward = 0f;
        duration = 0f;
        requiredJumpHeight = 0f;

        float gravity = Mathf.Abs(Physics.gravity.y);
        if (gravity <= 0.0001f)
            return false;

        Vector3 flatDelta = new(landing.x - takeoff.x, 0f, landing.z - takeoff.z);
        float span = flatDelta.magnitude;
        float rise = landing.y - takeoff.y;

        // Straight up or down: with no horizontal travel there is no run speed to set the clock, so
        // the flight is whatever the impulse alone produces.
        if (span <= 0.0001f)
        {
            if (rise < 0f)
            {
                duration = Mathf.Sqrt(-2f * rise / gravity);
                return true;
            }

            launchUp = Mathf.Sqrt(2f * gravity * rise);
            duration = launchUp / gravity;
            requiredJumpHeight = rise;
            return duration > 0.0001f;
        }

        float fastestCrossing = span / Mathf.Max(0.01f, maxRunSpeed);

        // Solving rise = v↑*t - g*t²/2 for the impulse that makes the flight last exactly that long.
        launchUp = (rise + 0.5f * gravity * fastestCrossing * fastestCrossing) / fastestCrossing;

        if (launchUp <= 0f)
        {
            // The drop is steep enough that falling already takes longer than the run needs. No
            // impulse at all then: step off the edge and let gravity set the clock.
            launchUp = 0f;
            duration = Mathf.Sqrt(-2f * rise / gravity);
        }
        else
        {
            duration = fastestCrossing;
        }

        if (duration <= 0.0001f)
            return false;

        launchForward = span / duration;
        requiredJumpHeight = launchUp * launchUp / (2f * gravity);
        return true;
    }

    /// <summary>
    /// The arc flown with a *given* upward impulse rather than the least one that works. Used by the
    /// bake to lift an arc over a ledge lip the minimum solution clipped, and nowhere else -- runtime
    /// never re-solves anything.
    /// </summary>
    public static bool TrySolveAt(Vector3 takeoff, Vector3 landing, float jumpHeight, float maxRunSpeed,
                                  out float launchUp, out float launchForward, out float duration)
    {
        launchUp = 0f;
        launchForward = 0f;
        duration = 0f;

        float gravity = Mathf.Abs(Physics.gravity.y);
        if (gravity <= 0.0001f)
            return false;

        Vector3 flatDelta = new(landing.x - takeoff.x, 0f, landing.z - takeoff.z);
        float span = flatDelta.magnitude;
        float rise = landing.y - takeoff.y;

        launchUp = Mathf.Sqrt(2f * gravity * Mathf.Max(0f, jumpHeight));

        // A negative discriminant means the arc never reaches that height at all, which happens
        // exactly when the climb exceeds the impulse. The height limit is not a rule laid on top of
        // the physics; it is the physics.
        float discriminant = launchUp * launchUp - 2f * gravity * rise;
        if (discriminant < 0f)
            return false;

        // The later root: the entity lands on the way down rather than clipping the spot on its way up.
        duration = (launchUp + Mathf.Sqrt(discriminant)) / gravity;
        if (duration <= 0.0001f)
            return false;

        launchForward = span / duration;
        return launchForward <= maxRunSpeed + 0.0001f;
    }

    /// <summary>
    /// Upward impulse needed to fly a stored arc backwards: the speed the forward arc *arrives* with.
    /// Launching at exactly that retraces the identical curve in the identical time, which is why a
    /// descent needs no arc of its own.
    /// </summary>
    public static float ReverseLaunchUp(float launchUp, float duration)
    {
        return Mathf.Abs(launchUp - Mathf.Abs(Physics.gravity.y) * duration);
    }

    /// <summary>Position along an arc <paramref name="time"/> seconds after launch.</summary>
    public static Vector3 Sample(Vector3 takeoff, Vector3 velocity, float time)
    {
        float gravity = Mathf.Abs(Physics.gravity.y);
        return takeoff + velocity * time + Vector3.up * (-0.5f * gravity * time * time);
    }

    /// <summary>
    /// Furthest flat distance any jump within an envelope can cover. The longest flight is the
    /// deepest drop taken at full impulse, so that is what sets the bound. Only used to size the
    /// bake's search neighbourhoods, which is the one thing that still wants a single number.
    /// </summary>
    public static float MaxReach(float maxJumpHeight, float maxRunSpeed, float maxDropHeight)
    {
        float gravity = Mathf.Abs(Physics.gravity.y);
        if (gravity <= 0.0001f)
            return 0f;

        float launchUp = Mathf.Sqrt(2f * gravity * Mathf.Max(0f, maxJumpHeight));
        float longestFlight = (launchUp + Mathf.Sqrt(launchUp * launchUp + 2f * gravity * Mathf.Max(0f, maxDropHeight)))
                              / gravity;

        return maxRunSpeed * longestFlight;
    }
}
