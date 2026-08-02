using UnityEngine;
using Random = System.Random;

public class MeleeWeapon : WeaponBase
{
    public LayerMask hitMask;
    public LayerMask appliedMask;
    private readonly Random rnd = new Random();

    protected override void ExecuteAttack(AttackVariant attack, float charged = -1)
    {
        if (attack.kind != AttackKind.Melee)
            return;

        MeleeAttackResolver.ResolveMeleeAttack(transform, attack, charged, hitMask, appliedMask, rnd);
    }
}
