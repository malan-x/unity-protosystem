// Packages/com.protosystem.core/Runtime/UI/Components/ToastComponent.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Компонент Toast уведомления.
    /// Реализует IToast для использования с ToastBuilder.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastComponent : MonoBehaviour, IToast
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Button clickButton;

        [Header("Type Colors")]
        [SerializeField] private Color infoColor = new Color(0.2f, 0.6f, 0.9f);
        [SerializeField] private Color successColor = new Color(0.2f, 0.8f, 0.4f);
        [SerializeField] private Color warningColor = new Color(0.9f, 0.7f, 0.2f);
        [SerializeField] private Color errorColor = new Color(0.9f, 0.3f, 0.3f);

        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.3f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private Action _onClick;
        private Coroutine _animationCoroutine;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
            
            if (clickButton != null)
                clickButton.onClick.AddListener(OnClicked);
            
            _canvasGroup.alpha = 0;
        }

        private void OnDestroy()
        {
            if (clickButton != null)
                clickButton.onClick.RemoveListener(OnClicked);
        }

        public void Setup(ToastConfig config)
        {
            if (messageText != null)
                messageText.text = config.Message ?? "";

            if (iconImage != null)
            {
                if (config.Icon != null)
                {
                    iconImage.sprite = config.Icon;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = config.Type switch
                {
                    ToastType.Success => successColor,
                    ToastType.Warning => warningColor,
                    ToastType.Error => errorColor,
                    _ => infoColor
                };
            }

            _onClick = config.OnClick;
        }

        public void Show()
        {
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            
            _animationCoroutine = StartCoroutine(AnimateIn());
        }

        public void Hide(Action onComplete = null)
        {
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            
            _animationCoroutine = StartCoroutine(AnimateOut(onComplete));
        }

        private IEnumerator AnimateIn()
        {
            float elapsed = 0f;
            Vector2 startPos = _rectTransform.anchoredPosition + new Vector2(0, -50);
            Vector2 endPos = _rectTransform.anchoredPosition;

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = animationCurve.Evaluate(elapsed / animationDuration);
                
                _canvasGroup.alpha = t;
                _rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            _rectTransform.anchoredPosition = endPos;
            _animationCoroutine = null;
        }

        private IEnumerator AnimateOut(Action onComplete)
        {
            float elapsed = 0f;
            Vector2 startPos = _rectTransform.anchoredPosition;
            Vector2 endPos = startPos + new Vector2(0, -50);

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = animationCurve.Evaluate(elapsed / animationDuration);
                
                _canvasGroup.alpha = 1f - t;
                _rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _animationCoroutine = null;
            onComplete?.Invoke();
        }

        private void OnClicked()
        {
            _onClick?.Invoke();
        }
    }
}
