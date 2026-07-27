using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [Header("Sensitivity UI")]
    [SerializeField] private Slider baseSensSlider;
    [SerializeField] private TMP_Text baseSensText;
    [SerializeField] private Slider adsSensSlider;
    [SerializeField] private TMP_Text adsSensText;

    [Header("Audio UI")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeText;

    [Header("Display UI")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private Resolution[] resolutions;

    private void Start()
    {
        InitializeResolutions();
        LoadCurrentUIValues();

        if (baseSensSlider != null) baseSensSlider.onValueChanged.AddListener(OnBaseSensChanged);
        if (adsSensSlider != null) adsSensSlider.onValueChanged.AddListener(OnAdsSensChanged);
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void LoadCurrentUIValues()
    {
        if (GameSettings.Instance == null) return;

        if (baseSensSlider != null)
        {
            baseSensSlider.value = GameSettings.Instance.baseSensitivity;
            UpdateSensText(GameSettings.Instance.baseSensitivity);
        }

        if (adsSensSlider != null)
        {
            adsSensSlider.value = GameSettings.Instance.adsSensitivityMultiplier;
            UpdateAdsSensText(GameSettings.Instance.adsSensitivityMultiplier);
        }

        if (volumeSlider != null)
        {
            volumeSlider.value = GameSettings.Instance.masterVolume;
            UpdateVolumeText(GameSettings.Instance.masterVolume);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = GameSettings.Instance.isFullscreen;
        }
    }

    private void InitializeResolutions()
    {
        if (resolutionDropdown == null) return;

        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = $"{resolutions[i].width} x {resolutions[i].height} @ {resolutions[i].refreshRateRatio.value:F0}Hz";
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void OnBaseSensChanged(float val)
    {
        if (GameSettings.Instance != null)
            GameSettings.Instance.SetBaseSensitivity(val);
        UpdateSensText(val);
    }

    private void OnAdsSensChanged(float val)
    {
        if (GameSettings.Instance != null)
            GameSettings.Instance.SetAdsSensitivityMultiplier(val);
        UpdateAdsSensText(val);
    }

    private void OnVolumeChanged(float val)
    {
        if (GameSettings.Instance != null)
            GameSettings.Instance.SetMasterVolume(val);
        UpdateVolumeText(val);
    }

    private void OnFullscreenChanged(bool isFull)
    {
        if (GameSettings.Instance != null)
            GameSettings.Instance.SetFullscreen(isFull);
    }

    private void OnResolutionChanged(int index)
    {
        if (resolutions == null || index < 0 || index >= resolutions.Length) return;
        Resolution res = resolutions[index];
        if (GameSettings.Instance != null)
            GameSettings.Instance.SetResolution(res.width, res.height);
    }

    private void UpdateSensText(float val)
    {
        if (baseSensText != null) 
        baseSensText.text = $"Sensitivity: {val:F1}";
    }

    private void UpdateAdsSensText(float val)
    {
        if (adsSensText != null) 
        adsSensText.text = $"Aim Sensitivity: {val:F2}";
    }

    private void UpdateVolumeText(float val)
    {
        if (volumeText != null) 
        volumeText.text = $"Volume: {Mathf.RoundToInt(val * 100f)}%";
    }
}