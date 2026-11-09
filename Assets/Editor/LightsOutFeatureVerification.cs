using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Unity -batchmode -nographics -projectPath <project>
// -executeMethod LightsOutFeatureVerification.Run -logFile <log> (without -quit).
public static class LightsOutFeatureVerification
{
    private const string Pending = "LightsOutFeatureVerification.Pending";

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Register() => EditorApplication.update += Tick;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false);
        try
        {
            VerifyMechanic();
            VerifySpawning();
            Debug.Log("LIGHTS OUT VERIFICATION PASSED: all starting boards solvable, nearest-switch interaction, door lock/unlock, one solve event, spawn rate zero/100, caps, regeneration, single puzzle per room and reward.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static int Solution(LightsOutPuzzle puzzle)
    {
        int state = 0;
        for (int i = 0; i < 3; i++) if (puzzle.IsLit(i)) state |= 1 << i;
        int[] effects = { 3, 7, 6 };
        for (int mask = 0; mask < 8; mask++)
        {
            int result = state;
            for (int i = 0; i < 3; i++) if ((mask & (1 << i)) != 0) result ^= effects[i];
            if (result == 7) return mask;
        }
        throw new Exception("Unsolvable Lights Out board.");
    }

    private static void Solve(LightsOutPuzzle puzzle)
    {
        int mask = Solution(puzzle);
        for (int i = 0; i < 3; i++) if ((mask & (1 << i)) != 0) puzzle.ToggleSwitch(i);
        Check(puzzle.IsSolved && puzzle.LitCount == 3, "Legal solution did not solve the board.");
    }

    private static void VerifyMechanic()
    {
        var actor = new GameObject("Puzzle test player");
        QaPlayerController player = actor.AddComponent<QaPlayerController>();
        player.BeginTest();
        for (int variation = 0; variation < 4; variation++)
        {
            var root = new GameObject("Puzzle mechanic test");
            LightsOutPuzzle puzzle = root.AddComponent<LightsOutPuzzle>();
            puzzle.Initialize(variation);
            Check(!puzzle.IsSolved && puzzle.LitCount < 3, "Puzzle spawned solved.");
            int mask = Solution(puzzle);
            Check((mask & (mask - 1)) != 0, "Starting puzzle takes fewer than two presses.");
            int events = 0;
            puzzle.Solved += () => events++;
            var doorObject = new GameObject("Test door");
            DungeonDoor door = doorObject.AddComponent<DungeonDoor>();
            PuzzleRoom room = root.AddComponent<PuzzleRoom>();
            room.Setup(puzzle, new[] { door }, new Bounds(Vector3.zero, Vector3.one * 10f));
            actor.transform.position = new Vector3(-1.3f, 0f, -0.65f);
            typeof(PuzzleRoom).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(room, null);
            Check(room.IsLocked && door.IsLocked, "Entering puzzle room did not lock its door.");
            bool right = puzzle.IsLit(2);
            bool left = puzzle.IsLit(0);
            bool middle = puzzle.IsLit(1);
            Physics.SyncTransforms();
            player.ApplyAction(new QaAction(0, 0, false, true, 0));
            Check(puzzle.IsLit(0) != left && puzzle.IsLit(1) != middle && puzzle.IsLit(2) == right,
                "E did not activate only the nearest (left) switch.");
            Solve(puzzle);
            Check(!door.IsLocked && door.IsOpen && !room.IsLocked, "Solving did not unlock/open the door.");
            Check(events == 1 && !puzzle.ToggleSwitch(0) && events == 1, "Solved puzzle could be triggered twice.");
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(doorObject);
        }
        UnityEngine.Object.DestroyImmediate(actor);
    }

    private static void VerifySpawning()
    {
        GenerationSettings original = AssetDatabase.LoadAssetAtPath<GenerationSettings>("Assets/PCGDungeon/Settings/Settings.asset");
        GenerationSettings settings = UnityEngine.Object.Instantiate(original);
        var host = new GameObject("Puzzle generator test");
        Generator generator = host.AddComponent<Generator>();
        DungeonValidator validator = host.AddComponent<DungeonValidator>();
        var serialized = new SerializedObject(generator);
        serialized.FindProperty("generationSettings").objectReferenceValue = settings;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        int[] rates = { 100, 100, 0, 25, 100 };
        int[] caps = { 2, 1, 2, 2, 0 };
        for (int run = 0; run < rates.Length; run++)
        {
            settings.generation.lightsOutSpawnRate = rates[run];
            settings.generation.maxLightsOutRooms = caps[run];
            Check(generator.RunProgram(new PcgGenerationParameters {
                Seed = 40 + run, RoomCount = 10, AreaWidth = 100, AreaHeight = 100,
                MinRoomWidth = 8, MaxRoomWidth = 12, MinRoomHeight = 8, MaxRoomHeight = 12, PowerupsPerRoom = 0
            }), "Dungeon failed generation.");
            ValidationResult validation = validator.Validate(generator);
            Check(validation.IsValid, "Generated dungeon rejected: " + validation.Message);
            LightsOutPuzzle[] puzzles = host.GetComponentsInChildren<LightsOutPuzzle>();
            Check(puzzles.Length <= caps[run], "Dungeon cap exceeded.");
            if (rates[run] == 0 || caps[run] == 0) Check(puzzles.Length == 0, "Disabled puzzles still spawned.");
            if (rates[run] == 100) Check(puzzles.Length == caps[run], "100% rate did not fill the cap.");
            foreach (Room room in host.GetComponentsInChildren<Room>())
            {
                Check(room.GetComponentsInChildren<RoomPuzzle>().Length <= 1, "Room contains more than one puzzle.");
                if (room == generator.PlayerSpawnRoom || room == generator.BossSpawnRoom)
                    Check(room.GetComponentInChildren<LightsOutPuzzle>() == null, "Puzzle in spawn/boss room.");
            }
            foreach (LightsOutPuzzle puzzle in puzzles)
            {
                Check(puzzle.GetComponentInParent<PuzzleRoom>().Puzzle == puzzle, "Puzzle is not connected to its room.");
                int before = host.GetComponentsInChildren<QaLootPickup>().Length;
                Solve(puzzle);
                Check(host.GetComponentsInChildren<QaLootPickup>().Length == before + 1, "Solving did not drop exactly one reward.");
            }
            Debug.Log($"Lights Out generation: rate {rates[run]}%, cap {caps[run]}, spawned {puzzles.Length}.");
        }
        UnityEngine.Object.DestroyImmediate(host);
        UnityEngine.Object.DestroyImmediate(settings);
    }
}
