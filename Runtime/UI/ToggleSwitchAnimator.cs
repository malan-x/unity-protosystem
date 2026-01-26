// Packages/com.protosystem.core/Runtime/UI/ToggleSwitchAnimator.cs
using UnityEngine;
using UnityEngine.UI;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Анимирует перемещение ручки toggle switch при переключении
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public class ToggleSwitchAnimator : MonoBehaviour
    {
        [Header("Handle Settings")]
        [Tooltip("Transform ручки которую нужно двигать")]
        public RectTransform handleTransform;
        
        [Tooltip("Позиция X когда toggle выключен")]
        public float offPositionX = 3f;
        
        [Tooltip("Позиция X когда toggle включен")]
        public float onPositionX = 25f;
        
        [Tooltip("Длительность анимации")]
        public float animationDuration = 0.15f;

        private Toggle _toggle;
        private float _targetX;
        private float _currentX;
        private bool _isAnimating;

        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
            
            if (handleTransform == null)
            {
                ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Warnings, "ToggleSwitchAnimator: handleTransform is not assigned!");
                return;
            }
            
            // Инициализируем позицию
            _currentX = _toggle.isOn ? onPositionX : offPositionX;
            _targetX = _currentX;
            UpdateHandlePosition(_currentX);
        }

        private void OnEnable()
        {
            if (_toggle != null)
            {
                _toggle.onValueChanged.AddListener(OnToggleValueChanged);
            }
        }

        private void OnDisable()
        {
            if (_toggle != null)
            {
                _toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
            }
        }

        private void OnToggleValueChanged(bool isOn)
        {
            if (handleTransform == null) return;
            
            _targetX = isOn ? onPositionX : offPositionX;
            _isAnimating = true;
            
            ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Info, $"Toggle changed to {isOn}, moving handle to X={_targetX}");
        }

        private void Update()
        {
            if (!_isAnimating || handleTransform == null) return;
            
            // Плавная анимация
            _currentX = Mathf.MoveTowards(_currentX, _targetX, 
                (Mathf.Abs(onPositionX - offPositionX) / animationDuration) * Time.unscaledDeltaTime);
            
            UpdateHandlePosition(_currentX);
            
            if (Mathf.Approximately(_currentX, _targetX))
            {
                _isAnimating = false;
            }
        }

        private void UpdateHandlePosition(float x)
        {
            var pos = handleTransform.anchoredPosition;
            pos.x = x;
            handleTransform.anchoredPosition = pos;
        }

        /// <summary>
        /// Мгновенно установить позицию без анимации (для инициализации)
        /// </summary>
        public void SetPositionImmediate(bool isOn)
        {
            if (handleTransform == null) return;
            
            _currentX = isOn ? onPositionX : offPositionX;
            _targetX = _currentX;
            _isAnimating = false;
            UpdateHandlePosition(_currentX);
        }
    }
}
