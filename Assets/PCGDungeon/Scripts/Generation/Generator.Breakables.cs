using System.Collections.Generic;
using UnityEngine;

public partial class Generator
{
    private void SpawnBreakableProps()
    {
        // This pass runs last and preserves the RNG stream used by the PCG policy.
        Random.State state = Random.state;
        try
        {
            Physics.SyncTransforms();
            Data settings = generationSettings.generation;
            foreach (Room room in parentRooms)
            {
                // Enable props already placed through the legacy breakables list.
                foreach (Transform child in room.transform)
                {
                    if (child.tag != "Breakable" && child.tag != "Destroyables" && child.GetComponent<BreakableProp>() == null) continue;
                    BreakableProp prop = child.GetComponent<BreakableProp>() ?? child.gameObject.AddComponent<BreakableProp>();
                    prop.Configure(RollLoot(), lootPickup, settings.breakableLootChance);
                }
                if (room.Type != RoomType.Normal || settings.breakablePropsPerRoom <= 0) continue;
                Bounds inner = RoomBounds(room, -1f);
                var candidates = new List<Transform>();
                foreach (Transform child in room.transform)
                {
                    if (!child.CompareTag("Floor") || !inner.Contains(child.position)) continue;
                    // Keep the centre open, with a full tile between props and walls.
                    Vector3 p = child.position;
                    float edge = Mathf.Min(p.x - inner.min.x, inner.max.x - p.x, p.z - inner.min.z, inner.max.z - p.z);
                    if (edge < 1.1f) candidates.Add(child);
                }
                int spawned = 0;
                while (candidates.Count > 0 && spawned < settings.breakablePropsPerRoom)
                {
                    int index = Random.Range(0, candidates.Count);
                    Transform tile = candidates[index];
                    candidates.RemoveAt(index);
                    Vector3 floor = tile.position;
                    Renderer floorRenderer = tile.GetComponent<Renderer>();
                    if (floorRenderer != null) floor.y = floorRenderer.bounds.max.y;
                    bool blocked = false;
                    foreach (Collider hit in Physics.OverlapBox(floor + Vector3.up * 0.55f, new Vector3(0.65f, 0.45f, 0.65f)))
                    {
                        // Room-sized trigger volumes detect entry; they are not obstacles.
                        if (hit.isTrigger && hit.GetComponent<Room>() != null) continue;
                        if (hit.transform != tile && !hit.CompareTag("Floor")) { blocked = true; break; }
                    }
                    foreach (GameObject door in doorGameObjects)
                        if (door != null && Vector3.Distance(door.transform.position, floor) < 2.5f) blocked = true;
                    if (blocked) continue;
                    GameObject model = BuildBreakable(room.transform, floor, spawned % 2 == 1);
                    model.GetComponent<BreakableProp>().Configure(RollLoot(), lootPickup, settings.breakableLootChance);
                    spawned++;
                    Physics.SyncTransforms();
                }
            }
        }
        finally { Random.state = state; }
    }

    private static GameObject BuildBreakable(Transform parent, Vector3 floor, bool barrel)
    {
        var root = new GameObject(barrel ? "Smashable Barrel" : "Smashable Crate");
        root.transform.SetParent(parent, false);
        root.transform.position = floor;
        BreakableProp prop = root.AddComponent<BreakableProp>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var wood = new Material(shader) { color = new Color(0.53f, 0.29f, 0.12f) };
        var trim = new Material(shader) { color = barrel ? new Color(0.23f, 0.25f, 0.29f) : new Color(0.76f, 0.51f, 0.25f) };
        prop.OwnMaterial(wood);
        prop.OwnMaterial(trim);
        AddPropPart(root.transform, barrel ? PrimitiveType.Cylinder : PrimitiveType.Cube,
            new Vector3(0f, 0.45f, 0f), new Vector3(0.75f, barrel ? 0.45f : 0.9f, 0.75f), wood);
        for (int i = 0; i < 2; i++)
        {
            float y = i == 0 ? 0.18f : 0.72f;
            AddPropPart(root.transform, barrel ? PrimitiveType.Cylinder : PrimitiveType.Cube,
                new Vector3(0f, y, 0f), new Vector3(0.79f, barrel ? 0.035f : 0.09f, 0.79f), trim);
        }
        if (!barrel)
        {
            GameObject brace = AddPropPart(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.45f, -0.39f),
                new Vector3(0.1f, 0.95f, 0.05f), trim);
            brace.transform.localRotation = Quaternion.Euler(0f, 0f, -40f);
        }
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = Vector3.up * 0.45f;
        collider.size = new Vector3(0.8f, 0.9f, 0.8f);
        return root;
    }

    private static GameObject AddPropPart(Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }
}
