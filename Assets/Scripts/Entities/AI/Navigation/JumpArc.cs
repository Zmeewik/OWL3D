using UnityEngine;

/// <summary>
/// The ballistic arc an entity flies when it jumps, solved once and shared by everything that needs
/// it: <see cref="JumpLinkMap"/> tests the arc against geometry while baking, and
/// <see cref="EnemyMovementSystem"/> launches the rigidbody along it at runtime. Keeping the maths
/// in one place is what stops the bake from approving a link the mover then flies differently.
/// </summary>
public static class JumpArc
{
    /// <summary>
    /// Extra height the arc peaks above whichever end is higher.
    ///
    /// This has to be generous, not merely positive. The wall of a ledge stands immediately in front
    /// of the takeoff point (the NavMesh rim is only inset from it by the bake's agent radius), so a
    /// arc that reaches its apex halfway along the jump is still well below the ledge top when it
    /// arrives at the wall and simply slams into the face: measured on a 2.55m climb, the entity was
    /// at 1.75m when it reached a wall whose top is 2.22m, lost all horizontal velocity to the
    /// collision and dropped straight back down. Peaking high early -- which also slows the
    /// horizontal component, since it stretches the flight time the distance is divided by -- is
    /// what actually clears the lip.
    /// </summary>
    public const float ApexClearance = 2f;

    /// <summary>Floor on the apex, so a flat hop still leaves the ground properly.</summary>
    public const float MinimumApex = 1.2f;

    /// <summary>
    /// Launch velocity and flight time to travel from <paramref name="takeoff"/> to
    /// <paramref name="landing"/> under gravity.
    /// </summary>
    public static void Solve(Vector3 takeoff, Vector3 landing, out Vector3 velocity, out float duration)
    {
        Vector3 flatDelta = new(landing.x - takeoff.x, 0f, landing.z - takeoff.z);
        float horizontalDistance = flatDelta.magnitude;
        float rise = landing.y - takeoff.y;

        float gravity = Mathf.Abs(Physics.gravity.y);
        float apex = Mathf.Max(MinimumApex, rise + ApexClearance);

        float timeUp = Mathf.Sqrt(2f * apex / gravity);
        float timeDown = Mathf.Sqrt(2f * Mathf.Max(0.05f, apex - rise) / gravity);
        duration = timeUp + timeDown;

        Vector3 horizontalVelocity = horizontalDistance > 0.0001f
            ? flatDelta / horizontalDistance * (horizontalDistance / duration)
            : Vector3.zero;

        velocity = horizontalVelocity + Vector3.up * (gravity * timeUp);
    }

    /// <summary>Position along the arc <paramref name="time"/> seconds after launch.</summary>
    public static Vector3 Sample(Vector3 takeoff, Vector3 velocity, float time)
    {
        float gravity = Mathf.Abs(Physics.gravity.y);
        return takeoff + velocity * time + Vector3.up * (-0.5f * gravity * time * time);
    }
}
