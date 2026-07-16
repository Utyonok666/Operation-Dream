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

    private readonly Color fullHealthColor = new Color(0.2f, 0.9f, 0.2f);
    private readonly Color mediumHealthColor = new Color(1f, 0.85f, 0.1f);
    private readonly Color lowHealthColor = new Color(0.9f, 0.15f, 0.15f);

    private void Start()
    {
        lastHealth = playerHealth.CurrentHealth;
    }

    private void Update()
    {
        UpdateAmmo();
        UpdateHealth();
        UpdateDamageFlash();
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