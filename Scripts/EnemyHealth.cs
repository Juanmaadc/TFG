using System;
using UnityEngine;

public class EnemyHealth2D : MonoBehaviour
{
    public static event Action<EnemyHealth2D> OnAnyEnemyDied;
    public static event Action<EnemyHealth2D, int, int> OnAnyEnemyHealthChanged;

    [SerializeField] private int maxHealth = 2;

    [Header("Level clear")]
    [Tooltip("Si está activo, este enemigo cuenta para bloquear el paso de nivel hasta que muera.")]
    [SerializeField] private bool countsForLevelClear = true;

    [Header("Audio feedback")]
    [Tooltip("Reproduce el sonido de enemigo recibiendo daño en AudioManager2D.")]
    [SerializeField] private bool playEnemyDamagedSound = true;

    [Tooltip("Reproduce el sonido de jugador haciendo daño en AudioManager2D. En tu juego casi todo el daño a enemigos viene del jugador, incluyendo ataques, flechas y hechizos.")]
    [SerializeField] private bool playPlayerDealDamageSound = true;

    [Header("Damage visual feedback")]
    [Tooltip("Si está activo, el enemigo se pondrá rojo brevemente cuando reciba daño.")]
    [SerializeField] private bool flashRedWhenDamaged = true;

    [Tooltip("Color temporal que se aplicará al SpriteRenderer del enemigo al recibir daño.")]
    [SerializeField] private Color damageFlashColor = Color.red;

    [Tooltip("Duración del flash rojo. Debe ser corto para que se note sin molestar.")]
    [SerializeField, Min(0.01f)] private float damageFlashDuration = 0.12f;

    [Tooltip("Si el enemigo muere con el golpe, retrasa un instante su destrucción para que se pueda ver el flash rojo final.")]
    [SerializeField, Min(0f)] private float deathDestroyDelayForFlash = 0.12f;

    [Tooltip("Si está activo, EnemyHealth2D añadirá automáticamente EnemyDamageFlash2D si el enemigo no lo tiene.")]
    [SerializeField] private bool autoAddDamageFlashComponent = true;

    [Header("Debug")]
    [SerializeField] private bool debugHealthLogs = false;

    private int currentHealth;
    private EnemyChaser2D enemyChaser;
    private FireballEnemy2D fireballEnemy;
    private EnemyDeathSpawner2D deathSpawner;
    private EnemyDamageFlash2D damageFlash;
    private bool isDead;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool CountsForLevelClear => countsForLevelClear;
    public bool IsDead => isDead;

    void Awake()
    {
        currentHealth = maxHealth;
        ResolveEnemyReferences();
        ResolveDamageFlash();
    }

    private void ResolveEnemyReferences()
    {
        enemyChaser = GetComponent<EnemyChaser2D>();
        if (enemyChaser == null) enemyChaser = GetComponentInParent<EnemyChaser2D>();
        if (enemyChaser == null) enemyChaser = GetComponentInChildren<EnemyChaser2D>(true);

        fireballEnemy = GetComponent<FireballEnemy2D>();
        if (fireballEnemy == null) fireballEnemy = GetComponentInParent<FireballEnemy2D>();
        if (fireballEnemy == null) fireballEnemy = GetComponentInChildren<FireballEnemy2D>(true);

        deathSpawner = GetComponent<EnemyDeathSpawner2D>();
        if (deathSpawner == null) deathSpawner = GetComponentInParent<EnemyDeathSpawner2D>();
        if (deathSpawner == null) deathSpawner = GetComponentInChildren<EnemyDeathSpawner2D>(true);
    }

    public void TakeDamage(int damage)
    {
        ApplyDamage(damage);
    }

    public void TakeDamage(int damage, Vector2 knockbackDirection, float knockbackDistance, float knockbackDuration)
    {
        ApplyDamage(damage);

        if (isDead || knockbackDirection.sqrMagnitude <= 0.001f || knockbackDistance <= 0f)
            return;

        Vector2 normalizedDirection = knockbackDirection.normalized;

        if (enemyChaser != null)
            enemyChaser.ApplyKnockback(normalizedDirection, knockbackDistance, knockbackDuration);

        if (fireballEnemy != null)
            fireballEnemy.ApplyKnockback(normalizedDirection, knockbackDistance, knockbackDuration);
    }

