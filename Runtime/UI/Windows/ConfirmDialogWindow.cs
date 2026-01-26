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
        [SerializeField] protected TMP_Text titleText;
        [SerializeField] protected TMP_Text messageText;
        [SerializeField] protected Button yesButton;
        [SerializeField] protected Button noButton;
        [SerializeField] protected TMP_Text yesButtonText;
        [SerializeField] protected TMP_Text noButtonText;

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
            if (_onYes == null)
            {
                ProtoLogger.LogWarning("ConfirmDialogWindow", "Yes clicked but no handler is set. " +
                                 "Open this dialog via UISystem.Instance.Dialog.Confirm(...) (or ensure DialogBuilder.Setup runs). " +
                                 "If you recently updated ProtoSystem, regenerate/overwrite the ConfirmDialog prefab.");
            }
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
            if (_onNo == null)
            {
                // It's valid to omit OnNo, but warn because users often expect it to do something.
                ProtoLogger.LogWarning("ConfirmDialogWindow", "No clicked but no handler is set (OnNo is null). " +
                                 "If you expect custom behavior on cancel, pass onNo to UISystem.Instance.Dialog.Confirm(...). ");
            }
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
