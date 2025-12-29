// Packages/com.protosystem.core/Runtime/UI/Components/ProgressComponent.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Компонент прогресс-бара.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ProgressComponent : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text percentText;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;

        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.3f;
        [SerializeField] private float fillSmoothing = 5f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private CanvasGroup _canvasGroup;
        private RectTransform _fillRect;
        private float _targetProgress;
        private float _currentProgress;
        private Coroutine _animationCoroutine;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            
            if (fillImage != null)
                _fillRect = fillImage.GetComponent<RectTransform>();
            
            _canvasGroup.alpha = 0;
            SetProgressImmediate(0);
        }

        private void Update()
        {
            if (Mathf.Abs(_currentProgress - _targetProgress) > 0.001f)
            {
                _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress, Time.unscaledDeltaTime * fillSmoothing);
                UpdateFillVisual();
            }
        }

        public void Setup(string message, float initialProgress = 0f)
        {
            if (messageText != null)
                messageText.text = message ?? "Loading...";
            
            SetProgressImmediate(initialProgress);
        }

        public void SetMessage(string message)
        {
            if (messageText != null)
                messageText.text = message;
        }

        public void SetProgress(float progress)
        {
            _targetProgress = Mathf.Clamp01(progress);
        }

        public void SetProgressImmediate(float progress)
        {
            _targetProgress = Mathf.Clamp01(progress);
            _currentProgress = _targetProgress;
            UpdateFillVisual();
        }

        private void UpdateFillVisual()
        {
            if (_fillRect != null)
            {
                _fillRect.anchorMax = new Vector2(_currentProgress, 1f);
            }

            if (percentText != null)
            {
                percentText.text = $"{Mathf.RoundToInt(_currentProgress * 100)}%";
            }
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
                float t = animationCurve.Evaluate(elapsed / animationDuration);
                _canvasGroup.alpha = t;
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
                float t = animationCurve.Evaluate(elapsed / animationDuration);
                _canvasGroup.alpha = 1f - t;
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _animationCoroutine = null;
            onComplete?.Invoke();
        }
    }
}
