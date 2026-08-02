/// <summary>
/// Every procedural-camera cue broadcast by <see cref="PlayerMovement.OnLifeCameraAction"/>.
/// Replaces the old string-keyed cue names ("dash", "wallrun", "hangup", ...): a typo in a
/// string silently did nothing (see the removed `stateNames.Contains(state)` guard); a typo
/// in an enum member is a compile error instead.
/// </summary>
public enum LifeCameraCue
{
    None,
    Movement,
    Jump,
    Dash,
    Land,
    Slide,
    Wallrun,
    HangUp,
    Fall,
    FallCliff,
    LegHit,
}
