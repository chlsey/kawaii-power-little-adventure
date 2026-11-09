using UnityEngine;

public sealed class LightsOutSwitch : MonoBehaviour, IInteractable
{
    private LightsOutPuzzle puzzle;
    private int index;

    public void Configure(LightsOutPuzzle owner, int switchIndex)
    {
        puzzle = owner;
        index = switchIndex;
    }

    public bool Interact(QaPlayerController player)
    {
        return puzzle != null && puzzle.ToggleSwitch(index);
    }
}
