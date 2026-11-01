using System;

// Gameplay state combined with model sensor readings by the policy mapping.
[Serializable]
public struct QaObservation
{
    public readonly float Health01;
    public readonly int PowerupCount;
    public readonly QaAction PreviousAction;

    public QaObservation(float health01, int powerupCount, QaAction previousAction)
    {
        Health01 = health01;
        PowerupCount = powerupCount;
        PreviousAction = previousAction;
    }
}
