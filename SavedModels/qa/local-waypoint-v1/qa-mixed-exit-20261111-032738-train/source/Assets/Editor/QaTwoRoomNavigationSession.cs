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
    private bool roomThroughput;
    private bool roomSequence;
    private bool mixedRooms;
    private QaMixedRoomLayout mixedLayout;
    private Vector3 ExitPosition => mixedRooms ? mixedLayout.Exit : manager.ExitPosition;
    private Room[] rooms;
    private readonly HashSet<Room> visitedRooms = new HashSet<Room>();
    private int newRooms, goalRoom, roomHandoffs;

    public override int ObservationSize => 134;
    public override int ActionSize => 2;

    public void Configure(PcgDungeonManager dungeonManager, string outputDirectory)
    {
        manager = dungeonManager;
        directory = outputDirectory;
        string session = Path.Combine(directory, "session.json");
        if (File.Exists(session))
        {
            var options = JsonUtility.FromJson<Settings>(File.ReadAllText(session));
            trainingRewards = options.training_rewards;
            roomThroughput = options.room_throughput;
            roomSequence = options.room_sequence;
            mixedRooms = options.mixed_rooms;
        }
        generator = Object.FindFirstObjectByType<Generator>();
        // Clone the asset so the normal game scene/settings remain unchanged.
        var settings = Object.Instantiate(generator.GenerationSettings);
        var data = settings.generation;
        int roomCount = roomThroughput ? 8 : 2;
        if (roomSequence) roomCount = 16;
        if (mixedRooms) roomCount = 3;
        data.minRooms = data.maxRooms = roomCount;
        data.amountOfAdjacentRoomsToConnect = 1;
        data.minRoomWidth = data.maxRoomWidth = data.minRoomHeight = data.maxRoomHeight = 20;
        data.minBossRoomWidth = data.maxBossRoomWidth = data.minBossRoomHeight = data.maxBossRoomHeight = 20;
        data.areaWidth = data.areaHeight = roomThroughput ? 160 : 100;
        if (roomSequence) { data.areaWidth = 480; data.areaHeight = 200; }
        if (mixedRooms)
        {
            data.areaWidth = 120; data.areaHeight = 160; data.minRooms = 1;
            // The ordered training chain supplies its connections explicitly, including
            // the valid single-room case that has no adjacent room to connect.
            data.amountOfAdjacentRoomsToConnect = 0;
        }
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
        int requestedRooms = roomSequence ? 16 : (roomThroughput ? 8 : 2);
        if (mixedRooms) requestedRooms = 1 + seed % 3;
        if (!manager.StartPlayerEpisode(new PcgGenerationParameters
        {
            Seed = seed, RoomCount = requestedRooms,
            AreaWidth = mixedRooms ? 120 : (roomSequence ? 480 : (roomThroughput ? 160 : 100)),
            AreaHeight = mixedRooms ? 160 : (roomSequence ? 200 : (roomThroughput ? 160 : 100)),
            MinRoomWidth = 20, MaxRoomWidth = 20, MinRoomHeight = 20, MaxRoomHeight = 20,
            PowerupsPerRoom = 0, TrainingRoomSequence = roomSequence || mixedRooms
        })) throw new InvalidOperationException("Two-room generation failed for seed " + seed);
        if (manager.GeneratedRoomCount != requestedRooms) throw new InvalidOperationException("Unexpected room count");
        player = manager.ActivePlayer;
        actor = player.GetComponent<CharacterController>();
        sensors = player.GetComponent<ModelSensorPackage>();
        rooms = generator.GetComponentsInChildren<Room>();
        if (mixedRooms)
        {
            Array.Sort(rooms, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            mixedLayout = new QaMixedRoomLayout();
            mixedLayout.Build(generator, rooms, player, seed);
        }
        // Room throughput keeps running after visiting the boss room; no exit trigger ends it.
        if (roomThroughput)
            foreach (QaExitZone exit in generator.GetComponentsInChildren<QaExitZone>()) exit.gameObject.SetActive(false);
        // Align the controller's feet with the generated floor's 0.1-unit top.
        actor.enabled = false;
        if (mixedRooms) player.transform.position = mixedLayout.Spawn;
        player.transform.position += Vector3.up * 0.11f;
        actor.enabled = true;
        if (mixedRooms) player.BeginTest();
        Physics.SyncTransforms();
        sensors.BeginAttempt();
        navigation = new QaDungeonExitHeuristic(generator, actor, sensors, ExitPosition, mixedRooms);
        visitedRooms.Clear();
        visitedRooms.Add(generator.PlayerSpawnRoom);
        newRooms = roomHandoffs = 0;
        goalRoom = -1;
        if (roomThroughput) SelectNextRoom();
        navigation.Advance(false);
        rewards = new QaWaypointRewardTracker();
        rewards.Assign(navigation.HasWaypoint, navigation.Version, navigation.Waypoint, player.transform.position);
        episodeReward = 0f;
        arrivals = collisionActions = awayActions = noWaypointActions = 0;
        pathLength = 0f;
        minimumExitDistance = QaNavigationCoordinator.Distance(player.transform.position, ExitPosition);
        previousPosition = player.transform.position;
        recorded = enteredExitRoom = false;
        bestWaypointDistance = navigation.HasWaypoint ? QaNavigationCoordinator.Distance(previousPosition, navigation.Waypoint) : -1f;
        Append("layouts.jsonl", new Layout
        {
            seed = seed, spawn = player.transform.position, exit = ExitPosition,
            rooms = manager.GeneratedRoomCount, floors = navigation.Floors,
            room_centers = Array.ConvertAll(rooms, room => room.transform.position),
            initial_route = navigation.InitialRoute.ToArray(), route_available = navigation.HasWaypoint,
            mixed_rooms = mixedRooms, room_details = mixedRooms ? mixedLayout.Rooms : null
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
            new_rooms = newRooms, goal_room = goalRoom,
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
        int newRoomCount = 0;
        if (mixedRooms)
        {
            int index = mixedLayout.RoomIndex(position);
            if (index >= 0 && visitedRooms.Add(rooms[index])) newRooms++;
        }
        if (roomThroughput)
        {
            // One unit inside the room bounds counts entry; spawn and revisits never pay.
            for (int i = 0; i < rooms.Length; i++)
            {
                if (visitedRooms.Contains(rooms[i])) continue;
                Vector3 delta = position - rooms[i].transform.position;
                if (Mathf.Abs(delta.x) >= rooms[i].GeneratedSize.x * 0.5f - 1f ||
                    Mathf.Abs(delta.z) >= rooms[i].GeneratedSize.y * 0.5f - 1f || Mathf.Abs(delta.y) >= 1.5f) continue;
                visitedRooms.Add(rooms[i]); newRooms++; newRoomCount++;
                Append("room-visits.jsonl", new RoomVisit { seed = seed, step = player.Metrics.StepCount,
                    room = i, new_rooms = newRooms, position = position });
            }
        }
        minimumExitDistance = Mathf.Min(minimumExitDistance, QaNavigationCoordinator.Distance(position, ExitPosition));
        if (Phase(position) == "exit_room") enteredExitRoom = true;
        if (navigation.HasWaypoint)
        {
            float distance = QaNavigationCoordinator.Distance(position, navigation.Waypoint);
            bestWaypointDistance = Mathf.Min(bestWaypointDistance, distance);
            if (distance <= QaNavigationCoordinator.AcceptanceRadius) arrivals++;
        }
        isDone = player.Metrics.Completed || player.Metrics.PlayerDied || player.Metrics.StepCount >= 600;
        if (roomThroughput && player.Metrics.Completed) throw new InvalidOperationException("Exit ended a room-throughput episode");
        if (roomSequence && newRooms == rooms.Length - 1) throw new InvalidOperationException("Prepared room sequence was exhausted");
        // Measure the action against its original instruction before navigation hands off.
        float reward = 0f;
        if (trainingRewards)
        {
            reward = rewards.Measure(position) - 0.001f;
            if (roomThroughput) reward += 10f * newRoomCount;
            else if (player.Metrics.Completed) reward += 50f;
            if (rewards.PositiveRewardTotal > 5.00001f) throw new InvalidOperationException("Two-room shaping budget exceeded");
        }
        episodeReward += reward;
        if (!isDone)
        {
            if (roomThroughput && newRoomCount > 0) SelectNextRoom();
            int version = navigation.Version;
            navigation.Advance(true);
            rewards.Assign(navigation.HasWaypoint, navigation.Version, navigation.Waypoint, position);
            if (version != navigation.Version)
                bestWaypointDistance = QaNavigationCoordinator.Distance(position, navigation.Waypoint);
        }
        if (isDone && !recorded)
        {
            string reason = player.Metrics.Completed ? "completed" : "timeout";
            if (roomThroughput) reason = "time_budget";
            if (player.Metrics.PlayerDied) reason = "player_died";
            Append("episodes.jsonl", new Episode
            {
                seed = seed, completed = player.Metrics.Completed, reason = reason,
                steps = player.Metrics.StepCount, elapsed_seconds = player.Metrics.ElapsedSeconds,
                path_length = pathLength, final_exit_distance = QaNavigationCoordinator.Distance(position, ExitPosition),
                minimum_exit_distance = minimumExitDistance, waypoint_arrivals = arrivals,
                assignments = navigation.Version, replans = navigation.Replans,
                no_waypoint_actions = noWaypointActions, low_movement_actions = collisionActions,
                away_actions = awayActions, stuck_events = player.Metrics.StuckEvents,
                final_phase = Phase(position), entered_exit_room = enteredExitRoom,
                final_waypoint_distance = navigation.HasWaypoint ? QaNavigationCoordinator.Distance(position, navigation.Waypoint) : -1f,
                closest_final_waypoint_distance = bestWaypointDistance, position = position,
                training_rewards = trainingRewards, positive_waypoint_reward = rewards.PositiveRewardTotal,
                total_reward = episodeReward,
                new_rooms = newRooms, room_handoffs = roomHandoffs, room_throughput = roomThroughput,
                room_count = rooms.Length, room_sequence = roomSequence, mixed_rooms = mixedRooms,
                room_types = mixedRooms ? Array.ConvertAll(mixedLayout.Rooms, room => room.type) : null
            });
            actions.Flush();
            recorded = true;
            manager.EndPlayerEpisode(reason);
        }
        return reward;
    }

    private string Phase(Vector3 point)
    {
        if (mixedRooms)
        {
            int index = mixedLayout.RoomIndex(point);
            if (index < 0) return "corridor";
            if (index == rooms.Length - 1) return "exit_room";
            return "room_" + index;
        }
        foreach (Room room in generator.GetComponentsInChildren<Room>())
        {
            Vector3 delta = point - room.transform.position;
            if (Mathf.Abs(delta.x) < room.GeneratedSize.x * 0.5f && Mathf.Abs(delta.z) < room.GeneratedSize.y * 0.5f)
            {
                if (roomThroughput) return "room_" + Array.IndexOf(rooms, room);
                return room == generator.BossSpawnRoom ? "exit_room" : "spawn_room";
            }
        }
        return "corridor";
    }

    private void Append(string name, object value) => File.AppendAllText(Path.Combine(directory, name), JsonUtility.ToJson(value) + "\n");
    private void OnDestroy() { actions?.Dispose(); }

    private void SelectNextRoom()
    {
        goalRoom = -1;
        int shortest = int.MaxValue;
        for (int i = 0; i < rooms.Length; i++)
        {
            if (visitedRooms.Contains(rooms[i])) continue;
            if (roomSequence) { goalRoom = i; break; }
            int length = navigation.RouteSteps(rooms[i].transform.position);
            if (length <= 0 || length >= shortest) continue;
            shortest = length; goalRoom = i;
        }
        if (goalRoom < 0) navigation.ClearGoal();
        else { navigation.SetGoal(rooms[goalRoom].transform.position); roomHandoffs++; }
    }

    [Serializable] private class Settings { public bool training_rewards, room_throughput, room_sequence, mixed_rooms; }
    [Serializable] private class RoomVisit { public int seed, step, room, new_rooms; public Vector3 position; }

    [Serializable] private class Layout
    {
        public int seed, rooms;
        public bool route_available;
        public Vector3 spawn, exit;
        public Vector3[] floors, initial_route, room_centers;
        public bool mixed_rooms;
        public QaMixedRoomLayout.RoomData[] room_details;
    }
    [Serializable] private class ActionRecord
    {
        public int seed, step, waypoint_version, new_rooms, goal_room;
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
        public int new_rooms, room_handoffs;
        public bool room_throughput;
        public int room_count;
        public bool room_sequence;
        public bool mixed_rooms;
        public string[] room_types;
    }
}

/// <summary>Test-only oracle: A* to the known exit, then furthest clear waypoint within ten units.</summary>
public sealed class QaDungeonExitHeuristic
{
    private readonly CharacterController actor;
    private readonly ModelSensorPackage sensors;
    private readonly HashSet<Vector2Int> floorCells = new HashSet<Vector2Int>();
    private Vector3 exit;
    private bool hasGoal = true;
    private readonly bool retainArrivedWaypoint;
    private float closest;
    private int stalled;
    public bool HasWaypoint { get; private set; }
    public Vector3 Waypoint { get; private set; }
    public int Version { get; private set; }
    public int Replans { get; private set; }
    public Vector3[] Floors { get; }
    public List<Vector3> InitialRoute { get; private set; }

    public QaDungeonExitHeuristic(Generator generator, CharacterController actor, ModelSensorPackage sensors, Vector3 exit,
        bool retainArrivedWaypoint = false)
    {
        this.actor = actor; this.sensors = sensors; this.exit = exit;
        this.retainArrivedWaypoint = retainArrivedWaypoint;
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
        if (!hasGoal) return;
        Vector3 position = actor.transform.position;
        bool keepArrival = retainArrivedWaypoint && HasWaypoint &&
            QaNavigationCoordinator.Distance(position, Waypoint) <= QaNavigationCoordinator.AcceptanceRadius;
        if (HasWaypoint && QaNavigationCoordinator.Distance(position, Waypoint) > QaNavigationCoordinator.AcceptanceRadius)
        {
            float distance = QaNavigationCoordinator.Distance(position, Waypoint);
            if (closest - distance >= 0.05f) { closest = distance; stalled = 0; }
            else if (actionCompleted) stalled++;
            if (stalled < 20 && CanUseWaypoint(Waypoint)) return;
            Replans++;
        }
        HasWaypoint = false;
        List<Vector3> route = Route(position, exit);
        if (InitialRoute == null) InitialRoute = new List<Vector3>(route);
        for (int i = route.Count - 1; i >= 0; i--)
        {
            float distance = QaNavigationCoordinator.Distance(position, route[i]);
            if (distance <= 0.5f || distance > 10f || !CanUseWaypoint(route[i])) continue;
            Waypoint = route[i]; HasWaypoint = true; Version++; stalled = 0; closest = distance;
            return;
        }
        // At a tight turn, entering the arrival radius can precede a clear line to the
        // next route point. Keep the same single instruction so the actor can finish
        // approaching the bend instead of dropping guidance and waiting indefinitely.
        if (keepArrival && CanUseWaypoint(Waypoint)) HasWaypoint = true;
    }

    private static Vector2Int Cell(Vector3 point) => new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.z));

    public void SetGoal(Vector3 point) { exit = point; hasGoal = true; HasWaypoint = false; stalled = 0; }
    public void ClearGoal()
    {
        hasGoal = false; HasWaypoint = false;
        if (InitialRoute == null) InitialRoute = new List<Vector3>();
    }
    public int RouteSteps(Vector3 point) => Route(actor.transform.position, point).Count;

    private List<Vector3> Route(Vector3 position, Vector3 destination)
    {
        Vector2Int start = Cell(position), target = Cell(destination);
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
