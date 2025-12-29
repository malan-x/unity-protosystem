// Packages/com.protosystem.core/Runtime/UI/Components/ModalOverlayComponent.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Компонент затемнения для модальных окон.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(Image))]
    public class ModalOverlayComponent : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Color overlayColor = new Color(0, 0, 0, 0.5f);
        [SerializeField] private bool closeOnClick = true;

        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.2f;

        private CanvasGroup _canvasGroup;
        private Image _image;
        private Button _button;
        private Action _onClose;
        private Coroutine _animationCoroutine;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _image = GetComponent<Image>();
            _button = GetComponent<Button>();
            
            _image.color = overlayColor;
            
            if (_button != null)
                _button.onClick.AddListener(OnClicked);
            
            _canvasGroup.alpha = 0;
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnClicked);
        }

        public void Setup(Color? color = null, bool? closeOnClick = null, Action onClose = null)
        {
            if (color.HasValue)
            {
                overlayColor = color.Value;
                _image.color = overlayColor;
            }

            if (closeOnClick.HasValue)
                this.closeOnClick = closeOnClick.Value;

            _onClose = onClose;
        }

        public void Show()
        {
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            
            gameObject.SetActive(true);
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
            gameObject.SetActive(false);
            _animationCoroutine = null;
            onComplete?.Invoke();
        }

        private void OnClicked()
        {
            if (closeOnClick)
            {
                _onClose?.Invoke();
            }
        }
    }
}
