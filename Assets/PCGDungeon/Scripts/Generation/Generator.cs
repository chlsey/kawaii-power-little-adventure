using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.AI;

public class Generator : MonoBehaviour
{
    //[SerializeField] private GameObject m_wall;

    [SerializeField] private GenerationSettings generationSettings;

    private DungeonType dungeonType;
    private CathedralParameters cathedralParameters;
    private CathedralSettings cathedralSettings;
    private bool deleteInnerCorners;

    private int rooms;
    private int minRooms;
    private int maxRooms;
    private int areaWidth;
    private int areaHeight;
    private int minRoomWidth;
    private int maxRoomWidth;
    private int minRoomHeight;
    private int maxRoomHeight;
    private int destroyablesRate;
    private int minBossRoomWidth;
    private int maxBossRoomWidth;
    private int minBossRoomHeight;
    private int maxBossRoomHeight;
    private int cornerObjectsRate;
    private int middleObjectsRate;
    private int minEnemiesPerRoom;
    private int maxEnemiesPerRoom;
    private int minDistanceBetweenRooms;
    private int wallObjConsequitiveDistance;
    private int amountOfAdjacentRoomsToConnect;
    private int powerupsPerRoom;

    private List<ObjectList> walls;
    private List<ObjectList> doors;
    private List<ObjectList> bosses;
    private List<ObjectList> enemies;
    private List<ObjectList> uCorners;
    private List<ObjectList> flooring;
    private List<ObjectList> breakables;
    private List<ObjectList> wallObjects;
    private List<ObjectList> outerCorners;
    private List<ObjectList> innerCorners;
    private List<ObjectList> squareCorners;
    private List<ObjectList> staticObjects;
    private List<ObjectList> middleObjects;

    private List<Vector3> wallObjDisplacements;
    private List<Vector3> doorsDisplacements;

    private GameObject lootPickup;
    private List<LootEntry> lootTable;

    private GameObject exitPrefab;
    private bool exitRequiresInteract;

    private int puzzleRoomCount;
    private List<ObjectList> puzzles;
    private List<Room> puzzleRooms = new List<Room>();

    private GameObject boss;
    private GameObject player;
    private GameObject[,] area;
    private GameObject[] parents;
    private GameObject emptyObject;
    private GameObject emptyParentRoom;

    private Vector2Int[] roomPos;
    private Vector2Int[] roomArea;

    private Room[] parentRooms;
    private Room bossSpawnRoom;
    private Room playerSpawnRoom;

    private List<GameObject> corridors = new List<GameObject>();
    // Tiles under columns and arches, which loot, enemies and puzzles keep off.
    private HashSet<Vector2Int> occupiedTiles = new HashSet<Vector2Int>();
    private List<GameObject> doorGameObjects = new List<GameObject>();

    private string[] floorRemovables = { "Floor", "(", ")", "", " ", "," };

    public GameObject Player { get => player; }
    public Room PlayerSpawnRoom { get => playerSpawnRoom; }
    public Room BossSpawnRoom { get => bossSpawnRoom; }
    public List<GameObject> DoorGameObjects { get => doorGameObjects; }
    public List<ObjectList> Enemies { get => enemies; }
    public int AmountOfAdjacentRoomsToConnect { get => amountOfAdjacentRoomsToConnect; }
    public GenerationSettings GenerationSettings { get => generationSettings; }
    public List<Room> PuzzleRooms { get => puzzleRooms; }
    public int RequestedPuzzleRoomCount { get => puzzleRoomCount; }

    
    public bool RunProgram(PcgGenerationParameters parameters)
    {
        Random.InitState(parameters.Seed);

        ClearChildren();

        GetGenerationSettings(); // Still obtains prefabs and fixed theme data.

        if (parameters.Cathedral != null)
            cathedralParameters = parameters.Cathedral;

        rooms = parameters.RoomCount;
        areaWidth = parameters.AreaWidth;
        areaHeight = parameters.AreaHeight;

        minRoomWidth = parameters.MinRoomWidth;
        maxRoomWidth = parameters.MaxRoomWidth;

        minRoomHeight = parameters.MinRoomHeight;
        maxRoomHeight = parameters.MaxRoomHeight;

        powerupsPerRoom = parameters.PowerupsPerRoom;

        // amountOfAdjacentRoomsToConnect =
        //     parameters.AdjacentRoomConnections;

        InitialiseEmptyObjects();
        return Generate();
    }

    private void ClearChildren()
    {
        // Generated corridors are destroyed with the generator's children, so
        // discard their cached references before the next generation pass.
        if (emptyObject != null) DestroyImmediate(emptyObject);
        if (emptyParentRoom != null) DestroyImmediate(emptyParentRoom);
        corridors.Clear();
        doorGameObjects.Clear();
        puzzleRooms.Clear();
        occupiedTiles.Clear();

        if(transform.childCount > 0)
        {
            while(transform.childCount!= 0)
            {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }
        }
    }

    private void GetGenerationSettings()
    {
        Data settings;
        settings = GenerationSettings.generation;

        dungeonType = settings.dungeonType;
        cathedralSettings = settings.cathedral;
        cathedralParameters = cathedralSettings != null ? cathedralSettings.parameters : new CathedralParameters();

        minRooms = settings.minRooms;
        maxRooms = settings.maxRooms;
        if (minRooms == 0)
            Debug.LogError("Minimum amount of rooms can not equal zero for generation to work.");
        if (maxRooms == 0)
            Debug.LogError("Maximum amount of rooms can not equal zero for generation to work.");

        amountOfAdjacentRoomsToConnect = settings.amountOfAdjacentRoomsToConnect;

        if (AmountOfAdjacentRoomsToConnect == 0)
            Debug.LogError("Amount of AdjacentRoomsToConnect can not equal zero for generation to work.");

        if (AmountOfAdjacentRoomsToConnect >= minRooms)
            Debug.LogError("Amount of Adjacent Rooms to connect needs to be smaller than minimum of rooms");

        minRoomWidth = settings.minRoomWidth;
        maxRoomWidth = settings.maxRoomWidth;
        if (minRoomWidth == 0)
            Debug.LogError("Minimum Room Width can not equal zero for generation to work.");
        if (maxRoomWidth == 0)
            Debug.LogError("Maximum Room Width can not equal zero for generation to work.");

        minRoomHeight = settings.minRoomHeight;
        maxRoomHeight = settings.maxRoomHeight;
        if (minRoomHeight == 0)
            Debug.LogError("Minimum Room Height can not equal zero for generation to work.");
        if (maxRoomHeight == 0)
            Debug.LogError("Maximum Room Height can not equal zero for generation to work.");

        minBossRoomWidth = settings.minBossRoomWidth;
        maxBossRoomWidth = settings.maxBossRoomWidth;
        if (minBossRoomWidth == 0)
            Debug.LogError("Minimum Boss Room Width can not equal zero for generation to work.");
        if (maxBossRoomWidth == 0)
            Debug.LogError("Maximum Boss Room Width can not equal zero for generation to work.");

        minBossRoomHeight = settings.minBossRoomHeight;
        maxBossRoomHeight = settings.maxBossRoomHeight;
        if (minBossRoomHeight == 0)
            Debug.LogError("Minimum Boss Room Height can not equal zero for generation to work.");
        if (maxBossRoomHeight == 0)
            Debug.LogError("Maximum Boss Room Height can not equal zero for generation to work.");

        areaWidth = settings.areaWidth;
        areaHeight = settings.areaHeight;
        if (areaWidth == 0)
            Debug.LogError("Area Width can not equal zero for generation to work.");
        if (areaHeight == 0)
            Debug.LogError("Area Height can not equal zero for generation to work.");

        minDistanceBetweenRooms = settings.minDistanceBetweenRooms;
        if(minDistanceBetweenRooms == 0)
            Debug.Log("Minimum distance between rooms advised to be at least one.");

        walls = settings.walls;
        if (!CheckListObject(walls))
            Debug.LogError("Walls can't be null or nonexistent for generation to work.");

        outerCorners = settings.outerCorners;
        if (!CheckListObject(outerCorners))
            Debug.LogError("Outer Corners can't be null or nonexistent for generation to work.");

        innerCorners = settings.innerCorners;
        if (!CheckListObject(innerCorners))
            Debug.Log("Inner Corners are null, they are not however not required.");

        deleteInnerCorners = settings.deleteInnerCorners;

        uCorners = settings.uCorners;
        if (!CheckListObject(uCorners))
            Debug.Log("U Corners can't be null or nonexistent for generation.");

        squareCorners = settings.squareCorners;
        if (!CheckListObject(squareCorners))
            Debug.LogError("Square Corners can't be null or nonexistent for generation.");

        doors = settings.doors;
        if (!CheckListObject(doors))
            Debug.Log("Doors are null or nonexistent.");
        
        doorsDisplacements = settings.doorsDisplacements;

        flooring = settings.flooring;
        if (!CheckListObject(flooring))
            Debug.LogError("Flooring can't be null or nonexistent for generation.");

        minEnemiesPerRoom = settings.minEnemiesPerRoom;
        if (minEnemiesPerRoom == 0)
            Debug.Log("Minimum enemies per room are currently equal to 0.");

        maxEnemiesPerRoom = settings.maxEnemiesPerRoom;
        if (maxEnemiesPerRoom == 0)
            Debug.Log("Maximum enemies per room are currently equal to 0");

        player = settings.player;
        if(player == null)
            Debug.Log("Player is null or nonexistent.");

        lootPickup = settings.lootPickup;
        lootTable = settings.lootTable;

        exitPrefab = settings.exitPrefab;
        exitRequiresInteract = settings.exitRequiresInteract;

        puzzleRoomCount = settings.puzzleRoomCount;
        puzzles = settings.puzzles;

        enemies = settings.enemies;
        if (!CheckListObject(Enemies))
            Debug.Log("Enemies are null or nonexistent.");

        bosses = settings.bosses;
        if (!CheckListObject(bosses))
            Debug.Log("Bosses are null or nonexistent.");

        breakables = settings.breakables;
        if (!CheckListObject(breakables))
            Debug.Log("Breakables are null or nonexistent.");

        staticObjects = settings.staticObjects;
        if (!CheckListObject(staticObjects))
            Debug.Log("Static Objects are null or nonexistent.");

        wallObjects = settings.wallObjects;
        if (!CheckListObject(wallObjects))
            Debug.Log("Wall Objects are null or nonexistent.");

        wallObjConsequitiveDistance = settings.wallObjConsequitiveDistance;
        if (wallObjConsequitiveDistance == 0)
            Debug.Log("Consequitive distance between Wall objects can not equal 0 for generation to work. Advised to be at least 2 for non cluster.");

        wallObjDisplacements = settings.wallObjDisplacements;

        while (wallObjDisplacements.Count != wallObjects.Count)
        {
            if(wallObjDisplacements.Count < wallObjects.Count)
            {
                wallObjDisplacements.Add(new Vector3());
            }
            else
            {
                wallObjDisplacements.RemoveAt(wallObjDisplacements.Count - 1);
            }
        } 

        Debug.Log("Wall OBj displacement will vary accordingly to listed item.");

        cornerObjectsRate = settings.cornerObjectsRate;
        if (cornerObjectsRate == 0 && staticObjects.Count > 0 && breakables.Count > 0)
            Debug.Log("Rate of corner objects can not equal 0 for corner objects to be generated.");

        destroyablesRate = settings.destroyablesRate;
        if (destroyablesRate == 0 && breakables.Count > 0)
            Debug.Log("Rate of Destroyables can not equal 0 for corner objects to be generated.");

        middleObjects = settings.middleObjects;
        if (!CheckListObject(middleObjects))
            Debug.Log("Middle Objects are null or nonexistent.");

        middleObjectsRate = settings.middleObjectsRate;
        if(middleObjectsRate == 0 && middleObjects.Count > 0)
            Debug.Log("Rate of MiddleObjects can not equal 0 for MiddleObjects objects to be generated.");
    }

