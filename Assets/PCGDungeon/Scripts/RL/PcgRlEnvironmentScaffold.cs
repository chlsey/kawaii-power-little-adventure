using UnityEngine;

/// <summary>
/// Holds editor-configured placeholders for a future PCG training environment.
/// It records a seed and space sizes but does not yet connect dungeon generation to the training bridge.
/// </summary>
[AddComponentMenu("Reinforcement Learning/PCG Environment (Scaffold)")]
[DisallowMultipleComponent]
public sealed class PcgRlEnvironmentScaffold : MonoBehaviour
{
    [Header("Scaffold Only - Not Connected")]
    [SerializeField] private int seed = 0;
    [SerializeField] private int observationSize = 0;
    [SerializeField] private int actionSize = 0;

    /// <summary>
    /// Provides the seed reserved for future PCG episode resets.
    /// </summary>
    public int Seed => seed;

    /// <summary>
    /// Provides the planned number of state values a PCG environment would send to the trainer.
    /// </summary>
    public int ObservationSize => observationSize;

    /// <summary>
    /// Provides the planned number of values a PCG environment would accept from the trainer.
    /// </summary>
    public int ActionSize => actionSize;
}
