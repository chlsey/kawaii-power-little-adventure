using System;
using System.Collections;
using System.IO;
using Kcccr.UnityReinforcementLearning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Runs seeded waypoint chains without exploration or exit discovery.</summary>
[InitializeOnLoad]
public static class QaWaypointChainSession
{
    private const string Key = "qa-waypoint-chain";
    private static string directory;

    static QaWaypointChainSession()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            directory = SessionState.GetString(Key, "");
            if (directory.Length == 0 || state != PlayModeStateChange.EnteredPlayMode) return;
            Application.logMessageReceived += CaptureError;
            var source = Object.FindFirstObjectByType<QaSingleRoomRlEnvironment>();
            source.StartCoroutine(Observe(source));
        };
    }

    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        directory = args[Array.IndexOf(args, "-trainingRunDirectory") + 1];
        SessionState.SetString(Key, directory);
        EditorSceneManager.OpenScene("Assets/Scenes/QaWaypoint40Training.unity");
        EditorApplication.EnterPlaymode();
    }

    private static IEnumerator Observe(QaSingleRoomRlEnvironment source)
    {
        EditorApplication.LockReloadAssemblies();
        Application.runInBackground = true;
        Time.timeScale = 100f;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        yield return null;
        var host = source.GetComponent<RlTrainingHost>();
        host.StopTraining();
        source.ResetEpisode(270000);
        var diagnostic = source.gameObject.AddComponent<QaWaypointChainEnvironment>();
        diagnostic.Configure(source, directory);
        var serialized = new SerializedObject(host);
        serialized.FindProperty("environment").objectReferenceValue = diagnostic;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (!host.StartTraining()) throw new InvalidOperationException("Diagnostic bridge startup");
        File.WriteAllText(Path.Combine(directory, "unity-ready.txt"), host.Port.ToString());
        while (!File.Exists(Path.Combine(directory, "session-finished.txt"))) yield return null;
        host.StopTraining();
        SessionState.SetString(Key, "");
        EditorApplication.UnlockReloadAssemblies();
        File.WriteAllText(Path.Combine(directory, "unity-finished.txt"), "Evaluation complete\n");
        EditorApplication.Exit(0);
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
}

/// <summary>Predefined targets; arrival changes the instruction without resetting the player.</summary>
public sealed class QaWaypointChainEnvironment : RlEnvironmentBehaviour
{
    private QaSingleRoomRlEnvironment source;
    private CharacterController actor;
    private BoxCollider floor;
    private string directory;
    private Vector3[] targets;
    private Settings settings = new Settings();
    private QaWaypointRewardTracker rewards;
    private int seed, leg, legStartStep;
    private float actionEnd, initialDistance, minimumDistance, pathLength, positiveReward;
    private Vector3 previousPosition;
    private bool recorded;
    private float[] observation;

    public override int ObservationSize => 134;
    public override int ActionSize => 2;

    public void Configure(QaSingleRoomRlEnvironment environment, string outputDirectory)
    {
        source = environment;
        directory = outputDirectory;
        string path = Path.Combine(directory, "session.json");
        if (File.Exists(path)) settings = JsonUtility.FromJson<Settings>(File.ReadAllText(path));
        actor = source.Player.GetComponent<CharacterController>();
        var serialized = new SerializedObject(source);
        floor = (BoxCollider)serialized.FindProperty("roomFloor").objectReferenceValue;
        ((Transform)serialized.FindProperty("exit").objectReferenceValue).gameObject.SetActive(false);
        if (floor.bounds.size.x != 40f || floor.bounds.size.z != 40f || source.Sensors.RayRange != 12f)
            throw new InvalidOperationException("Expected empty 40 x 40 room and 12-unit vision");
    }

