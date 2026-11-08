// Anything the player can use with the Interact action (levers, switches).
// Item pickups stay on QaLootPickup so the QA Loot classification is unchanged.
public interface IInteractable
{
    // Returns true if the interaction did something.
    bool Interact(QaPlayerController player);
}
