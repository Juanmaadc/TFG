using UnityEngine;

public class EnemyHealth2D : MonoBehaviour
{
    [SerializeField] private int maxHealth = 2;

    private int currentHealth;
    private EnemyChaser2D enemyChaser;

    void Awake()
    {
        currentHealth = maxHealth;
        enemyChaser = GetComponent<EnemyChaser2D>();

        if (enemyChaser == null)
            enemyChaser = GetComponentInParent<EnemyChaser2D>();
    }

    public void TakeDamage(int damage)
    {
        ApplyDamage(damage);
    }

    public void TakeDamage(int damage, Vector2 knockbackDirection, float knockbackDistance, float knockbackDuration)
    {
        ApplyDamage(damage);

        if (enemyChaser != null && knockbackDirection.sqrMagnitude > 0.001f && knockbackDistance > 0f)
        {
            enemyChaser.ApplyKnockback(knockbackDirection.normalized, knockbackDistance, knockbackDuration);
        }
    }

    private void ApplyDamage(int damage)
    {
        if (damage <= 0)
            return;

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
