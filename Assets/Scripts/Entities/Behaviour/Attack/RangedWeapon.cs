using UnityEngine;
using System;

public class RangedWeapon : WeaponBase
{
    protected override void ExecuteAttack(AttackVariant attack, float charged = -1)
    {
        switch (attack.kind)
        {
            case AttackKind.Projectile:
                var proj = Instantiate(
                    attack.projectilePrefab,
                    transform.position,
                    transform.rotation
                );
                proj.Launch(attack);
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
                        
                        var isCharged = charged == -1 ? false : true;
                        DamagePacket packet = new DamagePacket(dmg, attack.damage.tags, attack.damage.effects, isCharged);
                        health.ApplyDamage(packet);
                        
                        
                        //Apply knockback
                        if (attack.knockbackForce > 0 && rb != null)
                        {
                            var force = attack.knockbackForce;
                            if(charged != -1)
                                force = Mathf.Lerp(0, attack.knockbackForce, charged);
                            Vector3 kbDir = (hit.transform.position - transform.position).normalized;
                            rb.AddForce(kbDir * force, ForceMode.Impulse);
                        }
                    }
                }
                break;
        }
    }
}