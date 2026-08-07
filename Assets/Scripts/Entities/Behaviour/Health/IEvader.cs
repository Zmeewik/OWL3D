/// <summary>
/// Something that can get out of the way of an incoming hit. <see cref="EntityHealth"/> asks before
/// it applies anything, so an evade genuinely prevents the hit rather than reacting to having
/// already taken it -- which is the whole point of a dodge.
/// </summary>
public interface IEvader
{
    /// <summary>
    /// Decides whether this hit is evaded, and starts the evasive move if so.
    ///
    /// A successful evade always cancels the flinch (the dodge animation is what should be visible,
    /// not a hit reaction) and, for a swing, cancels the damage outright -- you can duck a blade.
    /// A bullet is already there by the time anyone reacts, so evading one still hurts; the dodge
    /// just means the entity is elsewhere for whatever comes next.
    /// </summary>
    bool TryEvade(DamagePacket packet);
}
