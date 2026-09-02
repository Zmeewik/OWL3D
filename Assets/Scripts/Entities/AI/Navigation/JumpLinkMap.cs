using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bakes the level's <see cref="JumpGraph"/> and hands it to the AI at runtime.
///
/// This exists because Unity's own auto-generated off-mesh links cannot express the jumps enemies
/// need. Drop Height only ever produces links *down* off a ledge, Jump Distance only bridges a
/// horizontal gap between surfaces at roughly the same height, and neither can be filtered per enemy
/// class or costed against the time it really takes to fly. Nothing in the bake will ever let an
/// agent climb onto a raised platform, which is exactly what was needed -- a target standing on a
/// ledge was simply unreachable, so pathing returned PathPartial and the enemy ran around the base
/// of it forever.
///
/// Links are found by walking the NavMesh's own boundary edges rather than its vertices. A large
/// flat platform triangulates into just two triangles with four corner vertices, so anything driven
/// off the vertex list can only ever produce jump points at the corners of a surface. Sampling
/// along each boundary edge at a fixed <see cref="spacing"/> instead puts candidate takeoff points
/// evenly along the whole rim of every ledge, and each of those connects to the nearest few reachable
/// rims -- up and down alike, since an entity always jumps and never merely steps off.
///
/// Baking is an editor-time step. The result goes into a <see cref="JumpGraph"/> asset rather than
/// into the scene, so a rebake doesn't bury the scene file under thousands of changed lines.
/// </summary>
[DisallowMultipleComponent]
public class JumpLinkMap : MonoBehaviour
{
    [Header("Graph asset")]
    [Tooltip("Where the bake writes to and the AI reads from. Create one via Assets > Create > OWL > AI > Jump Graph.")]
    [SerializeField] private JumpGraph graph;

    [Header("Sampling")]
    [Tooltip("Distance between candidate takeoff points sampled along each NavMesh boundary edge. " +
             "Smaller means a jump is available from more places along a ledge, at the cost of more links.")]
    [SerializeField, Min(0.25f)] private float spacing = 2f;

    [Tooltip("How far in from each end of a boundary edge sampling starts, so takeoff points sit " +
             "along the rim rather than on the triangulation's corners. Without it the endpoints of " +
             "every edge are sampled exactly, and any edge shorter than the spacing above yields " +
             "nothing but its two vertices -- which is most of them, so jumps end up going corner to " +
             "corner. An edge too short to inset falls back to a single point at its midpoint, which " +
             "is still off the corners. Set to 0 to sample the vertices themselves again.")]
    [SerializeField, Min(0f)] private float edgeMargin = 0.5f;

    [Tooltip("Smallest height difference worth a jump, in either direction. Below this the agent can " +
             "just walk or step across, and turning it into an arc would only look silly.")]
    [SerializeField] private float minJumpHeight = 1f;

    [Header("Jump envelope")]
    [Tooltip("Highest climb a link may express. Bake this to the most capable enemy in the game -- " +
             "each class then filters the graph down to what it can personally make (JumpCapability), " +
             "so one bake serves them all. Anything above this is never generated for anyone.")]
    [SerializeField] private float maxJumpUpHeight = 4f;

    [Tooltip("Furthest drop a link may express. Independent of the climb limit: entities are " +
             "generally willing to leap down much further than they can jump up.")]
    [SerializeField] private float maxDropHeight = 6f;

    [Tooltip("Run speed the links are baked for -- how fast a jumper is moving when it leaves the " +
             "ground, which is what its horizontal reach is made of. Bake this to the fastest enemy " +
             "in the game. There is no separate span setting: how far a jump goes is the flight time " +
             "the impulse buys, spent at this speed, and JumpArc works it out.")]
    [SerializeField] private float runSpeed = 7f;

    [Tooltip("How many landings a single takeoff point may connect to, nearest first. One is not " +
             "enough: with a single connection per takeoff the links are isolated hops rather than a " +
             "graph, and no route can ever chain two jumps together.")]
    [SerializeField, Range(1, 8)] private int maxLinksPerTakeoff = 4;

    [Header("Drop sampling")]
    [Tooltip("How many directions are fanned out from each rim point looking for ground to leap down " +
             "onto. Climbs have to land on the rim of the surface above -- that is the only part of it " +
             "a jump can reach -- but a drop lands on open floor, which is the interior of the mesh " +
             "and has no boundary points on it at all. Probing outward is the only way to find it; " +
             "without this, drops only ever exist where some *other* surface's rim happens to lie " +
             "below, which is why they cluster at corners and next to neighbouring structures.")]
    [SerializeField, Range(4, 24)] private int dropDirections = 12;

    [Tooltip("How many distances along each direction are probed, between the minimum run below and " +
             "the furthest a jump in this envelope can reach.")]
    [SerializeField, Range(1, 8)] private int dropDistanceSteps = 4;

