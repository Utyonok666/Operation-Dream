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

        // Only hide the normal HUD on death; no built-in respawn menu logic here.
    }

    private void UpdateAmmo()
    {
        WeaponController weapon = weaponSwitcher.ActiveWeapon;

        if (weapon == null)
        {
            ammoText.text = "";
            reloadText.gameObject.SetActive(false);
            return;
        }

        ammoText.text = $"{weapon.CurrentAmmo} / {weapon.CurrentReserveAmmo}";
        reloadText.gameObject.SetActive(weapon.IsReloading);
    }

    private void UpdateHealth()
    {
        float targetPercent =
            (float)playerHealth.CurrentHealth / playerHealth.MaxHealth;

        currentHealthPercent = Mathf.Lerp(
            currentHealthPercent,
            targetPercent,
            Time.deltaTime * 10f);

        healthBar.rectTransform.localScale =
            new Vector3(1f, currentHealthPercent, 1f);

        healthText.text = playerHealth.CurrentHealth.ToString();

        if (targetPercent > 0.6f)
        {
            healthBar.color = fullHealthColor;
        }
        else if (targetPercent > 0.3f)
        {
            healthBar.color = mediumHealthColor;
        }
        else
        {
            healthBar.color = lowHealthColor;
        }
    }

    private void UpdateDamageFlash()
    {
        if (playerHealth.CurrentHealth < lastHealth)
        {
            Color color = damageOverlay.color;
            color.a = 0.35f;
            damageOverlay.color = color;
        }

        Color fade = damageOverlay.color;

        fade.a = Mathf.Lerp(
            fade.a,
            0f,
            Time.deltaTime * 8f);

        damageOverlay.color = fade;

        lastHealth = playerHealth.CurrentHealth;
    }
}