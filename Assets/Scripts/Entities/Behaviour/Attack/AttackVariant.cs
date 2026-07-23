using UnityEngine;

public enum AttackKind { Melee, Projectile, Ray }

[CreateAssetMenu(menuName = "Combat/Attack Variant")]
public class AttackVariant : ScriptableObject
{
    public string variantName;

    public AttackKind kind;
    public float cooldown = 0.3f;
    
    [Header("Charge")]
    public float maxChargeMultyplier;
    public bool chargeable;     
    public float maxChargeTime; 
    public float minChargeTime;
    
    public DamageProfile damage;

    [Header("Melee")]
    public float range = 2f;
    public float radius = 0.4f;

    [Header("Projectile")]
    public Projectile[] projectilePrefabs;
    public HitObject[] hitObjects;
    public float projectileSpeed = 20f;

    [Header("Ray")]
    public float rayDistance = 50f;
    
    [Header("Knockback")]
    public float knockbackForce = 0f;
}