using System.Collections.Generic;
using UnityEngine;

public partial class Generator
{
    private readonly HashSet<Room> lightsOutRooms = new HashSet<Room>();

    private void AssignLightsOutRooms(List<Room> candidates)
    {
        Data settings = generationSettings.generation;
        if (settings.lightsOutSpawnRate <= 0 || settings.maxLightsOutRooms <= 0) return;
        // Sample without replacement so neither a room nor an existing lever gets a second puzzle.
        var remaining = new List<Room>(candidates);
        while (remaining.Count > 0 && lightsOutRooms.Count < settings.maxLightsOutRooms)
        {
            int index = Random.Range(0, remaining.Count);
            Room room = remaining[index];
            remaining.RemoveAt(index);
            if (room.GeneratedSize.x < 7 || room.GeneratedSize.y < 7) continue;
            if (Random.Range(0, 100) >= settings.lightsOutSpawnRate) continue;
            room.Type = RoomType.Puzzle;
            lightsOutRooms.Add(room);
            puzzleRooms.Add(room);
        }
    }

    private RoomPuzzle SpawnLightsOut(Room room)
    {
        // Centre the whole three-switch assembly, leaving wall/door clearance on every side.
        Transform bestTile = null;
        float bestDistance = float.PositiveInfinity;
        foreach (Transform tile in room.transform)
        {
            if (!tile.CompareTag("Floor")) continue;
            float distance = (tile.position - room.transform.position).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; bestTile = tile; }
        }
        if (bestTile == null) return null;
        Vector3 floor = bestTile.position;
        Renderer renderer = bestTile.GetComponent<Renderer>();
        if (renderer != null) floor.y = renderer.bounds.max.y;
        var root = new GameObject("Lights Out Puzzle");
        root.transform.SetParent(room.transform, true);
        root.transform.position = floor;
        LightsOutPuzzle puzzle = root.AddComponent<LightsOutPuzzle>();
        puzzle.Initialize(Random.Range(0, 4));
        puzzle.Solved += () => SpawnPowerupAt(room, floor + Vector3.back * 1.6f);
        return puzzle;
    }
}
