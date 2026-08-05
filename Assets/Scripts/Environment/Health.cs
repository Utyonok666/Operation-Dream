using System;
using UnityEngine;
using Mirror;

// ============================================================
// Health
// Server-authoritative HP: damage, regen, death, respawn.
// Серверно-авторитетное HP: урон, реген, смерть, респавн.
// Implements IDamageable so weapons don't need to know the concrete target type.
// Реализует IDamageable, чтобы оружие не знало конкретный тип цели.
// ============================================================
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    [Header("Regeneration")]
    [SerializeField] private bool useRegeneration = true;
    [SerializeField] private float regenerationDelay = 8f; // Wait time after last hit before regen starts // Пауза после удара перед началом регена
    [SerializeField] private float regenerationSpeed = 24f; // HP per second // ХП в секунду

    private float currentHealth; // Float internally for smooth regen math // Float внутри для плавного счёта регена
    private float lastDamageTime;
    private bool isDead;

    public int CurrentHealth => Mathf.RoundToInt(currentHealth);
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;

    // World-space direction of the last hit, used so the corpse falls the way the bullet was flying
    // Направление последнего попадания, нужно чтобы труп падал именно туда, куда летела пуля
    public Vector3 LastHitDirection { get; private set; }

    // C# events, not SyncVars — used locally (e.g. by PlayerHUD) to react to changes
    // Обычные C# события, не SyncVar — используются локально (напр. PlayerHUD), чтобы реагировать на изменения
    public event Action<int> OnHealthChanged;
    public event Action OnDeath;

    private void Awake()
    {
        currentHealth = maxHealth;
        isDead = false;
    }

    private void Update()
    {
        // All HP logic is server-only — clients never decide their own HP
        // Вся логика HP только на сервере — клиенты никогда не решают своё HP сами
        if (!NetworkServer.active)
            return;

        if (isDead)
            return;

        if (!useRegeneration)
            return;

        if (currentHealth >= maxHealth)
            return;

        if (Time.time < lastDamageTime + regenerationDelay)
            return; // Still within the "no regen" window after last hit // Ещё в окне "без регена" после удара

        currentHealth += regenerationSpeed * Time.deltaTime;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        OnHealthChanged?.Invoke(CurrentHealth);
    }

    // Directly sets HP (e.g. from an external system), handles death/revive edge cases
    // Напрямую выставляет HP (напр. из внешней системы), обрабатывает грани смерти/оживления
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
            isDead = false; // Value pushed back above 0 revives without a full Respawn() call // Значение выше 0 оживляет без полного Respawn()

        currentHealth = clampedValue;
        OnHealthChanged?.Invoke(CurrentHealth);
    }

    // IDamageable implementation, no hit direction // Реализация IDamageable, без направления попадания
    public void TakeDamage(float damage)
    {
        TakeDamage(damage, Vector3.zero);
    }

    // Overload with hit direction — old callers using TakeDamage(damage) still work fine
    // Оверлоад с направлением попадания — старый вызов TakeDamage(damage) продолжает работать
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
        OnDeath?.Invoke(); // PlayerDeath.cs subscribes to this to trigger the death sequence // PlayerDeath.cs подписан на это, чтобы запустить сценарий смерти
    }

    public void Respawn()
    {
        if (!NetworkServer.active)
            return;

        isDead = false;
        currentHealth = maxHealth;
        lastDamageTime = Time.time; // Reset regen delay so regen doesn't instantly kick in // Сброс таймера регена, чтобы он не сработал сразу

        OnHealthChanged?.Invoke(CurrentHealth);
    }
}