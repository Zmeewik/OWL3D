using UnityEngine;
using System;
using System.Collections.Generic;
using Random = System.Random;

public class MeleeWeapon : WeaponBase
{
    public LayerMask hitMask;
    public LayerMask appliedMask;
    Random random = new Random();

    void DebugDrawSphere(Vector3 pos, float radius, Color color)
    {
        Debug.DrawLine(pos + Vector3.up * radius, pos - Vector3.up * radius, color);
        Debug.DrawLine(pos + Vector3.right * radius, pos - Vector3.right * radius, color);
        Debug.DrawLine(pos + Vector3.forward * radius, pos - Vector3.forward * radius, color);
    }

    protected override void ExecuteAttack(AttackVariant attack, float charged = -1)
    {
        if (attack.kind != AttackKind.Melee)
            return;

        Vector3 origin = transform.position;
        Vector3 dir = transform.forward;

        //print("Start attack");
        
        Collider[] overlaps = Physics.OverlapSphere(origin, attack.radius, hitMask);
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            attack.radius,
            dir,
            attack.range,
            hitMask
        );
        
        HashSet<Collider> hitColliders = new();
        
        foreach (var col in overlaps)
            hitColliders.Add(col);

        foreach (var hit in hits)
            hitColliders.Add(hit.collider);
        
        //print($"Unique hits: {hitColliders.Count}");

        foreach (var collider in hitColliders)
        {
            //print("Sphere cast successful!");
            var health = collider.GetComponent<EntityHealth>();
            var receiver = collider.GetComponent<EntityHealth>();
            var rb = collider.attachedRigidbody;

            if (health)
            {
                //Apply damage pocket
                print("Health found!");
                float dmg = 0;
                if(charged == -1 || charged >= 1)
                    dmg = DamageCalculator.CalculateDamage(attack.damage, receiver);
                else
                {
                    dmg = DamageCalculator.CalculateDamage(attack.damage, receiver);
                    var baseAttack = dmg / attack.maxChargeMultyplier;
                    dmg = Mathf.Lerp(baseAttack, attack.damage.baseDamage, charged);
                }
                
                var force = attack.knockbackForce;
                if(charged != -1)
                    force = Mathf.Lerp(0, attack.knockbackForce, charged);
                Vector3 kbDir = dir.normalized;
                var isCharged = charged == -1 ? false : true;
                DamagePacket packet = new DamagePacket(dmg, attack.damage.tags, attack.damage.effects, force * kbDir, transform.position, isCharged);
                health.ApplyDamage(packet);
                print("Damage packet send");
                
                //Apply knockback
                if (attack.knockbackForce > 0 && rb != null)
                {
                    rb.AddForce(kbDir * force, ForceMode.Impulse);
                }
            }
        }
        
        if (Physics.Raycast(
                transform.position,
                dir,
                out RaycastHit hit1,
                attack.range + attack.radius,
                appliedMask))
        {
            var rbBodyPart = hit1.collider.attachedRigidbody;
            rbBodyPart?.AddForce(transform.forward * attack.knockbackForce, ForceMode.Impulse);
            
            // Add hit effect
            if (attack.hitObjects.Length > 0)
            {
                var index = random.Next(0, attack.hitObjects.Length);
                var hitObject = Instantiate(attack.hitObjects[index]);
                // Set bullets
                hitObject.transform.position = hit1.point;
                hitObject.transform.rotation = Quaternion.LookRotation(-hit1.normal);
                hitObject.transform.parent = hit1.transform;
                hitObject.gameObject.SetActive(true);
                hitObject.Disappear();
            }
            
            // Activate effect
            Particles.Instance.StartEffect("MetalHit", hit1.point, Quaternion.LookRotation(-hit1.normal));
        }
    }
}