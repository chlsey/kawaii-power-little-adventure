// Checks a generated dungeon and decides whether it should be accepted.
// For the POC this only checks that something was actually generated.

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

        return ValidationResult.Pass();
    }
}
