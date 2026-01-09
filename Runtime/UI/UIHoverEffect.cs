// Packages/com.protosystem.core/Runtime/UI/UIHoverEffect.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Компонент для hover-эффектов UI элементов (как в HTML/CSS)
    /// Осветляет элемент при наведении и затемняет при нажатии
    /// Поддерживает как обычные Image, так и UITwoColorImage
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UIHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Настройки Hover")]
        [Tooltip("Множитель яркости при наведении (1.0 = без изменений)")]
        [Range(1.0f, 1.5f)]
        public float hoverBrightness = 1.15f;
        
        [Tooltip("Множитель яркости при нажатии")]
        [Range(0.7f, 1.0f)]
        public float pressedBrightness = 0.9f;
        
        [Tooltip("Скорость перехода (секунды)")]
        [Range(0.05f, 0.5f)]
        public float transitionDuration = 0.15f;
        
        [Header("Масштабирование (опционально)")]
        [Tooltip("Увеличивать при наведении")]
        public bool scaleOnHover = false;
        
        [Tooltip("Множитель масштаба при наведении")]
        [Range(1.0f, 1.2f)]
        public float hoverScale = 1.05f;
        
        [Header("Альфа для полупрозрачных элементов")]
        [Tooltip("Также увеличивать альфу при hover (для тёмных полупрозрачных элементов)")]
        public bool boostAlphaOnHover = false;
        
        [Tooltip("Целевая альфа при наведении")]
        [Range(0.1f, 1.0f)]
        public float hoverAlpha = 0.3f;

        private Graphic _graphic;
        private Button _button;
        private UITwoColorImage _twoColorImage;
        private Color _originalColor;
        private Color _originalFillColor;
        private Color _originalBorderColor;
        private Vector3 _originalScale;
        private bool _isHovered;
        private bool _isPressed;
        private float _currentBrightness = 1f;
        private float _targetBrightness = 1f;
        private bool _initialized = false;
        private bool _usesTwoColor = false;

        private void Awake()
        {
            _graphic = GetComponent<Graphic>();
            _button = GetComponent<Button>();
            _twoColorImage = GetComponent<UITwoColorImage>();
            _originalScale = transform.localScale;
        }
        
        private void Start()
        {
            // Инициализируем цвет в Start, чтобы Button успел применить свои настройки
            InitializeColor();
            
            // Отключаем ColorTint у Button, т.к. мы управляем цветом сами
            if (_button != null)
            {
                _button.transition = Selectable.Transition.None;
                Debug.Log($"[UIHoverEffect] Disabled Button.ColorTint on {gameObject.name}, using custom hover effect");
            }
        }
        
        private void InitializeColor()
        {
            if (_initialized) return;
            
            // Проверяем есть ли UITwoColorImage
            if (_twoColorImage != null)
            {
                _usesTwoColor = true;
                _originalFillColor = _twoColorImage.FillColor;
                _originalBorderColor = _twoColorImage.BorderColor;
                Debug.Log($"[UIHoverEffect] Initialized (TwoColor) on {gameObject.name}, fill={_originalFillColor}, border={_originalBorderColor}");
            }
            else
            {
                _usesTwoColor = false;
                _originalColor = _graphic.color;
                Debug.Log($"[UIHoverEffect] Initialized on {gameObject.name}, originalColor={_originalColor}");
            }
            
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;
            
            // Плавный переход яркости
            if (!Mathf.Approximately(_currentBrightness, _targetBrightness))
            {
                _currentBrightness = Mathf.MoveTowards(_currentBrightness, _targetBrightness, 
                    Time.unscaledDeltaTime / transitionDuration);
                
                ApplyBrightness();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_initialized) InitializeColor();
            
            _isHovered = true;
            UpdateTargetBrightness();
            
            if (scaleOnHover)
            {
                LeanTweenScale(hoverScale);
            }
            
            Debug.Log($"[UIHoverEffect] Hover ENTER on {gameObject.name}, targetBrightness={_targetBrightness}");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            _isPressed = false;
            UpdateTargetBrightness();
            
            if (scaleOnHover)
            {
                LeanTweenScale(1f);
            }
            
            Debug.Log($"[UIHoverEffect] Hover EXIT on {gameObject.name}");
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            UpdateTargetBrightness();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            UpdateTargetBrightness();
        }

        private void UpdateTargetBrightness()
        {
            if (_isPressed)
            {
                _targetBrightness = pressedBrightness;
            }
            else if (_isHovered)
            {
                _targetBrightness = hoverBrightness;
            }
            else
            {
                _targetBrightness = 1f;
            }
        }

        private void ApplyBrightness()
        {
            if (_usesTwoColor && _twoColorImage != null)
            {
                // Режим UITwoColorImage - меняем FillColor
                float targetAlpha = _originalFillColor.a;
                if (boostAlphaOnHover && _isHovered && !_isPressed)
                {
                    float hoverProgress = Mathf.Clamp01((_currentBrightness - 1f) / (hoverBrightness - 1f));
                    targetAlpha = Mathf.Lerp(_originalFillColor.a, hoverAlpha, hoverProgress);
                }
                
                Color newFillColor = new Color(
                    Mathf.Clamp01(_originalFillColor.r * _currentBrightness),
                    Mathf.Clamp01(_originalFillColor.g * _currentBrightness),
                    Mathf.Clamp01(_originalFillColor.b * _currentBrightness),
                    targetAlpha
                );
                
                // Рамку тоже немного осветляем
                Color newBorderColor = new Color(
                    Mathf.Clamp01(_originalBorderColor.r * Mathf.Lerp(1f, _currentBrightness, 0.5f)),
                    Mathf.Clamp01(_originalBorderColor.g * Mathf.Lerp(1f, _currentBrightness, 0.5f)),
                    Mathf.Clamp01(_originalBorderColor.b * Mathf.Lerp(1f, _currentBrightness, 0.5f)),
                    _originalBorderColor.a
                );
                
                _twoColorImage.FillColor = newFillColor;
                _twoColorImage.BorderColor = newBorderColor;
            }
            else if (_graphic != null)
            {
                // Стандартный режим - меняем Image.color
                float targetAlpha = _originalColor.a;
                if (boostAlphaOnHover && _isHovered && !_isPressed)
                {
                    float hoverProgress = Mathf.Clamp01((_currentBrightness - 1f) / (hoverBrightness - 1f));
                    targetAlpha = Mathf.Lerp(_originalColor.a, hoverAlpha, hoverProgress);
                }
                
                Color newColor = new Color(
                    Mathf.Clamp01(_originalColor.r * _currentBrightness),
                    Mathf.Clamp01(_originalColor.g * _currentBrightness),
                    Mathf.Clamp01(_originalColor.b * _currentBrightness),
                    targetAlpha
                );
                
                _graphic.color = newColor;
            }
        }

        private void LeanTweenScale(float targetScale)
        {
            // Простая анимация масштаба без LeanTween
            StopAllCoroutines();
            StartCoroutine(ScaleCoroutine(targetScale));
        }

        private System.Collections.IEnumerator ScaleCoroutine(float targetScale)
        {
            Vector3 target = _originalScale * targetScale;
            float elapsed = 0f;
            Vector3 start = transform.localScale;
            
            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / transitionDuration;
                transform.localScale = Vector3.Lerp(start, target, t);
                yield return null;
            }
            
            transform.localScale = target;
        }

        /// <summary>
        /// Обновляет базовый цвет (вызывать если цвет изменился извне)
        /// </summary>
        public void RefreshOriginalColor()
        {
            if (_usesTwoColor && _twoColorImage != null)
            {
                _originalFillColor = _twoColorImage.FillColor;
                _originalBorderColor = _twoColorImage.BorderColor;
                Debug.Log($"[UIHoverEffect] RefreshOriginalColor (TwoColor) on {gameObject.name}, fill={_originalFillColor}");
            }
            else if (_graphic != null)
            {
                _originalColor = _graphic.color;
                Debug.Log($"[UIHoverEffect] RefreshOriginalColor on {gameObject.name}, newColor={_originalColor}");
            }
            _initialized = true;
        }

        private void OnDisable()
        {
            // Сброс к оригинальному состоянию
            if (_usesTwoColor && _twoColorImage != null && _initialized)
            {
                _twoColorImage.FillColor = _originalFillColor;
                _twoColorImage.BorderColor = _originalBorderColor;
            }
            else if (_graphic != null && _initialized)
            {
                _graphic.color = _originalColor;
            }
            transform.localScale = _originalScale;
            _currentBrightness = 1f;
            _targetBrightness = 1f;
            _isHovered = false;
            _isPressed = false;
        }
    }
}
