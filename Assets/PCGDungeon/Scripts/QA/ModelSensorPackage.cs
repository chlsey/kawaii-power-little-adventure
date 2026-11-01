using UnityEngine;

/// <summary>Provides model perception independently of player movement, combat, and QA reporting.</summary>
[AddComponentMenu("Reinforcement Learning/Model Sensor Package")]
[DisallowMultipleComponent]
public sealed class ModelSensorPackage : MonoBehaviour
{
    public const int RayCount = 64;
    public const int LocalGridSize = 97;
    public const int GridChannelCount = 4;
    public const int LocalGridValueCount = LocalGridSize * LocalGridSize * GridChannelCount;

    [SerializeField, Min(0.01f)] private float rayRange = 12f;
    // Stay below the dungeon's wall tops after the CharacterController settles.
    [SerializeField] private float rayHeight = 0.5f;

    public float RayRange => rayRange;

    private readonly QaRayObservation[] rays = new QaRayObservation[RayCount];
    private readonly ModelSensorTrace[] traces = new ModelSensorTrace[RayCount];
    private readonly float[] localGrid = new float[LocalGridValueCount];
    private Vector2Int centerCell;
    private bool windowDirty;

    public ModelSensorGrid Grid { get; private set; }
    public bool IsTracking { get; private set; }
    public bool ExitVisible { get; private set; }
    public QaRayObservation[] Rays => rays;
    public ModelSensorTrace[] Traces => traces;
    public int DiscoveredWallCellCount
    {
        get
        {
            if (Grid == null)
                return 0;
            return Grid.DiscoveredWallCellCount;
        }
    }

    public int ViewedOpenCellCount
    {
        get
        {
            if (Grid == null)
                return 0;
            return Grid.ViewedOpenCellCount;
        }
    }

    /// <summary>Starts fresh perception memory and captures the spawn view for a new attempt.</summary>
    public void BeginAttempt()
    {
        Grid = new ModelSensorGrid(ModelSensorGrid.WorldToCell(transform.position));
        IsTracking = true;
        Capture();
    }

    /// <summary>Captures the terminal view and retains it for the final model observation.</summary>
    public void EndAttempt()
    {
        if (!IsTracking)
            return;
        Capture();
        IsTracking = false;
    }

    /// <summary>Updates rays, traces, and persistent map at a model action boundary.</summary>
    public void Capture()
    {
        if (!IsTracking)
            return;

        Vector3 origin = transform.position + Vector3.up * rayHeight;
        centerCell = ModelSensorGrid.WorldToCell(transform.position);
        Grid.MarkVisited(transform.position);
        ExitVisible = false;

        for (int i = 0; i < RayCount; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, i * (360f / RayCount), 0f) * Vector3.forward;
            rays[i] = QaRayObservation.Miss;
            Vector3 endpoint = origin + direction * rayRange;
            float nearestDistance = float.PositiveInfinity;

            // Room triggers are ignored, but marked exit triggers remain visible.
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, rayRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform))
                    continue;

                ModelSensorTarget target = hit.collider.GetComponentInParent<ModelSensorTarget>();
                if (target == null || target.HitId == QaObjectType.None || hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                rays[i] = new QaRayObservation(target.HitId, Mathf.Clamp01(hit.distance / rayRange));
                endpoint = hit.point;
            }

            traces[i] = new ModelSensorTrace(origin, endpoint, rays[i].HitId);
            Grid.Trace(origin, endpoint, rays[i].HitId);
            if (rays[i].HitId == QaObjectType.LevelEnd)
                ExitVisible = true;
        }

        windowDirty = true;
    }

    /// <summary>Reads the fixed player-centered map window without scanning or consuming discoveries.</summary>
    public ModelSensorSnapshot ReadSnapshot()
    {
        if (Grid == null)
            throw new System.InvalidOperationException("Begin a sensor attempt before reading its snapshot.");

        if (windowDirty)
        {
            int half = LocalGridSize / 2;
            int index = 0;
            for (int z = -half; z <= half; z++)
            {
                for (int x = -half; x <= half; x++)
                {
                    ModelMapCell cell = Grid.ReadCell(centerCell + new Vector2Int(x, z));
                    localGrid[index++] = System.Convert.ToSingle(cell.Observed);
                    localGrid[index++] = System.Convert.ToSingle(cell.Wall);
                    localGrid[index++] = System.Convert.ToSingle(cell.Exit);
                    localGrid[index++] = System.Convert.ToSingle(cell.Visited);
                }
            }
            windowDirty = false;
        }

        return new ModelSensorSnapshot(rays, localGrid);
    }
}
