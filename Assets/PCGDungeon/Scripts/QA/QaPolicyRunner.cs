using Kcccr.UnityReinforcementLearning;
using UnityEngine;

public enum QaPolicyMode
{
    Random,
    Trained
}

/// <summary>Chooses player commands using either the local trained ONNX policy or the seeded random baseline.</summary>
public class QaPolicyRunner : MonoBehaviour
{
    [SerializeField] private QaPolicyMode mode;
    [SerializeField] private RlModelRunner modelRunner;
    [Range(0f, 1f)] [SerializeField] private float attackChance = 0.1f;
    [Range(0f, 1f)] [SerializeField] private float interactChance = 0.1f;

    private int episodeSeed;
    private int stepIndex;

    // Begins a new episode's random command sequence, seeded for reproducibility.
    public void BeginEpisode(int seed)
    {
        episodeSeed = seed;
        stepIndex = 0;
    }

    public QaAction Decide(QaObservation observation, ModelSensorSnapshot sensors)
    {
        if (mode == QaPolicyMode.Trained)
        {
            float[] actions = modelRunner.Predict(QaPlayerPolicyMapping.Observe(observation, sensors));
            return QaPlayerPolicyMapping.Decode(actions);
        }

        Random.InitState(episodeSeed + stepIndex);
        stepIndex++;

        float moveX = Random.Range(-1f, 1f);
        float moveY = Random.Range(-1f, 1f);
        bool attack = Random.value < attackChance;
        bool interact = Random.value < interactChance;

        return new QaAction(moveX, moveY, attack, interact, 0);
    }
}
