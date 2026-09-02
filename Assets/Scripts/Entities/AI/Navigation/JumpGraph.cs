using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The baked route graph enemies plan over: every jump the level affords, plus how long it takes to
/// walk between the ends of those jumps.
///
/// Two kinds of edge live here. **Jump edges** are the links themselves -- takeoff to landing,
/// through the air. **Ground edges** are the walk from a landing back to some other takeoff, and
/// their lengths come from real <c>NavMesh.CalculatePath</c> calls run once at bake time. Baking
/// them is what makes a multi-jump route affordable at runtime: the search is a walk over these
/// arrays, with pathfinding calls needed only to get on and off the graph.
///
/// Only landing-to-takeoff ground edges are stored, because that is the only direction a route can
/// use one. Getting *to* the first takeoff and *from* the last landing is solved live against the
/// entity's real position (see <see cref="JumpPathfinder"/>); everything in between is table lookup.
///
/// This is an asset rather than scene data on purpose: a rebake replaces thousands of links and tens
/// of thousands of edges, and burying that in the scene file would make every rebake an unreadable
/// diff on top of whatever else was being worked on.
/// </summary>
[CreateAssetMenu(fileName = "JumpGraph", menuName = "OWL/AI/Jump Graph")]
public class JumpGraph : ScriptableObject
{
    /// <summary>Node is the takeoff end of at least one link.</summary>
    public const byte FlagTakeoff = 1 << 0;

    /// <summary>Node is the landing end of at least one link.</summary>
    public const byte FlagLanding = 1 << 1;

    [Header("Baked data")]
    [SerializeField] private JumpLink[] links = new JumpLink[0];
    [SerializeField] private Vector3[] nodes = new Vector3[0];
    [SerializeField] private byte[] nodeFlags = new byte[0];

    [Tooltip("CSR row offsets into the ground edge arrays; length is node count + 1.")]
    [SerializeField] private int[] groundEdgeStart = new int[1];
    [SerializeField] private int[] groundEdgeTarget = new int[0];
    [SerializeField] private float[] groundEdgeLength = new float[0];

    [Tooltip("CSR row offsets into jumpEdgeLink; length is node count + 1.")]
    [SerializeField] private int[] jumpEdgeStart = new int[1];

    [Tooltip("Link index per edge, with the direction folded into the sign: a plain index travels the " +
             "arc as stored, its bitwise complement travels the same arc backwards. Every link " +
             "therefore appears twice -- once at each end -- which is what lets a route descend a " +
             "ledge it could equally have climbed, without a second record for the descent.")]
    [SerializeField] private int[] jumpEdgeLink = new int[0];

    [Header("Envelope this graph was baked with")]
    [SerializeField] private float bakedMaxJumpUpHeight;
    [SerializeField] private float bakedMaxDropHeight;
    [SerializeField] private float bakedRunSpeed;

    [Tooltip("Fastest horizontal speed any baked arc reaches. Keeps the A* heuristic admissible -- an " +
             "arc can outrun the entity, and a heuristic that assumed otherwise would stop the search " +
             "being optimal.")]
    [SerializeField] private float maxJumpHorizontalSpeed = 1f;

    [SerializeField] private float gridCellSize = 8f;

    private Dictionary<Vector2Int, List<int>> nodeGrid;

    public IReadOnlyList<JumpLink> Links => links;
    public int LinkCount => links.Length;
    public int NodeCount => nodes.Length;
    public bool IsEmpty => links.Length == 0 || nodes.Length == 0;

    public float MaxJumpHorizontalSpeed => Mathf.Max(0.01f, maxJumpHorizontalSpeed);
    public float BakedMaxJumpUpHeight => bakedMaxJumpUpHeight;
    public float BakedMaxDropHeight => bakedMaxDropHeight;
    public float BakedRunSpeed => bakedRunSpeed;

    public JumpLink Link(int index) => links[index];
    public Vector3 NodePosition(int node) => nodes[node];
    public bool NodeIs(int node, byte flag) => (nodeFlags[node] & flag) != 0;

    // ---- Graph traversal ----------------------------------------------------
    // CSR accessors rather than returning collections: the A* inner loop walks these thousands of
    // times per query, and anything that allocates an enumerator there shows up in the frame.

    public int GroundEdgeBegin(int node) => groundEdgeStart[node];
    public int GroundEdgeEnd(int node) => groundEdgeStart[node + 1];
    public int GroundEdgeTarget(int edge) => groundEdgeTarget[edge];
    public float GroundEdgeLength(int edge) => groundEdgeLength[edge];

    public int JumpEdgeBegin(int node) => jumpEdgeStart[node];
    public int JumpEdgeEnd(int node) => jumpEdgeStart[node + 1];

    /// <summary>Which link this edge travels. See <see cref="JumpEdgeIsReverse"/> for which way round.</summary>
    public int JumpEdgeLink(int edge)
    {
        int encoded = jumpEdgeLink[edge];
        return encoded < 0 ? ~encoded : encoded;
    }

