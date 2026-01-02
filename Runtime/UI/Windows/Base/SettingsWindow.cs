// Packages/com.protosystem.core/Runtime/UI/Windows/Base/SettingsWindow.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Окно настроек
    /// </summary>
    [UIWindow("Settings", WindowType.Normal, WindowLayer.Windows, Level = 1, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
    public class SettingsWindow : UIWindowBase
    {
        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TMP_Text masterVolumeText;
        [SerializeField] private TMP_Text musicVolumeText;
        [SerializeField] private TMP_Text sfxVolumeText;

        [Header("Graphics")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vsyncToggle;

        [Header("Gameplay")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private TMP_Text sensitivityText;
        [SerializeField] private Toggle invertYToggle;

        [Header("Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button backButton;

        // Cached settings
        private float _masterVolume = 1f;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;
        private int _qualityLevel;
        private bool _fullscreen;
        private bool _vsync;
        private float _sensitivity = 1f;
        private bool _invertY;

        protected override void Awake()
        {
            base.Awake();
            
            // Sliders
            masterVolumeSlider?.onValueChanged.AddListener(OnMasterVolumeChanged);
            musicVolumeSlider?.onValueChanged.AddListener(OnMusicVolumeChanged);
            sfxVolumeSlider?.onValueChanged.AddListener(OnSfxVolumeChanged);
            sensitivitySlider?.onValueChanged.AddListener(OnSensitivityChanged);
            
            // Toggles
            fullscreenToggle?.onValueChanged.AddListener(OnFullscreenChanged);
            vsyncToggle?.onValueChanged.AddListener(OnVsyncChanged);
            invertYToggle?.onValueChanged.AddListener(OnInvertYChanged);
            
            // Dropdowns
            qualityDropdown?.onValueChanged.AddListener(OnQualityChanged);
            resolutionDropdown?.onValueChanged.AddListener(OnResolutionChanged);
            
            // Buttons
            applyButton?.onClick.AddListener(OnApplyClicked);
            resetButton?.onClick.AddListener(OnResetClicked);
            backButton?.onClick.AddListener(OnBackClicked);
        }

        public override void Show(System.Action onComplete = null)
        {
            base.Show(onComplete);
            LoadCurrentSettings();
        }

        protected virtual void LoadCurrentSettings()
        {
            // Audio
            _masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            _musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
            _sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1f);
            
            if (masterVolumeSlider != null) masterVolumeSlider.value = _masterVolume;
            if (musicVolumeSlider != null) musicVolumeSlider.value = _musicVolume;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = _sfxVolume;
            
            // Graphics
            _qualityLevel = QualitySettings.GetQualityLevel();
            _fullscreen = Screen.fullScreen;
            _vsync = QualitySettings.vSyncCount > 0;
            
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
                qualityDropdown.value = _qualityLevel;
            }
            
            if (fullscreenToggle != null) fullscreenToggle.isOn = _fullscreen;
            if (vsyncToggle != null) vsyncToggle.isOn = _vsync;
            
            // Gameplay
            _sensitivity = PlayerPrefs.GetFloat("Sensitivity", 1f);
            _invertY = PlayerPrefs.GetInt("InvertY", 0) == 1;
            
            if (sensitivitySlider != null) sensitivitySlider.value = _sensitivity;
            if (invertYToggle != null) invertYToggle.isOn = _invertY;
            
            UpdateTexts();
        }

        private void UpdateTexts()
        {
            if (masterVolumeText != null) masterVolumeText.text = $"{Mathf.RoundToInt(_masterVolume * 100)}%";
            if (musicVolumeText != null) musicVolumeText.text = $"{Mathf.RoundToInt(_musicVolume * 100)}%";
            if (sfxVolumeText != null) sfxVolumeText.text = $"{Mathf.RoundToInt(_sfxVolume * 100)}%";
            if (sensitivityText != null) sensitivityText.text = $"{_sensitivity:F1}";
        }

        #region Event Handlers

        private void OnMasterVolumeChanged(float value)
        {
            _masterVolume = value;
            UpdateTexts();
            // AudioListener.volume = value;
        }

        private void OnMusicVolumeChanged(float value)
        {
            _musicVolume = value;
            UpdateTexts();
        }

        private void OnSfxVolumeChanged(float value)
        {
            _sfxVolume = value;
            UpdateTexts();
        }

        private void OnSensitivityChanged(float value)
        {
            _sensitivity = value;
            UpdateTexts();
        }

        private void OnFullscreenChanged(bool value)
        {
            _fullscreen = value;
        }

        private void OnVsyncChanged(bool value)
        {
            _vsync = value;
        }

        private void OnInvertYChanged(bool value)
        {
            _invertY = value;
        }

        private void OnQualityChanged(int index)
        {
            _qualityLevel = index;
        }

        private void OnResolutionChanged(int index)
        {
            // Применить разрешение
        }

        protected virtual void OnApplyClicked()
        {
            ApplySettings();
            UISystem.Back();
        }

        protected virtual void OnResetClicked()
        {
            // Сброс к дефолтам
            _masterVolume = 1f;
            _musicVolume = 1f;
            _sfxVolume = 1f;
            _sensitivity = 1f;
            _invertY = false;
            _fullscreen = true;
            _vsync = true;
            _qualityLevel = QualitySettings.names.Length - 1;
            
            // Обновляем UI
            if (masterVolumeSlider != null) masterVolumeSlider.value = _masterVolume;
            if (musicVolumeSlider != null) musicVolumeSlider.value = _musicVolume;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = _sfxVolume;
            if (sensitivitySlider != null) sensitivitySlider.value = _sensitivity;
            if (invertYToggle != null) invertYToggle.isOn = _invertY;
            if (fullscreenToggle != null) fullscreenToggle.isOn = _fullscreen;
            if (vsyncToggle != null) vsyncToggle.isOn = _vsync;
            if (qualityDropdown != null) qualityDropdown.value = _qualityLevel;
            
            UpdateTexts();
        }

        protected virtual void OnBackClicked()
        {
            UISystem.Back();
        }

        #endregion

        protected virtual void ApplySettings()
        {
            // Audio
            PlayerPrefs.SetFloat("MasterVolume", _masterVolume);
            PlayerPrefs.SetFloat("MusicVolume", _musicVolume);
            PlayerPrefs.SetFloat("SfxVolume", _sfxVolume);
            AudioListener.volume = _masterVolume;
            
            // Graphics
            QualitySettings.SetQualityLevel(_qualityLevel);
            Screen.fullScreen = _fullscreen;
            QualitySettings.vSyncCount = _vsync ? 1 : 0;
            
            // Gameplay
            PlayerPrefs.SetFloat("Sensitivity", _sensitivity);
            PlayerPrefs.SetInt("InvertY", _invertY ? 1 : 0);
            
            PlayerPrefs.Save();
            
            Debug.Log("[Settings] Applied and saved");
        }
    }
}
