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

        // Rank colliders by how far along the swing's forward direction they were actually
        // reached (SphereCastAll's hit.distance), not by raw distance to the weapon's position.
        // The weapon/hand origin sits much closer to the torso/arms than to the head, so a plain
        // closest-point comparison (what this used to do) picked whatever body part happened to
        // be nearest the weapon almost regardless of aim, not what the swing was actually aimed
        // at. OverlapSphere is omnidirectional and only used as a fallback distance for colliders
        // the directional sweep didn't itself report (e.g. something already overlapping the
        // weapon at the very start of the swing).
        Dictionary<Collider, float> distanceByCollider = new();
        foreach (var hit in hits)
        {
            if (!distanceByCollider.TryGetValue(hit.collider, out var existingDist) || hit.distance < existingDist)
                distanceByCollider[hit.collider] = hit.distance;
        }
        foreach (var col in overlaps)
        {
            if (!distanceByCollider.ContainsKey(col))
                distanceByCollider[col] = Vector3.Distance(origin, col.ClosestPoint(origin));
        }

        // A single swing's overlap can now include several colliders belonging to the same
        // entity (its main collision volume plus any of its named hitbox colliders -- see
        // ComplexColliderHandle). GetComponentInParent (needed since hitbox colliders live on
        // child bones, not the same GameObject as EntityHealth) would otherwise resolve the same
        // EntityHealth multiple times and apply damage once per overlapping collider. Dedupe by
        // entity, always preferring a named BodyParts-layer collider over the generic main
        // collider (the main collider often geometrically encloses/overlaps the named hitboxes,
        // e.g. hits from behind, and would otherwise "win" and collapse every hit to a flat
        // body-multiplier); among colliders of the same priority, prefer whichever the swing
        // reached first.
        int bodyPartsLayer = LayerMask.NameToLayer("BodyParts");
        Dictionary<EntityHealth, (Collider collider, float distance)> closestPerEntity = new();
        foreach (var kvp in distanceByCollider)
        {
            var collider = kvp.Key;
            var distance = kvp.Value;
            var health = collider.GetComponentInParent<EntityHealth>();
            if (health == null)
                continue;

            if (!closestPerEntity.TryGetValue(health, out var existing))
            {
                closestPerEntity[health] = (collider, distance);
                continue;
            }

            bool isBodyPart = collider.gameObject.layer == bodyPartsLayer;
            bool existingIsBodyPart = existing.collider.gameObject.layer == bodyPartsLayer;
            bool isCloser = distance < existing.distance;

            if ((isBodyPart && !existingIsBodyPart) || (isBodyPart == existingIsBodyPart && isCloser))
                closestPerEntity[health] = (collider, distance);
        }

        // The sphere sweep is forgiving about whether a swing lands at all, but its radius
        // blurs precision between two adjacent boxes (e.g. Head sitting right on top of
        // Torso_Upper/spine): the wider torso box can register as "reached" fractionally before
        // the head even when the player is clearly aiming at the head/neck. A zero-radius
        // raycast along the exact same swing direction has no such ambiguity, so wherever it
        // directly lines up with an entity already in range, its precise hit collider overrides
        // the sphere-based pick for that entity.
        if (Physics.Raycast(origin, dir, out RaycastHit preciseHit, attack.range, hitMask))
        {
            var preciseHealth = preciseHit.collider.GetComponentInParent<EntityHealth>();
            if (preciseHealth != null && closestPerEntity.ContainsKey(preciseHealth))
                closestPerEntity[preciseHealth] = (preciseHit.collider, preciseHit.distance);
        }

        foreach (var kvp in closestPerEntity)
        {
            var health = kvp.Key;
            var collider = kvp.Value.collider;
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
