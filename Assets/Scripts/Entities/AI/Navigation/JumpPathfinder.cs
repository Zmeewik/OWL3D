using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Finds the quickest way from A to B when jumping is on the table: A* over the baked
/// <see cref="JumpGraph"/>, costed in seconds, against the plain walk as a baseline.
///
/// The previous approach could only ever splice a *single* nearby link into a NavMesh path, so a
/// route needing two jumps, or a jump starting further away than the enemy's own search radius, was
/// unrepresentable -- the AI would take the long way round without ever knowing a shorter one existed.
/// Searching a real graph is what makes an arbitrary chain of jumps reachable, and what turns "jump
/// or go around" from a special case into the ordinary outcome of comparing two costs.
///
/// The walk is always computed and always competes. A jump route is returned only when it is
/// genuinely faster, which is exactly the wanted behaviour: leap when the path to the target runs
/// over the ledge, go around when it doesn't -- or when this entity's legs can't make the leap, since
/// links outside its <see cref="JumpCapability"/> are never expanded in the first place.
///
/// Cost is kept bounded three ways: an early-out that skips the search entirely when the walk is
/// already near-optimal (the common flat chase), a per-frame ceiling on full solves, and a hard cap
/// on expansions. Interior ground segments are turned into corner lists only for the route that
/// actually wins, so the losers never pay for pathfinding detail nobody reads.
/// </summary>
public sealed class JumpPathfinder
{
    public static JumpPathfinder Shared { get; } = new JumpPathfinder();

    /// <summary>How far from either end of the trip a link endpoint may sit and still be considered.</summary>
    private const float SearchRadius = 35f;

    private const int MaxEntryCandidates = 6;
    private const int MaxExitCandidates = 6;

    /// <summary>
    /// How many candidates may be pathfound before the search settles for however few it has.
    ///
    /// Without this the candidate loops run until they collect their full quota of *successes*, and
    /// in exactly the situation jump routing exists for -- a region the NavMesh can't reach -- nearly
    /// every candidate fails, so the loop pays for the whole neighbourhood. Measured at 73 path
    /// queries on a route across disconnected geometry, against the 13 the design budgets for.
    /// Candidates are tried best-first, so a truncated scan keeps the ones most likely to matter.
    ///
    /// Sized generously rather than tightly. Ranking is by how short a route through a candidate
    /// could be, which knows nothing about whether it is reachable at all, so in a fragmented region
    /// a run of top-ranked candidates can all fail before a usable one turns up. Measured over 161
    /// node pairs the NavMesh cannot connect: 12 attempts found 75 routes, 20 found 88, unlimited
    /// found 97. Tightening it costs exactly the routes jump planning exists to find, and buys about
    /// a fifth of a millisecond.
    /// </summary>
    private const int MaxCandidateAttempts = 20;
    private const int MaxExpansions = 4096;
    private const int MaxCorners = 128;

    /// <summary>
    /// How much longer than the straight line the walk may be before a jump is even worth looking
    /// for. A ground route already within this of the ideal has no corner left to cut, so nothing a
    /// jump does can help -- this is what keeps an ordinary chase across open floor from paying for a
    /// graph search every repath.
    /// </summary>
    private const float DetourSlack = 1.15f;

    /// <summary>Safety valve, not a tuning knob: keeps a crowd of enemies from all solving on one frame.</summary>
    private const int MaxSolvesPerFrame = 2;

    /// <summary>
    /// Height difference above which a route segment that can't be walked in a straight line is
    /// treated as a gap to be flown.
    ///
    /// Has to clear the bake's own agentClimb (0.62m here) with real margin. This level is built from
    /// many separate floor pieces sitting at the same height but not quite welded, so their shared
    /// edge bakes as two islands joined by a trivial link -- blocked to a NavMesh raycast, and a
    /// "gap" by that test alone, over a difference an agent simply steps across. Requiring a real
    /// height difference is what tells an actual ledge apart from a seam.
    /// </summary>
    private const float NativeLinkMinHeight = 1f;

