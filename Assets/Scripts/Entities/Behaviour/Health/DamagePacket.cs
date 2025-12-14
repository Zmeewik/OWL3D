using UnityEngine;

public struct DamagePacket
{
    public float damage;
    public bool charged;
    public DamageEffect[] effects;
    public DamageTag[] tags;

    public DamagePacket(float damage, DamageTag[] tags, DamageEffect[] effects, bool charged = false)
    {
        this.damage = damage;
        this.tags = tags;
        this.effects = effects;
        this.charged = charged;
    }
}