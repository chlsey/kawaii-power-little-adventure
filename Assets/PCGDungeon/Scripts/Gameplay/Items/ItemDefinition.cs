using UnityEngine;

// Base data for anything the player can pick up. Concrete item types are
// ScriptableObject assets, so a new sword or potion is a new asset, not new code.
public abstract class ItemDefinition : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField] private Color pickupColor = Color.green;
    // Optional world model for this item; the generator uses Settings' lootPickup otherwise.
    [SerializeField] private GameObject pickupPrefab;

    public string DisplayName { get => string.IsNullOrEmpty(displayName) ? name : displayName; }
    public Sprite Icon { get => icon; }
    public Color PickupColor { get => pickupColor; }
    public GameObject PickupPrefab { get => pickupPrefab; }
}
