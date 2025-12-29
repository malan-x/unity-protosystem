// Packages/com.protosystem.core/Runtime/UI/Windows/ConfirmDialogWindow.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Базовый диалог подтверждения.
    /// Используется DialogBuilder.Confirm()
    /// </summary>
    [UIWindow("ConfirmDialog", WindowType.Modal, WindowLayer.Modals)]
    public class ConfirmDialogWindow : UIWindowBase, IConfirmDialog
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;
        [SerializeField] private TMP_Text yesButtonText;
        [SerializeField] private TMP_Text noButtonText;

        private Action _onYes;
        private Action _onNo;

        protected override void Awake()
        {
            base.Awake();
            
            if (yesButton != null)
                yesButton.onClick.AddListener(OnYesClicked);
            if (noButton != null)
                noButton.onClick.AddListener(OnNoClicked);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            if (yesButton != null)
                yesButton.onClick.RemoveListener(OnYesClicked);
            if (noButton != null)
                noButton.onClick.RemoveListener(OnNoClicked);
        }

        public void Setup(ConfirmDialogConfig config)
        {
            if (titleText != null)
                titleText.text = config.Title ?? "Confirm";
            
            if (messageText != null)
                messageText.text = config.Message ?? "";
            
            if (yesButtonText != null)
                yesButtonText.text = config.YesText ?? "Yes";
            
            if (noButtonText != null)
                noButtonText.text = config.NoText ?? "No";

            // Скрываем кнопку No если текст пустой (Alert mode)
            if (noButton != null)
                noButton.gameObject.SetActive(!string.IsNullOrEmpty(config.NoText));

            _onYes = config.OnYes;
            _onNo = config.OnNo;
        }

        private void OnYesClicked()
        {
            _onYes?.Invoke();
            
            EventBus.Publish(EventBus.UI.DialogConfirmed, new DialogEventData
            {
                DialogId = WindowId,
                Confirmed = true
            });
            
            Close();
        }

        private void OnNoClicked()
        {
            _onNo?.Invoke();
            
            EventBus.Publish(EventBus.UI.DialogCancelled, new DialogEventData
            {
                DialogId = WindowId,
                Confirmed = false
            });
            
            Close();
        }
    }
}
