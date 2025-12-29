// Packages/com.protosystem.core/Runtime/UI/Builders/DialogBuilder.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Builder для создания диалоговых окон
    /// </summary>
    public class DialogBuilder
    {
        private readonly UISystem _system;

        public DialogBuilder(UISystem system)
        {
            _system = system;
        }

        #region Confirm Dialog

        /// <summary>
        /// Показать диалог подтверждения
        /// </summary>
        public void Confirm(string message, Action onYes, Action onNo = null, string title = null, string yesText = "Yes", string noText = "No")
        {
            var config = new ConfirmDialogConfig
            {
                Title = title ?? "Confirm",
                Message = message,
                YesText = yesText,
                NoText = noText,
                OnYes = onYes,
                OnNo = onNo
            };

            ShowConfirmDialog(config);
        }

        /// <summary>
        /// Показать диалог с настройками
        /// </summary>
        public void Confirm(ConfirmDialogConfig config)
        {
            ShowConfirmDialog(config);
        }

        private void ShowConfirmDialog(ConfirmDialogConfig config)
        {
            // Ищем зарегистрированное окно диалога или используем префаб из конфига
            var dialogWindow = _system.Navigator.OpenDirect("ConfirmDialog");
            
            if (dialogWindow == NavigationResult.WindowNotFound)
            {
                Debug.LogWarning("[DialogBuilder] ConfirmDialog window not found in graph. " +
                    "Register it or assign confirmDialogPrefab in UISystemConfig");
                
                // Fallback - вызываем callback сразу
                config.OnYes?.Invoke();
                return;
            }

            // Настраиваем диалог
            var dialog = _system.CurrentWindow as IConfirmDialog;
            if (dialog != null)
            {
                dialog.Setup(config);
            }

            EventBus.Publish(EventBus.UI.DialogShown, new DialogEventData
            {
                DialogId = "ConfirmDialog",
                Message = config.Message
            });
        }

        #endregion

        #region Alert Dialog

        /// <summary>
        /// Показать информационный диалог
        /// </summary>
        public void Alert(string message, Action onOk = null, string title = null, string okText = "OK")
        {
            Confirm(message, onOk, null, title ?? "Alert", okText, null);
        }

        #endregion

        #region Input Dialog

        /// <summary>
        /// Показать диалог с текстовым вводом
        /// </summary>
        public void Input(string message, Action<string> onSubmit, Action onCancel = null, 
            string title = null, string defaultValue = "", string placeholder = "", 
            string submitText = "OK", string cancelText = "Cancel")
        {
            var config = new InputDialogConfig
            {
                Title = title ?? "Input",
                Message = message,
                DefaultValue = defaultValue,
                Placeholder = placeholder,
                SubmitText = submitText,
                CancelText = cancelText,
                OnSubmit = onSubmit,
                OnCancel = onCancel
            };

            ShowInputDialog(config);
        }

        private void ShowInputDialog(InputDialogConfig config)
        {
            var result = _system.Navigator.OpenDirect("InputDialog");
            
            if (result == NavigationResult.WindowNotFound)
            {
                Debug.LogWarning("[DialogBuilder] InputDialog window not found");
                config.OnCancel?.Invoke();
                return;
            }

            var dialog = _system.CurrentWindow as IInputDialog;
            dialog?.Setup(config);

            EventBus.Publish(EventBus.UI.DialogShown, new DialogEventData
            {
                DialogId = "InputDialog",
                Message = config.Message
            });
        }

        #endregion

        #region Choice Dialog

        /// <summary>
        /// Показать диалог выбора
        /// </summary>
        public void Choice(string message, string[] options, Action<int> onSelect, Action onCancel = null, string title = null)
        {
            var config = new ChoiceDialogConfig
            {
                Title = title ?? "Choose",
                Message = message,
                Options = new List<string>(options),
                OnSelect = onSelect,
                OnCancel = onCancel
            };

            ShowChoiceDialog(config);
        }

        private void ShowChoiceDialog(ChoiceDialogConfig config)
        {
            var result = _system.Navigator.OpenDirect("ChoiceDialog");
            
            if (result == NavigationResult.WindowNotFound)
            {
                Debug.LogWarning("[DialogBuilder] ChoiceDialog window not found");
                config.OnCancel?.Invoke();
                return;
            }

            var dialog = _system.CurrentWindow as IChoiceDialog;
            dialog?.Setup(config);

            EventBus.Publish(EventBus.UI.DialogShown, new DialogEventData
            {
                DialogId = "ChoiceDialog",
                Message = config.Message
            });
        }

        #endregion
    }

    #region Dialog Configs

    public class ConfirmDialogConfig
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public string YesText { get; set; } = "Yes";
        public string NoText { get; set; } = "No";
        public Action OnYes { get; set; }
        public Action OnNo { get; set; }
    }

    public class InputDialogConfig
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public string DefaultValue { get; set; } = "";
        public string Placeholder { get; set; } = "";
        public string SubmitText { get; set; } = "OK";
        public string CancelText { get; set; } = "Cancel";
        public Action<string> OnSubmit { get; set; }
        public Action OnCancel { get; set; }
        public Func<string, bool> Validator { get; set; } // Опциональная валидация
    }

    public class ChoiceDialogConfig
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public List<string> Options { get; set; } = new();
        public Action<int> OnSelect { get; set; }
        public Action OnCancel { get; set; }
    }

    #endregion

    #region Dialog Interfaces

    /// <summary>
    /// Интерфейс для диалога подтверждения
    /// </summary>
    public interface IConfirmDialog
    {
        void Setup(ConfirmDialogConfig config);
    }

    /// <summary>
    /// Интерфейс для диалога ввода
    /// </summary>
    public interface IInputDialog
    {
        void Setup(InputDialogConfig config);
    }

    /// <summary>
    /// Интерфейс для диалога выбора
    /// </summary>
    public interface IChoiceDialog
    {
        void Setup(ChoiceDialogConfig config);
    }

    #endregion
}