    [Tooltip("Shortest forward travel a drop may have. A leap straight down lands with the entity " +
             "still scraping the wall it just left, so a drop is always given some run at it.")]
    [SerializeField] private float minDropRun = 1.5f;

    [Tooltip("How many drops one rim point keeps, shortest first.")]
    [SerializeField, Range(1, 8)] private int maxDropsPerTakeoff = 3;

    [Header("Placement")]
    [Tooltip("How far past the upper rim the landing point is pushed, so the entity lands on the " +
             "platform rather than balanced on its very edge.")]
    [SerializeField] private float landingInset = 1.2f;

    [Tooltip("How far BACK from the rim the takeoff of a CLIMB is pulled, away from the ledge. Jumping " +
             "with your back against a wall doesn't work: the entity's capsule is already touching " +
             "the ledge face at the rim, so physics cancels the horizontal launch velocity into it " +
             "and the jump goes straight up and drops back down. Standing back leaves room to be " +
             "airborne and above the lip by the time it arrives. Drops don't get this -- there is no " +
             "wall ahead of a descent, so backing up would only waste horizontal reach.")]
    [SerializeField] private float takeoffInset = 1.5f;

    [Header("Validation")]
    [Tooltip("Geometry the jump arc must clear. The arc is sampled against this to reject links that " +
             "would fly through a wall instead of over a ledge.")]
    [SerializeField] private LayerMask obstructionMask = ~0;

    [Tooltip("How close to either end of a jump the arc stops being tested against geometry. Both " +
             "ends stand on solid ground by construction, so a probe there hits the very surfaces the " +
             "jump leaves and arrives on, and every link would read as blocked. This is a tolerance " +
             "on the check, not a shape given to the arc -- the arc itself has nothing to set. Keep " +
             "it well under the takeoff inset, or a climb stops being checked against the ledge wall " +
             "standing right in front of it, which is the case this check exists for.")]
    [SerializeField] private float arcEndClearance = 1f;

    [Tooltip("How many times a crossing may retry with a taller arc when the cheapest one clips the " +
             "ledge. Zero means take the cheapest or nothing. Each step trades a stronger jumper for " +
             "more clearance, and the sweep stops at the envelope's own jump height.")]
    [SerializeField, Range(0, 8)] private int arcLiftSteps = 4;

    [Header("Ground connectivity")]
    [Tooltip("How far apart two link endpoints may be and still have the walk between them baked. " +
             "This is what lets a route chain jumps: land here, run to there, jump again.")]
    [SerializeField] private float groundLinkRadius = 25f;

    [Tooltip("How many walk-to-the-next-takeoff edges each landing keeps, nearest first. Capping the " +
             "degree is what keeps the bake from being quadratic in link count; the nearest few are " +
             "in practice the only ones a shortest route ever wants.")]
    [SerializeField, Range(2, 32)] private int maxGroundEdgesPerNode = 12;

    [Tooltip("Endpoints closer together than this collapse into one routing node. Several links " +
             "leaving the same ledge share a takeoff, and treating them as one node is what keeps " +
             "the connectivity bake small.")]
    [SerializeField, Min(0.05f)] private float nodeWeldRadius = 0.5f;

    [Header("Gizmos")]
    [Tooltip("Crossings are drawn in one colour because there is one kind of them. A ledge you can " +
             "climb is the same record as the ledge you can drop off; colouring by direction implied " +
             "two sorts of thing where there is only one.")]
    [SerializeField] private bool drawCrossings = true;
    [Tooltip("Ground connectivity is tens of thousands of lines -- off unless you're diagnosing it.")]
    [SerializeField] private bool drawGroundEdges;
    [Tooltip("Only links within this distance of the scene pivot are drawn, so a full graph doesn't stall the editor.")]
    [SerializeField] private float gizmoRange = 40f;

    /// <summary>Scene-wide instance. Static so movement code can reach it without per-entity wiring.</summary>
    public static JumpLinkMap Instance { get; private set; }

    /// <summary>The baked graph, or null if none is assigned. Enemies fall back to plain ground pathing without it.</summary>
    public JumpGraph Graph => graph;

    /// <summary>
    /// Furthest a jump inside this bake's envelope can travel. Derived from the impulse and the run
    /// speed rather than set: a separately-typed distance could contradict what the arc can actually
    /// do, and used to.
    /// </summary>
    private float BakeReach => JumpArc.MaxReach(maxJumpUpHeight, runSpeed, maxDropHeight);

    private void OnEnable() => Instance = this;

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

#if UNITY_EDITOR

    // ---- Baking -------------------------------------------------------------

