using UnityEngine;

public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance { get; private set; }

    [Header("Sensitivity")]
    public float baseSensitivity = 2f;
    public float adsSensitivityMultiplier = 1.0f; // Множитель чувст. в прицеле (0.5 = в 2 раза медленнее)

    [Header("Audio")]
    public float masterVolume = 1f;

    [Header("Graphics")]
    public bool isFullscreen = true;

    private const string SENS_KEY = "BaseSensitivity";
    private const string ADS_SENS_KEY = "AdsSensitivityMultiplier";
    private const string VOLUME_KEY = "MasterVolume";
    private const string FULLSCREEN_KEY = "IsFullscreen";
    private const string RES_WIDTH_KEY = "ResolutionWidth";
    private const string RES_HEIGHT_KEY = "ResolutionHeight";

    private void Awake()
    {
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

    public void LoadSettings()
    {
        baseSensitivity = PlayerPrefs.GetFloat(SENS_KEY, 2f);
        adsSensitivityMultiplier = PlayerPrefs.GetFloat(ADS_SENS_KEY, 1.0f);
        masterVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);
        isFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, 1) == 1;

        AudioListener.volume = masterVolume;

        if (PlayerPrefs.HasKey(RES_WIDTH_KEY) && PlayerPrefs.HasKey(RES_HEIGHT_KEY))
        {
            int width = PlayerPrefs.GetInt(RES_WIDTH_KEY);
            int height = PlayerPrefs.GetInt(RES_HEIGHT_KEY);
            Screen.SetResolution(width, height, isFullscreen);
        }
    }

    public void SetBaseSensitivity(float value)
    {
        baseSensitivity = value;
        PlayerPrefs.SetFloat(SENS_KEY, value);
        PlayerPrefs.Save();
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
    /// Динамически расчитывает чувствительность в зависимости от текущего FOV камеры (как в CS/Valorant).
    /// При FOV 10 у снайперки сенса автоматически станет плавной и контролируемой.
    /// </summary>
    public float GetCurrentSensitivity(bool isAiming, float currentFOV, float baseFOV = 75f)
    {
        if (!isAiming)
            return baseSensitivity;

        // Защита от деления на ноль
        if (baseFOV <= 0f) baseFOV = 75f;

        // Пропорция приближения (при FOV 10 получим 10 / 75 = 0.133)
        float fovFactor = currentFOV / baseFOV;

        // Базовая сенса * масштабирование от FOV * пользовательский множитель из настроек
        return baseSensitivity * fovFactor * adsSensitivityMultiplier;
    }
}