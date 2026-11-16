using UnityEngine;

[CreateAssetMenu(fileName = "Shield", menuName = "PCG Dungeon/Items/Shield")]
public sealed class ShieldDefinition : ItemDefinition
{
    // Subtracted from every hit the player takes while equipped (never below zero).
    [SerializeField, Min(0f)] private float defense = 10f;

    public float Defense { get => defense; }
}
