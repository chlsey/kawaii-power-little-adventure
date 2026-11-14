using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Training-only 1-3-room geometry using the dungeon manager's real player and exit.</summary>
public sealed class QaMixedRoomLayout
{
    public static readonly string[] Types = { "regular", "winding", "middle_walls", "sharp_corners", "L_shape" };
    private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    public RoomData[] Rooms { get; private set; }
    public Vector3 Spawn { get; private set; }
    public Vector3 Exit { get; private set; }
    private HashSet<Vector2Int>[] roomCells;

    [Serializable] public sealed class RoomData
    {
        public string type;
        public Vector3 center, entry, exit;
        public Vector3[] floor_cells;
    }

    public void Build(Generator generator, Room[] rooms, QaPlayerController player, int seed)
    {
        // Keep the existing player/exit and Room metadata; replace only generated geometry.
        QaExitZone[] exits = generator.GetComponentsInChildren<QaExitZone>();
        if (exits.Length != 1) throw new InvalidOperationException("Expected one game exit");
        foreach (Transform part in generator.GetComponentsInChildren<Transform>(true))
            if (part.CompareTag("Floor")) part.tag = "Untagged";
        foreach (Collider collider in generator.GetComponentsInChildren<Collider>())
        {
            if (collider.GetComponentInParent<QaPlayerController>() == player || collider.GetComponentInParent<QaExitZone>() != null) continue;
            if (collider.GetComponent<Room>() != null) collider.enabled = false;
            else collider.gameObject.SetActive(false);
        }

        var random = new System.Random(seed);
        int rotation = (seed / 15) % 4;
        var floor = new HashSet<Vector2Int>();
        roomCells = new HashSet<Vector2Int>[rooms.Length];
        Rooms = new RoomData[rooms.Length];
        var entries = new Vector2Int[rooms.Length];
        var ends = new Vector2Int[rooms.Length];
        for (int i = 0; i < rooms.Length; i++)
        {
            int kind;
            if (i == 0) kind = (seed / 3) % Types.Length;
            else kind = random.Next(Types.Length);
            Vector2Int center = new Vector2Int(i * 24, random.Next(-4, 5));
            var local = new HashSet<Vector2Int>();
            Vector2Int entry, end;
            CarveRoom(kind, random, local, out entry, out end);
            entries[i] = center + entry; ends[i] = center + end;
            roomCells[i] = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in local)
            {
                Vector2Int world = World(center + cell, rotation);
                roomCells[i].Add(world); floor.Add(world);
            }
            rooms[i].transform.position = Point(World(center, rotation));
            Rooms[i] = new RoomData { type = Types[kind], center = rooms[i].transform.position,
                entry = Point(World(entries[i], rotation)), exit = Point(World(ends[i], rotation)),
                floor_cells = Points(roomCells[i]) };
        }
        // Each connector stays between adjacent room bounding boxes, so it cannot bypass
        // the obstacle or turn inside either room. It is three units wide, with open doors.
        for (int i = 1; i < rooms.Length; i++)
        {
            int middle = (ends[i - 1].x + entries[i].x) / 2;
            var corridor = new HashSet<Vector2Int>();
            CarveLine(corridor, ends[i - 1], new Vector2Int(middle, ends[i - 1].y), 1);
            CarveLine(corridor, new Vector2Int(middle, ends[i - 1].y), new Vector2Int(middle, entries[i].y), 1);
            CarveLine(corridor, new Vector2Int(middle, entries[i].y), entries[i], 1);
            foreach (Vector2Int cell in corridor) floor.Add(World(cell, rotation));
        }
        Spawn = Rooms[0].entry; Exit = Rooms[Rooms.Length - 1].exit;
        CheckRoute(floor, Cell(Spawn), Cell(Exit));
        var root = new GameObject("Mixed training room geometry");
        root.transform.SetParent(generator.transform);
        foreach (Vector3 point in Points(floor))
        {
            Vector2Int cell = Cell(point);
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "Mixed room floor"; tile.tag = "Floor";
            tile.transform.SetParent(root.transform); tile.transform.position = point;
            tile.transform.localScale = new Vector3(1f, 0.2f, 1f);
            foreach (Vector2Int direction in Directions)
            {
                if (floor.Contains(cell + direction)) continue;
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Mixed room wall"; wall.transform.SetParent(root.transform);
                wall.transform.position = point + new Vector3(direction.x * 0.5f, 1.1f, direction.y * 0.5f);
                if (direction.x == 0) wall.transform.localScale = new Vector3(1.1f, 2f, 0.2f);
                else wall.transform.localScale = new Vector3(0.2f, 2f, 1.1f);
                wall.AddComponent<ModelSensorTarget>().HitId = QaObjectType.Wall;
            }
        }
        exits[0].transform.position = Exit;
        exits[0].RequireInteract = false;
        if (RoomIndex(Exit) != rooms.Length - 1) throw new InvalidOperationException("Exit outside final room");
    }

    private static void CarveRoom(int kind, System.Random random, HashSet<Vector2Int> cells, out Vector2Int entry, out Vector2Int end)
    {
        int low = -random.Next(4, 7), high = random.Next(4, 7);
        int mirror = random.Next(2) * 2 - 1;
        var nodes = new List<Vector2Int>();
        if (kind == 0 || kind == 2)
        {
            int half = random.Next(5, 9);
            for (int x = -7; x <= 7; x++)
                for (int z = -half; z <= half; z++) cells.Add(new Vector2Int(x, z));
            if (kind == 2)
            {
                int count = random.Next(1, 3);
                for (int j = 0; j < count; j++)
                {
                    int wallX = count == 1 ? 0 : -2 + j * 5;
                    int bottom = -half + 2 + j;
                    int top = half - 2 + j;
                    for (int z = bottom; z <= top; z++) cells.Remove(new Vector2Int(wallX, z));
                }
            }
            entry = new Vector2Int(-7, random.Next(-half + 2, half - 1));
            end = new Vector2Int(7, random.Next(-half + 2, half - 1));
            return;
        }
        if (kind == 1)
        {
            int left = random.Next(-4, -2), right = random.Next(1, 3);
            nodes.Add(new Vector2Int(-7, low)); nodes.Add(new Vector2Int(left, low));
            nodes.Add(new Vector2Int(left, high)); nodes.Add(new Vector2Int(right, high));
            nodes.Add(new Vector2Int(right, low)); nodes.Add(new Vector2Int(7, low));
            nodes.Add(new Vector2Int(7, high));
        }
        else if (kind == 3)
        {
            nodes.Add(new Vector2Int(-7, low)); nodes.Add(new Vector2Int(-2, low));
            nodes.Add(new Vector2Int(-2, high)); nodes.Add(new Vector2Int(3, high));
            nodes.Add(new Vector2Int(3, 0)); nodes.Add(new Vector2Int(7, 0));
        }
        else
        {
            nodes.Add(new Vector2Int(-7, low)); nodes.Add(new Vector2Int(5, low));
            nodes.Add(new Vector2Int(5, high)); nodes.Add(new Vector2Int(7, high));
        }
        for (int i = 0; i < nodes.Count; i++) nodes[i] = new Vector2Int(nodes[i].x, nodes[i].y * mirror);
        int radius = kind == 4 ? 2 : 1;
        for (int i = 1; i < nodes.Count; i++) CarveLine(cells, nodes[i - 1], nodes[i], radius);
        entry = nodes[0]; end = nodes[nodes.Count - 1];
    }

    private static void CarveLine(HashSet<Vector2Int> cells, Vector2Int start, Vector2Int end, int radius)
    {
        if (start.x != end.x && start.y != end.y) throw new InvalidOperationException("Expected orthogonal connector");
        Vector2Int direction = new Vector2Int(Math.Sign(end.x - start.x), Math.Sign(end.y - start.y));
        int length = Math.Abs(end.x - start.x) + Math.Abs(end.y - start.y);
        for (int step = 0; step <= length; step++)
            for (int x = -radius; x <= radius; x++)
                for (int z = -radius; z <= radius; z++) cells.Add(start + direction * step + new Vector2Int(x, z));
    }

    private static void CheckRoute(HashSet<Vector2Int> floor, Vector2Int start, Vector2Int goal)
    {
        var queue = new Queue<Vector2Int>(); queue.Enqueue(start);
        var distance = new Dictionary<Vector2Int, int> { [start] = 0 };
        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            if (cell == goal)
            {
                if (distance[cell] > 220) throw new InvalidOperationException("Mixed route exceeds the sixty-second control budget");
                return;
            }
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = cell + direction;
                if (!floor.Contains(next) || distance.ContainsKey(next)) continue;
                distance[next] = distance[cell] + 1; queue.Enqueue(next);
            }
        }
        throw new InvalidOperationException("Disconnected mixed room layout");
    }

    public int RoomIndex(Vector3 point)
    {
        Vector2Int cell = Cell(point);
        for (int i = 0; i < roomCells.Length; i++) if (roomCells[i].Contains(cell)) return i;
        return -1;
    }
    private static Vector2Int World(Vector2Int point, int rotation)
    {
        for (int i = 0; i < rotation; i++) point = new Vector2Int(-point.y, point.x);
        return point + new Vector2Int(50, 80);
    }
    private static Vector2Int Cell(Vector3 point) => new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.z));
    private static Vector3 Point(Vector2Int cell) => new Vector3(cell.x, 0f, cell.y);
    private static Vector3[] Points(HashSet<Vector2Int> cells)
    {
        var ordered = new List<Vector2Int>(cells);
        ordered.Sort((a, b) => { if (a.x == b.x) return a.y.CompareTo(b.y); return a.x.CompareTo(b.x); });
        return ordered.ConvertAll(Point).ToArray();
    }
}
