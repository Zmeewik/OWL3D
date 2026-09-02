using UnityEngine;

/// <summary>
/// A marker an <see cref="AlarmPost"/> spawns reinforcements at when raised. Deliberately dumb --
/// it just knows what to spawn and how many, the alarm post decides when.
/// </summary>
public class AlarmSpawnPoint : MonoBehaviour
{
    [Tooltip("Enemy class spawned here.")]
    [SerializeField] private EnemyConfig config;

    [Tooltip("How many to spawn at once.")]
    [Min(1)] [SerializeField] private int count = 1;

    [Tooltip("Random horizontal offset so multiple spawns here don't land on top of each other.")]
    [SerializeField] private float scatterRadius = 1.5f;

    public void Spawn(EnemyManager manager)
    {
        if (manager == null || config == null)
            return;

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * scatterRadius;
            Vector3 position = transform.position + new Vector3(offset.x, 0f, offset.y);
            manager.Spawn(config, position, transform.rotation);
        }
    }
}
