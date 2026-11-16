using UnityEngine;

// A timed speed boost. Picking up the same powerup again restarts its timer instead of stacking.
[CreateAssetMenu(fileName = "Powerup", menuName = "PCG Dungeon/Items/Powerup")]
public sealed class PowerupDefinition : ItemDefinition
{
    [SerializeField, Min(1f)] private float speedMultiplier = 1.5f;
    [SerializeField, Min(0.1f)] private float durationSeconds = 5f;

    public float SpeedMultiplier { get => speedMultiplier; }
    public float DurationSeconds { get => durationSeconds; }
}
