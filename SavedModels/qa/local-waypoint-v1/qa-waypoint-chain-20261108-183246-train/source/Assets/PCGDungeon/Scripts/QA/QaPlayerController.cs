using System;
using UnityEngine;

// Applies gameplay commands and records player state and episode metrics.
// Unity policies and the RL training adapter use the same controller.
[RequireComponent(typeof(CharacterController))]
public sealed class QaPlayerController : MonoBehaviour
{
    public const float StepDurationSeconds = 0.1f;

    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float turnDegreesPerSecond = 720f;
    [SerializeField] private float attackRange = 1.25f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float interactRange = 1.25f;
    [SerializeField] private float maxHealth = 10f;
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
    public event Action EpisodeEnded;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    // Resets health, metrics and the previous action, matching QAEnvironment.reset semantics.
    public void BeginTest(Generator generator)
    {
        health = maxHealth;
        powerupCount = 0;
        previousAction = QaAction.Noop;
        metrics = new QaEpisodeMetrics();
        stuckCheckPosition = transform.position;
        stepsSinceStuckCheck = 0;
        episodeActive = true;
    }

    // Returns gameplay state without acting. Model perception is collected separately.
    public QaObservation Observe()
    {
        return new QaObservation(Mathf.Clamp01(health / maxHealth), powerupCount, previousAction);
    }

    // Applies one QA command and returns the resulting observation, matching QAEnvironment.step.
    public QaObservation ApplyAction(QaAction action)
    {
        if (!episodeActive)
            throw new InvalidOperationException("Call BeginTest before applying actions.");

        // Exit/death callbacks can publish this action's final state during movement.
        metrics.StepCount++;
        metrics.ElapsedSeconds = metrics.StepCount * StepDurationSeconds;
        Move(action);

        if (action.Attack)
            PerformAttack();

        if (action.Interact)
            PerformInteract();

        UpdateStuckTracking();

        previousAction = action;
        return Observe();
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
            EpisodeEnded?.Invoke();
        }
    }

    // Called by QaExitZone when the player reaches the level's designated exit.
    public void ReportExitReached()
    {
        if (!episodeActive)
            return;

        metrics.Completed = true;
        episodeActive = false;
        EpisodeEnded?.Invoke();
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
}
