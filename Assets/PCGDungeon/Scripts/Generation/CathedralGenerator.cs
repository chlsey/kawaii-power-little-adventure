using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Stage one of the cathedral, after Diablo's: a spine of large rooms joined by a
// wide corridor, then smaller rooms recursively budded off them until the level
// is big enough. Creates no GameObjects.
public class CathedralGenerator : IPredungeonGenerator
{
    private readonly CathedralParameters parameters;

    public CathedralGenerator(CathedralParameters parameters)
    {
        this.parameters = parameters;
    }

    // Null when the spine does not fit the grid or no attempt reached MinWalkableTiles.
    public Predungeon Generate()
    {
        if (parameters.BudSizes == null || parameters.BudSizes.Length == 0)
            return null;

        for (int attempt = 0; attempt < parameters.MaxAttempts; attempt++)
        {
            Predungeon predungeon = new Predungeon(parameters.GridWidth, parameters.GridHeight);

            bool spineAlongX = parameters.SpineAxis == SpineAxis.X
                || (parameters.SpineAxis == SpineAxis.Random && Random.Range(0, 2) == 0);

            // A spine that does not fit will not fit on a retry either.
            if (!PlaceSpine(predungeon, spineAlongX))
                return null;

            // Spine rooms are the first rooms, so budding adds to the list as it goes.
            int spineRooms = predungeon.Rooms.Count;
            for (int i = 0; i < spineRooms; i++)
                Bud(predungeon, i, !spineAlongX);

            if (predungeon.CountFloor() < parameters.MinWalkableTiles)
                continue;

            ChooseBossAndSpawn(predungeon);
            return predungeon;
        }

        return null;
    }

    // Two large rooms and the hall between them, laid end to end and centred on the
    // grid. They share edges with no wall, so they read as one dumbbell-shaped space.
    private bool PlaceSpine(Predungeon predungeon, bool alongX)
    {
        int alongLength = alongX ? predungeon.Width : predungeon.Height;
        int acrossLength = alongX ? predungeon.Height : predungeon.Width;
        int size = parameters.SpineRoomSize;
        int hallLength = parameters.SpineCorridorLength;
        int hallWidth = Mathf.Clamp(parameters.SpineCorridorWidth, 1, size);
        int border = Predungeon.Border;

        int total = 2 * size + hallLength;
        if (total > alongLength - 2 * border || size > acrossLength - 2 * border)
            return false;

        int along = (alongLength - total) / 2;
        int across = (acrossLength - size) / 2;

        AddSpineRoom(predungeon, alongX, along, across, size, size);
        AddSpineRoom(predungeon, alongX, along + size, across + (size - hallWidth) / 2, hallLength, hallWidth);
        AddSpineRoom(predungeon, alongX, along + size + hallLength, across, size, size);

        AddColumns(predungeon, 0);
        AddColumns(predungeon, 2);
        AddColonnade(predungeon, 1, alongX);

        return true;
    }

    // Four free-standing columns, mirrored about the room's centre.
    private void AddColumns(Predungeon predungeon, int roomIndex)
    {
        RectInt room = predungeon.Rooms[roomIndex].Rect;
        int inset = Mathf.Clamp(parameters.ColumnInset, 1, (Mathf.Min(room.width, room.height) - 1) / 2);
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>
        {
            new Vector2Int(room.xMin + inset, room.yMin + inset),
            new Vector2Int(room.xMax - 1 - inset, room.yMin + inset),
            new Vector2Int(room.xMin + inset, room.yMax - 1 - inset),
            new Vector2Int(room.xMax - 1 - inset, room.yMax - 1 - inset)
        };

        foreach (Vector2Int cell in cells)
        {
            predungeon.Features.Add(new PreFeature
            {
                Kind = PreFeatureKind.Column,
                Position = new Vector2(cell.x, cell.y),
                Room = roomIndex
            });
        }
    }

    // Two rows of arches running the length of the hall, side by side with no gaps.
    private void AddColonnade(Predungeon predungeon, int hallIndex, bool alongX)
    {
        RectInt hall = predungeon.Rooms[hallIndex].Rect;
        int length = alongX ? hall.width : hall.height;
        int width = alongX ? hall.height : hall.width;
        int alongStart = alongX ? hall.xMin : hall.yMin;
        int acrossStart = alongX ? hall.yMin : hall.xMin;

        int span = Mathf.Max(1, parameters.ColonnadeSpacing);
        int inset = Mathf.Clamp(parameters.ColonnadeInset, 0, (width - 1) / 2);
        HashSet<int> rows = new HashSet<int> { acrossStart + inset, acrossStart + width - 1 - inset };

        for (int offset = 0; offset + span <= length; offset += span)
        {
            // The middle of the tiles the arch covers.
            float along = alongStart + offset + (span - 1) / 2f;

            foreach (int row in rows)
            {
                predungeon.Features.Add(new PreFeature
                {
                    Kind = PreFeatureKind.Arch,
                    Position = alongX ? new Vector2(along, row) : new Vector2(row, along),
                    Room = hallIndex,
                    Span = span,
                    AlongX = alongX
                });
            }
        }
    }

