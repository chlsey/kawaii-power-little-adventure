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
public static class QaLocalMovementSession
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
        Time.timeScale = 20f;
        QualitySettings.vSyncCount = 0;
        Application.runInBackground = true;
        yield return null;
        Session session = ReadSession();
        var host = environment.GetComponent<RlTrainingHost>();
        Require(host.Port == session.port && host.IsListening, "Training host readiness");
        Require(environment.ObservationSize == 134 && environment.ActionSize == 2, "Local movement contract");
        if (session.verify)
            yield return VerifyVision(environment, session);
        File.WriteAllText(Path.Combine(directory, "unity-ready.txt"), "134 observations; 2 movement actions\n");

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
                    stuck_penalty_steps = environment.StuckPenaltySteps
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
        for (int step = 0; step < 300; step++)
        {
            environment.ApplyAction(new float[2]);
            while (environment.IsActionRunning())
                yield return null;
            float reward = environment.CalculateReward(out timedOut);
            Require(Mathf.Abs(reward + 0.001f) < 0.00001f && timedOut == (step == 299), "Time cost and 30-second timeout");
        }
        environment.ResetEpisode(0);
        File.WriteAllText(Path.Combine(directory, "verification.json"),
            "{\"room_width\":" + session.room_width.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"room_depth\":" + session.room_depth.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"min_exit_distance\":" + session.min_exit_distance.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"max_exit_distance\":" + session.max_exit_distance.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"initial_exit_visible\":" + session.initial_exit_visible.ToString().ToLowerInvariant() +
            ",\"observation_size\":134,\"action_size\":2,\"reset_seeds\":100,\"all_resets_match_visibility\":true,\"sensor_guided_compass_completions\":8,\"hidden_exit_signal_zero\":true,\"timeout_actions\":300,\"vision_range\":12,\"exploration_reward\":0}");
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
        public string scene;
        public int port, episodes;
        public bool verify, initial_exit_visible;
        public float room_width, room_depth, min_exit_distance, max_exit_distance;
    }
    [Serializable] private class Episode
    {
        public int episode, seed, steps, stuck_events, stuck_penalty_steps;
        public float elapsed_seconds, initial_exit_distance, final_exit_distance, spawn_x, spawn_z, exit_x, exit_z, exploration_reward, stuck_penalty;
        public bool completed, player_died, initial_exit_visible;
        public string end_reason;
    }
}
