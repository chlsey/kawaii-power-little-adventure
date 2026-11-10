using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Kcccr.UnityReinforcementLearning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Frozen local-policy evaluation through the real dungeon manager/generator.</summary>
[InitializeOnLoad]
public static class QaTwoRoomNavigationSession
{
    private const string Key = "qa-two-room-navigation";
    private static string directory;

    static QaTwoRoomNavigationSession()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            directory = SessionState.GetString(Key, "");
            if (directory.Length == 0 || state != PlayModeStateChange.EnteredPlayMode) return;
            Application.logMessageReceived += CaptureError;
            var manager = Object.FindFirstObjectByType<PcgDungeonManager>();
            manager.StartCoroutine(RunSession(manager));
        };
    }

    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        directory = args[Array.IndexOf(args, "-trainingRunDirectory") + 1];
        SessionState.SetString(Key, directory);
        EditorSceneManager.OpenScene("Assets/Scenes/QaPlayerTraining.unity");
        EditorApplication.EnterPlaymode();
    }

    private static IEnumerator RunSession(PcgDungeonManager manager)
    {
        EditorApplication.LockReloadAssemblies();
        Application.runInBackground = true;
        Time.timeScale = 100f;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        yield return null;
        var host = manager.GetComponent<RlTrainingHost>();
        host.StopTraining();
        manager.GetComponent<QaPlayerRlEnvironment>().enabled = false;
        manager.RemoteControlActive = true;
        manager.enabled = false;
        var environment = manager.gameObject.AddComponent<QaTwoRoomNavigationEnvironment>();
        environment.Configure(manager, directory);
        var serialized = new SerializedObject(host);
        serialized.FindProperty("environment").objectReferenceValue = environment;
        serialized.FindProperty("port").intValue = 7784;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (!host.StartTraining()) throw new InvalidOperationException("Two-room evaluation bridge startup");
        File.WriteAllText(Path.Combine(directory, "unity-ready.txt"), host.Port.ToString());
        while (!File.Exists(Path.Combine(directory, "session-finished.txt"))) yield return null;
        host.StopTraining();
        manager.EndPlayerEpisode("evaluation_finished");
        SessionState.SetString(Key, "");
        EditorApplication.UnlockReloadAssemblies();
        File.WriteAllText(Path.Combine(directory, "unity-finished.txt"), "Frozen evaluation complete\n");
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

public sealed class QaTwoRoomNavigationEnvironment : RlEnvironmentBehaviour
{
    private PcgDungeonManager manager;
    private Generator generator;
    private QaPlayerController player;
    private CharacterController actor;
    private ModelSensorPackage sensors;
    private QaDungeonExitHeuristic navigation;
    private string directory;
    private int seed, arrivals, collisionActions, awayActions, noWaypointActions;
    private float actionEnd, pathLength, minimumExitDistance, bestWaypointDistance;
    private Vector3 previousPosition;
    private bool recorded, enteredExitRoom;
    private float[] observation;
    private StreamWriter actions;
    private bool trainingRewards;
    private QaWaypointRewardTracker rewards;
    private float episodeReward;

    public override int ObservationSize => 134;
    public override int ActionSize => 2;

    public void Configure(PcgDungeonManager dungeonManager, string outputDirectory)
    {
        manager = dungeonManager;
        directory = outputDirectory;
        string session = Path.Combine(directory, "session.json");
        if (File.Exists(session)) trainingRewards = JsonUtility.FromJson<Settings>(File.ReadAllText(session)).training_rewards;
        generator = Object.FindFirstObjectByType<Generator>();
        // Clone the asset so the normal game scene/settings remain unchanged.
        var settings = Object.Instantiate(generator.GenerationSettings);
        var data = settings.generation;
        data.minRooms = data.maxRooms = 2;
        data.amountOfAdjacentRoomsToConnect = 1;
        data.minRoomWidth = data.maxRoomWidth = data.minRoomHeight = data.maxRoomHeight = 20;
        data.minBossRoomWidth = data.maxBossRoomWidth = data.minBossRoomHeight = data.maxBossRoomHeight = 20;
        data.areaWidth = data.areaHeight = 100;
        data.minEnemiesPerRoom = data.maxEnemiesPerRoom = data.puzzleRoomCount = 0;
        data.enemies.Clear(); data.bosses.Clear(); data.breakables.Clear();
        data.staticObjects.Clear(); data.wallObjects.Clear(); data.middleObjects.Clear();
        data.cornerObjectsRate = data.destroyablesRate = data.middleObjectsRate = 0;
        data.exitRequiresInteract = false;
        var serialized = new SerializedObject(generator);
        serialized.FindProperty("generationSettings").objectReferenceValue = settings;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        File.WriteAllText(Path.Combine(directory, "generation-settings.json"), JsonUtility.ToJson(data, true));
        actions = new StreamWriter(Path.Combine(directory, "actions.jsonl"));
    }

    public override void ResetEpisode(int episodeSeed)
    {
        seed = episodeSeed;
        if (!manager.StartPlayerEpisode(new PcgGenerationParameters
        {
            Seed = seed, RoomCount = 2, AreaWidth = 100, AreaHeight = 100,
            MinRoomWidth = 20, MaxRoomWidth = 20, MinRoomHeight = 20, MaxRoomHeight = 20,
            PowerupsPerRoom = 0
        })) throw new InvalidOperationException("Two-room generation failed for seed " + seed);
        if (manager.GeneratedRoomCount != 2) throw new InvalidOperationException("Expected exactly two rooms");
        player = manager.ActivePlayer;
        actor = player.GetComponent<CharacterController>();
        sensors = player.GetComponent<ModelSensorPackage>();
        // Align the controller's feet with the generated floor's 0.1-unit top.
        actor.enabled = false;
        player.transform.position += Vector3.up * 0.11f;
        actor.enabled = true;
        Physics.SyncTransforms();
        sensors.BeginAttempt();
        navigation = new QaDungeonExitHeuristic(generator, actor, sensors, manager.ExitPosition);
        navigation.Advance(false);
        rewards = new QaWaypointRewardTracker();
        rewards.Assign(navigation.HasWaypoint, navigation.Version, navigation.Waypoint, player.transform.position);
        episodeReward = 0f;
        arrivals = collisionActions = awayActions = noWaypointActions = 0;
        pathLength = 0f;
        minimumExitDistance = QaNavigationCoordinator.Distance(player.transform.position, manager.ExitPosition);
        previousPosition = player.transform.position;
        recorded = enteredExitRoom = false;
        bestWaypointDistance = navigation.HasWaypoint ? QaNavigationCoordinator.Distance(previousPosition, navigation.Waypoint) : -1f;
        Append("layouts.jsonl", new Layout
        {
            seed = seed, spawn = player.transform.position, exit = manager.ExitPosition,
            rooms = manager.GeneratedRoomCount, floors = navigation.Floors,
            initial_route = navigation.InitialRoute.ToArray(), route_available = navigation.HasWaypoint
        });
    }

    public override float[] CollectObservation()
    {
        sensors.Capture();
        observation = QaWaypointPolicyMapping.Observe(player.Observe(), sensors,
            navigation.HasWaypoint, navigation.Waypoint, player.transform.position);
        return observation;
    }

    public override void ApplyAction(float[] action)
    {
        Vector3 position = player.transform.position;
        actions.WriteLine(JsonUtility.ToJson(new ActionRecord
        {
            seed = seed, step = player.Metrics.StepCount + 1, position = position,
            waypoint = navigation.Waypoint, has_waypoint = navigation.HasWaypoint,
            waypoint_version = navigation.Version, phase = Phase(position),
            observation = observation, action = action
        }));
        if (navigation.HasWaypoint)
        {
            Vector3 delta = navigation.Waypoint - position;
            if (delta.x * action[0] + delta.z * action[1] <= 0f) awayActions++;
        }
        else noWaypointActions++;
        player.ApplyAction(QaLocalMovementPolicyMapping.Decode(action));
        actionEnd = Time.fixedTime + QaPlayerController.StepDurationSeconds;
    }

    public override bool IsActionRunning() => Time.fixedTime < actionEnd;

    public override float CalculateReward(out bool isDone)
    {
        Vector3 position = player.transform.position;
        float moved = QaNavigationCoordinator.Distance(previousPosition, position);
        pathLength += moved;
        if (moved < 0.02f) collisionActions++;
        previousPosition = position;
        minimumExitDistance = Mathf.Min(minimumExitDistance, QaNavigationCoordinator.Distance(position, manager.ExitPosition));
        if (Phase(position) == "exit_room") enteredExitRoom = true;
        if (navigation.HasWaypoint)
        {
            float distance = QaNavigationCoordinator.Distance(position, navigation.Waypoint);
            bestWaypointDistance = Mathf.Min(bestWaypointDistance, distance);
            if (distance <= QaNavigationCoordinator.AcceptanceRadius) arrivals++;
        }
        isDone = player.Metrics.Completed || player.Metrics.PlayerDied || player.Metrics.StepCount >= 600;
        // Measure the action against its original instruction before navigation hands off.
        float reward = 0f;
        if (trainingRewards)
        {
            reward = rewards.Measure(position) - 0.001f;
            if (player.Metrics.Completed) reward += 50f;
            if (rewards.PositiveRewardTotal > 5.00001f) throw new InvalidOperationException("Two-room shaping budget exceeded");
        }
        episodeReward += reward;
        if (!isDone)
        {
            int version = navigation.Version;
            navigation.Advance(true);
            rewards.Assign(navigation.HasWaypoint, navigation.Version, navigation.Waypoint, position);
            if (version != navigation.Version)
                bestWaypointDistance = QaNavigationCoordinator.Distance(position, navigation.Waypoint);
        }
        if (isDone && !recorded)
        {
            string reason = player.Metrics.Completed ? "completed" : "timeout";
            if (player.Metrics.PlayerDied) reason = "player_died";
            Append("episodes.jsonl", new Episode
            {
                seed = seed, completed = player.Metrics.Completed, reason = reason,
                steps = player.Metrics.StepCount, elapsed_seconds = player.Metrics.ElapsedSeconds,
                path_length = pathLength, final_exit_distance = QaNavigationCoordinator.Distance(position, manager.ExitPosition),
                minimum_exit_distance = minimumExitDistance, waypoint_arrivals = arrivals,
                assignments = navigation.Version, replans = navigation.Replans,
                no_waypoint_actions = noWaypointActions, low_movement_actions = collisionActions,
                away_actions = awayActions, stuck_events = player.Metrics.StuckEvents,
                final_phase = Phase(position), entered_exit_room = enteredExitRoom,
                final_waypoint_distance = navigation.HasWaypoint ? QaNavigationCoordinator.Distance(position, navigation.Waypoint) : -1f,
                closest_final_waypoint_distance = bestWaypointDistance, position = position,
                training_rewards = trainingRewards, positive_waypoint_reward = rewards.PositiveRewardTotal,
                total_reward = episodeReward
            });
            actions.Flush();
            recorded = true;
            manager.EndPlayerEpisode(reason);
        }
        return reward;
    }

    private string Phase(Vector3 point)
    {
        foreach (Room room in generator.GetComponentsInChildren<Room>())
        {
            Vector3 delta = point - room.transform.position;
            if (Mathf.Abs(delta.x) < room.GeneratedSize.x * 0.5f && Mathf.Abs(delta.z) < room.GeneratedSize.y * 0.5f)
                return room == generator.BossSpawnRoom ? "exit_room" : "spawn_room";
        }
        return "corridor";
    }

    private void Append(string name, object value) => File.AppendAllText(Path.Combine(directory, name), JsonUtility.ToJson(value) + "\n");
    private void OnDestroy() { actions?.Dispose(); }

    [Serializable] private class Settings { public bool training_rewards; }

    [Serializable] private class Layout
    {
        public int seed, rooms;
        public bool route_available;
        public Vector3 spawn, exit;
        public Vector3[] floors, initial_route;
    }
    [Serializable] private class ActionRecord
    {
        public int seed, step, waypoint_version;
        public bool has_waypoint;
        public string phase;
        public Vector3 position, waypoint;
        public float[] observation, action;
    }
    [Serializable] private class Episode
    {
        public int seed, steps, waypoint_arrivals, assignments, replans, no_waypoint_actions, low_movement_actions, away_actions, stuck_events;
        public bool completed, entered_exit_room;
        public bool training_rewards;
        public string reason, final_phase;
        public float elapsed_seconds, path_length, final_exit_distance, minimum_exit_distance, final_waypoint_distance, closest_final_waypoint_distance;
        public Vector3 position;
        public float positive_waypoint_reward, total_reward;
    }
}

/// <summary>Test-only oracle: A* to the known exit, then furthest clear waypoint within ten units.</summary>
public sealed class QaDungeonExitHeuristic
{
    private readonly CharacterController actor;
    private readonly ModelSensorPackage sensors;
    private readonly HashSet<Vector2Int> floorCells = new HashSet<Vector2Int>();
    private readonly Vector3 exit;
    private float closest;
    private int stalled;
    public bool HasWaypoint { get; private set; }
    public Vector3 Waypoint { get; private set; }
    public int Version { get; private set; }
    public int Replans { get; private set; }
    public Vector3[] Floors { get; }
    public List<Vector3> InitialRoute { get; private set; }

    public QaDungeonExitHeuristic(Generator generator, CharacterController actor, ModelSensorPackage sensors, Vector3 exit)
    {
        this.actor = actor; this.sensors = sensors; this.exit = exit;
        var floors = new List<Vector3>();
        foreach (Transform tile in generator.GetComponentsInChildren<Transform>())
        {
            if (!tile.CompareTag("Floor")) continue;
            floors.Add(tile.position);
            floorCells.Add(Cell(tile.position));
        }
        Floors = floors.ToArray();
    }

    public void Advance(bool actionCompleted)
    {
        Vector3 position = actor.transform.position;
        if (HasWaypoint && QaNavigationCoordinator.Distance(position, Waypoint) > QaNavigationCoordinator.AcceptanceRadius)
        {
            float distance = QaNavigationCoordinator.Distance(position, Waypoint);
            if (closest - distance >= 0.05f) { closest = distance; stalled = 0; }
            else if (actionCompleted) stalled++;
            if (stalled < 20 && CanUseWaypoint(Waypoint)) return;
            Replans++;
        }
        HasWaypoint = false;
        List<Vector3> route = Route(position);
        if (InitialRoute == null) InitialRoute = new List<Vector3>(route);
        for (int i = route.Count - 1; i >= 0; i--)
        {
            float distance = QaNavigationCoordinator.Distance(position, route[i]);
            if (distance <= 0.5f || distance > 10f || !CanUseWaypoint(route[i])) continue;
            Waypoint = route[i]; HasWaypoint = true; Version++; stalled = 0; closest = distance;
            return;
        }
    }

    private static Vector2Int Cell(Vector3 point) => new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.z));

    private List<Vector3> Route(Vector3 position)
    {
        Vector2Int start = Cell(position), target = Cell(exit);
        var open = new List<Vector2Int> { start };
        var cost = new Dictionary<Vector2Int, int> { [start] = 0 };
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var closed = new HashSet<Vector2Int>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        while (open.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < open.Count; i++)
                if (cost[open[i]] + Manhattan(open[i], target) < cost[open[best]] + Manhattan(open[best], target)) best = i;
            Vector2Int cell = open[best];
            open.RemoveAt(best);
            if (cell == target)
            {
                var route = new List<Vector3>();
                while (cell != start)
                {
                    route.Add(new Vector3(cell.x, position.y, cell.y));
                    cell = parent[cell];
                }
                route.Reverse();
                return route;
            }
            closed.Add(cell);
            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = cell + direction;
                if (!floorCells.Contains(next) || closed.Contains(next)) continue;
                int candidate = cost[cell] + 1;
                if (cost.TryGetValue(next, out int previous) && candidate >= previous) continue;
                cost[next] = candidate; parent[next] = cell;
                if (!open.Contains(next)) open.Add(next);
            }
        }
        return new List<Vector3>();
    }

    private static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private bool CanUseWaypoint(Vector3 point)
    {
        Vector3 position = actor.transform.position;
        Vector3 delta = point - position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance < 0.0001f || distance > sensors.RayRange) return false;
        // Corridor centres lie on integer tile coordinates; the normal quarter-unit
        // grid/nominal unscaled radius is too conservative for this scaled game prefab.
        float radius = actor.radius * Mathf.Max(Mathf.Abs(actor.transform.lossyScale.x), Mathf.Abs(actor.transform.lossyScale.z)) + 0.01f;
        Vector3 center = actor.transform.TransformPoint(actor.center);
        float half = Mathf.Max(0f, actor.height * Mathf.Abs(actor.transform.lossyScale.y) * 0.5f - radius);
        Vector3 bottom = center - Vector3.up * half, top = center + Vector3.up * half;
        foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius, delta / distance, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (IsWall(hit.collider)) return false;
        foreach (Collider hit in Physics.OverlapCapsule(bottom + delta, top + delta, radius,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (IsWall(hit)) return false;
        // The complete swept segment must stay over generated floor, including corners.
        for (int i = 0; i <= Mathf.CeilToInt(distance / 0.05f); i++)
            if (!floorCells.Contains(Cell(Vector3.Lerp(position, point, Mathf.Min(1f, i * 0.05f / distance))))) return false;
        return true;
    }

    private static bool IsWall(Collider hit)
    {
        var marker = hit.GetComponentInParent<ModelSensorTarget>();
        return marker != null && marker.HitId == QaObjectType.Wall;
    }
}
