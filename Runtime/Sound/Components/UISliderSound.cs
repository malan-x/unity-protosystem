// Packages/com.protosystem.core/Runtime/Sound/Components/UISliderSound.cs

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Воспроизводит звук при изменении значения Slider.
    /// Включает cooldown для предотвращения спама при плавном перетаскивании.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    [AddComponentMenu("ProtoSystem/Sound/UI Slider Sound")]
    public class UISliderSound : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Sound ID")]
        [Tooltip("Звук при изменении значения")]
        [SoundId] public string sliderSound = "ui_slider";
        
        [Header("Settings")]
        [Tooltip("Громкость воспроизведения")]
        [Range(0f, 1f)] public float volume = 1f;
        
        [Tooltip("Минимальный интервал между звуками (секунды)")]
        [Range(0.01f, 0.5f)] public float cooldown = 0.05f;
        
        [Tooltip("Минимальное изменение значения для воспроизведения звука")]
        [Range(0.001f, 0.1f)] public float minValueChange = 0.01f;
        
        [Tooltip("Воспроизводить звук только при перетаскивании пользователем")]
        public bool onlyWhileDragging = true;
        
        private Slider _slider;
        private float _lastPlayTime;
        private float _lastValue;
        private bool _isDragging;
        
        private void Awake()
        {
            _slider = GetComponent<Slider>();
            _lastValue = _slider.value;
        }
        
        private void OnEnable()
        {
            if (_slider != null)
            {
                _slider.onValueChanged.AddListener(OnValueChanged);
            }
        }
        
        private void OnDisable()
        {
            if (_slider != null)
            {
                _slider.onValueChanged.RemoveListener(OnValueChanged);
            }
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            _isDragging = true;
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            _isDragging = false;
        }
        
        private void OnValueChanged(float value)
        {
            // Проверяем режим только при перетаскивании
            if (onlyWhileDragging && !_isDragging)
            {
                _lastValue = value;
                return;
            }

            // Проверяем минимальное изменение значения
            float delta = Mathf.Abs(value - _lastValue);
            if (delta < minValueChange)
            {
                return;
            }

            // Проверяем cooldown
            float currentTime = Time.unscaledTime;
            if (currentTime - _lastPlayTime < cooldown)
            {
                return;
            }

            // Воспроизводим звук
            if (!string.IsNullOrEmpty(sliderSound))
            {
                SoundManagerSystem.Play(sliderSound, volumeMultiplier: volume);
            }

            _lastPlayTime = currentTime;
            _lastValue = value;
        }
        
        /// <summary>
        /// Устанавливает значение слайдера с воспроизведением звука.
        /// </summary>
        public void SetValueWithSound(float value)
        {
            bool wasDragging = _isDragging;
            _isDragging = true;
            _slider.value = value;
            _isDragging = wasDragging;
        }
        
        /// <summary>
        /// Устанавливает значение слайдера без воспроизведения звука.
        /// </summary>
        public void SetValueSilent(float value)
        {
            _slider.SetValueWithoutNotify(value);
            _lastValue = value;
        }
    }
}
