using System.Collections.Generic;
using UnityEngine;

// Locks a room's doors once the player is fully inside, and unlocks them when
// the room's puzzle is solved. Added to the room by Generator.SpawnPuzzles.
public sealed class PuzzleRoom : MonoBehaviour
{
    private const float PlayerSearchInterval = 0.5f;

    private readonly List<DungeonDoor> doors = new List<DungeonDoor>();
    private Bounds lockBounds;
    private QaPlayerController player;
    private float nextPlayerSearch;

    public RoomPuzzle Puzzle { get; private set; }
    public IReadOnlyList<DungeonDoor> Doors { get => doors; }
    public bool IsLocked { get; private set; }

    // lockBounds is the room shrunk by a tile, so doors never close on a player standing in the doorway.
    public void Setup(RoomPuzzle puzzle, IEnumerable<DungeonDoor> roomDoors, Bounds roomLockBounds)
    {
        Puzzle = puzzle;
        doors.AddRange(roomDoors);
        lockBounds = roomLockBounds;
        Puzzle.Solved += Unlock;
    }

    private void OnDestroy()
    {
        if (Puzzle != null)
            Puzzle.Solved -= Unlock;
    }

    private void FixedUpdate()
    {
        if (IsLocked || Puzzle == null || Puzzle.IsSolved)
            return;

        if (player == null && Time.fixedTime >= nextPlayerSearch)
        {
            player = FindFirstObjectByType<QaPlayerController>();
            nextPlayerSearch = Time.fixedTime + PlayerSearchInterval;
        }

        if (player == null)
            return;

        Vector3 position = player.transform.position;
        if (lockBounds.Contains(new Vector3(position.x, lockBounds.center.y, position.z)))
            Lock();
    }

    private void Lock()
    {
        IsLocked = true;
        foreach (DungeonDoor door in doors)
            door.Lock();
    }

    private void Unlock()
    {
        IsLocked = false;
        foreach (DungeonDoor door in doors)
            door.Unlock();
    }
}
