using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Inspector for <see cref="JumpLinkMap"/>: bakes the graph, reports what came out of it, and solves
/// a route between two transforms so the plan can be seen before the game is ever run.
///
/// The preview matters because most of what this system decides is invisible at rest. Gizmos show
/// which jumps exist; only a solved route shows which ones an enemy would actually choose, and how
/// that compares to walking. It is a design aid, not a substitute for watching it in Play Mode --
/// nothing here exercises the physics of an arc or the mover consuming the route.
/// </summary>
[CustomEditor(typeof(JumpLinkMap))]
public class JumpLinkMapEditor : Editor
{
    private Transform previewFrom;
    private Transform previewTo;
    private EnemyConfig previewConfig;
    private string previewResult;
    private bool previewFoldout = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var map = (JumpLinkMap)target;

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(map.Graph == null))
        {
            if (GUILayout.Button("Rebuild Jump Graph", GUILayout.Height(28)))
                map.Rebuild();
        }

        if (map.Graph == null)
        {
            EditorGUILayout.HelpBox(
                "No JumpGraph asset assigned. Create one via Assets > Create > OWL > AI > Jump Graph " +
                "and drop it in, then rebake. Without it enemies fall back to plain ground pathing.",
                MessageType.Warning);
            return;
        }

        DrawGraphSummary(map.Graph);
        DrawRoutePreview(map.Graph);
    }

    private void DrawGraphSummary(JumpGraph graph)
    {
        if (graph.IsEmpty)
        {
            EditorGUILayout.HelpBox("Graph is empty. Bake a NavMesh first, then rebuild.", MessageType.Info);
            return;
        }

        float longestSpan = 0f, fastestLaunch = 0f, tallestArc = 0f;
        for (int i = 0; i < graph.LinkCount; i++)
        {
            var link = graph.Link(i);
            longestSpan = Mathf.Max(longestSpan, link.horizontalDistance);
            fastestLaunch = Mathf.Max(fastestLaunch, link.launchForward);
            tallestArc = Mathf.Max(tallestArc, link.RequiredJumpHeight);
        }

        int groundEdges = 0;
        for (int n = 0; n < graph.NodeCount; n++)
            groundEdges += graph.GroundEdgeEnd(n) - graph.GroundEdgeBegin(n);

        EditorGUILayout.LabelField("Baked graph", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{graph.LinkCount} crossings, each usable both ways");
        EditorGUILayout.LabelField($"{graph.NodeCount} nodes, {groundEdges} ground edges");
        EditorGUILayout.LabelField($"longest span {longestSpan:0.##}m, steepest arc needs a " +
                                   $"{tallestArc:0.##}m jump at {fastestLaunch:0.##}m/s");
        EditorGUILayout.LabelField($"envelope: climb {graph.BakedMaxJumpUpHeight:0.##}m, drop " +
                                   $"{graph.BakedMaxDropHeight:0.##}m, run {graph.BakedRunSpeed:0.##}m/s");

        DrawReachability(graph);
    }

    /// <summary>
    /// How much of the graph each enemy class can actually use.
    ///
    /// The bake envelope is a ceiling on what gets generated, not a promise anyone can fly it, and the
    /// two can drift a long way apart without anything saying so: a graph baked for a ten-metre jumper
    /// left a four-metre class able to use a fifth of the climbs, and the only visible symptom was
    /// enemies quietly no longer jumping. <see cref="JumpGraph.Covers"/> notices only the harmless
    /// direction, a class too strong for its graph.
    /// </summary>
    private void DrawReachability(JumpGraph graph)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Reachable per class", EditorStyles.boldLabel);

        foreach (var guid in AssetDatabase.FindAssets("t:EnemyConfig"))
        {
            var config = AssetDatabase.LoadAssetAtPath<EnemyConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (config == null)
                continue;

            var capability = JumpCapability.FromConfig(config);

            int usable = 0, forward = 0, reverse = 0;
            float unlockAt = 0f;
            for (int i = 0; i < graph.LinkCount; i++)
            {
                var link = graph.Link(i);
                bool f = capability.Allows(link, false);
                bool r = capability.Allows(link, true);
                if (f) forward++;
                if (r) reverse++;
                if (f || r) usable++;
                else if (link.launchForward <= capability.runSpeed)
                    unlockAt = Mathf.Max(unlockAt, Mathf.Min(link.RequiredJumpHeight, link.ReverseRequiredJumpHeight));
            }

            float share = graph.LinkCount > 0 ? usable / (float)graph.LinkCount : 0f;
            EditorGUILayout.LabelField($"{config.className}: {usable}/{graph.LinkCount} ({share:P0}) " +
                                       $"— {forward} forward, {reverse} reverse");

            if (share < 0.5f && unlockAt > capability.maxJumpUpHeight)
            {
                EditorGUILayout.HelpBox(
                    $"{config.className} jumps {capability.maxJumpUpHeight:0.##}m at {capability.runSpeed:0.##}m/s " +
                    $"and can use only {share:P0} of this level's crossings. maxJumpUpHeight of " +
                    $"{unlockAt:0.##}m would unlock the rest; shortening the takeoff and landing insets " +
                    "is the other lever.", MessageType.Warning);
            }
        }
    }

    private void DrawRoutePreview(JumpGraph graph)
    {
        EditorGUILayout.Space();
        previewFoldout = EditorGUILayout.Foldout(previewFoldout, "Route preview", true);
        if (!previewFoldout)
            return;

        previewFrom = (Transform)EditorGUILayout.ObjectField("From", previewFrom, typeof(Transform), true);
        previewTo = (Transform)EditorGUILayout.ObjectField("To", previewTo, typeof(Transform), true);
        previewConfig = (EnemyConfig)EditorGUILayout.ObjectField("As enemy class", previewConfig, typeof(EnemyConfig), false);

        using (new EditorGUI.DisabledScope(previewFrom == null || previewTo == null || previewConfig == null))
        {
            if (GUILayout.Button("Solve route"))
                SolvePreview(graph);
        }

        if (!string.IsNullOrEmpty(previewResult))
            EditorGUILayout.HelpBox(previewResult, MessageType.None);
    }

    private void SolvePreview(JumpGraph graph)
    {
        if (!NavMesh.SamplePosition(previewFrom.position, out var fromHit, 4f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(previewTo.position, out var toHit, 4f, NavMesh.AllAreas))
        {
            previewResult = "One of the two points isn't near the NavMesh.";
            return;
        }

        var capability = JumpCapability.FromConfig(previewConfig);
        var profile = new LocomotionProfile(previewConfig.moveSpeed, previewConfig.acceleration,
                                            previewConfig.landingRecovery);

        var route = new JumpRoute();
        var pathfinder = JumpPathfinder.Shared;

        bool found = pathfinder.TrySolve(graph, fromHit.position, toHit.position,
                                         capability, profile, 0f, route);

        if (!found)
        {
            previewResult = "No route at all -- not even a partial walk.";
            return;
        }

        string walk = float.IsInfinity(pathfinder.LastGroundTime)
            ? "walking never arrives"
            : $"walking {pathfinder.LastGroundTime:0.00}s";

        previewResult =
            $"{route.Count} waypoints, {route.JumpCount} jump(s)\n" +
            $"route {route.EstimatedTime:0.00}s vs {walk}\n" +
            $"{pathfinder.LastExpansions} expansions, {pathfinder.LastPathQueries} path queries";

        DrawPreviewInScene(route);
    }

    /// <summary>
    /// Draws the solved route into the scene view. Jumps are drawn as the arc that will really be
    /// flown rather than a straight line, because whether an arc clears the lip it is aimed over is
    /// exactly the thing worth looking at.
    /// </summary>
    private void DrawPreviewInScene(JumpRoute route)
    {
        const float duration = 20f;

        for (int i = 1; i < route.Count; i++)
        {
            var previousWaypoint = route[i - 1];
            var waypoint = route[i];

            if (!previousWaypoint.jumpFromHere)
            {
                Debug.DrawLine(previousWaypoint.position, waypoint.position, Color.green, duration, false);
                continue;
            }

            // The arc the route is carrying, not one solved here: the point of the preview is to see
            // what will actually be flown.
            const int steps = 12;
            Vector3 previous = previousWaypoint.position;
            for (int s = 1; s <= steps; s++)
            {
                Vector3 point = JumpArc.Sample(previousWaypoint.position, previousWaypoint.launchVelocity,
                                               previousWaypoint.flightTime * s / steps);
                Debug.DrawLine(previous, point, Color.cyan, duration, false);
                previous = point;
            }
        }

        SceneView.RepaintAll();
    }
}
