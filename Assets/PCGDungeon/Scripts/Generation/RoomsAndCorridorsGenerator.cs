using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Stage one of the generic dungeon: carves randomly placed rooms, then the
// corridors between nearby rooms, out of a solid grid. Creates no GameObjects.
public class RoomsAndCorridorsGenerator : IPredungeonGenerator
{
    private const int MaxFailedPlacementsPerRoom = 500;

    public int AreaWidth;
    public int AreaHeight;
    public int RoomCount;

    public int MinRoomWidth;
    public int MaxRoomWidth;
    public int MinRoomHeight;
    public int MaxRoomHeight;

    public int MinBossRoomWidth;
    public int MaxBossRoomWidth;
    public int MinBossRoomHeight;
    public int MaxBossRoomHeight;

    public int MinDistanceBetweenRooms;
    public int AdjacentRoomsToConnect;

    // Null when the rooms could not be fitted into the area.
    public Predungeon Generate()
    {
        Predungeon predungeon = new Predungeon(AreaWidth, AreaHeight);

        if (!PlaceRooms(predungeon))
            return null;

        ConnectNearestRooms(predungeon);
        ConnectIslands(predungeon);
        predungeon.SpawnRoom = predungeon.FarthestRoomFrom(predungeon.BossRoom);

        return predungeon;
    }

    private bool PlaceRooms(Predungeon predungeon)
    {
        predungeon.BossRoom = Random.Range(0, RoomCount);
        int failedPlacements = 0;

        for (int i = 0; i < RoomCount; i++)
        {
            RectInt rect;
            while (!TryPlaceRoom(predungeon, i == predungeon.BossRoom, out rect))
            {
                failedPlacements++;
                if (failedPlacements > MaxFailedPlacementsPerRoom * RoomCount)
                    return false;
            }

            predungeon.CarveRect(rect);
            predungeon.Rooms.Add(new PreRoom(rect));
        }

        return true;
    }

    private bool TryPlaceRoom(Predungeon predungeon, bool isBossRoom, out RectInt rect)
    {
        // Unity's integer Random.Range excludes the upper bound.
        Vector2Int size = isBossRoom
            ? new Vector2Int(Random.Range(MinBossRoomWidth, MaxBossRoomWidth), Random.Range(MinBossRoomHeight, MaxBossRoomHeight))
            : new Vector2Int(Random.Range(MinRoomWidth, MaxRoomWidth), Random.Range(MinRoomHeight, MaxRoomHeight));

        int border = Predungeon.Border;
        int maxX = AreaWidth - border - size.x;
        int maxY = AreaHeight - border - size.y;
        if (maxX < border || maxY < border)
        {
            rect = default;
            return false;
        }

        rect = new RectInt(Random.Range(border, maxX + 1), Random.Range(border, maxY + 1), size.x, size.y);

        int gap = MinDistanceBetweenRooms;
        return predungeon.IsSolid(new RectInt(rect.x - gap, rect.y - gap, size.x + 2 * gap, size.y + 2 * gap));
    }

    // Each room is linked to a random number of its closest rooms.
    private void ConnectNearestRooms(Predungeon predungeon)
    {
        List<PreRoom> rooms = predungeon.Rooms;
        HashSet<Vector2Int> linked = new HashSet<Vector2Int>();

        for (int a = 0; a < rooms.Count; a++)
        {
            int connections = Mathf.Min(Random.Range(1, AdjacentRoomsToConnect + 1), rooms.Count - 1);

            foreach (int b in ClosestRooms(rooms, a).Take(connections).ToList())
            {
                // A pair is only dug once, whichever room picked the other first.
                if (!linked.Add(new Vector2Int(Mathf.Min(a, b), Mathf.Max(a, b))))
                    continue;

                Vector2Int from = rooms[a].MidPoints[Random.Range(0, rooms[a].MidPoints.Count)];
                Vector2Int to = rooms[b].MidPoints[Random.Range(0, rooms[b].MidPoints.Count)];
                DigCorridor(predungeon, a, from, to);
            }
        }
    }

    // Nearest-neighbour links can leave groups of rooms cut off from each other,
    // so keep joining the closest unreached room to the reached ones until none are left.
    private void ConnectIslands(Predungeon predungeon)
    {
        List<PreRoom> rooms = predungeon.Rooms;

        while (true)
        {
            bool[,] reached = predungeon.Reachable(rooms[0].MidPoints[0]);

            int bestReached = -1;
            int bestUnreached = -1;
            float bestDistance = float.MaxValue;

            for (int r = 0; r < rooms.Count; r++)
            {
                if (!IsReached(reached, rooms[r]))
                    continue;

                for (int u = 0; u < rooms.Count; u++)
                {
                    if (IsReached(reached, rooms[u]))
                        continue;

                    float distance = (rooms[r].Rect.center - rooms[u].Rect.center).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestReached = r;
                        bestUnreached = u;
                    }
                }
            }

            if (bestUnreached < 0)
                return;

            DigCorridor(predungeon, bestReached, rooms[bestReached].MidPoints[0], rooms[bestUnreached].MidPoints[0]);
        }
    }

    private static bool IsReached(bool[,] reached, PreRoom room)
    {
        return reached[room.MidPoints[0].x, room.MidPoints[0].y];
    }

    private static IEnumerable<int> ClosestRooms(List<PreRoom> rooms, int room)
    {
        return Enumerable.Range(0, rooms.Count)
            .Where(other => other != room)
            .OrderBy(other => (rooms[room].Rect.center - rooms[other].Rect.center).sqrMagnitude);
    }

    private static void DigCorridor(Predungeon predungeon, int startRoom, Vector2Int from, Vector2Int to)
    {
        PreCorridor corridor = new PreCorridor { StartRoom = startRoom };

        // Points in line get a straight corridor, anything else an L with a random first leg.
        bool yFirst = from.x == to.x || (from.y != to.y && Random.Range(0, 2) == 0);
        Vector2Int corner = yFirst ? new Vector2Int(from.x, to.y) : new Vector2Int(to.x, from.y);

        CarveLine(predungeon, corridor, from, corner);
        CarveLine(predungeon, corridor, corner, to);

        if (corridor.Cells.Count > 0)
            predungeon.Corridors.Add(corridor);
    }

    // Carves the axis-aligned line from the tile after from up to and including to.
    private static void CarveLine(Predungeon predungeon, PreCorridor corridor, Vector2Int from, Vector2Int to)
    {
        Vector2Int step = new Vector2Int(System.Math.Sign(to.x - from.x), System.Math.Sign(to.y - from.y));

        for (Vector2Int cell = from + step; cell != to + step; cell += step)
        {
            if (predungeon.Carve(cell))
                corridor.Cells.Add(cell);
        }
    }
}
