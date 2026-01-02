// Packages/com.protosystem.core/Runtime/UI/Windows/Base/LoadingWindow.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Экран загрузки
    /// </summary>
    [UIWindow("Loading", WindowType.Overlay, WindowLayer.Loading)]
    public class LoadingWindow : UIWindowBase
    {
        [Header("Progress")]
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text statusText;

        [Header("Visual")]
        [SerializeField] private GameObject spinnerObject;
        [SerializeField] private TMP_Text tipsText;

        [Header("Settings")]
        [SerializeField] private float fillSmoothing = 5f;
        [SerializeField] private string[] loadingTips;

        private float _targetProgress;
        private float _currentProgress;

        protected override void Awake()
        {
            base.Awake();
        }

        public override void Show(System.Action onComplete = null)
        {
            base.Show(onComplete);
            
            _targetProgress = 0f;
            _currentProgress = 0f;
            UpdateProgressVisual();
            
            ShowRandomTip();
        }

        private void Update()
        {
            // Плавное заполнение
            if (Mathf.Abs(_currentProgress - _targetProgress) > 0.001f)
            {
                _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress, Time.unscaledDeltaTime * fillSmoothing);
                UpdateProgressVisual();
            }
        }

        /// <summary>
        /// Установить прогресс (0-1)
        /// </summary>
        public void SetProgress(float progress)
        {
            _targetProgress = Mathf.Clamp01(progress);
        }

        /// <summary>
        /// Установить прогресс мгновенно
        /// </summary>
        public void SetProgressImmediate(float progress)
        {
            _targetProgress = Mathf.Clamp01(progress);
            _currentProgress = _targetProgress;
            UpdateProgressVisual();
        }

        /// <summary>
        /// Установить текст статуса
        /// </summary>
        public void SetStatus(string status)
        {
            if (statusText != null)
                statusText.text = status;
        }

        /// <summary>
        /// Показать спиннер
        /// </summary>
        public void ShowSpinner(bool show)
        {
            if (spinnerObject != null)
                spinnerObject.SetActive(show);
        }

        /// <summary>
        /// Показать случайный совет
        /// </summary>
        public void ShowRandomTip()
        {
            if (tipsText == null || loadingTips == null || loadingTips.Length == 0) return;
            
            int index = Random.Range(0, loadingTips.Length);
            tipsText.text = loadingTips[index];
        }

        /// <summary>
        /// Установить конкретный совет
        /// </summary>
        public void SetTip(string tip)
        {
            if (tipsText != null)
                tipsText.text = tip;
        }

        private void UpdateProgressVisual()
        {
            if (progressFill != null)
                progressFill.fillAmount = _currentProgress;
            
            if (progressText != null)
                progressText.text = $"{Mathf.RoundToInt(_currentProgress * 100)}%";
        }
    }
}
