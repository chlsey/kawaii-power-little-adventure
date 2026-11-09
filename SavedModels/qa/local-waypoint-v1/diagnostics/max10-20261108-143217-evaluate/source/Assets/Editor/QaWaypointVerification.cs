using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class QaLocalMovementSession
{
    // seeded distant starts -> no assignments or waypoint inputs -> idle timeout and ray-guided real exits
    private static IEnumerator VerifyNavigatorDisabled(QaSingleRoomRlEnvironment environment, Session session)
    {
        Require(environment.NavigatorDisabled, "Navigator actually disabled");
        for (int i = 0; i < session.evaluation_episodes; i++)
        {
            environment.ResetEpisode(session.evaluation_seed + i);
            float[] observation = environment.CollectObservation();
            Require(!environment.InitialExitVisible, "Unseen exit in no-navigator control");
            float distance = QaNavigationCoordinator.Distance(environment.SpawnPosition, environment.ExitPosition);
            Require(distance >= session.min_exit_distance - 0.0001f && distance <= session.max_exit_distance + 0.0001f,
                "Matched distant-exit placement");
            Require(!environment.Navigation.HasWaypoint && environment.Navigation.WaypointVersion == 0, "No assigned navigation target");
            for (int feature = 130; feature < 134; feature++) Require(observation[feature] == 0f, "No waypoint features");
        }
        environment.ResetEpisode(session.evaluation_seed);
        int actions = Mathf.RoundToInt(session.episode_seconds / QaPlayerController.StepDurationSeconds);
        for (int step = 0; step < actions; step++)
        {
            environment.ApplyAction(new float[2]);
            while (environment.IsActionRunning()) yield return null;
            float reward = environment.CalculateReward(out bool done);
            Require(Mathf.Abs(reward + 0.001f) < 0.00001f && done == (step == actions - 1), "Unchanged time cost and deadline");
        }
        var exit = (Transform)new SerializedObject(environment).FindProperty("exit").objectReferenceValue;
        for (int direction = 0; direction < 8; direction++)
        {
            environment.ResetEpisode(session.evaluation_seed);
            float angle = direction * Mathf.PI / 4f;
            PlaceActors(environment, exit, new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 3f);
            bool done = false;
            for (int step = 0; step < 40 && !done; step++)
            {
                float[] sensed = QaLocalMovementPolicyMapping.Observe(environment.Player.Observe(), environment.Sensors.Rays);
                Require(sensed[130] == 1f, "Visible exit ray channels preserved");
                float[] policyObservation = environment.CollectObservation();
                for (int feature = 130; feature < 134; feature++) Require(policyObservation[feature] == 0f, "Exit summary not substituted for waypoint");
                environment.ApplyAction(new[] { sensed[131], sensed[132] });
                while (environment.IsActionRunning()) yield return null;
                environment.CalculateReward(out done);
            }
            Require(done && environment.Player.Metrics.Completed && environment.Navigation.WaypointVersion == 0 &&
                environment.WaypointCompletionBonus == 0f, "Actual exit trigger still completes without navigator");
        }
        File.WriteAllText(Path.Combine(directory, "verification.json"),
            "{\"navigator_disabled\":true,\"seeded_resets\":" + session.evaluation_episodes +
            ",\"waypoint_inputs_zero\":true,\"assignments_zero\":true,\"exit_rays_preserved\":true,\"reference_compass_clears\":8," +
            "\"idle_timeout_actions\":" + actions + "}");
    }

    private static IEnumerator VerifyWaypoints(QaSingleRoomRlEnvironment environment, Session session)
    {
        var serialized = new SerializedObject(environment);
        var floor = (BoxCollider)serialized.FindProperty("roomFloor").objectReferenceValue;
        var exit = (Transform)serialized.FindProperty("exit").objectReferenceValue;
        Require(floor.bounds.size.x == session.room_width && floor.bounds.size.z == session.room_depth, "Configured open-room dimensions");
        Require(environment.Sensors.RayRange == 12f, "12-unit vision");
        Require(environment.Navigation.MaxWaypointDistance == session.max_waypoint_distance &&
            session.max_waypoint_distance > QaNavigationCoordinator.AcceptanceRadius &&
            session.max_waypoint_distance <= environment.Sensors.RayRange, "Configured local waypoint limit");
        Require(serialized.FindProperty("completionReward").floatValue == session.completion_reward, "Configured exit completion reward");
        Require(serialized.FindProperty("minExitDistance").floatValue == session.min_exit_distance &&
            serialized.FindProperty("maxExitDistance").floatValue == session.max_exit_distance, "Configured placement contract");
        foreach (string field in new[] { "viewDiscoveryRewardPerCell", "maxViewDiscoveryRewardPerAction", "maxViewDiscoveryRewardPerAttempt" })
            Require(serialized.FindProperty(field).floatValue == 0f, "Exploration reward disabled");

        // seeded reset -> inspect actual visibility and assigned signal -> check all planned placements
        int resetSeeds = session.training_episodes + session.evaluation_episodes;
        for (int index = 0; index < resetSeeds; index++)
        {
            int seed = session.evaluation_seed + index - session.training_episodes;
            if (index < session.training_episodes) seed = session.training_seed + index;
            environment.ResetEpisode(seed);
            float distance = QaNavigationCoordinator.Distance(environment.SpawnPosition, environment.ExitPosition);
            Require(!environment.InitialExitVisible && distance >= session.min_exit_distance - 0.0001f &&
                distance <= session.max_exit_distance + 0.0001f, "Initially unseen seeded exit " + seed);
            Require(environment.Navigation.HasWaypoint && !environment.Navigation.GoalIsExit, "Initial exploration waypoint " + seed);
            Require(environment.WaypointCompletionBonus == 0f && environment.Navigation.Replans == 0 &&
                environment.Navigation.ExcludedCount == 0, "Complete attempt reset");
            float[] observation = environment.CollectObservation();
            Require(observation[130] == 1f && observation[133] > 0f &&
                observation[133] <= session.max_waypoint_distance / environment.Sensors.RayRange + 0.00001f, "Local assigned waypoint signal");
        }

        VerifyMapRoutes();
        VerifyWaypointRewards();

        // identical spawn -> eight visible assigned exits -> follow directions -> actual triggers complete and pay configured reward
        for (int direction = 0; direction < 8; direction++)
        {
            environment.ResetEpisode(session.training_seed);
            float angle = direction * Mathf.PI / 4f;
            PlaceActors(environment, exit, new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 3f);
            environment.ResetNavigation();
            float[] first = environment.CollectObservation();
            Require(first[130] == 1f && Vector2.Dot(new Vector2(first[131], first[132]),
                new Vector2(Mathf.Sin(angle), Mathf.Cos(angle))) > 0.98f, "Different targets from identical spawn " + direction);
            bool done = false;
            for (int step = 0; step < 40 && !done; step++)
            {
                float[] observation = environment.CollectObservation();
                environment.ApplyAction(new[] { observation[131], observation[132] });
                while (environment.IsActionRunning()) yield return null;
                float reward = environment.CalculateReward(out done);
                if (environment.Player.Metrics.Completed)
                    Require(reward >= session.completion_reward - 0.0011f, "Actual exit trigger pays configured completion reward");
            }
            Require(done && environment.Player.Metrics.Completed, "Compass completion " + direction);
        }

        // stationary assigned exit -> 20 failed actions replan -> repeated failure resumes exploration -> reset clears recovery
        environment.ResetEpisode(session.training_seed);
        PlaceActors(environment, exit, new Vector3(3f, 0f, 0f));
        environment.ResetNavigation();
        var actor = environment.Player.GetComponent<CharacterController>();
        for (int step = 0; step < 40; step++) environment.Navigation.Advance(environment.Sensors, actor, true);
        Require(environment.Navigation.Replans == 1 && environment.Navigation.AbandonedGoals == 1 &&
            !environment.Navigation.GoalIsExit, "Repeated exit failure resumes exploration");
        environment.ResetEpisode(session.training_seed);
        Vector3 fixedWaypoint = environment.Navigation.Waypoint;
        int fixedVersion = environment.Navigation.WaypointVersion;
        environment.Navigation.Advance(environment.Sensors, actor, false);
        Require(environment.Navigation.Waypoint == fixedWaypoint && environment.Navigation.WaypointVersion == fixedVersion,
            "Waypoint stays fixed between actions");

        // fixed local barrier -> query path and actor clearance -> remove fixture before open-room episodes
        PlaceActors(environment, exit, new Vector3(3f, 0f, 0f));
        var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrier.name = "Navigation verification barrier";
        barrier.transform.position = new Vector3(1.5f, 1f, 0f);
        barrier.transform.localScale = new Vector3(0.25f, 2f, 2f);
        barrier.AddComponent<ModelSensorTarget>().HitId = QaObjectType.Wall;
        Physics.SyncTransforms();
        Require(!environment.Sensors.CanUseWaypoint(new Vector3(3f, actor.transform.position.y, 0f), actor), "Barrier blocks local waypoint");
        Require(!environment.Sensors.CanUseWaypoint(new Vector3(1.2f, actor.transform.position.y, 1.1f), actor), "Actor clearance blocks grazing path");
        Object.DestroyImmediate(barrier);
        Physics.SyncTransforms();

        // unseen exit -> follow observation-only reference actions -> handoffs and discovery -> actual completion
        int successes = 0, handoffs = 0, discoveries = 0;
        int minHandoffsBeforeDiscovery = int.MaxValue;
        float bonuses = 0f;
        for (int episode = 0; episode < 10; episode++)
        {
            environment.ResetEpisode(session.evaluation_seed + episode);
            bool done = false, discovered = false;
            int handoffsBeforeDiscovery = 0;
            int version = environment.Navigation.WaypointVersion;
            for (int step = 0; step < 300 && !done; step++)
            {
                float[] observation = environment.CollectObservation();
                environment.ApplyAction(new[] { observation[131], observation[132] });
                while (environment.IsActionRunning()) yield return null;
                environment.CalculateReward(out done);
                if (environment.Navigation.WaypointVersion != version)
                {
                    handoffs++; version = environment.Navigation.WaypointVersion;
                    if (!discovered && !environment.Sensors.ExitVisible) handoffsBeforeDiscovery++;
                }
                discovered |= environment.Sensors.ExitVisible;
                Require(environment.WaypointCompletionBonus <= 5f && environment.ExplorationRewardEarned == 0f, "Reward caps in reference episode");
                if (!environment.Player.Metrics.Completed && environment.WaypointCompletionBonus > 0f)
                    Require(!done || step == 299, "Waypoint arrival does not complete room");
            }
            if (environment.Player.Metrics.Completed) successes++;
            if (discovered) discoveries++;
            bonuses += environment.WaypointCompletionBonus;
            minHandoffsBeforeDiscovery = Mathf.Min(minHandoffsBeforeDiscovery, handoffsBeforeDiscovery);
        }
        Require(successes == 10 && discoveries == 10 && handoffs > 10 && bonuses > 0f, "Reference navigation completes unseen-exit task: " + successes);
        if (session.min_exit_distance > 24f)
        {
            // Allow two units for the exit fixture's footprint when bounding discovery distance.
            int requiredHandoffs = Mathf.FloorToInt((session.min_exit_distance - environment.Sensors.RayRange - 2f) /
                session.max_waypoint_distance);
            Require(minHandoffsBeforeDiscovery >= requiredHandoffs, "Distant exits require local handoffs before discovery");
        }
        File.WriteAllText(Path.Combine(directory, "verification.json"), JsonUtility.ToJson(new WaypointVerification
        {
            reference_successes = successes, reference_episodes = 10, reference_handoffs = handoffs,
            reference_exit_discoveries = discoveries, reference_waypoint_bonus = bonuses,
            reset_seeds = resetSeeds, room_width = session.room_width, room_depth = session.room_depth,
            min_exit_distance = session.min_exit_distance, max_exit_distance = session.max_exit_distance,
            minimum_handoffs_before_exit_discovery = minHandoffsBeforeDiscovery,
            completion_reward = session.completion_reward, max_waypoint_distance = session.max_waypoint_distance
        }, true));
        environment.ResetEpisode(session.training_seed);
    }

    // observed fixture with barrier -> A* detours -> unknown gap and diagonal corner stay blocked
    private static void VerifyMapRoutes()
    {
        var grid = new ModelSensorGrid(Vector2Int.zero);
        for (int z = -12; z <= 12; z++)
        for (int x = -12; x <= 12; x++)
        {
            Vector3 point = QaObservedRoutePlanner.Point(new Vector2Int(x, z), 0f);
            grid.Trace(point, point, QaObjectType.None);
        }
        for (int z = -7; z <= 7; z++)
        {
            Vector3 point = QaObservedRoutePlanner.Point(new Vector2Int(0, z), 0f);
            grid.Trace(point, point, QaObjectType.Wall);
        }
        var planner = new QaObservedRoutePlanner(grid, 0.38f);
        Vector3 start = QaObservedRoutePlanner.Point(new Vector2Int(-8, 0), 0f);
        var route = planner.Route(start, new Vector2Int(8, 0));
        Require(route.Count > 0 && !planner.IsFree(new Vector2Int(1, 0)), "Inflated barrier route exists");
        bool detour = false;
        foreach (Vector3 point in route)
        {
            Require(planner.IsFree(ModelSensorGrid.WorldToCell(point)), "Route uses observed free cells");
            detour |= Mathf.Abs(point.z) > 2f;
        }
        Require(detour, "A* detours around fixed barrier");
        Require(!planner.SegmentKnown(start, QaObservedRoutePlanner.Point(new Vector2Int(8, 0), 0f)), "No shortcut through barrier");
        var gap = new ModelSensorGrid(Vector2Int.zero);
        gap.Trace(new Vector3(0.125f, 0f, 0.125f), new Vector3(0.125f, 0f, 0.125f), QaObjectType.None);
        gap.Trace(new Vector3(0.375f, 0f, 0.375f), new Vector3(0.375f, 0f, 0.375f), QaObjectType.None);
        var blocked = new QaObservedRoutePlanner(gap, 0f);
        Require(blocked.Route(new Vector3(0.125f, 0f, 0.125f), new Vector2Int(1, 1)).Count == 0, "No diagonal corner cutting across unknown gaps");
        Require(!blocked.SegmentKnown(new Vector3(0.125f, 0f, 0.125f), new Vector3(0.375f, 0f, 0.375f)), "Unknown shortcut stays unknown");
        var selector = new QaFrontierGoalSelector();
        Require(!selector.TrySelect(blocked, new Vector3(0.125f, 0f, 0.125f), new Vector3[0], true, out _), "No reachable frontier at least one unit away");

        // observed line -> nearest frontier -> exclude it -> prefer only a reachable discovered exit
        var line = new ModelSensorGrid(Vector2Int.zero);
        Vector3 origin = new Vector3(0.125f, 0f, 0.125f);
        line.Trace(origin, new Vector3(3.125f, 0f, 0.125f), QaObjectType.None);
        var linePlanner = new QaObservedRoutePlanner(line, 0f);
        Require(selector.TrySelect(linePlanner, origin, new Vector3[0], true, out QaMapGoal frontier) &&
            frontier.Cell == new Vector2Int(4, 0) && !frontier.IsExit, "Nearest reachable frontier at least one unit away");
        Vector3 excluded = QaObservedRoutePlanner.Point(frontier.Cell, 0f);
        Require(selector.TrySelect(linePlanner, origin, new[] { excluded }, true, out QaMapGoal next) &&
            QaNavigationCoordinator.Distance(excluded, QaObservedRoutePlanner.Point(next.Cell, 0f)) > 0.5f,
            "Completed or abandoned frontier exclusion radius");
        Vector3 remote = new Vector3(9.125f, 0f, 0.125f);
        line.Trace(remote, remote, QaObjectType.LevelEnd);
        Require(selector.TrySelect(new QaObservedRoutePlanner(line, 0f), origin, new Vector3[0], true, out QaMapGoal unreachable) &&
            !unreachable.IsExit, "Unreachable discovered exit does not suppress exploration");
        Vector3 sensed = new Vector3(2.125f, 0f, 0.125f);
        line.Trace(sensed, sensed, QaObjectType.LevelEnd);
        Require(selector.TrySelect(new QaObservedRoutePlanner(line, 0f), origin, new Vector3[0], true, out QaMapGoal reachable) &&
            reachable.IsExit && reachable.Cell == new Vector2Int(8, 0), "Reachable exit supersedes frontier");
    }

    // assigned targets -> approach and revisit -> check one bonus, location exclusion, switch baseline and episode cap
    private static void VerifyWaypointRewards()
    {
        var rewards = new QaWaypointRewardTracker();
        rewards.Assign(true, 1, Vector3.right * 2f, Vector3.zero);
        Require(Mathf.Abs(rewards.Measure(Vector3.right * 2f) - 1.2f) < 0.0001f, "Approach plus single arrival");
        Require(rewards.Measure(Vector3.right * 2f) == 0f, "Arrival exactly once");
        rewards.Assign(true, 2, Vector3.right * 2.2f, Vector3.zero);
        rewards.Measure(Vector3.right * 2.2f);
        Require(rewards.CompletionBonus == 1f, "Nearby rewarded location excluded");
        rewards.Assign(true, 3, Vector3.right * 4f, Vector3.zero);
        Require(rewards.Measure(Vector3.zero) == 0f, "Switch cannot manufacture progress");
        Require(rewards.Measure(Vector3.left) == 0f, "No extra retreat penalty");
        rewards.Assign(true, 4, Vector3.right * 8f, Vector3.right * 8f);
        Require(rewards.Measure(Vector3.right * 8f) == 0f && rewards.CompletionBonus == 1f, "No bonus when assigned inside acceptance radius");
        for (int i = 0; i < 7; i++)
        {
            rewards.Assign(true, 5 + i, Vector3.right * (10f + i), Vector3.zero);
            rewards.Measure(Vector3.right * (10f + i));
        }
        Require(rewards.PositiveRewardTotal == 5f && rewards.CompletionBonus <= 5f,
            "Combined approach and arrival episode cap");
        rewards.Assign(false, 0, Vector3.zero, Vector3.zero);
        rewards.Assign(true, 20, Vector3.right * 30f, Vector3.zero);
        Require(rewards.Measure(Vector3.right * 30f) == 0f, "Target changes cannot refill exhausted budget");
        rewards.Reset();
        Require(rewards.CompletionBonus == 0f && rewards.PositiveRewardTotal == 0f &&
            rewards.Measure(Vector3.zero) == 0f, "Reward reset");
    }

    [Serializable] private class WaypointVerification
    {
        public string policy_version = QaWaypointPolicyMapping.Version;
        public int reset_seeds = 600, compass_completions = 8;
        public bool map_barrier_checks = true, unknown_gap_checks = true, recovery_checks = true, reward_checks = true;
        public int reference_episodes, reference_successes, reference_handoffs, reference_exit_discoveries;
        public int minimum_handoffs_before_exit_discovery;
        public float room_width, room_depth, min_exit_distance, max_exit_distance;
        public float reference_waypoint_bonus;
        public float completion_reward;
        public float max_waypoint_distance;
    }
}
