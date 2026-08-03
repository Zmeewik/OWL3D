using UnityEngine;

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
    }

    public override void TickSystem(float deltaTime)
    {
        if (cooldownRemaining > 0f)
            cooldownRemaining -= deltaTime;
    }

    public bool InMeleeRange(Vector3 targetPosition) => Flat(targetPosition - transform.position).magnitude <= meleeRange;
    public bool InRangedRange(Vector3 targetPosition) => Flat(targetPosition - transform.position).magnitude <= rangedRange;

    /// <summary>
    /// Attacks with whatever fits the distance -- melee when close enough, otherwise the gun.
    /// Returns true if a swing/shot actually went out, so callers can drive an attack animation
    /// only when something happened.
    /// </summary>
    public bool TryAttack(Vector3 aimPoint)
    {
        if (!IsReady)
            return false;

        if (HasMelee && InMeleeRange(aimPoint))
            return Fire(meleeWeapon, aimPoint);

        if (HasRanged && InRangedRange(aimPoint))
            return Fire(rangedWeapon, aimPoint);

        return false;
    }

    private bool Fire(WeaponBase weapon, Vector3 aimPoint)
    {
        if (weapon == null || weapon.attacks == null || weapon.attacks.Length == 0 || weapon.attacks[0] == null)
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
        weapon.HandleInput(0, AttackInputType.Pressed);
        cooldownRemaining = attackInterval;
        return true;
    }

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
