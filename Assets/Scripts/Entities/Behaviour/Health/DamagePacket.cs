using UnityEngine;

public struct DamagePacket
{
    public float damage;
    public bool charged;
    public DamageEffect[] effects;
    public DamageTag[] tags;
    public Vector3 forceApplied;
    public Vector3 collisionPoint;

    public DamagePacket(float damage, DamageTag[] tags, DamageEffect[] effects, Vector3 forceApplied, Vector3 collisionPoint, bool charged = false)
    {
        this.damage = damage;
        this.tags = tags;
        this.effects = effects;
        this.charged = charged;
        this.forceApplied = forceApplied;
        this.collisionPoint = collisionPoint;
    }
}