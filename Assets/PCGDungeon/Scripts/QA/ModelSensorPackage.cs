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

    /// <summary>Local visibility and swept actor clearance, bounded by the current sensor range.</summary>
    public bool CanUseWaypoint(Vector3 point, CharacterController actor)
    {
        var body = new QaNavigationCapsule(actor);
        return CanUseWaypoint(point, body, body.Clearance, body.Clearance);
    }

    // An escape may begin inside the planning margin, but must clear the real actor
    // for its entire sweep and finish with the normal waypoint safety margin.
    public bool CanUseExplorationEscape(Vector3 point, CharacterController actor)
    {
        var body = new QaNavigationCapsule(actor);
        return CanUseWaypoint(point, body, body.Radius, body.Clearance);
    }

    private bool CanUseWaypoint(Vector3 point, QaNavigationCapsule body, float radius, float destinationRadius)
    {
        Vector3 delta = point - transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance > rayRange || distance < 0.0001f) return false;
        Vector3 direction = delta / distance;
        Vector3 origin = transform.position + Vector3.up * rayHeight;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            if (IsWall(hit.collider)) return false;
        }
        return CapsuleSegmentClear(body, transform.position, point, radius, destinationRadius);
    }

    // Refinement only evaluates cells already observed as non-wall. It cannot
    // introduce a route into unknown space or supply a hidden destination.
    public bool CanOccupyObservedPoint(Vector3 point, QaNavigationCapsule body)
    {
        ModelMapCell cell = Grid.ReadCell(ModelSensorGrid.WorldToCell(point));
        if (!cell.Observed || cell.Wall) return false;
        Vector3 delta = point - body.Position;
        foreach (Collider hit in Physics.OverlapCapsule(body.Bottom + delta, body.Top + delta, body.Clearance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            if (IsWall(hit)) return false;
        return true;
    }

    public bool CanTraverseObservedSegment(Vector3 from, Vector3 to, QaNavigationCapsule body)
    {
        return CapsuleSegmentClear(body, from, to, body.Clearance, body.Clearance);
    }

    private bool CapsuleSegmentClear(QaNavigationCapsule body, Vector3 from, Vector3 to,
        float radius, float destinationRadius)
    {
        Vector3 shift = from - body.Position;
        Vector3 bottom = body.Bottom + shift, top = body.Top + shift;
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        Vector3 direction = delta.normalized;
        foreach (Collider hit in Physics.OverlapCapsule(bottom, top, radius,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            if (IsWall(hit)) return false;
        foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius, direction, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            if (IsWall(hit.collider)) return false;
        foreach (Collider hit in Physics.OverlapCapsule(bottom + delta, top + delta, destinationRadius,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            if (IsWall(hit)) return false;
        return true;
    }

    private bool IsWall(Collider collider)
    {
        if (collider.transform.IsChildOf(transform)) return false;
        ModelSensorTarget target = collider.GetComponentInParent<ModelSensorTarget>();
        return target != null && target.HitId == QaObjectType.Wall;
    }

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

/// <summary>World-space upright controller shape shared by planning and physical checks.</summary>
public readonly struct QaNavigationCapsule
{
    public readonly Vector3 Position, Bottom, Top;
    public readonly float Radius, Clearance;

    public QaNavigationCapsule(CharacterController actor)
    {
        // Controller bounds reflect the active, scaled collider. This game uses
        // upright controllers; take the larger horizontal extent conservatively.
        Bounds bounds = actor.bounds;
        Radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float half = Mathf.Max(0f, bounds.extents.y - Radius);
        Position = actor.transform.position;
        Bottom = bounds.center - Vector3.up * half;
        Top = bounds.center + Vector3.up * half;
        Clearance = Radius + 0.05f;
    }
}
