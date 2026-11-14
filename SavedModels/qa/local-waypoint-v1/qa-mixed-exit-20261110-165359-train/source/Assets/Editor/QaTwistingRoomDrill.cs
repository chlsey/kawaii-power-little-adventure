using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Seeded, enclosed winding rooms for single-target sharp-turn movement drills.</summary>
public sealed class QaTwistingRoomDrill
{
    private GameObject walls;
    private static readonly Vector2Int[] Directions =
        { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

    public Vector3[] Nodes { get; private set; }
    public Vector3[] FloorCells { get; private set; }
    public int Spacing { get; private set; }

    public void Generate(int seed, float height)
    {
        if (walls != null)
        {
            // Disable old colliders immediately; destruction completes at the frame boundary.
            walls.SetActive(false);
            Object.Destroy(walls);
        }
        var random = new System.Random(seed);
        Spacing = random.Next(5, 8);
        int count = random.Next(7, 12);
        var route = new List<Vector2Int> { Vector2Int.zero };
        var used = new HashSet<Vector2Int> { Vector2Int.zero };
        if (!Extend(route, used, count, random))
            throw new InvalidOperationException("Could not generate twisting drill route");

        Nodes = new Vector3[count];
        var floor = new HashSet<Vector2Int>();
        for (int i = 0; i < count; i++)
            Nodes[i] = new Vector3(route[i].x * Spacing, height, route[i].y * Spacing);
        // Carve a three-unit-wide connected room along the self-avoiding route. Every
        // successive segment turns ninety degrees; nonadjacent segments cannot cross.
        for (int i = 1; i < count; i++)
        {
            Vector2Int start = route[i - 1] * Spacing;
            Vector2Int direction = route[i] - route[i - 1];
            for (int step = 0; step <= Spacing; step++)
            {
                Vector2Int center = start + direction * step;
                for (int x = -1; x <= 1; x++)
                    for (int z = -1; z <= 1; z++) floor.Add(center + new Vector2Int(x, z));
            }
        }
        var cells = new List<Vector2Int>(floor);
        cells.Sort((a, b) =>
        {
            if (a.x == b.x) return a.y.CompareTo(b.y);
            return a.x.CompareTo(b.x);
        });
        FloorCells = new Vector3[cells.Count];
        walls = new GameObject("Seeded twisting room walls");
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            FloorCells[i] = new Vector3(cell.x, height, cell.y);
            foreach (Vector2Int direction in Directions)
            {
                if (floor.Contains(cell + direction)) continue;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Twisting room wall";
                wall.transform.SetParent(walls.transform);
                wall.transform.position = new Vector3(cell.x + direction.x * 0.5f,
                    height + 1f, cell.y + direction.y * 0.5f);
                if (direction.x != 0) wall.transform.localScale = new Vector3(0.2f, 2f, 1.1f);
                else wall.transform.localScale = new Vector3(1.1f, 2f, 0.2f);
                wall.AddComponent<ModelSensorTarget>().HitId = QaObjectType.Wall;
            }
        }
    }

    private static bool Extend(List<Vector2Int> route, HashSet<Vector2Int> used, int count, System.Random random)
    {
        if (route.Count == count) return true;
        int[] order = { 0, 1, 2, 3 };
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int temporary = order[i]; order[i] = order[j]; order[j] = temporary;
        }
        foreach (int index in order)
        {
            Vector2Int direction = Directions[index];
            if (route.Count > 1)
            {
                Vector2Int previous = route[route.Count - 1] - route[route.Count - 2];
                if (previous.x * direction.x + previous.y * direction.y != 0) continue;
            }
            Vector2Int next = route[route.Count - 1] + direction;
            if (Mathf.Abs(next.x) > 2 || Mathf.Abs(next.y) > 2 || used.Contains(next)) continue;
            route.Add(next); used.Add(next);
            if (Extend(route, used, count, random)) return true;
            route.RemoveAt(route.Count - 1); used.Remove(next);
        }
        return false;
    }

    public Vector3 Target(int leg)
    {
        // Traverse to the far endpoint, reverse, and repeat without resetting movement.
        int segmentCount = Nodes.Length - 1;
        int index = (leg + 1) % (2 * segmentCount);
        if (index > segmentCount) index = 2 * segmentCount - index;
        return Nodes[index];
    }

    public bool IsEndpoint(int leg) => (leg + 1) % (Nodes.Length - 1) == 0;
}
