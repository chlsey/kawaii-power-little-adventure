using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns attempt-local navigation; the action policy receives only its visible waypoint.</summary>
public sealed class QaNavigationCoordinator
{
    public const float AcceptanceRadius = 0.5f;
    public const float DefaultMaxWaypointDistance = 10f;
    public float MaxWaypointDistance { get; set; } = DefaultMaxWaypointDistance;
    private readonly IMapGoalSelector selector;
    private readonly List<Vector3> excluded = new List<Vector3>();
    private QaMapGoal goal;
    private bool hasGoal, replanned, exitDeferred, exploringEscape;
    private float bestDistance;
    private int stalledActions;
    private QaObservedRoutePlanner cachedPlanner;
    private int plannerVersion, exitDiscoveryVersion, waypointRouteIndex = -1;
    private float plannerClearance;
    public int PlannerBuilds { get; private set; }
    public int GoalSelections { get; private set; }
    public int RouteSearches { get; private set; }
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
        excluded.Clear(); Route.Clear(); hasGoal = HasWaypoint = replanned = exitDeferred = exploringEscape = false;
        Waypoint = Vector3.zero; WaypointVersion = Replans = AbandonedGoals = stalledActions = 0;
        bestDistance = 0f; Status = "reset";
        cachedPlanner = null; plannerVersion = exitDiscoveryVersion = -1; waypointRouteIndex = -1;
        PlannerBuilds = GoalSelections = RouteSearches = 0;
    }

    public void Advance(ModelSensorPackage sensors, CharacterController actor, bool actionCompleted)
    {
        Vector3 position = actor.transform.position;
        var planner = GetPlanner(sensors, actor, out bool knowledgeChanged);
        bool exitDiscovered = exitDiscoveryVersion != sensors.Grid.ExitDiscoveryVersion;
        exitDiscoveryVersion = sensors.Grid.ExitDiscoveryVersion;
        bool startFree = planner.IsFree(ModelSensorGrid.WorldToCell(position));

        // Hold an escape until arrival in navigable space, even if the starting
        // cell becomes free partway through the move.
        if (exploringEscape && HasWaypoint)
        {
            bool arrived = Distance(position, Waypoint) <= AcceptanceRadius;
            bool valid = planner.IsFree(ModelSensorGrid.WorldToCell(Waypoint)) &&
                planner.SegmentObserved(position, Waypoint) && sensors.CanUseExplorationEscape(Waypoint, actor);
            if (arrived && startFree)
            {
                exploringEscape = HasWaypoint = false;
                Route.Clear(); waypointRouteIndex = -1;
            }
            else
            {
                if (!arrived && valid) return;
                AssignExplorationEscape(planner, sensors, actor, position);
                return;
            }
        }
        if (!startFree)
        {
            AssignExplorationEscape(planner, sensors, actor, position);
            return;
        }

        if (HasWaypoint)
        {
            float distance = Distance(position, Waypoint);
            if (distance <= AcceptanceRadius)
            {
                // Consume the reached route prefix, keeping the remaining route.
                if (waypointRouteIndex >= 0) Route.RemoveRange(0, waypointRouteIndex + 1);
                HasWaypoint = false; waypointRouteIndex = -1; replanned = false;
            }
            else if (!planner.SegmentKnown(position, Waypoint) || !sensors.CanUseWaypoint(Waypoint, actor))
            {
                HasWaypoint = false; Route.Clear(); waypointRouteIndex = -1;
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
                    HasWaypoint = false; Route.Clear(); waypointRouteIndex = -1; stalledActions = 0;
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
                exitDeferred = false;
            }
            hasGoal = HasWaypoint = false; Route.Clear(); waypointRouteIndex = -1;
        }

        // A valid instruction does not need another flood fill. Newly observed
        // exits can preempt exploration; other discoveries preserve a valid goal.
        bool selectGoal = !hasGoal || (!goal.IsExit && !exitDeferred &&
            (exitDiscovered || knowledgeChanged && sensors.Grid.ExitDiscoveryVersion > 0));
        bool routeSearched = false;
        while (true)
        {
            if (selectGoal)
            {
                GoalSelections++;
                if (selector.TrySelect(planner, position, excluded, !exitDeferred, out QaMapGoal selected) &&
                    (!hasGoal || selected.IsExit && !goal.IsExit))
                {
                    goal = selected; hasGoal = true; HasWaypoint = replanned = false;
                    Route.Clear(); waypointRouteIndex = -1; routeSearched = false;
                }
                selectGoal = false;
            }
            if (!hasGoal)
            {
                HasWaypoint = false; Route.Clear(); Status = "no_reachable_goal";
                return;
            }
            if (HasWaypoint) return;
            if (Route.Count == 0)
            {
                Route = planner.Route(position, goal.Cell); RouteSearches++; routeSearched = true;
            }
            if (TryAssignRouteWaypoint(planner, sensors, actor, position)) return;
            // A retained route may have been invalidated beyond its last waypoint.
            // Search once from the current position before abandoning its goal.
            if (!routeSearched)
            {
                Route.Clear();
                continue;
            }
            Abandon(); selectGoal = true; routeSearched = false;
        }
    }

    private QaObservedRoutePlanner GetPlanner(ModelSensorPackage sensors, CharacterController actor, out bool knowledgeChanged)
    {
        float clearance = actor.radius + actor.skinWidth + 0.05f;
        knowledgeChanged = cachedPlanner == null || cachedPlanner.Grid != sensors.Grid ||
            plannerVersion != sensors.Grid.NavigationVersion || plannerClearance != clearance;
        if (knowledgeChanged)
        {
            cachedPlanner = new QaObservedRoutePlanner(sensors.Grid, clearance);
            plannerVersion = sensors.Grid.NavigationVersion; plannerClearance = clearance; PlannerBuilds++;
        }
        return cachedPlanner;
    }

    private bool TryAssignRouteWaypoint(QaObservedRoutePlanner planner, ModelSensorPackage sensors,
        CharacterController actor, Vector3 position)
    {
        for (int i = Route.Count - 1; i >= 0; i--)
        {
            float distance = Distance(position, Route[i]);
            if (distance <= AcceptanceRadius || distance > MaxWaypointDistance ||
                !planner.SegmentKnown(position, Route[i]) || !sensors.CanUseWaypoint(Route[i], actor)) continue;
            Waypoint = Route[i]; HasWaypoint = true; WaypointVersion++; waypointRouteIndex = i;
            bestDistance = distance; stalledActions = 0; Status = goal.IsExit ? "exit" : "exploring";
            return true;
        }
        return false;
    }

    private void AssignExplorationEscape(QaObservedRoutePlanner planner, ModelSensorPackage sensors,
        CharacterController actor, Vector3 position)
    {
        hasGoal = HasWaypoint = replanned = false; Route.Clear(); waypointRouteIndex = -1;
        if (TryExplorationEscape(planner, sensors, actor, position, out Vector3 escape))
        {
            Waypoint = escape; HasWaypoint = exploringEscape = true; WaypointVersion++;
            Route.Add(escape); bestDistance = Distance(position, escape); stalledActions = 0; Status = "exploring";
        }
        else
        {
            exploringEscape = false;
            Status = "exploration_blocked";
        }
    }

    private bool TryExplorationEscape(QaObservedRoutePlanner planner, ModelSensorPackage sensors,
        CharacterController actor, Vector3 position, out Vector3 point)
    {
        point = default;
        float nearest = Mathf.Min(2f, MaxWaypointDistance);
        bool found = false;
        int extent = Mathf.CeilToInt(nearest / ModelSensorGrid.CellSize);
        Vector2Int start = ModelSensorGrid.WorldToCell(position);
        for (int z = -extent; z <= extent; z++)
        for (int x = -extent; x <= extent; x++)
        {
            Vector2Int cell = start + new Vector2Int(x, z);
            if (!planner.IsFree(cell)) continue;
            Vector3 candidate = QaObservedRoutePlanner.Point(cell, position.y);
            float distance = Distance(position, candidate);
            if (distance <= AcceptanceRadius + 0.05f || distance > nearest ||
                !planner.SegmentObserved(position, candidate) || !sensors.CanUseExplorationEscape(candidate, actor))
                continue;
            point = candidate; nearest = distance; found = true;
        }
        return found;
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
        hasGoal = HasWaypoint = false; replanned = false; Route.Clear(); waypointRouteIndex = -1;
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
