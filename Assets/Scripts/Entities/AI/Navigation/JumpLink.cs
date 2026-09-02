using UnityEngine;

/// <summary>
/// One baked crossing between two walkable surfaces, and the arc that joins them.
///
/// There is one kind of link. Climbing and dropping are not two sorts of record sitting side by side
/// -- they are the same curve travelled in opposite directions, which is why the arc is stored once
/// and each direction merely asks what it costs to enter. Storing them separately is what used to
/// make a single crossing appear twice in the graph and in two colours on screen.
///
/// The arc is *stored*, not re-derived. What a class can make is decided by comparing its own limits
/// against <see cref="RequiredJumpHeight"/> and <see cref="launchForward"/>, so a link is either in
/// the graph with a known cost or it is not. Re-solving per class silently lost links whenever the
/// class differed from the envelope the bake had validated against.
/// </summary>
[System.Serializable]
public struct JumpLink
{
    /// <summary>Where the stored arc starts.</summary>
    public Vector3 takeoff;

    /// <summary>Where the stored arc ends. Travelling the link backwards swaps the two.</summary>
    public Vector3 landing;

    /// <summary>Height gained going forward. Negative means the stored direction is a descent.</summary>
    public float rise;

    /// <summary>Flat (XZ) span, the same either way round.</summary>
    public float horizontalDistance;

    /// <summary>Upward launch speed of the stored arc.</summary>
    public float launchUp;

    /// <summary>Horizontal launch speed. Identical in both directions -- only its sign flips.</summary>
    public float launchForward;

    /// <summary>Flight time. Identical in both directions: a reversed arc takes exactly as long.</summary>
    public float duration;

    /// <summary>Index of <see cref="takeoff"/> in the graph's node list. Assigned by the bake.</summary>
    public int takeoffNode;

    /// <summary>Index of <see cref="landing"/> in the graph's node list. Assigned by the bake.</summary>
    public int landingNode;

    private static float Gravity => Mathf.Abs(Physics.gravity.y);

    /// <summary>How high a jumper must be able to leap to fly this arc forward.</summary>
    public readonly float RequiredJumpHeight => launchUp * launchUp / (2f * Gravity);

    /// <summary>
    /// How high a jumper must be able to leap to fly it backwards. Always the smaller of the two for
    /// a climb: coming back down you only need the speed the climb arrived with, which is why a ledge
    /// you can barely get onto is trivial to step off.
    /// </summary>
    public readonly float ReverseRequiredJumpHeight
    {
        get
        {
            float up = JumpArc.ReverseLaunchUp(launchUp, duration);
            return up * up / (2f * Gravity);
        }
    }

    /// <summary>Launch velocity for the stored direction.</summary>
    public readonly Vector3 ForwardVelocity => Heading(takeoff, landing) * launchForward + Vector3.up * launchUp;

    /// <summary>Launch velocity for the same arc flown backwards.</summary>
    public readonly Vector3 ReverseVelocity =>
        Heading(landing, takeoff) * launchForward + Vector3.up * JumpArc.ReverseLaunchUp(launchUp, duration);

    public readonly Vector3 From(bool reverse) => reverse ? landing : takeoff;
    public readonly Vector3 To(bool reverse) => reverse ? takeoff : landing;
    public readonly Vector3 VelocityFor(bool reverse) => reverse ? ReverseVelocity : ForwardVelocity;

    /// <summary>How far this direction descends, or zero when it climbs.</summary>
    public readonly float FallFor(bool reverse) => Mathf.Max(0f, reverse ? rise : -rise);

    /// <summary>How high a jumper must leap to take this direction.</summary>
    public readonly float RequiredJumpHeightFor(bool reverse) =>
        reverse ? ReverseRequiredJumpHeight : RequiredJumpHeight;

    private static Vector3 Heading(Vector3 from, Vector3 to)
    {
        Vector3 flat = new(to.x - from.x, 0f, to.z - from.z);
        return flat.sqrMagnitude > 0.0000001f ? flat.normalized : Vector3.zero;
    }

    /// <summary>Builds a link around an already-solved arc. Node indices are assigned later, by the bake.</summary>
    public static JumpLink Create(Vector3 takeoff, Vector3 landing,
                                  float launchUp, float launchForward, float duration)
    {
        return new JumpLink
        {
            takeoff = takeoff,
            landing = landing,
            rise = landing.y - takeoff.y,
            horizontalDistance = new Vector2(landing.x - takeoff.x, landing.z - takeoff.z).magnitude,
            launchUp = launchUp,
            launchForward = launchForward,
            duration = duration,
            takeoffNode = -1,
            landingNode = -1,
        };
    }
}
