using UnityEngine;

// Minimal marker/health component for a generated enemy. Detection by
// QaPlayerController uses this component rather than a tag, so classification
// stays correct even if a designer forgets to tag a prefab.
public sealed class QaEnemy : MonoBehaviour
{
    [SerializeField] private float health = 3f;
    [SerializeField] private float contactDamagePerSecond = 2f;

    private void Awake()
    {
        // Contact damage is sensed as a trigger; this prefab has no physical blocking in this POC.
        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider != null)
            ownCollider.isTrigger = true;
    }

    // Returns true once this hit reduces health to zero or below.
    public bool TakeDamage(float amount)
    {
        health -= amount;
        if (health > 0f)
            return false;

        Destroy(gameObject);
        return true;
    }

    private void OnTriggerStay(Collider other)
    {
        QaPlayerController player = other.GetComponentInParent<QaPlayerController>();
        if (player != null)
            player.ReportDamage(contactDamagePerSecond * Time.deltaTime);
    }
}
