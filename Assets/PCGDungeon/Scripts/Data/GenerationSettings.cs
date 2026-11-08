using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Generation Settings")]
public class GenerationSettings : ScriptableObject
{
    [SerializeField] public Data generation;
}

[Serializable]
public class Data
{
    [SerializeField] public int minRooms;
    [SerializeField] public int maxRooms;

    [SerializeField] public int amountOfAdjacentRoomsToConnect;

    //Make Minimum of 2
    [SerializeField] public int minRoomWidth;
    [SerializeField] public int maxRoomWidth;

    [SerializeField] public int minRoomHeight;
    [SerializeField] public int maxRoomHeight;

    [SerializeField] public int minBossRoomWidth;
    [SerializeField] public int maxBossRoomWidth;

    [SerializeField] public int minBossRoomHeight;
    [SerializeField] public int maxBossRoomHeight;

    [SerializeField] public int areaWidth;
    [SerializeField] public int areaHeight;
    
    [SerializeField] public int minDistanceBetweenRooms;

    [SerializeField] public List<ObjectList> walls = new List<ObjectList>();
    [SerializeField] public List<ObjectList> outerCorners = new List<ObjectList>();
    [SerializeField] public List<ObjectList> innerCorners = new List<ObjectList>();
    [SerializeField] public bool deleteInnerCorners;
    [SerializeField] public List<ObjectList> uCorners = new List<ObjectList>();
    [SerializeField] public List<ObjectList> squareCorners = new List<ObjectList>();
    [SerializeField] public List<ObjectList> doors = new List<ObjectList>();
    [SerializeField] public List<Vector3> doorsDisplacements;
    [SerializeField] public List<ObjectList> flooring = new List<ObjectList>();

    [SerializeField] public GameObject player;

    [SerializeField] public int minEnemiesPerRoom;
    [SerializeField] public int maxEnemiesPerRoom;

    [SerializeField] public List<ObjectList> enemies = new List<ObjectList>();
    [SerializeField] public List<ObjectList> bosses = new List<ObjectList>();
    [SerializeField] public List<ObjectList> breakables = new List<ObjectList>();
    [Min(0)] public int breakablePropsPerRoom = 3;
    [Range(0f, 1f)] public float breakableLootChance = 0.4f;
    [SerializeField] public List<ObjectList> staticObjects = new List<ObjectList>();
    [SerializeField] public List<ObjectList> wallObjects = new List<ObjectList>();

    [SerializeField] public int wallObjConsequitiveDistance;
    [SerializeField] public List<Vector3> wallObjDisplacements;

    [Range(0, 100)]
    [SerializeField] public int cornerObjectsRate;
    [Range(0, 100)]
    [SerializeField] public int destroyablesRate;

    [SerializeField] public List<ObjectList> middleObjects = new List<ObjectList>();
    [Range(0, 100)]
    [SerializeField] public int middleObjectsRate;

    // Pickup prefab (with QaLootPickup) used for items that have no pickup prefab of their own.
    // Empty falls back to the green placeholder cube.
    [SerializeField] public GameObject lootPickup;
    // Items rolled for each powerup slot. Empty keeps the plain QA powerups.
    [SerializeField] public List<LootEntry> lootTable = new List<LootEntry>();

    // Rooms that lock the player in until a puzzle is solved. Never the spawn or boss room.
    [Min(0)]
    [SerializeField] public int puzzleRoomCount;
    // Puzzle prefabs (each with a RoomPuzzle component). Empty uses a placeholder lever.
    [SerializeField] public List<ObjectList> puzzles = new List<ObjectList>();

    // Level exit model. Empty builds a glowing pillar and light as a placeholder.
    [SerializeField] public GameObject exitPrefab;
    // Off: touching the exit completes the level (what the QA bot and trained models do).
    // On: the player must also press Interact next to it.
    [SerializeField] public bool exitRequiresInteract;
}

[System.Serializable]
public class LootEntry
{
    public ItemDefinition item;
    [Min(0)] public int weight = 1;
}

[System.Serializable]
public class ObjectList
{
    public GameObject gameObject;

    public string name;
    //Can add other variables if wanted
}
