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
        [Header("Audio Mixer (для динамических слайдеров)")]
        [SerializeField] private AudioMixer audioMixer;
        
        [Header("Volume Sliders (Auto-generated)")]
        [SerializeField] private List<VolumeSliderData> volumeSliders = new List<VolumeSliderData>();
        
        [Header("Audio (стандартные)")]
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

        // SettingsSystem reference
        private SettingsSystem _settings;
        
        // Режим работы аудио
        private bool _useDynamicAudio = false;

        protected override void Awake()
        {
            base.Awake();
            
            // Определяем режим работы аудио
            _useDynamicAudio = audioMixer != null && volumeSliders.Count > 0;
            
            SetupAudioListeners();
            SetupGraphicsListeners();
            SetupGameplayListeners();
            SetupButtons();
        }

        private void SetupAudioListeners()
        {
            if (_useDynamicAudio)
            {
                // Динамические слайдеры из AudioMixer
                foreach (var data in volumeSliders)
                {
                    if (data.slider != null)
                    {
                        var captured = data;
                        data.slider.onValueChanged.AddListener(value => OnDynamicVolumeChanged(captured, value));
                    }
                }
            }
            else
            {
                // Стандартные слайдеры → SettingsSystem
                masterVolumeSlider?.onValueChanged.AddListener(OnMasterVolumeChanged);
                musicVolumeSlider?.onValueChanged.AddListener(OnMusicVolumeChanged);
                sfxVolumeSlider?.onValueChanged.AddListener(OnSfxVolumeChanged);
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
            
            // Получаем SettingsSystem
            _settings = SettingsSystem.Instance;
            
            if (_settings == null)
            {
                Debug.LogWarning("[SettingsWindow] SettingsSystem not found! Settings will not be saved.");
            }
            
            LoadCurrentSettings();
        }

        #region Load Settings

        protected virtual void LoadCurrentSettings()
        {
            LoadAudioSettings();
            LoadVideoSettings();
            LoadControlsSettings();
            LoadGameplaySettings();
            UpdateAllTexts();
        }
        
        private void LoadAudioSettings()
        {
            if (_useDynamicAudio)
            {
                LoadDynamicAudioSettings();
            }
            else
            {
                LoadStandardAudioSettings();
            }
        }
        
        private void LoadDynamicAudioSettings()
        {
            // Динамический режим: AudioMixer параметры хранятся в PlayerPrefs
            foreach (var data in volumeSliders)
            {
                if (data.slider == null) continue;
                
                float savedValue = PlayerPrefs.GetFloat($"Volume_{data.parameterName}", 1f);
                data.currentValue = savedValue;
                data.slider.SetValueWithoutNotify(savedValue);
                ApplyVolumeToMixer(data.parameterName, savedValue);
                UpdateVolumeText(data);
            }
        }
        
        private void LoadStandardAudioSettings()
        {
            if (_settings?.Audio == null) return;
            
            float master = _settings.Audio.MasterVolume;
            float music = _settings.Audio.MusicVolume;
            float sfx = _settings.Audio.SFXVolume;
            
            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(master);
            if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(music);
            if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(sfx);
        }
        
        private void LoadVideoSettings()
        {
            // Quality dropdown
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
                
                int quality = _settings?.Video?.Quality ?? QualitySettings.GetQualityLevel();
                qualityDropdown.SetValueWithoutNotify(quality);
            }
            
            // Fullscreen
            if (fullscreenToggle != null)
            {
                bool isFullscreen = _settings?.Video != null 
                    ? _settings.Video.Fullscreen.Value != "Windowed"
                    : Screen.fullScreen;
                SetToggleWithoutNotify(fullscreenToggle, isFullscreen);
            }
            
            // VSync
            if (vsyncToggle != null)
            {
                bool vsync = _settings?.Video?.VSync ?? (QualitySettings.vSyncCount > 0);
                SetToggleWithoutNotify(vsyncToggle, vsync);
            }
            
            // TODO: Resolution dropdown
        }
        
        private void LoadControlsSettings()
        {
            if (_settings?.Controls == null) return;
            
            float sensitivity = _settings.Controls.Sensitivity;
            bool invertY = _settings.Controls.InvertY;
            
            if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(sensitivity);
            if (invertYToggle != null) SetToggleWithoutNotify(invertYToggle, invertY);
        }
        
        private void LoadGameplaySettings()
        {
            // Добавить загрузку Gameplay настроек если нужны UI элементы
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
            if (_useDynamicAudio)
            {
                foreach (var data in volumeSliders)
                {
                    UpdateVolumeText(data);
                }
            }
            else
            {
                if (_settings?.Audio != null)
                {
                    if (masterVolumeText != null) 
                        masterVolumeText.text = $"{Mathf.RoundToInt(_settings.Audio.MasterVolume * 100)}%";
                    if (musicVolumeText != null) 
                        musicVolumeText.text = $"{Mathf.RoundToInt(_settings.Audio.MusicVolume * 100)}%";
                    if (sfxVolumeText != null) 
                        sfxVolumeText.text = $"{Mathf.RoundToInt(_settings.Audio.SFXVolume * 100)}%";
                }
            }
        }
        
        private void UpdateControlsTexts()
        {
            if (_settings?.Controls != null && sensitivityText != null)
            {
                sensitivityText.text = $"{_settings.Controls.Sensitivity:F1}";
            }
        }
        
        private void UpdateVolumeText(VolumeSliderData data)
        {
            if (data.valueText != null)
            {
                data.valueText.text = $"{Mathf.RoundToInt(data.currentValue * 100)}%";
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
        
        /// <summary>
        /// Установить значение Toggle без вызова события onValueChanged
        /// </summary>
        private void SetToggleWithoutNotify(Toggle toggle, bool value)
        {
            if (toggle == null) return;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.isOn = value;
            
            // Восстанавливаем listener
            if (toggle == fullscreenToggle)
                toggle.onValueChanged.AddListener(OnFullscreenChanged);
            else if (toggle == vsyncToggle)
                toggle.onValueChanged.AddListener(OnVsyncChanged);
            else if (toggle == invertYToggle)
                toggle.onValueChanged.AddListener(OnInvertYChanged);
        }

        #endregion

        #region Event Handlers - Audio

        private void OnDynamicVolumeChanged(VolumeSliderData data, float value)
        {
            data.currentValue = value;
            ApplyVolumeToMixer(data.parameterName, value);
            UpdateVolumeText(data);
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (_settings?.Audio != null)
            {
                _settings.Audio.MasterVolume.Value = value;
            }
            UpdateAudioTexts();
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (_settings?.Audio != null)
            {
                _settings.Audio.MusicVolume.Value = value;
            }
            UpdateAudioTexts();
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (_settings?.Audio != null)
            {
                _settings.Audio.SFXVolume.Value = value;
            }
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
            // TODO: Implement resolution change via SettingsSystem
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
            // Сбрасываем через SettingsSystem
            _settings?.ResetAllToDefaults();
            
            // Для динамического аудио — сбрасываем вручную
            if (_useDynamicAudio)
            {
                foreach (var data in volumeSliders)
                {
                    data.currentValue = 1f;
                    if (data.slider != null) data.slider.SetValueWithoutNotify(1f);
                    ApplyVolumeToMixer(data.parameterName, 1f);
                }
            }
            
            // Перезагружаем UI
            LoadCurrentSettings();
        }

        protected virtual void OnBackClicked()
        {
            // Откатываем несохранённые изменения
            _settings?.RevertAll();
            UISystem.Back();
        }

        #endregion

        #region Apply Settings

        protected virtual void ApplySettings()
        {
            // Динамическое аудио (AudioMixer) — сохраняем отдельно в PlayerPrefs
            if (_useDynamicAudio)
            {
                foreach (var data in volumeSliders)
                {
                    PlayerPrefs.SetFloat($"Volume_{data.parameterName}", data.currentValue);
                    ApplyVolumeToMixer(data.parameterName, data.currentValue);
                }
                PlayerPrefs.Save();
            }
            
            // Всё остальное — через SettingsSystem
            if (_settings != null)
            {
                _settings.ApplyAndSave();
                Debug.Log("[SettingsWindow] Settings applied via SettingsSystem");
            }
            else
            {
                Debug.LogWarning("[SettingsWindow] SettingsSystem not available, settings not saved!");
            }
        }

        #endregion

        #region Public API (для генератора)
        
        /// <summary>
        /// Программное добавление слайдера громкости
        /// </summary>
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
        
        /// <summary>
        /// Установить AudioMixer
        /// </summary>
        public void SetAudioMixer(AudioMixer mixer)
        {
            audioMixer = mixer;
        }

        #endregion
    }
}
