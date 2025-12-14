using UnityEngine;

public abstract class DamageEffect : ScriptableObject
{
    public abstract void ApplyEffect(EntityHealth target);
}

