using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Baked map of "you can jump UP from here to there" links, and the runtime lookup enemies use to
/// find one.
///
/// This exists because Unity's own auto-generated off-mesh links cannot express an upward jump at
/// all: Drop Height only ever generates links *down* off a ledge, and Jump Distance only bridges a
/// horizontal gap between surfaces at roughly the same height. Nothing in the bake will ever let an
/// agent climb onto a raised platform, which is exactly what was needed -- a target standing on a
/// ledge was simply unreachable, so pathing returned PathPartial and the enemy ran around the base
/// of it forever.
///
/// Links are found by walking the NavMesh's own boundary edges rather than its vertices. A large
/// flat platform triangulates into just two triangles with four corner vertices, so anything driven
/// off the vertex list can only ever produce jump points at the corners of a surface. Sampling
/// along each boundary edge at a fixed <see cref="spacing"/> instead puts candidate takeoff points
/// evenly along the whole rim of every ledge.
///
/// Baking is an editor-time step (context menu below); at runtime the serialized links are dropped
/// into a uniform grid so an enemy only ever tests the handful of links in the cells around itself,
/// never the whole set.
/// </summary>
[DisallowMultipleComponent]
public class JumpLinkMap : MonoBehaviour
{
    [System.Serializable]
    public struct JumpLink
    {
        /// <summary>Standing spot on the lower surface, at the rim of the ledge.</summary>
        public Vector3 takeoff;

        /// <summary>Standing spot on the upper surface, inset from its rim so the landing isn't on the edge.</summary>
        public Vector3 landing;
    }

    [Header("Baking")]
    [Tooltip("Distance between candidate takeoff points sampled along each NavMesh boundary edge. " +
             "Smaller means a jump is available from more places along a ledge, at the cost of more links.")]
    [SerializeField, Min(0.25f)] private float spacing = 2f;

    [Tooltip("Highest climb a link may express. Anything taller isn't a jump the entity could make.")]
    [SerializeField] private float maxJumpHeight = 4f;

    [Tooltip("Smallest height difference worth a jump. Below this the agent can just walk or step up.")]
    [SerializeField] private float minJumpHeight = 1f;

    [Tooltip("Furthest horizontal distance a link may span.")]
    [SerializeField] private float maxJumpDistance = 4f;

    [Tooltip("How far past the upper rim the landing point is pushed, so the entity lands on the " +
             "platform rather than balanced on its very edge.")]
    [SerializeField] private float landingInset = 1.2f;

    [Tooltip("How far BACK from the lower rim the takeoff is pulled, away from the ledge. Jumping " +
             "with your back against a wall doesn't work: the entity's capsule is already touching " +
             "the ledge face at the rim, so physics cancels the horizontal launch velocity into it " +
             "and the jump goes straight up and drops back down. Standing back leaves room to be " +
             "airborne and above the lip by the time it arrives.")]
    [SerializeField] private float takeoffInset = 1.5f;

    [Tooltip("Geometry the jump arc must clear. The arc is sampled against this to reject links that " +
             "would fly through a wall instead of over a ledge.")]
    [SerializeField] private LayerMask obstructionMask = ~0;

    [Header("Runtime lookup")]
    [Tooltip("How far from itself an enemy will look for a usable link.")]
    [SerializeField] private float searchRadius = 6f;

    [Tooltip("How far above/below the enemy a link's takeoff may sit and still count as reachable from where it stands.")]
    [SerializeField] private float takeoffHeightTolerance = 1.5f;

    [Header("Baked data")]
    [SerializeField] private List<JumpLink> links = new();

    /// <summary>Scene-wide instance. Static so movement code can reach it without per-entity wiring.</summary>
    public static JumpLinkMap Instance { get; private set; }

    public IReadOnlyList<JumpLink> Links => links;

    private readonly Dictionary<Vector2Int, List<int>> grid = new();
    private float cellSize;

    private void OnEnable()
    {
        Instance = this;
        BuildGrid();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    // ---- Runtime lookup -----------------------------------------------------

    private void BuildGrid()
    {
        grid.Clear();
        cellSize = Mathf.Max(1f, searchRadius);

        for (int i = 0; i < links.Count; i++)
        {
            var cell = CellOf(links[i].takeoff);
            if (!grid.TryGetValue(cell, out var bucket))
            {
                bucket = new List<int>();
                grid[cell] = bucket;
            }
            bucket.Add(i);
        }
    }

    private Vector2Int CellOf(Vector3 p) =>
        new(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.z / cellSize));

