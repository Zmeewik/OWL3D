using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared melee hit-detection + damage/knockback/hit-effect logic. MeleeWeapon.ExecuteAttack
/// and MeleeRangedWeapon.AttackMelee used to carry byte-for-byte identical copies of this
/// (~90 lines each, differing only in a local System.Random field's name).
/// </summary>
public static class MeleeAttackResolver
{
    /// <param name="owner">
    /// Whoever is swinging. Their own hitboxes sit on the same layers the swing searches, and a
    /// weapon is often mounted at chest/head height inside them, so without this an entity damages
    /// and knocks back itself on every attack. Passing null keeps the old (unfiltered) behaviour.
    /// </param>
    public static void ResolveMeleeAttack(
        Transform weaponTransform,
        AttackVariant attack,
        float charged,
        LayerMask hitMask,
        LayerMask appliedMask,
        System.Random rnd,
        Transform owner = null)
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

            // Compared against the resolved entity rather than the collider's parentage, because a
            // weapon isn't necessarily a child of its wielder (the player's are mounted under the
            // camera) -- what matters is that the thing we're about to damage is the swinger.
            if (owner != null && health.transform == owner)
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

        // The sphere sweep decides *whether* a swing connects (forgiving, radius-based), but a
        // zero-radius ray along the same direction decides *which* body part it lands on: the
        // sweep's radius blurs the boundary between adjacent boxes (Head sits directly on
        // Torso_Upper, so aiming at the neck kept resolving to the spine).
        //
        // The mask here is deliberately BodyParts-only rather than hitMask. hitMask also
        // includes the Entity layer, whose main capsule collider encloses the whole body -- a
        // thin ray always strikes that outer capsule surface *before* reaching any hitbox
        // inside it, and Physics.Raycast returns only the closest hit, so a hitMask ray would
        // resolve to the main collider every single time (this is what made the previous
        // attempt at this worse rather than better). Restricting the mask makes the ray see
        // only named body parts, so the nearest one along the aim direction wins.
        //
        // Only entities the sweep already found get refined -- the ray narrows down the body
        // part, it never extends the swing's reach to something the sweep didn't hit.
        int bodyPartsMask = bodyPartsLayer >= 0 ? 1 << bodyPartsLayer : 0;
        if (bodyPartsMask != 0 && Physics.Raycast(
                origin,
                dir,
                out RaycastHit preciseHit,
                attack.range + attack.radius,
                bodyPartsMask,
                QueryTriggerInteraction.Collide))
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
            DamagePacket packet = new DamagePacket(dmg, attack.damage.tags, attack.damage.effects, force * kbDir, origin, isCharged, rb, owner);
            health.ApplyDamage(packet);

            // The hit collider's own rigidbody (a named limb bone) is kinematic while the entity
            // is alive (ComplexColliderHandle.InitializePart) and silently ignores AddForce, so
            // knockback needs to land on the entity's main/root rigidbody instead - matching
            // Projectile.cs's existing (working) ranged-knockback pattern. Falls back to the hit
            // collider's own rigidbody if the entity has no root rigidbody for some reason.
            if (attack.knockbackForce > 0)
            {
                var mainRb = health.GetComponent<Rigidbody>();
                if (mainRb != null)
                    mainRb.AddForce(kbDir * force, ForceMode.Impulse);
                else if (rb != null)
                    rb.AddForce(kbDir * force, ForceMode.Impulse);
            }
        }

        // Same self-hit guard for the effect/knockback ray: appliedMask includes BodyParts, so
        // without it a swing shoves the swinger's own rigidbody and sticks a hit decal on them.
        if (Physics.Raycast(origin, dir, out RaycastHit hit1, attack.range + attack.radius, appliedMask) &&
            !(owner != null && (hit1.transform == owner || hit1.transform.IsChildOf(owner))))
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
