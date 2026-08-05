using UnityEngine;

/// <summary>What an attack attempt actually produced, so callers can animate the matching action.</summary>
public enum EnemyAttackResult
{
    None,
    Melee,
    Ranged,
}

/// <summary>
/// Fires the entity's weapons. Doesn't reimplement any combat maths -- it drives the existing
/// <see cref="WeaponBase"/> components through the same entry point player input uses
/// (<see cref="WeaponBase.HandleInput"/>), so enemy shots go through the same resolvers, damage
/// calculation and body-part hit attribution as the player's.
/// </summary>
public class EnemyAttackSystem : EnemySystem
{
    [Header("Weapons")]
    [SerializeField] private WeaponBase rangedWeapon;
    [SerializeField] private WeaponBase meleeWeapon;

    [Header("Ranges")]
    [SerializeField] private float rangedRange = 18f;
    [SerializeField] private float meleeRange = 2.2f;

    [Header("Timing")]
    [Tooltip("Minimum delay between AI attacks, on top of each AttackVariant's own cooldown.")]
    [SerializeField] private float attackInterval = 1.1f;

    [Tooltip("Frames between the attack animation starting and the shot or strike actually going " +
             "out, so the hit lands on the animation's contact frame instead of the instant the " +
             "attack is decided.")]
    [SerializeField, Min(0f)] private float attackDelayFrames = 10f;

    [Tooltip("Frame rate the attack clips were authored at. The Runner's clips come out of Blender " +
             "at 24 fps, which is what turns the frame count above into seconds.")]
    [SerializeField, Min(1f)] private float animationFrameRate = 24f;

    // A committed attack waiting for its contact frame.
    private bool hasPendingAttack;
    private float pendingDelay;
    private WeaponBase pendingWeapon;
    private TeamMember pendingTarget;
    private TeamMember.AimPoint pendingAimPoint;

    /// <summary>Seconds between the animation starting and the hit going out.</summary>
    public float AttackDelay => animationFrameRate > 0f ? attackDelayFrames / animationFrameRate : 0f;

    [Header("Aim")]
    [Tooltip("Relative chance of aiming at each body part. Picked per shot; zero disables a part.")]
    [SerializeField, Min(0f)] private float aimHeadWeight = 0.2f;
    [SerializeField, Min(0f)] private float aimBodyWeight = 0.6f;
    [SerializeField, Min(0f)] private float aimLegsWeight = 0.2f;

    [Tooltip("Spread cone pushed onto this entity's weapons on initialization, overriding whatever " +
             "the weapon prefab carried. Negative leaves the weapon's own value alone.")]
    [SerializeField] private float weaponSpreadDegrees = -1f;

    private float cooldownRemaining;

    public bool HasRanged => rangedWeapon != null;
    public bool HasMelee => meleeWeapon != null;

    /// <summary>Furthest distance any equipped weapon can act at -- what chase logic closes to.</summary>
    public float EffectiveRange => HasRanged ? rangedRange : (HasMelee ? meleeRange : 0f);

    public float MeleeRange => meleeRange;
    public float RangedRange => rangedRange;
    public bool IsReady => cooldownRemaining <= 0f;

    public void ConfigureFrom(EnemyConfig config)
    {
        if (config == null) return;
        rangedRange = config.rangedRange;
        meleeRange = config.meleeRange;
        attackInterval = config.attackInterval;
        attackDelayFrames = config.attackDelayFrames;
        weaponSpreadDegrees = config.weaponSpreadDegrees;
        aimHeadWeight = config.aimHeadWeight;
        aimBodyWeight = config.aimBodyWeight;
        aimLegsWeight = config.aimLegsWeight;
    }

    public override void Initialize(Enemy owner)
    {
        base.Initialize(owner);

        // The ranged resolvers hand `owner` to the spawned projectile so it can ignore the shooter;
        // an unset owner throws the moment the first shot is fired.
        if (rangedWeapon != null && rangedWeapon.owner == null)
            rangedWeapon.owner = owner.transform;
        if (meleeWeapon != null && meleeWeapon.owner == null)
            meleeWeapon.owner = owner.transform;

        if (weaponSpreadDegrees >= 0f && rangedWeapon != null)
            rangedWeapon.spreadDegrees = weaponSpreadDegrees;
    }

    /// <summary>
    /// Which part of a target this entity aims at for the next shot, drawn from the configured
    /// weights. Chosen per shot rather than held for the whole engagement, so fire wanders over a
    /// target the way spread alone can't -- and it feeds straight into the body-part damage
    /// multipliers, so an AI weighted towards headshots genuinely hurts more.
    /// </summary>
    public TeamMember.AimPoint ChooseAimPoint()
    {
        float total = aimHeadWeight + aimBodyWeight + aimLegsWeight;
        if (total <= 0f)
            return TeamMember.AimPoint.Center;

        float roll = Random.value * total;

        if (roll < aimHeadWeight)
            return TeamMember.AimPoint.Head;

        return roll < aimHeadWeight + aimBodyWeight
            ? TeamMember.AimPoint.Center
            : TeamMember.AimPoint.Feet;
    }