    /// <summary>
    /// Nearest usable link for an entity standing at <paramref name="from"/> trying to reach
    /// <paramref name="destination"/>. Only links whose landing actually makes progress toward the
    /// destination qualify, so an entity never jumps a ledge that leads away from its target.
    ///
    /// Only the grid cells overlapping the search radius are examined -- the cost doesn't grow with
    /// the size of the map.
    /// </summary>
    public bool TryFindLink(Vector3 from, Vector3 destination, out JumpLink result)
    {
        result = default;
        if (links.Count == 0)
            return false;

        float currentDistance = Flat(destination - from).magnitude;
        float bestDistance = float.MaxValue;
        bool found = false;

        int range = Mathf.CeilToInt(searchRadius / cellSize);
        var center = CellOf(from);

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dz = -range; dz <= range; dz++)
            {
                if (!grid.TryGetValue(new Vector2Int(center.x + dx, center.y + dz), out var bucket))
                    continue;

                for (int b = 0; b < bucket.Count; b++)
                {
                    var link = links[bucket[b]];

                    // Must be a takeoff this entity could actually walk to and use -- one on a
                    // different storey directly overhead is not.
                    if (Mathf.Abs(link.takeoff.y - from.y) > takeoffHeightTolerance)
                        continue;

                    float toTakeoff = Flat(link.takeoff - from).magnitude;
                    if (toTakeoff > searchRadius || toTakeoff >= bestDistance)
                        continue;

                    // Jumping has to be worth it: the landing must leave us closer to the target
                    // than standing still does.
                    if (Flat(destination - link.landing).magnitude >= currentDistance)
                        continue;

                    bestDistance = toTakeoff;
                    result = link;
                    found = true;
                }
            }
        }

        return found;
    }

    // ---- Baking -------------------------------------------------------------

    [ContextMenu("Rebuild Jump Links")]
    public void Rebuild()
    {
        links.Clear();

        var samples = SampleBoundaryEdges();
        var sampleGrid = BuildSampleGrid(samples, maxJumpDistance);

        for (int i = 0; i < samples.Count; i++)
        {
            if (TryFindLanding(samples[i], samples, sampleGrid, out var takeoff, out var landing))
                links.Add(new JumpLink { takeoff = takeoff, landing = landing });
        }

        BuildGrid();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"[{name}] Baked {links.Count} jump links from {samples.Count} boundary samples.", this);
    }

    /// <summary>
    /// Points spread evenly along every open edge of the NavMesh -- the rim of each walkable
    /// surface, which is precisely where a jump can start or end.
    ///
    /// An edge belongs to the outline exactly when a single triangle uses it; interior edges are
    /// shared by two. Vertices are welded by position first because the triangulation repeats the
    /// same corner once per triangle touching it, which would otherwise make every edge look
    /// unshared.
    /// </summary>
    private List<Vector3> SampleBoundaryEdges()
    {
        var triangulation = NavMesh.CalculateTriangulation();
        var vertices = triangulation.vertices;
        var indices = triangulation.indices;

        var weldedIndex = new Dictionary<Vector3Int, int>();
        var welded = new List<Vector3>();
        var remap = new int[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            var key = new Vector3Int(
                Mathf.RoundToInt(vertices[i].x * 100f),
                Mathf.RoundToInt(vertices[i].y * 100f),
                Mathf.RoundToInt(vertices[i].z * 100f));

            if (!weldedIndex.TryGetValue(key, out int index))
            {
                index = welded.Count;
                welded.Add(vertices[i]);
                weldedIndex[key] = index;
            }

            remap[i] = index;
        }

        var edgeUse = new Dictionary<long, int>();
        for (int t = 0; t < indices.Length; t += 3)
        {
            AddEdge(edgeUse, remap[indices[t]], remap[indices[t + 1]]);
            AddEdge(edgeUse, remap[indices[t + 1]], remap[indices[t + 2]]);
            AddEdge(edgeUse, remap[indices[t + 2]], remap[indices[t]]);
        }

        var samples = new List<Vector3>();
        var seen = new HashSet<Vector3Int>();

        foreach (var pair in edgeUse)
        {
            if (pair.Value != 1)
                continue;

            Vector3 a = welded[(int)(pair.Key >> 32)];
            Vector3 b = welded[(int)(pair.Key & 0xFFFFFFFF)];

            int steps = Mathf.Max(1, Mathf.FloorToInt(Vector3.Distance(a, b) / spacing));
            for (int s = 0; s <= steps; s++)
            {
                Vector3 p = Vector3.Lerp(a, b, s / (float)steps);

                var key = new Vector3Int(
                    Mathf.RoundToInt(p.x / spacing),
                    Mathf.RoundToInt(p.y / spacing),
                    Mathf.RoundToInt(p.z / spacing));

                if (seen.Add(key))
                    samples.Add(p);
            }
        }

        return samples;
    }

    private static void AddEdge(Dictionary<long, int> edgeUse, int a, int b)
    {
        // Key both directions the same way, so the two triangles sharing an edge agree on it.
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        edgeUse.TryGetValue(key, out int count);
        edgeUse[key] = count + 1;
    }

    private Dictionary<Vector2Int, List<int>> BuildSampleGrid(List<Vector3> samples, float cell)
    {
        var result = new Dictionary<Vector2Int, List<int>>();
        for (int i = 0; i < samples.Count; i++)
        {
            var key = new Vector2Int(Mathf.FloorToInt(samples[i].x / cell), Mathf.FloorToInt(samples[i].z / cell));
            if (!result.TryGetValue(key, out var bucket))
            {
                bucket = new List<int>();
                result[key] = bucket;
            }
            bucket.Add(i);
        }
        return result;
    }

    /// <summary>
    /// Best upward landing for a takeoff point: the closest higher rim sample within reach that
    /// isn't already walkable from there, inset onto its platform, with a clear arc over the wall
    /// between the two.
    /// </summary>
    private bool TryFindLanding(Vector3 rim, List<Vector3> samples,
                                Dictionary<Vector2Int, List<int>> sampleGrid,
                                out Vector3 takeoffPoint, out Vector3 landing)
    {
        takeoffPoint = rim;
        landing = default;

        float bestDistance = float.MaxValue;
        bool found = false;

        Vector3 takeoff = rim;
        var center = new Vector2Int(Mathf.FloorToInt(takeoff.x / maxJumpDistance),
                                    Mathf.FloorToInt(takeoff.z / maxJumpDistance));

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!sampleGrid.TryGetValue(new Vector2Int(center.x + dx, center.y + dz), out var bucket))
                    continue;

                for (int b = 0; b < bucket.Count; b++)
                {
                    Vector3 candidate = samples[bucket[b]];

                    float rise = candidate.y - takeoff.y;
                    if (rise < minJumpHeight || rise > maxJumpHeight)
                        continue;

                    float flatDistance = Flat(candidate - takeoff).magnitude;
                    if (flatDistance < 0.05f || flatDistance > maxJumpDistance || flatDistance >= bestDistance)
                        continue;

                    // If it can already be walked to, it isn't a jump.
                    if (!NavMesh.Raycast(takeoff, candidate, out _, NavMesh.AllAreas))
                        continue;

                    if (!TryInsetLanding(takeoff, candidate, out var inset))
                        continue;

                    // Stand back from the rim before launching, then check the arc from where the
                    // jump will really start rather than from the edge.
                    Vector3 launchFrom = InsetTakeoff(rim, inset);

                    if (!ArcIsClear(launchFrom, inset))
                        continue;

                    bestDistance = flatDistance;
                    takeoffPoint = launchFrom;
                    landing = inset;
                    found = true;
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Pulls the takeoff back from the rim, directly away from the ledge it's about to jump onto, so
    /// the entity isn't launching with its capsule already in contact with the wall. Falls back to
    /// the rim itself when there's no walkable ground to back onto.
    /// </summary>
    private Vector3 InsetTakeoff(Vector3 rim, Vector3 landing)
    {
        Vector3 towardLedge = Flat(landing - rim);
        if (towardLedge.sqrMagnitude < 0.0001f)
            return rim;

        Vector3 pulled = rim - towardLedge.normalized * takeoffInset;
        if (NavMesh.SamplePosition(pulled, out var hit, takeoffInset, NavMesh.AllAreas)
            && Mathf.Abs(hit.position.y - rim.y) < 0.5f)
        {
            return hit.position;
        }

        return rim;
    }

    /// <summary>Pushes the landing further onto the upper surface, away from the rim it was sampled on.</summary>
    private bool TryInsetLanding(Vector3 takeoff, Vector3 rimPoint, out Vector3 result)
    {
        Vector3 outward = Flat(rimPoint - takeoff);
        if (outward.sqrMagnitude < 0.0001f)
        {
            result = rimPoint;
            return true;
        }

        Vector3 pushed = rimPoint + outward.normalized * landingInset;
        if (NavMesh.SamplePosition(pushed, out var hit, landingInset, NavMesh.AllAreas)
            && Mathf.Abs(hit.position.y - rimPoint.y) < 0.5f)
        {
            result = hit.position;
            return true;
        }

        // No room to inset (a narrow ledge) -- the rim itself still works as a landing.
        result = rimPoint;
        return true;
    }

    /// <summary>
    /// Flies the arc the entity will actually fly and rejects the link if any point of it is buried
    /// in geometry. A straight line between the two ends would always be blocked by the wall of the
    /// ledge, which is the whole thing a jump exists to clear -- so the arc has to be tested, not
    /// the chord.
    /// </summary>
    private bool ArcIsClear(Vector3 takeoff, Vector3 landing)
    {
        JumpArc.Solve(takeoff, landing, out var velocity, out float duration);

        const int steps = 10;
        for (int i = 1; i < steps; i++)
        {
            Vector3 point = JumpArc.Sample(takeoff, velocity, duration * i / steps);
            if (Physics.CheckSphere(point, 0.3f, obstructionMask, QueryTriggerInteraction.Ignore))
                return false;
        }

        return true;
    }

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    private void OnDrawGizmos()
    {
        if (links == null)
            return;

        for (int i = 0; i < links.Count; i++)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(links[i].takeoff, 0.25f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(links[i].landing, 0.25f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(links[i].takeoff, links[i].landing);
        }
    }
}
