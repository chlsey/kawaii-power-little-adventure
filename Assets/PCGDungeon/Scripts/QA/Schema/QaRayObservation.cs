using System;

// One model sensor ray: a target identifier and distance divided by ray range.
[Serializable]
public struct QaRayObservation
{
    public readonly QaObjectType HitId;
    public readonly float Distance01;

    public QaRayObservation(QaObjectType hitId, float distance01)
    {
        HitId = hitId;
        Distance01 = distance01;
    }

    public static QaRayObservation Miss => new QaRayObservation(QaObjectType.None, 1f);
}
