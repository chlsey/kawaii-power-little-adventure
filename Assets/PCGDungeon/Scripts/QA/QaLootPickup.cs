using UnityEngine;

// Minimal marker component for a generated powerup/loot object.
// QaPlayerController collects it via Interact rather than on contact.
public sealed class QaLootPickup : MonoBehaviour
{
    private bool collected;

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
