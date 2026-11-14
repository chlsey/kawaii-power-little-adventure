using UnityEngine;

// Marks the level's completion goal. Generator places one of these in the
// boss room (the dungeon's farthest point from spawn) so that reaching it,
// not merely generation succeeding, is what counts as episode completion.
//
// Walking into it completes the level, which is how the QA bot and trained
// models finish. Pressing Interact next to it completes it too; with
// RequireInteract set, only Interact does, so a player has to choose to leave.
[RequireComponent(typeof(Collider))]
public sealed class QaExitZone : MonoBehaviour, IInteractable
{
    public bool RequireInteract { get; set; }

    public bool Interact(QaPlayerController player)
    {
        if (!player.EpisodeActive)
            return false;

        player.ReportExitReached();
        return true;
    }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        gameObject.tag = "Exit";
    }

    private void OnTriggerEnter(Collider other)
    {
        QaPlayerController player = other.GetComponentInParent<QaPlayerController>();
        if (player != null && !RequireInteract)
            player.ReportExitReached();
    }
}
