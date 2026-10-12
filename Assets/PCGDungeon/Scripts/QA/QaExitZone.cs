using UnityEngine;

// Marks the level's completion goal. Generator places one of these in the
// boss room (the dungeon's farthest point from spawn) so that reaching it,
// not merely generation succeeding, is what counts as episode completion.
[RequireComponent(typeof(Collider))]
public sealed class QaExitZone : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        gameObject.tag = "Exit";
    }

    private void OnTriggerEnter(Collider other)
    {
        QaPlayerController player = other.GetComponentInParent<QaPlayerController>();
        if (player != null)
            player.ReportExitReached();
    }
}
