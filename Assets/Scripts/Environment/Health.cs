using System;
using UnityEngine;
using Mirror;

public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    [Header("Regeneration")]
    [SerializeField] private bool useRegeneration = true;
    [SerializeField] private float regenerationDelay = 8f;
    [SerializeField] private float regenerationSpeed = 24f;

    private float currentHealth;
    private float lastDamageTime;
    private bool isDead;

    public int CurrentHealth => Mathf.RoundToInt(currentHealth);
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;

    // Направление последнего попадания (мировой вектор полёта пули).
    // Нужно для того, чтобы тело при смерти падало именно туда, куда летела пуля.
    public Vector3 LastHitDirection { get; private set; }

    public event Action<int> OnHealthChanged;
    public event Action OnDeath;

    private void Awake()
    {
        currentHealth = maxHealth;
        isDead = false;
    }

    private void Update()
    {
        if (!NetworkServer.active)
            return;

        if (isDead)
            return;

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
        int clampedValue = Mathf.Clamp(value, 0, maxHealth);

        if (clampedValue <= 0 && !isDead)
        {
            isDead = true;
            currentHealth = 0f;
            OnHealthChanged?.Invoke(CurrentHealth);
            OnDeath?.Invoke();
            return;
        }

        if (isDead && clampedValue > 0)
            isDead = false;

        currentHealth = clampedValue;
        OnHealthChanged?.Invoke(CurrentHealth);
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, Vector3.zero);
    }

    // Новый оверлоад: то же самое, но плюс направление выстрела.
    // Старый код, вызывающий TakeDamage(damage), продолжает работать как раньше -
    // просто без направления (тело при смерти упадёт назад по умолчанию).
    public void TakeDamage(float damage, Vector3 hitDirection)
    {
        if (!NetworkServer.active)
            return;

        if (isDead)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        lastDamageTime = Time.time;

        if (hitDirection != Vector3.zero)
            LastHitDirection = hitDirection.normalized;

        OnHealthChanged?.Invoke(CurrentHealth);

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(int amount)
    {
        if (!NetworkServer.active)
            return;

        if (isDead)
            return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        OnHealthChanged?.Invoke(CurrentHealth);
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        currentHealth = 0f;

        OnHealthChanged?.Invoke(CurrentHealth);
        OnDeath?.Invoke();
    }

    public void Respawn()
    {
        if (!NetworkServer.active)
            return;

        isDead = false;
        currentHealth = maxHealth;
        lastDamageTime = Time.time;

        OnHealthChanged?.Invoke(CurrentHealth);
    }
}