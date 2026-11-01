using UnityEngine;
using UnityEngine.InputSystem;

public class PcgDungeonManager : MonoBehaviour
{
    [SerializeField] private Generator dungeonGenerator;
    [SerializeField] private PcgPolicyRunner policyRunner;
    [SerializeField] private DungeonValidator validator;
    [SerializeField] private QaPolicyRunner qaPolicyRunner;

    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private float qaStepIntervalSeconds = 1f;
    [SerializeField] private int maxQaSteps = 1000;
    [SerializeField] private float autoResetIntervalSeconds;
    [SerializeField] private int maxQaRuns;

    private QaPlayerController activePlayer;
    private ModelSensorPackage activeSensors;
    private float qaStepTimer;
    private float autoResetTimer;
    private bool remoteControlActive;
    private PcgGenerationParameters activeParameters;
    private DungeonQaAdapter qaAdapter;
    private int completedQaRuns;

    // The spawned QA player for the current episode, ready for ApplyAction calls. Null until a dungeon is accepted.
    public QaPlayerController ActivePlayer => activePlayer;
    public int ActiveSeed => activeParameters.Seed;
    public string PlayerEpisodeEndReason { get; private set; }
    public int CompletedQaRuns => completedQaRuns;
    public bool QaTestingComplete => maxQaRuns > 0 && completedQaRuns >= maxQaRuns;
    public Vector3 ExitPosition => dungeonGenerator.BossSpawnRoom.transform.position;
    public GenerationSettings GenerationSettings => dungeonGenerator.GenerationSettings;
    public int GeneratedRoomCount => dungeonGenerator.GetComponentsInChildren<Room>().Length;

    // Keep the boss's separate dimensions out of the regular-room training metric.
    public Vector2 GeneratedRegularRoomSize
    {
        get
        {
            Vector2 total = Vector2.zero;
            int count = 0;
            foreach (Room room in dungeonGenerator.GetComponentsInChildren<Room>())
            {
                if (room == dungeonGenerator.BossSpawnRoom)
                    continue;

                Vector2Int size = room.GeneratedSize;
                total += new Vector2(size.x, size.y);
                count++;
            }
            if (count == 0)
                return Vector2.zero;
            return total / count;
        }
    }

    // External RL training owns stepping while enabled; local play stays idle.
    public bool RemoteControlActive
    {
        get => remoteControlActive;
        set
        {
            remoteControlActive = value;
            if (value)
                EndPlayerEpisode("reset");
        }
    }

    private void Awake()
    {
        if (qaPolicyRunner == null)
            qaPolicyRunner = GetComponent<QaPolicyRunner>() ?? gameObject.AddComponent<QaPolicyRunner>();
    }

    private void Start()
    {
        if (generateOnStart)
            StartEpisode();
    }

