using UnityEngine;

// Diablo-style follow camera: a fixed high angle behind and above the player
// that tracks position but never rotates with them. Re-finds the player after
// R regenerates the dungeon, since the generator replaces the player object.
[RequireComponent(typeof(Camera))]
public sealed class DungeonCamera : MonoBehaviour
{
    [SerializeField] private float pitch = 55f;
    [SerializeField] private float yaw = 45f;
    [SerializeField] private float distance = 12f;
    [SerializeField] private float followSmoothTime = 0.15f;
    // Aim at the player's chest rather than their feet.
    [SerializeField] private float targetHeight = 1f;

    private const float PlayerSearchInterval = 0.5f;

    private Transform target;
    private Vector3 velocity;
    private float nextPlayerSearch;

    // Flattened camera directions, for camera-relative movement input.
    public Vector3 PlanarForward { get => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward; }
    public Vector3 PlanarRight { get => Quaternion.Euler(0f, yaw, 0f) * Vector3.right; }

    private void LateUpdate()
    {
        if (target == null && Time.unscaledTime >= nextPlayerSearch)
        {
            nextPlayerSearch = Time.unscaledTime + PlayerSearchInterval;
            QaPlayerController player = FindFirstObjectByType<QaPlayerController>();
            if (player != null)
            {
                target = player.transform;
                transform.position = DesiredPosition(); // Snap to a new player instead of gliding across the map.
                velocity = Vector3.zero;
            }
        }

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        if (target != null)
            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref velocity, followSmoothTime);
    }

    private Vector3 DesiredPosition()
    {
        Vector3 focus = target.position + Vector3.up * targetHeight;
        return focus - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * distance;
    }
}
