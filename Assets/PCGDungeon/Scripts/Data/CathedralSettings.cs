using UnityEngine;

// Holds the cathedral's default parameters and the models it decorates with.
// Assign it to the Cathedral field of the GenerationSettings asset; without one
// the CathedralParameters defaults apply and no decorations are placed.
[CreateAssetMenu(menuName = "Cathedral Settings")]
public class CathedralSettings : ScriptableObject
{
    [SerializeField] public CathedralParameters parameters = new CathedralParameters();

    // Stands in the end rooms. Placed at the prefab's own height above the floor.
    [SerializeField] public GameObject columnPrefab;
    // Lines the hall. Any model works: it is scaled and turned to span the tiles it covers.
    [SerializeField] public GameObject archPrefab;
    // Arches are scaled down if spanning their tiles would make them taller than this. Zero disables the limit.
    [SerializeField] public float archMaxHeight = 2.5f;
}
