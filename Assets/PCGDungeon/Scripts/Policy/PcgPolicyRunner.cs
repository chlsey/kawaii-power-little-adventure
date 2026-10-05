// Interface file between the onnx model file and the procedural generation system.
// This file is responsible for taking the output of the model and converting it 
// into a set of parameters that can be used to generate a dungeon.

using UnityEngine;

public class PcgPolicyRunner : MonoBehaviour
{
    public PcgGenerationParameters Parameters;

    public PcgGenerationParameters Decide(int seed)
    {
        Random.InitState(seed);

        Parameters = new PcgGenerationParameters
        {
            Seed = seed,
            RoomCount = Random.Range(8, 16),
            AreaWidth = Random.Range(90, 121),
            AreaHeight = Random.Range(90, 121),
            MinRoomWidth = Random.Range(5, 8),
            MaxRoomWidth = Random.Range(9, 14),
            MinRoomHeight = Random.Range(5, 8),
            MaxRoomHeight = Random.Range(9, 14),
        };
        return Parameters;

    }
}