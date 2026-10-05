using UnityEngine;
using UnityEngine.InputSystem;

public class PcgDungeonManager : MonoBehaviour
{
    [SerializeField] private Generator dungeonGenerator;
    [SerializeField] private PcgPolicyRunner policyRunner;
    [SerializeField] private DungeonValidator validator;
    // [SerializeField] private QaPlayerController qaPlayer;

    [SerializeField] private bool generateOnStart = true;

    private void Start()
    {
        if (generateOnStart)
            StartEpisode();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            StartEpisode();
    }

    [ContextMenu("Start Episode")]
    public void StartEpisode()
    {
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
        // qaPlayer.BeginTest(dungeonGenerator);
    }
}