    // ---- Diagnostics (read by the editor preview and Play Mode checks) ------

    /// <summary>Seconds the plain walk would take, or infinity when walking doesn't arrive at all.</summary>
    public float LastGroundTime { get; private set; }

    /// <summary>Seconds the route actually returned is predicted to take.</summary>
    public float LastRouteTime { get; private set; }

    public bool LastRouteUsedJumps { get; private set; }
    public int LastExpansions { get; private set; }
    public int LastPathQueries { get; private set; }

    // ---- Scratch -----------------------------------------------------------

    private readonly NavMeshPath groundPath = new();
    private readonly NavMeshPath segmentPath = new();
    private readonly Vector3[] cornerBuffer = new Vector3[MaxCorners];

    private readonly List<int> nodeBuffer = new();
    private readonly HashSet<int> nodeSeen = new();

    private readonly List<int> entryNodes = new();
    private readonly List<float> entryCosts = new();
    private readonly List<float> entrySpeeds = new();
    private readonly List<NavMeshPath> entryPaths = new();

    private readonly List<int> exitNodes = new();
    private readonly List<NavMeshPath> exitPaths = new();

    private readonly List<int> reversedNodes = new();
    private readonly List<int> reversedLinks = new();

    private float[] gCost = new float[0];
    private float[] arrivalSpeed = new float[0];
    private int[] cameFromNode = new int[0];
    private int[] cameFromLink = new int[0];
    private int[] openStamp = new int[0];
    private int[] closedStamp = new int[0];
    private int[] exitStamp = new int[0];
    private float[] exitLength = new float[0];
    private int generation;

    private float[] heapPriority = new float[64];
    private int[] heapNode = new int[64];
    private int heapCount;

    private int budgetFrame = -1;
    private int solvesThisFrame;

    private Vector3 rankFrom;
    private Vector3 rankTo;
    private JumpGraph rankGraph;
    private readonly Comparison<int> nodeRanker;

    private JumpPathfinder()
    {
        nodeRanker = CompareByDetour;
    }

    /// <summary>
    /// Fills <paramref name="result"/> with the quickest route from <paramref name="from"/> to
    /// <paramref name="to"/>. Both are expected to already sit on the NavMesh.
    ///
    /// Returns false only when there is no route at all -- not when the answer is simply "walk",
    /// which is a perfectly good route and the one returned most of the time.
    /// </summary>
    /// <param name="entrySpeed">Horizontal speed the entity is already carrying, so a route that
    /// keeps its momentum isn't costed as though it had to start from a standstill.</param>
    public bool TrySolve(JumpGraph graph, Vector3 from, Vector3 to,
                         in JumpCapability capability, in LocomotionProfile profile,
                         float entrySpeed, JumpRoute result)
    {
        result.Clear();
        LastRouteUsedJumps = false;
        LastExpansions = 0;
        LastPathQueries = 1;

        // The walk, always. It is both the fallback and the bar a jump route has to clear.
        NavMesh.CalculatePath(from, to, NavMesh.AllAreas, groundPath);
        bool groundComplete = groundPath.status == NavMeshPathStatus.PathComplete;

        float groundTime = float.PositiveInfinity;
        if (groundComplete)
        {
            int count = groundPath.GetCornersNonAlloc(cornerBuffer);
            if (count >= 2)
                groundTime = LocomotionCost.GroundTime(cornerBuffer, count, entrySpeed, profile, out _);
            else
                groundTime = 0f;
        }

        LastGroundTime = groundTime;

        if (graph == null || graph.IsEmpty ||
            !WorthSearching(from, to, groundComplete, groundTime, entrySpeed, profile) ||
            !ClaimFrameBudget())
        {
            return EmitGround(result, groundTime, capability);
        }

        EnsureScratch(graph.NodeCount);
        generation++;

        CollectEntries(graph, from, to, capability, profile, entrySpeed);
        if (entryNodes.Count == 0)
            return EmitGround(result, groundTime, capability);

        CollectExits(graph, from, to);
        if (exitNodes.Count == 0)
            return EmitGround(result, groundTime, capability);

        int bestExit = Search(graph, to, capability, profile, groundTime, out float bestTime);
        if (bestExit < 0)
            return EmitGround(result, groundTime, capability);

        return EmitJumpRoute(graph, result, bestExit, bestTime, to, capability);
    }

