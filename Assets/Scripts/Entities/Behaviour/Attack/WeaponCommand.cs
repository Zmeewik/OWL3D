/// <summary>
/// Every command sent over <see cref="IWeaponCommand.OnWeaponCommand"/> (previously a raw
/// string; WeaponManager.HandleCommand's switch is the sole consumer and defines the full
/// vocabulary it understands). A typo in a string literal compiled fine and did nothing;
/// a typo in an enum member is a compile error.
///
/// Only LeftAttackCancel/RightAttackCancel/BlockEnd are currently ever sent (from
/// PlayerWallRun, to cancel an in-progress attack/block when a wall-climb starts) — the rest
/// mirror WeaponManager's own public API and appear to be a leftover from an earlier design
/// where all input routed through this channel. Preserved rather than deleted: removing
/// unreached members wasn't part of this pass, and they're harmless to keep.
/// </summary>
public enum WeaponCommand
{
    LeftAttack,
    RightAttack,
    LeftAttackStart,
    RightAttackStart,
    LeftAttackContinue,
    RightAttackContinue,
    LeftAttackEnd,
    RightAttackEnd,
    LeftAttackCancel,
    RightAttackCancel,
    BlockStart,
    BlockContinue,
    BlockAction,
    BlockBreak,
    BlockEnd,
    MeleeHit,
    Idle,
    PutAway,
    ShowOff,
}
