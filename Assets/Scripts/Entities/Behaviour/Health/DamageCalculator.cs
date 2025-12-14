using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "Damage Calculator", menuName = "Combat/Damage Calculator")]
public class DamageCalculator : ScriptableObject
{
    [Range(0f, 3f)] public static float baseMultiplier = 1f;
    public float vulnerableBonus = 1.5f;
    public float resistancePenalty = 0.5f;

    public static float CalculateDamage(DamageProfile profile, EntityHealth target)
    {
        float damage = profile.baseDamage * baseMultiplier;

        foreach (var tag in profile.tags)
        {
            if (target.VulnerableTo.Contains(tag))
                damage *= tag.vulnerableBonus;

            if (target.ResistantTo.Contains(tag))
                damage *= tag.resistancePenalty;
        }

        return damage;
    }
}