using UnityEngine;

[CreateAssetMenu(fileName = "New Damage Profile", menuName = "Combat/Damage Profile")]
public class DamageProfile : ScriptableObject
{
    public float baseDamage = 10f;
    public DamageTag[] tags;
    public DamageEffect[] effects;
}