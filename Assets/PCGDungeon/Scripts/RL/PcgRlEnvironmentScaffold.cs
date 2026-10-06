using UnityEngine;

/// <summary>
/// Use case: record PCG training placeholders. Inputs: seed and observation/action sizes.
/// Outputs: those values.
/// </summary>
[AddComponentMenu("Reinforcement Learning/PCG Environment (Scaffold)")]
[DisallowMultipleComponent]
public sealed class PcgRlEnvironmentScaffold : MonoBehaviour
{
    [Header("Scaffold Only - Not Connected")]
    [SerializeField] private int seed = 0;
    [SerializeField] private int observationSize = 0;
    [SerializeField] private int actionSize = 0;

    public int Seed => seed;

    public int ObservationSize => observationSize;

    public int ActionSize => actionSize;
}