    private bool CheckListObject(List<ObjectList> objectList)
    {
        int listCount = 0;

        for (int i = 0; i < objectList.Count; i++)
        {
            if (objectList[i].gameObject != null)
                listCount += 1;
        }


        if (listCount == objectList.Count && objectList.Count != 0)
        {
            return true;
        }

        return false;
    }

    private void InitialiseEmptyObjects()
    {
        emptyObject = new GameObject();
        emptyObject.hideFlags = HideFlags.HideInHierarchy;

        emptyParentRoom = new GameObject();
        emptyParentRoom.hideFlags = HideFlags.HideInHierarchy;
        emptyParentRoom.AddComponent(typeof(Room));
        emptyParentRoom.tag = "Room";
    }

    // Stage one decides which tiles are walkable; stage two builds them. The
    // wall, corner and door passes after that still scan the built floor tiles.
    private bool Generate()
    {
        Predungeon predungeon = CreatePredungeonGenerator().Generate();

        if (predungeon == null)
        {
            Debug.LogError($"Could not generate a {dungeonType} dungeon. Aborting generation.");
            return false;
        }

        BuildTiles(predungeon);

        GenerateWalls();

        DoubleCheckCorners();

        GenerateDoors();

        AssignRoomTypes();
        
        GenerateContent();

        DoubleCheckRoomCorners();

        PlaceFeatures(predungeon);

        SpawnEntities();

        SpawnQaPlaceholders();

        SpawnPuzzles();

        return true;
    }

    private IPredungeonGenerator CreatePredungeonGenerator()
    {
        switch (dungeonType)
        {
            case DungeonType.RoomsAndCorridors:
                return new RoomsAndCorridorsGenerator
                {
                    AreaWidth = areaWidth,
                    AreaHeight = areaHeight,
                    RoomCount = rooms,
                    MinRoomWidth = minRoomWidth,
                    MaxRoomWidth = maxRoomWidth,
                    MinRoomHeight = minRoomHeight,
                    MaxRoomHeight = maxRoomHeight,
                    MinBossRoomWidth = minBossRoomWidth,
                    MaxBossRoomWidth = maxBossRoomWidth,
                    MinBossRoomHeight = minBossRoomHeight,
                    MaxBossRoomHeight = maxBossRoomHeight,
                    MinDistanceBetweenRooms = minDistanceBetweenRooms,
                    AdjacentRoomsToConnect = amountOfAdjacentRoomsToConnect,
                };
            case DungeonType.Cathedral:
                return new CathedralGenerator(cathedralParameters);
            default:
                throw new System.NotSupportedException($"No predungeon generator for dungeon type {dungeonType}.");
        }
    }

    // Turns the predungeon's floor into room and corridor objects. A room's
    // floor tiles must stay its first children, in x-then-y order, because the
    // corner and enemy placement below index into them.
    private void BuildTiles(Predungeon predungeon)
    {
        int roomCount = predungeon.Rooms.Count;
        area = new GameObject[predungeon.Width, predungeon.Height];
        parents = new GameObject[roomCount];
        parentRooms = new Room[roomCount];
        roomArea = new Vector2Int[roomCount];
        roomPos = new Vector2Int[roomCount];

        for (int i = 0; i < predungeon.Rooms.Count; i++)
        {
            PreRoom preRoom = predungeon.Rooms[i];
            roomPos[i] = preRoom.Rect.position;
            roomArea[i] = preRoom.Rect.size;

            Vector2 center = CalculateCenterPoint(roomArea[i], roomPos[i]);

            parents[i] = Instantiate(emptyParentRoom, new Vector3(center.x, 0, center.y), Quaternion.identity, gameObject.transform);
            parents[i].name = "Room" + (i + 1) + "(" + roomPos[i].x + ", " + roomPos[i].y + ")";

            BoxCollider collider = parents[i].AddComponent<BoxCollider>();
            collider.size = new Vector3(roomArea[i].x - 1, 3, roomArea[i].y - 1);
            collider.isTrigger = true;

            for (int a = 0; a < roomArea[i].x; a++)
            {
                for (int b = 0; b < roomArea[i].y; b++)
                {
                    CreateFloor(roomPos[i].x + a, roomPos[i].y + b, parents[i].transform);
                }
            }

            parentRooms[i] = parents[i].GetComponent<Room>();
            parentRooms[i].GeneratedSize = roomArea[i];

            foreach (Vector2Int midPoint in preRoom.MidPoints)
            {
                parentRooms[i].MidPoints.Add(area[midPoint.x, midPoint.y]);
            }
        }

        bossSpawnRoom = parentRooms[predungeon.BossRoom];
        playerSpawnRoom = predungeon.SpawnRoom >= 0 ? parentRooms[predungeon.SpawnRoom] : null;

        foreach (PreCorridor preCorridor in predungeon.Corridors)
        {
            GameObject corridor = Instantiate(emptyObject);
            corridor.transform.SetParent(parents[preCorridor.StartRoom].transform);
            corridor.name = "Corridor " + corridors.Count;
            corridors.Add(corridor);

            foreach (Vector2Int cell in preCorridor.Cells)
            {
                CreateFloor(cell.x, cell.y, corridor.transform);
            }
        }
    }

    private void CreateFloor(int x, int y, Transform parent)
    {
        int randomFlooring = Random.Range(0, flooring.Count);

        area[x, y] = Instantiate(flooring[randomFlooring].gameObject, new Vector3(x, 0, y), Quaternion.identity, parent);
        area[x, y].name = "Floor" + " (" + x + ", " + y + ")";
    }

    // Marks spawn and boss rooms, then picks puzzleRoomCount of the rest as
    // puzzle rooms. Random is only consumed when puzzle rooms are requested, so
    // existing seeds generate the same dungeon as before.
    private void AssignRoomTypes()
    {
        List<Room> candidates = new List<Room>();
        foreach (Room room in parentRooms)
        {
            if (room == null)
                continue;

            room.Type = room == playerSpawnRoom ? RoomType.Spawn
                : room == bossSpawnRoom ? RoomType.Boss
                : RoomType.Normal;

            if (room.Type == RoomType.Normal)
                candidates.Add(room);
        }

        for (int i = 0; i < puzzleRoomCount && candidates.Count > 0; i++)
        {
            int pick = Random.Range(0, candidates.Count);
            candidates[pick].Type = RoomType.Puzzle;
            puzzleRooms.Add(candidates[pick]);
            candidates.RemoveAt(pick);
        }

        if (puzzleRooms.Count < puzzleRoomCount)
            Debug.LogWarning($"Only {puzzleRooms.Count} rooms were available for {puzzleRoomCount} puzzle rooms.");
    }

    private void SpawnPuzzles()
    {
        foreach (Room room in puzzleRooms)
        {
            RoomPuzzle puzzle = SpawnPuzzle(room);
            if (puzzle == null)
                continue;

            // Doors sit in the doorway just outside the floor, and slide sideways
            // when open, so search a couple of tiles past the room's edge.
            Bounds doorSearch = RoomBounds(room, 2f);
            List<DungeonDoor> roomDoors = new List<DungeonDoor>();
            foreach (GameObject doorObject in doorGameObjects)
            {
                DungeonDoor door = doorObject != null ? doorObject.GetComponent<DungeonDoor>() : null;
                if (door != null && doorSearch.Contains(door.transform.position))
                    roomDoors.Add(door);
            }

            room.gameObject.AddComponent<PuzzleRoom>().Setup(puzzle, roomDoors, RoomBounds(room, -1f));
        }
    }

    // Places the puzzle on a floor tile at least one tile in from the walls.
    private RoomPuzzle SpawnPuzzle(Room room)
    {
        Bounds inner = RoomBounds(room, -1f);
        List<Transform> tiles = new List<Transform>();
        for (int i = 0; i < room.transform.childCount; i++)
        {
            Transform child = room.transform.GetChild(i);
            if (child.CompareTag("Floor") && !room.MidPoints.Contains(child.gameObject)
                && !room.Corners.Contains(child.gameObject) && inner.Contains(child.position) && !IsOccupied(child))
                tiles.Add(child);
        }

        if (tiles.Count == 0)
        {
            Debug.LogWarning($"{room.name} is too small for a puzzle.");
            return null;
        }

        Vector3 floor = tiles[Random.Range(0, tiles.Count)].position;
        // Entries added in the Inspector without a prefab assigned are skipped.
        List<GameObject> puzzlePrefabs = new List<GameObject>();
        if (puzzles != null)
        {
            foreach (ObjectList entry in puzzles)
            {
                if (entry != null && entry.gameObject != null)
                    puzzlePrefabs.Add(entry.gameObject);
            }
        }

        GameObject puzzleObject;
        if (puzzlePrefabs.Count > 0)
        {
            GameObject prefab = puzzlePrefabs[Random.Range(0, puzzlePrefabs.Count)];
            puzzleObject = Instantiate(prefab, floor + Vector3.up * prefab.transform.position.y, Quaternion.identity, room.transform);
        }
        else
        {
            puzzleObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            puzzleObject.name = "Lever (Placeholder)";
            puzzleObject.transform.SetParent(room.transform);
            puzzleObject.transform.localScale = new Vector3(0.3f, 0.6f, 0.3f);
            puzzleObject.transform.position = floor + Vector3.up * 0.6f;
            puzzleObject.GetComponent<Renderer>().material.color = Color.yellow;
            puzzleObject.AddComponent<LeverPuzzle>();
        }

        RoomPuzzle puzzle = puzzleObject.GetComponentInChildren<RoomPuzzle>();
        if (puzzle == null)
            Debug.LogWarning($"Puzzle prefab {puzzleObject.name} has no RoomPuzzle component.");
        return puzzle;
    }

    // The room's floor-tile centres, grown (or shrunk, if negative) by padding tiles.
    private static Bounds RoomBounds(Room room, float padding)
    {
        Vector2Int size = room.GeneratedSize;
        return new Bounds(room.transform.position,
            new Vector3(size.x - 1 + padding * 2f, 10f, size.y - 1 + padding * 2f));
    }

