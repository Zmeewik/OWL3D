using UnityEngine;

/// <summary>
/// Per-class tuning for an enemy type (a guard, a bandit, ...). Held by <see cref="EnemyManager"/>
/// and pushed into an entity's systems on initialization, so a whole class can be retuned in one
/// asset instead of per scene instance.
/// </summary>
[CreateAssetMenu(fileName = "EnemyConfig", menuName = "OWL/AI/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    [Header("Identity")]
    public string className = "Security";

    [Tooltip("Prefab spawned by EnemyManager.Spawn for this class.")]
    public Enemy prefab;

    public Team team = Team.Security;

    [Tooltip("Radio channel members of this class listen on by default.")]
    public int radioFrequency = 1;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float acceleration = 25f;
    public float stoppingDistance = 0.6f;

    [Header("Avoidance")]
    [Tooltip("Extra cushion on top of both entities' actual physical radii (which scale with each " +
             "entity's transform, so this stays correct at any size).")]
    public float avoidanceMargin = 1.5f;
    [Tooltip("How hard the push-away steer is at zero distance between two entities, tapering to 0 at the effective radius.")]
    public float avoidanceStrength = 3f;

    [Header("Vision")]
    public float viewDistance = 25f;
    [Range(1f, 360f)] public float viewAngle = 110f;
    [Tooltip("How long a target must stay in view before this class reacts to it.")]
    public float timeToSpot = 0.6f;
    [Tooltip("How long a target may stay out of view before this class gives up on it.")]
    public float timeToLose = 2.5f;

    [Header("Combat")]
    public float rangedRange = 18f;
    public float meleeRange = 2.2f;
    public float attackInterval = 1.1f;

    [Tooltip("Frames of wind-up before a shot or strike actually goes out, so the hit lands on the " +
             "animation's contact frame. Clips are authored at 24 fps.")]
    [Min(0f)] public float attackDelayFrames = 10f;

    [Tooltip("Spread cone in degrees pushed onto this class's weapons. Negative keeps whatever the " +
             "weapon prefab was set to.")]
    public float weaponSpreadDegrees = 3f;

    [Header("Aim distribution")]
    [Tooltip("Relative chance of aiming at each body part, rolled per shot.")]
    [Min(0f)] public float aimHeadWeight = 0.2f;
    [Min(0f)] public float aimBodyWeight = 0.6f;
    [Min(0f)] public float aimLegsWeight = 0.2f;

    [Header("Communication")]
    [Tooltip("How far a spotted-target shout carries to nearby allies.")]
    public float alertRadius = 20f;

    [Header("Social")]
    [Tooltip("How close this class walks to a friendly before interacting with them.")]
    public float interactRange = 2.5f;
}
