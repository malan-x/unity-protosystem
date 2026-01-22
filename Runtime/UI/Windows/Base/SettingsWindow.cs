// Packages/com.protosystem.core/Runtime/UI/Windows/Base/SettingsWindow.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;
using ProtoSystem.Settings;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Данные слайдера громкости для динамической генерации (AudioMixer)
    /// </summary>
    [Serializable]
    public class VolumeSliderData
    {
        [Tooltip("Имя параметра в AudioMixer")]
        public string parameterName;
        
        [Tooltip("Отображаемое имя")]
        public string displayName;
        
        [Tooltip("Компонент слайдера")]
        public Slider slider;
        
        [Tooltip("Текст значения")]
        public TMP_Text valueText;
        
        [HideInInspector]
        public float currentValue = 1f;
    }
    
    /// <summary>
    /// Окно настроек — UI интерфейс для SettingsSystem
    /// </summary>
    [UIWindow("Settings", WindowType.Normal, WindowLayer.Windows, Level = 1, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
    public class SettingsWindow : UIWindowBase
    {
        [Header("Audio Mixer")]
        [SerializeField] protected AudioMixer audioMixer;
        
        [Header("Volume Sliders (динамические из AudioMixer)")]
        [SerializeField] protected List<VolumeSliderData> volumeSliders = new List<VolumeSliderData>();

        [Header("Graphics")]
        [SerializeField] protected TMP_Dropdown qualityDropdown;
        [SerializeField] protected TMP_Dropdown resolutionDropdown;
        [SerializeField] protected Toggle fullscreenToggle;
        [SerializeField] protected Toggle vsyncToggle;

        [Header("Gameplay")]
        [SerializeField] protected Slider sensitivitySlider;
        [SerializeField] protected TMP_Text sensitivityText;
        [SerializeField] protected Toggle invertYToggle;

        [Header("Buttons")]
        [SerializeField] protected Button applyButton;
        [SerializeField] protected Button resetButton;
        [SerializeField] protected Button backButton;

        // SettingsSystem reference
        private SettingsSystem _settings;
        
        // Кастомная секция для дополнительных AudioMixer параметров
        private DynamicSettingsSection _audioMixerSection;
        private const string AUDIO_MIXER_SECTION = "AudioMixer";
        
        // Флаг: были ли изменения применены
        private bool _changesApplied = false;

        protected override void Awake()
        {
            base.Awake();
            
            SetupAudioListeners();
            SetupGraphicsListeners();
            SetupGameplayListeners();
            SetupButtons();
        }

        private void SetupAudioListeners()
        {
            foreach (var data in volumeSliders)
            {
                if (data.slider != null)
                {
                    var captured = data;
                    data.slider.onValueChanged.AddListener(value => OnVolumeSliderChanged(captured, value));
                }
            }
        }

        private void SetupGraphicsListeners()
        {
            fullscreenToggle?.onValueChanged.AddListener(OnFullscreenChanged);
            vsyncToggle?.onValueChanged.AddListener(OnVsyncChanged);
            qualityDropdown?.onValueChanged.AddListener(OnQualityChanged);
            resolutionDropdown?.onValueChanged.AddListener(OnResolutionChanged);
        }

        private void SetupGameplayListeners()
        {
            sensitivitySlider?.onValueChanged.AddListener(OnSensitivityChanged);
            invertYToggle?.onValueChanged.AddListener(OnInvertYChanged);
        }

        private void SetupButtons()
        {
            applyButton?.onClick.AddListener(OnApplyClicked);
            resetButton?.onClick.AddListener(OnResetClicked);
            backButton?.onClick.AddListener(OnBackClicked);
        }

        public override void Show(Action onComplete = null)
        {
            base.Show(onComplete);

            _settings = SettingsSystem.Instance;
            _changesApplied = false;

            if (_settings == null)
            {
                Debug.LogWarning("[SettingsWindow] SettingsSystem not found!");
            }
            else
            {
                // Регистрируем кастомные AudioMixer параметры
                RegisterCustomAudioParameters();
            }

            LoadCurrentSettings();
        }

        public override void Hide(Action onComplete = null)
        {
            // Если изменения не были применены — откатываем
            if (!_changesApplied)
            {
                RevertChanges();
            }

            base.Hide(onComplete);
        }

        #region Custom Audio Parameters Registration
        
        /// <summary>
        /// Регистрирует кастомные AudioMixer параметры как секцию в SettingsSystem
        /// </summary>
        private void RegisterCustomAudioParameters()
        {
            // Собираем кастомные параметры (не стандартные Audio)
            var customParams = new List<VolumeSliderData>();
            foreach (var data in volumeSliders)
            {
                if (!IsStandardAudioParameter(data.parameterName))
                {
                    customParams.Add(data);
                }
            }
            
            if (customParams.Count == 0) return;
            
            // Проверяем, есть ли уже такая секция
            _audioMixerSection = _settings.GetSection(AUDIO_MIXER_SECTION) as DynamicSettingsSection;
            
            if (_audioMixerSection == null)
            {
                // Создаём новую секцию
                _audioMixerSection = new DynamicSettingsSection(AUDIO_MIXER_SECTION, "Custom AudioMixer parameters");
                
                foreach (var data in customParams)
                {
                    _audioMixerSection.AddFloat(data.parameterName, $"{data.displayName} volume (0.0 - 1.0)", 0, 1f);
                }
                
                _settings.RegisterSection(_audioMixerSection);
                Debug.Log($"[SettingsWindow] Registered {customParams.Count} custom audio parameters");
            }
        }
        
        /// <summary>
        /// Проверить, является ли параметр стандартным (Audio секция)
        /// </summary>
        private bool IsStandardAudioParameter(string parameterName)
        {
            string lower = parameterName.ToLower();
            return lower.Contains("master") || lower == "volume" ||
                   lower.Contains("music") ||
                   lower.Contains("sfx") || lower.Contains("effect") ||
                   lower.Contains("voice") || lower.Contains("dialog");
        }

        #endregion

        #region Load Settings

        protected virtual void LoadCurrentSettings()
        {
            LoadAudioSettings();
            LoadVideoSettings();
            LoadControlsSettings();
            UpdateAllTexts();
        }
        
        private void LoadAudioSettings()
        {
            foreach (var data in volumeSliders)
            {
                if (data.slider == null) continue;
                
                float savedValue = GetVolumeFromSettings(data.parameterName);
                data.currentValue = savedValue;
                data.slider.SetValueWithoutNotify(savedValue);
                ApplyVolumeToMixer(data.parameterName, savedValue);
            }
        }
        
        /// <summary>
        /// Получить значение громкости из SettingsSystem
        /// </summary>
        private float GetVolumeFromSettings(string parameterName)
        {
            if (_settings == null) return 1f;
            
            // Стандартные параметры → Audio секция
            if (_settings.Audio != null)
            {
                string lower = parameterName.ToLower();
                
                if (lower.Contains("master") || lower == "volume")
                    return _settings.Audio.MasterVolume.Value;
                if (lower.Contains("music"))
                    return _settings.Audio.MusicVolume.Value;
                if (lower.Contains("sfx") || lower.Contains("effect"))
                    return _settings.Audio.SFXVolume.Value;
                if (lower.Contains("voice") || lower.Contains("dialog"))
                    return _settings.Audio.VoiceVolume.Value;
            }
            
            // Кастомные параметры → AudioMixer секция
            if (_audioMixerSection != null)
            {
                var setting = _audioMixerSection.GetSetting(parameterName);
                if (setting != null)
                {
                    return (float)setting.GetValue();
                }
            }
            
            return 1f;
        }
        
        /// <summary>
        /// Установить значение громкости в SettingsSystem
        /// </summary>
        private void SetVolumeToSettings(string parameterName, float value)
        {
            if (_settings == null) return;
            
            // Стандартные параметры → Audio секция
            if (_settings.Audio != null)
            {
                string lower = parameterName.ToLower();
                
                if (lower.Contains("master") || lower == "volume")
                {
                    _settings.Audio.MasterVolume.Value = value;
                    return;
                }
                if (lower.Contains("music"))
                {
                    _settings.Audio.MusicVolume.Value = value;
                    return;
                }
                if (lower.Contains("sfx") || lower.Contains("effect"))
                {
                    _settings.Audio.SFXVolume.Value = value;
                    return;
                }
                if (lower.Contains("voice") || lower.Contains("dialog"))
                {
                    _settings.Audio.VoiceVolume.Value = value;
                    return;
                }
            }
            
            // Кастомные параметры → AudioMixer секция
            if (_audioMixerSection != null)
            {
                var setting = _audioMixerSection.GetSetting(parameterName);
                setting?.SetValue(value);
            }
        }
        
        private void LoadVideoSettings()
        {
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
                
                int quality = _settings?.Video?.Quality ?? QualitySettings.GetQualityLevel();
                qualityDropdown.SetValueWithoutNotify(quality);
            }
            
            if (fullscreenToggle != null)
            {
                bool isFullscreen = _settings?.Video != null 
                    ? _settings.Video.Fullscreen.Value != "Windowed"
                    : Screen.fullScreen;
                SetToggleWithoutNotify(fullscreenToggle, isFullscreen);
            }
            
            if (vsyncToggle != null)
            {
                bool vsync = _settings?.Video?.VSync ?? (QualitySettings.vSyncCount > 0);
                SetToggleWithoutNotify(vsyncToggle, vsync);
            }
        }
        
        private void LoadControlsSettings()
        {
            if (_settings?.Controls == null) return;
            
            float sensitivity = _settings.Controls.Sensitivity.Value;
            bool invertY = _settings.Controls.InvertY.Value;
            
            if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(sensitivity);
            if (invertYToggle != null) SetToggleWithoutNotify(invertYToggle, invertY);
        }

        #endregion

        #region UI Update

        private void UpdateAllTexts()
        {
            UpdateAudioTexts();
            UpdateControlsTexts();
        }
        
        private void UpdateAudioTexts()
        {
            foreach (var data in volumeSliders)
            {
                if (data.valueText != null)
                {
                    data.valueText.text = $"{Mathf.RoundToInt(data.currentValue * 100)}%";
                }
            }
        }
        
        private void UpdateControlsTexts()
        {
            if (_settings?.Controls != null && sensitivityText != null)
            {
                sensitivityText.text = $"{_settings.Controls.Sensitivity.Value:F1}";
            }
        }

        #endregion

        #region Audio Mixer Helpers
        
        private void ApplyVolumeToMixer(string parameterName, float linearValue)
        {
            if (audioMixer == null) return;
            float dbValue = LinearToDecibel(linearValue);
            audioMixer.SetFloat(parameterName, dbValue);
        }
        
        private float LinearToDecibel(float linear)
        {
            if (linear <= 0.0001f) return -80f;
            return Mathf.Log10(linear) * 20f;
        }
        
        private void SetToggleWithoutNotify(Toggle toggle, bool value)
        {
            if (toggle == null) return;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.isOn = value;
            
            if (toggle == fullscreenToggle)
                toggle.onValueChanged.AddListener(OnFullscreenChanged);
            else if (toggle == vsyncToggle)
                toggle.onValueChanged.AddListener(OnVsyncChanged);
            else if (toggle == invertYToggle)
                toggle.onValueChanged.AddListener(OnInvertYChanged);
        }

        #endregion

        #region Event Handlers - Audio

        private void OnVolumeSliderChanged(VolumeSliderData data, float value)
        {
            data.currentValue = value;
            ApplyVolumeToMixer(data.parameterName, value);
            SetVolumeToSettings(data.parameterName, value);
            UpdateAudioTexts();
        }

        #endregion

        #region Event Handlers - Video

        private void OnFullscreenChanged(bool value)
        {
            if (_settings?.Video != null)
            {
                _settings.Video.Fullscreen.Value = value ? "FullScreenWindow" : "Windowed";
            }
        }

        private void OnVsyncChanged(bool value)
        {
            if (_settings?.Video != null)
            {
                _settings.Video.VSync.Value = value;
            }
        }

        private void OnQualityChanged(int index)
        {
            if (_settings?.Video != null)
            {
                _settings.Video.Quality.Value = index;
            }
        }

        private void OnResolutionChanged(int index)
        {
            // TODO: Implement resolution change
        }

        #endregion

        #region Event Handlers - Controls

        private void OnSensitivityChanged(float value)
        {
            if (_settings?.Controls != null)
            {
                _settings.Controls.Sensitivity.Value = value;
            }
            UpdateControlsTexts();
        }

        private void OnInvertYChanged(bool value)
        {
            if (_settings?.Controls != null)
            {
                _settings.Controls.InvertY.Value = value;
            }
        }

        #endregion

        #region Buttons

        protected virtual void OnApplyClicked()
        {
            ApplySettings();
            UISystem.Back();
        }

        protected virtual void OnResetClicked()
        {
            _settings?.ResetAllToDefaults();
            LoadCurrentSettings();
        }

        protected virtual void OnBackClicked()
        {
            // RevertChanges() вызовется автоматически в Hide() т.к. _changesApplied == false
            UISystem.Back();
        }

        /// <summary>
        /// Откатить все изменения и применить к AudioMixer
        /// </summary>
        private void RevertChanges()
        {
            _settings?.RevertAll();

            // Восстанавливаем значения AudioMixer из SettingsSystem
            foreach (var data in volumeSliders)
            {
                float savedValue = GetVolumeFromSettings(data.parameterName);
                data.currentValue = savedValue;
                ApplyVolumeToMixer(data.parameterName, savedValue);
            }
        }

        #endregion

        #region Apply Settings

        protected virtual void ApplySettings()
        {
            // Применяем к AudioMixer
            foreach (var data in volumeSliders)
            {
                ApplyVolumeToMixer(data.parameterName, data.currentValue);
            }

            // Сохраняем всё через SettingsSystem
            if (_settings != null)
            {
                _settings.ApplyAndSave();
                Debug.Log("[SettingsWindow] Settings applied via SettingsSystem");
            }
            else
            {
                Debug.LogWarning("[SettingsWindow] SettingsSystem not available!");
            }

            _changesApplied = true;
        }

        #endregion

        #region Public API
        
        public void AddVolumeSlider(string parameterName, string displayName, Slider slider, TMP_Text valueText)
        {
            volumeSliders.Add(new VolumeSliderData
            {
                parameterName = parameterName,
                displayName = displayName,
                slider = slider,
                valueText = valueText,
                currentValue = 1f
            });
        }
        
        public void SetAudioMixer(AudioMixer mixer)
        {
            audioMixer = mixer;
        }

        #endregion
    }
}
