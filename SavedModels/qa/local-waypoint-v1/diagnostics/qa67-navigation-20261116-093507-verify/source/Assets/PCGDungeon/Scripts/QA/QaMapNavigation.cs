using System.Collections.Generic;
using UnityEngine;

public readonly struct QaMapGoal
{
    public readonly Vector2Int Cell;
    public readonly bool IsExit;
    public QaMapGoal(Vector2Int cell, bool isExit) { Cell = cell; IsExit = isExit; }
}

/// <summary>Goal selection sees sensor knowledge only, never scene/generator destinations.</summary>
public interface IMapGoalSelector
{
    bool TrySelect(QaObservedRoutePlanner planner, Vector3 position, IList<Vector3> excluded,
        bool allowExit, out QaMapGoal goal);
}

public sealed class QaFrontierGoalSelector : IMapGoalSelector
{
    public bool TrySelect(QaObservedRoutePlanner planner, Vector3 position, IList<Vector3> excluded,
        bool allowExit, out QaMapGoal goal)
    {
        goal = default;
        bool found = false;
        float nearest = float.PositiveInfinity;
        foreach (Vector2Int cell in planner.Reachable(position))
        {
            ModelMapCell knowledge = planner.Grid.ReadCell(cell);
            bool isExit = knowledge.Exit && allowExit;
            Vector3 point = QaObservedRoutePlanner.Point(cell, position.y);
            float distance = QaNavigationCoordinator.Distance(position, point);
            if (!isExit)
            {
                if (knowledge.Exit || distance < 1f || !planner.IsFrontier(cell))
                    continue;
                bool skip = false;
                foreach (Vector3 old in excluded)
                    if (QaNavigationCoordinator.Distance(point, old) <= 0.5f) skip = true;
                if (skip) continue;
            }
            if (found && goal.IsExit && !isExit) continue;
            if (!found || (isExit && !goal.IsExit) || distance < nearest)
            {
                goal = new QaMapGoal(cell, isExit);
                nearest = distance;
                found = true;
            }
        }
        return found;
    }
}

/// <summary>Eight-neighbour A* over observed free cells with conservative wall inflation.</summary>
public sealed class QaObservedRoutePlanner
{
    // Avoid the Mono generic Vector2Int comparer path seen in native navigation crashes.
    // Keep coordinate equality and Unity's hash unchanged so route ordering is preserved.
    private sealed class CellEquality : IEqualityComparer<Vector2Int>
    {
        public bool Equals(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
        public int GetHashCode(Vector2Int cell) => cell.GetHashCode();
    }
    private static readonly IEqualityComparer<Vector2Int> CellComparer = new CellEquality();
    public ModelSensorGrid Grid { get; }
    private readonly HashSet<Vector2Int> free = new HashSet<Vector2Int>(CellComparer);
    private readonly HashSet<Vector2Int> inflated = new HashSet<Vector2Int>(CellComparer);
    private readonly HashSet<Vector2Int> refined = new HashSet<Vector2Int>(CellComparer);
    private readonly Dictionary<Vector2Int, Dictionary<Vector2Int, bool>> edges =
        new Dictionary<Vector2Int, Dictionary<Vector2Int, bool>>(CellComparer);
    private readonly System.Func<Vector3, bool> canOccupy;
    private readonly System.Func<Vector3, Vector3, bool> canTraverse;
    private readonly float height;
    public int RefinementChecks { get; private set; }
    public int RefinedCells => refined.Count;

    public QaObservedRoutePlanner(ModelSensorGrid grid, float clearance,
        System.Func<Vector3, bool> canOccupy = null,
        System.Func<Vector3, Vector3, bool> canTraverse = null, float height = 0f)
    {
        Grid = grid;
        this.canOccupy = canOccupy; this.canTraverse = canTraverse; this.height = height;
        var walls = new List<Vector2Int>();
        for (int z = 0; z < grid.Depth; z++)
        for (int x = 0; x < grid.Width; x++)
        {
            Vector2Int cell = grid.MinCell + new Vector2Int(x, z);
            ModelMapCell value = grid.ReadCell(cell);
            if (value.Observed && !value.Wall) free.Add(cell);
            if (value.Wall) walls.Add(cell);
        }
        int extent = Mathf.CeilToInt(clearance / ModelSensorGrid.CellSize + 0.5f);
        foreach (Vector2Int wall in walls)
        for (int z = -extent; z <= extent; z++)
        for (int x = -extent; x <= extent; x++)
        {
            // Distance from candidate centre to the full occupied wall-cell square.
            float dx = Mathf.Max(0f, (Mathf.Abs(x) - 0.5f) * ModelSensorGrid.CellSize);
            float dz = Mathf.Max(0f, (Mathf.Abs(z) - 0.5f) * ModelSensorGrid.CellSize);
            if (dx * dx + dz * dz <= clearance * clearance)
            {
                Vector2Int cell = wall + new Vector2Int(x, z);
                if (free.Remove(cell)) inflated.Add(cell);
            }
        }
    }

    public static Vector3 Point(Vector2Int cell, float height) =>
        new Vector3((cell.x + 0.5f) * ModelSensorGrid.CellSize, height, (cell.y + 0.5f) * ModelSensorGrid.CellSize);

