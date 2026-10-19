using UnityEngine;

// Keep the policy contract identical in training and deployed inference.
public static class PcgPolicyMapping
{
    public const int MinRooms = 8;
    public const int MaxRooms = 15;
    public const int RoomCountChoices = MaxRooms - MinRooms + 1;

    public static float[] Observe(int targetRoomCount)
    {
        return new[] { 2f * (targetRoomCount - MinRooms) / (MaxRooms - MinRooms) - 1f };
    }

    public static int DecodeRoomCount(float action)
    {
        return MinRooms + Mathf.RoundToInt((Mathf.Clamp(action, -1f, 1f) + 1f)
            * (MaxRooms - MinRooms) / 2f);
    }

    public static PcgGenerationParameters Parameters(int seed, int roomCount, GenerationSettings settings)
    {
        Data fixedLayout = settings.generation;
        return new PcgGenerationParameters
        {
            Seed = seed,
            RoomCount = roomCount,
            AreaWidth = fixedLayout.areaWidth,
            AreaHeight = fixedLayout.areaHeight,
            MinRoomWidth = fixedLayout.minRoomWidth,
            MaxRoomWidth = fixedLayout.maxRoomWidth,
            MinRoomHeight = fixedLayout.minRoomHeight,
            MaxRoomHeight = fixedLayout.maxRoomHeight,
        };
    }
}
