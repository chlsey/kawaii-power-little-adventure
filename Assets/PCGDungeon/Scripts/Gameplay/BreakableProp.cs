using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Works on custom prefabs as well as the generator's built-in crates/barrels.
public sealed class BreakableProp : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float health = 2f;
    [SerializeField, Range(0f, 1f)] private float lootChance = 0.4f;
    private ItemDefinition loot;
    private GameObject pickupPrefab;
    private bool broken;
    private Coroutine hitRoutine;
    private readonly List<Material> ownedMaterials = new List<Material>();

    public void Configure(ItemDefinition item, GameObject fallbackPickup, float chance)
    {
        loot = item;
        pickupPrefab = item != null && item.PickupPrefab != null ? item.PickupPrefab : fallbackPickup;
        lootChance = Mathf.Clamp01(chance);
    }

    public void OwnMaterial(Material material) => ownedMaterials.Add(material);

    public void TakeDamage(float damage)
    {
        if (broken || damage <= 0f) return;
        health -= damage;
        if (hitRoutine != null) StopCoroutine(hitRoutine);
        if (health > 0f)
        {
            hitRoutine = StartCoroutine(Flash());
            return;
        }
        broken = true; // Multiple colliders/attacks cannot break or reward twice.
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        if (Random.value < lootChance) DropLoot();
        StartCoroutine(Burst());
    }

    private IEnumerator Flash()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", Color.white);
        block.SetColor("_Color", Color.white);
        foreach (Renderer renderer in renderers) renderer.SetPropertyBlock(block);
        yield return new WaitForSeconds(0.1f);
        foreach (Renderer renderer in renderers) if (renderer != null) renderer.SetPropertyBlock(null);
        hitRoutine = null;
    }

    private void DropLoot()
    {
        Vector3 position = transform.position + Vector3.up * 0.5f;
        GameObject drop;
        if (pickupPrefab != null)
            drop = Instantiate(pickupPrefab, transform.position + Vector3.up * pickupPrefab.transform.position.y,
                Quaternion.identity, transform.parent);
        else
        {
            drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drop.transform.SetParent(transform.parent, true);
            drop.transform.position = position;
            drop.transform.localScale = Vector3.one * 0.5f;
            // Property blocks color the pickup without allocating a material.
            var block = new MaterialPropertyBlock();
            Color color = loot != null ? loot.PickupColor : Color.green;
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            drop.GetComponent<Renderer>().SetPropertyBlock(block);
        }
        drop.name = loot != null ? loot.DisplayName : "Barrel loot";
        QaLootPickup pickup = drop.GetComponent<QaLootPickup>() ?? drop.AddComponent<QaLootPickup>();
        pickup.Item = loot;
        if (drop.GetComponentInChildren<Collider>() == null) drop.AddComponent<SphereCollider>();
        foreach (Collider collider in drop.GetComponentsInChildren<Collider>()) collider.isTrigger = true;
    }

    private IEnumerator Burst()
    {
        const int count = 14;
        var pieces = new Transform[count];
        var velocities = new Vector3[count];
        var sizes = new Vector3[count];
        Renderer source = GetComponentInChildren<Renderer>();
        for (int i = 0; i < count; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(i < 8 ? PrimitiveType.Cube : PrimitiveType.Sphere);
            piece.name = i < 8 ? "Wood splinter" : "Dust puff";
            Collider collider = piece.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            pieces[i] = piece.transform;
            piece.transform.SetParent(transform, true);
            piece.transform.position = transform.position + Vector3.up * 0.45f;
            sizes[i] = i < 8 ? new Vector3(0.09f, 0.25f, 0.08f) : Vector3.one * 0.2f;
            piece.transform.localScale = sizes[i];
            float angle = i * Mathf.PI * 2f / count;
            velocities[i] = new Vector3(Mathf.Cos(angle) * 1.7f, 2f + (i % 3) * 0.4f, Mathf.Sin(angle) * 1.7f);
            Renderer renderer = piece.GetComponent<Renderer>();
            if (source != null) renderer.sharedMaterial = source.sharedMaterial;
            if (i >= 8)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color(0.8f, 0.72f, 0.58f));
                block.SetColor("_Color", new Color(0.8f, 0.72f, 0.58f));
                renderer.SetPropertyBlock(block);
            }
        }
        float elapsed = 0f;
        while (elapsed < 0.8f)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            for (int i = 0; i < count; i++)
            {
                velocities[i] += Vector3.down * (6f * dt);
                pieces[i].position += velocities[i] * dt;
                Vector3 position = pieces[i].position;
                position.y = Mathf.Max(transform.position.y + 0.05f, position.y);
                pieces[i].position = position;
                pieces[i].Rotate(new Vector3(130f, 90f, 170f) * dt);
                pieces[i].localScale = sizes[i] * Mathf.Clamp01((0.8f - elapsed) / 0.3f);
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
    }
}
