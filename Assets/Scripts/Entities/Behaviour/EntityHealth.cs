using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class EntityHealth : MonoBehaviour, IAnimationSender
{
    [Header("Health settings")]
    [SerializeField] private float maxHealth = 100f;
    [Header("Body part control")]
    [SerializeField] private Bodypart[] bodyparts = new Bodypart[] {
        new Bodypart("head", 2f),
        new Bodypart("body", 1f),
        new Bodypart("right_arm", 1.2f),
        new Bodypart("left_arm", 1.2f),
        new Bodypart("right_leg", 1.2f),
        new Bodypart("left_leg", 1.2f)
    };
    [SerializeField]
    private float currentHealth;

    [Header("Tag-based defense")]
    public List<DamageTag> VulnerableTo = new();
    public List<DamageTag> ResistantTo = new();

    [Header("Animation")] 
    [SerializeField] private float ragdollAnimationTime;
    [SerializeField] private float animationDistance;
    [SerializeField] LayerMask layerMask;
    

    // Actions on damage events
    // float = amount of damage/heal
    public event Action<float> OnTakeDamage;
    public event Action OnDeath;
    public event Action<float> OnHealed;
    
    //Animation activation
    public Action<string, bool, float> OnAnimateCommand { get; set; }
    [SerializeField] private ComplexColliderHandle complexColliderHandle;

    private void Awake() {
        currentHealth = maxHealth;
        OnTakeDamage?.Invoke(0);
    }

    public void ApplyDamage(DamagePacket damagePacket)
    {
        if (currentHealth <= 0) return;

        // Check for the block
        if (TryGetComponent<IDefender>(out var defender))
        {
            if (defender.IsBlocking && !damagePacket.charged)
            {
                OnTakeDamage?.Invoke(0);
                return;
            }
        }

        float finalDamage = damagePacket.damage;

        // Applying effects and damage
        foreach (var effect in damagePacket.effects)
            effect.ApplyEffect(this);

        currentHealth -= finalDamage;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnTakeDamage?.Invoke(finalDamage);
        string anim = Vector3.Dot( transform.forward, damagePacket.forceApplied.normalized) > 0 ? "hit_front" : "hit_back";
        Animate(anim, speed: 2);

        // Death sequence
        if (currentHealth <= 0)
        {
            OnDeath?.Invoke();
            if (complexColliderHandle != null)
            {
                complexColliderHandle.ActivateGravity();
                // Apply force to correct body part
                Rigidbody rbBodyPart = null;
                if(damagePacket.bodyPart != null)
                    rbBodyPart = damagePacket.bodyPart;
                if (Physics.Raycast(
                        damagePacket.collisionPoint,
                        damagePacket.forceApplied.normalized,
                        out RaycastHit hit,
                        animationDistance,
                        layerMask))
                {
                    rbBodyPart = hit.collider?.GetComponent<Rigidbody>();
                }
                complexColliderHandle.ActivateRagdoll();
                //rbBodyPart?.AddForce(damagePacket.forceApplied, ForceMode.Impulse);
            }
        }
    }


    // Apply pure damage for effects
    public void ApplyPureDamage(float amount)
    {
        if (currentHealth <= 0) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);
        OnTakeDamage?.Invoke(amount);

        if (currentHealth <= 0)
            OnDeath?.Invoke();
    }

    // Heals some amount of health
    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealed?.Invoke(amount);
    }

    // Restore all health
    public void RestoreHealth()
    {
        currentHealth = maxHealth;
    }
    
    //Animate entity if needed
    void Animate(string anim, bool loop = false, float speed = 1)
    {
        OnAnimateCommand?.Invoke(anim, loop, speed);
    }

    public float GetHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
}

[System.Serializable]
public class Bodypart
{
    public string name;
    public float damageMultiplyer;
    public Bodypart(string name, float damageMultiplyer)
    {
        this.name = name;
        this.damageMultiplyer = damageMultiplyer;
    }
}