    [ContextMenu("Rebuild Jump Graph")]
    public void Rebuild()
    {
        if (graph == null)
        {
            Debug.LogError($"[{name}] No JumpGraph asset assigned -- nothing to bake into.", this);
            return;
        }

        var samples = SampleBoundaryEdges();
        if (samples.Count == 0)
        {
            Debug.LogWarning($"[{name}] No NavMesh boundary edges found. Bake a NavMesh first.", this);
            return;
        }

        // The insets are part of the flight, not extras on top of it, so they come straight out of
        // the distance budget. Left unnoticed this produces a graph with almost nothing in it and no
        // obvious reason why.
        float insetOverhead = takeoffInset + landingInset;
        float reach = BakeReach;
        if (insetOverhead > reach * 0.5f)
        {
            Debug.LogWarning(
                $"[{name}] Takeoff and landing insets total {insetOverhead:0.##}m of the {reach:0.##}m these " +
                $"legs can reach, leaving only {reach - insetOverhead:0.##}m of actual ledge gap. Raise " +
                "maxJumpUpHeight or runSpeed -- both buy reach -- or shorten the insets.", this);
        }

        var links = BuildLinks(samples, out bool cancelled);
        if (cancelled)
        {
            Debug.LogWarning($"[{name}] Bake cancelled -- graph left untouched.", this);
            return;
        }

        if (links.Count == 0)
        {
            Debug.LogWarning($"[{name}] {samples.Count} boundary samples produced no links. Check the jump envelope.", this);
            return;
        }

        var nodes = new List<Vector3>();
        var nodeFlags = new List<byte>();
        AssignNodes(links, nodes, nodeFlags);

        BuildJumpEdges(links, nodes.Count, out var jumpStart, out var jumpLink);

        if (!BuildGroundEdges(nodes, nodeFlags, out var groundStart, out var groundTarget, out var groundLength))
        {
            Debug.LogWarning($"[{name}] Ground connectivity bake cancelled -- graph not written.", this);
            return;
        }

        float fastestArc = FastestArcSpeed(links);

        graph.SetBakedData(links.ToArray(), nodes.ToArray(), nodeFlags.ToArray(),
                           groundStart, groundTarget, groundLength,
                           jumpStart, jumpLink,
                           maxJumpUpHeight, maxDropHeight, runSpeed,
                           fastestArc, Mathf.Max(4f, BakeReach * 2f));

        UnityEditor.AssetDatabase.SaveAssetIfDirty(graph);

        Debug.Log($"[{name}] Baked {links.Count} crossings over {nodes.Count} nodes and " +
                  $"{groundTarget.Length} ground edges, from {samples.Count} boundary samples. Every " +
                  $"crossing is usable both ways, so the graph carries {links.Count * 2} jump edges.\n" +
                  ReachabilityReport(links), this);
    }

    /// <summary>
    /// What each enemy class can actually do with the graph that was just baked.
    ///
    /// The envelope here is a ceiling on what gets generated, not a promise that anyone can fly it,
    /// and the two drifted apart badly once: a graph baked for a ten-metre jumper left a four-metre
    /// class able to use a fifth of the climbs, with nothing anywhere saying so. The graph looked
    /// full, the enemies simply stopped jumping. <see cref="JumpGraph.Covers"/> only ever noticed a
    /// class that was too *strong* for the bake, which is the harmless direction.
    /// </summary>
    public string ReachabilityReport(List<JumpLink> links)
    {
        var report = new System.Text.StringBuilder();

        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:EnemyConfig"))
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyConfig>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (config == null)
                continue;

            var capability = JumpCapability.FromConfig(config);

            int forward = 0, reverse = 0, either = 0;
            float neededHeight = 0f;
            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                bool f = capability.Allows(link, false);
                bool r = capability.Allows(link, true);
                if (f) forward++;
                if (r) reverse++;
                if (f || r) either++;

                // What raising the jump alone would have to reach to make this crossing usable at all.
                if (!f && !r && link.launchForward <= capability.runSpeed)
                    neededHeight = Mathf.Max(neededHeight, Mathf.Min(link.RequiredJumpHeight, link.ReverseRequiredJumpHeight));
            }

            float share = links.Count > 0 ? either / (float)links.Count : 0f;
            report.Append($"  {config.className}: {either}/{links.Count} crossings usable " +
                          $"({share:P0}) -- {forward} forward, {reverse} reverse, " +
                          $"jump {capability.maxJumpUpHeight:0.##}m / drop {capability.maxDropHeight:0.##}m / " +
                          $"run {capability.runSpeed:0.##}m/s");

            if (neededHeight > capability.maxJumpUpHeight)
                report.Append($"; maxJumpUpHeight {neededHeight:0.##}m would unlock the rest");

            report.AppendLine();