    // ---- Early-out ---------------------------------------------------------

    /// <summary>
    /// Whether a jump could plausibly beat the walk. A walk already close to the straight line
    /// between the two points has nothing to cut across. When the walk doesn't arrive at all, the
    /// opposite holds: any jump route is an improvement over never getting there.
    /// </summary>
    private static bool WorthSearching(Vector3 from, Vector3 to, bool groundComplete, float groundTime,
                                       float entrySpeed, in LocomotionProfile profile)
    {
        if (!groundComplete)
            return true;

        float idealTime = LocomotionCost.GroundTime(Vector3.Distance(from, to), entrySpeed, profile);
        return groundTime > idealTime * DetourSlack;
    }

    private bool ClaimFrameBudget()
    {
        int frame = Time.frameCount;
        if (frame != budgetFrame)
        {
            budgetFrame = frame;
            solvesThisFrame = 0;
        }

        if (solvesThisFrame >= MaxSolvesPerFrame)
            return false;

        solvesThisFrame++;
        return true;
    }

    // ---- Getting on and off the graph --------------------------------------

    /// <summary>
    /// Candidate endpoints, gathered around *both* ends of the trip.
    ///
    /// Searching only around the entity would miss the takeoff at the foot of a distant building, and
    /// searching only around the target would miss the ledge the entity has to leave right now to get
    /// anywhere at all. Both failures are real -- an enemy stranded on a roof has its only exit under
    /// its feet, while a target on a far roof has its only entrance fifty metres away -- so the union
    /// is gathered and then ranked by how short a route through each could possibly be.
    /// </summary>
    private void GatherCandidates(JumpGraph graph, Vector3 from, Vector3 to, byte flag)
    {
        nodeBuffer.Clear();
        nodeSeen.Clear();

        graph.QueryNodes(from, SearchRadius, flag, nodeBuffer);
        graph.QueryNodes(to, SearchRadius, flag, nodeBuffer);

        // The two queries overlap whenever the trip is short; drop the repeats before ranking.
        int write = 0;
        for (int i = 0; i < nodeBuffer.Count; i++)
        {
            if (nodeSeen.Add(nodeBuffer[i]))
                nodeBuffer[write++] = nodeBuffer[i];
        }
        nodeBuffer.RemoveRange(write, nodeBuffer.Count - write);

        rankGraph = graph;
        rankFrom = from;
        rankTo = to;
        nodeBuffer.Sort(nodeRanker);
    }

    /// <summary>
    /// Ranks a node by the shortest conceivable route through it. Nearest-to-the-entity would be the
    /// obvious ordering and is the wrong one: the takeoff that matters for a target on a distant roof
    /// is at the foot of that building, not underfoot. Summing both legs finds it while still
    /// preferring close links when they are on the way.
    /// </summary>
    private int CompareByDetour(int a, int b)
    {
        Vector3 pa = rankGraph.NodePosition(a);
        Vector3 pb = rankGraph.NodePosition(b);

        float scoreA = (pa - rankFrom).magnitude + (pa - rankTo).magnitude;
        float scoreB = (pb - rankFrom).magnitude + (pb - rankTo).magnitude;
        return scoreA.CompareTo(scoreB);
    }

