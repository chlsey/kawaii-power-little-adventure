using Kcccr.UnityReinforcementLearning;
using UnityEngine;

/// <summary>Trains the existing QA player to find a randomly placed exit in one fixed room.</summary>
[AddComponentMenu("Reinforcement Learning/QA Single Room Environment")]
[DisallowMultipleComponent]
public sealed class QaSingleRoomRlEnvironment : RlEnvironmentBehaviour
{
    [SerializeField] private BoxCollider roomFloor;
    [SerializeField] private QaPlayerController player;
    [SerializeField] private Transform exit;
    [SerializeField] private float completionReward = 50f;
    [SerializeField] private float timeCostPerSecond = 0.01f;
    [SerializeField] private float visibleExitProgressRewardPerUnit = 0.1f;
    [SerializeField, Min(0f)] private float viewDiscoveryRewardPerCell = 0.001f;
    [SerializeField, Min(0f)] private float maxViewDiscoveryRewardPerAction = 0.02f;
    [SerializeField, Min(0f)] private float maxViewDiscoveryRewardPerAttempt = 2f;
    [SerializeField, Min(0f)] private float stuckDistanceThreshold = 0.05f;
    [SerializeField, Min(1)] private int stuckStepThreshold = 20;
    [SerializeField, Min(0f)] private float stuckPenaltyPerSecond = 0.1f;
    [SerializeField] private float maxEpisodeSeconds = 100f;

    private CharacterController character;
    private bool attemptEnded;
    private ModelSensorPackage sensors;
    private bool hasSensedExit;
    private Vector3 sensedExitPoint;
    private float closestSensedExitDistance;
    private bool exitVisibleForAction;
    private float actionEndTime;
    private int viewDiscoveryBaseline;
    private float explorationRewardEarned;
    private Vector3 stuckAnchorPosition;
    private int stationaryMovementSteps;
    private bool movementRequested;
    private float stuckPenaltyTotal;
    private int stuckPenaltySteps;

    public override int ObservationSize => QaPlayerPolicyMapping.ObservationSize;
    public override int ActionSize => QaPlayerPolicyMapping.ActionSize;

    public QaPlayerController Player => player;
    public ModelSensorPackage Sensors => sensors;
    public int ActiveSeed { get; private set; }
    public Vector3 SpawnPosition { get; private set; }
    public Vector3 ExitPosition => exit.position;

    private void Awake()
    {
        character = player.GetComponent<CharacterController>();
        sensors = player.GetComponent<ModelSensorPackage>();
        Application.runInBackground = true;
    }

    public override void ResetEpisode(int seed)
    {
        // Reuse the room and actors; only their seeded positions and tracking reset.
        sensors.EndAttempt();
        player.EpisodeEnded -= OnPlayerEnded;
        var random = new System.Random(seed);
        Vector3 spawn = SampleFloorPosition(random);
        Vector3 goal;
        do
        {
            goal = SampleFloorPosition(random);
        }
        while (Vector3.Distance(spawn, goal) < 3f);

        character.enabled = false;
        exit.gameObject.SetActive(false);
        player.transform.SetPositionAndRotation(spawn, Quaternion.identity);
        exit.position = goal;
        character.enabled = true;
        exit.gameObject.SetActive(true);
        Physics.SyncTransforms();
        player.BeginTest(null);
        player.EpisodeEnded += OnPlayerEnded;
        sensors.BeginAttempt();
        ActiveSeed = seed;
        SpawnPosition = spawn;
        attemptEnded = false;
        hasSensedExit = false;
        exitVisibleForAction = false;
        sensedExitPoint = Vector3.zero;
        closestSensedExitDistance = 0f;
        viewDiscoveryBaseline = sensors.ViewedOpenCellCount;
        explorationRewardEarned = 0f;
        stuckAnchorPosition = player.transform.position;
        stationaryMovementSteps = 0;
        movementRequested = false;
        stuckPenaltyTotal = 0f;
        stuckPenaltySteps = 0;
        actionEndTime = Time.fixedTime;
    }

    private Vector3 SampleFloorPosition(System.Random random)
    {
        Bounds bounds = roomFloor.bounds;
        float x = Mathf.Lerp(bounds.min.x + 1.5f, bounds.max.x - 1.5f, (float)random.NextDouble());
        float z = Mathf.Lerp(bounds.min.z + 1.5f, bounds.max.z - 1.5f, (float)random.NextDouble());
        return new Vector3(x, bounds.max.y + 0.01f, z);
    }

    private void OnPlayerEnded()
    {
        attemptEnded = true;
        sensors.EndAttempt();
    }

