using UnityEngine;

// Simple steering AI for a generated enemy: wait in its room, chase the player
// on sight, wind up and hit when in range, and walk home when the player leaves.
// Enemies are leashed to the room they spawned in (its trigger BoxCollider), so
// they never need pathfinding through corridors. Everything runs on the fixed
// timestep with no randomness, so a seeded episode plays out identically.
[RequireComponent(typeof(QaEnemy), typeof(Rigidbody))]
public sealed class EnemyBrain : MonoBehaviour
{
    private enum State { Idle, Chase, Attack, Return }

    [Header("Senses")]
    [SerializeField] private float aggroRange = 6f;
    [SerializeField] private float eyeHeight = 0.6f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float bodyRadius = 0.35f;
    [SerializeField] private float lookAhead = 0.75f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.1f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float windupTime = 0.4f;
    [SerializeField] private float cooldownTime = 1f;

    // Rotations tried, in order, when the straight path is blocked.
    private static readonly float[] SteerAngles = { 0f, 45f, -45f, 90f, -90f };
    private const float PlayerSearchInterval = 1f;

    private Rigidbody body;
    private QaPlayerController player;
    private Bounds leash;
    private bool hasLeash;
    private Vector3 homePosition;
    private State state = State.Idle;
    private float stateTimer;
    private float strikeTimer = -1f;
    private float nextPlayerSearch;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        homePosition = transform.position;

        // The generator parents enemies to their room, which owns a trigger sized to its floor.
        Room room = GetComponentInParent<Room>();
        BoxCollider roomCollider = room != null ? room.GetComponent<BoxCollider>() : null;
        if (roomCollider != null)
        {
            leash = roomCollider.bounds;
            leash.Expand(new Vector3(-bodyRadius * 2f, 0f, -bodyRadius * 2f));
            hasLeash = true;
        }
    }

    private void FixedUpdate()
    {
        FindPlayer();
        stateTimer -= Time.fixedDeltaTime;

        switch (state)
        {
            case State.Idle:
                if (CanSeePlayer())
                    state = State.Chase;
                break;

            case State.Chase:
                if (!CanSeePlayer())
                    state = State.Return;
                else if (FlatDistanceTo(player.transform.position) <= attackRange)
                    BeginAttack();
                else
                    MoveTowards(player.transform.position);
                break;

            case State.Attack:
                if (strikeTimer >= 0f && stateTimer <= cooldownTime)
                {
                    strikeTimer = -1f;
                    Strike();
                }
                FaceTowards(player != null ? player.transform.position : transform.position + transform.forward);
                if (stateTimer <= 0f)
                    state = CanSeePlayer() ? State.Chase : State.Return;
                break;

            case State.Return:
                if (CanSeePlayer())
                    state = State.Chase;
                else if (FlatDistanceTo(homePosition) <= 0.1f)
                    state = State.Idle;
                else
                    MoveTowards(homePosition);
                break;
        }
    }

    private void BeginAttack()
    {
        state = State.Attack;
        stateTimer = windupTime + cooldownTime;
        strikeTimer = windupTime;
    }

    // Lands at the end of the windup, so the player can dodge by stepping out of range.
    private void Strike()
    {
        if (player != null && FlatDistanceTo(player.transform.position) <= attackRange)
            player.ReportDamage(attackDamage);
    }

    private void FindPlayer()
    {
        if (player != null || Time.fixedTime < nextPlayerSearch)
            return;

        player = FindFirstObjectByType<QaPlayerController>();
        nextPlayerSearch = Time.fixedTime + PlayerSearchInterval;
    }

    // In range, inside this enemy's room, and not behind a wall.
    private bool CanSeePlayer()
    {
        if (player == null)
            return false;

        Vector3 target = player.transform.position;
        if (FlatDistanceTo(target) > aggroRange)
            return false;
        if (hasLeash && !leash.Contains(new Vector3(target.x, leash.center.y, target.z)))
            return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = Flat(target - eye);
        if (!Physics.Raycast(eye, toPlayer.normalized, out RaycastHit hit, toPlayer.magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
            return true;

        return hit.collider.GetComponentInParent<QaPlayerController>() != null;
    }

    private void MoveTowards(Vector3 target)
    {
        Vector3 desired = Flat(target - transform.position);
        if (desired.sqrMagnitude < 0.0001f)
            return;

        Vector3 direction = ChooseDirection(desired.normalized);
        if (direction == Vector3.zero)
            return;

        float step = Mathf.Min(moveSpeed * Time.fixedDeltaTime, desired.magnitude);
        Vector3 next = transform.position + direction * step;
        if (hasLeash)
        {
            next.x = Mathf.Clamp(next.x, leash.min.x, leash.max.x);
            next.z = Mathf.Clamp(next.z, leash.min.z, leash.max.z);
        }

        body.MovePosition(next);
        FaceTowards(transform.position + direction);
    }

    // Steers around props by trying the straight path first, then wider angles.
    private Vector3 ChooseDirection(Vector3 desired)
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        foreach (float angle in SteerAngles)
        {
            Vector3 candidate = Quaternion.Euler(0f, angle, 0f) * desired;
            if (!Physics.SphereCast(origin, bodyRadius, candidate, out RaycastHit hit, lookAhead, obstacleMask, QueryTriggerInteraction.Ignore)
                || hit.collider.GetComponentInParent<QaPlayerController>() != null)
                return candidate;
        }

        return Vector3.zero;
    }

    private void FaceTowards(Vector3 target)
    {
        Vector3 direction = Flat(target - transform.position);
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion look = Quaternion.LookRotation(direction);
        body.MoveRotation(Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.fixedDeltaTime));
    }

    private float FlatDistanceTo(Vector3 target)
    {
        return Flat(target - transform.position).magnitude;
    }

    private static Vector3 Flat(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
