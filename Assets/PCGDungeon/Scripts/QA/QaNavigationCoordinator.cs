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
    private const float RecoveryAcceptanceRadius = 0.2f;
    private const float CornerAcceptanceRadius = 0.05f;
    private readonly List<NavigationSample> recoveryHistory = new List<NavigationSample>();
    private int loopReturns, recoveryFailures, escapeStalledActions;
    private float escapeBestDistance;
    private bool recoveryInterior, avoidEscape, resumingRoute;
    private Vector3 failedEscape, lastEscape;
    private float goalBestDistance;
    private int goalNoProgressActions;
    private bool goalRecoveryActive;
    private struct NavigationSample
    {
        public Vector3 Position, Target;
        public bool Recovering;
    }
    private QaObservedRoutePlanner cachedPlanner;
    private int plannerVersion, exitDiscoveryVersion, waypointRouteIndex = -1;
    private float plannerClearance;
    private QaNavigationCapsule plannerBody;
    private QaObservedRoutePlanner failedSelectionPlanner;
    private Vector3 failedSelectionPosition;
    private int failedSelectionExcluded, failedSelectionExitVersion;
    private bool failedSelectionExitDeferred;
    private int completedRefinementChecks, completedRefinedCells;
    public int PlannerBuilds { get; private set; }
    public int GoalSelections { get; private set; }
    public int RouteSearches { get; private set; }
    public int GoalSearchSkips { get; private set; }
    public double PlanningMilliseconds { get; private set; }
    public QaObservedRoutePlanner PlannerSnapshot => cachedPlanner;
    public int RefinementChecks => completedRefinementChecks + (cachedPlanner?.RefinementChecks ?? 0);
    public int RefinedCells => completedRefinedCells + (cachedPlanner?.RefinedCells ?? 0);
    // Optional frozen-evaluation observer; never supplies navigation decisions.
    public event System.Action<string> DiagnosticEvent;
    public bool HasWaypoint { get; private set; }
    public Vector3 Waypoint { get; private set; }
    public int WaypointVersion { get; private set; }
    public int Replans { get; private set; }
    public int AbandonedGoals { get; private set; }
    public int ExcludedCount => excluded.Count;
    public Vector3[] ExcludedFrontiers => excluded.ToArray();
    public bool HasGoal => hasGoal;
    public Vector2Int GoalCell => goal.Cell;
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
        GoalSearchSkips = 0; PlanningMilliseconds = 0; failedSelectionPlanner = null;
        completedRefinementChecks = completedRefinedCells = 0;
        recoveryHistory.Clear(); loopReturns = recoveryFailures = escapeStalledActions = 0;
        recoveryInterior = avoidEscape = resumingRoute = false;
        goalNoProgressActions = 0; goalRecoveryActive = false;
    }

    public void Advance(ModelSensorPackage sensors, CharacterController actor, bool actionCompleted)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        try { AdvanceCore(sensors, actor, actionCompleted); }
        finally
        {
            PlanningMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - started) *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }
    }

    private void AdvanceCore(ModelSensorPackage sensors, CharacterController actor, bool actionCompleted)
    {
        Vector3 position = actor.transform.position;
        var planner = GetPlanner(sensors, actor, out bool knowledgeChanged);
        bool exitDiscovered = exitDiscoveryVersion != sensors.Grid.ExitDiscoveryVersion;
        bool startFree = planner.IsFree(ModelSensorGrid.WorldToCell(position)) &&
            sensors.CanOccupyObservedPoint(position, new QaNavigationCapsule(actor));

        // Recovery must improve the retained goal, even when a full cycle is
        // longer than the short position/target history.
        if (actionCompleted && hasGoal)
        {
            float distance = Distance(position, QaObservedRoutePlanner.Point(goal.Cell, position.y));
            if (goalBestDistance - distance >= 0.05f)
            {
                goalBestDistance = distance; goalNoProgressActions = 0;
            }
            else goalNoProgressActions++;
            if (goalRecoveryActive && goalNoProgressActions >= 60)
            {
                DiagnosticEvent?.Invoke("goal_progress_stalled");
                EscalateRecovery();
                AssignExplorationEscape(planner, sensors, actor, position);
                return;
            }
        }

        // Keep recurrence evidence across normal/recovery waypoint changes.
        if (actionCompleted && HasWaypoint && RepeatsRecoveryLoop(position))
        {
            DiagnosticEvent?.Invoke("oscillation_detected");
            EscalateRecovery();
            AssignExplorationEscape(planner, sensors, actor, position);
            return;
        }

        // Hold an escape until arrival in navigable space, even if the starting
        // cell becomes free partway through the move.
        if (exploringEscape && HasWaypoint)
        {
            float distance = Distance(position, Waypoint);
            bool arrived = distance <= RecoveryAcceptanceRadius;
            if (recoveryInterior) arrived = distance <= AcceptanceRadius;
            bool valid = planner.IsFree(ModelSensorGrid.WorldToCell(Waypoint)) &&
                planner.SegmentObserved(position, Waypoint, true) && sensors.CanUseExplorationEscape(Waypoint, actor);
            if (arrived && startFree && (!recoveryInterior || IsRecoveryInterior(planner, position)))
            {
                DiagnosticEvent?.Invoke("escape_arrived_free");
                exploringEscape = HasWaypoint = false;
                resumingRoute = true; waypointRouteIndex = -1;
                knowledgeChanged = true;
            }
            else
            {
                if (actionCompleted)
                {
                    if (escapeBestDistance - distance >= 0.05f)
                    {
                        escapeBestDistance = distance; escapeStalledActions = 0;
                    }
                    else escapeStalledActions++;
                }
                if (valid && escapeStalledActions < 20) return;
                if (valid) DiagnosticEvent?.Invoke("escape_stalled");
                else DiagnosticEvent?.Invoke("escape_invalid");
                EscalateRecovery();
                AssignExplorationEscape(planner, sensors, actor, position);
                return;
            }
        }
        // A blocked cell centre does not invalidate a clear segment from the
        // actor's actual position. Retain its instruction only while observed
        // space and the full normal-clearance capsule sweep both remain valid.
        bool waypointObserved = HasWaypoint && planner.SegmentObserved(position, Waypoint);
        bool waypointClear = waypointObserved && sensors.CanUseWaypoint(Waypoint, actor);
        if (!startFree && !waypointClear)
        {
            DiagnosticEvent?.Invoke("start_cell_blocked");
            AssignExplorationEscape(planner, sensors, actor, position);
            return;
        }
        exitDiscoveryVersion = sensors.Grid.ExitDiscoveryVersion;

        if (HasWaypoint)
        {
            float distance = Distance(position, Waypoint);
            bool arrived = distance <= AcceptanceRadius;
            // Near a corner, arriving early can leave every remaining route
            // segment occluded. Keep steering to the turn until a handoff is clear.
            if (arrived && distance > CornerAcceptanceRadius && waypointRouteIndex >= 0 &&
                waypointRouteIndex < Route.Count - 1)
            {
                arrived = false;
                for (int i = waypointRouteIndex + 1; i < Route.Count; i++)
                {
                    float nextDistance = Distance(position, Route[i]);
                    if (nextDistance > AcceptanceRadius && nextDistance <= MaxWaypointDistance &&
                        planner.SegmentKnown(position, Route[i]) && sensors.CanUseWaypoint(Route[i], actor))
                    {
                        arrived = true;
                        break;
                    }
                }
            }
            if (arrived)
            {
                DiagnosticEvent?.Invoke("waypoint_arrived");
                // Consume the reached route prefix, keeping the remaining route.
                if (waypointRouteIndex >= 0) Route.RemoveRange(0, waypointRouteIndex + 1);
                HasWaypoint = false; waypointRouteIndex = -1; replanned = false;
                recoveryFailures = 0; avoidEscape = false;
                goalRecoveryActive = false; goalNoProgressActions = 0;
            }
            else if (!waypointObserved)
            {
                DiagnosticEvent?.Invoke("waypoint_segment_invalid");
                HasWaypoint = false; Route.Clear(); waypointRouteIndex = -1;
            }
            else if (!waypointClear)
            {
                DiagnosticEvent?.Invoke("waypoint_clearance_invalid");
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
                    DiagnosticEvent?.Invoke(replanned ? "stalled_abandon" : "stalled_replan");
                    HasWaypoint = false; Route.Clear(); waypointRouteIndex = -1; stalledActions = 0;
                    if (!replanned) { replanned = true; Replans++; }
                    else Abandon();
                }
            }
        }

        if (hasGoal && (Distance(position, QaObservedRoutePlanner.Point(goal.Cell, position.y)) <= AcceptanceRadius ||
            !planner.IsFree(goal.Cell)))
        {
            DiagnosticEvent?.Invoke(planner.IsFree(goal.Cell) ? "goal_reached" : "goal_cell_invalid");
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
                bool unchangedFailure = !hasGoal && failedSelectionPlanner == planner &&
                    failedSelectionPosition == position && failedSelectionExcluded == excluded.Count &&
                    failedSelectionExitVersion == sensors.Grid.ExitDiscoveryVersion &&
                    failedSelectionExitDeferred == exitDeferred;
                if (unchangedFailure) GoalSearchSkips++;
                else
                {
                    GoalSelections++;
                    if (selector.TrySelect(planner, position, excluded, !exitDeferred, out QaMapGoal selected) &&
                    (!hasGoal || selected.IsExit && !goal.IsExit))
                    {
                        DiagnosticEvent?.Invoke(selected.IsExit ? "select_exit" : "select_frontier");
                        goal = selected; hasGoal = true; HasWaypoint = replanned = false;
                        Route.Clear(); waypointRouteIndex = -1; routeSearched = false;
                        recoveryHistory.Clear(); loopReturns = recoveryFailures = 0; avoidEscape = false;
                        stalledActions = 0; resumingRoute = false;
                        goalBestDistance = Distance(position, QaObservedRoutePlanner.Point(goal.Cell, position.y));
                        goalNoProgressActions = 0; goalRecoveryActive = false;
                        failedSelectionPlanner = null;
                    }
                    else if (!hasGoal)
                    {
                        failedSelectionPlanner = planner; failedSelectionPosition = position;
                        failedSelectionExcluded = excluded.Count;
                        failedSelectionExitVersion = sensors.Grid.ExitDiscoveryVersion;
                        failedSelectionExitDeferred = exitDeferred;
                    }
                }
                selectGoal = false;
            }
            if (!hasGoal)
            {
                HasWaypoint = false; Route.Clear(); Status = "no_reachable_goal";
                DiagnosticEvent?.Invoke("no_reachable_goal");
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
        var body = new QaNavigationCapsule(actor);
        float clearance = body.Clearance;
        knowledgeChanged = cachedPlanner == null || cachedPlanner.Grid != sensors.Grid ||
            plannerVersion != sensors.Grid.NavigationVersion || Mathf.Abs(plannerClearance - clearance) > 0.0001f ||
            Mathf.Abs(plannerBody.Position.y - body.Position.y) > 0.0001f ||
            plannerBody.Top - plannerBody.Position != body.Top - body.Position ||
            plannerBody.Bottom - plannerBody.Position != body.Bottom - body.Position;
        if (knowledgeChanged)
        {
            completedRefinementChecks += cachedPlanner?.RefinementChecks ?? 0;
            completedRefinedCells += cachedPlanner?.RefinedCells ?? 0;
            cachedPlanner = new QaObservedRoutePlanner(sensors.Grid, clearance,
                point => sensors.CanOccupyObservedPoint(point, body),
                (from, to) => sensors.CanTraverseObservedSegment(from, to, body), body.Position.y);
            plannerBody = body;
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
            bestDistance = distance;
            if (!resumingRoute) stalledActions = 0;
            resumingRoute = false; Status = goal.IsExit ? "exit" : "exploring";
            DiagnosticEvent?.Invoke("route_assigned");
            return true;
        }
        return false;
    }

    private void AssignExplorationEscape(QaObservedRoutePlanner planner, ModelSensorPackage sensors,
        CharacterController actor, Vector3 position)
    {
        HasWaypoint = false; waypointRouteIndex = -1;
        if (!goalRecoveryActive) goalNoProgressActions = 0;
        goalRecoveryActive = true;
        if (TryExplorationEscape(planner, sensors, actor, position, out Vector3 escape))
        {
            Waypoint = escape; HasWaypoint = exploringEscape = true; WaypointVersion++;
            lastEscape = escape; escapeBestDistance = Distance(position, escape); escapeStalledActions = 0;
            Status = "recovering";
            DiagnosticEvent?.Invoke("escape_assigned");
        }
        else
        {
            exploringEscape = false;
            Status = "exploration_blocked";
            DiagnosticEvent?.Invoke("exploration_blocked");
        }
    }

    private bool TryExplorationEscape(QaObservedRoutePlanner planner, ModelSensorPackage sensors,
        CharacterController actor, Vector3 position, out Vector3 point)
    {
        point = default;
        float limit = Mathf.Min(2f, MaxWaypointDistance);
        float bestScore = float.PositiveInfinity;
        bool found = false;
        bool foundInterior = false;
        int extent = Mathf.CeilToInt(limit / ModelSensorGrid.CellSize);
        Vector2Int start = ModelSensorGrid.WorldToCell(position);
        for (int z = -extent; z <= extent; z++)
        for (int x = -extent; x <= extent; x++)
        {
            Vector2Int cell = start + new Vector2Int(x, z);
            if (!planner.IsFree(cell)) continue;
            Vector3 candidate = QaObservedRoutePlanner.Point(cell, position.y);
            float distance = Distance(position, candidate);
            if (distance <= RecoveryAcceptanceRadius + 0.05f || distance > limit ||
                avoidEscape && Distance(candidate, failedEscape) <= 0.75f)
                continue;
            bool interior = IsRecoveryInterior(planner, candidate);
            if (foundInterior && !interior) continue;
            float score = distance;
            if (hasGoal)
            {
                Vector3 destination = QaObservedRoutePlanner.Point(goal.Cell, position.y);
                score += 0.75f * (Distance(candidate, destination) - Distance(position, destination));
            }
            if (found && interior == foundInterior && score >= bestScore) continue;
            if (!planner.SegmentObserved(position, candidate, true) || !sensors.CanUseExplorationEscape(candidate, actor)) continue;
            point = candidate; bestScore = score; foundInterior = interior; found = true;
        }
        recoveryInterior = foundInterior;
        return found;
    }

    private static bool IsRecoveryInterior(QaObservedRoutePlanner planner, Vector3 position)
    {
        // Prefer half a unit of observed planning room beyond normal clearance.
        // Narrow spaces can still use a normally clear fallback destination.
        Vector2Int cell = ModelSensorGrid.WorldToCell(position);
        for (int z = -2; z <= 2; z++)
        for (int x = -2; x <= 2; x++)
            if (x * x + z * z <= 4 && !planner.IsFree(cell + new Vector2Int(x, z))) return false;
        return true;
    }

    private bool RepeatsRecoveryLoop(Vector3 position)
    {
        bool repeated = false;
        bool recoverySeen = exploringEscape;
        for (int i = recoveryHistory.Count - 1; i >= 0; i--)
        {
            recoverySeen |= recoveryHistory[i].Recovering;
            if (i < recoveryHistory.Count - 1 && recoverySeen &&
                Distance(position, recoveryHistory[i].Position) <= 0.1f &&
                Distance(Waypoint, recoveryHistory[i].Target) <= 0.25f &&
                Distance(position, recoveryHistory[recoveryHistory.Count - 1].Position) >= 0.05f)
                repeated = true;
        }
        if (repeated) loopReturns++;
        else loopReturns = 0;
        if (recoveryHistory.Count == 32) recoveryHistory.RemoveAt(0);
        recoveryHistory.Add(new NavigationSample { Position = position, Target = Waypoint, Recovering = exploringEscape });
        return loopReturns >= 3;
    }

    private void EscalateRecovery()
    {
        failedEscape = lastEscape; avoidEscape = true; recoveryFailures++;
        HasWaypoint = exploringEscape = false;
        recoveryHistory.Clear(); loopReturns = 0;
        goalNoProgressActions = 0;
        // Repeated recovery failure can abandon a goal, but entering recovery cannot.
        if (recoveryFailures >= 3)
        {
            DiagnosticEvent?.Invoke("recovery_exhausted");
            Abandon(); recoveryFailures = 0;
        }
    }

    private void Abandon()
    {
        if (hasGoal)
        {
            DiagnosticEvent?.Invoke(goal.IsExit ? "abandon_exit" : "abandon_frontier");
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
