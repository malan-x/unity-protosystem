// Packages/com.protosystem.core/Runtime/UI/Windows/Base/MainMenuWindow.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Главное меню игры
    /// </summary>
    [UIWindow("MainMenu", WindowType.Normal, WindowLayer.Windows)]
    [UITransition("play", "GameHUD")]
    [UITransition("settings", "Settings")]
    [UITransition("credits", "Credits")]
    public class MainMenuWindow : UIWindowBase
    {
        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;

        [Header("Texts (optional)")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text versionText;

        protected override void Awake()
        {
            base.Awake();
            
            playButton?.onClick.AddListener(OnPlayClicked);
            settingsButton?.onClick.AddListener(OnSettingsClicked);
            creditsButton?.onClick.AddListener(OnCreditsClicked);
            quitButton?.onClick.AddListener(OnQuitClicked);
        }

        protected virtual void OnPlayClicked()
        {
            UISystem.Navigate("play");
        }

        protected virtual void OnSettingsClicked()
        {
            UISystem.Navigate("settings");
        }

        protected virtual void OnCreditsClicked()
        {
            UISystem.Navigate("credits");
        }

        protected virtual void OnQuitClicked()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        /// <summary>
        /// Установить версию игры
        /// </summary>
        public void SetVersion(string version)
        {
            if (versionText != null)
                versionText.text = version;
        }

        /// <summary>
        /// Установить заголовок
        /// </summary>
        public void SetTitle(string title)
        {
            if (titleText != null)
                titleText.text = title;
        }
    }
}
