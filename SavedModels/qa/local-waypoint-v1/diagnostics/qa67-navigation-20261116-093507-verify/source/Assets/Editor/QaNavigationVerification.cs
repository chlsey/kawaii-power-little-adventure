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
            VerifyFailedSelection(actor, sensors);
            File.WriteAllText(Path.Combine(directory, "verification.json"), JsonUtility.ToJson(new Result
            {
                status = "passed", scenarios = 3, assertions = checks,
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

    private static int checks;
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

    [Serializable] private class Result
    {
        public string status;
        public int scenarios, assertions;
        public Bounds controller_bounds;
        public float physical_radius, clearance;
        public Vector3 bottom, top;
    }
}
