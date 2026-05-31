using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth2D : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private float invulnerabilityTime = 0.75f;

    [Header("Audio feedback")]
    [Tooltip("Reproduce un sonido desde AudioManager2D cuando el jugador recibe daño.")]
    [SerializeField] private bool playPlayerDamagedSound = true;

    [Header("Optional feedback")]
    [SerializeField] private Animator anim;

    [Header("Knockback")]
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Debug")]
    [SerializeField] private bool debugHealthLogs = false;

    private int currentHealth;
    private bool isInvulnerable;
    private bool initialized;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0;
    public bool IsInvulnerable => isInvulnerable;

    public static PlayerHealth2D LastChangedHealth { get; private set; }
    public static event Action<PlayerHealth2D, int, int> OnAnyHealthChanged;
    public static event Action<PlayerHealth2D, int, int, int> OnAnyDamaged;
    public static event Action<PlayerHealth2D> OnAnyDied;

    public event Action<int, int> OnHealthChanged;
    public event Action<int, int, int> OnDamaged;
    public event Action OnDied;

    void Awake()
    {
        InitializeIfNeeded();
    }

    void OnEnable()
    {
        InitializeIfNeeded();
        NotifyHealthChanged();
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        currentHealth = maxHealth;
        initialized = true;

        if (anim == null)
            anim = GetComponent<Animator>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, Vector2.zero, 0f, 0f);
    }

    public void TakeDamage(int damage, Vector2 knockbackDirection, float knockbackDistance, float knockbackDuration)
    {
        InitializeIfNeeded();

        if (damage <= 0 || IsDead || isInvulnerable)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        NotifyHealthChanged();
        NotifyDamaged(damage);
        PlayPlayerDamageAudioFeedback();

        if (debugHealthLogs)
            Debug.Log($"PlayerHealth2D: {name} recibe {damage} de daño. Vida {currentHealth}/{maxHealth}", this);

        if (anim != null)
            anim.SetTrigger("Hurt");

        if (playerMovement != null && knockbackDirection.sqrMagnitude > 0.001f)
            playerMovement.ApplyKnockback(knockbackDirection, knockbackDistance, knockbackDuration);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        StartCoroutine(DamageInvulnerability());
    }

    private void PlayPlayerDamageAudioFeedback()
    {
        if (playPlayerDamagedSound && AudioManager2D.Instance != null)
            AudioManager2D.Instance.PlayPlayerDamagedSound();
    }

    public void HealToFull()
    {
        InitializeIfNeeded();

        if (IsDead)
            return;

        if (currentHealth >= maxHealth)
            return;

        currentHealth = maxHealth;
        NotifyHealthChanged();
    }

    public void Heal(int amount)
    {
        InitializeIfNeeded();

        if (amount <= 0 || IsDead)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        NotifyHealthChanged();
    }

    public void ForceNotifyHealthChanged()
    {
        InitializeIfNeeded();
        NotifyHealthChanged();
    }

    private IEnumerator DamageInvulnerability()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityTime);
        isInvulnerable = false;
    }

    private void Die()
    {
        Debug.Log("PlayerHealth2D: el jugador ha muerto.");

        if (anim != null)
            anim.SetTrigger("Die");

        OnDied?.Invoke();
        OnAnyDied?.Invoke(this);
        gameObject.SetActive(false);
    }

    private void NotifyDamaged(int damage)
    {
        LastChangedHealth = this;
        OnDamaged?.Invoke(damage, currentHealth, maxHealth);
        OnAnyDamaged?.Invoke(this, damage, currentHealth, maxHealth);
    }

    private void NotifyHealthChanged()
    {
        LastChangedHealth = this;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnAnyHealthChanged?.Invoke(this, currentHealth, maxHealth);
    }
}
