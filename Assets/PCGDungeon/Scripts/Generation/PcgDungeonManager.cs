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

    private QaPlayerController activePlayer;
    private float qaStepTimer;
    private bool remoteControlActive;

    // The spawned QA player for the current episode, ready for ApplyAction calls. Null until a dungeon is accepted.
    public QaPlayerController ActivePlayer => activePlayer;

    // Set by QaTcpHost while a remote bot session owns episode control, so the
    // local R-key/dummy-policy loop stands down instead of fighting it for the
    // same generator/player. Clears the local player reference so this loop
    // can't later touch an object the remote session already destroyed.
    public bool RemoteControlActive
    {
        get => remoteControlActive;
        set
        {
            remoteControlActive = value;
            if (value)
                activePlayer = null;
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
        QaAction action = qaPolicyRunner.Decide();
        activePlayer.ApplyAction(action);
    }

    [ContextMenu("Start Episode")]
    public void StartEpisode()
    {
        if (remoteControlActive)
            return;

        int seed = Random.Range(0, int.MaxValue);

        PcgGenerationParameters parameters =
            policyRunner.Decide(seed);

        if (!dungeonGenerator.RunProgram(parameters))
        {
            Debug.Log("Dungeon generation failed for seed " + seed);
            return;
        }

        ValidationResult validationResult =
            validator.Validate(dungeonGenerator);

        if (!validationResult.IsValid)
        {
            Debug.Log("Dungeon rejected: " + validationResult.Message);
            return;
        }

        Debug.Log("Dungeon accepted (seed " + seed + ")");

        activePlayer = SpawnOrReuseQaPlayer();
        activePlayer.BeginTest(dungeonGenerator);
        qaPolicyRunner.BeginEpisode(seed);
        qaStepTimer = 0f;
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
        return qaPlayer != null ? qaPlayer : playerObject.AddComponent<QaPlayerController>();
    }
}
