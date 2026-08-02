using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "Damage Calculator", menuName = "Combat/Damage Calculator")]
public class DamageCalculator : ScriptableObject
{
    [Range(0f, 3f)] public static float baseMultiplier = 1f;
    public float vulnerableBonus = 1.5f;
    public float resistancePenalty = 0.5f;

    public static float CalculateDamage(DamageProfile profile, EntityHealth target)
    {
        float damage = profile.baseDamage * baseMultiplier;

        foreach (var tag in profile.tags)
        {
            if (target.VulnerableTo.Contains(tag))
                damage *= tag.vulnerableBonus;

            if (target.ResistantTo.Contains(tag))
                damage *= tag.resistancePenalty;
        }

        return damage;
    }

    /// <summary>
    /// Damage for a chargeable attack, scaled by how long it was held. This exact charge-lerp
    /// (and the sibling CalculateChargedKnockback) used to be re-implemented inline in
    /// MeleeWeapon, MeleeRangedWeapon (twice: melee + ranged), RangedWeapon, and
    /// Projectile.FixedUpdate.
    ///
    /// Convention (matching every existing call site): charged == -1 means "not a charge
    /// attempt" (not-chargeable weapons always pass this) -> full damage. charged >= 1 means
    /// "fully charged" -> full damage. Otherwise linearly interpolates between
    /// (full damage / attack.maxChargeMultyplier) and the attack's full baseDamage.
    ///
    /// Both Ray-attack call sites (RangedWeapon and MeleeRangedWeapon.AttackRanged) previously
    /// used `Math.Clamp(charged, baseAttack, attack.damage.baseDamage)` here instead of a Lerp
    /// -- clamping a 0..1 charge fraction between two damage magnitudes, which for ordinary
    /// damage values (e.g. 10..20) means the clamp returns `baseAttack` for essentially any
    /// `charged` in its normal range. That looks like a copy-paste mistake rather than an
    /// intentional divergence from the melee/projectile charge formula (which all correctly
    /// used Lerp) -- unifying onto one helper fixes it as a side effect. Flagging explicitly
    /// since it changes ray-attack charge damage scaling; revert to Math.Clamp here if that
    /// was actually intentional.
    /// </summary>
    public static float CalculateChargedDamage(AttackVariant attack, EntityHealth target, float charged)
    {
        float damage = CalculateDamage(attack.damage, target);
        if (charged == -1 || charged >= 1)
            return damage;

        float baseAttack = damage / attack.maxChargeMultyplier;
        return Mathf.Lerp(baseAttack, attack.damage.baseDamage, charged);
    }

    /// <summary>Knockback for a chargeable attack, scaled the same way as CalculateChargedDamage.</summary>
    public static float CalculateChargedKnockback(AttackVariant attack, float charged)
    {
        return charged == -1 ? attack.knockbackForce : Mathf.Lerp(0, attack.knockbackForce, charged);
    }
}