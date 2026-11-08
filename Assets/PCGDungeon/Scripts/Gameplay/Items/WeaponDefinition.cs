using UnityEngine;

[CreateAssetMenu(fileName = "Weapon", menuName = "PCG Dungeon/Items/Weapon")]
public sealed class WeaponDefinition : ItemDefinition
{
    [SerializeField] private float damage = 2f;
    [SerializeField] private float range = 1.25f;
    [SerializeField] private float cooldownSeconds = 0.5f;

    public float Damage { get => damage; }
    public float Range { get => range; }
    public float CooldownSeconds { get => cooldownSeconds; }
}
