using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run with Unity -batchmode -nographics -projectPath <project>
// -executeMethod BreakableFeatureVerification.Run -logFile <log> (without -quit).
public static class BreakableFeatureVerification
{
    private const string Pending = "BreakableFeatureVerification.Pending";
    private static int stage;
    private static double deadline;
    private static BreakableProp prop;
    private static QaPlayerController player;
    private static Transform arena;

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Register() { EditorApplication.update += Tick; }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (stage == 0)
            {
                VerifyGeneration();
                arena = new GameObject("Breakable test arena").transform;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.SetParent(arena);
                floor.transform.position = new Vector3(1000f, -0.1f, 0f);
                floor.transform.localScale = new Vector3(10f, 0.2f, 10f);
                var actor = new GameObject("Test player");
                actor.transform.SetParent(arena);
                actor.transform.position = new Vector3(1000f, 0f, 0f);
                player = actor.AddComponent<QaPlayerController>();
                player.BeginTest();
                MethodInfo build = typeof(Generator).GetMethod("BuildBreakable", BindingFlags.Static | BindingFlags.NonPublic);
                var model = (GameObject)build.Invoke(null, new object[] { arena, new Vector3(1000f, 0f, 1f), false });
                prop = model.GetComponent<BreakableProp>();
                prop.Configure(null, null, 1f);
                // A second collider verifies that a single swing hits each prop once.
                model.AddComponent<SphereCollider>().radius = 0.35f;
                Physics.SyncTransforms();
                player.ApplyAction(new QaAction(0f, 0f, true, false, 0));
                Check(model.GetComponent<BoxCollider>().enabled, "One swing destroyed a two-health prop with multiple colliders.");
                Check(model.GetComponentInChildren<Renderer>().HasPropertyBlock(), "First hit did not flash.");
                deadline = EditorApplication.timeSinceStartup + 0.3;
                stage = 1;
            }
            else if (stage == 1 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(!prop.GetComponentInChildren<Renderer>().HasPropertyBlock(), "Hit flash did not reset.");
                Physics.SyncTransforms();
                player.ApplyAction(new QaAction(0f, 0f, true, false, 0));
                Check(!prop.GetComponent<BoxCollider>().enabled, "Second swing did not break the prop.");
                Check(arena.GetComponentsInChildren<QaLootPickup>().Length == 1, "Guaranteed loot did not drop exactly once.");
                Check(prop.transform.childCount > 4, "Destruction did not create debris.");
                prop.TakeDamage(100f);
                Check(arena.GetComponentsInChildren<QaLootPickup>().Length == 1, "Broken prop awarded duplicate loot.");
                deadline = EditorApplication.timeSinceStartup + 1.2;
                stage = 2;
            }
            else if (stage == 2 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(prop == null, "Debris did not clean itself up.");
                Physics.SyncTransforms();
                player.ApplyAction(new QaAction(0f, 0f, false, true, 0));
                Check(player.Metrics.PowerupsUsed == 1, "Dropped loot could not be collected with Interact.");
                Debug.Log("BREAKABLE VERIFICATION PASSED: three generated seeds, room/door clearance, multi-collider two-hit combat, flash reset, debris, single loot drop, cleanup and collection.");
                SessionState.SetBool(Pending, false);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(Pending, false);
            EditorApplication.Exit(1);
        }
    }

    private static void VerifyGeneration()
    {
        GenerationSettings settings = AssetDatabase.LoadAssetAtPath<GenerationSettings>("Assets/PCGDungeon/Settings/Settings.asset");
        Check(settings != null, "Generation settings missing.");
        var host = new GameObject("Generator verification");
        Generator generator = host.AddComponent<Generator>();
        var serialized = new SerializedObject(generator);
        serialized.FindProperty("generationSettings").objectReferenceValue = settings;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        for (int seed = 40; seed < 43; seed++)
        {
            Check(generator.RunProgram(new PcgGenerationParameters {
                Seed = seed, RoomCount = 8, AreaWidth = 100, AreaHeight = 100,
                MinRoomWidth = 8, MaxRoomWidth = 12, MinRoomHeight = 8, MaxRoomHeight = 12,
                PowerupsPerRoom = 1
            }), "Dungeon generation failed.");
            BreakableProp[] props = host.GetComponentsInChildren<BreakableProp>();
            Check(props.Length > 0, "Dungeon has no breakable props.");
            foreach (BreakableProp candidate in props)
            {
                Check(candidate.GetComponentInParent<Room>().Type == RoomType.Normal, "Prop blocks a spawn/boss/puzzle room.");
                foreach (GameObject door in generator.DoorGameObjects)
                    Check(Vector3.Distance(door.transform.position, candidate.transform.position) >= 2.5f, "Prop too close to a door.");
            }
            Debug.Log($"Breakable generation seed {seed}: {props.Length} props, room and door checks passed.");
        }
        UnityEngine.Object.DestroyImmediate(host);
    }
}
