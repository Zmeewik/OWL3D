using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One AI entity. Owns nothing behavioural itself -- it wires together the capability systems and
/// the strategy that decides what to do with them, and drives their tick order explicitly so
/// "look, then decide, then act" is guaranteed rather than left to Unity's component update order.
/// </summary>
[DisallowMultipleComponent]
public class Enemy : MonoBehaviour
{
    [Header("Class")]
    [SerializeField] private EnemyConfig config;

    [Header("References (auto-filled when left empty)")]
    [SerializeField] private TeamMember self;
    [SerializeField] private EntityHealth health;
    [SerializeField] private EnemyStrategy strategy;

    private readonly List<EnemySystem> systems = new();
    private bool initialized;
    private bool dead;

    public EnemyConfig Config => config;
    public TeamMember Self => self;
    public EntityHealth Health => health;
    public EnemyStrategy Strategy => strategy;

    public EnemyMovementSystem Movement { get; private set; }
    public EnemyRotationSystem Rotation { get; private set; }
    public EnemyAttackSystem Attack { get; private set; }
    public EnemyVisionSystem Vision { get; private set; }
    public EnemyAnimationSystem Animation { get; private set; }

    public Team Team => self != null ? self.team : Team.Neutral;
    public int RadioFrequency => self != null ? self.radioFrequency : 0;
    public bool IsAlive => !dead && (health == null || health.GetHealth() > 0f);

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        EnemyManager.Register(this);
    }

    private void OnDisable()
    {
        EnemyManager.Unregister(this);
    }

    private void Start()
    {
        // Scene-placed enemies initialize themselves; spawned ones are initialized by the manager
        // before they're enabled, and skip this.
        if (!initialized)
            Initialize(config);
    }

    private void CacheReferences()
    {
        if (self == null) self = GetComponent<TeamMember>();
        if (health == null) health = GetComponent<EntityHealth>();
        if (strategy == null) strategy = GetComponentInChildren<EnemyStrategy>();

        systems.Clear();
        GetComponentsInChildren(true, systems);

        Movement = Find<EnemyMovementSystem>();
        Rotation = Find<EnemyRotationSystem>();
        Attack = Find<EnemyAttackSystem>();
        Vision = Find<EnemyVisionSystem>();
        Animation = Find<EnemyAnimationSystem>();
    }

    private T Find<T>() where T : EnemySystem
    {
        for (int i = 0; i < systems.Count; i++)
            if (systems[i] is T match)
                return match;
        return null;
    }

    /// <summary>
    /// Applies a class config and brings every system online. Safe to call again to re-class an
    /// entity at runtime (that's what manager-driven spawning does).
    /// </summary>
    public void Initialize(EnemyConfig enemyConfig)
    {
        CacheReferences();
        config = enemyConfig;

        if (config != null && self != null)
        {
            self.team = config.team;
            self.radioFrequency = config.radioFrequency;
        }

        // Config is pushed before Initialize so systems that cache derived values from it see the
        // final numbers.
        Movement?.ConfigureFrom(config);
        Vision?.ConfigureFrom(config);
        Attack?.ConfigureFrom(config);

        for (int i = 0; i < systems.Count; i++)
            systems[i].Initialize(this);

        if (strategy != null)
            strategy.Initialize(this);

        if (health != null)
            health.OnDeath += HandleDeath;

        initialized = true;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        dead = true;
        Movement?.Stop();
        Rotation?.ClearAim();
        strategy?.ClearActions();
        EnemyManager.Unregister(this);
    }

    private void Update()
    {
        if (!initialized || !IsAlive) return;

        float dt = Time.deltaTime;

        // Explicit order: perceive, then decide, then let the remaining systems act on that
        // decision within the same frame.
        Vision?.TickSystem(dt);
        strategy?.Tick(dt);

        for (int i = 0; i < systems.Count; i++)
        {
            if (systems[i] == Vision) continue;
            systems[i].TickSystem(dt);
        }
    }

    private void FixedUpdate()
    {
        if (!initialized || !IsAlive) return;

        float dt = Time.fixedDeltaTime;
        for (int i = 0; i < systems.Count; i++)
            systems[i].FixedTickSystem(dt);
    }

    /// <summary>Entry point for everything <see cref="EnemyManager"/> broadcasts.</summary>
    public void ReceiveCommand(EnemyCommand command)
    {
        if (!IsAlive || strategy == null) return;
        strategy.HandleCommand(command);
    }
}