    public override void ResetEpisode(int episodeSeed)
    {
        source.Sensors.EndAttempt();
        seed = episodeSeed;
        leg = legStartStep = 0;
        recorded = false;
        pathLength = 0f;
        positiveReward = 0f;
        actor.enabled = false;
        source.Player.transform.SetPositionAndRotation(new Vector3(0f, floor.bounds.max.y + 0.01f, 0f), Quaternion.identity);
        actor.enabled = true;
        source.Player.BeginTest();
        previousPosition = source.Player.transform.position;

        // Precompute the entire chain independently of policy actions. A half-unit arrival
        // tolerance leaves actual handoff distances in [1,10] for nominal [1.5,9.5] legs.
        var random = new System.Random(seed);
        int length = settings.min_waypoints;
        if (settings.min_waypoints != settings.max_waypoints)
            length = new System.Random(seed + 100000).Next(settings.min_waypoints, settings.max_waypoints + 1);
        // One arrival needs at least two actions at the configured 1-unit minimum.
        // This precomputed sequence cannot be exhausted within the 600-action deadline.
        if (settings.continuous) length = 601;
        targets = new Vector3[length];
        Vector3 origin = previousPosition;
        for (int i = 0; i < targets.Length; i++)
        {
            Vector3 point;
            do
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                float distance = (float)(1.5 + random.NextDouble() * 8);
                point = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            } while (Mathf.Abs(point.x) > 15f || Mathf.Abs(point.z) > 15f);
            targets[i] = origin = point;
        }
        Physics.SyncTransforms();
        source.Sensors.BeginAttempt();
        File.AppendAllText(Path.Combine(directory, "chains.jsonl"),
            JsonUtility.ToJson(new Chain { seed = seed, targets = targets }) + "\n");
        Assign();
    }

    private void Assign()
    {
        initialDistance = minimumDistance = QaNavigationCoordinator.Distance(source.Player.transform.position, targets[leg]);
        if (initialDistance < 0.9999f || initialDistance > 10.0001f)
            throw new InvalidOperationException("Assigned chain target outside 1-10 units");
        legStartStep = source.Player.Metrics.StepCount;
        if (settings.training_rewards)
        {
            // Each leg keeps the existing progress/arrival shaping. Dividing by chain
            // length gives every handoff credit without exhausting the episode budget early.
            rewards = new QaWaypointRewardTracker();
            rewards.Assign(true, leg + 1, targets[leg], source.Player.transform.position);
        }
    }

    public override float[] CollectObservation()
    {
        source.Sensors.Capture();
        observation = QaWaypointPolicyMapping.Observe(source.Player.Observe(), source.Sensors,
            true, targets[Mathf.Min(leg, targets.Length - 1)], source.Player.transform.position);
        if (source.Sensors.ExitVisible) throw new InvalidOperationException("Disabled exit leaked into perception");
        return observation;
    }

    public override void ApplyAction(float[] action)
    {
        Vector3 position = source.Player.transform.position;
        if (settings.trace_actions) File.AppendAllText(Path.Combine(directory, "actions.jsonl"), JsonUtility.ToJson(new ActionRecord
        {
            seed = seed, leg = leg, step = source.Player.Metrics.StepCount + 1,
            x = position.x, z = position.z, observation = observation, action = action
        }) + "\n");
        source.Player.ApplyAction(QaLocalMovementPolicyMapping.Decode(action));
        actionEnd = Time.fixedTime + QaPlayerController.StepDurationSeconds;
    }

    public override bool IsActionRunning() => Time.fixedTime < actionEnd;

    public override float CalculateReward(out bool isDone)
    {
        Vector3 position = source.Player.transform.position;
        float distance = QaNavigationCoordinator.Distance(position, targets[leg]);
        minimumDistance = Mathf.Min(minimumDistance, distance);
        pathLength += QaNavigationCoordinator.Distance(previousPosition, position);
        previousPosition = position;
        bool reached = distance <= QaNavigationCoordinator.AcceptanceRadius;
        bool timeout = source.Player.Metrics.StepCount >= 600;
        float reward = 0f;
        if (settings.training_rewards)
        {
            float positive = rewards.Measure(position);
            // At most 300 arrivals and 240 movement units fit in 60 seconds.
            // Scaling bounds all positive credit by 4.86, so later arrivals still pay.
            if (settings.continuous)
                positive *= 0.015f;
            else
                positive /= targets.Length;
            positiveReward += positive;
            if (positiveReward > 5.00001f) throw new InvalidOperationException("Chain waypoint reward budget exceeded");
            reward = positive - 0.001f;
        }
        if (reached || timeout)
        {
            File.AppendAllText(Path.Combine(directory, "legs.jsonl"), JsonUtility.ToJson(new Leg
            {
                seed = seed, leg = leg, reached = reached,
                steps = source.Player.Metrics.StepCount - legStartStep,
                initial_distance = initialDistance, final_distance = distance, minimum_distance = minimumDistance,
                target_x = targets[leg].x, target_z = targets[leg].z,
                final_x = position.x, final_z = position.z
            }) + "\n");
            if (reached) leg++;
            if (leg < targets.Length && !timeout) Assign();
        }
        isDone = (!settings.continuous && leg == targets.Length) || timeout;
        if (isDone && !recorded)
        {
            File.AppendAllText(Path.Combine(directory, "episodes.jsonl"), JsonUtility.ToJson(new Episode
            {
                seed = seed, reached = !settings.continuous && leg == targets.Length, reached_waypoints = leg,
                planned_waypoints = settings.continuous ? 0 : targets.Length,
                continuous = settings.continuous, positive_waypoint_reward = positiveReward,
                steps = source.Player.Metrics.StepCount, elapsed_seconds = source.Player.Metrics.ElapsedSeconds,
                path_length = pathLength, stuck_events = source.Player.Metrics.StuckEvents
            }) + "\n");
            recorded = true;
            source.Sensors.EndAttempt();
        }
        return reward;
    }

    [Serializable] private class Settings
    {
        public int min_waypoints = 10, max_waypoints = 10;
        public bool training_rewards;
        public bool trace_actions = true;
        public bool continuous;
    }
    [Serializable] private class Chain { public int seed; public Vector3[] targets; }
    [Serializable] private class ActionRecord
    {
        public int seed, leg, step;
        public float x, z;
        public float[] observation, action;
    }
    [Serializable] private class Leg
    {
        public int seed, leg, steps;
        public bool reached;
        public float initial_distance, final_distance, minimum_distance, target_x, target_z, final_x, final_z;
    }
    [Serializable] private class Episode
    {
        public int seed, steps, reached_waypoints, planned_waypoints, stuck_events;
        public bool reached, continuous;
        public float elapsed_seconds, path_length, positive_waypoint_reward;
    }
}