    private void CollectEntries(JumpGraph graph, Vector3 from, Vector3 to,
                                in JumpCapability capability, in LocomotionProfile profile, float entrySpeed)
    {
        entryNodes.Clear();
        entryCosts.Clear();
        entrySpeeds.Clear();

        GatherCandidates(graph, from, to, JumpGraph.FlagTakeoff);

        int attempts = 0;
        for (int i = 0; i < nodeBuffer.Count && entryNodes.Count < MaxEntryCandidates
                                             && attempts < MaxCandidateAttempts; i++)
        {
            int node = nodeBuffer[i];

            // No point walking to a ledge whose every jump is beyond this entity. Free to test, so it
            // doesn't count against the attempt budget.
            if (!HasUsableJump(graph, node, capability))
                continue;

            var path = PathSlot(entryPaths, entryNodes.Count);
            attempts++;
            LastPathQueries++;
            NavMesh.CalculatePath(from, graph.NodePosition(node), NavMesh.AllAreas, path);
            if (path.status != NavMeshPathStatus.PathComplete)
                continue;

            int count = path.GetCornersNonAlloc(cornerBuffer);
            if (count < 1)
                continue;

            float cost = LocomotionCost.GroundTime(cornerBuffer, count, entrySpeed, profile, out float exitSpeed);
            entryNodes.Add(node);
            entryCosts.Add(cost);
            entrySpeeds.Add(exitSpeed);
        }
    }

    private void CollectExits(JumpGraph graph, Vector3 from, Vector3 to)
    {
        exitNodes.Clear();

        GatherCandidates(graph, from, to, JumpGraph.FlagLanding);

        int attempts = 0;
        for (int i = 0; i < nodeBuffer.Count && exitNodes.Count < MaxExitCandidates
                                             && attempts < MaxCandidateAttempts; i++)
        {
            int node = nodeBuffer[i];

            var path = PathSlot(exitPaths, exitNodes.Count);
            attempts++;
            LastPathQueries++;
            NavMesh.CalculatePath(graph.NodePosition(node), to, NavMesh.AllAreas, path);
            if (path.status != NavMeshPathStatus.PathComplete)
                continue;

            int count = path.GetCornersNonAlloc(cornerBuffer);
            if (count < 1)
                continue;

            // The length, not the time: how long this last leg takes depends on the speed the route
            // arrives carrying, which isn't known until the search settles the node.
            exitStamp[node] = generation;
            exitLength[node] = PathLength(cornerBuffer, count);
            exitNodes.Add(node);
        }
    }

    private static bool HasUsableJump(JumpGraph graph, int node, in JumpCapability capability)
    {
        for (int e = graph.JumpEdgeBegin(node); e < graph.JumpEdgeEnd(node); e++)
        {
            if (capability.Allows(graph.Link(graph.JumpEdgeLink(e)), graph.JumpEdgeIsReverse(e)))
                return true;
        }
        return false;
    }

    // ---- Parent-chain encoding ---------------------------------------------
    //
    // The chain has to remember which way a crossing was travelled as well as which one it was, and
    // -1 is already taken to mean "arrived on foot". Packing the direction into the low bit keeps
    // every real entry non-negative, so the sentinel stays unambiguous.

    private static int Encode(int linkIndex, bool reverse) => (linkIndex << 1) | (reverse ? 1 : 0);
    private static int DecodeLink(int encoded) => encoded >> 1;
    private static bool DecodeReverse(int encoded) => (encoded & 1) != 0;

    private static NavMeshPath PathSlot(List<NavMeshPath> pool, int index)
    {
        while (pool.Count <= index)
            pool.Add(new NavMeshPath());
        return pool[index];
    }

    private static float PathLength(Vector3[] corners, int count)
    {
        float total = 0f;
        for (int i = 1; i < count; i++)
            total += Vector3.Distance(corners[i - 1], corners[i]);
        return total;
    }

    // ---- The search --------------------------------------------------------

