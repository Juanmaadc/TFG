using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth2D : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private float invulnerabilityTime = 0.75f;

    [Header("Optional feedback")]
    [SerializeField] private Animator anim;

    private int currentHealth;
    private bool isInvulnerable;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0;
    public bool IsInvulnerable => isInvulnerable;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDied;

    void Awake()
    {
        currentHealth = maxHealth;

        if (anim == null)
            anim = GetComponent<Animator>();

        NotifyHealthChanged();
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDead || isInvulnerable)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        NotifyHealthChanged();

        if (anim != null)
            anim.SetTrigger("Hurt");

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        StartCoroutine(DamageInvulnerability());
    }


    public void HealToFull()
    {
        if (IsDead)
            return;

        if (currentHealth >= maxHealth)
            return;

        currentHealth = maxHealth;
        NotifyHealthChanged();
    }
    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
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
        gameObject.SetActive(false);
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
