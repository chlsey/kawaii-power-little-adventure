// Checks a generated dungeon and decides whether it should be accepted.
// Checks that something was generated and that every puzzle room can trap
// and then release the player.

using UnityEngine;

public struct ValidationResult
{
    public bool IsValid;
    public string Message;

    public static ValidationResult Pass() => new ValidationResult { IsValid = true, Message = "OK" };
    public static ValidationResult Fail(string message) => new ValidationResult { IsValid = false, Message = message };
}

public class DungeonValidator : MonoBehaviour
{
    public ValidationResult Validate(Generator generator)
    {
        if (generator == null)
            return ValidationResult.Fail("No generator.");

        if (generator.transform.childCount == 0)
            return ValidationResult.Fail("Generator produced no objects.");

        if (generator.PlayerSpawnRoom == null)
            return ValidationResult.Fail("No spawn room was selected.");

        return ValidatePuzzleRooms(generator);
    }

    private static ValidationResult ValidatePuzzleRooms(Generator generator)
    {
        if (generator.PuzzleRooms.Count < generator.RequestedPuzzleRoomCount)
            return ValidationResult.Fail($"Only {generator.PuzzleRooms.Count} of {generator.RequestedPuzzleRoomCount} puzzle rooms could be placed.");

        foreach (Room room in generator.PuzzleRooms)
        {
            if (room == generator.PlayerSpawnRoom || room == generator.BossSpawnRoom)
                return ValidationResult.Fail($"{room.name} is a puzzle room but also the spawn or boss room.");

            PuzzleRoom puzzleRoom = room.GetComponent<PuzzleRoom>();
            if (puzzleRoom == null || puzzleRoom.Puzzle == null)
                return ValidationResult.Fail($"{room.name} is a puzzle room with no puzzle.");

            // Without doors the room can't lock, so the puzzle would be skippable.
            if (puzzleRoom.Doors.Count == 0)
                return ValidationResult.Fail($"{room.name} is a puzzle room with no doors.");

            if (puzzleRoom.Puzzle.GetComponentInParent<Room>() != room)
                return ValidationResult.Fail($"{room.name}'s puzzle is outside the room, so it can't be reached once locked.");

            if (room.GetComponentInChildren<QaEnemy>() != null)
                return ValidationResult.Fail($"{room.name} is a puzzle room but contains enemies.");
        }

        return ValidationResult.Pass();
    }
}
