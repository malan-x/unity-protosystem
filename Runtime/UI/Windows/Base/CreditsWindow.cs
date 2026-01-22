// Packages/com.protosystem.core/Runtime/UI/Windows/Base/CreditsWindow.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Окно авторов / титры
    /// </summary>
    [UIWindow("Credits", WindowType.Normal, WindowLayer.Windows, Level = 1)]
    public class CreditsWindow : UIWindowBase
    {
        [Header("Data")]
        [SerializeField] protected CreditsData creditsData;

        [Header("Content")]
        [SerializeField] protected TMP_Text creditsText;
        [SerializeField] protected ScrollRect scrollRect;
        [SerializeField] protected RectTransform contentTransform;

        [Header("Auto-scroll")]
        [SerializeField] protected bool autoScroll = true;
        [SerializeField] protected float scrollSpeed = 30f;

        [Header("Buttons")]
        [SerializeField] protected Button backButton;
        [SerializeField] protected Button skipButton;

        private bool _isScrolling;
        private float _scrollPosition;

        protected override void Awake()
        {
            base.Awake();
            
            backButton?.onClick.AddListener(OnBackClicked);
            skipButton?.onClick.AddListener(OnSkipClicked);
        }

        public override void Show(System.Action onComplete = null)
        {
            base.Show(onComplete);

            // Автозагрузка из Resources если не привязан
            if (creditsData == null)
            {
                creditsData = Resources.Load<CreditsData>("Data/Credits/CreditsData");
            }

            // Загружаем данные из CreditsData если есть
            if (creditsData != null)
            {
                LoadFromData();
            }

            // Сброс скролла
            _scrollPosition = 1f;
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;

            _isScrolling = autoScroll;
        }

        private void Update()
        {
            if (!_isScrolling || scrollRect == null) return;
            
            // Автоскролл
            _scrollPosition -= (scrollSpeed * Time.unscaledDeltaTime) / contentTransform.rect.height;
            scrollRect.verticalNormalizedPosition = Mathf.Max(0, _scrollPosition);
            
            // Остановка в конце
            if (_scrollPosition <= 0)
            {
                _isScrolling = false;
            }
        }

        /// <summary>
        /// Загрузить текст из CreditsData
        /// </summary>
        public void LoadFromData()
        {
            if (creditsData == null || creditsText == null) return;
            creditsText.text = creditsData.GenerateCreditsText();
        }

        /// <summary>
        /// Загрузить данные из указанного CreditsData
        /// </summary>
        public void LoadFromData(CreditsData data)
        {
            creditsData = data;
            LoadFromData();
        }

        /// <summary>
        /// Установить текст титров напрямую
        /// </summary>
        public void SetCreditsText(string text)
        {
            if (creditsText != null)
                creditsText.text = text;
        }

        /// <summary>
        /// Добавить секцию в титры
        /// </summary>
        public void AddSection(string title, params string[] names)
        {
            if (creditsText == null) return;
            
            creditsText.text += $"\n<size=24><b>{title}</b></size>\n";
            foreach (var name in names)
            {
                creditsText.text += $"{name}\n";
            }
            creditsText.text += "\n";
        }

        /// <summary>
        /// Очистить титры
        /// </summary>
        public void ClearCredits()
        {
            if (creditsText != null)
                creditsText.text = "";
        }

        protected virtual void OnBackClicked()
        {
            UISystem.Back();
        }

        protected virtual void OnSkipClicked()
        {
            // Мгновенный скролл в конец
            _isScrolling = false;
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        // Остановка скролла при касании
        public void OnScrollBeginDrag()
        {
            _isScrolling = false;
        }
    }
}
