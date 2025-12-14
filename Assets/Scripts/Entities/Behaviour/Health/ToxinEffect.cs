using UnityEngine;

[CreateAssetMenu(fileName = "ToxinEffect", menuName = "Combat/Effects/Toxin")]
public class ToxinEffect : DamageEffect
{
    public float damagePerSecond = 2f;
    public float duration = 5f;

    public override void ApplyEffect(EntityHealth target)
    {
        target.StartCoroutine(ToxinRoutine(target));
    }

    private System.Collections.IEnumerator ToxinRoutine(EntityHealth target)
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
