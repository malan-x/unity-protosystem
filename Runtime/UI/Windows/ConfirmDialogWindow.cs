// Packages/com.protosystem.core/Runtime/UI/Windows/ConfirmDialogWindow.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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

        protected override void OnShow()
        {
            base.OnShow();

            // Выбираем кнопку Yes для клавиатурной навигации
            if (yesButton != null)
                yesButton.Select();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Enter / Return → подтвердить
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                OnYesClicked();
        }

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
            SetupTextWithLoc(titleText, config.Title ?? "Confirm", config.TitleKey);
            SetupTextWithLoc(messageText, config.Message ?? "", config.MessageKey);
            SetupTextWithLoc(yesButtonText, config.YesText ?? "Yes", config.YesTextKey);
            SetupTextWithLoc(noButtonText, config.NoText ?? "No", config.NoTextKey);

            // Скрываем кнопку No если текст пустой (Alert mode)
            if (noButton != null)
                noButton.gameObject.SetActive(!string.IsNullOrEmpty(config.NoText));

            _onYes = config.OnYes;
            _onNo = config.OnNo;
        }

        /// <summary>
        /// Устанавливает текст и обновляет LocalizeTMP (если есть).
        /// Если locKey задан — SetKey() для реактивной смены языка.
        /// Если не задан — очищает ключ, чтобы LocalizeTMP не перезатирал текст.
        /// </summary>
        private void SetupTextWithLoc(TMP_Text text, string value, string locKey)
        {
            if (text == null) return;
            text.text = value;

            var loc = text.GetComponent<LocalizeTMP>();
            if (loc == null) return;

            if (!string.IsNullOrEmpty(locKey))
                loc.SetKey(locKey, value);
            else
                loc.SetKey("", null);
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