    private int Search(JumpGraph graph, Vector3 goal, in JumpCapability capability,
                       in LocomotionProfile profile, float groundTime, out float bestTime)
    {
        // An arc can outrun the entity, so the heuristic has to divide by whichever is faster or it
        // would over-estimate and stop being admissible.
        float referenceSpeed = Mathf.Max(profile.moveSpeed, graph.MaxJumpHorizontalSpeed);

        heapCount = 0;

        for (int i = 0; i < entryNodes.Count; i++)
            Relax(graph, entryNodes[i], entryCosts[i], entrySpeeds[i], -1, -1, goal, referenceSpeed);

        // The walk is the bar. Anything the search can't beat isn't worth returning.
        bestTime = groundTime;
        int bestExit = -1;
        int expansions = 0;

        while (heapCount > 0 && expansions < MaxExpansions)
        {
            HeapPop(out float priority, out int node);

            // Everything left in the queue is already worse than the best route found so far.
            if (priority >= bestTime)
                break;

            if (closedStamp[node] == generation)
                continue;
            closedStamp[node] = generation;
            expansions++;

            float g = gCost[node];
            float speed = arrivalSpeed[node];

            if (exitStamp[node] == generation)
            {
                float total = g + LocomotionCost.GroundTime(exitLength[node], speed, profile);
                if (total < bestTime)
                {
                    bestTime = total;
                    bestExit = node;
                }
            }

            // Every crossing is filed under both of its ends, so this same loop offers the ledge
            // above and the ledge below without the graph holding a separate record for each.
            for (int e = graph.JumpEdgeBegin(node); e < graph.JumpEdgeEnd(node); e++)
            {
                int linkIndex = graph.JumpEdgeLink(e);
                bool reverse = graph.JumpEdgeIsReverse(e);

                var link = graph.Link(linkIndex);
                if (!capability.Allows(link, reverse))
                    continue;

                float cost = LocomotionCost.JumpTime(link, profile, out float landingSpeed);
                Relax(graph, graph.JumpEdgeTarget(e), g + cost, landingSpeed,
                      node, Encode(linkIndex, reverse), goal, referenceSpeed);
            }

            for (int e = graph.GroundEdgeBegin(node); e < graph.GroundEdgeEnd(node); e++)
            {
                int target = graph.GroundEdgeTarget(e);
                float length = graph.GroundEdgeLength(e);

                Relax(graph, target,
                      g + LocomotionCost.GroundTime(length, speed, profile),
                      LocomotionCost.GroundExitSpeed(length, speed, profile),
                      node, -1, goal, referenceSpeed);
            }
        }

        LastExpansions = expansions;
        return bestExit;
    }

    private void Relax(JumpGraph graph, int node, float g, float speed, int fromNode, int fromLink,
                       Vector3 goal, float referenceSpeed)
    {
        // Settled nodes are not reopened. The heuristic ignores landing recovery, so it under-estimates
        // and stays admissible; it is not provably consistent, which in the worst case costs a slightly
        // suboptimal route rather than a wrong one -- worth it to keep the queue bounded.
        if (closedStamp[node] == generation)
            return;

        if (openStamp[node] == generation && gCost[node] <= g)
            return;

        openStamp[node] = generation;
        gCost[node] = g;
        arrivalSpeed[node] = speed;
        cameFromNode[node] = fromNode;
        cameFromLink[node] = fromLink;

        float heuristic = Vector3.Distance(graph.NodePosition(node), goal) / referenceSpeed;
        HeapPush(g + heuristic, node);
    }

    // ---- Emitting ----------------------------------------------------------

    /// <summary>
    /// Falls back to the plain NavMesh route. Re-reads the corners rather than trusting the shared
    /// buffer, which the candidate queries above will have overwritten by the time we get here.
    /// A partial path is still emitted: walking as close as the mesh allows is what the entity did
    /// before jump links existed, and is better than standing still.
    /// </summary>
    private bool EmitGround(JumpRoute result, float groundTime, in JumpCapability capability)
    {
        if (groundPath.status == NavMeshPathStatus.PathInvalid)
            return false;

        int count = groundPath.GetCornersNonAlloc(cornerBuffer);
        if (count <= 0)
            return false;

        result.AddCorners(cornerBuffer, count);
        FlyUnwalkableSegments(result, capability);
        result.SetEstimatedTime(float.IsInfinity(groundTime) ? 0f : groundTime);
        LastRouteTime = groundTime;
        LastRouteUsedJumps = result.JumpCount > 0;
        return result.IsValid;
    }