    // Deliberately standalone from GenerateContent/the wall-object/corner
    // pipeline: the count comes from powerupsPerRoom and the contents from the
    // optional loot table, so neither PcgGenerationParameters nor the policy
    // mappings change when items are added.
    private void SpawnQaPlaceholders()
    {
        for (int i = 0; i < parentRooms.Length; i++)
        {
            Room room = parentRooms[i];
            if (room == null || room == bossSpawnRoom)
                continue; // The boss room is the QA exit goal, not a loot room.

            for (int p = 0; p < powerupsPerRoom; p++)
                SpawnPowerupPlaceholder(room);
        }
    }

    private void SpawnPowerupPlaceholder(Room room)
    {
        ItemDefinition item = RollLoot();
        GameObject prefab = item != null && item.PickupPrefab != null ? item.PickupPrefab : lootPickup;
        Vector3 floor = GetRandomFloorPositionInRoom(room);

        GameObject powerup;
        if (prefab != null)
        {
            // Like other generated objects, the prefab's own Y is its height above the floor.
            powerup = Instantiate(prefab, floor + Vector3.up * prefab.transform.position.y, Quaternion.identity, room.transform);
        }
        else
        {
            powerup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            powerup.transform.SetParent(room.transform);
            // Sized/positioned to straddle QaPlayerController's ray sensor height (0.6);
            // the previous 0.4-scale cube at y=0.3 topped out at y=0.5, just under the
            // rays, so it was invisible to Sense() no matter how close the player got.
            powerup.transform.localScale = Vector3.one * 0.6f;
            powerup.transform.position = floor + Vector3.up * 0.5f;
            powerup.GetComponent<Renderer>().material.color = item != null ? item.PickupColor : Color.green;
        }

        powerup.name = item != null ? item.DisplayName : "QaPowerup (Placeholder)";
        QaLootPickup pickup = powerup.GetComponent<QaLootPickup>();
        if (pickup == null)
            pickup = powerup.AddComponent<QaLootPickup>();
        pickup.Item = item;
    }

    // Weighted pick from the loot table; null (a plain QA powerup) when the table is empty.
    private ItemDefinition RollLoot()
    {
        if (lootTable == null)
            return null;

        int totalWeight = 0;
        foreach (LootEntry entry in lootTable)
        {
            if (entry.item != null)
                totalWeight += entry.weight;
        }

        if (totalWeight <= 0)
            return null;

        int roll = Random.Range(0, totalWeight);
        foreach (LootEntry entry in lootTable)
        {
            if (entry.item == null)
                continue;

            roll -= entry.weight;
            if (roll < 0)
                return entry.item;
        }

        return null;
    }

    private Vector3 GetRandomFloorPositionInRoom(Room room)
    {
        List<Transform> floorTiles = new List<Transform>();
        for (int i = 0; i < room.transform.childCount; i++)
        {
            Transform child = room.transform.GetChild(i);
            if (child.CompareTag("Floor") && !IsOccupied(child))
                floorTiles.Add(child);
        }

        return floorTiles.Count > 0
            ? floorTiles[Random.Range(0, floorTiles.Count)].position
            : room.transform.position;
    }

    private Vector2 CalculateCenterPoint(Vector2Int area, Vector2Int pos)
    {
        Vector2 tempRoomPos = pos;

        if (area.x == 2 || area.x == 4)
        {
            tempRoomPos.x -= 0.5f;
        }

        if (area.y == 2 || area.y == 4)
        {
            tempRoomPos.y -= 0.5f;
        }

        Vector2 tempPos = new Vector2(tempRoomPos.x + (area.x / 2), tempRoomPos.y + (area.y / 2));

        return tempPos;
    }

    private void GenerateWalls()
    {
        //Loop through the whole area
        //check each object, and then check all 8 blocks around if there is something
        for (int a = 0; a < area.GetLength(0); a++)
        {
            for (int b = 0; b < area.GetLength(1); b++)
            {
                if(CheckNullAndTag("Floor", true, area[a, b]))
                {
                    CreateWalls(a, b);

                    CreateOuterCorners(a, b);

                    CreateInnerCorners(a, b);
                }
            }
        }
    }

