using UnityEngine;

[CreateAssetMenu(fileName = "New DamageTag", menuName = "Combat/Damage Tag List")]
public class DamageTags : ScriptableObject
{
    [Header("Damage tags")]
    public DamageTag[] classes;
    public DamageTag[] impact;
    public DamageTag[] element;

    [Header("Damage effects")]
    public DamageEffect[] effects;
}