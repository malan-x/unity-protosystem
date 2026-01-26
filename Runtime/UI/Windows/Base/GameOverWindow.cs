// Packages/com.protosystem.core/Runtime/UI/Windows/Base/GameOverWindow.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Окно конца игры (победа/поражение)
    /// </summary>
    [UIWindow("GameOver", WindowType.Normal, WindowLayer.Windows, Level = 1, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
    public class GameOverWindow : UIWindowBase
    {
        [Header("Content")]
        [SerializeField] protected TMP_Text titleText;
        [SerializeField] protected TMP_Text messageText;
        [SerializeField] protected Image iconImage;
        
        [Header("Colors")]
        [SerializeField] protected Color victoryColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] protected Color defeatColor = new Color(0.8f, 0.2f, 0.2f);

        [Header("Buttons")]
        [SerializeField] protected Button restartButton;
        [SerializeField] protected Button mainMenuButton;
        [SerializeField] protected Button quitButton;

        private bool _isVictory;

        protected override void Awake()
        {
            base.Awake();
            
            restartButton?.onClick.AddListener(OnRestartClicked);
            mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
            quitButton?.onClick.AddListener(OnQuitClicked);
        }

        /// <summary>
        /// Показать окно победы
        /// </summary>
        public void ShowVictory(string message = null)
        {
            _isVictory = true;
            SetupContent("ПОБЕДА", message ?? "Поздравляем! Вы победили!", victoryColor);
            Show();
        }

        /// <summary>
        /// Показать окно поражения
        /// </summary>
        public void ShowDefeat(string message = null)
        {
            _isVictory = false;
            SetupContent("ПОРАЖЕНИЕ", message ?? "К сожалению, вы проиграли.", defeatColor);
            Show();
        }

        private void SetupContent(string title, string message, Color color)
        {
            if (titleText != null)
            {
                titleText.text = title;
                titleText.color = color;
            }
            
            if (messageText != null)
                messageText.text = message;
            
            if (iconImage != null)
                iconImage.color = color;
        }

        protected virtual void OnRestartClicked()
        {
            ProtoLogger.LogRuntime("GameOverWindow", "Restart clicked");
            // Переопределить в наследниках для логики рестарта
        }

        protected virtual void OnMainMenuClicked()
        {
            ProtoLogger.LogRuntime("GameOverWindow", "Main Menu clicked");
            UISystem.Navigate("main_menu");
        }

        protected virtual void OnQuitClicked()
        {
            ProtoLogger.LogRuntime("GameOverWindow", "Quit clicked");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Была ли это победа?
        /// </summary>
        public bool IsVictory => _isVictory;
    }
}
