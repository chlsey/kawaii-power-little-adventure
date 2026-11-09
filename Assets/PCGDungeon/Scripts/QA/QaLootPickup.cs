using UnityEngine;

// Minimal marker component for a generated powerup/loot object.
// QaPlayerController collects it via Interact rather than on contact.
// With no item assigned it is a plain QA powerup that only counts toward powerupCount.
public sealed class QaLootPickup : MonoBehaviour
{
    [SerializeField] private ItemDefinition item;

    private bool collected;

    public ItemDefinition Item { get => item; set => item = value; }

    // Returns true the first time it is collected; false on any further call.
    public bool TryCollect()
    {
        if (collected)
            return false;

        collected = true;
        Destroy(gameObject);
        return true;
    }
}
