using UnityEngine;

// ============================================================
// GameSettings
// Global singleton storing/persisting player settings (sensitivity, audio, graphics).
// Глобальный синглтон с настройками игрока (чувствительность, звук, графика).
// Uses PlayerPrefs for saving between sessions. Not networked — purely local/per-client.
// Хранит через PlayerPrefs между сессиями. Не сетевой — чисто локальный, для каждого клиента свой.
// ============================================================
public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance { get; private set; } // Global access point // Точка глобального доступа

    [Header("Sensitivity")]
    public float baseSensitivity = 2f;
    public float adsSensitivityMultiplier = 1.0f; // Multiplier while aiming (ADS) // Множитель чувст. в прицеле

    [Header("Audio")]
    public float masterVolume = 1f;

    [Header("Graphics")]
    public bool isFullscreen = true;

    // PlayerPrefs keys // Ключи для PlayerPrefs
    private const string SENS_KEY = "BaseSensitivity";
    private const string ADS_SENS_KEY = "AdsSensitivityMultiplier";
    private const string VOLUME_KEY = "MasterVolume";
    private const string FULLSCREEN_KEY = "IsFullscreen";
    private const string RES_WIDTH_KEY = "ResolutionWidth";
    private const string RES_HEIGHT_KEY = "ResolutionHeight";

    private void Awake()
    {
        // Classic singleton guard: first instance survives, duplicates self-destruct
        // Классический синглтон: первый экземпляр остаётся, дубликаты уничтожаются
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Load saved values from PlayerPrefs and apply them // Загружаем сохранённые значения и применяем их
    public void LoadSettings()
    {
        baseSensitivity = PlayerPrefs.GetFloat(SENS_KEY, 2f);
        adsSensitivityMultiplier = PlayerPrefs.GetFloat(ADS_SENS_KEY, 1.0f);
        masterVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);
        isFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, 1) == 1; // PlayerPrefs has no bool, use 1/0 // Нет типа bool, используем 1/0

        AudioListener.volume = masterVolume; // Apply volume immediately // Применяем громкость сразу

        // Apply saved resolution only if both keys exist // Применяем разрешение, только если есть оба ключа
        if (PlayerPrefs.HasKey(RES_WIDTH_KEY) && PlayerPrefs.HasKey(RES_HEIGHT_KEY))
        {
            int width = PlayerPrefs.GetInt(RES_WIDTH_KEY);
            int height = PlayerPrefs.GetInt(RES_HEIGHT_KEY);
            Screen.SetResolution(width, height, isFullscreen);
        }
    }

    // Setters below: update field + apply effect + save to disk immediately
    // Сеттеры ниже: обновляют поле + применяют эффект + сразу сохраняют на диск

    public void SetBaseSensitivity(float value)
    {
        baseSensitivity = value;
        PlayerPrefs.SetFloat(SENS_KEY, value);
        PlayerPrefs.Save(); // Force write to disk // Принудительный сброс на диск
    }

    public void SetAdsSensitivityMultiplier(float value)
    {
        adsSensitivityMultiplier = value;
        PlayerPrefs.SetFloat(ADS_SENS_KEY, value);
        PlayerPrefs.Save();
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = value;
        AudioListener.volume = masterVolume;
        PlayerPrefs.SetFloat(VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool isFull)
    {
        isFullscreen = isFull;
        Screen.fullScreen = isFull;
        PlayerPrefs.SetInt(FULLSCREEN_KEY, isFull ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetResolution(int width, int height)
    {
        Screen.SetResolution(width, height, isFullscreen);
        PlayerPrefs.SetInt(RES_WIDTH_KEY, width);
        PlayerPrefs.SetInt(RES_HEIGHT_KEY, height);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Computes sensitivity based on camera FOV, like CS/Valorant (zoomed-in = slower).
    /// Считает чувствительность от FOV камеры, как в CS/Valorant (зум = медленнее).
    /// </summary>
    public float GetCurrentSensitivity(bool isAiming, float currentFOV, float baseFOV = 75f)
    {
        if (!isAiming)
            return baseSensitivity; // Hip-fire: raw base sens // От бедра — обычная сенса

        if (baseFOV <= 0f) baseFOV = 75f; // Guard against divide-by-zero // Защита от деления на ноль

        float fovFactor = currentFOV / baseFOV; // Zoom ratio, e.g. 10/75 = 0.133 // Пропорция зума

        return baseSensitivity * fovFactor * adsSensitivityMultiplier;
    }
}