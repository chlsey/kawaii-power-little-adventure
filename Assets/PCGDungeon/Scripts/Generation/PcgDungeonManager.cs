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

    // Tab toggles between the QA policy and keyboard play (WASD move, Space
    // attack, E interact). Both drive the same QaPlayerController.ApplyAction.
    [SerializeField] private bool manualControl;
    [SerializeField] private float manualStepIntervalSeconds = 0.1f;

    private QaPlayerController activePlayer;
    private float qaStepTimer;
    private bool remoteControlActive;
    private bool manualAttackQueued;
    private bool manualInteractQueued;
    private DungeonCamera dungeonCamera;

    // The spawned QA player for the current episode, ready for ApplyAction calls. Null until a dungeon is accepted.
    public QaPlayerController ActivePlayer => activePlayer;
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

    // Set by QaTcpHost while a remote bot session owns episode control, so the
    // local R-key/dummy-policy loop stands down instead of fighting it for the
    // same player. Update()'s own remoteControlActive guard is what actually
    // stops the local loop from touching the player; this does not clear
    // activePlayer, since QaTcpHost hands that exact existing player over to
    // the remote session rather than spawning its own.
    public bool RemoteControlActive
    {
        get => remoteControlActive;
        set => remoteControlActive = value;
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

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            StartEpisode();

        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
        {
            manualControl = !manualControl;
            qaStepTimer = 0f;
            Debug.Log(manualControl ? "Manual control (WASD, Space attack, E interact)" : "QA policy control");
        }

        DriveQaPlayer();
    }

    // Steps the QA bot at a fixed cadence rather than once per frame, so
    // gameplay timing matches QaPlayerController's StepDurationSeconds assumption.
    private void DriveQaPlayer()
    {
        if (activePlayer == null || !activePlayer.EpisodeActive)
            return;

        Keyboard keyboard = Keyboard.current;
        if (manualControl && keyboard != null)
        {
            // Latch presses between steps so a quick tap isn't missed.
            manualAttackQueued |= keyboard.spaceKey.wasPressedThisFrame;
            manualInteractQueued |= keyboard.eKey.wasPressedThisFrame;
        }

        qaStepTimer += Time.deltaTime;
        if (qaStepTimer < (manualControl ? manualStepIntervalSeconds : qaStepIntervalSeconds))
            return;

        qaStepTimer = 0f;
        QaAction action = manualControl ? ReadManualAction(keyboard) : qaPolicyRunner.Decide();
        activePlayer.ApplyAction(action);
    }

    private QaAction ReadManualAction(Keyboard keyboard)
    {
        if (keyboard == null)
            return QaAction.Noop;

        float right = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);

        // W moves up the screen: rotate input by the follow camera's fixed yaw when there is one.
        Vector3 move = new Vector3(right, 0f, forward);
        if (dungeonCamera == null && Camera.main != null)
            dungeonCamera = Camera.main.GetComponent<DungeonCamera>();
        if (dungeonCamera != null)
            move = dungeonCamera.PlanarRight * right + dungeonCamera.PlanarForward * forward;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        float moveX = move.x;
        float moveY = move.z;
        bool attack = manualAttackQueued || keyboard.spaceKey.isPressed;
        bool interact = manualInteractQueued;

        manualAttackQueued = false;
        manualInteractQueued = false;
        return new QaAction(moveX, moveY, attack, interact, 0);
    }

    [ContextMenu("Start Episode")]
    public void StartEpisode()
    {
        if (remoteControlActive)
            return;

        int seed = Random.Range(0, int.MaxValue);

        PcgGenerationParameters parameters =
            policyRunner.Decide(seed);

        if (!GenerateDungeon(parameters))
        {
            Debug.Log("Dungeon generation failed for seed " + seed);
            return;
        }

        Debug.Log("Dungeon accepted (seed " + seed + ", policy rooms " + parameters.RoomCount
            + ", generated rooms " + GeneratedRoomCount + ")");

        activePlayer = SpawnOrReuseQaPlayer();
        activePlayer.BeginTest();
        qaPolicyRunner.BeginEpisode(seed);
        qaStepTimer = 0f;
    }

    // Training uses the same generation path without spawning or stepping a QA player.
    public bool GenerateDungeon(PcgGenerationParameters parameters)
    {
        activePlayer = null;
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
            playerObject.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            playerObject.name = "QaPlayer (Placeholder)";
            playerObject.transform.SetParent(dungeonGenerator.transform);
            playerObject.transform.position = dungeonGenerator.PlayerSpawnRoom.transform.position + Vector3.up;
            DestroyImmediate(playerObject.GetComponent<CapsuleCollider>());
            // CharacterController's radius/height are independent of transform scale and
            // default to 0.5/2 - exactly a full 1-unit grid tile with no clearance at all.
            CharacterController controller = playerObject.AddComponent<CharacterController>();
            controller.radius = 0.3f;
            controller.height = 1.6f;
            controller.center = new Vector3(0f, 0.8f, 0f);
        }

        QaPlayerController qaPlayer = playerObject.GetComponent<QaPlayerController>();
        return qaPlayer != null ? qaPlayer : playerObject.AddComponent<QaPlayerController>();
    }
}
