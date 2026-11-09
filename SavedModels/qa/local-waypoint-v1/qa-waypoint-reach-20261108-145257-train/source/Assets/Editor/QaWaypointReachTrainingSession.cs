using System;
using System.Collections;
using System.IO;
using Kcccr.UnityReinforcementLearning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class QaWaypointReachTrainingSession
{
    public const string Scene = "Assets/Scenes/QaWaypointReachTraining.unity";
    private const string Key = "qa-waypoint-reach-training";
    private static string directory;

    static QaWaypointReachTrainingSession()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            directory = SessionState.GetString(Key, "");
            if (directory.Length == 0 || state != PlayModeStateChange.EnteredPlayMode) return;
            Application.logMessageReceived += CaptureError;
            var environment = Object.FindFirstObjectByType<QaWaypointReachRlEnvironment>();
            environment.StartCoroutine(Observe(environment));
        };
    }

    public static void Prepare()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/QaWaypoint40Training.unity");
        var old = Object.FindFirstObjectByType<QaSingleRoomRlEnvironment>();
        var serialized = new SerializedObject(old);
        var floor = serialized.FindProperty("roomFloor").objectReferenceValue;
        var player = serialized.FindProperty("player").objectReferenceValue;
        var exit = (Transform)serialized.FindProperty("exit").objectReferenceValue;
        var root = old.gameObject;
        Object.DestroyImmediate(exit.gameObject);
        Object.DestroyImmediate(old);
        root.name = "Waypoint skill drill";
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Assigned waypoint";
        marker.transform.localScale = Vector3.one * 0.5f;
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        var environment = root.AddComponent<QaWaypointReachRlEnvironment>();
        serialized = new SerializedObject(environment);
        serialized.FindProperty("roomFloor").objectReferenceValue = floor;
        serialized.FindProperty("player").objectReferenceValue = player;
        serialized.FindProperty("waypoint").objectReferenceValue = marker.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        serialized = new SerializedObject(root.GetComponent<RlTrainingHost>());
        serialized.FindProperty("environment").objectReferenceValue = environment;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(root.scene, Scene);
        EditorApplication.Exit(0);
    }

    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        directory = args[Array.IndexOf(args, "-trainingRunDirectory") + 1];
        SessionState.SetString(Key, directory);
        EditorSceneManager.OpenScene(Scene);
        EditorApplication.EnterPlaymode();
    }

    private static IEnumerator Observe(QaWaypointReachRlEnvironment environment)
    {
        EditorApplication.LockReloadAssemblies();
        Application.runInBackground = true;
        Time.timeScale = 100f;
        QualitySettings.vSyncCount = 0;
        yield return null;
        var session = JsonUtility.FromJson<Session>(File.ReadAllText(Path.Combine(directory, "session.json")));
        // Apply only the selected stage's placement range; the reusable scene retains nearby defaults.
        var settings = new SerializedObject(environment);
        settings.FindProperty("minWaypointDistance").floatValue = session.min_distance;
        settings.FindProperty("maxWaypointDistance").floatValue = session.max_distance;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var host = environment.GetComponent<RlTrainingHost>();
        Require(host.IsListening && host.Port == 7783, "Waypoint drill bridge readiness");
        if (session.verify) yield return Verify(environment, session);
        if (session.mode == "verify")
        {
            Finish(host);
            yield break;
        }
        File.WriteAllText(Path.Combine(directory, "policy-contract.json"), JsonUtility.ToJson(new Contract
        {
            min_distance = settings.FindProperty("minWaypointDistance").floatValue,
            max_distance = settings.FindProperty("maxWaypointDistance").floatValue,
            episode_seconds = settings.FindProperty("maxEpisodeSeconds").floatValue,
            waypoint_budget = settings.FindProperty("waypointRewardBudget").floatValue,
            local_objective_floor = settings.FindProperty("minimumLocalObjectiveReward").floatValue
        }, true));
        var previous = environment.Player.Metrics;
        int captured = 0;
        File.WriteAllText(Path.Combine(directory, "unity-ready.txt"), "Waypoint drill ready\n");
        while (!File.Exists(Path.Combine(directory, "session-finished.txt")))
        {
            var metrics = environment.Player.Metrics;
            if (!environment.Sensors.IsTracking && metrics != null && !ReferenceEquals(metrics, previous))
            {
                Require(!metrics.Completed, "Waypoint arrival cannot mark game completion");
                var record = new Episode
                {
                    seed = environment.Seed, reached = environment.Reached, steps = metrics.StepCount,
                    elapsed_seconds = metrics.ElapsedSeconds, initial_distance = environment.InitialDistance,
                    final_distance = environment.Distance, positive_waypoint_reward = environment.PositiveRewardTotal,
                    arrival_bonus = environment.ArrivalBonus, target_x = environment.Target.x,
                    target_z = environment.Target.z, player_died = metrics.PlayerDied, game_completed = metrics.Completed
                };
                string path = Path.Combine(directory, "episode-" + captured + ".json");
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(record));
                File.Move(path + ".tmp", path);
                File.AppendAllText(Path.Combine(directory, "episodes.jsonl"), JsonUtility.ToJson(record) + "\n");
                previous = metrics;
                captured++;
            }
            yield return null;
        }
        Require(captured == session.episodes, "Exact requested episode count");
        Finish(host);
    }

    // seeded nearby targets -> reference following and idle timeout -> check rewards and no game completion
    private static IEnumerator Verify(QaWaypointReachRlEnvironment environment, Session session)
    {
        Require(Object.FindObjectsByType<QaExitZone>(FindObjectsSortMode.None).Length == 0, "Drill has no exit");
        bool[] quadrants = new bool[4];
        for (int i = 0; i < 100; i++)
        {
            environment.ResetEpisode(300000 + i);
            float[] observation = environment.CollectObservation();
            Require(environment.InitialDistance >= session.min_distance - 0.00001f &&
                environment.InitialDistance <= session.max_distance + 0.00001f && observation[130] == 1f &&
                Mathf.Abs(observation[133] - Mathf.Clamp01(environment.InitialDistance / environment.Sensors.RayRange)) < 0.00001f,
                "Configured assigned-waypoint contract");
            quadrants[(environment.Target.x >= 0 ? 1 : 0) + (environment.Target.z >= 0 ? 2 : 0)] = true;
            for (int ray = 3; ray < 130; ray += 2) Require(observation[ray] == 0f, "No exit signal");
        }
        foreach (bool covered in quadrants) Require(covered, "Seeded directions cover all quadrants");
        for (int i = 0; i < 8; i++)
        {
            environment.ResetEpisode(300000 + i);
            bool done = false;
            while (!done)
            {
                float[] observation = environment.CollectObservation();
                environment.ApplyAction(new[] { observation[131], observation[132] });
                while (environment.IsActionRunning()) yield return null;
                environment.CalculateReward(out done);
            }
            Require(environment.Reached && !environment.Player.Metrics.Completed &&
                environment.PositiveRewardTotal <= 5f && environment.ArrivalBonus == 1f, "Reference skill arrival, no exit payout");
        }
        environment.ResetEpisode(300000);
        for (int i = 0; i < 600; i++)
        {
            environment.ApplyAction(new float[2]);
            while (environment.IsActionRunning()) yield return null;
            float reward = environment.CalculateReward(out bool done);
            Require(Mathf.Abs(reward + 0.001f) < 0.00001f && done == (i == 599), "60-second idle timeout without progress reward");
        }
        var tracker = new QaWaypointRewardTracker();
        for (int i = 0; i < 100; i++)
        {
            tracker.Assign(true, i, Vector3.right * (i + 2), Vector3.zero);
            tracker.Measure(Vector3.right * (i + 2));
        }
        Require(tracker.PositiveRewardTotal == 5f && tracker.PositiveRewardTotal < 50f, "Aggregate waypoint budget stays subordinate");
        tracker.Reset();
        Require(tracker.PositiveRewardTotal == 0f, "Reward budget resets only with attempt");
        File.WriteAllText(Path.Combine(directory, "verification.json"),
            "{\"seeded_resets\":100,\"reference_arrivals\":8,\"idle_timeout_actions\":600,\"no_exit\":true," +
            "\"waypoint_budget\":5,\"local_objective_floor\":50,\"combined_reward_cap_checked\":true," +
            "\"min_distance\":" + session.min_distance.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"max_distance\":" + session.max_distance.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
    }

    private static void Finish(RlTrainingHost host)
    {
        host.StopTraining();
        SessionState.SetString(Key, "");
        EditorApplication.UnlockReloadAssemblies();
        File.WriteAllText(Path.Combine(directory, "unity-finished.txt"), "Complete\n");
        EditorApplication.Exit(0);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CaptureError(string message, string stack, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception) && !stack.Contains("UnityEditor.Search.SearchDatabase"))
        {
            File.AppendAllText(Path.Combine(directory, "unity-failure.txt"), message + "\n" + stack + "\n");
            SessionState.SetString(Key, "");
            EditorApplication.Exit(1);
        }
    }

    [Serializable] private class Session
    {
        public string mode;
        public bool verify;
        public int episodes;
        public float min_distance = 3f, max_distance = 6f;
    }
    [Serializable] private class Contract
    {
        public string observation_contract = "local-waypoint-v1";
        public float min_distance, max_distance, episode_seconds, waypoint_budget, local_objective_floor;
    }
    [Serializable] private class Episode
    {
        public int seed, steps;
        public bool reached, player_died, game_completed;
        public float elapsed_seconds, initial_distance, final_distance, positive_waypoint_reward, arrival_bonus, target_x, target_z;
    }
}
