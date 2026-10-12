using UnityEngine;

// Stand-in for a trained QA policy, mirroring PcgPolicyRunner's role on the
// generation side: it produces commands so the loop can run end-to-end
// before a real model exists. Replace Decide() with a trained policy later;
// QaPlayerController.ApplyAction does not change.
public class QaPolicyRunner : MonoBehaviour
{
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

    // Produces the next QA command. A trained policy would consume the
    // previous QaObservation here instead of acting blindly.
    public QaAction Decide()
    {
        Random.InitState(episodeSeed + stepIndex);
        stepIndex++;

        float moveX = Random.Range(-1f, 1f);
        float moveY = Random.Range(-1f, 1f);
        bool attack = Random.value < attackChance;
        bool interact = Random.value < interactChance;

        return new QaAction(moveX, moveY, attack, interact, 0);
    }
}
