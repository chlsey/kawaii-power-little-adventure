using System;
using System.Collections;
using System.IO;
using Kcccr.UnityReinforcementLearning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>One fixed waypoint, real player physics, and no navigation or learning.</summary>
[InitializeOnLoad]
public static class QaWaypointReachSession
{
    private const string Key = "qa-waypoint-reach-session";
    private static string directory;

    static QaWaypointReachSession()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            directory = SessionState.GetString(Key, "");
            if (directory.Length == 0 || state != PlayModeStateChange.EnteredPlayMode) return;
            Application.logMessageReceived += CaptureError;
            var environment = Object.FindFirstObjectByType<QaSingleRoomRlEnvironment>();
            environment.StartCoroutine(Observe(environment));
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

    // empty room -> fixed waypoint observations -> real player movement -> arrival or 60-second timeout
    private static IEnumerator Observe(QaSingleRoomRlEnvironment environment)
    {
        EditorApplication.LockReloadAssemblies();
        Application.runInBackground = true;
        Time.timeScale = 1f;
        yield return null;
        environment.GetComponent<RlTrainingHost>().StopTraining();
        environment.ResetEpisode(260000);
        var serialized = new SerializedObject(environment);
        var exit = (Transform)serialized.FindProperty("exit").objectReferenceValue;
        var floor = (BoxCollider)serialized.FindProperty("roomFloor").objectReferenceValue;
        exit.gameObject.SetActive(false);

        // Move to the room centre and erase reset-time perception of the disabled exit.
        var player = environment.Player;
        var actor = player.GetComponent<CharacterController>();
        actor.enabled = false;
        player.transform.SetPositionAndRotation(new Vector3(0f, floor.bounds.max.y + 0.01f, 0f), Quaternion.identity);
        actor.enabled = true;
        player.BeginTest();
        Physics.SyncTransforms();
        environment.Sensors.BeginAttempt();
        Vector3 target = player.transform.position + new Vector3(1f, 0f, 1f).normalized * 4f;
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Fixed waypoint (not an exit)";
        marker.transform.position = target + Vector3.up * 0.25f;
        marker.transform.localScale = Vector3.one * 0.5f;
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        File.WriteAllText(Path.Combine(directory, "setup.json"), JsonUtility.ToJson(new Setup
        {
            room_width = floor.bounds.size.x, room_depth = floor.bounds.size.z,
            target_x = target.x, target_z = target.z,
            episode_seconds = 60f, acceptance_radius = QaNavigationCoordinator.AcceptanceRadius,
            observation_contract = environment.PolicyVersion
        }, true));

        // A fixed assignment replaces the coordinator; its four features retain the policy contract.
        for (int step = 0; step <= 600; step++)
        {
            environment.Sensors.Capture();
            Vector3 delta = target - player.transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            bool reached = distance <= QaNavigationCoordinator.AcceptanceRadius;
            bool done = reached || step == 600;
            float[] observation = QaLocalMovementPolicyMapping.Observe(player.Observe(), environment.Sensors.Rays);
            observation[130] = 1f;
            observation[131] = delta.normalized.x;
            observation[132] = delta.normalized.z;
            observation[133] = Mathf.Clamp01(distance / environment.Sensors.RayRange);
            var frame = new Frame
            {
                step = step, elapsed_seconds = player.Metrics.ElapsedSeconds,
                x = player.transform.position.x, z = player.transform.position.z,
                distance = distance, reached = reached, done = done,
                exit_visible = environment.Sensors.ExitVisible, observation = observation
            };
            string framePath = Path.Combine(directory, "observation-" + step + ".json");
            File.WriteAllText(framePath + ".tmp", JsonUtility.ToJson(frame));
            File.Move(framePath + ".tmp", framePath);
            if (done)
            {
                environment.Sensors.EndAttempt();
                SessionState.SetString(Key, "");
                EditorApplication.UnlockReloadAssemblies();
                EditorApplication.Exit(0);
                yield break;
            }
            string actionPath = Path.Combine(directory, "action-" + step + ".json");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!File.Exists(actionPath))
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException("Frozen policy action " + step);
                yield return null;
            }
            var action = JsonUtility.FromJson<ActionRecord>(File.ReadAllText(actionPath));
            player.ApplyAction(QaLocalMovementPolicyMapping.Decode(action.action));
            float until = Time.fixedTime + QaPlayerController.StepDurationSeconds;
            while (Time.fixedTime < until) yield return null;
        }
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

    [Serializable] private class ActionRecord { public float[] action; }
    [Serializable] private class Setup
    {
        public float room_width, room_depth, target_x, target_z, episode_seconds, acceptance_radius;
        public string observation_contract;
    }
    [Serializable] private class Frame
    {
        public int step;
        public float elapsed_seconds, x, z, distance;
        public bool reached, done, exit_visible;
        public float[] observation;
    }
}
