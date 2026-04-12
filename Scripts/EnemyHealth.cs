using UnityEngine;

public class EnemyHealth2D : MonoBehaviour
{
    [SerializeField] private int maxHealth = 2;

    private int currentHealth;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}