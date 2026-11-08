using UnityEngine;

[CreateAssetMenu(fileName = "Shield", menuName = "PCG Dungeon/Items/Shield")]
public sealed class ShieldDefinition : ItemDefinition
{
    // Fraction of incoming damage removed while equipped.
    [SerializeField, Range(0f, 1f)] private float damageReduction = 0.3f;

    public float DamageReduction { get => damageReduction; }
}
