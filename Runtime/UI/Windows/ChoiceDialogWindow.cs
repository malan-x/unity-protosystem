// Packages/com.protosystem.core/Runtime/UI/Windows/ChoiceDialogWindow.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Окно диалога выбора.
    /// </summary>
    [UIWindow("ChoiceDialog", WindowType.Modal, WindowLayer.Modals)]
    public class ChoiceDialogWindow : UIWindowBase, IChoiceDialog
    {
        [Header("UI Elements")]
        [SerializeField] protected TMP_Text titleText;
        [SerializeField] protected TMP_Text messageText;
        [SerializeField] protected Transform choicesContainer;
        [SerializeField] protected Button choiceButtonTemplate;
        [SerializeField] protected Button cancelButton;
        [SerializeField] protected TMP_Text cancelButtonText;

        private Action<int> _onSelect;
        private Action _onCancel;
        private List<Button> _spawnedButtons = new List<Button>();

        protected override void Awake()
        {
            base.Awake();
            
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelClicked);
            
            // Скрываем шаблон
            if (choiceButtonTemplate != null)
                choiceButtonTemplate.gameObject.SetActive(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(OnCancelClicked);
            
            ClearChoices();
        }

        public void Setup(ChoiceDialogConfig config)
        {
            if (titleText != null)
                titleText.text = config.Title ?? "Выбор";

            if (messageText != null)
                messageText.text = config.Message ?? "";

            _onSelect = config.OnSelect;
            _onCancel = config.OnCancel;

            // Создаём кнопки выбора
            ClearChoices();
            
            if (config.Options != null && choiceButtonTemplate != null)
            {
                for (int i = 0; i < config.Options.Count; i++)
                {
                    var choiceIndex = i;
                    var choiceText = config.Options[i];
                    
                    var buttonGO = Instantiate(choiceButtonTemplate.gameObject, choicesContainer);
                    buttonGO.SetActive(true);
                    
                    var button = buttonGO.GetComponent<Button>();
                    var text = buttonGO.GetComponentInChildren<TMP_Text>();
                    
                    if (text != null)
                        text.text = choiceText;
                    
                    button.onClick.AddListener(() => OnChoiceClicked(choiceIndex));
                    _spawnedButtons.Add(button);
                }
            }
        }

        private void ClearChoices()
        {
            foreach (var button in _spawnedButtons)
            {
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    Destroy(button.gameObject);
                }
            }
            _spawnedButtons.Clear();
        }

        private void OnChoiceClicked(int index)
        {
            _onSelect?.Invoke(index);
            Close();
        }

        private void OnCancelClicked()
        {
            _onCancel?.Invoke();
            Close();
        }
    }
}
