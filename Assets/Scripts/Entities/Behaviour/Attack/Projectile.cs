using UnityEngine;

public class Projectile : MonoBehaviour
{
    protected HitObject hitObject;   
    protected AttackVariant attack;
    protected float chargeFactor;
    [SerializeField] protected Rigidbody rb;
    [SerializeField] protected float projectileDisappearTime;
    [SerializeField] private Transform owner;
    [SerializeField] private LayerMask hitMask;
    protected Vector3 previousPosition;
    [SerializeField] Transform rayOrigin;
    [SerializeField] private bool notSetToNormal;
    protected Vector3 offsetOfRaycast;
    private Collider projectileCollider;
    

    private void Awake() => rb = GetComponent<Rigidbody>();

    public void Launch(AttackVariant profile, float chargeFactor, HitObject hitObject, Transform ownerCollider)
    {
        attack = profile;
        this.chargeFactor = chargeFactor;
        this.hitObject = hitObject;
        hitObject.gameObject.SetActive(false);
        owner = ownerCollider.transform;
        offsetOfRaycast = rayOrigin.localPosition;
        projectileCollider = GetComponent<Collider>();
        previousPosition = transform.position;
        
        rb.velocity = transform.forward * profile.projectileSpeed;
        ActionOnStart();
        Destroy(gameObject, 3f);
    }

    protected virtual void ActionOnStart()
    {
        
    }
    
    protected virtual void ActionOnEnd()
    {
        
    }

    protected virtual void ActionOnBulletHit(RaycastHit hit)
    {
        // Activate effect
        Destroy(gameObject);
    }

    void FixedUpdate()
    {
        Vector3 currentPosition = transform.position;
        Vector3 direction = currentPosition - previousPosition;
        float distance = direction.magnitude;

        RaycastHit[] hits = Physics.RaycastAll(
            previousPosition,
            direction.normalized,
            distance,
            hitMask);
        
        foreach (var hit in hits)
        {
            if (hit.collider == projectileCollider)
                continue;
            
            if (hit.transform.name == owner.name)
                return;

            if (hit.transform.gameObject.layer == LayerMask.NameToLayer("Bullet"))
            {
                ActionOnBulletHit(hit);
                break;
            }

            rb.velocity = Vector3.zero;
            
            var rootObject = hit.transform.root;

            var health = rootObject.transform.GetComponent<EntityHealth>();

            if (health)
            {
                float dmg = DamageCalculator.CalculateChargedDamage(attack, health, chargeFactor);
                float force = DamageCalculator.CalculateChargedKnockback(attack, chargeFactor);

                bool isCharged = chargeFactor != -1;
                var bodypartRB = hit.transform.GetComponent<Rigidbody>();
                DamagePacket packet = new DamagePacket(
                    dmg,
                    attack.damage.tags,
                    attack.damage.effects,
                    force * transform.forward,
                    transform.position,
                    isCharged,
                    bodypartRB);
                
                print(rootObject.name + " is damaged at: " + packet.damage);
                
                health.ApplyDamage(packet);
                
                var goalRB = rootObject.GetComponent<Rigidbody>();
                if (force > 0 && goalRB != null)
                    goalRB.AddForce(transform.forward * force, ForceMode.Impulse);
                if (force > 0 && bodypartRB != null)
                    bodypartRB.AddForce(transform.forward * force/2, ForceMode.Impulse);
            }
        
            // Set bullets
            hitObject.transform.position = hit.point;
            if(!notSetToNormal)
                hitObject.transform.rotation = Quaternion.LookRotation(-hit.normal);
            else
                hitObject.transform.rotation = transform.rotation;
            hitObject.gameObject.SetActive(true);
            hitObject.transform.parent = hit.transform;
            hitObject.Disappear();
            
            // Activate effect
            Particles.Instance.StartEffect("MetalHit", hit.point, hitObject.transform.rotation);
            
            var objectRB = hit.collider.attachedRigidbody;
            if (attack.knockbackForce > 0 && objectRB != null)
                objectRB.AddForce(transform.forward * attack.knockbackForce, ForceMode.Impulse);
        
            ActionOnEnd();
            print("hit_end");
            Destroy(gameObject);
        }

        previousPosition = currentPosition;
    }

    /*private void OnTriggerExit(Collider other)
    {
        if(owner != null && other.transform.root == owner)
            owner = null;
    }*/
    
    /*private void OnTriggerEnter(Collider other)
    {
        print("hit");
        if (other.transform.root == owner)
            return;
        
        rb.velocity = Vector3.zero;

        var health = other.GetComponent<EntityHealth>();
        var receiver = other.GetComponent<EntityHealth>();

        if (health)
        {
            float dmg;

            if (chargeFactor == -1 || chargeFactor >= 1)
            {
                dmg = DamageCalculator.CalculateDamage(attack.damage, receiver);
            }
            else
            {
                dmg = DamageCalculator.CalculateDamage(attack.damage, receiver);

                float baseAttack = dmg / attack.maxChargeMultyplier;
                dmg = Mathf.Lerp(baseAttack, attack.damage.baseDamage, chargeFactor);
            }

            float force = attack.knockbackForce;
            if (chargeFactor != -1)
                force = Mathf.Lerp(0, attack.knockbackForce, chargeFactor);

            bool isCharged = chargeFactor != -1;

            DamagePacket packet = new DamagePacket(
                dmg,
                attack.damage.tags,
                attack.damage.effects,
                force * transform.forward,
                transform.position,
                isCharged);

            health.ApplyDamage(packet);

            if (force > 0 && rb != null)
                rb.AddForce(transform.forward * force, ForceMode.Impulse);
        }
        
        // Set bullets
        hitObject.transform.position = transform.position;
        hitObject.transform.rotation = Quaternion.LookRotation(transform.forward);
        hitObject.gameObject.SetActive(true);
        hitObject.transform.parent = other.transform;
        hitObject.Disappear();
        
        ActionOnEnd();
        print("hit_end");
        Destroy(gameObject);
    }*/
}