    /// <summary>
    /// Converts stretches of a route that can't actually be walked into jumps.
    ///
    /// The NavMesh bake generates its own off-mesh links for ledge drops and small gaps, and a path
    /// crossing one arrives as an ordinary pair of corners with nothing to distinguish it. Left
    /// alone the entity steers straight at the far corner and simply walks off the ledge, which is
    /// not what "it always jumps" means -- and, unlike a link from the graph, nothing would have
    /// checked the arc or watched for the landing.
    ///
    /// <c>NavMesh.Raycast</c> is the tool built for "can I walk straight from A to B on this mesh".
    /// It works on the mesh surface rather than in Euclidean space, so it isn't fooled by a slope or
    /// uneven ground the way sampling the midpoint's interpolated height was -- that used to flag
    /// plain sloped paths as flying over empty space.
    ///
    /// A blocked ray plus a height difference is good evidence of a ledge, but not proof, and the
    /// candidate is checked against the entity's envelope before it is believed. Path corners are the
    /// turning points of the funnel, so a corridor that doubles back on itself -- a ramp climbing
    /// inside a shaft -- puts two of them far apart in height with solid geometry on the straight
    /// line between: measured, this produced apparent crossings of 14.2m needing a 15.3m/s launch,
    /// alongside the genuine 1-3m ledges the bake's own drop height generates. Requiring the crossing
    /// to be one this entity could actually fly separates the two, and costs nothing when it can't --
    /// a leap it cannot make is no better than the walk it would otherwise do.
    /// </summary>
    private void FlyUnwalkableSegments(JumpRoute route, in JumpCapability capability)
    {
        for (int i = 1; i < route.Count; i++)
        {
            var from = route[i - 1];
            if (from.jumpFromHere)
                continue;

            Vector3 to = route[i].position;
            if (Mathf.Abs(to.y - from.position.y) < NativeLinkMinHeight)
                continue;

            if (!NavMesh.Raycast(from.position, to, out _, NavMesh.AllAreas))
                continue;

            // Solved here rather than baked, because this crossing is one the NavMesh invented and the
            // graph knows nothing about. The cheapest arc that reaches is the one taken; failing to
            // solve is itself the answer, and the segment stays a walk.
            if (!JumpArc.TrySolveMinimum(from.position, to, capability.runSpeed,
                                         out float launchUp, out float launchForward,
                                         out float duration, out float requiredHeight))
            {
                continue;
            }

            var link = JumpLink.Create(from.position, to, launchUp, launchForward, duration);
            if (requiredHeight > capability.maxJumpUpHeight || !capability.Allows(link, false))
                continue;

            route.ConvertToJump(i - 1, link);
        }
    }

