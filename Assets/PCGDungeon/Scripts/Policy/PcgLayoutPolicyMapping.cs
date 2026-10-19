using UnityEngine;

// Shares the three-control model contract between training and deployed inference.
public static class PcgLayoutPolicyMapping
{
    public const int MinRoomSize = 8;
    public const int MaxRoomSize = 12;
    public const int RoomSizeChoices = MaxRoomSize - MinRoomSize + 1;

    public static Vector3Int TargetForSeed(int seed)
    {
        return new Vector3Int(
            PcgPolicyMapping.MinRooms + seed % PcgPolicyMapping.RoomCountChoices,
            MinRoomSize + seed / PcgPolicyMapping.RoomCountChoices % RoomSizeChoices,
            MinRoomSize + seed / (PcgPolicyMapping.RoomCountChoices * RoomSizeChoices) % RoomSizeChoices);
    }

    public static float[] Observe(Vector3Int target)
    {
        return new[]
        {
            PcgPolicyMapping.Observe(target.x)[0],
            2f * (target.y - MinRoomSize) / (MaxRoomSize - MinRoomSize) - 1f,
            2f * (target.z - MinRoomSize) / (MaxRoomSize - MinRoomSize) - 1f,
        };
    }

    public static PcgGenerationParameters Parameters(int seed, float[] action, GenerationSettings settings)
    {
        var parameters = PcgPolicyMapping.Parameters(seed, PcgPolicyMapping.DecodeRoomCount(action[0]), settings);
        parameters.MinRoomWidth = DecodeSize(action[1]);
        parameters.MinRoomHeight = DecodeSize(action[2]);
        // Unity's integer Random.Range excludes its upper bound, giving every regular room the chosen size.
        parameters.MaxRoomWidth = parameters.MinRoomWidth + 1;
        parameters.MaxRoomHeight = parameters.MinRoomHeight + 1;
        return parameters;
    }

    public static int DecodeSize(float action)
    {
        return MinRoomSize + Mathf.RoundToInt((Mathf.Clamp(action, -1f, 1f) + 1f)
            * (MaxRoomSize - MinRoomSize) / 2f);
    }
}
