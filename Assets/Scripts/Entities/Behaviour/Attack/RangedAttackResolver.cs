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
        System.Random rnd,
        float spreadDegrees = 0f)
    {
        switch (attack.kind)
        {
            case AttackKind.Projectile:
                ResolveProjectile(attack, charged, weaponTransform, owner, muzzle, muzzleEffect, rnd, spreadDegrees);
                break;
            case AttackKind.Ray:
                ResolveRay(attack, charged, weaponTransform, owner, rnd, spreadDegrees);
                break;
        }
    }

    /// <summary>
    /// Deflects a firing direction by a random amount inside a cone. Applied to the whole rotation
    /// rather than just the direction vector so a projectile is *spawned* rotated -- Projectile
    /// takes its velocity from its own transform.forward, so rotating the spawn is what actually
    /// makes the shot travel off-axis.
    /// </summary>
    /// <summary>How far down the sight line to look for what the shot is actually pointed at.</summary>
    private const float AimProbeDistance = 500f;

    /// <summary>
    /// Where the sight line lands: the first thing under it that isn't the shooter, or a far point
    /// along it when the line hits nothing. Triggers count, so a body-part hitbox is a valid thing
    /// to be aiming at.
    /// </summary>
    private static Vector3 ResolveAimPoint(Vector3 sightOrigin, Vector3 sightDirection, Transform owner)
    {
        var hits = Physics.RaycastAll(sightOrigin, sightDirection, AimProbeDistance, ~0,
                                      QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (owner != null && (hit.transform == owner || hit.transform.IsChildOf(owner)))
                continue;

            return hit.point;
        }

        return sightOrigin + sightDirection * AimProbeDistance;
    }

    private static Quaternion ApplySpread(Quaternion rotation, float spreadDegrees, System.Random rnd)
    {
        if (spreadDegrees <= 0f)
            return rotation;

        float pitch = (float)(rnd.NextDouble() * 2d - 1d) * spreadDegrees;
        float yaw = (float)(rnd.NextDouble() * 2d - 1d) * spreadDegrees;
        return rotation * Quaternion.Euler(pitch, yaw, 0f);
    }

    private static void ResolveProjectile(
        AttackVariant attack,
        float charged,
        Transform weaponTransform,
        Transform owner,
        Transform muzzle,
        string muzzleEffect,
        System.Random rnd,
        float spreadDegrees)
    {
        Projectile proj = null;
        if (attack.projectilePrefabs.Length > 0)
        {
            var indexProjectile = rnd.Next(0, attack.projectilePrefabs.Length);

            // The sight line is the weapon's own forward -- for the player that is the crosshair,
            // for an enemy it is the aim the attack system pointed the weapon along.
            var sightRotation = ApplySpread(weaponTransform.rotation, spreadDegrees, rnd);
            Vector3 sightDirection = sightRotation * Vector3.forward;
            Vector3 aimPoint = ResolveAimPoint(weaponTransform.position, sightDirection, owner);

            // Bullets leave the muzzle, but they must still travel to where the sights point.
            // Firing parallel to the sight line from an offset muzzle sends the shot wide by that
            // offset, which is exactly what looked wrong; aiming the spawn at the sight's landing
            // point converges the two. With no muzzle assigned the shot starts at the weapon
            // centre, where the sight line already begins, so nothing changes for it.
            Vector3 spawnPosition = muzzle != null ? muzzle.position : weaponTransform.position;
            Vector3 fireDirection = aimPoint - spawnPosition;
            var firingRotation = fireDirection.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(fireDirection.normalized, Vector3.up)
                : sightRotation;

            proj = Object.Instantiate(attack.projectilePrefabs[indexProjectile], spawnPosition, firingRotation);
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

    private static void ResolveRay(
        AttackVariant attack,
        float charged,
        Transform weaponTransform,
        Transform owner,
        System.Random rnd,
        float spreadDegrees)
    {
        Vector3 direction = ApplySpread(weaponTransform.rotation, spreadDegrees, rnd) * Vector3.forward;

        if (!Physics.Raycast(weaponTransform.position, direction, out var hit, attack.rayDistance))
            return;

        // EntityHealth lives on the character root, not on the named hitbox bone the ray may
        // actually strike (see ComplexColliderHandle) -- GetComponentInParent (not GetComponent)
        // is required or any hit on a limb collider silently resolves no EntityHealth at all.
        var health = hit.collider.GetComponentInParent<EntityHealth>();
        var rb = hit.collider.attachedRigidbody;
        if (!health)
            return;

        // A hitscan fired from inside the shooter's own hitboxes would otherwise resolve straight
        // back onto the shooter.
        if (owner != null && health.transform == owner)
            return;

        float dmg = DamageCalculator.CalculateChargedDamage(attack, health, charged);
        float force = DamageCalculator.CalculateChargedKnockback(attack, charged);
        Vector3 kbDir = (hit.transform.position - weaponTransform.position).normalized;
        bool isCharged = charged != -1;
        // Passing the hit collider's rigidbody through as bodyPart lets EntityHealth.ApplyDamage
        // auto-map it (by name) to a per-body-part damage multiplier.
        DamagePacket packet = new DamagePacket(dmg, attack.damage.tags, attack.damage.effects, kbDir * force, hit.point, isCharged, rb);
        health.ApplyDamage(packet);

        // Same fix as MeleeAttackResolver: the hit collider's own rigidbody (a named limb bone)
        // is kinematic while the entity is alive and ignores AddForce, so knockback needs the
        // entity's main/root rigidbody instead.
        if (attack.knockbackForce > 0)
        {
            var mainRb = health.GetComponent<Rigidbody>();
            if (mainRb != null)
                mainRb.AddForce(kbDir * force, ForceMode.Impulse);
            else if (rb != null)
                rb.AddForce(kbDir * force, ForceMode.Impulse);
        }
    }
}