    /// <summary>
    /// Attacks a target, picking which body part to aim at itself. Range is still judged from the
    /// target's own position, so aiming high or low never changes whether a shot is taken.
    /// </summary>
    public EnemyAttackResult TryAttack(TeamMember target)
    {
        // Alive is checked here as well as at release: without it an attack could be committed at a
        // corpse, reporting a swing the caller then animates while no shot ever goes out.
        if (target == null || !target.IsAlive || !IsReady)
            return EnemyAttackResult.None;

        // Don't shoot mid-draw: the weapon is still travelling from the holster to the hand, so a
        // shot fired now leaves the muzzle somewhere around the hip.
        if (enemy != null && enemy.Animation != null && enemy.Animation.IsChangingWeapon)
            return EnemyAttackResult.None;

        Vector3 targetPosition = target.transform.position;

        if (HasMelee && InMeleeRange(targetPosition))
            return Commit(meleeWeapon, target, TeamMember.AimPoint.Center) ? EnemyAttackResult.Melee : EnemyAttackResult.None;

        if (HasRanged && InRangedRange(targetPosition))
            return Commit(rangedWeapon, target, ChooseAimPoint()) ? EnemyAttackResult.Ranged : EnemyAttackResult.None;

        return EnemyAttackResult.None;
    }

    /// <summary>
    /// Books an attack and starts its wind-up. The caller gets the result straight away so the
    /// animation begins now; the weapon itself goes off once the delay elapses.
    /// </summary>
    private bool Commit(WeaponBase weapon, TeamMember target, TeamMember.AimPoint aimPoint)
    {
        if (!CanUse(weapon))
            return false;

        pendingWeapon = weapon;
        pendingTarget = target;
        pendingAimPoint = aimPoint;
        pendingDelay = AttackDelay;
        hasPendingAttack = true;

        // Cooldown runs from the moment the attack starts, so the rhythm follows the animation
        // rather than drifting by the wind-up each time.
        cooldownRemaining = attackInterval;

        if (pendingDelay <= 0f)
            ReleasePendingAttack();

        return true;
    }

    public override void TickSystem(float deltaTime)
    {
        if (cooldownRemaining > 0f)
            cooldownRemaining -= deltaTime;

        if (!hasPendingAttack)
            return;

        pendingDelay -= deltaTime;
        if (pendingDelay <= 0f)
            ReleasePendingAttack();
    }

    private void ReleasePendingAttack()
    {
        hasPendingAttack = false;

        if (pendingWeapon == null)
            return;

        // Aim is resolved now, not when the attack was decided, so a target that moved during the
        // wind-up is still tracked -- and a target that died in the meantime isn't shot at.
        if (pendingTarget == null || !pendingTarget.IsAlive)
        {
            pendingWeapon = null;
            pendingTarget = null;
            return;
        }

        Fire(pendingWeapon, pendingTarget.GetAimPoint(pendingAimPoint));
        pendingWeapon = null;
        pendingTarget = null;
    }

    private static bool CanUse(WeaponBase weapon) =>
        weapon != null && weapon.attacks != null && weapon.attacks.Length > 0 && weapon.attacks[0] != null;

    public bool InMeleeRange(Vector3 targetPosition) => Flat(targetPosition - transform.position).magnitude <= meleeRange;
    public bool InRangedRange(Vector3 targetPosition) => Flat(targetPosition - transform.position).magnitude <= rangedRange;

    private bool Fire(WeaponBase weapon, Vector3 aimPoint)
    {
        if (!CanUse(weapon))
            return false;

        // Point the weapon itself at the target rather than relying on the body's facing. Two
        // reasons this is required, not cosmetic: every resolver fires along the weapon's own
        // forward axis, and the mount sits off to one side of the body -- so a mount left parallel
        // to the body sends shots down a line offset sideways by the mount's own offset, which at
        // this rig's scale is wide enough to miss a torso entirely. Body rotation is also yaw-only,
        // so it can never account for a target above or below.
        Vector3 toTarget = aimPoint - weapon.transform.position;
        if (toTarget.sqrMagnitude > 0.0001f)
            weapon.transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

        // Index 0 / Pressed is the uncharged primary attack -- the same path a player click takes.
        // The cooldown is not touched here: Commit already started it when the attack began, so the
        // interval measures animation-to-animation rather than gaining the wind-up every cycle.
        weapon.HandleInput(0, AttackInputType.Pressed);
        return true;
    }

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
