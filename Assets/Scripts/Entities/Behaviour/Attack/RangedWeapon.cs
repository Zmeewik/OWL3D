using UnityEngine;
using Random = System.Random;

public class RangedWeapon : WeaponBase
{
    private readonly Random rnd = new Random();
    [SerializeField] protected Transform muzzle;
    [SerializeField] protected string muzzleEffect;

    protected override void ExecuteAttack(AttackVariant attack, float charged = -1)
    {
        RangedAttackResolver.ResolveRangedAttack(attack, charged, transform, owner, muzzle, muzzleEffect, rnd);
    }
}
