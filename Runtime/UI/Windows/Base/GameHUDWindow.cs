// Packages/com.protosystem.core/Runtime/UI/Windows/Base/GameHUDWindow.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Игровой HUD - основной интерфейс во время игры
    /// </summary>
    [UIWindow("GameHUD", WindowType.Normal, WindowLayer.HUD, Level = 0, ShowInGraph = false)]
    [UITransition("pause", "PauseMenu")]
    public class GameHUDWindow : UIWindowBase
    {
        [Header("Health")]
        [SerializeField] protected Image healthFill;
        [SerializeField] protected TMP_Text healthText;

        [Header("Stamina / Energy")]
        [SerializeField] protected Image staminaFill;
        [SerializeField] protected TMP_Text staminaText;

        [Header("Score / Info")]
        [SerializeField] protected TMP_Text scoreText;
        [SerializeField] protected TMP_Text timerText;
        [SerializeField] protected TMP_Text objectiveText;

        [Header("Interaction")]
        [SerializeField] protected GameObject interactionPrompt;
        [SerializeField] protected TMP_Text interactionText;

        [Header("Pause Button (optional)")]
        [SerializeField] protected Button pauseButton;

        protected float _maxHealth = 100f;
        protected float _maxStamina = 100f;

        protected override void Awake()
        {
            base.Awake();
            
            pauseButton?.onClick.AddListener(OnPauseClicked);
            
            HideInteractionPrompt();
        }

        private void Update()
        {
            // Пауза по Escape обрабатывается в UISystem
            // Но можно добавить дополнительную логику здесь
        }

        public override void OnBackPressed()
        {
            // In gameplay we treat Back/Escape as pause toggle.
            UISystem.Navigate("pause");
        }

        protected virtual void OnPauseClicked()
        {
            UISystem.Navigate("pause");
        }

        #region Health

        public void SetMaxHealth(float max)
        {
            _maxHealth = max;
        }

        public void SetHealth(float current)
        {
            if (healthFill != null)
                healthFill.fillAmount = Mathf.Clamp01(current / _maxHealth);
            
            if (healthText != null)
                healthText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(_maxHealth)}";
        }

        public void SetHealthNormalized(float normalized)
        {
            if (healthFill != null)
                healthFill.fillAmount = Mathf.Clamp01(normalized);
            
            if (healthText != null)
                healthText.text = $"{Mathf.RoundToInt(normalized * 100)}%";
        }

        #endregion

        #region Stamina

        public void SetMaxStamina(float max)
        {
            _maxStamina = max;
        }

        public void SetStamina(float current)
        {
            if (staminaFill != null)
                staminaFill.fillAmount = Mathf.Clamp01(current / _maxStamina);
            
            if (staminaText != null)
                staminaText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(_maxStamina)}";
        }

        public void SetStaminaNormalized(float normalized)
        {
            if (staminaFill != null)
                staminaFill.fillAmount = Mathf.Clamp01(normalized);
        }

        #endregion

        #region Info

        public void SetScore(int score)
        {
            if (scoreText != null)
                scoreText.text = score.ToString();
        }

        public void SetScore(string text)
        {
            if (scoreText != null)
                scoreText.text = text;
        }

        public void SetTimer(float seconds)
        {
            if (timerText != null)
            {
                int mins = Mathf.FloorToInt(seconds / 60f);
                int secs = Mathf.FloorToInt(seconds % 60f);
                timerText.text = $"{mins:00}:{secs:00}";
            }
        }

        public void SetObjective(string text)
        {
            if (objectiveText != null)
                objectiveText.text = text;
        }

        #endregion

        #region Interaction

        public void ShowInteractionPrompt(string text)
        {
            if (interactionPrompt != null)
                interactionPrompt.SetActive(true);
            
            if (interactionText != null)
                interactionText.text = text;
        }

        public void HideInteractionPrompt()
        {
            if (interactionPrompt != null)
                interactionPrompt.SetActive(false);
        }

        #endregion
    }
}
