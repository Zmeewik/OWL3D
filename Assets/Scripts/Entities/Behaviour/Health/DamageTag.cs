using UnityEngine;

[CreateAssetMenu(fileName = "New DamageTag", menuName = "Combat/Damage Tag")]
public class DamageTag : ScriptableObject
{
    public string tagName;
    public Color color;
    public float resistancePenalty;
    public float vulnerableBonus;
}