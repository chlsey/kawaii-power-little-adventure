using Kcccr.UnityReinforcementLearning;
using UnityEngine;

/// <summary>A navigation skill drill: assigned-waypoint arrival ends the drill, not a game objective.</summary>
public sealed class QaWaypointReachRlEnvironment : RlEnvironmentBehaviour
{
    [SerializeField] private BoxCollider roomFloor;
    [SerializeField] private QaPlayerController player;
    [SerializeField] private Transform waypoint;
    [SerializeField] private float minWaypointDistance = 3f;
    [SerializeField] private float maxWaypointDistance = 6f;
    [SerializeField] private float maxEpisodeSeconds = 60f;
    [SerializeField] private float waypointRewardBudget = 5f;
    [SerializeField] private float minimumLocalObjectiveReward = 50f;
    private ModelSensorPackage sensors;
    private CharacterController actor;
    private QaWaypointRewardTracker rewards;
    private float actionEnd;

    public override int ObservationSize => QaWaypointPolicyMapping.ObservationSize;
    public override int ActionSize => QaWaypointPolicyMapping.ActionSize;
    public QaPlayerController Player => player;
    public ModelSensorPackage Sensors => sensors;
    public bool Reached => Distance <= QaNavigationCoordinator.AcceptanceRadius;
    public float Distance => QaNavigationCoordinator.Distance(player.transform.position, waypoint.position);
    public float PositiveRewardTotal => rewards.PositiveRewardTotal;
    public float ArrivalBonus => rewards.CompletionBonus;
    public int Seed { get; private set; }
    public float InitialDistance { get; private set; }
    public Vector3 Target => waypoint.position;

    private void Awake()
    {
        sensors = player.GetComponent<ModelSensorPackage>();
        actor = player.GetComponent<CharacterController>();
        rewards = new QaWaypointRewardTracker(waypointRewardBudget);
    }

    public override void ResetEpisode(int seed)
    {
        if (!(waypointRewardBudget > 0f && waypointRewardBudget < minimumLocalObjectiveReward))
            throw new System.InvalidOperationException("Waypoint budget must be below every active local-objective reward.");
        sensors.EndAttempt();
        var random = new System.Random(seed);
        float angle = (float)(random.NextDouble() * System.Math.PI * 2);
        float distance = Mathf.Lerp(minWaypointDistance, maxWaypointDistance, (float)random.NextDouble());
        actor.enabled = false;
        player.transform.SetPositionAndRotation(new Vector3(0f, roomFloor.bounds.max.y + 0.01f, 0f), Quaternion.identity);
        actor.enabled = true;
        player.BeginTest(null);
        waypoint.position = player.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
        Physics.SyncTransforms();
        sensors.BeginAttempt();
        rewards.Reset();
        rewards.Assign(true, 1, waypoint.position, player.transform.position);
        Seed = seed;
        InitialDistance = Distance;
        actionEnd = Time.fixedTime;
    }

    public override float[] CollectObservation()
    {
        sensors.Capture();
        return QaWaypointPolicyMapping.Observe(player.Observe(), sensors, true, waypoint.position, player.transform.position);
    }

    public override void ApplyAction(float[] action)
    {
        player.ApplyAction(QaLocalMovementPolicyMapping.Decode(action));
        actionEnd = Time.fixedTime + QaPlayerController.StepDurationSeconds;
    }

    public override bool IsActionRunning() => Time.fixedTime < actionEnd;

    public override float CalculateReward(out bool isDone)
    {
        float reward = rewards.Measure(player.transform.position) - 0.001f;
        isDone = Reached || player.Metrics.PlayerDied || player.Metrics.ElapsedSeconds >= maxEpisodeSeconds;
        if (isDone) sensors.EndAttempt();
        // Never call ReportExitReached: drill success is separate from actual gameplay completion.
        return reward;
    }
}
