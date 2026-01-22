// Packages/com.protosystem.core/Runtime/UI/Windows/Base/PauseMenuWindow.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Меню паузы
    /// </summary>
    [UIWindow("PauseMenu", WindowType.Normal, WindowLayer.Windows, Level = 1, PauseGame = true, ShowInGraph = false)]
    [UITransition("settings", "Settings")]
    [UITransition("mainmenu", "MainMenu")]
    public class PauseMenuWindow : UIWindowBase
    {
        [Header("Buttons")]
        [SerializeField] protected Button resumeButton;
        [SerializeField] protected Button settingsButton;
        [SerializeField] protected Button mainMenuButton;
        [SerializeField] protected Button quitButton;

        [Header("Title")]
        [SerializeField] protected TMP_Text titleText;

        protected override void Awake()
        {
            base.Awake();
            
            resumeButton?.onClick.AddListener(OnResumeClicked);
            settingsButton?.onClick.AddListener(OnSettingsClicked);
            mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
            quitButton?.onClick.AddListener(OnQuitClicked);
        }

        protected virtual void OnResumeClicked()
        {
            UISystem.Back();
        }

        protected virtual void OnSettingsClicked()
        {
            UISystem.Navigate("settings");
        }

        protected virtual void OnMainMenuClicked()
        {
            UISystem.Instance?.Dialog.Confirm(
                "Несохранённый прогресс будет потерян.",
                onYes: () =>
                {
                    UITimeManager.Instance.ResetAllPauses();
                    UISystem.Navigate("mainmenu");
                },
                title: "Выйти в главное меню?"
            );
        }

        protected virtual void OnQuitClicked()
        {
            UISystem.Instance?.Dialog.Confirm(
                "Несохранённый прогресс будет потерян.",
                onYes: () =>
                {
                    #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                    #else
                    Application.Quit();
                    #endif
                },
                title: "Выйти из игры?"
            );
        }

        public override void OnBackPressed()
        {
            OnResumeClicked();
        }
    }
}
