using UnityEngine;

/// <summary>
/// Shared ranged (projectile + hitscan ray) attack logic. MeleeRangedWeapon.AttackRanged and
/// RangedWeapon.ExecuteAttack used to carry near-identical copies of this.
///
/// RangedWeapon's Projectile case had a real bug relative to MeleeRangedWeapon's: it guarded
/// the hit-object Instantiate with `attack.projectilePrefabs.Length > 0` instead of
/// `attack.hitObjects.Length > 0`. If a weapon had projectiles configured but zero hit-object
/// prefabs, RangedWeapon would still attempt `attack.hitObjects[0]` on an empty array and throw
/// an IndexOutOfRangeException at runtime. Unifying onto MeleeRangedWeapon's (correct) guard
/// fixes this as a side effect.
/// </summary>
public static class RangedAttackResolver
{
    public static void ResolveRangedAttack(
        AttackVariant attack,
        float charged,
        Transform weaponTransform,
        Transform owner,
        Transform muzzle,
        string muzzleEffect,
        System.Random rnd)
    {
        switch (attack.kind)
        {
            case AttackKind.Projectile:
                ResolveProjectile(attack, charged, weaponTransform, owner, muzzle, muzzleEffect, rnd);
                break;
            case AttackKind.Ray:
                ResolveRay(attack, charged, weaponTransform);
                break;
        }
    }

    private static void ResolveProjectile(
        AttackVariant attack,
        float charged,
        Transform weaponTransform,
        Transform owner,
        Transform muzzle,
        string muzzleEffect,
        System.Random rnd)
    {
        Projectile proj = null;
        if (attack.projectilePrefabs.Length > 0)
        {
            var indexProjectile = rnd.Next(0, attack.projectilePrefabs.Length);
            proj = Object.Instantiate(
                attack.projectilePrefabs[indexProjectile],
                weaponTransform.position + weaponTransform.forward * 1f,
                weaponTransform.rotation);
        }

        HitObject hitObj = null;
        if (attack.hitObjects.Length > 0)
        {
            var indexHitObject = rnd.Next(0, attack.hitObjects.Length);
            hitObj = Object.Instantiate(
                attack.hitObjects[indexHitObject],
                weaponTransform.position,
                weaponTransform.rotation);
        }

        // Start animation muzzle flash
        if (muzzle != null)
            Particles.Instance.StartEffect(muzzleEffect, muzzle.position, Quaternion.LookRotation(weaponTransform.forward));

        proj?.Launch(attack, charged, hitObj, owner);
    }

    private static void ResolveRay(AttackVariant attack, float charged, Transform weaponTransform)
    {
        if (!Physics.Raycast(weaponTransform.position, weaponTransform.forward, out var hit, attack.rayDistance))
            return;

        var health = hit.collider.GetComponent<EntityHealth>();
        var rb = hit.collider.attachedRigidbody;
        if (!health)
            return;

        float dmg = DamageCalculator.CalculateChargedDamage(attack, health, charged);
        float force = DamageCalculator.CalculateChargedKnockback(attack, charged);
        Vector3 kbDir = (hit.transform.position - weaponTransform.position).normalized;
        bool isCharged = charged != -1;
        DamagePacket packet = new DamagePacket(dmg, attack.damage.tags, attack.damage.effects, kbDir * force, hit.point, isCharged);
        health.ApplyDamage(packet);

        if (attack.knockbackForce > 0 && rb != null)
            rb.AddForce(kbDir * force, ForceMode.Impulse);
    }
}