    private void ApplyDamage(int damage)
    {
        if (isDead || damage <= 0)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        if (debugHealthLogs)
            Debug.Log($"EnemyHealth2D: {name} recibe {damage} de daño. Vida {currentHealth}/{maxHealth}.", this);

        PlayDamageAudioFeedback();
        TriggerDamageFlash();

        OnAnyEnemyHealthChanged?.Invoke(this, currentHealth, maxHealth);

        if (currentHealth <= 0)
            Die();
    }

    private void PlayDamageAudioFeedback()
    {
        if (AudioManager2D.Instance == null)
            return;

        if (playEnemyDamagedSound)
            AudioManager2D.Instance.PlayEnemyDamagedSound();

        if (playPlayerDealDamageSound)
            AudioManager2D.Instance.PlayPlayerDealDamageSound();
    }

    private void TriggerDamageFlash()
    {
        if (!flashRedWhenDamaged)
            return;

        ResolveDamageFlash();

        if (damageFlash != null)
            damageFlash.Flash(damageFlashColor, damageFlashDuration);
    }

    private void ResolveDamageFlash()
    {
        if (damageFlash != null)
            return;

        GameObject feedbackRoot = GetFeedbackRootObject();

        if (feedbackRoot != null)
            damageFlash = feedbackRoot.GetComponent<EnemyDamageFlash2D>();

        if (damageFlash == null)
            damageFlash = GetComponent<EnemyDamageFlash2D>();

        if (damageFlash == null)
            damageFlash = GetComponentInParent<EnemyDamageFlash2D>();

        if (damageFlash == null)
            damageFlash = GetComponentInChildren<EnemyDamageFlash2D>(true);

        if (damageFlash == null && autoAddDamageFlashComponent && feedbackRoot != null)
            damageFlash = feedbackRoot.AddComponent<EnemyDamageFlash2D>();

        if (damageFlash != null && feedbackRoot != null)
            damageFlash.SetRenderersSearchRoot(feedbackRoot.transform);
    }

    private GameObject GetFeedbackRootObject()
    {
        // Enemy2 suele tener EnemyDeathSpawner2D en la raíz y el SpriteRenderer en un hijo.
        // Por eso se prioriza esa raíz antes que el objeto que tiene EnemyChaser2D.
        if (deathSpawner != null && HasSpriteRendererInChildren(deathSpawner.gameObject))
            return deathSpawner.gameObject;

        if (HasSpriteRendererInChildren(gameObject))
            return gameObject;

        if (enemyChaser != null && HasSpriteRendererInChildren(enemyChaser.gameObject))
            return enemyChaser.gameObject;

        if (fireballEnemy != null && HasSpriteRendererInChildren(fireballEnemy.gameObject))
            return fireballEnemy.gameObject;

        if (deathSpawner != null)
            return deathSpawner.gameObject;

        if (enemyChaser != null)
            return enemyChaser.gameObject;

        if (fireballEnemy != null)
            return fireballEnemy.gameObject;

        return gameObject;
    }

    private bool HasSpriteRendererInChildren(GameObject candidate)
    {
        return candidate != null && candidate.GetComponentInChildren<SpriteRenderer>(true) != null;
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (debugHealthLogs)
            Debug.Log($"EnemyHealth2D: {name} muerto.", this);

        OnAnyEnemyDied?.Invoke(this);

        GameObject objectToDestroy = gameObject;

        if (deathSpawner != null)
            objectToDestroy = deathSpawner.gameObject;
        else if (enemyChaser != null)
            objectToDestroy = enemyChaser.gameObject;
        else if (fireballEnemy != null)
            objectToDestroy = fireballEnemy.gameObject;

        float destroyDelay = flashRedWhenDamaged ? Mathf.Max(deathDestroyDelayForFlash, damageFlashDuration) : 0f;

        if (destroyDelay > 0f)
            Destroy(objectToDestroy, destroyDelay);
        else
            Destroy(objectToDestroy);
    }
}
