using Kcccr.UnityReinforcementLearning;
using UnityEngine;

/// <summary>Rewards each generated dungeon against its requested room count and optional regular-room dimensions.</summary>
[AddComponentMenu("Reinforcement Learning/PCG Environment")]
[DisallowMultipleComponent]
public sealed class PcgRlEnvironment : RlEnvironmentBehaviour
{
    [SerializeField] private PcgDungeonManager dungeonManager;
    [SerializeField] private bool trainRoomSize;

    private int seed;
    private int targetRoomCount;
    private Vector3Int layoutTarget;
    private bool generationSucceeded;

    public override int ObservationSize
    {
        get
        {
            if (trainRoomSize)
                return 3;
            return 1;
        }
    }
    public override int ActionSize => ObservationSize;

    private void OnEnable()
    {
        dungeonManager.RemoteControlActive = true;
        Application.runInBackground = true;
    }

    private void OnDisable()
    {
        dungeonManager.RemoteControlActive = false;
    }

    public override void ResetEpisode(int episodeSeed)
    {
        seed = episodeSeed;
        targetRoomCount = PcgPolicyMapping.MinRooms + seed % PcgPolicyMapping.RoomCountChoices;
        layoutTarget = PcgLayoutPolicyMapping.TargetForSeed(seed);
        generationSucceeded = false;
    }

    public override float[] CollectObservation()
    {
        if (trainRoomSize)
            return PcgLayoutPolicyMapping.Observe(layoutTarget);
        return PcgPolicyMapping.Observe(targetRoomCount);
    }

    public override void ApplyAction(float[] action)
    {
        PcgGenerationParameters parameters;
        if (trainRoomSize)
        {
            parameters = PcgLayoutPolicyMapping.Parameters(seed, action, dungeonManager.GenerationSettings);
        }
        else
        {
            parameters = PcgPolicyMapping.Parameters(
                seed, PcgPolicyMapping.DecodeRoomCount(action[0]), dungeonManager.GenerationSettings);
        }
        generationSucceeded = dungeonManager.GenerateDungeon(parameters);
    }

    public override bool IsActionRunning()
    {
        return false;
    }

    public override float CalculateReward(out bool isDone)
    {
        isDone = true;
        if (!generationSucceeded)
        {
            return -1f;
        }

        // Measure the generated rooms rather than rewarding the requested parameter.
        float countError = Mathf.Abs(dungeonManager.GeneratedRoomCount - targetRoomCount)
            / (float)(PcgPolicyMapping.MaxRooms - PcgPolicyMapping.MinRooms);
        if (trainRoomSize)
        {
            Vector2 size = dungeonManager.GeneratedRegularRoomSize;
            float widthError = Mathf.Abs(size.x - layoutTarget.y)
                / (PcgLayoutPolicyMapping.MaxRoomSize - PcgLayoutPolicyMapping.MinRoomSize);
            float heightError = Mathf.Abs(size.y - layoutTarget.z)
                / (PcgLayoutPolicyMapping.MaxRoomSize - PcgLayoutPolicyMapping.MinRoomSize);
            return 1f - (countError + widthError + heightError) / 3f;
        }
        return 1f - countError;
    }
}
