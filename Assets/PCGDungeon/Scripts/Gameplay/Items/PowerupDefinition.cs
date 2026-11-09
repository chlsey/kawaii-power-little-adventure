using UnityEngine;

public enum PowerupEffect
{
    Heal,
    DamageBoost,
    SpeedBoost,
    DamageReduction
}

[CreateAssetMenu(fileName = "Powerup", menuName = "PCG Dungeon/Items/Powerup")]
public sealed class PowerupDefinition : ItemDefinition
{
    [SerializeField] private PowerupEffect effect = PowerupEffect.Heal;

    // Heal: health restored. DamageBoost/SpeedBoost: multiplier (1.5 = +50%).
    // DamageReduction: fraction of incoming damage removed (0..1).
    [SerializeField] private float amount = 3f;

    // Ignored for Heal, which is instant.
    [SerializeField] private float durationSeconds = 10f;

    public PowerupEffect Effect { get => effect; }
    public float Amount { get => amount; }
    public float DurationSeconds { get => durationSeconds; }
    public bool IsInstant { get => effect == PowerupEffect.Heal || durationSeconds <= 0f; }
}
