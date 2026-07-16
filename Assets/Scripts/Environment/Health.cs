using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    [Header("Regeneration")]
    [SerializeField] private bool useRegeneration = true;
    [SerializeField] private float regenerationDelay = 8f;
    [SerializeField] private float regenerationSpeed = 24f;

    private float currentHealth;

    public int CurrentHealth => Mathf.RoundToInt(currentHealth);
    public int MaxHealth => maxHealth;

    public event Action<int> OnHealthChanged;

    private float lastDamageTime;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (!useRegeneration)
            return;

        if (currentHealth >= maxHealth)
            return;

        if (Time.time < lastDamageTime + regenerationDelay)
            return;

        currentHealth += regenerationSpeed * Time.deltaTime;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        OnHealthChanged?.Invoke(CurrentHealth);
    }

    public void SetHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth);
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        lastDamageTime = Time.time;

        OnHealthChanged?.Invoke(CurrentHealth);

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        OnHealthChanged?.Invoke(CurrentHealth);
    }

    private void Die()
    {
        Debug.Log("Player died");
    }
}