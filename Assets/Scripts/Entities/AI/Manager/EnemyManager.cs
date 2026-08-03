using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single owner of the AI population: it knows every live enemy and every enemy class asset, makes
/// and destroys them, initializes them, and is the only route by which events reach them.
///
/// The registry is static so an enemy can register from OnEnable without depending on the manager
/// existing yet or on script execution order -- broadcasts simply reach whoever is registered at
/// the time. The instance side holds the scene-authored data (class configs, spawn points).
/// </summary>
[DisallowMultipleComponent]
public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance { get; private set; }

    [Header("Enemy classes")]
    [Tooltip("Class assets this manager can spawn, looked up by EnemyConfig.className.")]
    [SerializeField] private List<EnemyConfig> configs = new();

    [Header("Scene enemies")]
    [Tooltip("Enemies already placed in the scene. Filled automatically at startup from whatever " +
             "registers itself; entries added by hand are initialized on startup too.")]
    [SerializeField] private List<Enemy> sceneEnemies = new();

    private static readonly List<Enemy> active = new();

    /// <summary>Every enemy currently alive and enabled.</summary>
    public static IReadOnlyList<Enemy> Active => active;

    public IReadOnlyList<EnemyConfig> Configs => configs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{name}] A second EnemyManager was found; destroying this one.", this);
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        InitializeSceneEnemies();
    }

    /// <summary>
    /// Brings every scene-placed enemy online. Enemies also self-initialize in their own Start, so
    /// this is about the manager owning the pass when it needs to (re-classing a group, or
    /// re-initializing after a config change), not about them being dead without it.
    /// </summary>
    public void InitializeSceneEnemies()
    {
        for (int i = 0; i < active.Count; i++)
        {
            var enemy = active[i];
            if (enemy == null) continue;

            if (!sceneEnemies.Contains(enemy))
                sceneEnemies.Add(enemy);
        }
    }

    // ---- Registry -----------------------------------------------------------

    public static void Register(Enemy enemy)
    {
        if (enemy == null || active.Contains(enemy))
            return;

        active.Add(enemy);
    }

    public static void Unregister(Enemy enemy)
    {
        if (enemy == null) return;
        active.Remove(enemy);

        if (Instance != null)
            Instance.sceneEnemies.Remove(enemy);
    }

    // ---- Spawning -----------------------------------------------------------

    public EnemyConfig FindConfig(string className)
    {
        for (int i = 0; i < configs.Count; i++)
            if (configs[i] != null && configs[i].className == className)
                return configs[i];

        return null;
    }

    /// <summary>Creates an enemy of a class by name. Returns null if the class or its prefab is missing.</summary>
    public Enemy Spawn(string className, Vector3 position, Quaternion rotation)
    {
        var config = FindConfig(className);
        if (config == null)
        {
            Debug.LogWarning($"[{name}] No EnemyConfig named '{className}'.", this);
            return null;
        }

        return Spawn(config, position, rotation);
    }

    public Enemy Spawn(EnemyConfig config, Vector3 position, Quaternion rotation)
    {
        if (config == null || config.prefab == null)
        {
            Debug.LogWarning($"[{name}] Enemy config '{(config != null ? config.className : "null")}' has no prefab.", this);
            return null;
        }

        var enemy = Instantiate(config.prefab, position, rotation);

        // Initialize explicitly rather than letting the instance's own Start do it, so the spawned
        // enemy is fully configured (team, ranges) the moment this method returns and can be
        // commanded straight away.
        enemy.Initialize(config);

        if (!sceneEnemies.Contains(enemy))
            sceneEnemies.Add(enemy);

        return enemy;
    }

    public void Despawn(Enemy enemy)
    {
        if (enemy == null) return;

        Unregister(enemy);
        Destroy(enemy.gameObject);
    }

    public void DespawnAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            Despawn(active[i]);
    }

    // ---- Command bus --------------------------------------------------------

    /// <summary>
    /// Delivers an event to whichever enemies its scope selects. Static so anything can raise an
    /// event without holding a manager reference -- including an enemy's own strategy calling out
    /// what it just saw.
    /// </summary>
    public static void Broadcast(EnemyCommand command)
    {
        for (int i = 0; i < active.Count; i++)
        {
            var enemy = active[i];
            if (enemy == null || !enemy.IsAlive)
                continue;

            // Never deliver a shout back to whoever shouted it.
            if (command.source != null &&
                (command.source == enemy.transform || command.source.IsChildOf(enemy.transform)))
            {
                continue;
            }

            if (!Matches(enemy, command))
                continue;

            enemy.ReceiveCommand(command);
        }
    }

    private static bool Matches(Enemy enemy, EnemyCommand command)
    {
        switch (command.scope)
        {
            case EnemyCommandScope.Radius:
                return Vector3.Distance(enemy.transform.position, command.origin) <= command.radius;

            case EnemyCommandScope.RadioFrequency:
                return enemy.RadioFrequency == command.radioFrequency;

            default:
                return true;
        }
    }
}