    private void Update()
    {
        if (remoteControlActive)
            return;

        if (activePlayer != null && maxQaSteps > 0 && activePlayer.Metrics.StepCount >= maxQaSteps)
            EndPlayerEpisode("max_steps");

        if (QaTestingComplete)
            return;

        // Continue the testing schedule even after the current attempt has ended.
        if (autoResetIntervalSeconds > 0f)
        {
            autoResetTimer += Time.deltaTime;
            if (autoResetTimer >= autoResetIntervalSeconds)
            {
                StartEpisode();
                return;
            }
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            StartEpisode();

        DriveQaPlayer();
    }

    // Steps the QA bot at a fixed cadence rather than once per frame, so
    // gameplay timing matches QaPlayerController's StepDurationSeconds assumption.
    private void DriveQaPlayer()
    {
        if (activePlayer == null || !activePlayer.EpisodeActive)
            return;

        qaStepTimer += Time.deltaTime;
        if (qaStepTimer < qaStepIntervalSeconds)
            return;

        qaStepTimer = 0f;
        ModelSensorPackage sensors = activeSensors;
        sensors.Capture();
        QaAction action = qaPolicyRunner.Decide(activePlayer.Observe(), sensors.ReadSnapshot());
        activePlayer.ApplyAction(action);
        sensors.Capture();
    }

    [ContextMenu("Start Episode")]
    public void StartEpisode()
    {
        if (remoteControlActive || QaTestingComplete)
            return;

        int seed = Random.Range(0, int.MaxValue);
        StartPlayerEpisode(seed);
    }

    // Generates a seeded dungeon and initializes its player for local play or external training.
    public bool StartPlayerEpisode(int seed)
    {
        if (QaTestingComplete)
            return false;

        PcgGenerationParameters parameters =
            policyRunner.Decide(seed);

        if (!GenerateDungeon(parameters))
        {
            if (!QaTestingComplete)
                Debug.Log("Dungeon generation failed for seed " + seed);
            return false;
        }

        Debug.Log("Dungeon accepted (seed " + seed + ", policy rooms " + parameters.RoomCount
            + ", generated rooms " + GeneratedRoomCount + ")");

        activeParameters = parameters;
        activePlayer = SpawnOrReuseQaPlayer();
        activeSensors = activePlayer.GetComponent<ModelSensorPackage>();
        activePlayer.BeginTest(dungeonGenerator);
        Physics.SyncTransforms();
        activeSensors.BeginAttempt();
        activePlayer.EpisodeEnded += OnPlayerEpisodeEnded;
        PlayerEpisodeEndReason = "";
        // QA reporting is opt-in: developers attach the adapter in the scenes that record it.
        qaAdapter = GetComponent<DungeonQaAdapter>();
        if (qaAdapter != null)
            qaAdapter.StartUnit("dungeon-" + seed);
        qaPolicyRunner.BeginEpisode(seed);
        qaStepTimer = 0f;
        return true;
    }

    private void OnPlayerEpisodeEnded()
    {
        if (activePlayer.Metrics.Completed)
            EndPlayerEpisode("completed");
        else
            EndPlayerEpisode("player_died");
    }

    // Capture the final player state before regeneration can destroy its components.
    public void EndPlayerEpisode(string reason)
    {
        if (activePlayer == null)
            return;

        PlayerEpisodeEndReason = reason;
        activeSensors.EndAttempt();
        if (qaAdapter != null)
            qaAdapter.EndUnit();
        activePlayer.EpisodeEnded -= OnPlayerEpisodeEnded;
        activePlayer = null;
        activeSensors = null;
        completedQaRuns++;
        if (QaTestingComplete)
            Debug.Log("QA testing complete: " + completedQaRuns + " attempts.", this);
    }

    // Training uses the same generation path without spawning or stepping a QA player.
    public bool GenerateDungeon(PcgGenerationParameters parameters)
    {
        EndPlayerEpisode("reset");
        autoResetTimer = 0f;
        if (QaTestingComplete)
            return false;
        if (!dungeonGenerator.RunProgram(parameters))
        {
            return false;
        }

        return validator.Validate(dungeonGenerator).IsValid;
    }

    // The checked-in generation settings currently ship no player prefab, so
    // this falls back to a capsule placeholder rather than leaving nothing to play with.
    private QaPlayerController SpawnOrReuseQaPlayer()
    {
        GameObject playerObject = dungeonGenerator.Player;
        if (playerObject == null)
        {
            playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Vector3 currentScale = playerObject.transform.localScale;
            currentScale.Scale(new Vector3(0.25f, 1f, 0.25f));
            playerObject.name = "QaPlayer (Placeholder)";
            playerObject.transform.SetParent(dungeonGenerator.transform);
            playerObject.transform.position = dungeonGenerator.PlayerSpawnRoom.transform.position + Vector3.up;
            DestroyImmediate(playerObject.GetComponent<CapsuleCollider>());
            playerObject.AddComponent<CharacterController>();
        }

        QaPlayerController qaPlayer = playerObject.GetComponent<QaPlayerController>();
        if (qaPlayer == null)
            qaPlayer = playerObject.AddComponent<QaPlayerController>();
        if (playerObject.GetComponent<ModelSensorPackage>() == null)
            playerObject.AddComponent<ModelSensorPackage>();
        return qaPlayer;
    }
}
