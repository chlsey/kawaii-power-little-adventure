using Kcccr.UnityReinforcementLearning;
using UnityEngine;

public class PcgPolicyRunner : MonoBehaviour
{
    public enum PolicyMode { Random, Trained }
    public enum PolicyParameters { RoomCount, RoomCountAndSize }

    [SerializeField] private PolicyMode mode;
    [SerializeField] private PolicyParameters parametersMode;
    [SerializeField] private GenerationSettings generationSettings;
    [SerializeField] private RlModelRunner modelRunner;
    [SerializeField, Range(PcgPolicyMapping.MinRooms, PcgPolicyMapping.MaxRooms)]
    private int targetRoomCount = 12;
    [SerializeField, Range(PcgLayoutPolicyMapping.MinRoomSize, PcgLayoutPolicyMapping.MaxRoomSize)]
    private int targetRoomWidth = 10;
    [SerializeField, Range(PcgLayoutPolicyMapping.MinRoomSize, PcgLayoutPolicyMapping.MaxRoomSize)]
    private int targetRoomHeight = 10;

    public PcgGenerationParameters Parameters;
    public bool UsesLayoutPolicy => parametersMode == PolicyParameters.RoomCountAndSize;

    public int TargetRoomCount
    {
        get => targetRoomCount;
        set => targetRoomCount = Mathf.Clamp(value, PcgPolicyMapping.MinRooms, PcgPolicyMapping.MaxRooms);
    }

    public int TargetRoomWidth
    {
        get => targetRoomWidth;
        set => targetRoomWidth = Mathf.Clamp(value, PcgLayoutPolicyMapping.MinRoomSize, PcgLayoutPolicyMapping.MaxRoomSize);
    }

    public int TargetRoomHeight
    {
        get => targetRoomHeight;
        set => targetRoomHeight = Mathf.Clamp(value, PcgLayoutPolicyMapping.MinRoomSize, PcgLayoutPolicyMapping.MaxRoomSize);
    }

    public PcgGenerationParameters Decide(int seed)
    {
        if (UsesLayoutPolicy)
        {
            float[] action;
            if (mode == PolicyMode.Trained)
            {
                action = modelRunner.Predict(PcgLayoutPolicyMapping.Observe(
                    new Vector3Int(targetRoomCount, targetRoomWidth, targetRoomHeight)));
            }
            else
            {
                Random.InitState(seed);
                var target = new Vector3Int(
                    Random.Range(PcgPolicyMapping.MinRooms, PcgPolicyMapping.MaxRooms + 1),
                    Random.Range(PcgLayoutPolicyMapping.MinRoomSize, PcgLayoutPolicyMapping.MaxRoomSize + 1),
                    Random.Range(PcgLayoutPolicyMapping.MinRoomSize, PcgLayoutPolicyMapping.MaxRoomSize + 1));
                action = PcgLayoutPolicyMapping.Observe(target);
            }

            Parameters = PcgLayoutPolicyMapping.Parameters(seed, action, generationSettings);
            return Parameters;
        }

        int roomCount;
        if (mode == PolicyMode.Trained)
        {
            float[] action = modelRunner.Predict(PcgPolicyMapping.Observe(targetRoomCount));
            roomCount = PcgPolicyMapping.DecodeRoomCount(action[0]);
        }
        else
        {
            Random.InitState(seed);
            roomCount = Random.Range(PcgPolicyMapping.MinRooms, PcgPolicyMapping.MaxRooms + 1);
        }

        Parameters = PcgPolicyMapping.Parameters(seed, roomCount, generationSettings);
        return Parameters;
    }
}
