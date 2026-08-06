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

        // RaycastAll does not guarantee hits are ordered by distance. A fast-moving bullet
        // sweeping past several overlapping hitbox colliders in one tick (e.g. an arm collider
        // and the head collider behind it) could otherwise register whichever collider happened
        // to come first in Unity's internal (unordered) result instead of the surface the bullet
        // actually reached first.
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == projectileCollider)
                continue;

            // Never let a shot collide with whoever fired it. The old check compared names, which
            // only ever matched the shooter's root object -- every hitbox bone under it (layer
            // BodyParts, which this mask includes) still counted as a hit, so a shot spawned inside
            // the shooter's own rig died on the first frame. That never showed up with the player,
            // whose muzzle sits ahead of their collider, but an AI firing from a mount inside its
            // body killed every bullet instantly. It also used `return` rather than `continue`,
            // abandoning the remaining hits for that frame instead of just skipping this one.
            if (owner != null && (hit.transform == owner || hit.transform.IsChildOf(owner)))
                continue;

            if (hit.transform.gameObject.layer == LayerMask.NameToLayer("Bullet"))
            {
                ActionOnBulletHit(hit);
                break;
            }

            rb.velocity = Vector3.zero;
            
            var rootObject = hit.transform.root;

            var health = rootObject.transform.GetComponent<EntityHealth>();

            // No friendly fire: a shot that reaches a non-hostile entity first (a teammate standing
            // in the way) passes straight through instead of stopping on them, the same way it would
            // pass through anything else that isn't a valid target.
            if (health && IsFriendly(rootObject))
                continue;

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
                    bodypartRB,
                    owner);
                
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

    /// <summary>True if the shooter and this potential target are on the same side (or either lacks
    /// a team), so the shot should not treat them as hostile.</summary>
    private bool IsFriendly(Transform target)
    {
        var ownerTeam = owner != null ? owner.GetComponentInParent<TeamMember>() : null;
        var targetTeam = target.GetComponent<TeamMember>();
        if (ownerTeam == null || targetTeam == null)
            return false;

        return !Teams.IsHostile(ownerTeam.team, targetTeam.team);
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
