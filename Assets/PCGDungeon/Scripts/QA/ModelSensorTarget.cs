using UnityEngine;

/// <summary>Identifies an object and its child colliders to the model's ray sensors.</summary>
[AddComponentMenu("Reinforcement Learning/Model Sensor Target")]
[DisallowMultipleComponent]
public sealed class ModelSensorTarget : MonoBehaviour
{
    [SerializeField] private QaObjectType hitId = QaObjectType.Wall;

    public QaObjectType HitId
    {
        get => hitId;
        set => hitId = value;
    }
}