    public bool IsFree(Vector2Int cell)
    {
        if (free.Contains(cell)) return true;
        if (canOccupy == null || !inflated.Remove(cell)) return false;
        RefinementChecks++;
        if (!canOccupy(Point(cell, height))) return false;
        free.Add(cell); refined.Add(cell);
        return true;
    }

    // Does not run queries: diagnostic snapshots must not alter planning state.
    public int CellState(Vector2Int cell)
    {
        if (refined.Contains(cell)) return 2;
        if (free.Contains(cell)) return 1;
        if (inflated.Contains(cell)) return -1;
        return 0;
    }

    private bool EdgeClear(Vector2Int from, Vector2Int to)
    {
        if (canTraverse == null || !refined.Contains(from) && !refined.Contains(to)) return true;
        if (!edges.TryGetValue(from, out Dictionary<Vector2Int, bool> destinations))
        {
            destinations = new Dictionary<Vector2Int, bool>(CellComparer);
            edges.Add(from, destinations);
        }
        if (!destinations.TryGetValue(to, out bool clear))
        {
            clear = canTraverse(Point(from, height), Point(to, height));
            destinations.Add(to, clear);
        }
        return clear;
    }

    // Escape paths may cross the inflated margin, never an unknown or wall cell.
    public bool SegmentObserved(Vector3 from, Vector3 to)
    {
        int steps = Mathf.CeilToInt(QaNavigationCoordinator.Distance(from, to) / 0.02f);
        for (int i = 0; i <= steps; i++)
        {
            Vector2Int cell = ModelSensorGrid.WorldToCell(Vector3.Lerp(from, to, i / (float)Mathf.Max(1, steps)));
            ModelMapCell value = Grid.ReadCell(cell);
            if (!value.Observed || value.Wall) return false;
        }
        return true;
    }

    public bool SegmentKnown(Vector3 from, Vector3 to)
    {
        int steps = Mathf.CeilToInt(QaNavigationCoordinator.Distance(from, to) / 0.02f);
        Vector2Int previous = ModelSensorGrid.WorldToCell(from);
        for (int i = 0; i <= steps; i++)
        {
            Vector2Int cell = ModelSensorGrid.WorldToCell(Vector3.Lerp(from, to, i / (float)Mathf.Max(1, steps)));
            if (!IsFree(cell)) return false;
            if (cell.x != previous.x && cell.y != previous.y &&
                (!IsFree(new Vector2Int(cell.x, previous.y)) ||
                 !IsFree(new Vector2Int(previous.x, cell.y)))) return false;
            if (cell != previous && !EdgeClear(previous, cell)) return false;
            previous = cell;
        }
        return true;
    }

    private IEnumerable<Vector2Int> Neighbours(Vector2Int cell)
    {
        for (int z = -1; z <= 1; z++)
        for (int x = -1; x <= 1; x++)
        {
            if (x == 0 && z == 0) continue;
            Vector2Int next = cell + new Vector2Int(x, z);
            if (!IsFree(next)) continue;
            if (x != 0 && z != 0 && (!IsFree(cell + new Vector2Int(x, 0)) ||
                !IsFree(cell + new Vector2Int(0, z)))) continue;
            if (!EdgeClear(cell, next)) continue;
            yield return next;
        }
    }

    public HashSet<Vector2Int> Reachable(Vector3 position)
    {
        var reached = new HashSet<Vector2Int>(CellComparer);
        var queue = new Queue<Vector2Int>();
        Vector2Int start = ModelSensorGrid.WorldToCell(position);
        if (!IsFree(start)) return reached;
        reached.Add(start);
        queue.Enqueue(start);
        while (queue.Count > 0)
        foreach (Vector2Int next in Neighbours(queue.Dequeue()))
            if (reached.Add(next)) queue.Enqueue(next);
        return reached;
    }

    public bool IsFrontier(Vector2Int cell)
    {
        for (int z = -1; z <= 1; z++)
        for (int x = -1; x <= 1; x++)
            if (!Grid.ReadCell(cell + new Vector2Int(x, z)).Observed) return true;
        return false;
    }

    public List<Vector3> Route(Vector3 position, Vector2Int target)
    {
        Vector2Int start = ModelSensorGrid.WorldToCell(position);
        var open = new List<Vector2Int>();
        var closed = new HashSet<Vector2Int>(CellComparer);
        var cost = new Dictionary<Vector2Int, float>();
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        if (!IsFree(start) || !IsFree(target)) return new List<Vector3>();
        open.Add(start);
        cost[start] = 0f;
        while (open.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < open.Count; i++)
                if (cost[open[i]] + Vector2Int.Distance(open[i], target) <
                    cost[open[best]] + Vector2Int.Distance(open[best], target)) best = i;
            Vector2Int cell = open[best];
            open.RemoveAt(best);
            if (cell == target)
            {
                var route = new List<Vector3>();
                while (cell != start)
                {
                    route.Add(Point(cell, position.y));
                    cell = parent[cell];
                }
                route.Reverse();
                return route;
            }
            closed.Add(cell);
            foreach (Vector2Int next in Neighbours(cell))
            {
                if (closed.Contains(next)) continue;
                float candidate = cost[cell] + Vector2Int.Distance(cell, next);
                if (cost.TryGetValue(next, out float prior) && candidate >= prior) continue;
                cost[next] = candidate;
                parent[next] = cell;
                if (!open.Contains(next)) open.Add(next);
            }
        }
        return new List<Vector3>();
    }
}
