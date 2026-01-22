// Packages/com.protosystem.core/Runtime/UI/Windows/InputDialogWindow.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Диалог с текстовым вводом.
    /// Используется DialogBuilder.Input()
    /// </summary>
    [UIWindow("InputDialog", WindowType.Modal, WindowLayer.Modals)]
    public class InputDialogWindow : UIWindowBase, IInputDialog
    {
        [Header("UI Elements")]
        [SerializeField] protected TMP_Text titleText;
        [SerializeField] protected TMP_Text messageText;
        [SerializeField] protected TMP_InputField inputField;
        [SerializeField] protected Button submitButton;
        [SerializeField] protected Button cancelButton;
        [SerializeField] protected TMP_Text submitButtonText;
        [SerializeField] protected TMP_Text cancelButtonText;
        [SerializeField] protected TMP_Text errorText;

        private Action<string> _onSubmit;
        private Action _onCancel;
        private Func<string, bool> _validator;

        protected override void Awake()
        {
            base.Awake();
            
            if (submitButton != null)
                submitButton.onClick.AddListener(OnSubmitClicked);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelClicked);
            if (inputField != null)
                inputField.onValueChanged.AddListener(OnInputChanged);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            if (submitButton != null)
                submitButton.onClick.RemoveListener(OnSubmitClicked);
            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(OnCancelClicked);
            if (inputField != null)
                inputField.onValueChanged.RemoveListener(OnInputChanged);
        }

        public void Setup(InputDialogConfig config)
        {
            if (titleText != null)
                titleText.text = config.Title ?? "Input";
            
            if (messageText != null)
                messageText.text = config.Message ?? "";
            
            if (inputField != null)
            {
                inputField.text = config.DefaultValue ?? "";
                
                if (inputField.placeholder is TMP_Text placeholder)
                    placeholder.text = config.Placeholder ?? "";
            }
            
            if (submitButtonText != null)
                submitButtonText.text = config.SubmitText ?? "OK";
            
            if (cancelButtonText != null)
                cancelButtonText.text = config.CancelText ?? "Cancel";

            if (errorText != null)
                errorText.gameObject.SetActive(false);

            _onSubmit = config.OnSubmit;
            _onCancel = config.OnCancel;
            _validator = config.Validator;
        }

        protected override void OnShow()
        {
            base.OnShow();
            
            // Фокус на поле ввода
            if (inputField != null)
                inputField.Select();
        }

        private void OnInputChanged(string value)
        {
            // Валидация при изменении
            if (_validator != null && errorText != null)
            {
                bool isValid = _validator(value);
                submitButton.interactable = isValid;
            }
        }

        private void OnSubmitClicked()
        {
            string value = inputField?.text ?? "";
            
            // Финальная валидация
            if (_validator != null && !_validator(value))
            {
                if (errorText != null)
                {
                    errorText.gameObject.SetActive(true);
                    errorText.text = "Invalid input";
                }
                return;
            }
            
            _onSubmit?.Invoke(value);
            
            EventBus.Publish(EventBus.UI.DialogConfirmed, new DialogEventData
            {
                DialogId = WindowId,
                Confirmed = true,
                InputValue = value
            });
            
            Close();
        }

        private void OnCancelClicked()
        {
            _onCancel?.Invoke();
            
            EventBus.Publish(EventBus.UI.DialogCancelled, new DialogEventData
            {
                DialogId = WindowId,
                Confirmed = false
            });
            
            Close();
        }
    }
}
