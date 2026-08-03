/// <summary>
/// Named constants for the command strings sent over <see cref="IAnimationSender.OnAnimateCommand"/>
/// (produced by WeaponBase.OnAnimationCall and EntityHealth.Animate).
///
/// This channel is NOT converted to an enum like <see cref="LifeCameraCue"/> or
/// <see cref="WeaponCommand"/>: its consumers genuinely depend on the string *shape*, not just
/// fixed values -- EntityAttackAnimation.HandleAnimations does `.EndsWith("_start")` /
/// `.Replace("_start", "_continue")` to relate a charge-start command to its matching
/// charge-end, and WeaponAnimationController.ChangeAnimationName does token substitution on the
/// weapon-name portion of the string. Forcing that into an enum would need a bigger redesign of
/// how those two classes relate commands to each other, which is out of scope here.
///
/// What an enum-shaped fix WOULD have caught -- a typo in a raw string literal compiling fine
/// and silently matching nothing -- is addressed on the producer side only: WeaponBase and
/// EntityHealth reference these constants instead of retyping the literals. The consumer side's
/// `EntityAttackAnimation.animationList` / `EntityAnimator.animationList` entries remain plain
/// Inspector-authored string fields (Unity serializes them as strings; there's no equivalent
/// compile-time link from designer data back to a C# constant), so matching them correctly is
/// still a naming convention, not something code alone can fully close.
/// </summary>
public static class AnimateCommand
{
    public const string LeftAttack = "left_attack";
    public const string RightAttack = "right_attack";
    public const string MiddleAttack = "middle_attack";

    public const string LeftAttackStart = "left_attack_start";
    public const string RightAttackStart = "right_attack_start";
    public const string MiddleAttackStart = "middle_attack_start";

    public const string LeftAttackEnd = "left_attack_end";
    public const string RightAttackEnd = "right_attack_end";
    public const string MiddleAttackEnd = "middle_attack_end";

    public const string BlockStart = "block_start";
    public const string BlockEnd = "block_end";
    public const string BlockBreak = "block_break";
    public const string BlockAction = "block_action";

    public const string ShowOff = "show_off";
    public const string Idle = "idle";
    public const string PutAway = "put_away";
    public const string PickUp = "pick_up";

    public const string HitFront = "hit_front";
    public const string HitBack = "hit_back";
}
