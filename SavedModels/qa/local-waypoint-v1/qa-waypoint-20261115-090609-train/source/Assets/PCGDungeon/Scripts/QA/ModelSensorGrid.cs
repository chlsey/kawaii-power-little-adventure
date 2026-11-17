using UnityEngine;

/// <summary>Sensor knowledge about one horizontal patch of the world.</summary>
public struct ModelMapCell
{
    public bool Observed;
    public bool Wall;
    public bool Exit;
    public bool Visited;
}

/// <summary>Retains ray-discovered cells on a fixed world lattice while expanding its storage as needed.</summary>
public sealed class ModelSensorGrid
{
    public const float CellSize = 0.25f;
    private const int InitialSize = 128;
    private const float SurfaceOffset = 0.0001f;

    private ModelMapCell[,] cells;

    public Vector2Int MinCell { get; private set; }
    public int Width => cells.GetLength(0);
    public int Depth => cells.GetLength(1);
    public int DiscoveredWallCellCount { get; private set; }
    public int ViewedOpenCellCount { get; private set; }

    public ModelSensorGrid(Vector2Int centerCell)
    {
        MinCell = centerCell - new Vector2Int(InitialSize / 2, InitialSize / 2);
        cells = new ModelMapCell[InitialSize, InitialSize];
    }

    public static Vector2Int WorldToCell(Vector3 position)
    {
        return new Vector2Int(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.z / CellSize));
    }

    public ModelMapCell ReadCell(Vector2Int cell)
    {
        int x = cell.x - MinCell.x;
        int z = cell.y - MinCell.y;
        if (x < 0 || z < 0 || x >= Width || z >= Depth)
            return default;

        return cells[x, z];
    }

    public void MarkVisited(Vector3 position)
    {
        Vector2Int cell = WorldToCell(position);
        EnsureBounds(cell, cell);
        int x = cell.x - MinCell.x;
        int z = cell.y - MinCell.y;
        cells[x, z].Observed = true;
        cells[x, z].Visited = true;
    }

    /// <summary>Marks the cells crossed by a sensor ray without erasing previously discovered boundaries.</summary>
    public void Trace(Vector3 origin, Vector3 endpoint, QaObjectType hitId)
    {
        Vector2 start = new Vector2(origin.x, origin.z);
        Vector2 end = new Vector2(endpoint.x, endpoint.z);
        if (hitId != QaObjectType.None)
        {
            // Resolve boundary hits to the cell just inside the sensed surface.
            Vector2 direction = end - start;
            if (direction.sqrMagnitude > 0f)
                end += direction.normalized * SurfaceOffset;
        }

        Vector2Int cell = new Vector2Int(Mathf.FloorToInt(start.x / CellSize), Mathf.FloorToInt(start.y / CellSize));
        Vector2Int endCell = new Vector2Int(Mathf.FloorToInt(end.x / CellSize), Mathf.FloorToInt(end.y / CellSize));
        EnsureBounds(cell, endCell);

        Vector2 delta = end - start;
        int stepX = System.Math.Sign(delta.x);
        int stepZ = System.Math.Sign(delta.y);
        float tMaxX = float.PositiveInfinity;
        float tMaxZ = float.PositiveInfinity;
        float tDeltaX = float.PositiveInfinity;
        float tDeltaZ = float.PositiveInfinity;
        if (stepX != 0)
        {
            float boundary = cell.x * CellSize;
            if (stepX > 0)
                boundary += CellSize;
            tMaxX = (boundary - start.x) / delta.x;
            tDeltaX = CellSize / Mathf.Abs(delta.x);
        }
        if (stepZ != 0)
        {
            float boundary = cell.y * CellSize;
            if (stepZ > 0)
                boundary += CellSize;
            tMaxZ = (boundary - start.y) / delta.y;
            tDeltaZ = CellSize / Mathf.Abs(delta.y);
        }

        while (cell != endCell)
        {
            MarkViewedOpenCell(cell);
            if (tMaxX < tMaxZ)
            {
                cell.x += stepX;
                tMaxX += tDeltaX;
            }
            else if (tMaxZ < tMaxX)
            {
                cell.y += stepZ;
                tMaxZ += tDeltaZ;
            }
            else
            {
                // A corner crossing enters the diagonal cell, not the two cells beside it.
                cell.x += stepX;
                cell.y += stepZ;
                tMaxX += tDeltaX;
                tMaxZ += tDeltaZ;
            }
        }

        int endX = endCell.x - MinCell.x;
        int endZ = endCell.y - MinCell.y;
        if (hitId == QaObjectType.None)
            MarkViewedOpenCell(endCell);
        else
            cells[endX, endZ].Observed = true;
        if (hitId == QaObjectType.Wall && !cells[endX, endZ].Wall)
        {
            cells[endX, endZ].Wall = true;
            DiscoveredWallCellCount++;
        }
        else if (hitId == QaObjectType.LevelEnd)
        {
            cells[endX, endZ].Exit = true;
        }
    }

    // Only ray-revealed open cells contribute to exploration; visits and hit surfaces do not.
    private void MarkViewedOpenCell(Vector2Int cell)
    {
        int x = cell.x - MinCell.x;
        int z = cell.y - MinCell.y;
        if (!cells[x, z].Observed && !cells[x, z].Wall && !cells[x, z].Exit)
            ViewedOpenCellCount++;
        cells[x, z].Observed = true;
    }

    private void EnsureBounds(Vector2Int first, Vector2Int second)
    {
        int minX = MinCell.x;
        int minZ = MinCell.y;
        int width = Width;
        int depth = Depth;
        int requiredMinX = Mathf.Min(first.x, second.x);
        int requiredMinZ = Mathf.Min(first.y, second.y);
        int requiredMaxX = Mathf.Max(first.x, second.x);
        int requiredMaxZ = Mathf.Max(first.y, second.y);
        while (requiredMinX < minX)
        {
            minX -= width;
            width *= 2;
        }
        while (requiredMaxX >= minX + width)
            width *= 2;
        while (requiredMinZ < minZ)
        {
            minZ -= depth;
            depth *= 2;
        }
        while (requiredMaxZ >= minZ + depth)
            depth *= 2;

        if (width == Width && depth == Depth)
            return;

        var expanded = new ModelMapCell[width, depth];
        int offsetX = MinCell.x - minX;
        int offsetZ = MinCell.y - minZ;
        for (int z = 0; z < Depth; z++)
        {
            for (int x = 0; x < Width; x++)
                expanded[x + offsetX, z + offsetZ] = cells[x, z];
        }
        cells = expanded;
        MinCell = new Vector2Int(minX, minZ);
    }
}
