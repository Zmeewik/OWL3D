using UnityEngine;
using Random = System.Random;

public class MeleeRangedWeapon : WeaponBase
{
    public LayerMask hitMask;
    public LayerMask appliedMask;
    private readonly Random rnd = new Random();
    [SerializeField] protected Transform muzzle;
    [SerializeField] protected string muzzleEffect;

    protected override void ExecuteAttack(AttackVariant attack, float charged = -1)
    {
        if (attack.kind == AttackKind.Melee)
            MeleeAttackResolver.ResolveMeleeAttack(transform, attack, charged, hitMask, appliedMask, rnd, owner);
        else if (attack.kind == AttackKind.Projectile || attack.kind == AttackKind.Ray)
            RangedAttackResolver.ResolveRangedAttack(attack, charged, transform, owner, muzzle, muzzleEffect, rnd, spreadDegrees, aimSource);
    }
}
