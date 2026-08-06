using System;
using UnityEngine;

/// <summary>
/// The entity's eyes. Sweeps every registered <see cref="TeamMember"/> and reports the first one it
/// can actually see, after holding it in view long enough to react.
///
/// Visibility is three rays -- body centre, head and feet -- rather than one, so a target isn't
/// declared invisible just because a crate happens to cover its middle, and isn't declared visible
/// from a single lucky pixel of head poking over cover. Any one ray landing counts as seen.
/// </summary>
public class EnemyVisionSystem : EnemySystem
{
    [Header("References")]
    [Tooltip("Where sight is cast from. Defaults to the entity root, but should be a head bone.")]
    [SerializeField] private Transform eye;

    [Header("Cone")]
    [SerializeField] private float viewDistance = 25f;
    [Tooltip("Total field of view in degrees, centred on the eye's forward axis.")]
    [SerializeField, Range(1f, 360f)] private float viewAngle = 110f;

    [Header("Reaction")]
    [Tooltip("How long a target must stay in view before it counts as spotted.")]
    [SerializeField] private float timeToSpot = 0.6f;
    [Tooltip("How long a spotted target may stay out of view before it counts as lost.")]
    [SerializeField] private float timeToLose = 2.5f;

    [Header("Occlusion")]
    [Tooltip("What blocks sight. Should be world geometry only -- never the Entity or BodyParts layers, " +
             "or targets would block the rays aimed at them.")]
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("Last-Known-Position Marker")]
    [Tooltip("Show a translucent marker at LastKnownPosition while heading to check out a target that's out of sight.")]
    [SerializeField] private bool showLastKnownPositionMarker = true;
    [SerializeField] private Color markerColor = new(0.2f, 0.85f, 1f, 0.35f);

    private Transform marker;

    /// <summary>Raised once when a target has been held in view for <see cref="timeToSpot"/>.</summary>
    public event Action<TeamMember> OnTargetSpotted;

    /// <summary>Raised once when the current target has been out of view for <see cref="timeToLose"/>.</summary>
    public event Action<TeamMember> OnTargetLost;

    public TeamMember CurrentTarget { get; private set; }
    public Vector3 LastKnownPosition { get; private set; }
    public bool HasVisibleTarget { get; private set; }

    private TeamMember candidate;
    private float candidateVisibleTime;
    private float targetUnseenTime;

    private Team SelfTeam => enemy != null && enemy.Self != null ? enemy.Self.team : Team.Neutral;

    public override void Initialize(Enemy owner)
    {
        base.Initialize(owner);
        if (eye == null)
            eye = owner.transform;

        if (showLastKnownPositionMarker)
            BuildMarker();
    }

    private void OnDestroy()
    {
        if (marker != null)
            Destroy(marker.gameObject);
    }

    /// <summary>
    /// A translucent "ghost" standing at LastKnownPosition -- self-built the same way DebugHpBar
    /// builds its world-space canvas, so nothing needs authoring in the scene. Left unparented: it
    /// marks a fixed world point the entity is walking toward, not something that should follow the
    /// entity itself around like a nameplate would.
    /// </summary>
    private void BuildMarker()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = $"LastKnownPositionMarker ({enemy.name})";

        // Purely visual -- must never be hit by a shot, block a vision ray, or get sampled by the
        // NavMesh probe.
        var col = go.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        go.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);

        var renderer = go.GetComponent<Renderer>();
        renderer.material = BuildTransparentMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        marker = go.transform;
        marker.gameObject.SetActive(false);
    }

    /// <summary>
    /// Standard "make a URP Lit material transparent from script" recipe -- setting a colour's alpha
    /// alone does nothing in URP without also flipping the surface type and blend mode/queue to match.
    /// </summary>
    private Material BuildTransparentMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
        mat.color = markerColor;
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }

    /// <summary>Shown exactly while this entity has a target it's no longer looking at -- i.e. the window ChaseAndAttackAction spends walking to LastKnownPosition instead of the live one.</summary>
    private void UpdateMarker()
    {
        if (marker == null) return;

        bool investigating = CurrentTarget != null && !HasVisibleTarget;
        marker.gameObject.SetActive(investigating);
        if (investigating)
            marker.position = LastKnownPosition;
    }

    public void ConfigureFrom(EnemyConfig config)
    {
        if (config == null) return;
        viewDistance = config.viewDistance;
        viewAngle = config.viewAngle;
        timeToSpot = config.timeToSpot;
        timeToLose = config.timeToLose;
    }

    public override void TickSystem(float deltaTime)
    {
        // Track the existing target first: keeping something already spotted matters more than
        // scanning for a new one, and it must survive going briefly behind cover.
        if (CurrentTarget != null)
        {
            if (CurrentTarget.IsAlive && CanSee(CurrentTarget))
            {
                HasVisibleTarget = true;
                targetUnseenTime = 0f;
                LastKnownPosition = CurrentTarget.transform.position;
            }
            else
            {
                HasVisibleTarget = false;
                targetUnseenTime += deltaTime;
                if (targetUnseenTime >= timeToLose || !CurrentTarget.IsAlive)
                {
                    var lost = CurrentTarget;
                    CurrentTarget = null;
                    targetUnseenTime = 0f;
                    OnTargetLost?.Invoke(lost);
                }
            }

            UpdateMarker();
            return;
        }

        ScanForNewTarget(deltaTime);
        UpdateMarker();
    }

    private void ScanForNewTarget(float deltaTime)
    {
        TeamMember best = FindClosestVisible();

        if (best == null)
        {
            candidate = null;
            candidateVisibleTime = 0f;
            HasVisibleTarget = false;
            return;
        }

        HasVisibleTarget = true;

        // Switching candidate restarts the timer: reacting takes as long as it takes, per target.
        if (best != candidate)
        {
            candidate = best;
            candidateVisibleTime = 0f;
        }

        candidateVisibleTime += deltaTime;
        if (candidateVisibleTime < timeToSpot)
            return;

        CurrentTarget = candidate;
        LastKnownPosition = candidate.transform.position;
        targetUnseenTime = 0f;
        candidate = null;
        candidateVisibleTime = 0f;

        OnTargetSpotted?.Invoke(CurrentTarget);
    }

    private TeamMember FindClosestVisible()
    {
        TeamMember best = null;
        float bestDistance = float.MaxValue;

        var members = TeamMember.All;
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null || member == enemy.Self || !member.IsAlive)
                continue;

            // Neutrals are visible but uninteresting -- skipping them here keeps the AI from
            // latching onto scenery-like entities and never noticing the hostile behind them.
            if (Teams.Relation(SelfTeam, member.team) == TeamRelation.Neutral)
                continue;

            float distance = Vector3.Distance(eye.position, member.transform.position);
            if (distance >= bestDistance || !CanSee(member))
                continue;

            best = member;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>In range, inside the cone, and at least one of the three body rays unobstructed.</summary>
    public bool CanSee(TeamMember member)
    {
        if (member == null || eye == null)
            return false;

        Vector3 toMember = member.GetAimPoint(TeamMember.AimPoint.Center) - eye.position;
        if (toMember.magnitude > viewDistance)
            return false;

        if (Vector3.Angle(eye.forward, toMember) > viewAngle * 0.5f)
            return false;

        return HasClearLine(member, TeamMember.AimPoint.Center)
            || HasClearLine(member, TeamMember.AimPoint.Head)
            || HasClearLine(member, TeamMember.AimPoint.Feet);
    }

    private bool HasClearLine(TeamMember member, TeamMember.AimPoint point)
    {
        Vector3 target = member.GetAimPoint(point);
        Vector3 origin = eye.position;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        if (distance < 0.01f)
            return true;

        // QueryTriggerInteraction.Ignore so the entity's own trigger hitboxes (and the target's)
        // can't count as walls.
        if (!Physics.Raycast(origin, direction / distance, out var hit, distance, obstacleMask,
                QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        // Something was hit -- it only blocks sight if it isn't the target itself.
        return hit.transform.IsChildOf(member.transform) || hit.transform == member.transform;
    }

    private void OnDrawGizmosSelected()
    {
        var from = eye != null ? eye : transform;
        Gizmos.color = Color.yellow;
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * from.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * from.forward;
        Gizmos.DrawRay(from.position, left * viewDistance);
        Gizmos.DrawRay(from.position, right * viewDistance);

        if (CurrentTarget == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(from.position, CurrentTarget.GetAimPoint(TeamMember.AimPoint.Center));
    }
}
