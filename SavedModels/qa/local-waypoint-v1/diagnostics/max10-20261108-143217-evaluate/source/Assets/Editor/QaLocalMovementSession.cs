using System;
using System.Collections;
using System.IO;
using Kcccr.UnityReinforcementLearning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Editor entry point used by Training/run_local_movement.py for repeatable sessions.</summary>
[InitializeOnLoad]
public static partial class QaLocalMovementSession
{
    private const string SessionKey = "qa-local-movement-session";
    private static string directory;

    static QaLocalMovementSession()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            directory = SessionState.GetString(SessionKey, "");
            if (directory.Length == 0 || state != PlayModeStateChange.EnteredPlayMode)
                return;
            Application.logMessageReceived += CaptureError;
            var environment = Object.FindFirstObjectByType<QaSingleRoomRlEnvironment>();
            environment.StartCoroutine(Observe(environment));
        };
    }

    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        directory = args[Array.IndexOf(args, "-trainingRunDirectory") + 1];
        SessionState.SetString(SessionKey, directory);
        Session session = ReadSession();
        EditorSceneManager.OpenScene(session.scene);
        EditorApplication.EnterPlaymode();
    }

    private static Session ReadSession()
    {
        return JsonUtility.FromJson<Session>(File.ReadAllText(Path.Combine(directory, "session.json")));
    }

    private static IEnumerator Observe(QaSingleRoomRlEnvironment environment)
    {
        EditorApplication.LockReloadAssemblies();
        Session session = ReadSession();
        Time.timeScale = session.time_scale;
        QualitySettings.vSyncCount = 0;
        Application.runInBackground = true;
        yield return null;
        // Keep reward variants in run-owned configuration without changing historical scenes.
        var serialized = new SerializedObject(environment);
        serialized.FindProperty("completionReward").floatValue = session.completion_reward;
        serialized.FindProperty("maxEpisodeSeconds").floatValue = session.episode_seconds;
        serialized.FindProperty("disableWaypointNavigatorForEvaluation").boolValue = session.navigator_disabled;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        environment.Navigation.MaxWaypointDistance = session.max_waypoint_distance;
        var host = environment.GetComponent<RlTrainingHost>();
        Require(host.Port == session.port && host.IsListening, "Training host readiness");
        Require(environment.ObservationSize == 134 && environment.ActionSize == 2, "Local movement contract");
        Require(environment.PolicyVersion == session.policy_version, "Semantic policy version matches game configuration");
        if (session.verify)
        {
            if (session.navigator_disabled)
                yield return VerifyNavigatorDisabled(environment, session);
            else if (session.policy_version == QaWaypointPolicyMapping.Version)
                yield return VerifyWaypoints(environment, session);
            else
                yield return VerifyVision(environment, session);
        }
        if (session.mode == "verify")
        {
            host.StopTraining();
            SessionState.SetString(SessionKey, "");
            EditorApplication.UnlockReloadAssemblies();
            EditorApplication.Exit(0);
            yield break;
        }
        File.WriteAllText(Path.Combine(directory, "policy-contract.json"),
            "{\"observation_contract\":\"" + environment.PolicyVersion + "\",\"completion_reward\":" +
            serialized.FindProperty("completionReward").floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"episode_seconds\":" + serialized.FindProperty("maxEpisodeSeconds").floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"navigator_disabled\":" + environment.NavigatorDisabled.ToString().ToLowerInvariant() +
            ",\"max_waypoint_distance\":" + environment.Navigation.MaxWaypointDistance.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
        File.WriteAllText(Path.Combine(directory, "unity-ready.txt"), "134 observations; 2 movement actions\n");

        ExitDecisionTrace trace = null;
        if (session.trace_exit_decisions)
        {
            Require(session.mode == "evaluate", "Exit tracing is frozen evaluation only");
            trace = new ExitDecisionTrace(environment, directory);
        }

        QaEpisodeMetrics previous = null;
        int captured = 0;
        while (!File.Exists(Path.Combine(directory, "session-finished.txt")))
        {
            QaEpisodeMetrics metrics = environment.Player.Metrics;
            if (metrics != null && !environment.Sensors.IsTracking && !ReferenceEquals(previous, metrics))
            {
                Vector3 spawn = environment.SpawnPosition;
                Vector3 exit = environment.ExitPosition;
                var record = new Episode
                {
                    episode = captured + 1, seed = environment.ActiveSeed,
                    steps = metrics.StepCount, elapsed_seconds = metrics.ElapsedSeconds,
                    completed = metrics.Completed, player_died = metrics.PlayerDied,
                    stuck_events = metrics.StuckEvents,
                    end_reason = metrics.Completed ? "completed" : metrics.PlayerDied ? "player_died" : "max_duration",
                    initial_exit_visible = environment.InitialExitVisible,
                    initial_exit_distance = Vector3.Distance(spawn, exit),
                    final_exit_distance = Vector3.Distance(environment.Player.transform.position, exit),
                    spawn_x = spawn.x, spawn_z = spawn.z, exit_x = exit.x, exit_z = exit.z,
                    exploration_reward = environment.ExplorationRewardEarned,
                    stuck_penalty = environment.StuckPenaltyTotal,
                    stuck_penalty_steps = environment.StuckPenaltySteps,
                    waypoint_bonus = environment.WaypointCompletionBonus,
                    waypoint_assignments = environment.Navigation.WaypointVersion,
                    replans = environment.Navigation.Replans,
                    abandoned_goals = environment.Navigation.AbandonedGoals,
                    navigation_status = environment.Navigation.Status,
                    navigator_disabled = environment.NavigatorDisabled,
                    exit_discovered = HasDiscoveredExit(environment.Sensors.Grid)
                };
                string path = Path.Combine(directory, "episode-" + captured + ".json");
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(record));
                File.Move(path + ".tmp", path);
                previous = metrics;
                captured++;
            }
            yield return null;
        }
        Require(captured == session.episodes, "Requested complete episode count");
        trace?.Dispose();
        host.StopTraining();
        SessionState.SetString(SessionKey, "");
        EditorApplication.UnlockReloadAssemblies();
        File.WriteAllText(Path.Combine(directory, "unity-finished.txt"), "Session complete\n");
        EditorApplication.Exit(0);
    }

    private static IEnumerator VerifyVision(QaSingleRoomRlEnvironment environment, Session session)
    {
        var serialized = new SerializedObject(environment);
        var floor = (BoxCollider)serialized.FindProperty("roomFloor").objectReferenceValue;
        var exit = (Transform)serialized.FindProperty("exit").objectReferenceValue;
        Require(Mathf.Abs(floor.bounds.size.x - session.room_width) < 0.001f &&
            Mathf.Abs(floor.bounds.size.z - session.room_depth) < 0.001f, "Configured room dimensions");
        Require(environment.Sensors.RayRange == 12f, "Unchanged 12-unit vision range");
        Require(serialized.FindProperty("minExitDistance").floatValue == session.min_exit_distance &&
            serialized.FindProperty("maxExitDistance").floatValue == session.max_exit_distance, "Configured exit distances");
        foreach (string field in new[] { "viewDiscoveryRewardPerCell", "maxViewDiscoveryRewardPerAction", "maxViewDiscoveryRewardPerAttempt" })
            Require(serialized.FindProperty(field).floatValue == 0f, "Exploration reward disabled");
        for (int seed = 0; seed < 100; seed++)
        {
            environment.ResetEpisode(seed);
            float[] observation = environment.CollectObservation();
            Require(observation.Length == 134 && environment.InitialExitVisible == session.initial_exit_visible,
                "Configured visibility on seeded reset");
            float distance = Vector3.Distance(environment.ExitPosition, environment.SpawnPosition);
            Require(distance >= session.min_exit_distance - 0.0001f && distance <= session.max_exit_distance + 0.0001f,
                "Seeded spawn distance");
            if (session.initial_exit_visible)
            {
                Require(observation[130] == 1f, "Visible target signal");
                Vector3 towardExit = (environment.ExitPosition - environment.SpawnPosition).normalized;
                Vector3 sensedDirection = new Vector3(observation[131], 0f, observation[132]);
                Require(Vector3.Dot(towardExit, sensedDirection) > 0.9f, "Vision and movement direction alignment");
            }
            else
            {
                Require(distance > environment.Sensors.RayRange, "Exit starts beyond vision range");
                for (int i = 130; i < 134; i++)
                    Require(observation[i] == 0f, "Unseen exit supplies no target signal");
            }
        }

        // Exercise every compass direction using sensed directions, never the goal position as an action.
        for (int direction = 0; direction < 8; direction++)
        {
            environment.ResetEpisode(0);
            float angle = direction * Mathf.PI / 4f;
            PlaceActors(environment, exit, new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 3f);
            bool done = false;
            for (int step = 0; step < 30 && !done; step++)
            {
                float[] observation = environment.CollectObservation();
                Require(observation[130] == 1f, "Target remains sensed before reaching exit");
                environment.ApplyAction(new[] { observation[131], observation[132] });
                while (environment.IsActionRunning())
                    yield return null;
                environment.CalculateReward(out done);
            }
            Require(done && environment.Player.Metrics.Completed, "Sensor-guided completion in compass direction " + direction);
        }

        environment.ResetEpisode(0);
        // Keep the sensor's previously discovered exit in memory, but hide it from current vision.
        PlaceActors(environment, exit, new Vector3(3f, 0f, 0f));
        Require(environment.CollectObservation()[130] == 1f, "Visible exit recorded before memory check");
        PlaceActors(environment, exit, new Vector3(20f, 0f, 0f), false);
        float[] hidden = environment.CollectObservation();
        for (int i = 130; i < 134; i++)
            Require(hidden[i] == 0f, "Hidden goal cannot leak from memory into the local observation");
        environment.ResetEpisode(0);
        bool timedOut = false;
        int timeoutActions = Mathf.RoundToInt(session.episode_seconds / QaPlayerController.StepDurationSeconds);
        for (int step = 0; step < timeoutActions; step++)
        {
            environment.ApplyAction(new float[2]);
            while (environment.IsActionRunning())
                yield return null;
            float reward = environment.CalculateReward(out timedOut);
            Require(Mathf.Abs(reward + 0.001f) < 0.00001f && timedOut == (step == timeoutActions - 1), "Time cost and configured timeout");
        }
        environment.ResetEpisode(0);
        File.WriteAllText(Path.Combine(directory, "verification.json"),
            "{\"room_width\":" + session.room_width.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"room_depth\":" + session.room_depth.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"min_exit_distance\":" + session.min_exit_distance.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"max_exit_distance\":" + session.max_exit_distance.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"initial_exit_visible\":" + session.initial_exit_visible.ToString().ToLowerInvariant() +
            ",\"observation_size\":134,\"action_size\":2,\"reset_seeds\":100,\"all_resets_match_visibility\":true,\"sensor_guided_compass_completions\":8,\"hidden_exit_signal_zero\":true,\"timeout_actions\":" + timeoutActions + ",\"vision_range\":12,\"exploration_reward\":0}");
    }

    private static void PlaceActors(QaSingleRoomRlEnvironment environment, Transform exit, Vector3 offset, bool resetMemory = true)
    {
        var character = environment.Player.GetComponent<CharacterController>();
        character.enabled = false;
        Vector3 center = new Vector3(0f, environment.SpawnPosition.y, 0f);
        environment.Player.transform.position = center;
        exit.position = center + offset;
        character.enabled = true;
        Physics.SyncTransforms();
        if (resetMemory)
            environment.Sensors.BeginAttempt();
        else
            environment.Sensors.Capture();
    }

    private static bool HasDiscoveredExit(ModelSensorGrid grid)
    {
        // Read sensor memory for diagnostics; do not consult the true scene destination.
        for (int z = 0; z < grid.Depth; z++)
        for (int x = 0; x < grid.Width; x++)
            if (grid.ReadCell(grid.MinCell + new Vector2Int(x, z)).Exit) return true;
        return false;
    }

    private static void Require(bool condition, string message)
    {
        if (condition)
            return;
        File.WriteAllText(Path.Combine(directory, "unity-failure.txt"), message);
        EditorApplication.Exit(1);
        throw new InvalidOperationException(message);
    }

    private static void CaptureError(string message, string stack, LogType type)
    {
        // Unity's editor search index can throw independently of gameplay in batch mode.
        if ((type == LogType.Error || type == LogType.Exception) && !stack.Contains("UnityEditor.Search.SearchDatabase"))
        {
            File.AppendAllText(Path.Combine(directory, "unity-failure.txt"), message + "\n" + stack + "\n");
            EditorApplication.Exit(1);
        }
    }

    [Serializable] private class Session
    {
        public string scene, mode, policy_version = "local-vision-v1";
        public int port, episodes;
        public int training_seed = 2000, training_episodes = 500, evaluation_seed = 200000, evaluation_episodes = 100;
        public bool verify, initial_exit_visible, navigator_disabled, trace_exit_decisions;
        public float completion_reward = 50f;
        public float max_waypoint_distance = 6f;
        public float episode_seconds = 30f, time_scale = 20f;
        public float room_width, room_depth, min_exit_distance, max_exit_distance;
    }
    [Serializable] private class Episode
    {
        public int episode, seed, steps, stuck_events, stuck_penalty_steps;
        public float elapsed_seconds, initial_exit_distance, final_exit_distance, spawn_x, spawn_z, exit_x, exit_z, exploration_reward, stuck_penalty;
        public bool completed, player_died, initial_exit_visible, exit_discovered, navigator_disabled;
        public string end_reason, navigation_status;
        public float waypoint_bonus;
        public int waypoint_assignments, replans, abandoned_goals;
    }

    // Diagnostics never feed true exit positions or trace fields into the policy/navigation.
    private sealed class ExitDecisionTrace : IDisposable
    {
        private readonly QaSingleRoomRlEnvironment environment;
        private readonly StreamWriter writer;
        private readonly BoxCollider exitCollider;
        private Decision record;
        private Vector3 before, waypoint;

        public ExitDecisionTrace(QaSingleRoomRlEnvironment environment, string directory)
        {
            this.environment = environment;
            var serialized = new SerializedObject(environment);
            exitCollider = ((Transform)serialized.FindProperty("exit").objectReferenceValue).GetComponent<BoxCollider>();
            Bounds bounds = exitCollider.bounds;
            File.WriteAllText(Path.Combine(directory, "exit-trace-contract.json"),
                JsonUtility.ToJson(new TraceContract { exit_size = bounds.size,
                    acceptance_radius = QaNavigationCoordinator.AcceptanceRadius }));
            writer = new StreamWriter(Path.Combine(directory, "exit-decisions.jsonl"));
            environment.ActionStarting += Before;
            environment.ActionMeasured += After;
        }

        private void Before(float[] action)
        {
            var nav = environment.Navigation;
            before = environment.Player.transform.position;
            waypoint = nav.Waypoint;
            bool final = nav.HasWaypoint && nav.GoalIsExit && nav.Route.Count > 0 &&
                QaNavigationCoordinator.Distance(waypoint, nav.Route[nav.Route.Count - 1]) < 0.001f;
            record = new Decision
            {
                seed = environment.ActiveSeed, step = environment.Player.Metrics.StepCount + 1,
                before = before, waypoint = waypoint, exit = environment.ExitPosition,
                action_x = action[0], action_z = action[1],
                active = environment.Player.EpisodeActive,
                has_waypoint = nav.HasWaypoint, goal_is_exit = nav.GoalIsExit,
                final_exit_waypoint = final, waypoint_version = nav.WaypointVersion,
                status = nav.Status, exit_visible_before = environment.Sensors.ExitVisible,
                waypoint_distance_before = QaNavigationCoordinator.Distance(before, waypoint),
                exit_distance_before = QaNavigationCoordinator.Distance(before, environment.ExitPosition),
                seconds_before = environment.Player.Metrics.ElapsedSeconds
            };
            // Retain exact policy inputs for comparing steering on identical replay states.
            record.observation = environment.CollectObservation();
        }

        private void After(bool done)
        {
            record.after = environment.Player.transform.position;
            record.done = done;
            record.completed = environment.Player.Metrics.Completed;
            record.exit_visible_after = environment.Sensors.ExitVisible;
            record.waypoint_distance_after = QaNavigationCoordinator.Distance(record.after, waypoint);
            record.exit_distance_after = QaNavigationCoordinator.Distance(record.after, environment.ExitPosition);
            record.arrived = record.active && record.has_waypoint &&
                record.waypoint_distance_before > QaNavigationCoordinator.AcceptanceRadius &&
                record.waypoint_distance_after <= QaNavigationCoordinator.AcceptanceRadius;
            writer.WriteLine(JsonUtility.ToJson(record));
            if (done) writer.Flush();
        }

        public void Dispose()
        {
            environment.ActionStarting -= Before;
            environment.ActionMeasured -= After;
            writer.Dispose();
        }
    }

    [Serializable] private class TraceContract
    {
        public Vector3 exit_size;
        public float acceptance_radius;
    }

    [Serializable] private class Decision
    {
        public int seed, step, waypoint_version;
        public Vector3 before, after, waypoint, exit;
        public float action_x, action_z, seconds_before;
        public float waypoint_distance_before, waypoint_distance_after, exit_distance_before, exit_distance_after;
        public bool active, has_waypoint, goal_is_exit, final_exit_waypoint, arrived;
        public bool exit_visible_before, exit_visible_after, done, completed;
        public string status;
        public float[] observation;
    }
}
