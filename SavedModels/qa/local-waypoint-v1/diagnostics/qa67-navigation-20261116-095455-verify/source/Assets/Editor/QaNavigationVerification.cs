using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Direct physics and navigation scenarios, run with -executeMethod.</summary>
public static class QaNavigationVerification
{
    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        string directory = args[Array.IndexOf(args, "-trainingRunDirectory") + 1];
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var player = new GameObject("scaled verification player");
            player.transform.position = new Vector3(0.125f, 0f, 0.125f);
            player.transform.localScale = Vector3.one * 0.6f;
            var actor = player.AddComponent<CharacterController>();
            actor.radius = 0.3f; actor.height = 1.8f; actor.center = Vector3.up * 0.9f;
            var sensors = player.AddComponent<ModelSensorPackage>();
            Physics.SyncTransforms();
            var body = new QaNavigationCapsule(actor);
            Bounds initialBounds = actor.bounds;
            VerifyScaledBody(actor, body);
            VerifyPassage(actor, sensors, body);
            VerifyCornerHandoff(actor, sensors);
            VerifyBoundaryRecovery(actor, sensors);
            VerifyFailedSelection(actor, sensors);
            File.WriteAllText(Path.Combine(directory, "verification.json"), JsonUtility.ToJson(new Result
            {
                status = "passed", scenarios = 5, assertions = checks,
                controller_bounds = initialBounds, physical_radius = body.Radius,
                clearance = body.Clearance, bottom = body.Bottom, top = body.Top
            }, true));
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(directory, "verification-failure.txt"), error.ToString());
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    // observed L passage -> follow coordinator targets -> retain turn until next segment clears -> reach end
    private static void VerifyCornerHandoff(CharacterController actor, ModelSensorPackage sensors)
    {
        actor.transform.position = new Vector3(0.125f, 0f, -2.125f);
        var walls = new GameObject[4];
        Vector3[] centers = { new Vector3(-0.5f, 1f, -1f), new Vector3(0.5f, 1f, -1.5f),
            new Vector3(2f, 1f, 0.5f), new Vector3(2.5f, 1f, -0.5f) };
        Vector3[] sizes = { new Vector3(0.2f, 3f, 3.2f), new Vector3(0.2f, 3f, 2.2f),
            new Vector3(5.2f, 3f, 0.2f), new Vector3(4.2f, 3f, 0.2f) };
        for (int i = 0; i < walls.Length; i++)
        {
            walls[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
            walls[i].transform.position = centers[i]; walls[i].transform.localScale = sizes[i];
            walls[i].AddComponent<ModelSensorTarget>();
        }
        Physics.SyncTransforms(); sensors.BeginAttempt();
        // Supply a previously observed L-shaped strip, not a hidden generator route.
        for (int z = -9; z <= 0; z++)
        for (int x = -1; x <= 0; x++) sensors.Grid.MarkVisited(QaObservedRoutePlanner.Point(new Vector2Int(x, z), 0f));
        for (int x = 0; x <= 12; x++)
        for (int z = -1; z <= 0; z++) sensors.Grid.MarkVisited(QaObservedRoutePlanner.Point(new Vector2Int(x, z), 0f));
        Vector3 target = new Vector3(3.125f, 0f, 0.125f);
        var navigation = new QaNavigationCoordinator(new FixedGoalSelector(ModelSensorGrid.WorldToCell(target)));
        navigation.DiagnosticEvent += reason => Debug.Log("corner-check " + reason + " at " + actor.transform.position +
            " target " + navigation.Waypoint + " route " + navigation.Route.Count);
        navigation.Advance(sensors, actor, false);
        int retainedTurns = 0;
        for (int step = 0; step < 100 && QaNavigationCoordinator.Distance(actor.transform.position, target) > 0.5f; step++)
        {
            Require(navigation.HasWaypoint, "Corner passage retains navigation guidance at step " + step +
                ": " + navigation.Status + " position " + actor.transform.position);
            Vector3 direction = navigation.Waypoint - actor.transform.position; direction.y = 0f;
            actor.Move(direction.normalized * Mathf.Min(0.15f, direction.magnitude));
            Physics.SyncTransforms(); sensors.Capture();
            int version = navigation.WaypointVersion;
            float distance = QaNavigationCoordinator.Distance(actor.transform.position, navigation.Waypoint);
            navigation.Advance(sensors, actor, true);
            if (distance <= 0.5f && distance > 0.05f && navigation.WaypointVersion == version && navigation.HasWaypoint)
                retainedTurns++;
        }
        Require(QaNavigationCoordinator.Distance(actor.transform.position, target) <= 0.5f,
            "Reference movement traverses the L passage");
        Require(retainedTurns > 0 && navigation.AbandonedGoals == 0,
            "Early arrival defers a blocked handoff instead of abandoning the goal");
        foreach (GameObject wall in walls) Object.DestroyImmediate(wall);
        Physics.SyncTransforms();
    }

    private static int checks;

    // safe body beside wall in occupied voxel -> sweep out to known free space -> real overlap still blocks
    private static void VerifyBoundaryRecovery(CharacterController actor, ModelSensorPackage sensors)
    {
        actor.transform.position = new Vector3(0.29f, 0f, 0.125f);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(0.74f, 1f, 0f);
        wall.transform.localScale = new Vector3(0.5f, 3f, 8f);
        wall.AddComponent<ModelSensorTarget>();
        Physics.SyncTransforms(); sensors.BeginAttempt();
        var body = new QaNavigationCapsule(actor);
        var planner = new QaObservedRoutePlanner(sensors.Grid, body.Clearance);
        Vector3 escape = actor.transform.position + Vector3.left;
        Require(sensors.Grid.ReadCell(ModelSensorGrid.WorldToCell(actor.transform.position)).Wall,
            "Coarse wall-hit voxel contains the physically usable actor position");
        Require(!planner.SegmentObserved(actor.transform.position, escape) &&
            planner.SegmentObserved(actor.transform.position, escape, true), "Only the starting voxel can be left");
        Require(sensors.CanUseExplorationEscape(escape, actor), "Actual capsule safely sweeps away from wall");
        var navigation = new QaNavigationCoordinator(new NoGoalSelector());
        navigation.Advance(sensors, actor, false);
        Require(navigation.HasWaypoint && navigation.Status == "recovering", "Boundary overlap receives recovery guidance");
        Require(sensors.CanUseExplorationEscape(navigation.Waypoint, actor), "Assigned escape passes physical checks");
        actor.transform.position += Vector3.right * 0.15f; Physics.SyncTransforms(); sensors.Capture();
        Require(!sensors.CanUseExplorationEscape(escape, actor), "Actual body-wall overlap cannot bypass sweep safety");
        Object.DestroyImmediate(wall); Physics.SyncTransforms();
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    // scaled controller -> read world capsule -> check bounds and previous overestimate
    private static void VerifyScaledBody(CharacterController actor, QaNavigationCapsule body)
    {
        Require(Mathf.Abs(body.Radius - Mathf.Max(actor.bounds.extents.x, actor.bounds.extents.z)) < 0.0001f,
            "World radius matches active collider bounds");
        Require(body.Radius < actor.radius, "Scaled body is smaller than its local radius");
        Require(Mathf.Abs(body.Top.y + body.Radius - actor.bounds.max.y) < 0.0001f &&
            Mathf.Abs(body.Bottom.y - body.Radius - actor.bounds.min.y) < 0.0001f,
            "Capsule endpoints match world height");
        Require(body.Clearance < actor.radius + actor.skinWidth + 0.05f,
            "Normal clearance no longer uses unscaled dimensions");
    }

    // physically clear narrow passage -> refine inflated observed cells -> route; close passage -> reject
    private static void VerifyPassage(CharacterController actor, ModelSensorPackage sensors, QaNavigationCapsule body)
    {
        float halfGap = body.Clearance + 0.02f;
        GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cube);
        left.transform.position = new Vector3(0.125f - halfGap - 0.25f, 1f, 1f);
        right.transform.position = new Vector3(0.125f + halfGap + 0.25f, 1f, 1f);
        left.transform.localScale = right.transform.localScale = new Vector3(0.5f, 3f, 8f);
        left.AddComponent<ModelSensorTarget>(); right.AddComponent<ModelSensorTarget>();
        Physics.SyncTransforms(); sensors.BeginAttempt();
        Vector3 target = actor.transform.position + Vector3.forward * 2f;
        sensors.Grid.Trace(actor.transform.position, target, QaObjectType.None);
        var original = new QaObservedRoutePlanner(sensors.Grid, body.Clearance);
        var refined = new QaObservedRoutePlanner(sensors.Grid, body.Clearance,
            p => sensors.CanOccupyObservedPoint(p, body),
            (a, b) => sensors.CanTraverseObservedSegment(a, b, body), body.Position.y);
        Vector2Int start = ModelSensorGrid.WorldToCell(actor.transform.position);
        Require(!original.IsFree(start), "Full wall-cell inflation closes the test passage");
        Require(refined.IsFree(start), "Physical refinement opens the observed clear start");
        Require(refined.Route(actor.transform.position, ModelSensorGrid.WorldToCell(target)).Count > 0,
            "Refined observed passage has a route");
        Require(sensors.CanUseWaypoint(target, actor), "Shared capsule sweep accepts the clear passage");
        Require(!refined.IsFree(new Vector2Int(1000, 1000)), "Unknown cells stay blocked");
        Vector2Int wall = ModelSensorGrid.WorldToCell(new Vector3(right.transform.position.x - 0.2499f, 0f, 0.125f));
        Require(sensors.Grid.ReadCell(wall).Wall && !refined.IsFree(wall), "Recorded wall cells stay blocked");
        right.transform.position = new Vector3(0.125f + body.Radius * 0.5f + 0.25f, 1f, 1f);
        Physics.SyncTransforms();
        Require(!sensors.CanUseWaypoint(target, actor), "Physical collision still rejects a narrowed passage");
        Require(!sensors.CanOccupyObservedPoint(actor.transform.position, body), "Occupied start is rejected");
        Object.DestroyImmediate(left); Object.DestroyImmediate(right);
        Physics.SyncTransforms();
    }

    // failed goal search -> repeat unchanged pose -> cache; move/change map/exit/body/reset -> search again
    private static void VerifyFailedSelection(CharacterController actor, ModelSensorPackage sensors)
    {
        sensors.BeginAttempt();
        var selector = new NoGoalSelector();
        var navigation = new QaNavigationCoordinator(selector);
        navigation.Advance(sensors, actor, false);
        navigation.Advance(sensors, actor, true);
        Require(selector.Calls == 1 && navigation.GoalSearchSkips == 1, "Stationary failure is cached");
        actor.transform.position += Vector3.right * 0.1f; Physics.SyncTransforms();
        navigation.Advance(sensors, actor, true);
        Require(selector.Calls == 2, "Changed position retries selection");
        sensors.Grid.MarkVisited(actor.transform.position + Vector3.right * 20f);
        navigation.Advance(sensors, actor, true);
        Require(selector.Calls == 3, "Changed map retries selection");
        sensors.Grid.Trace(actor.transform.position, actor.transform.position + Vector3.forward, QaObjectType.LevelEnd);
        navigation.Advance(sensors, actor, true);
        Require(selector.Calls == 4, "New exit knowledge retries selection");
        actor.radius += 0.02f; Physics.SyncTransforms();
        navigation.Advance(sensors, actor, true);
        Require(selector.Calls == 5, "Changed body retries selection");
        navigation.Reset(); navigation.Advance(sensors, actor, false);
        Require(selector.Calls == 6 && navigation.GoalSearchSkips == 0, "Reset clears cached failures");
    }

    private sealed class NoGoalSelector : IMapGoalSelector
    {
        public int Calls;
        public bool TrySelect(QaObservedRoutePlanner planner, Vector3 position,
            System.Collections.Generic.IList<Vector3> excluded, bool allowExit, out QaMapGoal goal)
        {
            Calls++; goal = default; return false;
        }
    }

    private sealed class FixedGoalSelector : IMapGoalSelector
    {
        private readonly Vector2Int target;
        public FixedGoalSelector(Vector2Int target) { this.target = target; }
        public bool TrySelect(QaObservedRoutePlanner planner, Vector3 position,
            System.Collections.Generic.IList<Vector3> excluded, bool allowExit, out QaMapGoal goal)
        {
            goal = new QaMapGoal(target, true);
            return allowExit && planner.IsFree(target) &&
                QaNavigationCoordinator.Distance(position, QaObservedRoutePlanner.Point(target, position.y)) > 0.5f;
        }
    }

    [Serializable] private class Result
    {
        public string status;
        public int scenarios, assertions;
        public Bounds controller_bounds;
        public float physical_radius, clearance;
        public Vector3 bottom, top;
    }
}
