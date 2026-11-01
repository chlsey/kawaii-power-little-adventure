using UnityEngine;

/// <summary>Borrowed model inputs, valid until the sensor next captures or builds its observation window.</summary>
public readonly struct ModelSensorSnapshot
{
    public readonly QaRayObservation[] Rays;
    public readonly float[] LocalGrid;

    public ModelSensorSnapshot(QaRayObservation[] rays, float[] localGrid)
    {
        Rays = rays;
        LocalGrid = localGrid;
    }
}

/// <summary>A sensed segment used to update and inspect the persistent map.</summary>
public readonly struct ModelSensorTrace
{
    public readonly Vector3 Origin;
    public readonly Vector3 Endpoint;
    public readonly QaObjectType HitId;

    public ModelSensorTrace(Vector3 origin, Vector3 endpoint, QaObjectType hitId)
    {
        Origin = origin;
        Endpoint = endpoint;
        HitId = hitId;
    }
}
