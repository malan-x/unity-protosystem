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
        public void Confirm(string message, Action onYes, Action onNo = null, string title = null, string yesText = null, string noText = null)
        {
            var config = new ConfirmDialogConfig
            {
                Title = title ?? UIKeys.L(UIKeys.Dialog.ConfirmTitle, UIKeys.Dialog.Fallback.ConfirmTitle),
                Message = message,
                YesText = yesText ?? UIKeys.L(UIKeys.Dialog.Yes, UIKeys.Dialog.Fallback.Yes),
                NoText = noText ?? UIKeys.L(UIKeys.Dialog.No, UIKeys.Dialog.Fallback.No),
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
            // Открываем модальное окно. Важно: модальные окна НЕ являются CurrentWindow,
            // поэтому для настройки нужно брать инстанс из навигатора.
            var result = _system.Navigator.OpenDirect("ConfirmDialog");

            if (result == NavigationResult.WindowNotFound)
            {
                ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Warnings, "ConfirmDialog window not found in graph. Register it or assign confirmDialogPrefab in UISystemConfig");
                
                // Fallback - вызываем callback сразу
                config.OnYes?.Invoke();
                return;
            }

            // Настраиваем диалог (берём именно открытое окно по ID)
            var dialogWindow = _system.Navigator.GetWindow("ConfirmDialog");
            var dialog = dialogWindow as IConfirmDialog;
            if (dialog == null && dialogWindow != null)
            {
                // Robust fallback: prefab might have the correct component not on the root,
                // or window might be an older base class instance.
                var component = dialogWindow.GetComponentInChildren<ConfirmDialogWindow>(true);
                dialog = component;
            }

            if (dialog == null)
            {
                ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Errors, "ConfirmDialog opened but no IConfirmDialog found on the instance. Your ConfirmDialog prefab is likely outdated.");
            }
            else
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
        public void Alert(string message, Action onOk = null, string title = null, string okText = null)
        {
            Confirm(message, onOk, null, 
                title ?? UIKeys.L(UIKeys.Dialog.AlertTitle, UIKeys.Dialog.Fallback.AlertTitle), 
                okText ?? UIKeys.L(UIKeys.Dialog.OK, UIKeys.Dialog.Fallback.OK), null);
        }

        #endregion

        #region Input Dialog

        /// <summary>
        /// Показать диалог с текстовым вводом
        /// </summary>
        public void Input(string message, Action<string> onSubmit, Action onCancel = null, 
            string title = null, string defaultValue = "", string placeholder = "", 
            string submitText = null, string cancelText = null)
        {
            var config = new InputDialogConfig
            {
                Title = title ?? UIKeys.L(UIKeys.Dialog.InputTitle, UIKeys.Dialog.Fallback.InputTitle),
                Message = message,
                DefaultValue = defaultValue,
                Placeholder = placeholder,
                SubmitText = submitText ?? UIKeys.L(UIKeys.Dialog.OK, UIKeys.Dialog.Fallback.OK),
                CancelText = cancelText ?? UIKeys.L(UIKeys.Dialog.Cancel, UIKeys.Dialog.Fallback.Cancel),
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
                ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Warnings, "InputDialog window not found");
                config.OnCancel?.Invoke();
                return;
            }

            var dialogWindow = _system.Navigator.GetWindow("InputDialog");
            var dialog = dialogWindow as IInputDialog;
            if (dialog == null && dialogWindow != null)
            {
                var component = dialogWindow.GetComponentInChildren<InputDialogWindow>(true);
                dialog = component;
            }

            if (dialog == null)
            {
                ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Errors, "InputDialog opened but no IInputDialog found on the instance. Your InputDialog prefab is likely outdated.");
            }
            else
            {
                dialog.Setup(config);
            }

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
                Title = title ?? UIKeys.L(UIKeys.Dialog.ChoiceTitle, UIKeys.Dialog.Fallback.ChoiceTitle),
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
                ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Warnings, "ChoiceDialog window not found");
                config.OnCancel?.Invoke();
                return;
            }

            var dialogWindow = _system.Navigator.GetWindow("ChoiceDialog");
            var dialog = dialogWindow as IChoiceDialog;
            if (dialog == null && dialogWindow != null)
            {
                var component = dialogWindow.GetComponentInChildren<ChoiceDialogWindow>(true);
                dialog = component;
            }

            if (dialog == null)
            {
                ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Errors, "ChoiceDialog opened but no IChoiceDialog found on the instance. Your ChoiceDialog prefab is likely outdated.");
            }
            else
            {
                dialog.Setup(config);
            }

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

        /// <summary>
        /// Ключи локализации (опционально).
        /// Если указаны, ConfirmDialogWindow.Setup() обновит LocalizeTMP.SetKey(),
        /// чтобы при смене языка текст корректно перелокализовался.
        /// </summary>
        public string TitleKey { get; set; }
        public string MessageKey { get; set; }
        public string YesTextKey { get; set; }
        public string NoTextKey { get; set; }
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
