using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HeartHealItem : MonoBehaviour
{
    [SerializeField] private bool destroyOnUse = true;
    [SerializeField] private bool requireMissingHealth = false;

    private bool used;

    void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used)
            return;

        if (!other.CompareTag("Player"))
            return;

        PlayerHealth2D playerHealth = other.GetComponent<PlayerHealth2D>();
        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth2D>();

        if (playerHealth == null)
        {
            Debug.LogWarning("HeartHealItem: no se encontró PlayerHealth2D en el jugador.");
            return;
        }

        if (requireMissingHealth && playerHealth.CurrentHealth >= playerHealth.MaxHealth)
            return;

        used = true;
        playerHealth.HealToFull();

        if (AudioManager2D.Instance != null)
            AudioManager2D.Instance.PlayHeartPickupSound();

        if (destroyOnUse)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }
}
