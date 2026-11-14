using UnityEngine;

public static class QaWaypointPolicyMapping
{
    public const string Version = "local-waypoint-v1";
    public const int ObservationSize = 134;
    public const int ActionSize = 2;

    public static float[] Observe(QaObservation observation, ModelSensorPackage sensors,
        QaNavigationCoordinator navigation, Vector3 position)
    {
        return Observe(observation, sensors, navigation.HasWaypoint, navigation.Waypoint, position);
    }

    public static float[] Observe(QaObservation observation, ModelSensorPackage sensors,
        bool hasWaypoint, Vector3 waypoint, Vector3 position)
    {
        // Preserve the historical ray channels; replace only the old exit summary.
        float[] values = QaLocalMovementPolicyMapping.Observe(observation, sensors.Rays);
        for (int i = 130; i < 134; i++) values[i] = 0f;
        if (hasWaypoint)
        {
            Vector3 delta = waypoint - position;
            delta.y = 0f;
            values[130] = 1f;
            values[131] = delta.normalized.x;
            values[132] = delta.normalized.z;
            values[133] = Mathf.Clamp01(delta.magnitude / sensors.RayRange);
        }
        return values;
    }
}
