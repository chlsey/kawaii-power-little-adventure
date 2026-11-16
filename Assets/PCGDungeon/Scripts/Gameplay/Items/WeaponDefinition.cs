using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Weapon", menuName = "PCG Dungeon/Items/Weapon")]
public sealed class WeaponDefinition : ItemDefinition
{
    // Added to the player's base attack while equipped.
    [SerializeField, FormerlySerializedAs("damage")] private float attackBonus = 10f;
    [SerializeField] private float range = 1.25f;
    [SerializeField] private float cooldownSeconds = 0.5f;

    public float AttackBonus { get => attackBonus; }
    public float Range { get => range; }
    public float CooldownSeconds { get => cooldownSeconds; }
}
