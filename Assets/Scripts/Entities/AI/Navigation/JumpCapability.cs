using UnityEngine;

/// <summary>
/// What one entity is able to jump: how high it can leap, how far down it is willing to go, and how
/// fast it is moving when it leaves the ground.
///
/// Only <see cref="maxDropHeight"/> is a genuine choice. Climbing is bounded by the impulse itself,
/// and reach is that impulse spent at running speed -- neither needs stating, and stating them
/// separately only creates numbers free to contradict the physics. Falling further, on the other
/// hand, is always possible; gravity needs no permission, so where a class declines has to be said.
///
/// Every test here is a comparison against figures the bake already stored on the link. Nothing is
/// re-solved. That is deliberate: while capability re-derived each arc for itself, a class whose
/// jump differed from the bake's envelope quietly failed most links -- the graph looked full and the
/// entity would not jump.
/// </summary>
public readonly struct JumpCapability
{
    /// <summary>How high this entity can raise itself, which is also the tallest climb it can make.</summary>
    public readonly float maxJumpUpHeight;

    /// <summary>Furthest down this entity is willing to leap. A choice, not a limit.</summary>
    public readonly float maxDropHeight;

    /// <summary>Speed carried into the jump, which is what its horizontal reach is made of.</summary>
    public readonly float runSpeed;

    public JumpCapability(float maxJumpUpHeight, float maxDropHeight, float runSpeed)
    {
        this.maxJumpUpHeight = Mathf.Max(0f, maxJumpUpHeight);
        this.maxDropHeight = Mathf.Max(0f, maxDropHeight);
        this.runSpeed = Mathf.Max(0.01f, runSpeed);
    }

    /// <summary>
    /// Whether this entity can travel <paramref name="link"/> in the given direction. One test for
    /// both ways round: the direction changes which stored impulse is asked for and which end the
    /// fall is measured from, and nothing else.
    /// </summary>
    public bool Allows(in JumpLink link, bool reverse)
    {
        if (link.launchForward > runSpeed)
            return false;

        if (link.RequiredJumpHeightFor(reverse) > maxJumpUpHeight)
            return false;

        return link.FallFor(reverse) <= maxDropHeight;
    }

    /// <summary>True when at least one direction of the link is usable. Cheap pre-filter for candidate picking.</summary>
    public bool AllowsEither(in JumpLink link) => Allows(link, false) || Allows(link, true);

    /// <summary>Furthest flat distance any jump this entity can make will cover.</summary>
    public float MaxReach => JumpArc.MaxReach(maxJumpUpHeight, runSpeed, maxDropHeight);

    /// <summary>The envelope built from an enemy class's tuning asset.</summary>
    public static JumpCapability FromConfig(EnemyConfig config)
    {
        return new JumpCapability(config.maxJumpUpHeight, config.maxDropHeight, config.moveSpeed);
    }
}
