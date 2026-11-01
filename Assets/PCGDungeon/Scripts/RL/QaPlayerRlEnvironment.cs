using System;
using Kcccr.UnityReinforcementLearning;
using UnityEngine;

/// <summary>Trains the QA player to reach the exit of a newly generated dungeon each episode.</summary>
[AddComponentMenu("Reinforcement Learning/QA Player Environment")]
[DisallowMultipleComponent]
public sealed class QaPlayerRlEnvironment : RlEnvironmentBehaviour
{
    [SerializeField] private PcgDungeonManager dungeonManager;
    [SerializeField] private float completionReward = 50f;
    [SerializeField] private float timeCostPerSecond = 0.01f;
    [SerializeField] private float visibleExitProgressRewardPerUnit = 0.1f;
    [SerializeField, Min(0f)] private float viewDiscoveryRewardPerCell = 0.001f;
    [SerializeField, Min(0f)] private float maxViewDiscoveryRewardPerAction = 0.02f;
    [SerializeField, Min(0f)] private float maxViewDiscoveryRewardPerAttempt = 2f;
    [SerializeField] private float maxEpisodeSeconds = 100f;

    private QaPlayerController player;
    private ModelSensorPackage sensors;
    private bool hasSensedExit;
    private Vector3 sensedExitPoint;
    private float closestSensedExitDistance;
    private bool exitVisibleForAction;
    private float actionEndTime;
    private int viewDiscoveryBaseline;
    private float explorationRewardEarned;

    public override int ObservationSize => QaPlayerPolicyMapping.ObservationSize;
    public override int ActionSize => QaPlayerPolicyMapping.ActionSize;

    private void OnEnable()
    {
        dungeonManager.RemoteControlActive = true;
        Application.runInBackground = true;
    }

    private void OnDisable()
    {
        dungeonManager.RemoteControlActive = false;
    }

    public override void ResetEpisode(int seed)
    {
        if (!dungeonManager.StartPlayerEpisode(seed))
        {
            throw new InvalidOperationException("Could not generate a player-training dungeon.");
        }

        player = dungeonManager.ActivePlayer;
        sensors = player.GetComponent<ModelSensorPackage>();
        hasSensedExit = false;
        exitVisibleForAction = false;
        sensedExitPoint = Vector3.zero;
        closestSensedExitDistance = 0f;
        viewDiscoveryBaseline = sensors.ViewedOpenCellCount;
        explorationRewardEarned = 0f;
        actionEndTime = Time.fixedTime;
    }

    public override float[] CollectObservation()
    {
        return QaPlayerPolicyMapping.Observe(player.Observe(), sensors.ReadSnapshot());
    }

    public override void ApplyAction(float[] action)
    {
        exitVisibleForAction = false;
        sensors.Capture();
        viewDiscoveryBaseline = sensors.ViewedOpenCellCount;
        // A death or exit callback can end the episode between trainer requests.
        if (player.EpisodeActive)
        {
            SenseExitBeforeAction();
            player.ApplyAction(QaPlayerPolicyMapping.Decode(action));
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

        isDone = metrics.Completed || metrics.PlayerDied || metrics.ElapsedSeconds >= maxEpisodeSeconds;
        if (metrics.Completed)
        {
            reward += completionReward;
        }
        if (isDone && !metrics.Completed && !metrics.PlayerDied)
        {
            dungeonManager.EndPlayerEpisode("max_duration");
        }
        return reward;
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
