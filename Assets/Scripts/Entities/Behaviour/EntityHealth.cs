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

        float finalDamage = damagePacket.damage * GetBodyPartMultiplier(damagePacket.bodyPart);

        // Applying effects and damage
        foreach (var effect in damagePacket.effects)
            effect.ApplyEffect(this);

        currentHealth -= finalDamage;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnTakeDamage?.Invoke(finalDamage);
        string anim = Vector3.Dot( transform.forward, damagePacket.forceApplied.normalized) > 0 ? AnimateCommand.HitFront : AnimateCommand.HitBack;
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

    // Auto-maps the hit rigidbody (ComplexColliderHandle names its hitbox bones e.g.
    // "Head", "Left_Shoulder", "Right_Thigh" -- see ComplexColliderHandle.CreateStandardHumanoid)
    // to one of the configured `bodyparts` multipliers by keyword, so no per-entity Inspector
    // wiring is needed. Falls back to "body" for the main collider or any unrecognized name.
    private float GetBodyPartMultiplier(Rigidbody bodyPart)
    {
        if (bodyPart == null)
            return FindMultiplier("body");

        string name = bodyPart.transform.name.ToLowerInvariant();
        print(name);
        bool isLeft = name.Contains("left");
        bool isRight = name.Contains("right");
        string side = isRight ? "right_" : isLeft ? "left_" : null;

        string generic = null;
        if (name.Contains("head"))
            generic = "head";
        else if (name.Contains("shoulder") || name.Contains("forearm") || name.Contains("hand") || name.Contains("arm"))
            generic = "arm";
        else if (name.Contains("thigh") || name.Contains("shin") || name.Contains("foot") || name.Contains("leg"))
            generic = "leg";

        if (generic == null)
            return FindMultiplier("body");

        // Side-specific entry first ("right_leg"), then the side-agnostic one ("leg"), then body.
        // That middle step is what lets a simplified rig resolve properly: the player's three
        // head/body/legs trigger boxes carry no side in their names, and without a generic entry
        // to fall back on a leg hit would silently collapse to the body multiplier.
        float multiplier;
        if (side != null && TryFindMultiplier(side + generic, out multiplier))
            return multiplier;
        if (TryFindMultiplier(generic, out multiplier))
            return multiplier;

        return FindMultiplier("body");
    }

    private bool TryFindMultiplier(string category, out float multiplier)
    {
        foreach (var part in bodyparts)
        {
            if (string.Equals(part.name, category, StringComparison.OrdinalIgnoreCase))
            {
                multiplier = part.damageMultiplyer;
                return true;
            }
        }

        multiplier = 1f;
        return false;
    }

    private float FindMultiplier(string category)
    {
        float multiplier;
        return TryFindMultiplier(category, out multiplier) ? multiplier : 1f;
    }
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