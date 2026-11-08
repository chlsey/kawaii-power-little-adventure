using System.Collections.Generic;
using UnityEngine;

// The player's weapon and shield slots plus any timed powerups. QaPlayerController
// asks this for its combat and movement stats; with nothing equipped every stat
// falls back to the controller's own values, so trained models see no change.
public sealed class PlayerEquipment : MonoBehaviour
{
    private sealed class ActivePowerup
    {
        public PowerupDefinition Definition;
        public float RemainingSeconds;
    }

    [SerializeField] private WeaponDefinition startingWeapon;
    [SerializeField] private ShieldDefinition startingShield;

    private readonly List<ActivePowerup> activePowerups = new List<ActivePowerup>();

    public WeaponDefinition Weapon { get; private set; }
    public ShieldDefinition Shield { get; private set; }

    // Restores the starting loadout at the start of each episode.
    public void ResetLoadout()
    {
        Weapon = startingWeapon;
        Shield = startingShield;
        activePowerups.Clear();
    }

    // Equips a weapon or shield, or starts a timed powerup. Instant powerups
    // (heals) are applied by the controller, which owns health.
    public void Equip(ItemDefinition item)
    {
        switch (item)
        {
            case WeaponDefinition weapon:
                Weapon = weapon;
                break;
            case ShieldDefinition shield:
                Shield = shield;
                break;
            case PowerupDefinition powerup when !powerup.IsInstant:
                activePowerups.Add(new ActivePowerup { Definition = powerup, RemainingSeconds = powerup.DurationSeconds });
                break;
        }
    }

    // Advanced by the controller once per action step, keeping durations deterministic.
    public void Tick(float seconds)
    {
        for (int i = activePowerups.Count - 1; i >= 0; i--)
        {
            activePowerups[i].RemainingSeconds -= seconds;
            if (activePowerups[i].RemainingSeconds <= 0f)
                activePowerups.RemoveAt(i);
        }
    }

    public float AttackDamage(float baseDamage)
    {
        float damage = Weapon != null ? Weapon.Damage : baseDamage;
        return damage * Multiplier(PowerupEffect.DamageBoost);
    }

    public float AttackRange(float baseRange)
    {
        return Weapon != null ? Weapon.Range : baseRange;
    }

    public float AttackCooldown { get => Weapon != null ? Weapon.CooldownSeconds : 0f; }

    public float MoveSpeed(float baseSpeed)
    {
        return baseSpeed * Multiplier(PowerupEffect.SpeedBoost);
    }

    // Shield and damage-reduction powerups stack multiplicatively.
    public float DamageTaken(float amount)
    {
        float kept = Shield != null ? 1f - Shield.DamageReduction : 1f;
        foreach (ActivePowerup active in activePowerups)
        {
            if (active.Definition.Effect == PowerupEffect.DamageReduction)
                kept *= 1f - Mathf.Clamp01(active.Definition.Amount);
        }

        return amount * kept;
    }

    private float Multiplier(PowerupEffect effect)
    {
        float multiplier = 1f;
        foreach (ActivePowerup active in activePowerups)
        {
            if (active.Definition.Effect == effect)
                multiplier *= active.Definition.Amount;
        }

        return multiplier;
    }
}
