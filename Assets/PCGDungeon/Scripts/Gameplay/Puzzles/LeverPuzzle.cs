using UnityEngine;

// The simplest puzzle: pull the lever once to open the room.
public sealed class LeverPuzzle : RoomPuzzle, IInteractable
{
    // Optional moving part; rotates about its local X axis when pulled.
    [SerializeField] private Transform handle;
    [SerializeField] private float pulledAngle = -60f;

    public bool Interact(QaPlayerController player)
    {
        if (IsSolved)
            return false;

        if (handle != null)
            handle.localRotation *= Quaternion.Euler(pulledAngle, 0f, 0f);

        MarkSolved();
        return true;
    }
}
