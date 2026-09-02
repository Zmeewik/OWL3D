using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A physical alarm column. Triggered through the existing <see cref="Mechanism"/>/<see cref="IInteractable"/>
/// interaction pattern (drop this object in as a Mechanism's actionImplemention), raising the alarm
/// broadcasts <see cref="EnemyCommandType.AlarmRaised"/> to every affected team on the level and
/// spawns whatever reinforcements are configured at its spawn points.
/// </summary>
[DisallowMultipleComponent]
public class AlarmPost : MonoBehaviour, IInteractable
{
    [Header("Response")]
    [Tooltip("Enemy teams that react to this post. Player and Neutral don't need to be listed here.")]
    [SerializeField] private List<Team> affectedTeams = new() { Team.Security };

    [Tooltip("How wide a ring responders spread out in around the post.")]
    [SerializeField] private float rallyRadius = 3f;

    [Header("Reinforcements")]
    [Tooltip("Spawn points activated when this alarm is raised.")]
    [SerializeField] private List<AlarmSpawnPoint> spawnPoints = new();

    [Tooltip("Off: the post can be triggered again (a lever). On: only the first trigger does anything.")]
    [SerializeField] private bool oneShot = false;

    private bool triggered;

    public void Activate(Collider goal) => Raise();

    public void Raise()
    {
        if (oneShot && triggered)
            return;

        triggered = true;

        EnemyManager.Broadcast(EnemyCommand.AlarmRaised(transform.position, affectedTeams, rallyRadius));

        var manager = EnemyManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning($"[{name}] Alarm raised with no EnemyManager in the scene -- no reinforcements spawned.", this);
            return;
        }

        for (int i = 0; i < spawnPoints.Count; i++)
            spawnPoints[i]?.Spawn(manager);
    }
}
