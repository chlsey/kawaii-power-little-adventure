using System;

// One sensor ray result, matching qa_framework's RayObservation contract:
// hit=false must report None with the conventional full-range distance of 1.
[Serializable]
public struct QaRayObservation
{
    public readonly bool Hit;
    public readonly float Distance01;
    public readonly QaObjectType ObjectType;

    public QaRayObservation(bool hit, float distance01, QaObjectType objectType)
    {
        Hit = hit;
        Distance01 = distance01;
        ObjectType = objectType;
    }

    public static QaRayObservation Miss => new QaRayObservation(false, 1f, QaObjectType.None);
}
