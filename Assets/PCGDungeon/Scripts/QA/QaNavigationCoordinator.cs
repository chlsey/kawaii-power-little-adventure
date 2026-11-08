using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns attempt-local navigation; the action policy receives only its visible waypoint.</summary>
public sealed class QaNavigationCoordinator
{
    public const float AcceptanceRadius = 0.5f;
    private readonly IMapGoalSelector selector;
    private readonly List<Vector3> excluded = new List<Vector3>();
    private QaMapGoal goal;
    private bool hasGoal, replanned, exitDeferred;
    private float bestDistance;
    private int stalledActions;
    public bool HasWaypoint { get; private set; }
    public Vector3 Waypoint { get; private set; }
    public int WaypointVersion { get; private set; }
    public int Replans { get; private set; }
    public int AbandonedGoals { get; private set; }
    public int ExcludedCount => excluded.Count;
    public bool GoalIsExit => hasGoal && goal.IsExit;
    public string Status { get; private set; } = "reset";
    public List<Vector3> Route { get; private set; } = new List<Vector3>();

    public QaNavigationCoordinator(IMapGoalSelector selector = null)
    {
        this.selector = selector ?? new QaFrontierGoalSelector();
    }

    public static float Distance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public void Reset()
    {
        excluded.Clear(); Route.Clear(); hasGoal = HasWaypoint = replanned = exitDeferred = false;
        Waypoint = Vector3.zero; WaypointVersion = Replans = AbandonedGoals = stalledActions = 0;
        bestDistance = 0f; Status = "reset";
    }

    public void Advance(ModelSensorPackage sensors, CharacterController actor, bool actionCompleted)
    {
        Vector3 position = actor.transform.position;
        var planner = new QaObservedRoutePlanner(sensors.Grid, actor.radius + actor.skinWidth + 0.05f);
        bool refresh = !HasWaypoint;
        if (HasWaypoint)
        {
            float distance = Distance(position, Waypoint);
            if (distance <= AcceptanceRadius)
            {
                HasWaypoint = false;
                refresh = true;
                replanned = false;
            }
            else if (!planner.SegmentKnown(position, Waypoint) || !sensors.CanUseWaypoint(Waypoint, actor))
            {
                HasWaypoint = false;
                refresh = true;
            }
            else if (actionCompleted)
            {
                if (bestDistance - distance >= 0.05f)
                {
                    bestDistance = distance;
                    stalledActions = 0;
                }
                else stalledActions++;
                if (stalledActions >= 20)
                {
                    HasWaypoint = false;
                    refresh = true;
                    stalledActions = 0;
                    if (!replanned) { replanned = true; Replans++; }
                    else Abandon();
                }
            }
        }

        if (hasGoal && (Distance(position, QaObservedRoutePlanner.Point(goal.Cell, position.y)) <= AcceptanceRadius ||
            !planner.IsFree(goal.Cell)))
        {
            if (!goal.IsExit)
            {
                excluded.Add(QaObservedRoutePlanner.Point(goal.Cell, 0f));
                // A completed exploration leg provides a new approach to a deferred exit.
                exitDeferred = false;
            }
            hasGoal = false; refresh = true;
        }

        // Try another reachable goal immediately if a route has no usable local waypoint.
        while (true)
        {
            // Reachability, rather than a hidden scene destination, controls exit priority.
            if (selector.TrySelect(planner, position, excluded, !exitDeferred, out QaMapGoal selected))
            {
                if (!hasGoal || (selected.IsExit && !goal.IsExit))
                {
                    goal = selected; hasGoal = true; refresh = true;
                    HasWaypoint = false; replanned = false;
                }
            }
            if (!hasGoal)
            {
                HasWaypoint = false; Route.Clear(); Status = "no_reachable_goal";
                return;
            }
            if (!refresh) return;
            Route = planner.Route(position, goal.Cell);
            for (int i = Route.Count - 1; i >= 0; i--)
            {
                float distance = Distance(position, Route[i]);
                if (distance <= AcceptanceRadius || distance > 6f || !planner.SegmentKnown(position, Route[i]) ||
                    !sensors.CanUseWaypoint(Route[i], actor)) continue;
                Waypoint = Route[i]; HasWaypoint = true; WaypointVersion++;
                bestDistance = distance; stalledActions = 0;
                Status = goal.IsExit ? "exit" : "exploring";
                return;
            }
            Abandon();
        }
    }

    private void Abandon()
    {
        if (hasGoal)
        {
            if (goal.IsExit) exitDeferred = true;
            else
            {
                excluded.Add(QaObservedRoutePlanner.Point(goal.Cell, 0f));
            }
            AbandonedGoals++;
        }
        hasGoal = HasWaypoint = false; replanned = false; Route.Clear();
    }
}

/// <summary>Rewards only new approach/arrival; target changes start a new distance baseline.</summary>
public sealed class QaWaypointRewardTracker
{
    private readonly List<Vector3> rewarded = new List<Vector3>();
    private Vector3 target;
    private int version = -1;
    private float closest;
    private bool armed, paid;
    public float CompletionBonus { get; private set; }
    public float PositiveRewardTotal { get; private set; }
    public float PositiveRewardBudget { get; }

    public QaWaypointRewardTracker(float positiveRewardBudget = 5f)
    {
        if (positiveRewardBudget <= 0f)
            throw new System.ArgumentOutOfRangeException(nameof(positiveRewardBudget));
        PositiveRewardBudget = positiveRewardBudget;
    }

    public void Reset() { rewarded.Clear(); version = -1; CompletionBonus = PositiveRewardTotal = 0f; armed = paid = false; }

    public void Assign(bool valid, int nextVersion, Vector3 point, Vector3 position)
    {
        if (!valid) { version = -1; armed = false; return; }
        if (version == nextVersion) return;
        version = nextVersion; target = point;
        closest = QaNavigationCoordinator.Distance(position, target);
        armed = closest > QaNavigationCoordinator.AcceptanceRadius; paid = false;
    }

    public float Measure(Vector3 position)
    {
        if (version < 0) return 0f;
        float distance = QaNavigationCoordinator.Distance(position, target);
        // Target changes never replenish the episode's shared approach/arrival budget.
        float reward = Mathf.Min(Mathf.Max(0f, closest - distance) * 0.1f,
            Mathf.Max(0f, PositiveRewardBudget - PositiveRewardTotal));
        closest = Mathf.Min(closest, distance);
        if (distance > QaNavigationCoordinator.AcceptanceRadius) armed = true;
        if (armed && !paid && distance <= QaNavigationCoordinator.AcceptanceRadius)
        {
            paid = true;
            bool duplicate = false;
            foreach (Vector3 old in rewarded)
                if (QaNavigationCoordinator.Distance(target, old) <= 0.5f) duplicate = true;
            if (!duplicate)
            {
                float bonus = Mathf.Min(1f, Mathf.Max(0f, PositiveRewardBudget - PositiveRewardTotal - reward));
                rewarded.Add(target); CompletionBonus += bonus; reward += bonus;
            }
        }
        PositiveRewardTotal += reward;
        return reward;
    }
}