    //TODO: Make functions to compact code
    private void DoubleCheckCorners()
    {
        foreach (GameObject obj in area)
        {
            if (obj != null)
            {
                if (obj.tag == "OuterCorner")
                {
                    int a = (int)obj.transform.position.x;
                    int b = (int)obj.transform.position.z;

                    int amountOfThrough = 0;

                    //Check up and down
                    if(CheckNullAndTag("Floor", true, area[a, b + 1], area[a, b - 1]))
                    {
                        //Right side from up to down
                        if (CheckNullAndTag("Floor", true, area[a + 1, b + 1], area[a + 1, b - 1], area[a + 1, b]))
                        {
                            amountOfThrough += 1;
                            DestroyImmediate(area[a, b]);

                            int randomUcorner = Random.Range(0, uCorners.Count);
                            area[a, b] = Instantiate(uCorners[randomUcorner].gameObject, new Vector3(a, uCorners[randomUcorner].gameObject.transform.position.y, b), Quaternion.Euler(0, -90, 0), area[a + 1, b + 1].transform.parent);
                            area[a, b].name = uCorners[randomUcorner].name + ": 1";
                        }

                        //Left side from up to down
                        if (CheckNullAndTag("Floor", true, area[a - 1, b + 1], area[a - 1, b], area[a - 1, b - 1]))
                        {
                            amountOfThrough += 1;
                            DestroyImmediate(area[a, b]);
                            int randomUcorner = Random.Range(0, uCorners.Count);
                            area[a, b] = Instantiate(uCorners[randomUcorner].gameObject, new Vector3(a, uCorners[randomUcorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 90, 0), area[a - 1, b + 1].transform.parent);
                            area[a, b].name = uCorners[randomUcorner].name + ": 2";
                        }   
                    }

                    if (CheckNullAndTag("Floor", true, area[a + 1, b], area[a - 1, b]))
                    {
                        if (CheckNullAndTag("Floor", true, area[a, b + 1], area[a + 1, b + 1], area[a - 1, b + 1]))
                        {
                            amountOfThrough += 1;
                            DestroyImmediate(area[a, b]);
                            int randomUcorner = Random.Range(0, uCorners.Count);
                            area[a, b] = Instantiate(uCorners[randomUcorner].gameObject, new Vector3(a, uCorners[randomUcorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 180, 0), area[a, b + 1].transform.parent);
                            area[a, b].name = uCorners[randomUcorner].name + ": 3";
                        }

                        if (CheckNullAndTag("Floor", true, area[a, b - 1], area[a + 1, b - 1], area[a - 1, b - 1]))
                        {
                            amountOfThrough += 1;
                            DestroyImmediate(area[a, b]);
                            int randomUcorner = Random.Range(0, uCorners.Count);
                            area[a, b] = Instantiate(uCorners[randomUcorner].gameObject, new Vector3(a, uCorners[randomUcorner].gameObject.transform.position.y, b), Quaternion.identity, area[a, b - 1].transform.parent);
                            area[a, b].name = uCorners[randomUcorner].name + ": 4";
                        } 
                    }

                    if (amountOfThrough == 4)
                    {
                        //THIS ISN?T WORKING
                        DestroyImmediate(area[a, b]);
                        int randomUcorner = Random.Range(0, uCorners.Count);
                        area[a, b] = Instantiate(squareCorners[randomUcorner].gameObject, new Vector3(a, uCorners[randomUcorner].gameObject.transform.position.y, b), Quaternion.identity, area[a, b - 1].transform.parent);
                        area[a, b].name = uCorners[randomUcorner].name + ": SQUARE";
                    }
                }
                else if (obj.tag == "Wall")
                {
                    float localX = obj.transform.position.x;
                    float localY = obj.transform.position.z;

                    int a = 0;
                    int b = 0;

                    if (localX % 1 > 0.5f)
                    {
                        a = (int)obj.transform.position.x + 1;

                    }
                    else if (localX % 1 < 0.5f)
                    {
                        a = (int)obj.transform.position.x;
                    }

                    if (localY % 1 > 0.5f)
                    {
                        b = (int)obj.transform.position.z + 1;
                    }
                    else if (localY % 1 < 0.5f)
                    {
                        b = (int)obj.transform.position.z;
                    }


                    if (CheckNullAndTag("Floor", true, area[a + 1, b], area[a + 1, b - 1], area[a, b - 1]))
                    {
                        if (CheckNullAndTag("Floor", false, area[a - 1, b], area[a, b + 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.identity, area[a + 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 1a";
                        }

                        if (CheckNullAndTag("Floor", false, area[a - 1, b], area[a + 1, b + 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.identity, area[a + 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 1b";
                        }

                        if (CheckNullAndTag("Floor", false, area[a - 1, b - 1], area[a, b]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.identity, area[a + 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 1c";
                        }

                        if (CheckNullAndTag("Floor", false, area[a - 1, b - 1], area[a + 1, b + 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.identity, area[a + 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 1d";
                        }

                        if (CheckNullAndTag("Floor", true, area[a - 1, b - 1], area[a - 1, b]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomUcorner = Random.Range(0, uCorners.Count);
                            area[a, b] = Instantiate(uCorners[randomUcorner].gameObject, new Vector3(a, uCorners[randomUcorner].gameObject.transform.position.y, b), Quaternion.identity, area[a + 1, b].transform.parent);
                            area[a, b].name = uCorners[randomUcorner].name + ": 1e";
                        }
                    }

                    if (CheckNullAndTag("Floor", true, area[a - 1, b], area[a - 1, b - 1], area[a, b - 1]))
                    {
                        if (CheckNullAndTag("Floor", false, area[a + 1, b], area[a, b + 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 90, 0), area[a - 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 2a";
                        }

                        if (CheckNullAndTag("Floor", false, area[a + 1, b - 1], area[a, b + 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 90, 0), area[a - 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 2b";
                        }

                        if (CheckNullAndTag("Floor", false, area[a + 1, b], area[a - 1, b + 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 90, 0), area[a - 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 2c";
                        }

                        if (CheckNullAndTag("Floor", false, area[a + 1, b - 1], area[a - 1, b + 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 90, 0), area[a - 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 2d";
                        }

                        if (CheckNullAndTag("Floor", true, area[a - 1, b + 1], area[a, b + 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomUcorner = Random.Range(0, uCorners.Count);
                            area[a, b] = Instantiate(uCorners[randomUcorner].gameObject, new Vector3(a, uCorners[randomUcorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 90, 0), area[a - 1, b].transform.parent);
                            area[a, b].name = uCorners[randomUcorner].name + ": 2e";
                        }
                    }

                    if (CheckNullAndTag("Floor", true, area[a - 1, b], area[a - 1, b + 1], area[a, b + 1]))
                    {
                        if (CheckNullAndTag("Floor", false, area[a, b - 1], area[a + 1, b]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 180, 0), area[a - 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 3a";
                        }

                        if (CheckNullAndTag("Floor", false, area[a, b - 1], area[a + 1, b + 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 180, 0), area[a - 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 3b";
                        }

                        if (CheckNullAndTag("Floor", false, area[a - 1, b - 1], area[a + 1, b]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 180, 0), area[a - 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 3cunts";
                        }

                        if (CheckNullAndTag("Floor", false, area[a - 1, b - 1], area[a + 1, b + 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 180, 0), area[a - 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 3d";
                        }

                        if (CheckNullAndTag("Floor", true, area[a + 1, b + 1], area[a + 1, b ]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomUcorner = Random.Range(0, uCorners.Count);
                            area[a, b] = Instantiate(uCorners[randomUcorner].gameObject, new Vector3(a, uCorners[randomUcorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 180, 0), area[a - 1, b].transform.parent);
                            area[a, b].name = uCorners[randomUcorner].name + ": 3e";
                        }
                    }

                    if (CheckNullAndTag("Floor", true, area[a + 1, b], area[a + 1, b + 1], area[a, b + 1]))
                    {
                        if (CheckNullAndTag("Floor", false, area[a, b - 1], area[a - 1, b]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.Euler(0, -90, 0), area[a + 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 4a";
                        }

                        if (CheckNullAndTag("Floor", false, area[a, b - 1], area[a - 1, b + 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.Euler(0, -90, 0), area[a + 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 4b";
                        }

                        if (CheckNullAndTag("Floor", false, area[a + 1, b - 1], area[a - 1, b]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.Euler(0, -90, 0), area[a + 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 4c";
                        }

                        if (CheckNullAndTag("Floor", false, area[a + 1, b - 1], area[a - 1, b + 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomOutercorner = Random.Range(0, outerCorners.Count);
                            area[a, b] = Instantiate(outerCorners[randomOutercorner].gameObject, new Vector3(a, outerCorners[randomOutercorner].gameObject.transform.position.y, b), Quaternion.Euler(0, -90, 0), area[a + 1, b].transform.parent);
                            area[a, b].name = outerCorners[randomOutercorner].name + ": 4d";
                        }

                        if (CheckNullAndTag("Floor", true, area[a, b - 1], area[a + 1, b - 1]))
                        {
                            DestroyImmediate(area[a, b]);
                            int randomUcorner = Random.Range(0, uCorners.Count);
                            area[a, b] = Instantiate(uCorners[randomUcorner].gameObject, new Vector3(a, uCorners[randomUcorner].gameObject.transform.position.y, b), Quaternion.Euler(0, -90, 0), area[a + 1, b].transform.parent);
                            area[a, b].name = uCorners[randomUcorner].name + ": 4e";
                        }
                    }
                }
            }
        }
    }

    //TODO: Make functions to compact code
    private void CreateWalls(int a, int b)
    {
        //CREATE WALLS MAKE THIS FUNCTION
        //========================================================
        //Check all sides, counting from up, down, left and rights
        //Check each side and see which one needs a wall
        bool isXBigger = false;

        //Check if Left side of floor needs wall
        if (CheckIfNull(area[a - 1, b]))
        {
            int angle = 0;
            int randomWall = Random.Range(0, walls.Count);

            float displacement = CheckWallSize(walls[randomWall].gameObject, ref isXBigger);
            displacement = displacement / 2;

            if (isXBigger) angle = 90;

            area[a - 1, b] = Instantiate(walls[randomWall].gameObject, new Vector3(a - 0.5f - displacement, walls[randomWall].gameObject.transform.position.y, b), Quaternion.Euler(new Vector3(0, angle, 0)));
            area[a - 1, b].name = walls[randomWall].name + " 1 A";
            //Check if walls are needed on the other side in the same block

            if (CheckNullAndTag("Floor", true, area[a - 2, b]))
            {
                angle = 0;
                randomWall = Random.Range(0, walls.Count);

                displacement = CheckWallSize(walls[randomWall].gameObject, ref isXBigger);
                displacement = displacement / 2;

                if (isXBigger) angle = 90;

                //Create a corridor on the other side, but the same block itself
                GameObject localObject = Instantiate(walls[randomWall].gameObject, new Vector3(a - 1.5f + displacement, walls[randomWall].gameObject.transform.position.y, b), Quaternion.Euler(new Vector3(0, angle, 0)));
                localObject.name = walls[randomWall].name + " 1 B";
                //Set this object's parent to be the object created previously, so when it needs to get destroyed it will get destroyed
                //Since it can't be accessed through the array.
                localObject.transform.parent = area[a - 1, b].transform;
            }

            area[a - 1, b].transform.SetParent(area[a, b].transform.parent);
        }

        //Check if Down side of floor needs wall
        if (CheckIfNull(area[a, b - 1]))
        {
            int angle = -90;
            int randomWall = Random.Range(0, walls.Count);

            float displacement = CheckWallSize(walls[randomWall].gameObject, ref isXBigger);
            displacement = displacement / 2;

            if (isXBigger) angle = 0;

            area[a, b - 1] = Instantiate(walls[randomWall].gameObject, new Vector3(a, walls[randomWall].gameObject.transform.position.y, b - 0.5f - displacement), Quaternion.Euler(new Vector3(0, angle, 0)));
            area[a, b - 1].name = walls[randomWall].name + " 2 A";

            //Check if walls are needed on the other side in the same block
            if (CheckNullAndTag("Floor", true, area[a, b - 2]))
            {
                angle = -90;
                randomWall = Random.Range(0, walls.Count);
                displacement = CheckWallSize(walls[randomWall].gameObject, ref isXBigger);
                displacement = displacement / 2;

                if (isXBigger) angle = 0;

                //Create a corridor on the other side, but the same block itself
                GameObject localObject = Instantiate(walls[randomWall].gameObject, new Vector3(a, walls[randomWall].gameObject.transform.position.y, b - 1.5f + displacement), Quaternion.Euler(new Vector3(0, angle, 0)));
                localObject.name = walls[randomWall].name + " 2 B";
                //Set this object's parent to be the object created previously, so when it needs to get destroyed it will get destroyed
                //Since it can't be accessed through the array.
                localObject.transform.parent = area[a, b - 1].transform;
            }

            area[a, b - 1].transform.SetParent(area[a, b].transform.parent);
        }

        //Check if Up side of floor needs wall
        if (CheckIfNull(area[a, b + 1]))
        {
            int angle = 90;
            int randomWall = Random.Range(0, walls.Count);

            float displacement = CheckWallSize(walls[randomWall].gameObject, ref isXBigger);
            displacement = displacement / 2;

            if (isXBigger) angle = 180;

            area[a, b + 1] = Instantiate(walls[randomWall].gameObject, new Vector3(a, walls[randomWall].gameObject.transform.position.y, b + 0.5f + displacement), Quaternion.Euler(new Vector3(0, angle, 0)));
            area[a, b + 1].name = walls[randomWall].name + " 3 A";
            //Check if walls are needed on the other side in the same block
            if (CheckNullAndTag("Floor", true, area[a, b + 2]))
            {
                angle = 90;
                randomWall = Random.Range(0, walls.Count);
                displacement = CheckWallSize(walls[randomWall].gameObject, ref isXBigger);
                displacement = displacement / 2;

                if (isXBigger) angle = 180;

                //Create a corridor on the other side, but the same block itself
                GameObject localObject = Instantiate(walls[randomWall].gameObject, new Vector3(a, walls[randomWall].gameObject.transform.position.y, b + 1.5f - displacement), Quaternion.Euler(new Vector3(0, angle, 0)));
                localObject.name = walls[randomWall].name + "3 B";
                //Set this object's parent to be the object created previously, so when it needs to get destroyed it will get destroyed
                //Since it can't be accessed through the array.
                localObject.transform.parent = area[a, b + 1].transform;
            }

            area[a, b + 1].transform.SetParent(area[a, b].transform.parent);
        }

        //Check if Right side of floor needs wall
        if (CheckIfNull(area[a + 1, b]))
        {
            int angle = 180;
            int randomWall = Random.Range(0, walls.Count);

            float displacement = CheckWallSize(walls[randomWall].gameObject, ref isXBigger);
            displacement = displacement / 2;

            if(isXBigger) angle = 90;

            area[a + 1, b] = Instantiate(walls[randomWall].gameObject, new Vector3(a + 0.5f + displacement, walls[randomWall].gameObject.transform.position.y, b), Quaternion.Euler(new Vector3(0, angle, 0)));
            area[a + 1, b].name = walls[randomWall].name + " 4 A";

            //Check if walls are needed on the other side in the same block
            if (CheckNullAndTag("Floor", true, area[a + 2, b]))
            {
                angle = 180;
                randomWall = Random.Range(0, walls.Count);
                //Create a corridor on the other side, but the same block itself
                displacement = CheckWallSize(walls[randomWall].gameObject, ref isXBigger);
                displacement = displacement / 2;

                if (isXBigger) angle = 90;

                GameObject localObject = Instantiate(walls[randomWall].gameObject, new Vector3(a + 1.5f - displacement, walls[randomWall].gameObject.transform.position.y, area[a + 1, b].transform.position.z), Quaternion.Euler(new Vector3(0, angle, 0)));
                localObject.name = walls[randomWall].name + "4 B";
                //Set this object's parent to be the object created previously, so when it needs to get destroyed it will get destroyed
                //Since it can't be accessed through the array.
                localObject.transform.parent = area[a + 1, b].transform;
            }

            area[a + 1, b].transform.SetParent(area[a, b].transform.parent);
        }
    }

    private void CreateOuterCorners(int a, int b)
    {
        //Check if Right and Left side of floor have walls
        int number;
        int extraRotation = 0;

        //Always check if it is null first before trying to get the tag of the object.
        if (CheckNullAndTag("Floor", false, area[a + 1, b], area[a - 1, b]))
        { 
            //Check if object down is a floor
            if (CheckNullAndTag("Floor", true, area[a, b - 1]))
            {
                //Check if Object down and to the right is a floor

                if (CheckNullAndTag("Floor", true, area[a + 1, b - 1]))
                {
                    number = 1;
                    int randomOuterCorner = Random.Range(0, outerCorners.Count);

                    //Destroy previous object in the list and create new one
                    DestroyImmediate(area[a + 1, b]);
                    area[a + 1, b] = Instantiate(outerCorners[randomOuterCorner].gameObject, new Vector3(a + 1, outerCorners[randomOuterCorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 90, 0), area[a + 1, b - 1].transform.parent);
                    area[a + 1, b].name = outerCorners[randomOuterCorner].name + number;
                }

                if (CheckNullAndTag("Floor", true, (area[a - 1, b - 1])))
                {
                    number = 2;
                    int randomOuterCorner = Random.Range(0, outerCorners.Count);

                    //Destroy previous object in the list and create new one
                    DestroyImmediate(area[a - 1, b]);
                    area[a - 1, b] = Instantiate(outerCorners[randomOuterCorner].gameObject, new Vector3(a - 1, outerCorners[randomOuterCorner].gameObject.transform.position.y, b), Quaternion.identity, (area[a - 1, b - 1]).transform.parent);
                    area[a - 1, b].name = outerCorners[randomOuterCorner].name + number;
                }
            }

            //Check if object Up is a floor
            if (CheckNullAndTag("Floor", true, area[a, b + 1]))
            {
                //Check if Object up and to the left is a floor
                if (CheckNullAndTag("Floor", true, area[a - 1, b + 1]))
                {
                    number = 3;
                    int randomOuterCorner = Random.Range(0, outerCorners.Count);
                    //Destroy previous object in the list and create new one
                    DestroyImmediate(area[a - 1, b]);
                    area[a - 1, b] = Instantiate(outerCorners[randomOuterCorner].gameObject, new Vector3(a - 1, outerCorners[randomOuterCorner].gameObject.transform.position.y, b), Quaternion.Euler(0, -90, 0), area[a - 1, b + 1].transform.parent);
                    area[a - 1, b].name = outerCorners[randomOuterCorner].name + number;
                }

                //Check if Object up and to the right is a floor
                if (CheckNullAndTag("Floor", true, area[a + 1, b + 1]))
                {
                    number = 4;
                    int randomOuterCorner = Random.Range(0, outerCorners.Count);
                    //Destroy previous object in the list and create new one
                    DestroyImmediate(area[a + 1, b]);
                    area[a + 1, b] = Instantiate(outerCorners[randomOuterCorner].gameObject, new Vector3(a + 1, outerCorners[randomOuterCorner].gameObject.transform.position.y, b), Quaternion.Euler(0, 180, 0), area[a + 1, b + 1].transform.parent);
                    area[a + 1, b].name = outerCorners[randomOuterCorner].name + number;
                }
            }
        }

        //Check if Up and Down side of floor have walls

        //Always check if it is null first before trying to get the tag of the object.
        if (CheckNullAndTag("Floor", false, area[a, b + 1], area[a, b - 1]))
        {

            //Check if object to the right is a floor
            if (CheckNullAndTag("Floor", true, area[a + 1, b]))
            {
                // Check if Object down and to the right is a floor

                if (CheckNullAndTag("Floor", true, area[a + 1, b - 1]))
                {
                    number = 5;
                    int randomOuterCorner = Random.Range(0, outerCorners.Count);
                    //Destroy previous object in the list and create new one
                    DestroyImmediate(area[a, b - 1]);
                    area[a, b - 1] = Instantiate(outerCorners[randomOuterCorner].gameObject, new Vector3(a, outerCorners[randomOuterCorner].gameObject.transform.position.y, b - 1), Quaternion.Euler(0, -90, 0), area[a + 1, b - 1].transform.parent);
                    area[a, b - 1].name = outerCorners[randomOuterCorner].name + number;
                }

                // Check if Object up and to the right is a floor
                if (CheckNullAndTag("Floor", true, area[a + 1, b + 1])) 
                {
                    number = 6;
                    int randomOuterCorner = Random.Range(0, outerCorners.Count);
                    //Destroy previous object in the list and create new one
                    DestroyImmediate(area[a, b + 1]);
                    area[a, b + 1] = Instantiate(outerCorners[randomOuterCorner].gameObject, new Vector3(a, outerCorners[randomOuterCorner].gameObject.transform.position.y, b + 1), Quaternion.identity, area[a + 1, b + 1].transform.parent);
                    area[a, b + 1].name = outerCorners[randomOuterCorner].name + number;
                }
            }

            //Check if object to the left is a floor
            if(CheckNullAndTag("Floor", true, area[a - 1, b]))
            {
                // Check if Object down and to the left is a floor
                if(CheckNullAndTag("Floor", true, area[a - 1, b - 1]))
                {
                    number = 7;
                    int randomOuterCorner = Random.Range(0, outerCorners.Count);
                    //Destroy previous object in the list and create new one
                    DestroyImmediate(area[a, b - 1]);
                    area[a, b - 1] = Instantiate(outerCorners[randomOuterCorner].gameObject, new Vector3(a, outerCorners[randomOuterCorner].gameObject.transform.position.y, b - 1), Quaternion.Euler(0, 180, 0), area[a - 1, b - 1].transform.parent);
                    area[a, b - 1].name = outerCorners[randomOuterCorner].name + number;
                }

                // Check if Object up and to the left is a floor

                if (CheckNullAndTag("Floor", true, area[a - 1, b + 1]))
                {               
                    number = 8;
                    int randomOuterCorner = Random.Range(0, outerCorners.Count);
                    //Destroy previous object in the list and create new one
                    DestroyImmediate(area[a, b + 1]);
                    area[a, b + 1] = Instantiate(outerCorners[randomOuterCorner].gameObject, new Vector3(a, outerCorners[randomOuterCorner].gameObject.transform.position.y, b + 1), Quaternion.Euler(0, 90, 0), area[a - 1, b + 1].transform.parent);
                    area[a, b + 1].name = outerCorners[randomOuterCorner].name + number;
                }
            }
        }
    }

    //TODO: Make functions to compact code
    private void CreateInnerCorners(int a, int b)
    {
        if(innerCorners.Count > 0)
        {
            int randomInnerCorner = Random.Range(0, this.innerCorners.Count );
            GameObject innerCorner = null;
            Vector2Int innerCornersCoords = new Vector2Int();
    
            if (CheckNullAndTag("Wall", "OuterCorner", true, area[a - 1, b], area[a, b - 1]))
            {
                innerCornersCoords.x = -1;
                innerCornersCoords.y = -1;
    
                innerCorner = Instantiate(this.innerCorners[randomInnerCorner].gameObject, new Vector3(a - this.innerCorners[randomInnerCorner].gameObject.transform.position.x, this.innerCorners[randomInnerCorner].gameObject.transform.position.y, b - this.innerCorners[randomInnerCorner].gameObject.transform.position.z), Quaternion.identity, area[a - 1, b].transform.parent);
            }
            else if (CheckNullAndTag("Wall", "OuterCorner", true, area[a - 1, b], area[a, b + 1]))
            {
                innerCornersCoords.x = -1;
                innerCornersCoords.y = 1;
    
                innerCorner = Instantiate(this.innerCorners[randomInnerCorner].gameObject, new Vector3(a - this.innerCorners[randomInnerCorner].gameObject.transform.position.x, this.innerCorners[randomInnerCorner].gameObject.transform.position.y, b + this.innerCorners[randomInnerCorner].gameObject.transform.position.z), Quaternion.Euler(new Vector3(0, 90, 0)), area[a - 1, b].transform.parent);
            }
            else if (CheckNullAndTag("Wall", "OuterCorner", true, area[a + 1, b], area[a, b - 1]))
            {
                innerCornersCoords.x = 1;
                innerCornersCoords.y = -1;
    
                innerCorner = Instantiate(this.innerCorners[randomInnerCorner].gameObject, new Vector3(a + this.innerCorners[randomInnerCorner].gameObject.transform.position.x, this.innerCorners[randomInnerCorner].gameObject.transform.position.y, b - this.innerCorners[randomInnerCorner].gameObject.transform.position.z), Quaternion.Euler(new Vector3(0, -90, 0)), area[a + 1, b].transform.parent);
            }
            else if (CheckNullAndTag("Wall", "OuterCorner", true, area[a + 1, b], area[a, b + 1]))
            {
                innerCornersCoords.x = 1;
                innerCornersCoords.y = 1;
    
                innerCorner = Instantiate(this.innerCorners[randomInnerCorner].gameObject, new Vector3(a + this.innerCorners[randomInnerCorner].gameObject.transform.position.x, this.innerCorners[randomInnerCorner].gameObject.transform.position.y, b + this.innerCorners[randomInnerCorner].gameObject.transform.position.z), Quaternion.Euler(new Vector3(0, 180, 0)), area[a + 1, b].transform.parent);
            }
    
            if(deleteInnerCorners && innerCornersCoords.x != 0 && innerCornersCoords.y != 0)
            {
                DestroyImmediate(area[a + innerCornersCoords.x, b]);
                DestroyImmediate(area[a, b + innerCornersCoords.y]);
            }
    
            if(innerCorner != null)
            innerCorner.name = this.innerCorners[randomInnerCorner].name;
        }
    }

    private bool CheckNullAndTag(string tag, bool isTagEqual, params GameObject[] obj)
    {
        List<int> checkList = new List<int>();

        for (int i = 0; i < obj.Length; i++)
        {
            if (obj[i] != null)
            {
                if (isTagEqual)
                {
                    if (obj[i].tag == tag)
                    {
                        checkList.Add(i);
                    }
                }
                else
                {
                    if (obj[i].tag != tag)
                    {
                        checkList.Add(i);
                    }
                }
            }
        }
        
        if(checkList.Count == obj.Length)
        {
            return true;
        }
        
        return false;
    }

    private bool CheckNullAndTag(string tag1, string tag2, bool isTagEqual, params GameObject[] obj)
    {
        List<int> checkList = new List<int>();

        for (int i = 0; i < obj.Length; i++)
        {
            if (obj[i] != null)
            {
                if (isTagEqual)
                {
                    if (obj[i].tag == tag1 || obj[i].tag == tag2)
                    {
                        checkList.Add(i);
                    }
                }
                else
                {
                    if (obj[i].tag != tag1 && obj[i].tag != tag2)
                    {
                        checkList.Add(i);
                    }
                }
            }
        }

        if (checkList.Count == obj.Length)
        {
            return true;
        }

        return false;
    }

    private bool CheckIfNull(params GameObject[] obj)
    {
        List<int> checkList = new List<int>();

        for (int i = 0; i < obj.Length; i++)
        {
            if (obj[i] == null)
            {
                checkList.Add(i);
            }
        }

        if (checkList.Count == obj.Length)
        {
            return true;
        }

        return false;
    }

    private float CheckWallSize(GameObject obj, ref bool isXBigger)
    {
        Vector3 size = obj.GetComponent<MeshRenderer>().bounds.size;
        float displacement;
        isXBigger = false;

        if (size.x > size.z)
        {
            isXBigger = true;
            displacement = size.z;
        }
        else if (size.z > size.x)
        {
            displacement = size.x;
        }
        else
        {
            displacement = 0;
        }

        return displacement;
    }

    //TODO: Make functions to compact code
    private void DoubleCheckRoomCorners()
    {
        for (int i = 0; i < parentRooms.Length; i++)
        {
            for (int j = 0; j < parentRooms[i].Corners.Count; j++)
            {
                string tempString = parentRooms[i].Corners[j].name;
                string[] tempStringArray = tempString.Split(floorRemovables, System.StringSplitOptions.RemoveEmptyEntries);

                int tempIntY = int.Parse(tempStringArray[1]);
                int tempIntX = int.Parse(tempStringArray[0]);

                int randomWall = 0;

                if (area[tempIntX + 1, tempIntY].CompareTag("OuterCorner"))
                {
                    if(area[tempIntX, tempIntY + 1].CompareTag("OuterCorner"))
                    {
                        randomWall = Random.Range(0, walls.Count);
                        GameObject tempWall = Instantiate(walls[randomWall].gameObject, area[tempIntX + 1, tempIntY].transform.position, Quaternion.Euler(0, 0, 0), parentRooms[i].transform);
                        tempWall.name = walls[randomWall].name;

                        randomWall = Random.Range(0, walls.Count);
                        tempWall = Instantiate(walls[randomWall].gameObject, area[tempIntX, tempIntY + 1].transform.position, Quaternion.Euler(0, 0, 0), parentRooms[i].transform);
                        tempWall.name = walls[randomWall].name;
                    }

                    if(area[tempIntX, tempIntY - 1].CompareTag("OuterCorner"))
                    {
                        randomWall = Random.Range(0, walls.Count);
                        GameObject tempWall = Instantiate(walls[randomWall].gameObject, area[tempIntX + 1, tempIntY].transform.position, Quaternion.Euler(0, 0, 0), parentRooms[i].transform);
                        tempWall.name = walls[randomWall].name;

                        randomWall = Random.Range(0, walls.Count);
                        tempWall = Instantiate(walls[randomWall].gameObject, area[tempIntX, tempIntY - 1].transform.position, Quaternion.Euler(0,0,0), parentRooms[i].transform);
                        tempWall.name = walls[randomWall].name;
                    }
                }

                if (area[tempIntX - 1, tempIntY].CompareTag("OuterCorner"))
                {
                    if (area[tempIntX, tempIntY + 1].CompareTag("OuterCorner"))
                    {
                        randomWall = Random.Range(0, walls.Count);
                        GameObject tempWall = Instantiate(walls[randomWall].gameObject, area[tempIntX - 1, tempIntY].transform.position, Quaternion.Euler(0, 0, 0), parentRooms[i].transform);
                        tempWall.name = walls[randomWall].name;

                        randomWall = Random.Range(0, walls.Count);
                        tempWall = Instantiate(walls[randomWall].gameObject, area[tempIntX, tempIntY + 1].transform.position, Quaternion.Euler(0, 0, 0), parentRooms[i].transform);
                        tempWall.name = walls[randomWall].name;
                    }

                    if (area[tempIntX, tempIntY - 1].CompareTag("OuterCorner"))
                    {
                        randomWall = Random.Range(0, walls.Count);
                        GameObject tempWall = Instantiate(walls[randomWall].gameObject, area[tempIntX - 1, tempIntY].transform.position, Quaternion.Euler(0, 0, 0), parentRooms[i].transform);
                        tempWall.name = walls[randomWall].name;

                        randomWall = Random.Range(0, walls.Count);
                        tempWall = Instantiate(walls[randomWall].gameObject, area[tempIntX, tempIntY - 1].transform.position, Quaternion.Euler(0, 0, 0), parentRooms[i].transform);
                        tempWall.name = walls[randomWall].name;
                    }
                }
            }
        }
    }

    // Doors without a matching displacement entry sit centred on their floor tile.
    private float DoorDisplacement(int door)
    {
        return doorsDisplacements != null && door < doorsDisplacements.Count ? doorsDisplacements[door].z : 0f;
    }

    private void GenerateDoors()
    {
        if(doors.Count > 0)
        {
            for (int a = 0; a < area.GetLength(0); a++)
            {
                for (int b = 0; b < area.GetLength(1); b++)
                {
                    if(CheckNullAndTag("Floor", true, area[a, b]))
                    {
                        if(CheckNullAndTag("Floor", false, area[a + 1, b], area[a - 1, b]))
                        {
                            if(CheckNullAndTag("Floor", true, area[a, b + 1]))
                            {
                                if(CheckNullAndTag("Floor", true, area[a + 1, b + 1]) || CheckNullAndTag("Floor", true, area[a - 1, b + 1]))
                                {
                                    //Instantiate doors
                                    if (CheckNullAndTag("Floor", true, area[a - 1, b + 2]) || CheckNullAndTag("Floor", true, area[a + 1, b - 2]))
                                    {
                                        int randomDoor = Random.Range(0, doors.Count);

                                        Vector3 pos = new Vector3(area[a, b].transform.position.x, area[a, b].transform.position.y + doors[randomDoor].gameObject.transform.position.y, area[a, b].transform.position.z + DoorDisplacement(randomDoor));
                                        doorGameObjects.Add(Instantiate(doors[randomDoor].gameObject, pos, Quaternion.identity, area[a, b].transform.parent)); 
                                    }
                                }
                            }

                            if (CheckNullAndTag("Floor", true, area[a, b - 1]))
                            {
                                if (CheckNullAndTag("Floor", true, area[a + 1, b - 1]) || CheckNullAndTag("Floor", true, area[a - 1, b - 1]))
                                {
                                    if (CheckNullAndTag("Floor", true, area[a - 1, b - 2]) || CheckNullAndTag("Floor", true, area[a + 1, b - 2]))
                                    {
                                        int randomDoor = Random.Range(0, doors.Count);

                                        Vector3 pos = new Vector3(area[a, b].transform.position.x, area[a, b].transform.position.y + doors[randomDoor].gameObject.transform.position.y, area[a, b].transform.position.z - DoorDisplacement(randomDoor));
                                        doorGameObjects.Add(Instantiate(doors[randomDoor].gameObject, pos, Quaternion.identity, area[a, b].transform.parent));
                                    }
                                }
                            }
                        }
                        else if (CheckNullAndTag("Floor", false, area[a, b - 1], area[a, b + 1]))
                        {
                            if (CheckNullAndTag("Floor", true, area[a + 1, b]))
                            {
                                if (CheckNullAndTag("Floor", true, area[a + 1, b - 1]) || CheckNullAndTag("Floor", true, area[a + 1, b + 1]))
                                {
                                    if (CheckNullAndTag("Floor", true, area[a + 2, b - 1]) || CheckNullAndTag("Floor", true, area[a + 2, b + 1]))
                                    {
                                        int randomDoor = Random.Range(0, doors.Count);

                                        Vector3 pos = new Vector3(area[a, b].transform.position.x + DoorDisplacement(randomDoor), area[a, b].transform.position.y + doors[randomDoor].gameObject.transform.position.y, area[a, b].transform.position.z);
                                        doorGameObjects.Add(Instantiate(doors[randomDoor].gameObject, pos, Quaternion.Euler(0, 90, 0), area[a, b].transform.parent));
                                    }
                                }
                            }

                            if (CheckNullAndTag("Floor", true, area[a - 1, b]))
                            {
                                if (CheckNullAndTag("Floor", true, area[a - 1, b - 1]) || CheckNullAndTag("Floor", true, area[a - 1, b + 1]))
                                {
                                    if(CheckNullAndTag("Floor", true, area[a - 2, b - 1]) || CheckNullAndTag("Floor", true, area[a - 2, b + 1]))
                                    {
                                        int randomDoor = Random.Range(0, doors.Count);

                                        Vector3 pos = new Vector3(area[a, b].transform.position.x - DoorDisplacement(randomDoor), area[a, b].transform.position.y + doors[randomDoor].gameObject.transform.position.y, area[a, b].transform.position.z);
                                        doorGameObjects.Add(Instantiate(doors[randomDoor].gameObject, pos, Quaternion.Euler(0, -90, 0), area[a, b].transform.parent));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // Columns and arches the layout asked for. They go in after the walls and content,
    // like the other decoration passes, and mark their tiles as taken.
    private void PlaceFeatures(Predungeon predungeon)
    {
        foreach (PreFeature feature in predungeon.Features)
        {
            GameObject prefab = null;
            if (cathedralSettings != null)
                prefab = feature.Kind == PreFeatureKind.Column ? cathedralSettings.columnPrefab : cathedralSettings.archPrefab;

            if (prefab == null)
                continue;

            Transform room = parentRooms[feature.Room].transform;
            Vector3 position = new Vector3(feature.Position.x, 0, feature.Position.y);

            if (feature.Kind == PreFeatureKind.Column)
            {
                // Like other generated objects, the prefab's own Y is its height above the floor.
                GameObject column = Instantiate(prefab, new Vector3(position.x, prefab.transform.position.y, position.z), Quaternion.identity, room);
                column.name = "Column";
            }
            else
            {
                PlaceArch(prefab, room, position, feature.Span, feature.AlongX);
            }

            foreach (Vector2Int cell in feature.Cells)
            {
                occupiedTiles.Add(cell);
            }
        }
    }

    // The arch model can be authored at any size and orientation, so it is scaled and
    // turned from its bounds to span the requested tiles, and set down on the floor.
    private void PlaceArch(GameObject prefab, Transform room, Vector3 position, float span, bool alongX)
    {
        GameObject arch = Instantiate(prefab, position, Quaternion.identity, room);
        arch.name = "Arch";

        Renderer[] renderers = arch.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds bounds = CombinedBounds(renderers);
        bool modelAlongX = bounds.size.x >= bounds.size.z;

        float scale = span / Mathf.Max(bounds.size.x, bounds.size.z);
        float maxHeight = cathedralSettings.archMaxHeight;
        if (maxHeight > 0f && bounds.size.y * scale > maxHeight)
            scale = maxHeight / bounds.size.y;

        arch.transform.localScale *= scale;
        if (modelAlongX != alongX)
            arch.transform.Rotate(0f, 90f, 0f, Space.World);

        bounds = CombinedBounds(renderers);
        arch.transform.position += new Vector3(position.x - bounds.center.x, -bounds.min.y, position.z - bounds.center.z);
    }

    private static Bounds CombinedBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private bool IsOccupied(Transform tile)
    {
        return occupiedTiles.Contains(new Vector2Int(Mathf.RoundToInt(tile.position.x), Mathf.RoundToInt(tile.position.z)));
    }

    private void SpawnEntities()
    {
        DestroyImmediate(PlayerSpawnRoom.gameObject.GetComponent<BoxCollider>());

        if(player != null)
        { 
            player = Instantiate(Player, PlayerSpawnRoom.transform.position, Quaternion.identity, gameObject.transform);
        }

        if(bosses.Count > 0)
        {
            int randomBoss = Random.Range(0, bosses.Count);
            boss = Instantiate(bosses[randomBoss].gameObject, bossSpawnRoom.transform.position, Quaternion.identity, bossSpawnRoom.transform);
            bossSpawnRoom.name = "Boss Room";
            boss.name = bosses[randomBoss].name;
        }

        if(enemies.Count > 0)
        {
            SpawnEnemies();
        }

        SpawnExitZone();
    }

    // The boss room is the dungeon's farthest point from spawn, so reaching it
    // is what QA counts as completion rather than generation alone succeeding.
    private void SpawnExitZone()
    {
        if (bossSpawnRoom == null)
            return;

        GameObject exitZone;
        if (exitPrefab != null)
        {
            exitZone = Instantiate(exitPrefab, bossSpawnRoom.transform.position + Vector3.up * exitPrefab.transform.position.y, Quaternion.identity, bossSpawnRoom.transform);
        }
        else
        {
            exitZone = new GameObject("QaExitZone");
            exitZone.transform.SetParent(bossSpawnRoom.transform);
            exitZone.transform.position = bossSpawnRoom.transform.position;
            BuildExitPlaceholder(exitZone.transform);
        }

        // The trigger must exist before QaExitZone, which requires a collider.
        if (exitZone.GetComponent<Collider>() == null)
            exitZone.AddComponent<BoxCollider>().size = new Vector3(2f, 3f, 2f);

        QaExitZone exit = exitZone.GetComponent<QaExitZone>();
        if (exit == null)
            exit = exitZone.AddComponent<QaExitZone>();
        exit.RequireInteract = exitRequiresInteract;
    }

    // A bright pillar over a floor disc with a cyan light, visible from across a room.
    // Visuals have no colliders, so ray sensors and the player only ever touch the trigger.
    private static void BuildExitPlaceholder(Transform exit)
    {
        Color glow = new Color(0.2f, 0.9f, 1f);

        CreateExitPart(exit, PrimitiveType.Cylinder, "Pillar", new Vector3(0.5f, 2.5f, 0.5f), Vector3.up * 2.5f, glow);
        CreateExitPart(exit, PrimitiveType.Cylinder, "Base", new Vector3(2f, 0.03f, 2f), Vector3.up * 0.03f, glow);

        GameObject lightObject = new GameObject("Light");
        lightObject.transform.SetParent(exit);
        lightObject.transform.localPosition = Vector3.up * 2f;
        Light exitLight = lightObject.AddComponent<Light>();
        exitLight.type = LightType.Point;
        exitLight.color = glow;
        exitLight.range = 8f;
        exitLight.intensity = 4f;
    }

    private static void CreateExitPart(Transform parent, PrimitiveType shape, string partName, Vector3 scale, Vector3 localPosition, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        DestroyImmediate(part.GetComponent<Collider>());
        part.name = partName;
        part.transform.SetParent(parent);
        part.transform.localScale = scale;
        part.transform.localPosition = localPosition;

        Material material = part.GetComponent<Renderer>().material;
        material.color = color;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 2f);
    }

    private void GenerateContent()
    {
        for (int i = 0; i < parentRooms.Length; i++)
        {
            GenerateRoomWallObjs(i);
        }

        GenerateCorridorWallObjs();

        GenerateObjects();
        
        GenerateMiddleObjects();
    }

    private void GenerateRoomWallObjs(int i)
    {
        if(wallObjects.Count > 0)
        {
            //Generate lighting in each 4 corners of the rooms
            parentRooms[i].Corners.Add(parentRooms[i].transform.GetChild(0).gameObject);
            //parentRooms[i].transform.GetChild(0).gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.red);

            int childCornerNumber = roomArea[i].y - 1;
            parentRooms[i].Corners.Add(parentRooms[i].transform.GetChild(childCornerNumber).gameObject);
            //parentRooms[i].transform.GetChild(childCornerNumber).gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.red);

            childCornerNumber = (roomArea[i].x * roomArea[i].y) - roomArea[i].y;
            parentRooms[i].Corners.Add(parentRooms[i].transform.GetChild(childCornerNumber).gameObject);
            //parentRooms[i].transform.GetChild(childCornerNumber).gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.red);

            childCornerNumber = (roomArea[i].x * roomArea[i].y) - 1;
            parentRooms[i].Corners.Add(parentRooms[i].transform.GetChild(childCornerNumber).gameObject);
            //parentRooms[i].transform.GetChild(childCornerNumber).gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.red);

            List<int> tempCorners = new List<int>();
            for (int j = 0; j < parentRooms[i].Corners.Count; j++)
            {
                Vector3Int tempVec3 = CheckSidesCorridor(parentRooms[i].Corners[j], 2);

                if (tempVec3.x == 1)
                {
                    int randomWallObject = Random.Range(0, wallObjects.Count);
                    Vector3 tempTorchDisplacement = new Vector3(-wallObjDisplacements[randomWallObject].x, wallObjDisplacements[randomWallObject].y, -wallObjDisplacements[randomWallObject].z);
                    float extraYAngle = 0;

                    if(j == 1 || j == 3)
                    {
                        tempTorchDisplacement.x = -tempTorchDisplacement.x;
                        tempTorchDisplacement.z = -tempTorchDisplacement.z;
                        extraYAngle = 180;   
                    }

                    GameObject tempWallObject = Instantiate(wallObjects[randomWallObject].gameObject, new Vector3(area[tempVec3.y, tempVec3.z].transform.position.x + tempTorchDisplacement.x, tempTorchDisplacement.y, area[tempVec3.y, tempVec3.z].transform.position.z + tempTorchDisplacement.z), Quaternion.Euler(0, extraYAngle, 0), parentRooms[i].transform);
                    tempWallObject.name = wallObjects[randomWallObject].name;
                }
                else
                {
                    //Remove from list
                    tempCorners.Add(j);
                }

                if(j == 3)
                {
                    int tempInt = 0;
                    for (int a = 0; a < tempCorners.Count; a++)
                    {
                        parentRooms[i].Corners.Remove(parentRooms[i].Corners[tempCorners[a] - tempInt]);

                        tempInt += 1;
                    }
                }
            }
        }
    }

    private void GenerateCorridorWallObjs()
    {
        if(wallObjects.Count > 0)
        {
            for (int i = 0; i < corridors.Count; i++)
            {
                if( corridors[i].transform.childCount > wallObjConsequitiveDistance)
                {
                    int checkIteration = 0;

                    for (int j = 0; j < corridors[i].transform.childCount; j++)
                    {
                        if(j > 0 && corridors[i].transform.GetChild(j).CompareTag("Floor"))
                        {
                            Vector3 position = corridors[i].transform.GetChild(j).transform.position;

                            if(area[(int)position.x + 1, (int)position.z].CompareTag("Wall") && area[(int)position.x - 1, (int)position.z].CompareTag("Wall") && checkIteration == wallObjConsequitiveDistance)
                            {
                                int randomWallObject = Random.Range(0, wallObjects.Count);
                                GameObject tempWallObject = Instantiate(wallObjects[randomWallObject].gameObject, new Vector3(position.x + wallObjDisplacements[randomWallObject].z, wallObjDisplacements[randomWallObject].y, position.z + wallObjDisplacements[randomWallObject].x), Quaternion.Euler(0, -90, 0), corridors[i].transform);
                                tempWallObject.name = wallObjects[randomWallObject].name;
                                checkIteration = 0;
                            }
                            else if(area[(int)position.x, (int)position.z + 1].CompareTag("Wall") && area[(int)position.x, (int)position.z - 1].CompareTag("Wall") && checkIteration == wallObjConsequitiveDistance)
                            {
                                int randomWallObject = Random.Range(0, wallObjects.Count);
                                GameObject tempWallObject = Instantiate(wallObjects[randomWallObject].gameObject, new Vector3(position.x + wallObjDisplacements[randomWallObject].x, wallObjDisplacements[randomWallObject].y, position.z + wallObjDisplacements[randomWallObject].z), Quaternion.Euler(0, 180, 0), corridors[i].transform);
                                tempWallObject.name = wallObjects[randomWallObject].name;
                                checkIteration = 0;  
                            }

                            checkIteration += 1;
                        }
                    }
                }
            }
        }
    }

    private void SpawnEnemies()
    {
        if(enemies.Count > 0)
        {
            for (int i = 0; i < parentRooms.Length; i++)
            {

                if (PlayerSpawnRoom != parentRooms[i] && bossSpawnRoom != parentRooms[i] && parentRooms[i].Type != RoomType.Puzzle)
                {
                    int area = 0;

                    for (int j = 0; j < parentRooms[i].transform.childCount; j++)
                    {
                        if(parentRooms[i].transform.GetChild(j).CompareTag("Floor"))
                        {
                            area += 1;
                        }
                    }

                    // Small rooms can be all midpoints and corners, which would leave the loop below
                    // searching forever.
                    if (!HasEnemySpot(parentRooms[i]))
                        continue;

                    int enem = Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom);

                    for (int a = 0; a < enem; a++)
                    {
                        bool isInstantiated = false;

                        do
                        {
                            int randomChild = Random.Range(0, area);

                            GameObject tempChild = parentRooms[i].transform.GetChild(randomChild).gameObject;

                            if(tempChild.CompareTag("Floor") && !parentRooms[i].MidPoints.Contains(tempChild) && !parentRooms[i].Corners.Contains(tempChild) && !IsOccupied(tempChild.transform))
                            {
                                Vector3 pos = tempChild.transform.position;

                                int randomEnemy = Random.Range(0, Enemies.Count);
                                int randomRotation = Random.Range(-360, 361);
                                GameObject tempEnemy = Instantiate(Enemies[randomEnemy].gameObject, new Vector3(pos.x, pos.y + Enemies[randomEnemy].gameObject.transform.position.y, pos.z), Quaternion.Euler(0, randomRotation, 0), parentRooms[i].transform);
                                tempEnemy.name = Enemies[randomEnemy].name;
                                

                                isInstantiated = true;
                            }

                        } while (isInstantiated == false);
                    }
                }
            }
        }
    }

    private bool HasEnemySpot(Room room)
    {
        for (int i = 0; i < room.transform.childCount; i++)
        {
            GameObject child = room.transform.GetChild(i).gameObject;
            if (child.CompareTag("Floor") && !room.MidPoints.Contains(child) && !room.Corners.Contains(child) && !IsOccupied(child.transform))
                return true;
        }

        return false;
    }

    private Vector3Int CheckSidesCorridor(GameObject obj, int maxSurroundings)
    {
        int additive = 0;

        string tempString = obj.name;
        string[] tempStringArray = tempString.Split(floorRemovables, System.StringSplitOptions.RemoveEmptyEntries);

        int tempIntY = int.Parse(tempStringArray[1]);
        int tempIntX = int.Parse(tempStringArray[0]);

        if(area[tempIntX, tempIntY + 1].CompareTag("Floor"))
        {
            additive += 1;    
        }

        if (area[tempIntX, tempIntY - 1].CompareTag("Floor"))
        {
            additive += 1;
        }

        if (area[tempIntX + 1, tempIntY].CompareTag("Floor"))
        {
            additive += 1;
        }

        if (area[tempIntX - 1, tempIntY].CompareTag("Floor"))
        {
            additive += 1;
        }

        if(additive > maxSurroundings)
        {
            return new Vector3Int(0, 0, 0);
        }

        return new Vector3Int(1, tempIntX, tempIntY);
    }

    private Vector3Int CheckSidesCorridor(GameObject obj, int maxSurroundings, Vector2Int coords, params string[] tags)
    {
        int additive = 0;

        for (int i = 0; i < tags.Length; i++)
        {
            if(CheckNullAndTag(tags[i], true, area[coords.x, coords.y + 1]))
            {
                additive += 1;
            }

            if (CheckNullAndTag(tags[i], true, area[coords.x, coords.y - 1]))
            {
                additive += 1;
            }

            if (CheckNullAndTag(tags[i], true, area[coords.x + 1, coords.y]))
            {
                additive += 1;
            }

            if (CheckNullAndTag(tags[i], true, area[coords.x - 1, coords.y]))
            {
                additive += 1;
            }
        }

        if (additive > maxSurroundings)
        {
            return new Vector3Int(0, 0, 0);
        }

        return new Vector3Int(1, coords.x, coords.y);
    }

    //TODO: Make functions to compact code
    private void GenerateObjects()
    {
        if (staticObjects.Count > 0 && breakables.Count > 0)
        {
            int randomNumber = 0;

            int onlyXObjectMin = 0;
            int onlyXObjectMax = 2;

            if (staticObjects.Count == 0)
            {
                onlyXObjectMin = 1;
                onlyXObjectMax = 2;
            }
            else if (breakables.Count == 0)
            {
                onlyXObjectMin = 0;
                onlyXObjectMax = 1;
            }

            int staticOrBreakable = Random.Range(onlyXObjectMin, onlyXObjectMax);

            GameObject tempContent = null;

            if (staticOrBreakable == 0)
            {
                int randomStaticObject = Random.Range(0, staticObjects.Count);
                tempContent = staticObjects[randomStaticObject].gameObject;
                tempContent.name = staticObjects[randomStaticObject].name;
            }
            else
            {
                int randomBreakableObject = Random.Range(0, breakables.Count);
                tempContent = breakables[randomBreakableObject].gameObject;
                tempContent.name = breakables[randomBreakableObject].name;
            }

            for (int i = 0; i < parentRooms.Length; i++)
            {
                for (int j = 0; j < parentRooms[i].Corners.Count; j++)
                {
                    Vector2Int coords = new Vector2Int((int)parentRooms[i].Corners[j].transform.position.x, (int)parentRooms[i].Corners[j].transform.position.z);

                    Vector2Int additive = new Vector2Int();


                    if (area[coords.x + 1, coords.y].CompareTag("Floor"))
                    {
                        if (area[coords.x, coords.y + 1].CompareTag("Floor"))
                        {
                            additive.x = 1;
                            additive.y = 0;

                            if (CheckSidesCorridor(area[coords.x + additive.x, coords.y + additive.y], 3, new Vector2Int(coords.x + additive.x, coords.y + additive.y), "Floor", "Destroyables", "Static").x != 0)
                            {
                                RandomiseContent(ref randomNumber, ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);

                                CreateDestroyableRows(ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);
                            }

                            additive.x = 0;
                            additive.y = 1;

                            if (CheckSidesCorridor(area[coords.x + additive.x, coords.y + additive.y], 3, new Vector2Int(coords.x + additive.x, coords.y + additive.y), "Floor", "Destroyables", "Static").x != 0)
                            {
                                RandomiseContent(ref randomNumber, ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);

                                CreateDestroyableRows(ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);
                            }
                        }

                        if (area[coords.x, coords.y - 1].CompareTag("Floor"))
                        {
                            additive.x = 1;
                            additive.y = 0;

                            if (CheckSidesCorridor(area[coords.x + additive.x, coords.y + additive.y], 3, new Vector2Int(coords.x + additive.x, coords.y + additive.y), "Floor", "Destroyables", "Static").x != 0)
                            {
                                RandomiseContent(ref randomNumber, ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);

                                CreateDestroyableRows(ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);
                            }

                            additive.x = 0;
                            additive.y = -1;

                            if (CheckSidesCorridor(area[coords.x + additive.x, coords.y + additive.y], 3, new Vector2Int(coords.x + additive.x, coords.y + additive.y), "Floor", "Destroyables", "Static").x != 0)
                            {
                                RandomiseContent(ref randomNumber, ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);

                                CreateDestroyableRows(ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);
                            }
                        }
                    }

                    if (area[coords.x - 1, coords.y].CompareTag("Floor"))
                    {
                        if (area[coords.x, coords.y + 1].CompareTag("Floor"))
                        {
                            additive.x = -1;
                            additive.y = 0;

                            if (CheckSidesCorridor(area[coords.x + additive.x, coords.y + additive.y], 3, new Vector2Int(coords.x + additive.x, coords.y + additive.y), "Floor", "Destroyables", "Static").x != 0)
                            {
                                RandomiseContent(ref randomNumber, ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);

                                CreateDestroyableRows(ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);
                            }

                            additive.x = 0;
                            additive.y = 1;

                            if (CheckSidesCorridor(area[coords.x + additive.x, coords.y + additive.y], 3, new Vector2Int(coords.x + additive.x, coords.y + additive.y), "Floor", "Destroyables", "Static").x != 0)
                            {
                                RandomiseContent(ref randomNumber, ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);

                                CreateDestroyableRows(ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);
                            }
                        }

                        if (area[coords.x, coords.y - 1].CompareTag("Floor"))
                        {
                            additive.x = -1;
                            additive.y = 0;

                            if (CheckSidesCorridor(area[coords.x + additive.x, coords.y + additive.y], 3, new Vector2Int(coords.x + additive.x, coords.y + additive.y), "Floor", "Destroyables", "Static").x != 0)
                            {
                                RandomiseContent(ref randomNumber, ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);

                                CreateDestroyableRows(ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);
                            }

                            additive.x = 0;
                            additive.y = -1;

                            if (CheckSidesCorridor(area[coords.x + additive.x, coords.y + additive.y], 3, new Vector2Int(coords.x + additive.x, coords.y + additive.y), "Floor", "Destroyables", "Static").x != 0)
                            {
                                RandomiseContent(ref randomNumber, ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);

                                CreateDestroyableRows(ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);
                            }
                        }
                    }
                }
            }
        }
    }

    private void GenerateMiddleObjects()
    {
        if(middleObjects.Count > 0)
        {
            for (int a = 0; a < parentRooms.Length; a++)
            {
                for (int b = 0; b < parentRooms[a].MidPoints.Count; b++)
                {
                    int randomChange = Random.Range(0, 101);
                    
                    if(randomChange > 0 && randomChange < middleObjectsRate)
                    {
                        int randomMiddleObject = Random.Range(0, middleObjects.Count);
                        int randomRotation = Random.Range(-360, 361);

                        if(parentRooms[a] != playerSpawnRoom && parentRooms[a] != bossSpawnRoom)
                        {
                            GameObject tempMiddleObject = Instantiate(middleObjects[randomMiddleObject].gameObject, parentRooms[a].MidPoints[b].transform.position, Quaternion.Euler(0, randomRotation, 0), parentRooms[a].transform);
                            tempMiddleObject.name = middleObjects[randomMiddleObject].name;
                        }
                    }
                }
            }
        }
    }

    //TODO: Make functions to compact code
    private void RandomiseContent(ref int randomNumber, ref int staticOrBreakable, ref GameObject tempContent, ref Vector2Int coords, ref int i, ref int j, ref Vector2Int additive)
    {
        randomNumber = Random.Range(0, 101);

        if (staticOrBreakable == 0)
        {
            int randomStaticObject = Random.Range(0, staticObjects.Count);
            tempContent = staticObjects[randomStaticObject].gameObject;
            tempContent.name = staticObjects[randomStaticObject].name;
        }
        else
        {
            int randomBreakableObject = Random.Range(0, breakables.Count);
            tempContent = breakables[randomBreakableObject].gameObject;
            tempContent.name = breakables[randomBreakableObject].name;
        }

        //If not floor, delete it, most likely it is a destroyable object
        if(!area[coords.x + additive.x, coords.y + additive.y].CompareTag("Floor") && !area[coords.x + additive.x, coords.y + additive.y].CompareTag("Wall") && !area[coords.x + additive.x, coords.y + additive.y].CompareTag("OuterCorner"))
        {
            DestroyImmediate(area[coords.x + additive.x, coords.y + additive.y]);
        }

        if(!CheckIfNull(area[coords.x + additive.x, coords.y + additive.y], parentRooms[i].Corners[j]) && area[coords.x + additive.x, coords.y + additive.y].transform.parent != null)
        {
            if (randomNumber > 0 && randomNumber <= cornerObjectsRate)
            {
                if(area[coords.x + additive.x, coords.y + additive.y].transform.parent.CompareTag("Room") && area[coords.x + additive.x, coords.y + additive.y].transform.position != parentRooms[i].Corners[j].transform.position)
                {
                    area[coords.x + additive.x, coords.y + additive.y] = Instantiate(tempContent, area[coords.x + additive.x, coords.y + additive.y].transform.position, Quaternion.identity, parentRooms[i].Corners[j].transform.parent.transform);

                    area[coords.x + additive.x, coords.y + additive.y].transform.position = new Vector3(area[coords.x + additive.x, coords.y + additive.y].transform.position.x, tempContent.transform.position.y, area[coords.x + additive.x, coords.y + additive.y].transform.position.z);
                }
            }
        }
    }

    //TODO: Make functions to compact code
    private void CreateDestroyableRows(ref int staticOrBreakable, ref GameObject tempContent, ref Vector2Int coords, ref int i, ref int j, ref Vector2Int additive)
    {
        int randomChance = Random.Range(0, 101);

        if(randomChance > 0 && randomChance <= destroyablesRate)
        {
            if (staticOrBreakable == 1)
            {
                if (CheckNullAndTag("Floor", true, area[coords.x + additive.x, coords.y + additive.y]) || CheckNullAndTag("Breakable", true, area[coords.x + additive.x, coords.y + additive.y]) || CheckNullAndTag("Static", true, area[coords.x + additive.x, coords.y + additive.y]))
                {
                    if (CheckSidesCorridor(area[coords.x + additive.x, coords.y + additive.x], 3, new Vector2Int(coords.x + additive.x, coords.y + additive.y), "Floor", "Breakable", "Static").x != 0)
                    {
                        if (!area[coords.x + additive.x, coords.y + additive.y].CompareTag("Floor"))
                        {
                            DestroyImmediate(area[coords.x + additive.x, coords.y + additive.y]);
                        }

                        //Check if it is in the room
                        if (area[coords.x + additive.x, coords.y + additive.y].transform.parent.CompareTag("Room") && area[coords.x + additive.x, coords.y + additive.y].transform.position != parentRooms[i].Corners[j].transform.position)
                        {
                            area[coords.x + additive.x, coords.y + additive.y] = Instantiate(tempContent, area[coords.x + additive.x, coords.y + additive.y].transform.position, Quaternion.identity, parentRooms[i].Corners[j].transform.parent.transform);

                            area[coords.x + additive.x, coords.y + additive.y].transform.position = new Vector3(area[coords.x + additive.x, coords.y + additive.y].transform.position.x, tempContent.transform.position.y, area[coords.x + additive.x, coords.y + additive.y].transform.position.z);
                        }

                        coords.x = coords.x + additive.x;
                        coords.y = coords.y + additive.y;

                        CreateDestroyableRows(ref staticOrBreakable, ref tempContent, ref coords, ref i, ref j, ref additive);
                    }
                }
            }
        }
    }
}
