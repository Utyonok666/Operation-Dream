using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private Health playerHealth;

    [Header("Ammo")]
    [SerializeField] private TMP_Text ammoText;

    [Header("Reload")]
    [SerializeField] private TMP_Text reloadText;

    [Header("Health")]
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Image healthBar;

    [Header("Damage Flash")]
    [SerializeField] private Image damageOverlay;

    private float currentHealthPercent = 1f;
    private int lastHealth;
    private bool isDead;

    // --- Переменные кэша для GC Оптимизации ---
    private int lastAmmo = -1;
    private int lastReserveAmmo = -1;
    private bool? lastIsReloading = null;
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
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandlePlayerDied;
            playerHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void Update()
    {
        if (isDead)
            return;

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

    private void RefreshVisibility()
    {
        bool showHud = !isDead;

        if (ammoText != null)
            ammoText.gameObject.SetActive(showHud);

        if (reloadText != null)
            reloadText.gameObject.SetActive(showHud);

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

        // Если нет активного оружия
        if (weapon == null)
        {
            if (lastWeapon != null)
            {
                if (ammoText != null) ammoText.text = "";
                if (reloadText != null) reloadText.gameObject.SetActive(false);
                ResetAmmoCache();
            }
            return;
        }

        int currentAmmo = weapon.CurrentAmmo;
        int currentReserve = weapon.CurrentReserveAmmo;
        bool isReloading = weapon.IsReloading;

        // Обновляем текст патронов только если сменилось оружие или количество
        if (weapon != lastWeapon || currentAmmo != lastAmmo || currentReserve != lastReserveAmmo)
        {
            if (ammoText != null)
                ammoText.SetText("{0} / {1}", currentAmmo, currentReserve); // Zero GC allocation

            lastAmmo = currentAmmo;
            lastReserveAmmo = currentReserve;
        }

        // Обновляем статус перезарядки только при изменении состояния
        if (weapon != lastWeapon || lastIsReloading == null || isReloading != lastIsReloading.Value)
        {
            if (reloadText != null)
                reloadText.gameObject.SetActive(isReloading);

            lastIsReloading = isReloading;
        }

        lastWeapon = weapon;
    }

    private void UpdateHealth()
    {
        if (playerHealth == null)
            return;

        int currentHealth = playerHealth.CurrentHealth;

        // 1. Текст хп — обновляется ТОЛЬКО при изменении числа (Zero GC allocation)
        if (currentHealth != lastDisplayedHealth)
        {
            if (healthText != null)
                healthText.SetText("{0}", currentHealth);

            lastDisplayedHealth = currentHealth;
        }

        // 2. Плавный fillAmount для HealthBar
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
            currentHealthPercent = targetPercent;
            healthBar.fillAmount = targetPercent;
        }

        // 3. Цвет полоски — обновляется ТОЛЬКО если сменился цветовой диапазон
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

        // Вспышка при получении урона
        if (currentHealth < lastHealth)
        {
            Color color = damageOverlay.color;
            color.a = 0.35f;
            damageOverlay.color = color;
        }

        // Затухание вспышки (выполняется только пока альфа больше 0)
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
        lastIsReloading = null;
    }
}