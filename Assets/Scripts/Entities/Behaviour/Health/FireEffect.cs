using UnityEngine;

[CreateAssetMenu(fileName = "FireEffect", menuName = "Combat/Effects/Fire")]
public class FireEffect : DamageEffect
{

    public float damagePerSecond = 2f;
    public float duration = 5f;

    public override void ApplyEffect(EntityHealth target)
    {
        target.StartCoroutine(BurnRoutine(target));
    }

    private System.Collections.IEnumerator BurnRoutine(EntityHealth target)
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
