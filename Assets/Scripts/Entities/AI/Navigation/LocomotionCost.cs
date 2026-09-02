using UnityEngine;

/// <summary>
/// How fast one entity actually covers ground, in the terms route costs are measured in.
/// </summary>
public readonly struct LocomotionProfile
{
    /// <summary>Cruising speed, already scaled by whatever multiplier the current action is using.</summary>
    public readonly float moveSpeed;

    /// <summary>
    /// Rate the horizontal velocity ramps at. Matches the <c>Vector3.MoveTowards(velocity, desired,
    /// acceleration * dt)</c> the mover actually applies, so the cost model predicts the same motion
    /// the entity will really produce rather than an idealised one.
    /// </summary>
    public readonly float acceleration;

    /// <summary>Dead time spent recovering on touchdown, on top of the arc's own flight time.</summary>
    public readonly float landingRecovery;

    public LocomotionProfile(float moveSpeed, float acceleration, float landingRecovery)
    {
        this.moveSpeed = Mathf.Max(0.01f, moveSpeed);
        this.acceleration = Mathf.Max(0f, acceleration);
        this.landingRecovery = Mathf.Max(0f, landingRecovery);
    }
}

/// <summary>
/// Route cost in **seconds**, not meters.
///
/// Distance is the wrong unit to choose a route with the moment jumping is on the table. A 4m jump
/// with a 2m climb is roughly 1.4 seconds of flight; running the same 4m at 7 m/s takes 0.57. Judged
/// in meters those two are identical and the jump looks like a bargain, which is exactly how the
/// previous chord-length comparison ended up preferring jumps that were plainly slower than walking
/// round. Measuring both in time -- flight time for the arc, ramp-aware travel time for the ground --
/// makes "jump or go around" a comparison of the same quantity, and is what the request for distance
/// that accounts for jumps, falls and acceleration actually needs.
///
/// Every function here is closed-form, because these run inside the A* inner loop.
/// </summary>
public static class LocomotionCost
{
    /// <summary>
    /// Seconds to cover <paramref name="distance"/> starting at <paramref name="entrySpeed"/>.
    ///
    /// Constant acceleration up to cruise, then constant speed. Solving
    /// <c>d = v0*t + a*t^2/2</c> for the ramp and adding the cruise remainder is what makes a route
    /// that stops and restarts cost more than one that keeps its momentum -- the whole reason
    /// acceleration is in the model at all.
    /// </summary>
    public static float GroundTime(float distance, float entrySpeed, in LocomotionProfile profile)
    {
        if (distance <= 0f)
            return 0f;

        float cruise = profile.moveSpeed;
        float v0 = Mathf.Clamp(entrySpeed, 0f, cruise);
        float a = profile.acceleration;

        // No ramp configured: the mover reaches cruise the instant it is asked to.
        if (a <= 0.01f)
            return distance / cruise;

        float rampDistance = (cruise * cruise - v0 * v0) / (2f * a);
        if (distance <= rampDistance)
            return (-v0 + Mathf.Sqrt(v0 * v0 + 2f * a * distance)) / a;

        return (cruise - v0) / a + (distance - rampDistance) / cruise;
    }

    /// <summary>Speed the entity is carrying once it has covered <paramref name="distance"/>. Feeds the next segment.</summary>
    public static float GroundExitSpeed(float distance, float entrySpeed, in LocomotionProfile profile)
    {
        float cruise = profile.moveSpeed;
        float v0 = Mathf.Clamp(entrySpeed, 0f, cruise);

        if (profile.acceleration <= 0.01f || distance <= 0f)
            return profile.acceleration <= 0.01f ? cruise : v0;

        return Mathf.Min(cruise, Mathf.Sqrt(v0 * v0 + 2f * profile.acceleration * distance));
    }

    /// <summary>
    /// Seconds a jump costs: the arc's real flight time plus touchdown recovery.
    ///
    /// <paramref name="exitSpeed"/> comes back as the horizontal speed the arc lands with, which is
    /// usually well under cruising speed -- a jump does not merely take time, it also dumps the
    /// momentum the following ground segment then has to rebuild. Threading it through is what stops
    /// a chain of short hops from costing the same as one uninterrupted run. It is the same figure
    /// either way round the link is travelled: a reversed arc keeps the horizontal speed and the
    /// flight time of the original, only flipping its heading.
    /// </summary>
    public static float JumpTime(in JumpLink link, in LocomotionProfile profile, out float exitSpeed)
    {
        // Read from the baked arc rather than solved again. The two used to be separate calculations
        // that could disagree, and did.
        exitSpeed = Mathf.Min(profile.moveSpeed, link.launchForward);
        return link.duration + profile.landingRecovery;
    }

    /// <summary>Total time along a corner list, carrying momentum from corner to corner.</summary>
    public static float GroundTime(Vector3[] corners, int count, float entrySpeed,
                                   in LocomotionProfile profile, out float exitSpeed)
    {
        exitSpeed = entrySpeed;
        float total = 0f;

        for (int i = 1; i < count; i++)
        {
            float segment = Vector3.Distance(corners[i - 1], corners[i]);
            total += GroundTime(segment, exitSpeed, profile);
            exitSpeed = GroundExitSpeed(segment, exitSpeed, profile);
        }

        return total;
    }
}
