// Packages/com.protosystem.core/Runtime/Sound/Components/UIToggleSound.cs

using UnityEngine;
using UnityEngine.UI;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Воспроизводит разные звуки при включении и выключении Toggle.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    [AddComponentMenu("ProtoSystem/Sound/UI Toggle Sound")]
    public class UIToggleSound : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        [Header("Sound IDs")]
        [Tooltip("Звук при включении (isOn = true)")]
        [SoundId] public string toggleOnSound = "ui_toggle_on";
        
        [Tooltip("Звук при выключении (isOn = false)")]
        [SoundId] public string toggleOffSound = "ui_toggle_off";
        
        [Header("Settings")]
        [Tooltip("Громкость воспроизведения")]
        [Range(0f, 1f)] public float volume = 1f;
        
        [Tooltip("Воспроизводить звук только при пользовательском взаимодействии")]
        public bool userInteractionOnly = true;
        
        private Toggle _toggle;
        private bool _isUserInteraction;
        
        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
        }
        
        private void OnEnable()
        {
            if (_toggle != null)
            {
                _toggle.onValueChanged.AddListener(OnValueChanged);
            }
        }
        
        private void OnDisable()
        {
            if (_toggle != null)
            {
                _toggle.onValueChanged.RemoveListener(OnValueChanged);
            }
        }
        
        /// <summary>
        /// Перехватываем клик для установки флага взаимодействия.
        /// </summary>
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _isUserInteraction = true;
        }
        
        /// <summary>
        /// Вызывается при изменении значения Toggle.
        /// </summary>
        /// <summary>
        /// Вызывается при изменении значения Toggle.
        /// </summary>
        private void OnValueChanged(bool isOn)
        {
            // Если включён режим userInteractionOnly, проверяем что это было взаимодействие пользователя
            if (userInteractionOnly && !_isUserInteraction)
            {
                return;
            }

            string soundId = isOn ? toggleOnSound : toggleOffSound;

            if (!string.IsNullOrEmpty(soundId))
            {
                SoundManagerSystem.Play(soundId, volumeMultiplier: volume);
            }

            _isUserInteraction = false;
        }
        
        /// <summary>
        /// Отмечает что следующее изменение — от пользователя.
        /// Вызывается из EventTrigger или вручную перед SetIsOnWithoutNotify + manual notify.
        /// </summary>
        public void MarkUserInteraction()
        {
            _isUserInteraction = true;
        }
        
        /// <summary>
        /// Устанавливает значение Toggle с воспроизведением звука.
        /// </summary>
        public void SetValueWithSound(bool value)
        {
            _isUserInteraction = true;
            _toggle.isOn = value;
        }
        
        /// <summary>
        /// Устанавливает значение Toggle без воспроизведения звука.
        /// </summary>
        public void SetValueSilent(bool value)
        {
            _toggle.SetIsOnWithoutNotify(value);
        }
    }
}
