using UnityEngine;

/// <summary>Current vision and movement only; no discovered-map or remembered-goal inputs.</summary>
public static class QaLocalMovementPolicyMapping
{
    public const int ExitSignalOffset = 2 + ModelSensorPackage.RayCount * 2;
    public const int ObservationSize = ExitSignalOffset + 4;
    public const int ActionSize = 2;

    public static float[] Observe(QaObservation observation, QaRayObservation[] rays)
    {
        if (rays.Length != ModelSensorPackage.RayCount)
            throw new System.ArgumentException("Local movement requires 64 vision rays.", nameof(rays));

        var values = new float[ObservationSize];
        values[0] = observation.PreviousAction.MoveX;
        values[1] = observation.PreviousAction.MoveY;
        int nearestExitRay = -1;
        float nearestExitDistance = float.PositiveInfinity;
        for (int i = 0; i < rays.Length; i++)
        {
            float distance = Mathf.Clamp01(rays[i].Distance01);
            int index = 2 + i * 2;
            // Separate channels identify walls and exits without treating labels as magnitudes.
            if (rays[i].HitId == QaObjectType.Wall)
                values[index] = 1f - distance;
            if (rays[i].HitId == QaObjectType.LevelEnd)
            {
                values[index + 1] = 1f - distance;
                if (distance < nearestExitDistance)
                {
                    nearestExitDistance = distance;
                    nearestExitRay = i;
                }
            }
        }

        if (nearestExitRay >= 0)
        {
            // Rays and movement share world X/Z axes, regardless of the actor's facing.
            float angle = nearestExitRay * Mathf.PI * 2f / ModelSensorPackage.RayCount;
            values[ExitSignalOffset] = 1f;
            values[ExitSignalOffset + 1] = Mathf.Sin(angle);
            values[ExitSignalOffset + 2] = Mathf.Cos(angle);
            values[ExitSignalOffset + 3] = nearestExitDistance;
        }
        return values;
    }

    public static QaAction Decode(float[] action)
    {
        return new QaAction(action[0], action[1], false, false, 0);
    }
}
