using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared melee hit-detection + damage/knockback/hit-effect logic. MeleeWeapon.ExecuteAttack
/// and MeleeRangedWeapon.AttackMelee used to carry byte-for-byte identical copies of this
/// (~90 lines each, differing only in a local System.Random field's name).
/// </summary>
public static class MeleeAttackResolver
{
    public static void ResolveMeleeAttack(
        Transform weaponTransform,
        AttackVariant attack,
        float charged,
        LayerMask hitMask,
        LayerMask appliedMask,
        System.Random rnd)
    {
        Vector3 origin = weaponTransform.position;
        Vector3 dir = weaponTransform.forward;

        Collider[] overlaps = Physics.OverlapSphere(origin, attack.radius, hitMask);
        RaycastHit[] hits = Physics.SphereCastAll(origin, attack.radius, dir, attack.range, hitMask);

        HashSet<Collider> hitColliders = new();
        foreach (var col in overlaps)
            hitColliders.Add(col);
        foreach (var hit in hits)
            hitColliders.Add(hit.collider);

        // A single swing's overlap can now include several colliders belonging to the same
        // entity (its main collision volume plus any of its named hitbox colliders -- see
        // ComplexColliderHandle). GetComponentInParent (needed since hitbox colliders live on
        // child bones, not the same GameObject as EntityHealth) would otherwise resolve the same
        // EntityHealth multiple times and apply damage once per overlapping collider. Dedupe by
        // entity, always preferring a named BodyParts-layer collider over the generic main
        // collider (the main collider often geometrically encloses/overlaps the named hitboxes,
        // e.g. hits from behind, and would otherwise "win" and collapse every hit to a flat
        // body-multiplier); among colliders of the same priority, prefer whichever is closest to
        // the swing origin.
        int bodyPartsLayer = LayerMask.NameToLayer("BodyParts");
        Dictionary<EntityHealth, Collider> closestPerEntity = new();
        foreach (var collider in hitColliders)
        {
            var health = collider.GetComponentInParent<EntityHealth>();
            if (health == null)
                continue;

            if (!closestPerEntity.TryGetValue(health, out var existing))
            {
                closestPerEntity[health] = collider;
                continue;
            }

            bool isBodyPart = collider.gameObject.layer == bodyPartsLayer;
            bool existingIsBodyPart = existing.gameObject.layer == bodyPartsLayer;
            bool isCloser = Vector3.SqrMagnitude(collider.ClosestPoint(origin) - origin) <
                             Vector3.SqrMagnitude(existing.ClosestPoint(origin) - origin);

            if ((isBodyPart && !existingIsBodyPart) || (isBodyPart == existingIsBodyPart && isCloser))
                closestPerEntity[health] = collider;
        }

        foreach (var kvp in closestPerEntity)
        {
            var health = kvp.Key;
            var collider = kvp.Value;
            var rb = collider.attachedRigidbody;

            float dmg = DamageCalculator.CalculateChargedDamage(attack, health, charged);
            float force = DamageCalculator.CalculateChargedKnockback(attack, charged);
            Vector3 kbDir = dir.normalized;
            bool isCharged = charged != -1;
            DamagePacket packet = new DamagePacket(dmg, attack.damage.tags, attack.damage.effects, force * kbDir, origin, isCharged, rb);
            health.ApplyDamage(packet);

            if (attack.knockbackForce > 0 && rb != null)
                rb.AddForce(kbDir * force, ForceMode.Impulse);
        }

        if (Physics.Raycast(origin, dir, out RaycastHit hit1, attack.range + attack.radius, appliedMask))
        {
            var rbBodyPart = hit1.collider.attachedRigidbody;
            rbBodyPart?.AddForce(dir * attack.knockbackForce, ForceMode.Impulse);

            // Add hit effect
            if (attack.hitObjects.Length > 0)
            {
                var index = rnd.Next(0, attack.hitObjects.Length);
                var hitObject = Object.Instantiate(attack.hitObjects[index]);
                hitObject.transform.position = hit1.point;
                hitObject.transform.rotation = Quaternion.LookRotation(-hit1.normal);
                hitObject.transform.parent = hit1.transform;
                hitObject.gameObject.SetActive(true);
                hitObject.Disappear();
            }

            Particles.Instance.StartEffect("MetalHit", hit1.point, Quaternion.LookRotation(-hit1.normal));
        }
    }
}