    private bool EmitJumpRoute(JumpGraph graph, JumpRoute result, int exitNode, float totalTime,
                               Vector3 destination, in JumpCapability capability)
    {
        // Walk the parent chain back to the entry node, then replay it forwards.
        reversedNodes.Clear();
        reversedLinks.Clear();

        int cursor = exitNode;
        while (cursor >= 0)
        {
            reversedNodes.Add(cursor);
            reversedLinks.Add(cameFromLink[cursor]);
            cursor = cameFromNode[cursor];
        }

        int entrySlot = entryNodes.IndexOf(reversedNodes[reversedNodes.Count - 1]);
        int exitSlot = exitNodes.IndexOf(exitNode);
        if (entrySlot < 0 || exitSlot < 0)
            return false;

        // 1. From where we stand to the first takeoff.
        int count = entryPaths[entrySlot].GetCornersNonAlloc(cornerBuffer);
        result.AddCorners(cornerBuffer, count);

        // 2. Through the graph. reversedLinks[i] is the edge that *arrived* at reversedNodes[i], so
        //    replaying forwards means walking the list backwards.
        for (int i = reversedNodes.Count - 2; i >= 0; i--)
        {
            int encoded = reversedLinks[i];
            if (encoded >= 0)
            {
                result.AddJump(graph.Link(DecodeLink(encoded)), DecodeReverse(encoded));
                continue;
            }

            // A baked ground edge stores only its length; its corners are pathfound now, once, for the
            // route that actually won -- otherwise the entity would cut straight through the walls
            // that length was measured around.
            Vector3 segmentFrom = graph.NodePosition(reversedNodes[i + 1]);
            Vector3 segmentTo = graph.NodePosition(reversedNodes[i]);

            LastPathQueries++;
            NavMesh.CalculatePath(segmentFrom, segmentTo, NavMesh.AllAreas, segmentPath);
            if (segmentPath.status == NavMeshPathStatus.PathComplete)
            {
                int segmentCount = segmentPath.GetCornersNonAlloc(cornerBuffer);
                result.AddCorners(cornerBuffer, segmentCount);
            }
            else
            {
                result.AddCorner(segmentTo);
            }
        }

        // 3. From the last landing to the destination.
        count = exitPaths[exitSlot].GetCornersNonAlloc(cornerBuffer);
        result.AddCorners(cornerBuffer, count);
        result.AddCorner(destination);

        // The walking stretches of a jump route cross the bake's own off-mesh links just as readily
        // as a pure ground route does.
        FlyUnwalkableSegments(result, capability);

        result.SetEstimatedTime(totalTime);
        LastRouteTime = totalTime;
        LastRouteUsedJumps = result.JumpCount > 0;
        return result.IsValid;
    }

    // ---- Scratch management ------------------------------------------------

    private void EnsureScratch(int nodeCount)
    {
        if (gCost.Length >= nodeCount)
            return;

        int size = Mathf.NextPowerOfTwo(Mathf.Max(64, nodeCount));
        gCost = new float[size];
        arrivalSpeed = new float[size];
        cameFromNode = new int[size];
        cameFromLink = new int[size];
        openStamp = new int[size];
        closedStamp = new int[size];
        exitStamp = new int[size];
        exitLength = new float[size];
        // Fresh arrays read as generation 0, and the caller bumps the generation straight after, so
        // no stale stamp can survive a resize.
    }

    // ---- Binary heap -------------------------------------------------------

    private void HeapPush(float priority, int node)
    {
        if (heapCount == heapPriority.Length)
        {
            Array.Resize(ref heapPriority, heapCount * 2);
            Array.Resize(ref heapNode, heapCount * 2);
        }

        int index = heapCount++;
        heapPriority[index] = priority;
        heapNode[index] = node;

        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (heapPriority[parent] <= heapPriority[index])
                break;

            Swap(parent, index);
            index = parent;
        }
    }

    private void HeapPop(out float priority, out int node)
    {
        priority = heapPriority[0];
        node = heapNode[0];

        heapCount--;
        heapPriority[0] = heapPriority[heapCount];
        heapNode[0] = heapNode[heapCount];

        int index = 0;
        while (true)
        {
            int left = index * 2 + 1;
            if (left >= heapCount)
                break;

            int smallest = left;
            int right = left + 1;
            if (right < heapCount && heapPriority[right] < heapPriority[left])
                smallest = right;

            if (heapPriority[index] <= heapPriority[smallest])
                break;

            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int a, int b)
    {
        (heapPriority[a], heapPriority[b]) = (heapPriority[b], heapPriority[a]);
        (heapNode[a], heapNode[b]) = (heapNode[b], heapNode[a]);
    }
}
