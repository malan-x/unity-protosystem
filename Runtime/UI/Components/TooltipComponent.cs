// Packages/com.protosystem.core/Runtime/UI/Components/TooltipComponent.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Компонент Tooltip.
    /// Реализует ITooltip для использования с TooltipBuilder.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class TooltipComponent : MonoBehaviour, ITooltip
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text contentText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private LayoutGroup layoutGroup;

        [Header("Style Colors")]
        [SerializeField] private Color defaultColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        [SerializeField] private Color infoColor = new Color(0.1f, 0.3f, 0.5f, 0.95f);
        [SerializeField] private Color warningColor = new Color(0.5f, 0.4f, 0.1f, 0.95f);
        [SerializeField] private Color errorColor = new Color(0.5f, 0.15f, 0.15f, 0.95f);

        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.15f;

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private ContentSizeFitter _sizeFitter;
        private Coroutine _animationCoroutine;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
            _sizeFitter = GetComponent<ContentSizeFitter>();
            
            _canvasGroup.alpha = 0;
            _canvasGroup.blocksRaycasts = false;
        }

        public void Setup(TooltipConfig config)
        {
            // Title
            if (titleText != null)
            {
                if (!string.IsNullOrEmpty(config.Title))
                {
                    titleText.text = config.Title;
                    titleText.gameObject.SetActive(true);
                }
                else
                {
                    titleText.gameObject.SetActive(false);
                }
            }

            // Content
            if (contentText != null)
                contentText.text = config.Text ?? "";

            // Icon
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

            // Style
            if (backgroundImage != null)
            {
                backgroundImage.color = config.Style switch
                {
                    TooltipStyle.Info => infoColor,
                    TooltipStyle.Warning => warningColor,
                    TooltipStyle.Error => errorColor,
                    _ => defaultColor
                };
            }

            // Пересчитываем размер
            if (layoutGroup != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
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
            
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = elapsed / animationDuration;
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            _animationCoroutine = null;
        }

        private IEnumerator AnimateOut(Action onComplete)
        {
            float elapsed = 0f;
            
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - (elapsed / animationDuration);
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _animationCoroutine = null;
            onComplete?.Invoke();
        }
    }
}
