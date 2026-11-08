using System;
using UnityEngine;

// Base for a puzzle placed in a puzzle room. PuzzleRoom locks the doors when the
// player walks in and unlocks them once Solved fires. New puzzle types (pressure
// plates, switch orders) subclass this and call MarkSolved.
public abstract class RoomPuzzle : MonoBehaviour
{
    public event Action Solved;

    public bool IsSolved { get; private set; }

    protected void MarkSolved()
    {
        if (IsSolved)
            return;

        IsSolved = true;
        Solved?.Invoke();
    }
}
