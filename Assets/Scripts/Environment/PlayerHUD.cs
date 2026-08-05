using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// PlayerHUD
// Local-only HUD: ammo, health bar/text, damage flash overlay.
// Чисто локальный HUD: патроны, полоска/текст ХП, вспышка при уроне.
// Heavily optimized to avoid per-frame allocations (see "cache" fields below).
// Сильно оптимизирован против аллокаций каждый кадр (см. поля "cache" ниже).
// ============================================================
public class PlayerHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private Health playerHealth;

    [Header("Ammo")]
    [SerializeField] private TMP_Text ammoText;

    [Header("Health")]
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Image healthBar;

    [Header("Damage Flash")]
    [SerializeField] private Image damageOverlay;

    private float currentHealthPercent = 1f;
    private int lastHealth;
    private bool isDead;

    // --- Cache fields for GC optimization ---
    // --- Кэш-переменные для оптимизации GC ---
    private int lastAmmo = -1;
    private int lastReserveAmmo = -1;
    private WeaponController lastWeapon = null;

    private int lastDisplayedHealth = -1;
    private Color? lastHealthBarColor = null;
    // ------------------------------------------

    private readonly Color fullHealthColor = new Color(0.2f, 0.9f, 0.2f);
    private readonly Color mediumHealthColor = new Color(1f, 0.85f, 0.1f);
    private readonly Color lowHealthColor = new Color(0.9f, 0.15f, 0.15f);

    private void Start()
    {
        if (playerHealth == null)
            playerHealth = GetComponentInParent<Health>();

        if (weaponSwitcher == null)
            weaponSwitcher = GetComponentInParent<WeaponSwitcher>();

        // Subscribe to Health's C# events instead of polling every frame
        // Подписываемся на C# события Health вместо опроса каждый кадр
        if (playerHealth != null)
        {
            playerHealth.OnDeath += HandlePlayerDied;
            playerHealth.OnHealthChanged += HandleHealthChanged;
        }

        lastHealth = playerHealth != null ? playerHealth.CurrentHealth : 0;
        HandleHealthChanged(playerHealth != null ? playerHealth.CurrentHealth : 0);
        RefreshVisibility();
    }

    private void OnDestroy()
    {
        // Always unsubscribe on destroy to avoid dangling references / memory leaks
        // Всегда отписываемся при уничтожении, иначе висячие ссылки / утечки памяти
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandlePlayerDied;
            playerHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void Update()
    {
        if (isDead)
            return; // No point updating HUD elements that are hidden anyway // Нет смысла обновлять скрытый HUD

        UpdateAmmo();
        UpdateHealth();
        UpdateDamageFlash();
    }

    private void HandlePlayerDied()
    {
        isDead = true;
        RefreshVisibility();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HandleHealthChanged(int _)
    {
        if (playerHealth == null)
            return;

        if (playerHealth.IsDead)
        {
            if (!isDead)
                HandlePlayerDied();
            return;
        }

        isDead = false;
        RefreshVisibility();
    }

    // Toggles all HUD elements on/off together based on alive/dead state
    // Включает/выключает все элементы HUD разом по состоянию жив/мёртв
    private void RefreshVisibility()
    {
        bool showHud = !isDead;

        if (ammoText != null)
            ammoText.gameObject.SetActive(showHud);

        if (healthText != null)
            healthText.gameObject.SetActive(showHud);

        if (healthBar != null)
            healthBar.gameObject.SetActive(showHud);

        if (damageOverlay != null)
            damageOverlay.gameObject.SetActive(showHud);
    }

    private void UpdateAmmo()
    {
        if (weaponSwitcher == null)
            return;

        WeaponController weapon = weaponSwitcher.ActiveWeapon;

        // No active weapon (e.g. switching) // Нет активного оружия (например, во время переключения)
        if (weapon == null)
        {
            if (lastWeapon != null)
            {
                if (ammoText != null) ammoText.text = "";
                ResetAmmoCache();
            }
            return;
        }

        int currentAmmo = weapon.CurrentAmmo;
        int currentReserve = weapon.CurrentReserveAmmo;

        // Only touch the text if weapon or ammo count actually changed
        // Трогаем текст, только если реально сменилось оружие или число патронов
        if (weapon != lastWeapon || currentAmmo != lastAmmo || currentReserve != lastReserveAmmo)
        {
            if (ammoText != null)
                ammoText.SetText("{0} / {1}", currentAmmo, currentReserve); // Zero GC allocation overload // Оверлоад без аллокаций GC

            lastAmmo = currentAmmo;
            lastReserveAmmo = currentReserve;
        }

        lastWeapon = weapon;
    }

    private void UpdateHealth()
    {
        if (playerHealth == null)
            return;

        int currentHealth = playerHealth.CurrentHealth;

        // 1. HP text — only updates when the number actually changes (Zero GC)
        // 1. Текст ХП — обновляется ТОЛЬКО при изменении числа (Zero GC)
        if (currentHealth != lastDisplayedHealth)
        {
            if (healthText != null)
                healthText.SetText("{0}", currentHealth);

            lastDisplayedHealth = currentHealth;
        }

        // 2. Smooth fillAmount for the health bar // 2. Плавный fillAmount для полоски ХП
        float targetPercent = playerHealth.MaxHealth > 0 
            ? (float)currentHealth / playerHealth.MaxHealth 
            : 0f;

        if (Mathf.Abs(currentHealthPercent - targetPercent) > 0.0001f)
        {
            currentHealthPercent = Mathf.Lerp(currentHealthPercent, targetPercent, Time.deltaTime * 10f);
            if (healthBar != null)
                healthBar.fillAmount = currentHealthPercent;
        }
        else if (healthBar != null && healthBar.fillAmount != targetPercent)
        {
            // Snap to exact value once close enough, to avoid an endless tiny Lerp tail
            // Довыставляем точное значение, когда близко, чтобы не тянуть бесконечный хвост Lerp
            currentHealthPercent = targetPercent;
            healthBar.fillAmount = targetPercent;
        }

        // 3. Bar color — only updates when it crosses into a different range
        // 3. Цвет полоски — обновляется, только если сменился диапазон
        Color targetColor = targetPercent > 0.6f ? fullHealthColor :
                            targetPercent > 0.3f ? mediumHealthColor : lowHealthColor;

        if (healthBar != null && lastHealthBarColor != targetColor)
        {
            healthBar.color = targetColor;
            lastHealthBarColor = targetColor;
        }
    }

    private void UpdateDamageFlash()
    {
        if (playerHealth == null || damageOverlay == null)
            return;

        int currentHealth = playerHealth.CurrentHealth;

        // Flash on damage taken // Вспышка при получении урона
        if (currentHealth < lastHealth)
        {
            Color color = damageOverlay.color;
            color.a = 0.35f;
            damageOverlay.color = color;
        }

        // Fade out (only runs while alpha is still above 0) // Затухание (работает, пока альфа больше 0)
        Color fade = damageOverlay.color;
        if (fade.a > 0.001f)
        {
            fade.a = Mathf.Lerp(fade.a, 0f, Time.deltaTime * 8f);
            if (fade.a <= 0.001f) fade.a = 0f;
            damageOverlay.color = fade;
        }

        lastHealth = currentHealth;
    }

    private void ResetAmmoCache()
    {
        lastWeapon = null;
        lastAmmo = -1;
        lastReserveAmmo = -1;
    }
}