    public override float[] CollectObservation()
    {
        return QaPlayerPolicyMapping.Observe(player.Observe(), sensors.ReadSnapshot());
    }

    public override void ApplyAction(float[] action)
    {
        movementRequested = false;
        exitVisibleForAction = false;
        sensors.Capture();
        viewDiscoveryBaseline = sensors.ViewedOpenCellCount;
        // A death or exit callback can end the episode between trainer requests.
        if (!attemptEnded && player.EpisodeActive)
        {
            SenseExitBeforeAction();
            QaAction command = QaPlayerPolicyMapping.Decode(action);
            movementRequested = new Vector2(command.MoveX, command.MoveY).sqrMagnitude > 0.0001f;
            player.ApplyAction(command);
        }
        actionEndTime = Time.fixedTime + QaPlayerController.StepDurationSeconds;
    }

    public override bool IsActionRunning()
    {
        // Let physics deliver exit/death callbacks before returning the step to Python.
        return Time.fixedTime < actionEndTime;
    }

    public override float CalculateReward(out bool isDone)
    {
        // The host requests reward before observation, so map the completed action here.
        sensors.Capture();
        int newViewedCells = sensors.ViewedOpenCellCount - viewDiscoveryBaseline;
        viewDiscoveryBaseline = sensors.ViewedOpenCellCount;
        QaEpisodeMetrics metrics = player.Metrics;
        float reward = -timeCostPerSecond * QaPlayerController.StepDurationSeconds;
        if (hasSensedExit)
        {
            float distance = DistanceToSensedExit();
            if (exitVisibleForAction && distance < closestSensedExitDistance)
                reward += (closestSensedExitDistance - distance) * visibleExitProgressRewardPerUnit;

            // Consume hidden progress too, so visibility changes cannot pay for old movement.
            closestSensedExitDistance = Mathf.Min(closestSensedExitDistance, distance);
        }

        if (!metrics.Completed && !exitVisibleForAction && !sensors.ExitVisible)
        {
            float explorationReward = Mathf.Min(newViewedCells * viewDiscoveryRewardPerCell,
                maxViewDiscoveryRewardPerAction,
                Mathf.Max(0f, maxViewDiscoveryRewardPerAttempt - explorationRewardEarned));
            reward += explorationReward;
            explorationRewardEarned += explorationReward;
        }

        if (!metrics.Completed && !metrics.PlayerDied)
            reward -= CalculateStuckPenalty();

        isDone = metrics.Completed || metrics.PlayerDied || metrics.ElapsedSeconds >= maxEpisodeSeconds;
        if (metrics.Completed)
        {
            reward += completionReward;
        }
        if (isDone && !metrics.Completed && !metrics.PlayerDied)
        {
            attemptEnded = true;
            sensors.EndAttempt();
        }
        return reward;
    }

    private float CalculateStuckPenalty()
    {
        Vector3 displacement = player.transform.position - stuckAnchorPosition;
        displacement.y = 0f;
        // Track continuous blocked movement; sliding away or stopping resets the timer.
        if (!movementRequested || displacement.sqrMagnitude >= stuckDistanceThreshold * stuckDistanceThreshold)
        {
            stuckAnchorPosition = player.transform.position;
            stationaryMovementSteps = 0;
            return 0f;
        }

        stationaryMovementSteps++;
        if (stationaryMovementSteps < stuckStepThreshold)
            return 0f;

        float penalty = stuckPenaltyPerSecond * QaPlayerController.StepDurationSeconds;
        stuckPenaltyTotal += penalty;
        stuckPenaltySteps++;
        return penalty;
    }

    private void SenseExitBeforeAction()
    {
        QaRayObservation[] rays = sensors.Rays;
        int nearestExitRay = -1;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < rays.Length; i++)
        {
            if (rays[i].HitId == QaObjectType.LevelEnd && rays[i].Distance01 < nearestDistance)
            {
                nearestExitRay = i;
                nearestDistance = rays[i].Distance01;
            }
        }

        exitVisibleForAction = nearestExitRay >= 0;
        if (exitVisibleForAction && !hasSensedExit)
        {
            // Keep the first sensed point fixed; changing rays must not manufacture progress.
            sensedExitPoint = sensors.Traces[nearestExitRay].Endpoint;
            closestSensedExitDistance = DistanceToSensedExit();
            hasSensedExit = true;
        }

        if (hasSensedExit)
            closestSensedExitDistance = Mathf.Min(closestSensedExitDistance, DistanceToSensedExit());
    }

    private float DistanceToSensedExit()
    {
        Vector3 difference = player.transform.position - sensedExitPoint;
        difference.y = 0f;
        return difference.magnitude;
    }
}
