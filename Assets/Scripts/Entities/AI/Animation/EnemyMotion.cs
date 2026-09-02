/// <summary>
/// One semantic movement an enemy can play. Member names are deliberately identical to the motion
/// token inside the clip names (E_Runner_Char_<b>Idle</b>_Weponized), so the clip name can be
/// derived from the enum with no lookup table to keep in sync -- the same trick
/// WeaponAnimationController.ChangeAnimationName uses for the player's weapons.
///
/// Not every motion exists in both armed states (the attacks and jumps are weaponized-only, for
/// instance); <see cref="EnemyAnimationSystem"/> falls back to the other state when a clip is
/// missing rather than requiring every combination to be authored.
/// </summary>
public enum EnemyMotion
{
    Idle,
    Walk,

    AttackRanged1,
    AttackRanged2,
    LegHit,

    HitFront,
    HitBack,
    Stagger,

    Jump,
    Land,
    InAir,

    BlockStart,
    BlockContinue,
    BlockEnd,
    BlockAction1,
    BlockAction2,
    BlockActionStrong,

    DodgeLeft,
    DodgeRight,

    Interaction,
    Talk,
    ShowOff,
    Reload,
    ChangeWeapon,
}

/// <summary>
/// Whether the enemy is currently holding its weapon or has it stowed. The member names match the
/// suffix token used in the clip names, misspelling included -- "Weponized" is how the animations
/// are actually named in the FBX, and renaming the enum to fix the spelling would silently break
/// every name lookup.
/// </summary>
public enum EnemyArmedState
{
    Unweponized,
    Weponized,
}
