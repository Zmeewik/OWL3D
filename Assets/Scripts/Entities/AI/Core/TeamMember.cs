using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks an entity as belonging to a <see cref="Team"/> and makes it discoverable by enemy vision.
/// Goes on the player and on every AI entity alike -- the player needs one too, or no AI can ever
/// classify them as a hostile.
///
/// Keeps a static registry of live members so <see cref="EnemyVisionSystem"/> can enumerate
/// candidates without a per-frame FindObjectsOfType (which is expensive and would also pick up
/// entities that aren't valid targets at all).
/// </summary>
[DisallowMultipleComponent]
public class TeamMember : MonoBehaviour
{
    /// <summary>Which point on a body a vision ray is aimed at.</summary>
    public enum AimPoint { Center, Head, Feet }

    [Header("Allegiance")]
    public Team team = Team.Neutral;

    [Tooltip("Radio channel this entity listens on. EnemyManager can address a command to everyone " +
             "sharing a frequency, regardless of distance.")]
    public int radioFrequency = 0;

    [Header("Aim points (optional)")]
    [Tooltip("Explicit head/feet markers for vision rays. Left empty, they're derived from the " +
             "entity's collider bounds instead.")]
    [SerializeField] private Transform headPoint;
    [SerializeField] private Transform feetPoint;

    [SerializeField] private EntityHealth health;
    [SerializeField] private new Collider collider;

    private static readonly List<TeamMember> all = new();
    public static IReadOnlyList<TeamMember> All => all;

    public EntityHealth Health => health;

    /// <summary>Dead entities stay registered (their corpse still exists) but stop being targetable.</summary>
    public bool IsAlive => health == null || health.GetHealth() > 0f;

    private void Awake()
    {
        if (health == null)
            health = GetComponent<EntityHealth>();
        if (collider == null)
            collider = GetComponent<Collider>();
    }

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    /// <summary>
    /// World position a vision ray should be aimed at. Falls back to collider bounds when no
    /// explicit marker is wired, so this works on any entity without extra scene setup.
    /// </summary>
    public Vector3 GetAimPoint(AimPoint point)
    {
        switch (point)
        {
            case AimPoint.Head:
                if (headPoint != null) return headPoint.position;
                return collider != null
                    ? new Vector3(collider.bounds.center.x, collider.bounds.max.y - 0.1f, collider.bounds.center.z)
                    : transform.position + Vector3.up * 1.7f;

            case AimPoint.Feet:
                if (feetPoint != null) return feetPoint.position;
                return collider != null
                    ? new Vector3(collider.bounds.center.x, collider.bounds.min.y + 0.1f, collider.bounds.center.z)
                    : transform.position;

            default:
                return collider != null ? collider.bounds.center : transform.position + Vector3.up;
        }
    }
}
