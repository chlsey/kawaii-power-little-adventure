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
    private PlayerEquipment equipment;
    private float attackCooldownRemaining;
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
    public PlayerEquipment Equipment => equipment;
    // Name of the last item used and when, so the HUD can confirm a pickup did something.
    public string LastPickupName { get; private set; }
    public float LastPickupTime { get; private set; }
    public float Health => health;
    public float MaxHealth => maxHealth;
    public bool IsDead => health <= 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        equipment = GetComponent<PlayerEquipment>();
        if (equipment == null)
            equipment = gameObject.AddComponent<PlayerEquipment>();

        // Health is live from spawn, not only during a QA episode; BeginTest refills it.
        health = maxHealth;
        metrics = new QaEpisodeMetrics();
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
        attackCooldownRemaining = 0f;
        equipment.ResetLoadout();
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
        equipment.Tick(StepDurationSeconds);
        attackCooldownRemaining -= StepDurationSeconds;

        Move(action);

        if (action.Attack && attackCooldownRemaining <= 0f)
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

        Vector3 motion = moveInput * (equipment.MoveSpeed(moveSpeed) * StepDurationSeconds);
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
        float range = equipment.AttackRange(attackRange);
        float damage = equipment.AttackDamage(attackDamage);
        attackCooldownRemaining = equipment.AttackCooldown;

        Vector3 center = transform.position + transform.forward * (range * 0.5f);
        Collider[] hits = Physics.OverlapSphere(center, range);
        foreach (Collider hit in hits)
        {
            QaEnemy enemy = hit.GetComponentInParent<QaEnemy>();
            if (enemy != null && enemy.TakeDamage(damage))
                metrics.EnemiesKilled++;
        }
    }

    private void PerformInteract()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange);
        var used = new System.Collections.Generic.HashSet<IInteractable>();
        foreach (Collider hit in hits)
        {
            // An object with several colliders is still only used once per Interact.
            IInteractable interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable != null && used.Add(interactable))
                interactable.Interact(this);

            QaLootPickup loot = hit.GetComponentInParent<QaLootPickup>();
            if (loot != null && loot.TryCollect())
            {
                powerupCount++;
                metrics.PowerupsUsed++;
                UseItem(loot.Item);
            }
        }
    }

    private void UseItem(ItemDefinition item)
    {
        LastPickupName = item != null ? item.DisplayName : "Plain powerup (no effect)";
        LastPickupTime = Time.time;

        if (item is PowerupDefinition powerup && powerup.Effect == PowerupEffect.Heal)
            health = Mathf.Min(maxHealth, health + powerup.Amount);
        else if (item != null)
            equipment.Equip(item);
    }

    // Called by enemy attacks. Shields and powerups reduce the damage first.
    // Marks player death once health reaches zero.
    public void ReportDamage(float amount)
    {
        amount = equipment.DamageTaken(amount);
        if (IsDead || amount <= 0f)
            return;

        health = Mathf.Max(0f, health - amount);
        if (!episodeActive)
            return;

        // Metrics only describe QA episodes.
        metrics.DamageTaken += amount;
        if (IsDead)
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
