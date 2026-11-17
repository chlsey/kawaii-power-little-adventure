using System.Collections.Generic;
using UnityEngine;

// Stage one's output: which tiles are walkable, before any tile prefab is chosen.
// The grid starts entirely solid and generators carve floor out of it;
// Generator.BuildTiles then turns the result into room and corridor objects.
public enum PreTile : byte
{
    Solid,
    Floor
}

public class PreRoom
{
    public RectInt Rect;
    // Spine rooms are the large open rooms a cathedral is built around.
    public bool IsSpine;

    // The tiles corridors start and end on: one tile for an odd tile count and two
    // for an even one, as close to the centre as the grid allows.
    public List<Vector2Int> MidPoints = new List<Vector2Int>();

    public PreRoom(RectInt rect)
    {
        Rect = rect;

        int centreX = rect.x + rect.width / 2;
        int centreY = rect.y + rect.height / 2;
        MidPoints.Add(new Vector2Int(centreX, centreY));

        if (rect.width % 2 == 0)
            MidPoints.Add(new Vector2Int(centreX - 1, centreY));
        else if (rect.height % 2 == 0)
            MidPoints.Add(new Vector2Int(centreX, centreY - 1));
    }
}

public enum PreFeatureKind
{
    Column,
    Arch
}

// A decoration the layout asks for. Positions are in tile coordinates, where
// tile centres are whole numbers, so an arch between two tiles sits on a half.
public class PreFeature
{
    public PreFeatureKind Kind;
    public Vector2 Position;
    // The room that owns the tile it stands on.
    public int Room;

    // Arches only: how many tiles it spans, and whether it spans along the x axis.
    public int Span = 1;
    public bool AlongX;

    // The tiles it stands over.
    public IEnumerable<Vector2Int> Cells
    {
        get
        {
            int count = Kind == PreFeatureKind.Arch ? Span : 1;
            for (int i = 0; i < count; i++)
            {
                float offset = i - (count - 1) / 2f;
                Vector2 centre = Position + (AlongX ? new Vector2(offset, 0) : new Vector2(0, offset));
                yield return new Vector2Int(Mathf.RoundToInt(centre.x), Mathf.RoundToInt(centre.y));
            }
        }
    }
}

// The floor one corridor carved, leaving out tiles that were already walkable.
public class PreCorridor
{
    public int StartRoom;
    public List<Vector2Int> Cells = new List<Vector2Int>();
}

public class Predungeon
{
    // Walls reach up to two tiles past a floor tile, so floor stays off the grid edge.
    public const int Border = 3;

    private static readonly Vector2Int[] Steps = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

    public readonly int Width;
    public readonly int Height;
    public readonly List<PreRoom> Rooms = new List<PreRoom>();
    public readonly List<PreCorridor> Corridors = new List<PreCorridor>();
    public readonly List<PreFeature> Features = new List<PreFeature>();

    public int BossRoom = -1;
    // The room farthest from the boss room, or -1 when there is only one room.
    public int SpawnRoom = -1;

    private readonly PreTile[,] tiles;

    public Predungeon(int width, int height)
    {
        Width = width;
        Height = height;
        tiles = new PreTile[width, height];
    }

    public bool IsFloor(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height
            && tiles[cell.x, cell.y] == PreTile.Floor;
    }

    // True if no tile of the rect (the part inside the grid) is floor yet.
    public bool IsSolid(RectInt rect)
    {
        for (int x = Mathf.Max(rect.xMin, 0); x < Mathf.Min(rect.xMax, Width); x++)
        {
            for (int y = Mathf.Max(rect.yMin, 0); y < Mathf.Min(rect.yMax, Height); y++)
            {
                if (tiles[x, y] != PreTile.Solid)
                    return false;
            }
        }

        return true;
    }

    public void CarveRect(RectInt rect)
    {
        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
                tiles[x, y] = PreTile.Floor;
        }
    }

    public int CountFloor()
    {
        int count = 0;
        foreach (PreTile tile in tiles)
        {
            if (tile == PreTile.Floor)
                count++;
        }

        return count;
    }

    // The room whose centre is farthest from the given room's, or -1 if there is no other room.
    public int FarthestRoomFrom(int room)
    {
        int farthest = -1;
        float farthestDistance = 0f;

        for (int other = 0; other < Rooms.Count; other++)
        {
            float distance = (Rooms[room].Rect.center - Rooms[other].Rect.center).sqrMagnitude;
            if (other != room && distance > farthestDistance)
            {
                farthestDistance = distance;
                farthest = other;
            }
        }

        return farthest;
    }

    // Returns whether the tile was solid, i.e. whether carving it changed anything.
    public bool Carve(Vector2Int cell)
    {
        if (tiles[cell.x, cell.y] == PreTile.Floor)
            return false;

        tiles[cell.x, cell.y] = PreTile.Floor;
        return true;
    }

    // Every floor tile connected to start by 4-neighbour steps.
    public bool[,] Reachable(Vector2Int start)
    {
        bool[,] seen = new bool[Width, Height];
        Stack<Vector2Int> open = new Stack<Vector2Int>();
        seen[start.x, start.y] = true;
        open.Push(start);

        while (open.Count > 0)
        {
            Vector2Int cell = open.Pop();
            foreach (Vector2Int step in Steps)
            {
                Vector2Int next = cell + step;
                if (IsFloor(next) && !seen[next.x, next.y])
                {
                    seen[next.x, next.y] = true;
                    open.Push(next);
                }
            }
        }

        return seen;
    }
}
