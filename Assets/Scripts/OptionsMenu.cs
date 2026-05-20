using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class OptionsMenu : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Slider for Master Volume (0 to 1).")]
    public Slider volumeSlider;

    [Tooltip("Dropdown for Graphics Quality.")]
    public TMP_Dropdown qualityDropdown; // Using TMP Dropdown as it is standard now

    [Tooltip("Toggle for Fullscreen mode.")]
    public Toggle fullscreenToggle;

    [Tooltip("Slider for Drone Control Sensitivity (Acro Rate multiplier etc).")]
    public Slider sensitivitySlider;

    [Header("Settings Keys")]
    private const string PREF_VOLUME = "MasterVolume";
    private const string PREF_QUALITY = "QualityLevel";
    private const string PREF_FULLSCREEN = "IsFullscreen";
    private const string PREF_SENSITIVITY = "ControlSensitivity";

    private void Start()
    {
        // Initialize UI with saved or default values

        // 1. Volume
        float savedVolume = PlayerPrefs.GetFloat(PREF_VOLUME, 1f);
        if (volumeSlider != null)
        {
            volumeSlider.value = savedVolume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        AudioListener.volume = savedVolume;

        // 2. Quality
        // Populate dropdown if empty? Or assume setup in Editor?
        // Let's assume user sets up options in Editor, or we can auto-fill.
        // Auto-filling is safer/more generic.
        if (qualityDropdown != null)
        {
            if (qualityDropdown.options.Count == 0)
            {
                qualityDropdown.ClearOptions();
                List<string> options = new List<string>(QualitySettings.names);
                qualityDropdown.AddOptions(options);
            }

            int savedQuality = PlayerPrefs.GetInt(PREF_QUALITY, QualitySettings.GetQualityLevel());
            qualityDropdown.value = savedQuality;
            qualityDropdown.RefreshShownValue();

            qualityDropdown.onValueChanged.AddListener(SetQuality);
        }

        // 3. Fullscreen
        bool isFullscreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = isFullscreen;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
        Screen.fullScreen = isFullscreen;

        // 4. Sensitivity
        // Default 1.0 (multiplier)
        float savedSens = PlayerPrefs.GetFloat(PREF_SENSITIVITY, 1f);
        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = savedSens;
            sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        }
    }

    /// <summary>
    /// Called by PauseMenu to ensure UI matches current state if changed elsewhere,
    /// or just to be sure when opening the menu.
    /// </summary>
    public void RefreshUI()
    {
        if (volumeSlider != null) volumeSlider.value = AudioListener.volume;
        if (qualityDropdown != null) qualityDropdown.value = QualitySettings.GetQualityLevel();
        if (fullscreenToggle != null) fullscreenToggle.isOn = Screen.fullScreen;

        // Sensitivity might need to be fetched from PlayerPrefs if not stored globally
        if (sensitivitySlider != null) sensitivitySlider.value = PlayerPrefs.GetFloat(PREF_SENSITIVITY, 1f);
    }

    public void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(PREF_VOLUME, value);
    }

    public void SetQuality(int index)
    {
        QualitySettings.SetQualityLevel(index);
        PlayerPrefs.SetInt(PREF_QUALITY, index);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(PREF_FULLSCREEN, isFullscreen ? 1 : 0);
    }

    public void SetSensitivity(float value)
    {
        PlayerPrefs.SetFloat(PREF_SENSITIVITY, value);

        // Apply immediately if possible
        ApplySensitivityToDrone(value);
    }

    private void ApplySensitivityToDrone(float multiplier)
    {
        // Try to find the drone controller and update it
        var drone = FindObjectOfType<DroneController>();
        if (drone != null)
        {
            drone.sensitivityMultiplier = multiplier;
        }
    }
}
