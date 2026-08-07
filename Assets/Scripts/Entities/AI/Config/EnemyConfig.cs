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
    public float avoidanceMargin = 2f;
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

    [Header("Searching")]
    [Tooltip("How long this class hunts around a spot after losing contact before giving up and idling.")]
    public float searchDuration = 20f;

    [Tooltip("Fraction of moveSpeed used while searching -- searching is a walk, not a chase.")]
    [Range(0.1f, 1f)] public float searchWalkSpeed = 0.45f;

    [Tooltip("How wide the side-to-side pacing around the search point is.")]
    public float searchSweepRadius = 4f;

    [Tooltip("How far back along an incoming shot this class walks to look for whoever fired it.")]
    public float shotInvestigateDistance = 12f;

    [Header("Ambient idle")]
    [Tooltip("How far from its post this class strolls while idling. Small on purpose -- a guard " +
             "shifts its footing around a post, it doesn't patrol a circle.")]
    public float ambientWanderRadius = 2.5f;

    [Tooltip("Fraction of moveSpeed used while milling about -- idling is a saunter.")]
    [Range(0.1f, 1f)] public float ambientWalkSpeed = 0.4f;

    [Tooltip("How close an ally has to be for this class to strike up a conversation with them.")]
    public float ambientChatRange = 6f;

    [Header("Communication")]
    [Tooltip("How far a spotted-target shout carries to nearby allies.")]
    public float alertRadius = 20f;
}
