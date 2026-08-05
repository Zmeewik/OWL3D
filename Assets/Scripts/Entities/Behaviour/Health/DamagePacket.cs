using UnityEngine;

public struct DamagePacket
{
    public float damage;
    public bool charged;
    public DamageEffect[] effects;
    public DamageTag[] tags;
    public Vector3 forceApplied;
    public Vector3 collisionPoint;
    public Rigidbody bodyPart;

    /// <summary>Whoever dealt this hit. Lets the victim aggro/turn on the actual attacker rather than just the hit direction.</summary>
    public Transform attacker;

    public DamagePacket(float damage, DamageTag[] tags, DamageEffect[] effects, Vector3 forceApplied, Vector3 collisionPoint, bool charged = false, Rigidbody bodyPart = null, Transform attacker = null)
    {
        this.damage = damage;
        this.tags = tags;
        this.effects = effects;
        this.charged = charged;
        this.forceApplied = forceApplied;
        this.collisionPoint = collisionPoint;
        this.bodyPart = bodyPart;
        this.attacker = attacker;
    }
}