using UnityEngine;

// A generated door that slides sideways to open. The whole door moves, so its
// collider leaves the doorway when open and blocks it when closed; no separate
// collider toggling is needed. PuzzleRoom locks doors to trap the player until
// the room's puzzle is solved.
public sealed class DungeonDoor : MonoBehaviour
{
    [SerializeField] private bool startsOpen = true;
    [SerializeField] private float slideDistance = 1f;
    [SerializeField] private float slideSpeed = 3f;

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private bool isOpen;

    public bool IsOpen { get => isOpen; }
    public bool IsLocked { get; private set; }
    public bool IsMoving { get => transform.localPosition != TargetPosition; }

    private Vector3 TargetPosition { get => isOpen ? openPosition : closedPosition; }

    private void Awake()
    {
        // The generator places doors in their closed position. Slide along the
        // door's own left so rotated (east/west) doors open along their wall.
        closedPosition = transform.localPosition;
        openPosition = closedPosition + transform.localRotation * Vector3.left * slideDistance;

        isOpen = startsOpen;
        transform.localPosition = TargetPosition;
    }

    // Fixed timestep keeps door timing identical across RL episodes with the same seed.
    private void FixedUpdate()
    {
        if (IsMoving)
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, TargetPosition, slideSpeed * Time.fixedDeltaTime);
    }

    // Returns false if the door is locked.
    public bool Open()
    {
        if (IsLocked)
            return false;

        isOpen = true;
        return true;
    }

    public void Close()
    {
        isOpen = false;
    }

    // Closes the door and keeps it closed until Unlock is called.
    public void Lock()
    {
        Close();
        IsLocked = true;
    }

    public void Unlock(bool openDoor = true)
    {
        IsLocked = false;
        if (openDoor)
            Open();
    }

    // For player interaction: opens a closed door, closes an open one.
    public bool Toggle()
    {
        if (isOpen)
        {
            Close();
            return true;
        }

        return Open();
    }
}
