using UnityEngine;
using System;
using Random = System.Random;

public class RangedWeapon : WeaponBase
{
    Random rnd = new Random();
    [SerializeField] protected Transform muzzle;
    [SerializeField] protected string muzzleEffect;
    
    protected override void ExecuteAttack(AttackVariant attack, float charged = -1)
    {
        switch (attack.kind)
        {
            case AttackKind.Projectile:
                var indexProjectile = rnd.Next(0, attack.projectilePrefabs.Length);
                var indexHitObject = rnd.Next(0, attack.hitObjects.Length);
                
                Projectile proj = null;
                if (attack.projectilePrefabs.Length > 0)
                {
                     proj = Instantiate(
                        attack.projectilePrefabs[indexProjectile],
                        transform.position + transform.forward * 1f,
                        transform.rotation
                    );
                }
                HitObject hitObj = null;
                if (attack.projectilePrefabs.Length > 0)
                {
                     hitObj = Instantiate(
                        attack.hitObjects[indexHitObject],
                        transform.position,
                        transform.rotation
                    );
                }
                
                // Start animation muzzle flash
                if(muzzle != null)
                    Particles.Instance.StartEffect(muzzleEffect, muzzle.position, Quaternion.LookRotation(transform.forward));

                proj?.Launch(attack, charged, hitObj, owner);
                break;

            case AttackKind.Ray:
                if (Physics.Raycast(transform.position, transform.forward, out var hit, attack.rayDistance))
                {
                    var health = hit.collider.GetComponent<EntityHealth>();
                    var receiver = hit.collider.GetComponent<EntityHealth>();
                    var rb = hit.collider.attachedRigidbody;

                    if (health)
                    {
                        //Apply damage
                        float dmg = 0;
                        if(charged == -1 || charged >= 1)
                            dmg = DamageCalculator.CalculateDamage(attack.damage, receiver);
                        else
                        {
                            dmg = DamageCalculator.CalculateDamage(attack.damage, receiver);
                            var baseAttack = dmg / attack.maxChargeMultyplier;
                            dmg = Math.Clamp(charged, baseAttack, attack.damage.baseDamage);
                        }
                        
                        // Apply damage and additional force
                        var force = attack.knockbackForce;
                        if(charged != -1)
                            force = Mathf.Lerp(0, attack.knockbackForce, charged);
                        Vector3 kbDir = (hit.transform.position - transform.position).normalized;
                        var isCharged = charged == -1 ? false : true;
                        DamagePacket packet = new DamagePacket(dmg, attack.damage.tags, attack.damage.effects, kbDir * force, hit.point, isCharged);
                        health.ApplyDamage(packet);
                        
                        //Apply knockback
                        if (attack.knockbackForce > 0 && rb != null)
                        {
                            rb.AddForce(kbDir * force, ForceMode.Impulse);
                        }
                    }
                }
                break;
        }
    }
}