            if (share < 0.5f)
            {
                Debug.LogWarning(
                    $"[{name}] {config.className} can use only {share:P0} of the baked crossings. Its jump " +
                    $"({capability.maxJumpUpHeight:0.##}m at {capability.runSpeed:0.##}m/s) is well short of what " +
                    "this level's gaps need -- raise maxJumpUpHeight on the config, or shorten the insets.", this);
            }
        }

        return report.Length > 0 ? report.ToString() : "  (no EnemyConfig assets found to report on)";
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

            float length = Vector3.Distance(a, b);
            float usable = length - edgeMargin * 2f;

            // Too short to inset from both ends. The midpoint is the only spot on it that isn't a
            // corner, and one mid-edge point is worth more than the two vertices it replaces: a
            // takeoff on a corner stands at the narrowest, most awkward part of the ledge.
            if (usable <= 0f)
            {
                AddSample(samples, seen, Vector3.Lerp(a, b, 0.5f));
                continue;
            }

            int steps = Mathf.Max(1, Mathf.FloorToInt(usable / spacing));
            for (int s = 0; s <= steps; s++)
            {
                float along = edgeMargin + usable * s / steps;
                AddSample(samples, seen, Vector3.Lerp(a, b, along / length));
            }
        }

        return samples;
    }

    /// <summary>
    /// Records a candidate point unless one is already occupying its cell. The merge grid is sized by
    /// <see cref="spacing"/>, which is what stops the several edges meeting at a corner from each
    /// contributing their own near-identical point there.
    /// </summary>
    private void AddSample(List<Vector3> samples, HashSet<Vector3Int> seen, Vector3 point)
    {
        var key = new Vector3Int(
            Mathf.RoundToInt(point.x / spacing),
            Mathf.RoundToInt(point.y / spacing),
            Mathf.RoundToInt(point.z / spacing));

        if (seen.Add(key))
            samples.Add(point);
    }

    private static void AddEdge(Dictionary<long, int> edgeUse, int a, int b)
    {
        // Key both directions the same way, so the two triangles sharing an edge agree on it.
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        edgeUse.TryGetValue(key, out int count);
        edgeUse[key] = count + 1;
    }

    /// <summary>
    /// Every crossing in the level, once each.
    ///
    /// Two searches feed one list. Matching rims against each other finds a crossing onto the ledge
    /// above; probing outward and downward finds one onto the open floor below, which the rim search
    /// cannot see because the interior of a surface carries no boundary points. They are two ways of
    /// *noticing* the same kind of thing, not two kinds of thing -- so whatever they turn up is
    /// deduplicated by its pair of endpoints, and a gap both of them spot is stored once. Which way
    /// round it ends up stored does not matter: the graph files every link under both of its ends.
    /// </summary>
    private List<JumpLink> BuildLinks(List<Vector3> samples, out bool cancelled)
    {
        var sampleGrid = BuildSampleGrid(samples, BakeReach);
        var links = new List<JumpLink>();
        var candidates = new List<JumpLink>();
        var seen = new HashSet<long>();

        cancelled = false;

        for (int i = 0; i < samples.Count; i++)
        {
            if ((i & 63) == 0 &&
                UnityEditor.EditorUtility.DisplayCancelableProgressBar(
                    "Baking jump graph", $"Crossings from boundary sample {i} / {samples.Count}",
                    i / (float)samples.Count * 0.5f))
            {
                UnityEditor.EditorUtility.ClearProgressBar();
                links.Clear();
                cancelled = true;
                return links;
            }

            CollectRimCrossings(samples[i], samples, sampleGrid, candidates);
            AddUnique(links, seen, candidates);

            CollectFloorCrossings(samples[i], candidates);
            AddUnique(links, seen, candidates);
        }

        UnityEditor.EditorUtility.ClearProgressBar();
        return links;
    }

    /// <summary>
    /// Appends the candidates whose crossing isn't already in the list. Keyed on the unordered pair of
    /// endpoint cells, so a gap found from above and again from below survives as one link rather
    /// than a matched pair pointing opposite ways.
    /// </summary>
    private void AddUnique(List<JumpLink> links, HashSet<long> seen, List<JumpLink> candidates)
    {
        for (int c = 0; c < candidates.Count; c++)
        {
            var link = candidates[c];

            long a = CellKey(link.takeoff);
            long b = CellKey(link.landing);
            long key = a < b ? a * 1_000_003L + b : b * 1_000_003L + a;

            if (seen.Add(key))
                links.Add(link);
        }
    }

    private long CellKey(Vector3 point)
    {
        int x = Mathf.RoundToInt(point.x / spacing);
        int y = Mathf.RoundToInt(point.y / spacing);
        int z = Mathf.RoundToInt(point.z / spacing);
        return ((long)(x + 32768) << 34) | ((long)(y + 32768) << 17) | (uint)(z + 32768);
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
    /// Every climb worth baking from one rim point: the nearest few reachable rims above it, each
    /// inset onto its surface with an arc verified clear of the geometry between.
    ///
    /// A crossing onto the surface above can only ever land on its *rim* -- that is the only part of
    /// it an arc coming from below can reach -- so matching rim points against each other is exactly
    /// right here. It is blind to the other case, a crossing onto open floor, because the interior of
    /// a surface carries no boundary points; <see cref="CollectFloorCrossings"/> goes looking for
    /// those instead. Both feed the same list, and both produce plain links: which end is uphill is a
    /// property of the geometry, not of how the crossing was noticed.
    ///
    /// Taking the nearest *few* rather than only the nearest is the difference between a set of
    /// isolated hops and a graph a route can actually chain through. They are sorted by span so the
    /// cap keeps the short, reliable jumps and discards the marginal ones at the edge of the envelope.
    /// </summary>
    private void CollectRimCrossings(Vector3 rim, List<Vector3> samples,
                                     Dictionary<Vector2Int, List<int>> sampleGrid, List<JumpLink> output)
    {
        output.Clear();

        // Insetting moves both ends *outward* along the jump, so a rim gap this much wider than the
        // envelope can still inset down into it. Filtering the rim pair against the envelope directly
        // would throw away links that end up legal, while accepting it would bake jumps longer than
        // anything can make -- the real test happens after insetting, below.
        float rimReach = BakeReach + takeoffInset + landingInset;

        var center = new Vector2Int(Mathf.FloorToInt(rim.x / rimReach),
                                    Mathf.FloorToInt(rim.z / rimReach));

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!sampleGrid.TryGetValue(new Vector2Int(center.x + dx, center.y + dz), out var bucket))
                    continue;

                for (int b = 0; b < bucket.Count; b++)
                {
                    Vector3 candidate = samples[bucket[b]];

                    float rimRise = candidate.y - rim.y;

                    // Loose pre-filter only. Insetting re-samples each end onto the NavMesh and
                    // accepts up to half a metre of height difference, so the bound is widened here
                    // and enforced properly on the finished link.
                    if (rimRise < minJumpHeight || rimRise > maxJumpUpHeight + 1f)
                        continue;

                    float rimDistance = Flat(candidate - rim).magnitude;
                    if (rimDistance < 0.05f || rimDistance > rimReach)
                        continue;

                    // If it can already be walked to, it isn't a jump.
                    if (!NavMesh.Raycast(rim, candidate, out _, NavMesh.AllAreas))
                        continue;

                    if (!TryInsetLanding(rim, candidate, out var landing))
                        continue;

                    // Stand back from the rim before launching, so the capsule isn't already against
                    // the ledge face when the horizontal velocity is applied.
                    Vector3 takeoff = InsetTakeoff(rim, landing);

                    // The envelope describes a jump that has to be actually flown, and what is flown
                    // is takeoff to landing -- not the rim pair those were derived from.
                    if (!TrySolveCrossing(takeoff, landing, out var link))
                        continue;

                    output.Add(link);
                }
            }
        }

        TrimToNearest(output, maxLinksPerTakeoff);
    }

    /// <summary>
    /// Crossings onto open floor below, found by fanning outward from a rim point and looking down.
    ///
    /// These cannot be found the way rim-to-rim ones are. A landing on open floor is in the *interior*
    /// of the mesh, where there are no boundary points to match against, so pairing rims only ever
    /// turned up the descents that happened to have another surface's rim underneath them -- they
    /// gathered at corners and beside neighbouring structures while the long open stretch of a ledge
    /// had none at all.
    ///
    /// So the ground is looked for directly: step out in a fan of directions, drop a ray, and confirm
    /// whatever it lands on is walkable. Directions still standing on the upper surface fall out for
    /// free, since their probe comes back at the same height. The result is an ordinary link, usable
    /// in both directions like any other -- a crossing found by looking down is not a "drop link".
    /// </summary>
    private void CollectFloorCrossings(Vector3 rim, List<JumpLink> output)
    {
        output.Clear();

        float probeCeiling = maxDropHeight + 4f;

        for (int d = 0; d < dropDirections; d++)
        {
            float angle = d * Mathf.PI * 2f / dropDirections;
            Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            for (int s = 0; s < dropDistanceSteps; s++)
            {
                float run = Mathf.Lerp(minDropRun, BakeReach,
                                       dropDistanceSteps == 1 ? 0f : s / (float)(dropDistanceSteps - 1));

                // Start the ray a little above the rim so a lip level with it doesn't shadow the floor.
                Vector3 origin = rim + direction * run + Vector3.up * 0.5f;
                if (!Physics.Raycast(origin, Vector3.down, out var hit, probeCeiling,
                                     obstructionMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                // Solid ground is not the same as ground an agent may stand on.
                if (!NavMesh.SamplePosition(hit.point, out var navHit, 1f, NavMesh.AllAreas))
                    continue;

                float fall = rim.y - navHit.position.y;
                if (fall < minJumpHeight || fall > maxDropHeight)
                    continue;

                // If it can already be walked to, it isn't a jump.
                if (!NavMesh.Raycast(rim, navHit.position, out _, NavMesh.AllAreas))
                    continue;

                // The rim stays the endpoint: there is no wall on the outward side of a ledge, so
                // backing away from it would only spend reach the crossing needs.
                if (!TrySolveCrossing(rim, navHit.position, out var link))
                    continue;

                output.Add(link);

                // The first ground found along a direction is the one to take; anything further out is
                // a longer leap to somewhere this one already reaches.
                break;
            }
        }

        TrimToNearest(output, maxDropsPerTakeoff);
    }

    /// <summary>
    /// Keeps the <paramref name="cap"/> shortest jumps that go somewhere meaningfully different.
    /// Sorting by span first means the cap spends itself on the short, reliable jumps rather than the
    /// marginal ones at the very edge of the envelope.
    /// </summary>
    private void TrimToNearest(List<JumpLink> output, int cap)
    {
        if (output.Count <= 1)
            return;

        output.Sort((a, b) => a.horizontalDistance.CompareTo(b.horizontalDistance));

        // Several candidates on the same far surface describe the same jump; keep one per cell so the
        // cap is spent on genuinely different destinations.
        var kept = new HashSet<Vector3Int>();
        int write = 0;
        for (int read = 0; read < output.Count && write < cap; read++)
        {
            Vector3 landing = output[read].landing;
            var cell = new Vector3Int(Mathf.RoundToInt(landing.x / spacing),
                                      Mathf.RoundToInt(landing.y / spacing),
                                      Mathf.RoundToInt(landing.z / spacing));
            if (!kept.Add(cell))
                continue;

            output[write++] = output[read];
        }

        output.RemoveRange(write, output.Count - write);
    }

    /// <summary>
    /// Solves and validates the one arc a crossing gets, or reports that it has none.
    ///
    /// Starts from the cheapest arc that reaches at all, because that is what a jump looks like when
    /// it isn't showing off -- a small step gets a small hop rather than everything the legs have. If
    /// that arc grazes the lip of the ledge it is leaving or arriving at, the impulse is stepped up
    /// and tried again: a taller arc clears more, at the cost of hanging in the air longer and so
    /// needing a stronger jumper. The first one that flies clean is kept, so arcs are as cheap as the
    /// geometry allows and no cheaper. Nothing here is a setting; the ceiling is the bake envelope.
    /// </summary>
    private bool TrySolveCrossing(Vector3 from, Vector3 to, out JumpLink link)
    {
        link = default;

        float climb = Mathf.Abs(to.y - from.y);
        if (climb < minJumpHeight)
            return false;

        // Gravity takes a body down any distance at all, so how far a descent may go is the one limit
        // physics cannot supply. Everything else answers itself when the arc solves or doesn't.
        if (to.y < from.y && climb > maxDropHeight)
            return false;

        if (!JumpArc.TrySolveMinimum(from, to, runSpeed, out float launchUp, out float launchForward,
                                     out float duration, out float minimumHeight))
        {
            return false;
        }

        if (minimumHeight > maxJumpUpHeight)
            return false;

        for (int step = 0; step <= arcLiftSteps; step++)
        {
            float height = Mathf.Lerp(minimumHeight, maxJumpUpHeight,
                                      arcLiftSteps == 0 ? 0f : step / (float)arcLiftSteps);

            if (step > 0 && !JumpArc.TrySolveAt(from, to, height, runSpeed, out launchUp, out launchForward, out duration))
                continue;

            if (!ArcIsClear(from, launchUp, launchForward, duration, to))
                continue;

            link = JumpLink.Create(from, to, launchUp, launchForward, duration);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Pulls a climb's takeoff back from the rim, directly away from the ledge it's about to jump
    /// onto, so the entity isn't launching with its capsule already in contact with the wall. Falls
    /// back to the rim itself when there's no walkable ground to back onto.
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

    /// <summary>Pushes the landing further onto the far surface, away from the rim it was sampled on.</summary>
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
    private bool ArcIsClear(Vector3 takeoff, float launchUp, float launchForward, float duration, Vector3 landing)
    {
        Vector3 flat = Flat(landing - takeoff);
        Vector3 heading = flat.sqrMagnitude > 0.0000001f ? flat.normalized : Vector3.zero;
        Vector3 velocity = heading * launchForward + Vector3.up * launchUp;

        float endClearanceSqr = arcEndClearance * arcEndClearance;

        const int steps = 12;
        for (int i = 1; i < steps; i++)
        {
            Vector3 point = JumpArc.Sample(takeoff, velocity, duration * i / steps);

            // Close to an end and still above it, the only thing in reach is that end's own ground --
            // which the jump starts standing on and finishes standing on. A drop's arc barely rises,
            // so without this its opening samples all report the platform it is leaving and the whole
            // link is thrown away. The test only relaxes *above* an endpoint, so a climb's arc is
            // still checked against the ledge wall standing in front of it, which is the case this
            // check exists for.
            if (point.y >= takeoff.y && (point - takeoff).sqrMagnitude < endClearanceSqr)
                continue;
            if (point.y >= landing.y && (point - landing).sqrMagnitude < endClearanceSqr)
                continue;

            if (Physics.CheckSphere(point, 0.3f, obstructionMask, QueryTriggerInteraction.Ignore))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Collapses link endpoints into routing nodes and records which end each node plays. Several
    /// links leaving the same ledge share a takeoff, so welding turns them into one node with several
    /// outgoing jump edges -- which is what keeps the connectivity bake below from being quadratic in
    /// the number of links.
    /// </summary>
    private void AssignNodes(List<JumpLink> links, List<Vector3> nodes, List<byte> flags)
    {
        var index = new Dictionary<Vector3Int, int>();

        int NodeFor(Vector3 position, byte flag)
        {
            var key = new Vector3Int(Mathf.RoundToInt(position.x / nodeWeldRadius),
                                     Mathf.RoundToInt(position.y / nodeWeldRadius),
                                     Mathf.RoundToInt(position.z / nodeWeldRadius));

            if (!index.TryGetValue(key, out int node))
            {
                node = nodes.Count;
                nodes.Add(position);
                flags.Add(0);
                index[key] = node;
            }

            flags[node] |= flag;
            return node;
        }

        // Both ends get both roles. Once a crossing can be travelled either way, "the takeoff end" and
        // "the landing end" stop describing anything: every endpoint is somewhere a jump can start and
        // somewhere one can finish. Marking them by the direction they happened to be found in left
        // the search unable to enter the graph at a landing -- and unable to walk between two of them,
        // since the ground bake only joined landings to takeoffs.
        const byte bothRoles = JumpGraph.FlagTakeoff | JumpGraph.FlagLanding;

        for (int i = 0; i < links.Count; i++)
        {
            var link = links[i];
            link.takeoffNode = NodeFor(link.takeoff, bothRoles);
            link.landingNode = NodeFor(link.landing, bothRoles);
            links[i] = link;
        }
    }

    /// <summary>
    /// Files every link under *both* of its ends, so a node can list the jumps leaving it without a
    /// scan. The entry at the takeoff travels the stored arc; the one at the landing travels it
    /// backwards, marked by storing the index's bitwise complement. Emitting both is what makes a
    /// descent free: it is the climb already in the graph, read the other way.
    /// </summary>
    private static void BuildJumpEdges(List<JumpLink> links, int nodeCount, out int[] start, out int[] encoded)
    {
        start = new int[nodeCount + 1];
        for (int i = 0; i < links.Count; i++)
        {
            start[links[i].takeoffNode + 1]++;
            start[links[i].landingNode + 1]++;
        }

        for (int n = 0; n < nodeCount; n++)
            start[n + 1] += start[n];

        encoded = new int[links.Count * 2];
        var cursor = (int[])start.Clone();
        for (int i = 0; i < links.Count; i++)
        {
            encoded[cursor[links[i].takeoffNode]++] = i;
            encoded[cursor[links[i].landingNode]++] = ~i;
        }
    }

    /// <summary>
    /// Walks from every landing to the nearest few takeoffs, measured with the real NavMesh path.
    ///
    /// Only landing-to-takeoff is baked, because that is the only direction a route can consume: you
    /// arrive somewhere by jumping and leave by jumping again, and the walk between the two is this
    /// edge. Reaching the *first* takeoff and leaving the *last* landing are solved live against the
    /// entity's actual position, so they don't belong in the table.
    ///
    /// The nearest-few cap is a deliberate approximation. A shortest route essentially never lands
    /// and then walks past a dozen closer takeoffs to reach a distant one -- if it did, walking the
    /// whole way would have been shorter anyway.
    /// </summary>
    private bool BuildGroundEdges(List<Vector3> nodes, List<byte> flags,
                                  out int[] start, out int[] target, out float[] length)
    {
        int nodeCount = nodes.Count;
        start = new int[nodeCount + 1];

        var targets = new List<int>();
        var lengths = new List<float>();

        var nodeGrid = new Dictionary<Vector2Int, List<int>>();
        float cell = Mathf.Max(1f, groundLinkRadius);
        for (int i = 0; i < nodeCount; i++)
        {
            var key = new Vector2Int(Mathf.FloorToInt(nodes[i].x / cell), Mathf.FloorToInt(nodes[i].z / cell));
            if (!nodeGrid.TryGetValue(key, out var bucket))
            {
                bucket = new List<int>();
                nodeGrid[key] = bucket;
            }
            bucket.Add(i);
        }

        var path = new NavMeshPath();
        var candidates = new List<int>();
        var rowTargets = new List<int>();
        var rowLengths = new List<float>();
        float radiusSqr = groundLinkRadius * groundLinkRadius;

        // Twice the kept degree: a scan that keeps hitting unreachable neighbours would otherwise pay
        // for the whole radius before giving up.
        int attemptBudget = maxGroundEdgesPerNode * 3;

        for (int from = 0; from < nodeCount; from++)
        {
            // Rows are appended in node order, so the row's first edge index is simply how many edges
            // exist so far -- no separate counting pass needed to build the CSR offsets.
            start[from] = targets.Count;

            if ((from & 15) == 0 &&
                UnityEditor.EditorUtility.DisplayCancelableProgressBar(
                    "Baking jump graph", $"Ground connectivity from node {from} / {nodeCount}",
                    0.5f + from / (float)nodeCount * 0.5f))
            {
                UnityEditor.EditorUtility.ClearProgressBar();
                target = null;
                length = null;
                return false;
            }

            if ((flags[from] & JumpGraph.FlagLanding) == 0)
                continue;

            candidates.Clear();
            CollectNeighbours(nodes, flags, nodeGrid, cell, from, radiusSqr, candidates);
            if (candidates.Count == 0)
                continue;

            Vector3 origin = nodes[from];
            candidates.Sort((a, b) =>
                (nodes[a] - origin).sqrMagnitude.CompareTo((nodes[b] - origin).sqrMagnitude));

            rowTargets.Clear();
            rowLengths.Clear();

            int attempts = 0;
            for (int c = 0; c < candidates.Count && rowTargets.Count < maxGroundEdgesPerNode
                                                 && attempts < attemptBudget; c++)
            {
                int to = candidates[c];
                attempts++;

                if (!NavMesh.CalculatePath(origin, nodes[to], NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete ||
                    path.corners.Length < 2)
                {
                    continue;
                }

                rowTargets.Add(to);
                rowLengths.Add(PathLength(path));
            }

            for (int r = 0; r < rowTargets.Count; r++)
            {
                targets.Add(rowTargets[r]);
                lengths.Add(rowLengths[r]);
            }
        }

        start[nodeCount] = targets.Count;
        UnityEditor.EditorUtility.ClearProgressBar();

        target = targets.ToArray();
        length = lengths.ToArray();
        return true;
    }

    private static void CollectNeighbours(List<Vector3> nodes, List<byte> flags,
                                          Dictionary<Vector2Int, List<int>> nodeGrid, float cell,
                                          int from, float radiusSqr, List<int> output)
    {
        Vector3 origin = nodes[from];
        var center = new Vector2Int(Mathf.FloorToInt(origin.x / cell), Mathf.FloorToInt(origin.z / cell));

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!nodeGrid.TryGetValue(new Vector2Int(center.x + dx, center.y + dz), out var bucket))
                    continue;

                for (int b = 0; b < bucket.Count; b++)
                {
                    int to = bucket[b];
                    if (to == from || (flags[to] & JumpGraph.FlagTakeoff) == 0)
                        continue;

                    if ((nodes[to] - origin).sqrMagnitude <= radiusSqr)
                        output.Add(to);
                }
            }
        }
    }

    private static float PathLength(NavMeshPath path)
    {
        var corners = path.corners;
        float total = 0f;
        for (int i = 1; i < corners.Length; i++)
            total += Vector3.Distance(corners[i - 1], corners[i]);
        return total;
    }

    private static float FastestArcSpeed(List<JumpLink> links)
    {
        float fastest = 0.01f;
        for (int i = 0; i < links.Count; i++)
            fastest = Mathf.Max(fastest, links[i].launchForward);
        return fastest;
    }

#endif

    // ---- Gizmos -------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        if (graph == null || graph.IsEmpty)
            return;

        Vector3 pivot = transform.position;
#if UNITY_EDITOR
        var view = UnityEditor.SceneView.lastActiveSceneView;
        if (view != null && view.camera != null)
            pivot = view.camera.transform.position;
#endif

        float rangeSqr = gizmoRange * gizmoRange;
        var links = graph.Links;

        if (drawCrossings)
        {
            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if ((link.takeoff - pivot).sqrMagnitude > rangeSqr)
                    continue;

                // One colour, both ends alike. Nothing here distinguishes the direction, because the
                // crossing does not have one -- it is travelled either way along the same curve.
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(link.takeoff, 0.15f);
                Gizmos.DrawSphere(link.landing, 0.15f);

                Gizmos.color = new Color(0f, 1f, 1f, 0.7f);
                DrawArc(link);
            }
        }

        if (!drawGroundEdges)
            return;

        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.25f);
        for (int node = 0; node < graph.NodeCount; node++)
        {
            Vector3 origin = graph.NodePosition(node);
            if ((origin - pivot).sqrMagnitude > rangeSqr)
                continue;

            for (int e = graph.GroundEdgeBegin(node); e < graph.GroundEdgeEnd(node); e++)
                Gizmos.DrawLine(origin, graph.NodePosition(graph.GroundEdgeTarget(e)));
        }
    }

    /// <summary>
    /// Draws the arc that was actually baked rather than the chord, and rather than re-solving one:
    /// what is on screen is what an entity will fly, in either direction, since the reverse retraces
    /// this same curve.
    /// </summary>
    private static void DrawArc(in JumpLink link)
    {
        const int steps = 10;
        Vector3 velocity = link.ForwardVelocity;
        Vector3 previous = link.takeoff;

        for (int i = 1; i <= steps; i++)
        {
            Vector3 point = JumpArc.Sample(link.takeoff, velocity, link.duration * i / steps);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