    /// <summary>Whether this edge travels its link backwards -- the same arc, flown the other way.</summary>
    public bool JumpEdgeIsReverse(int edge) => jumpEdgeLink[edge] < 0;

    /// <summary>Node this edge arrives at, which end depending on its direction.</summary>
    public int JumpEdgeTarget(int edge)
    {
        int encoded = jumpEdgeLink[edge];
        var link = links[encoded < 0 ? ~encoded : encoded];
        return encoded < 0 ? link.takeoffNode : link.landingNode;
    }

    // ---- Spatial lookup -----------------------------------------------------

    private void OnEnable() => nodeGrid = null;

    private void EnsureIndex()
    {
        if (nodeGrid != null)
            return;

        gridCellSize = Mathf.Max(1f, gridCellSize);
        nodeGrid = new Dictionary<Vector2Int, List<int>>();

        for (int i = 0; i < nodes.Length; i++)
        {
            var cell = CellOf(nodes[i]);
            if (!nodeGrid.TryGetValue(cell, out var bucket))
            {
                bucket = new List<int>();
                nodeGrid[cell] = bucket;
            }
            bucket.Add(i);
        }
    }

    private Vector2Int CellOf(Vector3 p) =>
        new(Mathf.FloorToInt(p.x / gridCellSize), Mathf.FloorToInt(p.z / gridCellSize));

    /// <summary>
    /// Nodes carrying <paramref name="flag"/> whose flat distance from <paramref name="center"/> is
    /// within <paramref name="radius"/>, appended to <paramref name="results"/>. Only the grid cells
    /// the radius actually overlaps are visited, so cost tracks the local density of links rather
    /// than the size of the level.
    /// </summary>
    public void QueryNodes(Vector3 center, float radius, byte flag, List<int> results)
    {
        if (nodes.Length == 0)
            return;

        EnsureIndex();

        float radiusSqr = radius * radius;
        int range = Mathf.CeilToInt(radius / gridCellSize);
        var origin = CellOf(center);

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dz = -range; dz <= range; dz++)
            {
                if (!nodeGrid.TryGetValue(new Vector2Int(origin.x + dx, origin.y + dz), out var bucket))
                    continue;

                for (int b = 0; b < bucket.Count; b++)
                {
                    int node = bucket[b];
                    if ((nodeFlags[node] & flag) == 0)
                        continue;

                    Vector3 offset = nodes[node] - center;
                    offset.y = 0f;
                    if (offset.sqrMagnitude <= radiusSqr)
                        results.Add(node);
                }
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="capability"/> stays inside the envelope this graph was baked for.
    /// An entity configured to jump further or higher than the bake explored isn't dangerous -- it
    /// simply won't find links it believes it could make, because they were never generated. Worth
    /// saying out loud rather than leaving as a silently duller AI.
    /// </summary>
    public bool Covers(in JumpCapability capability, out string shortfall)
    {
        shortfall = null;

        if (capability.maxJumpUpHeight > bakedMaxJumpUpHeight + 0.01f)
            shortfall = $"climbs up to {capability.maxJumpUpHeight:0.##}m but the graph was baked to {bakedMaxJumpUpHeight:0.##}m";
        else if (capability.maxDropHeight > bakedMaxDropHeight + 0.01f)
            shortfall = $"drops up to {capability.maxDropHeight:0.##}m but the graph was baked to {bakedMaxDropHeight:0.##}m";
        else if (capability.runSpeed > bakedRunSpeed + 0.01f)
            shortfall = $"runs at {capability.runSpeed:0.##}m/s but the graph was baked for {bakedRunSpeed:0.##}m/s, so its links stop short of this class's reach";

        return shortfall == null;
    }

#if UNITY_EDITOR
    /// <summary>Replaces the entire contents of the graph. Bake-time only -- see <see cref="JumpLinkMap"/>.</summary>
    public void SetBakedData(JumpLink[] bakedLinks, Vector3[] bakedNodes, byte[] bakedNodeFlags,
                             int[] groundStart, int[] groundTarget, float[] groundLength,
                             int[] jumpStart, int[] jumpLink,
                             float maxUp, float maxDrop, float runSpeed,
                             float fastestArcSpeed, float cellSize)
    {
        links = bakedLinks;
        nodes = bakedNodes;
        nodeFlags = bakedNodeFlags;
        groundEdgeStart = groundStart;
        groundEdgeTarget = groundTarget;
        groundEdgeLength = groundLength;
        jumpEdgeStart = jumpStart;
        jumpEdgeLink = jumpLink;

        bakedMaxJumpUpHeight = maxUp;
        bakedMaxDropHeight = maxDrop;
        bakedRunSpeed = runSpeed;
        maxJumpHorizontalSpeed = Mathf.Max(0.01f, fastestArcSpeed);
        gridCellSize = Mathf.Max(1f, cellSize);

        nodeGrid = null;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public int GroundEdgeCount => groundEdgeTarget.Length;
#endif
}