    private static void AddSpineRoom(Predungeon predungeon, bool alongX, int along, int across, int alongSize, int acrossSize)
    {
        RectInt rect = alongX
            ? new RectInt(along, across, alongSize, acrossSize)
            : new RectInt(across, along, acrossSize, alongSize);

        predungeon.CarveRect(rect);
        predungeon.Rooms.Add(new PreRoom(rect) { IsSpine = true });
    }

    // Diablo's L5roomGen, with a wall between rooms. Places a new room beyond the
    // middle of each end of the room along the current axis, when the space is free,
    // then buds from each room it added along the other axis. Each new room is
    // separated from its parent by one solid tile, with a doorway cut through it.
    private void Bud(Predungeon predungeon, int roomIndex, bool alongX)
    {
        RectInt room = predungeon.Rooms[roomIndex].Rect;

        if (Random.Range(0f, 1f) < parameters.AxisSwitchChance)
            alongX = !alongX;

        Vector2Int size = new Vector2Int(RandomBudSize(), RandomBudSize());
        List<int> added = new List<int>();

        for (int side = -1; side <= 1; side += 2)
        {
            RectInt rect = alongX
                ? new RectInt(side < 0 ? room.xMin - 1 - size.x : room.xMax + 1, room.y + (room.height - size.y) / 2, size.x, size.y)
                : new RectInt(room.x + (room.width - size.x) / 2, side < 0 ? room.yMin - 1 - size.y : room.yMax + 1, size.x, size.y);

            if (!Fits(predungeon, rect))
                continue;

            added.Add(predungeon.Rooms.Count);
            predungeon.CarveRect(rect);
            predungeon.Rooms.Add(new PreRoom(rect));
            CarveDoorway(predungeon, roomIndex, room, rect, alongX, side);
        }

        foreach (int index in added)
            Bud(predungeon, index, !alongX);
    }

    // Cuts the wall tile between parent and child, across the middle of where they overlap.
    private void CarveDoorway(Predungeon predungeon, int parentIndex, RectInt parent, RectInt child, bool alongX, int side)
    {
        int overlapStart = alongX ? Mathf.Max(parent.yMin, child.yMin) : Mathf.Max(parent.xMin, child.xMin);
        int overlapEnd = alongX ? Mathf.Min(parent.yMax, child.yMax) : Mathf.Min(parent.xMax, child.xMax);
        int width = Mathf.Clamp(parameters.DoorwayWidth, 1, overlapEnd - overlapStart);
        int start = overlapStart + (overlapEnd - overlapStart - width) / 2;
        int wall = alongX
            ? (side < 0 ? parent.xMin - 1 : parent.xMax)
            : (side < 0 ? parent.yMin - 1 : parent.yMax);

        PreCorridor doorway = new PreCorridor { StartRoom = parentIndex };
        for (int i = 0; i < width; i++)
        {
            Vector2Int cell = alongX ? new Vector2Int(wall, start + i) : new Vector2Int(start + i, wall);
            if (predungeon.Carve(cell))
                doorway.Cells.Add(cell);
        }

        predungeon.Corridors.Add(doorway);
    }

    private int RandomBudSize()
    {
        return parameters.BudSizes[Random.Range(0, parameters.BudSizes.Length)];
    }

    // The room and a one-tile ring around it must be solid, so the only way in is its doorway.
    private static bool Fits(Predungeon predungeon, RectInt rect)
    {
        int border = Predungeon.Border;
        return rect.xMin >= border && rect.yMin >= border
            && rect.xMax <= predungeon.Width - border && rect.yMax <= predungeon.Height - border
            && predungeon.IsSolid(new RectInt(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2));
    }

    // The boss gets the largest room that is not a spine room, since those stay open
    // set pieces; the player starts in the room farthest from it.
    private static void ChooseBossAndSpawn(Predungeon predungeon)
    {
        List<PreRoom> rooms = predungeon.Rooms;
        List<int> pool = Enumerable.Range(0, rooms.Count).Where(i => !rooms[i].IsSpine).ToList();
        if (pool.Count == 0)
            pool = Enumerable.Range(0, rooms.Count).ToList();

        int largest = pool.Max(i => rooms[i].Rect.width * rooms[i].Rect.height);
        List<int> candidates = pool.Where(i => rooms[i].Rect.width * rooms[i].Rect.height == largest).ToList();

        predungeon.BossRoom = candidates[Random.Range(0, candidates.Count)];
        predungeon.SpawnRoom = predungeon.FarthestRoomFrom(predungeon.BossRoom);
    }
}
