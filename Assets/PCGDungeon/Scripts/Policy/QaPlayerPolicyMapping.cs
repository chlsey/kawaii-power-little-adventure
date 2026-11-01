/// <summary>Shares the trained player's observation encoding and action decoding between training and local inference.</summary>
public static class QaPlayerPolicyMapping
{
    public const int RawObservationSize = 7 + ModelSensorPackage.RayCount * 2;
    public const int ObservationSize = RawObservationSize + ModelSensorPackage.LocalGridValueCount;
    public const int ActionSize = 5;

    public static float[] Observe(QaObservation observation, ModelSensorSnapshot sensors)
    {
        QaRayObservation[] rays = sensors.Rays;
        if (rays.Length != ModelSensorPackage.RayCount)
            throw new System.ArgumentException("The player model requires " + ModelSensorPackage.RayCount + " ray observations.", nameof(rays));
        if (sensors.LocalGrid.Length != ModelSensorPackage.LocalGridValueCount)
            throw new System.ArgumentException("The player model requires a 97 by 97 map window with four channels.", nameof(sensors));

        QaAction previous = observation.PreviousAction;
        var values = new float[ObservationSize];
        values[0] = observation.Health01;
        values[1] = observation.PowerupCount;
        values[2] = previous.MoveX;
        values[3] = previous.MoveY;
        if (previous.Attack)
            values[4] = 1f;
        if (previous.Interact)
            values[5] = 1f;
        values[6] = previous.Ability;

        // Each ray contributes its hit identifier followed by normalized distance.
        int index = 7;
        foreach (QaRayObservation ray in rays)
        {
            values[index] = (int)ray.HitId;
            values[index + 1] = ray.Distance01;
            index += 2;
        }
        System.Array.Copy(sensors.LocalGrid, 0, values, RawObservationSize, ModelSensorPackage.LocalGridValueCount);
        return values;
    }

    public static QaAction Decode(float[] action)
    {
        return new QaAction(action[0], action[1], action[2] > 0f, action[3] > 0f, 0);
    }
}
