using System;

// Mirrors qa_framework's QAObservation contract: normalized health, a raw
// powerup count, the previous applied action, and a fixed-size ray array.
[Serializable]
public struct QaObservation
{
    public readonly float Health01;
    public readonly int PowerupCount;
    public readonly QaRayObservation[] Rays;
    public readonly QaAction PreviousAction;

    public int RayCount => Rays.Length;

    public QaObservation(float health01, int powerupCount, QaRayObservation[] rays, QaAction previousAction)
    {
        Health01 = health01;
        PowerupCount = powerupCount;
        Rays = rays;
        PreviousAction = previousAction;
    }
}
