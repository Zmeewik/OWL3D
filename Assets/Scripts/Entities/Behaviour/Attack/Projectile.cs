using UnityEngine;

public class Projectile : MonoBehaviour
{
    private AttackVariant attack;
    private Rigidbody rb;

    private void Awake() => rb = GetComponent<Rigidbody>();

    public void Launch(AttackVariant profile)
    {
        attack = profile;
        rb.velocity = transform.forward * profile.projectileSpeed;
        Destroy(gameObject, 10f);
    }

    private void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponent<EntityHealth>();
        var receiver = other.GetComponent<EntityHealth>();

        if (health)
        {
            float dmg = DamageCalculator.CalculateDamage(attack.damage, receiver);
            
            DamagePacket packet = new DamagePacket();
            packet.damage = dmg;
            packet.tags = attack.damage.tags;
            packet.effects = attack.damage.effects;
            health.ApplyDamage(packet);
        }

        Destroy(gameObject);
    }
}
