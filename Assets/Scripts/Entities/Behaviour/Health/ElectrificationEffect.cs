using UnityEngine;

[CreateAssetMenu(fileName = "ElectrificationEffect", menuName = "Combat/Effects/Electrify")]
public class ElectrificationEffect : DamageEffect
{
    public float damagePerSecond = 2f;
    public float duration = 5f;

    public override void ApplyEffect(EntityHealth target)
    {
        target.StartCoroutine(ElectrifyRoutine(target));
    }

    private System.Collections.IEnumerator ElectrifyRoutine(EntityHealth target)
    {
        float time = 0;
        while (time < duration && target.GetHealth() > 0)
        {
            target.ApplyPureDamage(damagePerSecond * Time.deltaTime);
            time += Time.deltaTime;
            yield return null;
        }
    }
}