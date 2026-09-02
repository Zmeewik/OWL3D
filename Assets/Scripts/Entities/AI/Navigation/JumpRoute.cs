using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A planned route as the mover consumes it: a flat list of waypoints to steer through, where some
/// waypoints are launch points instead of ordinary corners.
///
/// Walking and jumping are deliberately kept in one sequence rather than split into typed steps. The
/// mover's job at any moment is "steer at the next waypoint"; the only extra thing it needs to know
/// is whether arriving at one means launching an arc rather than carrying on walking, which is one
/// flag. Anything richer would put route structure into a loop that only ever cares about the next
/// two metres.
/// </summary>
public sealed class JumpRoute
{
    public struct Waypoint
    {
        /// <summary>Where to steer.</summary>
        public Vector3 position;

        /// <summary>Arriving here means launching an arc rather than walking onward.</summary>
        public bool jumpFromHere;

        /// <summary>
        /// Launch velocity for the arc, already resolved for the direction being travelled. The mover
        /// applies it as-is: the arc was solved once at bake time, and nothing downstream re-derives
        /// it, so what is flown is exactly what was validated against the geometry.
        /// </summary>
        public Vector3 launchVelocity;

        /// <summary>Flight time of that arc. Same in both directions -- a reversed arc takes as long.</summary>
        public float flightTime;

        /// <summary>Where the arc lands. Meaningful only when <see cref="jumpFromHere"/>.</summary>
        public Vector3 landingPoint;
    }

    private readonly List<Waypoint> waypoints = new();

    /// <summary>Two waypoints closer than this describe the same spot; the second is dropped.</summary>
    private const float DuplicateEpsilonSqr = 0.0025f;

    public int Count => waypoints.Count;
    public Waypoint this[int index] => waypoints[index];
    public bool IsValid => waypoints.Count > 0;

    /// <summary>Predicted travel time in seconds -- the quantity this route won its comparison on.</summary>
    public float EstimatedTime { get; private set; }

    public int JumpCount { get; private set; }

    public void Clear()
    {
        waypoints.Clear();
        EstimatedTime = 0f;
        JumpCount = 0;
    }

    public void SetEstimatedTime(float seconds) => EstimatedTime = seconds;

    public void AddCorner(Vector3 position)
    {
        if (waypoints.Count > 0)
        {
            var last = waypoints[waypoints.Count - 1];
            // Never swallow a launch point: its flag is the only thing telling the mover to jump.
            if (!last.jumpFromHere && (last.position - position).sqrMagnitude < DuplicateEpsilonSqr)
                return;
        }

        waypoints.Add(new Waypoint { position = position });
    }

    public void AddCorners(Vector3[] corners, int count)
    {
        for (int i = 0; i < count; i++)
            AddCorner(corners[i]);
    }

    /// <summary>
    /// Appends the launch point and the spot it lands on. If the ground path already ended on the
    /// takeoff, that corner is promoted to the launch point rather than duplicated -- otherwise the
    /// mover would reach the corner, consume it as walked, and steer at a second identical point it
    /// is already standing on.
    /// </summary>
    public void AddJump(in JumpLink link, bool reverse)
    {
        Vector3 from = link.From(reverse);
        Vector3 to = link.To(reverse);

        var launch = new Waypoint
        {
            position = from,
            jumpFromHere = true,
            launchVelocity = link.VelocityFor(reverse),
            flightTime = link.duration,
            landingPoint = to,
        };

        if (waypoints.Count > 0)
        {
            var last = waypoints[waypoints.Count - 1];
            if (!last.jumpFromHere && (last.position - from).sqrMagnitude < DuplicateEpsilonSqr)
                waypoints[waypoints.Count - 1] = launch;
            else
                waypoints.Add(launch);
        }
        else
        {
            waypoints.Add(launch);
        }

        waypoints.Add(new Waypoint { position = to });
        JumpCount++;
    }

    /// <summary>
    /// Turns an ordinary corner into a launch point. Used for stretches the NavMesh bridges with its
    /// own off-mesh links, which arrive as plain corners with nothing marking them as a gap.
    /// </summary>
    public void ConvertToJump(int index, in JumpLink link)
    {
        var waypoint = waypoints[index];
        if (waypoint.jumpFromHere)
            return;

        waypoint.jumpFromHere = true;
        waypoint.launchVelocity = link.ForwardVelocity;
        waypoint.flightTime = link.duration;
        waypoint.landingPoint = link.landing;
        waypoints[index] = waypoint;
        JumpCount++;
    }

    /// <summary>
    /// Distance still to travel from <paramref name="position"/>, following the route from
    /// <paramref name="fromIndex"/> onward. Jump segments are counted by their chord, which understates
    /// the arc slightly -- fine for the "am I there yet" checks this feeds, and far closer than the
    /// straight line to the destination it replaces, which ignored every detour and every metre of
    /// height between here and there.
    /// </summary>
    public float RemainingDistance(Vector3 position, int fromIndex)
    {
        if (fromIndex >= waypoints.Count)
            return 0f;

        float total = Vector3.Distance(position, waypoints[fromIndex].position);
        for (int i = fromIndex + 1; i < waypoints.Count; i++)
            total += Vector3.Distance(waypoints[i - 1].position, waypoints[i].position);

        return total;
    }

    /// <summary>Where the route ends. Zero when it holds nothing.</summary>
    public Vector3 EndPoint => waypoints.Count > 0 ? waypoints[waypoints.Count - 1].position : Vector3.zero;
}
