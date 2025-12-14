using System;
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

    

    // Actions on damage events
    // float = amount of damage/heal
    public event Action<float> OnTakeDamage;
    public event Action OnDeath;
    public event Action<float> OnHealed;
    
    //Animation activation
    public Action<string, string, bool> OnAnimateCommand { get; set; }
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

        // Applying effects
        foreach (var effect in damagePacket.effects)
            effect.ApplyEffect(this);

        currentHealth -= finalDamage;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnTakeDamage?.Invoke(finalDamage);
        string anim = UnityEngine.Random.Range(0, 1) == 0 ? "E_Robot_Boxer_GetDamage1" : "E_Robot_Boxer_GetDamage2";
        Animate(anim);
        if (currentHealth <= 0)
        {
            OnDeath?.Invoke();
            complexColliderHandle.ActivateRagdoll();
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
    void Animate(string anim,  string part = "", bool loop = false)
    {
        OnAnimateCommand?.Invoke(anim, part, loop);
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