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
        float spreadDegrees = 0f,
        Transform aimSource = null)
    {
        // The sight line is whatever actually represents aim: the view camera for the player, the
        // weapon itself for an AI that points its weapon at the target on purpose.
        var sight = aimSource != null ? aimSource : weaponTransform;

        switch (attack.kind)
        {
            case AttackKind.Projectile:
                ResolveProjectile(attack, charged, weaponTransform, sight, owner, muzzle, muzzleEffect, rnd, spreadDegrees);
                break;
            case AttackKind.Ray:
                ResolveRay(attack, charged, sight, owner, rnd, spreadDegrees);
                break;
        }
    }

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
        Transform sight,
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

            var sightRotation = ApplySpread(sight.rotation, spreadDegrees, rnd);
            Vector3 sightDirection = sightRotation * Vector3.forward;
            Vector3 aimPoint = ResolveAimPoint(sight.position, sightDirection, owner);

            // Bullets leave the muzzle, but they must still travel to where the sights point.
            // Firing parallel to the sight line from an offset muzzle sends the shot wide by that
            // offset, which is exactly what looked wrong; aiming the spawn at the sight's landing
            // point converges the two. With no muzzle assigned the shot starts at the weapon
            // centre, where the sight line already begins, so nothing changes for it.
            Vector3 spawnPosition = muzzle != null ? muzzle.position : weaponTransform.position;

            // The aim point has to stay ahead of the muzzle. The sight line starts at the eye while
            // the muzzle is a stride further forward, so anything the line clips close by -- a wall
            // the player is up against, a doorframe -- lands *behind* the barrel, and converging on
            // it would fire the shot backwards. Pushing the aim out to just past the muzzle turns
            // those cases back into a straight shot along the sights.
            float muzzleAlongSight = Vector3.Dot(spawnPosition - sight.position, sightDirection);
            float minimumAimDistance = Mathf.Max(muzzleAlongSight + 1f, 2f);
            if (Vector3.Dot(aimPoint - sight.position, sightDirection) < minimumAimDistance)
                aimPoint = sight.position + sightDirection * minimumAimDistance;

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
        Transform sight,
        Transform owner,
        System.Random rnd,
        float spreadDegrees)
    {
        Vector3 direction = ApplySpread(sight.rotation, spreadDegrees, rnd) * Vector3.forward;

        if (!Physics.Raycast(sight.position, direction, out var hit, attack.rayDistance))
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
        Vector3 kbDir = (hit.transform.position - sight.position).normalized;
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
