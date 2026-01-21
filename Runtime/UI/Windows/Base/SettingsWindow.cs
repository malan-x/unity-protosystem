// Packages/com.protosystem.core/Runtime/UI/Windows/Base/SettingsWindow.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Данные слайдера громкости для динамической генерации
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
    /// Окно настроек с динамической поддержкой AudioMixer
    /// </summary>
    [UIWindow("Settings", WindowType.Normal, WindowLayer.Windows, Level = 1, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
    public class SettingsWindow : UIWindowBase
    {
        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer audioMixer;
        
        [Header("Volume Sliders (Auto-generated)")]
        [SerializeField] private List<VolumeSliderData> volumeSliders = new List<VolumeSliderData>();
        
        [Header("Legacy Audio (fallback if no mixer)")]
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
        
        // Режим работы
        private bool _useDynamicAudio = false;

        protected override void Awake()
        {
            base.Awake();
            
            // Определяем режим работы
            _useDynamicAudio = audioMixer != null && volumeSliders.Count > 0;
            
            if (_useDynamicAudio)
            {
                // Динамические слайдеры из AudioMixer
                foreach (var data in volumeSliders)
                {
                    if (data.slider != null)
                    {
                        // Захватываем data в замыкание правильно
                        var captured = data;
                        data.slider.onValueChanged.AddListener(value => OnVolumeSliderChanged(captured, value));
                    }
                }
            }
            else
            {
                // Legacy слайдеры
                masterVolumeSlider?.onValueChanged.AddListener(OnMasterVolumeChanged);
                musicVolumeSlider?.onValueChanged.AddListener(OnMusicVolumeChanged);
                sfxVolumeSlider?.onValueChanged.AddListener(OnSfxVolumeChanged);
            }
            
            // Остальные контролы
            sensitivitySlider?.onValueChanged.AddListener(OnSensitivityChanged);
            fullscreenToggle?.onValueChanged.AddListener(OnFullscreenChanged);
            vsyncToggle?.onValueChanged.AddListener(OnVsyncChanged);
            invertYToggle?.onValueChanged.AddListener(OnInvertYChanged);
            qualityDropdown?.onValueChanged.AddListener(OnQualityChanged);
            resolutionDropdown?.onValueChanged.AddListener(OnResolutionChanged);
            
            // Кнопки
            applyButton?.onClick.AddListener(OnApplyClicked);
            resetButton?.onClick.AddListener(OnResetClicked);
            backButton?.onClick.AddListener(OnBackClicked);
        }

        public override void Show(Action onComplete = null)
        {
            base.Show(onComplete);
            LoadCurrentSettings();
        }

        protected virtual void LoadCurrentSettings()
        {
            if (_useDynamicAudio)
            {
                LoadDynamicAudioSettings();
            }
            else
            {
                LoadLegacyAudioSettings();
            }
            
            // Graphics
            _qualityLevel = QualitySettings.GetQualityLevel();
            _fullscreen = Screen.fullScreen;
            _vsync = QualitySettings.vSyncCount > 0;
            
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
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
        
        private void LoadDynamicAudioSettings()
        {
            foreach (var data in volumeSliders)
            {
                if (data.slider == null) continue;
                
                // Загружаем из PlayerPrefs
                float savedValue = PlayerPrefs.GetFloat($"Volume_{data.parameterName}", 1f);
                data.currentValue = savedValue;
                data.slider.SetValueWithoutNotify(savedValue);
                
                // Применяем к миксеру
                ApplyVolumeToMixer(data.parameterName, savedValue);
                
                // Обновляем текст
                UpdateVolumeText(data);
            }
        }
        
        private void LoadLegacyAudioSettings()
        {
            _masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            _musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
            _sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1f);
            
            if (masterVolumeSlider != null) masterVolumeSlider.value = _masterVolume;
            if (musicVolumeSlider != null) musicVolumeSlider.value = _musicVolume;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = _sfxVolume;
        }

        private void UpdateTexts()
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
                if (masterVolumeText != null) masterVolumeText.text = $"{Mathf.RoundToInt(_masterVolume * 100)}%";
                if (musicVolumeText != null) musicVolumeText.text = $"{Mathf.RoundToInt(_musicVolume * 100)}%";
                if (sfxVolumeText != null) sfxVolumeText.text = $"{Mathf.RoundToInt(_sfxVolume * 100)}%";
            }
            
            if (sensitivityText != null) sensitivityText.text = $"{_sensitivity:F1}";
        }
        
        private void UpdateVolumeText(VolumeSliderData data)
        {
            if (data.valueText != null)
            {
                data.valueText.text = $"{Mathf.RoundToInt(data.currentValue * 100)}%";
            }
        }
        
        /// <summary>
        /// Применить линейное значение громкости к AudioMixer
        /// </summary>
        private void ApplyVolumeToMixer(string parameterName, float linearValue)
        {
            if (audioMixer == null) return;
            
            // Конвертируем линейное значение (0-1) в децибелы (-80 до 0)
            float dbValue = LinearToDecibel(linearValue);
            audioMixer.SetFloat(parameterName, dbValue);
        }
        
        /// <summary>
        /// Конвертировать линейную громкость (0-1) в децибелы
        /// </summary>
        private float LinearToDecibel(float linear)
        {
            if (linear <= 0.0001f) return -80f;
            return Mathf.Log10(linear) * 20f;
        }

        #region Event Handlers

        private void OnVolumeSliderChanged(VolumeSliderData data, float value)
        {
            data.currentValue = value;
            ApplyVolumeToMixer(data.parameterName, value);
            UpdateVolumeText(data);
        }

        private void OnMasterVolumeChanged(float value)
        {
            _masterVolume = value;
            UpdateTexts();
            AudioListener.volume = value;
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
            if (_useDynamicAudio)
            {
                foreach (var data in volumeSliders)
                {
                    data.currentValue = 1f;
                    if (data.slider != null) data.slider.value = 1f;
                    ApplyVolumeToMixer(data.parameterName, 1f);
                }
            }
            else
            {
                _masterVolume = 1f;
                _musicVolume = 1f;
                _sfxVolume = 1f;
                if (masterVolumeSlider != null) masterVolumeSlider.value = _masterVolume;
                if (musicVolumeSlider != null) musicVolumeSlider.value = _musicVolume;
                if (sfxVolumeSlider != null) sfxVolumeSlider.value = _sfxVolume;
            }
            
            _sensitivity = 1f;
            _invertY = false;
            _fullscreen = true;
            _vsync = true;
            _qualityLevel = QualitySettings.names.Length - 1;
            
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
            if (_useDynamicAudio)
            {
                foreach (var data in volumeSliders)
                {
                    PlayerPrefs.SetFloat($"Volume_{data.parameterName}", data.currentValue);
                    ApplyVolumeToMixer(data.parameterName, data.currentValue);
                }
            }
            else
            {
                PlayerPrefs.SetFloat("MasterVolume", _masterVolume);
                PlayerPrefs.SetFloat("MusicVolume", _musicVolume);
                PlayerPrefs.SetFloat("SfxVolume", _sfxVolume);
                AudioListener.volume = _masterVolume;
            }
            
            // Graphics
            QualitySettings.SetQualityLevel(_qualityLevel);
            Screen.fullScreen = _fullscreen;
            QualitySettings.vSyncCount = _vsync ? 1 : 0;
            
            // Gameplay
            PlayerPrefs.SetFloat("Sensitivity", _sensitivity);
            PlayerPrefs.SetInt("InvertY", _invertY ? 1 : 0);
            
            PlayerPrefs.Save();
            
            Debug.Log("[SettingsWindow] Settings applied and saved");
        }
        
        /// <summary>
        /// Программное добавление слайдера громкости (для генератора)
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
        /// Установить AudioMixer (для генератора)
        /// </summary>
        public void SetAudioMixer(AudioMixer mixer)
        {
            audioMixer = mixer;
        }
    }
}
