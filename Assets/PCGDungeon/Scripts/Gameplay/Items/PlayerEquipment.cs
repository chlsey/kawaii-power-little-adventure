using System.Collections.Generic;
using UnityEngine;

// The player's weapon and shield slots plus any timed speed powerups. QaPlayerController
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

    public float AttackCooldown { get => Weapon != null ? Weapon.CooldownSeconds : 0f; }
    public float Defense { get => Shield != null ? Shield.Defense : 0f; }

    // Product of all running speed powerups; 1 when none are active.
    public float SpeedMultiplier
    {
        get
        {
            float multiplier = 1f;
            foreach (ActivePowerup active in activePowerups)
                multiplier *= active.Definition.SpeedMultiplier;
            return multiplier;
        }
    }

    // Restores the starting loadout at the start of each episode.
    public void ResetLoadout()
    {
        Weapon = startingWeapon;
        Shield = startingShield;
        activePowerups.Clear();
    }

    // Equips a weapon or shield, or starts (or restarts) a speed powerup.
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
            case PowerupDefinition powerup:
                ActivePowerup running = activePowerups.Find(active => active.Definition == powerup);
                if (running != null)
                    running.RemainingSeconds = powerup.DurationSeconds;
                else
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

    // One line per running powerup, for the HUD.
    public IEnumerable<string> DescribeActivePowerups()
    {
        foreach (ActivePowerup active in activePowerups)
            yield return $"{active.Definition.DisplayName} x{active.Definition.SpeedMultiplier:0.#} speed, {active.RemainingSeconds:0.#}s";
    }

    public float AttackDamage(float baseDamage)
    {
        return baseDamage + (Weapon != null ? Weapon.AttackBonus : 0f);
    }

    public float AttackRange(float baseRange)
    {
        return Weapon != null ? Weapon.Range : baseRange;
    }

    public float MoveSpeed(float baseSpeed)
    {
        return baseSpeed * SpeedMultiplier;
    }

    public float DamageTaken(float amount)
    {
        return Mathf.Max(0f, amount - Defense);
    }
}
