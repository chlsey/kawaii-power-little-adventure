using System;
using System.Collections;
using System.IO;
using Kcccr.UnityReinforcementLearning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Runs randomized fixed-waypoint attempts over the existing RL bridge, without training.</summary>
[InitializeOnLoad]
public static class QaWaypointReachBatchSession
{
    private const string Key = "qa-waypoint-reach-batch";
    private static string directory;

    static QaWaypointReachBatchSession()
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
        var diagnostic = source.gameObject.AddComponent<QaWaypointReachBatchEnvironment>();
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

/// <summary>Real player and sensors, a fixed assigned target, and a 600-action timeout.</summary>
public sealed class QaWaypointReachBatchEnvironment : RlEnvironmentBehaviour
{
    private QaSingleRoomRlEnvironment source;
    private CharacterController actor;
    private BoxCollider floor;
    private Transform marker;
    private string directory;
    private Vector3 target, previousPosition;
    private float actionEnd, minimumDistance, pathLength;
    private bool recorded;
    private int seed;
    private float initialDistance, angle;

    public override int ObservationSize => 134;
    public override int ActionSize => 2;

    public void Configure(QaSingleRoomRlEnvironment environment, string outputDirectory)
    {
        source = environment;
        directory = outputDirectory;
        actor = source.Player.GetComponent<CharacterController>();
        var serialized = new SerializedObject(source);
        floor = (BoxCollider)serialized.FindProperty("roomFloor").objectReferenceValue;
        var exit = (Transform)serialized.FindProperty("exit").objectReferenceValue;
        exit.gameObject.SetActive(false);
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Assigned waypoint (not a sensed exit)";
        Object.DestroyImmediate(sphere.GetComponent<Collider>());
        marker = sphere.transform;
        marker.localScale = Vector3.one * 0.5f;
        if (floor.bounds.size.x != 40f || floor.bounds.size.z != 40f || source.Sensors.RayRange != 12f)
            throw new InvalidOperationException("Expected empty 40 x 40 room and 12-unit vision");
    }

    public override void ResetEpisode(int episodeSeed)
    {
        source.Sensors.EndAttempt();
        seed = episodeSeed;
        var random = new System.Random(seed);
        angle = (float)(random.NextDouble() * Math.PI * 2);
        initialDistance = (float)(13 + random.NextDouble() * 5);
        actor.enabled = false;
        source.Player.transform.SetPositionAndRotation(new Vector3(0f, floor.bounds.max.y + 0.01f, 0f), Quaternion.identity);
        actor.enabled = true;
        source.Player.BeginTest();
        target = source.Player.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * initialDistance;
        marker.position = target + Vector3.up * 0.25f;
        Physics.SyncTransforms();
        source.Sensors.BeginAttempt();
        minimumDistance = initialDistance;
        previousPosition = source.Player.transform.position;
        pathLength = 0f;
        recorded = false;
    }

    public override float[] CollectObservation()
    {
        source.Sensors.Capture();
        float[] values = QaLocalMovementPolicyMapping.Observe(source.Player.Observe(), source.Sensors.Rays);
        Vector3 delta = target - source.Player.transform.position;
        delta.y = 0f;
        values[130] = 1f;
        values[131] = delta.normalized.x;
        values[132] = delta.normalized.z;
        // Preserve the contract's distance saturation even when the assigned goal starts beyond vision.
        values[133] = Mathf.Clamp01(delta.magnitude / source.Sensors.RayRange);
        if (source.Sensors.ExitVisible) throw new InvalidOperationException("Disabled exit leaked into perception");
        return values;
    }

    public override void ApplyAction(float[] action)
    {
        source.Player.ApplyAction(QaLocalMovementPolicyMapping.Decode(action));
        actionEnd = Time.fixedTime + QaPlayerController.StepDurationSeconds;
    }

    public override bool IsActionRunning() => Time.fixedTime < actionEnd;

    public override float CalculateReward(out bool isDone)
    {
        Vector3 position = source.Player.transform.position;
        float distance = QaNavigationCoordinator.Distance(position, target);
        minimumDistance = Mathf.Min(minimumDistance, distance);
        pathLength += QaNavigationCoordinator.Distance(previousPosition, position);
        previousPosition = position;
        bool reached = distance <= QaNavigationCoordinator.AcceptanceRadius;
        isDone = reached || source.Player.Metrics.StepCount >= 600;
        if (isDone && !recorded)
        {
            var result = new Episode
            {
                seed = seed, reached = reached, steps = source.Player.Metrics.StepCount,
                elapsed_seconds = source.Player.Metrics.ElapsedSeconds,
                initial_distance = initialDistance, final_distance = distance, minimum_distance = minimumDistance,
                target_x = target.x, target_z = target.z, angle_radians = angle,
                final_x = position.x, final_z = position.z, path_length = pathLength,
                stuck_events = source.Player.Metrics.StuckEvents
            };
            File.AppendAllText(Path.Combine(directory, "episodes.jsonl"), JsonUtility.ToJson(result) + "\n");
            recorded = true;
            source.Sensors.EndAttempt();
        }
        return 0f;
    }

    [Serializable] private class Episode
    {
        public int seed, steps, stuck_events;
        public bool reached;
        public float elapsed_seconds, initial_distance, final_distance, minimum_distance;
        public float target_x, target_z, angle_radians, final_x, final_z, path_length;
    }
}
