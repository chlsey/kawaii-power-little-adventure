using System;
using UnityEngine;

// Drives one QA gameplay episode on a generated level: applies QaAction
// commands, casts the ray sensors, and accumulates QaEpisodeMetrics. This is
// the in-engine half of the qa_framework contract; a future Unity transport
// adapter calls BeginTest/ApplyAction in place of FakeQAEnvironment.
[RequireComponent(typeof(CharacterController))]
public sealed class QaPlayerController : MonoBehaviour
{
    private const float StepDurationSeconds = 0.1f;

    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float turnDegreesPerSecond = 720f;
    [SerializeField] private float attackRange = 1.25f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float interactRange = 1.25f;
    [SerializeField] private float maxHealth = 10f;
    [SerializeField] private int rayCount = 16;
    [SerializeField] private float rayDistance = 12f;
    [SerializeField] private float rayHeight = 0.6f;
    [SerializeField] private float stuckDistanceThreshold = 0.05f;
    [SerializeField] private int stuckStepThreshold = 20;

    private CharacterController controller;
    private float health;
    private int powerupCount;
    private QaAction previousAction;
    private QaEpisodeMetrics metrics;
    private bool episodeActive;
    private Vector3 stuckCheckPosition;
    private int stepsSinceStuckCheck;

    public bool EpisodeActive => episodeActive;
    public QaEpisodeMetrics Metrics => metrics;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    // Resets health, metrics and the previous action, matching QAEnvironment.reset semantics.
    public void BeginTest()
    {
        health = maxHealth;
        powerupCount = 0;
        previousAction = QaAction.Noop;
        metrics = new QaEpisodeMetrics();
        stuckCheckPosition = transform.position;
        stepsSinceStuckCheck = 0;
        episodeActive = true;
    }

    // Returns the current observation without acting, matching QAEnvironment.reset's
    // initial observation (sensed, but with no movement/attack/interact applied).
    public QaObservation Observe()
    {
        return Sense(previousAction);
    }

    // Applies one QA command and returns the resulting observation, matching QAEnvironment.step.
    public QaObservation ApplyAction(QaAction action)
    {
        if (!episodeActive)
            throw new InvalidOperationException("Call BeginTest before applying actions.");

        Move(action);

        if (action.Attack)
            PerformAttack();

        if (action.Interact)
            PerformInteract();

        metrics.StepCount++;
        metrics.ElapsedSeconds = metrics.StepCount * StepDurationSeconds;
        UpdateStuckTracking();

        QaObservation observation = Sense(action);
        previousAction = action;
        return observation;
    }

    private void Move(QaAction action)
    {
        Vector3 moveInput = new Vector3(action.MoveX, 0f, action.MoveY);
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        Vector3 motion = moveInput * (moveSpeed * StepDurationSeconds);
        motion += Physics.gravity * (StepDurationSeconds * StepDurationSeconds);
        controller.Move(motion);

        if (moveInput.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveInput, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, turnDegreesPerSecond * StepDurationSeconds);
        }
    }

    private void PerformAttack()
    {
        Vector3 center = transform.position + transform.forward * (attackRange * 0.5f);
        Collider[] hits = Physics.OverlapSphere(center, attackRange);
        foreach (Collider hit in hits)
        {
            QaEnemy enemy = hit.GetComponentInParent<QaEnemy>();
            if (enemy != null && enemy.TakeDamage(attackDamage))
                metrics.EnemiesKilled++;
        }
    }

    private void PerformInteract()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange);
        foreach (Collider hit in hits)
        {
            QaLootPickup loot = hit.GetComponentInParent<QaLootPickup>();
            if (loot != null && loot.TryCollect())
            {
                powerupCount++;
                metrics.PowerupsUsed++;
            }
        }
    }

    // Called by QaEnemy contact damage. Marks player death once health reaches zero.
    public void ReportDamage(float amount)
    {
        if (!episodeActive || amount <= 0f)
            return;

        health = Mathf.Max(0f, health - amount);
        metrics.DamageTaken += amount;

        if (health <= 0f)
        {
            metrics.PlayerDied = true;
            episodeActive = false;
        }
    }

    // Called by QaExitZone when the player reaches the level's designated exit.
    public void ReportExitReached()
    {
        if (!episodeActive)
            return;

        metrics.Completed = true;
        episodeActive = false;
    }

    private void UpdateStuckTracking()
    {
        stepsSinceStuckCheck++;
        if (stepsSinceStuckCheck < stuckStepThreshold)
            return;

        float moved = Vector3.Distance(transform.position, stuckCheckPosition);
        if (moved < stuckDistanceThreshold)
            metrics.StuckEvents++;

        stuckCheckPosition = transform.position;
        stepsSinceStuckCheck = 0;
    }

    private QaObservation Sense(QaAction previous)
    {
        var rays = new QaRayObservation[rayCount];
        Vector3 origin = transform.position + Vector3.up * rayHeight;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = i * (360f / rayCount);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            rays[i] = Physics.Raycast(origin, direction, out RaycastHit hit, rayDistance)
                ? new QaRayObservation(true, Mathf.Clamp01(hit.distance / rayDistance), ClassifyHit(hit.collider))
                : QaRayObservation.Miss;
        }

        return new QaObservation(Mathf.Clamp01(health / maxHealth), powerupCount, rays, previous);
    }

    private static QaObjectType ClassifyHit(Collider collider)
    {
        if (collider.CompareTag("Exit"))
            return QaObjectType.Exit;
        if (collider.GetComponentInParent<QaEnemy>() != null)
            return QaObjectType.Enemy;
        if (collider.GetComponentInParent<QaLootPickup>() != null)
            return QaObjectType.Loot;
        if (collider.CompareTag("Wall") || collider.CompareTag("OuterCorner"))
            return QaObjectType.Obstacle;

        return QaObjectType.None;
    }
}
