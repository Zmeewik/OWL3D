using UnityEngine;
using System;

public class MeleeWeapon : WeaponBase
{
    public LayerMask hitMask;

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

        print("Start attack");

        if (Physics.SphereCast(origin, attack.radius, dir, out var hit, attack.range, hitMask))
        {
            print("Sphere cast successful!");
            var health = hit.collider.GetComponent<EntityHealth>();
            var receiver = hit.collider.GetComponent<EntityHealth>();
            var rb = hit.collider.attachedRigidbody;

            if (health)
            {
                //Apply damage pocket
                print("Health found!");
                float dmg = 0;
                if(charged == -1 || charged >= 1)
                    dmg = DamageCalculator.CalculateDamage(attack.damage, receiver);
                else
                {
                    print(dmg);
                    dmg = DamageCalculator.CalculateDamage(attack.damage, receiver);
                    print(dmg);
                    var baseAttack = dmg / attack.maxChargeMultyplier;
                    print(baseAttack);
                    dmg = Mathf.Lerp(baseAttack, attack.damage.baseDamage, charged);
                    print(dmg);
                }
                
                var isCharged = charged == -1 ? false : true;
                DamagePacket packet = new DamagePacket(dmg, attack.damage.tags, attack.damage.effects, isCharged);
                health.ApplyDamage(packet);
                print("Damage pocket send");
                
                //Apply knockback
                if (attack.knockbackForce > 0 && rb != null)
                {
                    var force = attack.knockbackForce;
                    if(charged != -1)
                        force = Mathf.Lerp(0, attack.knockbackForce, charged);
                    Vector3 kbDir = (hit.transform.position - origin).normalized;
                    rb.AddForce(kbDir * force, ForceMode.Impulse);
                }
            }
        }
    }
}