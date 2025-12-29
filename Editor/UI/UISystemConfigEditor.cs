// Packages/com.protosystem.core/Editor/UI/UISystemConfigEditor.cs
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Кастомный редактор для UISystemConfig с кнопками создания базовых префабов
    /// </summary>
    [CustomEditor(typeof(UISystemConfig))]
    public class UISystemConfigEditor : UnityEditor.Editor
    {
        private string _prefabFolderPath;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Определяем путь для префабов
            string configPath = AssetDatabase.GetAssetPath(target);
            string configFolder = Path.GetDirectoryName(configPath);
            _prefabFolderPath = Path.Combine(configFolder, "UI Prefabs").Replace("\\", "/");
            
            // Animation Defaults
            EditorGUILayout.LabelField("Animation Defaults", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultAnimationDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultShowAnimation"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultHideAnimation"));
            
            EditorGUILayout.Space(10);
            
            // Modal Settings
            EditorGUILayout.LabelField("Modal Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("modalOverlayColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("closeModalOnOverlayClick"));
            
            EditorGUILayout.Space(10);
            
            // Toast Settings
            EditorGUILayout.LabelField("Toast Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultToastDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxToasts"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("toastPosition"));
            
            EditorGUILayout.Space(10);
            
            // Tooltip Settings
            EditorGUILayout.LabelField("Tooltip Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tooltipDelay"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tooltipOffset"));
            
            EditorGUILayout.Space(10);
            
            // Dialog Prefabs
            EditorGUILayout.LabelField("Dialog Prefabs", EditorStyles.boldLabel);
            DrawPrefabField("confirmDialogPrefab", "Confirm Dialog Prefab", CreateConfirmDialogPrefab);
            DrawPrefabField("inputDialogPrefab", "Input Dialog Prefab", CreateInputDialogPrefab);
            DrawPrefabField("choiceDialogPrefab", "Choice Dialog Prefab", CreateChoiceDialogPrefab);
            
            EditorGUILayout.Space(10);
            
            // Common Prefabs
            EditorGUILayout.LabelField("Common Prefabs", EditorStyles.boldLabel);
            DrawPrefabField("toastPrefab", "Toast Prefab", CreateToastPrefab);
            DrawPrefabField("tooltipPrefab", "Tooltip Prefab", CreateTooltipPrefab);
            DrawPrefabField("progressPrefab", "Progress Prefab", CreateProgressPrefab);
            DrawPrefabField("modalOverlayPrefab", "Modal Overlay Prefab", CreateModalOverlayPrefab);
            
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPrefabField(string propertyName, string label, System.Func<GameObject> createFunc)
        {
            var prop = serializedObject.FindProperty(propertyName);
            
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            
            // Вычисляем области
            float buttonWidth = 90;
            float spacing = 4;
            
            var fieldRect = new Rect(rect.x, rect.y, rect.width - buttonWidth - spacing, rect.height);
            var buttonRect = new Rect(rect.xMax - buttonWidth, rect.y, buttonWidth, rect.height);
            
            // Рисуем поле
            EditorGUI.PropertyField(fieldRect, prop, new GUIContent(label));
            
            // Рисуем кнопку
            using (new EditorGUI.DisabledScope(prop.objectReferenceValue != null))
            {
                if (GUI.Button(buttonRect, "🔨 Создать"))
                {
                    var prefab = createFunc();
                    if (prefab != null)
                    {
                        prop.objectReferenceValue = prefab;
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                    }
                }
            }
        }

        private void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder(_prefabFolderPath))
            {
                string parentFolder = Path.GetDirectoryName(_prefabFolderPath).Replace("\\", "/");
                AssetDatabase.CreateFolder(parentFolder, "UI Prefabs");
            }
        }

        private GameObject SavePrefab(GameObject instance, string prefabName)
        {
            EnsurePrefabFolder();
            
            string prefabPath = $"{_prefabFolderPath}/{prefabName}.prefab";
            
            // Проверяем существование
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                if (EditorUtility.DisplayDialog("Префаб существует", 
                    $"Префаб {prefabName} уже существует. Использовать существующий?", 
                    "Да", "Перезаписать"))
                {
                    Object.DestroyImmediate(instance);
                    return existing;
                }
                AssetDatabase.DeleteAsset(prefabPath);
            }
            
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            
            Debug.Log($"[UISystemConfig] Создан префаб: {prefabPath}");
            EditorGUIUtility.PingObject(prefab);
            
            return prefab;
        }

        /// <summary>
        /// Устанавливает значение приватного SerializeField через рефлексию
        /// </summary>
        private void SetPrivateField(Component component, string fieldName, object value)
        {
            var field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(component, value);
                EditorUtility.SetDirty(component);
            }
        }

        #region Prefab Creators

        private GameObject CreateConfirmDialogPrefab()
        {
            var root = CreateDialogBase("ConfirmDialog", new Vector2(400, 200));
            var content = root.transform.Find("Content");
            var header = root.transform.Find("Header");
            
            var titleText = header.Find("Title").GetComponent<TMP_Text>();
            
            // Сообщение
            var messageGO = CreateTextElement("Message", content, "Вы уверены?", 18);
            var messageRect = messageGO.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0, 0.4f);
            messageRect.anchorMax = new Vector2(1, 0.8f);
            messageRect.offsetMin = new Vector2(20, 0);
            messageRect.offsetMax = new Vector2(-20, 0);
            var messageText = messageGO.GetComponent<TMP_Text>();
            
            // Кнопки
            var buttonsContainer = CreateButtonsContainer(content);
            var yesButtonGO = CreateButton("YesButton", buttonsContainer.transform, "Да", new Vector2(120, 40));
            var noButtonGO = CreateButton("NoButton", buttonsContainer.transform, "Нет", new Vector2(120, 40));
            
            var yesButton = yesButtonGO.GetComponent<Button>();
            var noButton = noButtonGO.GetComponent<Button>();
            var yesButtonText = yesButtonGO.transform.Find("Text").GetComponent<TMP_Text>();
            var noButtonText = noButtonGO.transform.Find("Text").GetComponent<TMP_Text>();
            
            var dialogComponent = root.AddComponent<ConfirmDialogWindow>();
            SetPrivateField(dialogComponent, "titleText", titleText);
            SetPrivateField(dialogComponent, "messageText", messageText);
            SetPrivateField(dialogComponent, "yesButton", yesButton);
            SetPrivateField(dialogComponent, "noButton", noButton);
            SetPrivateField(dialogComponent, "yesButtonText", yesButtonText);
            SetPrivateField(dialogComponent, "noButtonText", noButtonText);
            
            return SavePrefab(root, "ConfirmDialog");
        }

        private GameObject CreateInputDialogPrefab()
        {
            var root = CreateDialogBase("InputDialog", new Vector2(400, 250));
            var content = root.transform.Find("Content");
            var header = root.transform.Find("Header");
            
            var titleText = header.Find("Title").GetComponent<TMP_Text>();
            
            // Сообщение
            var messageGO = CreateTextElement("Message", content, "Введите значение:", 18);
            var messageRect = messageGO.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0, 0.65f);
            messageRect.anchorMax = new Vector2(1, 0.85f);
            messageRect.offsetMin = new Vector2(20, 0);
            messageRect.offsetMax = new Vector2(-20, 0);
            var messageText = messageGO.GetComponent<TMP_Text>();
            
            // Поле ввода
            var inputGO = CreateInputField("InputField", content);
            var inputRect = inputGO.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0, 0.4f);
            inputRect.anchorMax = new Vector2(1, 0.6f);
            inputRect.offsetMin = new Vector2(20, 0);
            inputRect.offsetMax = new Vector2(-20, 0);
            var inputField = inputGO.GetComponent<TMP_InputField>();
            
            // Error text
            var errorGO = CreateTextElement("ErrorText", content, "", 14);
            var errorRect = errorGO.GetComponent<RectTransform>();
            errorRect.anchorMin = new Vector2(0, 0.25f);
            errorRect.anchorMax = new Vector2(1, 0.38f);
            errorRect.offsetMin = new Vector2(20, 0);
            errorRect.offsetMax = new Vector2(-20, 0);
            var errorText = errorGO.GetComponent<TMP_Text>();
            errorText.color = new Color(1f, 0.4f, 0.4f);
            errorText.fontSize = 14;
            
            // Кнопки
            var buttonsContainer = CreateButtonsContainer(content);
            var submitButtonGO = CreateButton("SubmitButton", buttonsContainer.transform, "OK", new Vector2(120, 40));
            var cancelButtonGO = CreateButton("CancelButton", buttonsContainer.transform, "Отмена", new Vector2(120, 40));
            
            var submitButton = submitButtonGO.GetComponent<Button>();
            var cancelButton = cancelButtonGO.GetComponent<Button>();
            var submitButtonText = submitButtonGO.transform.Find("Text").GetComponent<TMP_Text>();
            var cancelButtonText = cancelButtonGO.transform.Find("Text").GetComponent<TMP_Text>();
            
            var dialogComponent = root.AddComponent<InputDialogWindow>();
            SetPrivateField(dialogComponent, "titleText", titleText);
            SetPrivateField(dialogComponent, "messageText", messageText);
            SetPrivateField(dialogComponent, "inputField", inputField);
            SetPrivateField(dialogComponent, "errorText", errorText);
            SetPrivateField(dialogComponent, "submitButton", submitButton);
            SetPrivateField(dialogComponent, "cancelButton", cancelButton);
            SetPrivateField(dialogComponent, "submitButtonText", submitButtonText);
            SetPrivateField(dialogComponent, "cancelButtonText", cancelButtonText);
            
            return SavePrefab(root, "InputDialog");
        }

        private GameObject CreateChoiceDialogPrefab()
        {
            var root = CreateDialogBase("ChoiceDialog", new Vector2(400, 300));
            var content = root.transform.Find("Content");
            var header = root.transform.Find("Header");
            
            var titleText = header.Find("Title").GetComponent<TMP_Text>();
            
            // Сообщение
            var messageGO = CreateTextElement("Message", content, "Выберите вариант:", 18);
            var messageRect = messageGO.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0, 0.8f);
            messageRect.anchorMax = new Vector2(1, 0.95f);
            messageRect.offsetMin = new Vector2(20, 0);
            messageRect.offsetMax = new Vector2(-20, 0);
            var messageText = messageGO.GetComponent<TMP_Text>();
            
            // Контейнер для кнопок выбора
            var choicesGO = new GameObject("ChoicesContainer");
            choicesGO.transform.SetParent(content, false);
            var choicesRect = choicesGO.AddComponent<RectTransform>();
            choicesRect.anchorMin = new Vector2(0, 0.2f);
            choicesRect.anchorMax = new Vector2(1, 0.75f);
            choicesRect.offsetMin = new Vector2(20, 0);
            choicesRect.offsetMax = new Vector2(-20, 0);
            
            var vlg = choicesGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            
            // Шаблон кнопки выбора
            var choiceTemplate = CreateButton("ChoiceButtonTemplate", choicesGO.transform, "Вариант", new Vector2(0, 40));
            var choiceLE = choiceTemplate.AddComponent<LayoutElement>();
            choiceLE.preferredHeight = 40;
            choiceTemplate.SetActive(false);
            var choiceButton = choiceTemplate.GetComponent<Button>();
            
            // Кнопка отмены
            var cancelContainer = CreateButtonsContainer(content);
            var cancelRect = cancelContainer.GetComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0, 0);
            cancelRect.anchorMax = new Vector2(1, 0.18f);
            
            var cancelButtonGO = CreateButton("CancelButton", cancelContainer.transform, "Отмена", new Vector2(120, 40));
            var cancelButton = cancelButtonGO.GetComponent<Button>();
            var cancelButtonText = cancelButtonGO.transform.Find("Text").GetComponent<TMP_Text>();
            
            var dialogComponent = root.AddComponent<ChoiceDialogWindow>();
            SetPrivateField(dialogComponent, "titleText", titleText);
            SetPrivateField(dialogComponent, "messageText", messageText);
            SetPrivateField(dialogComponent, "choicesContainer", choicesGO.transform);
            SetPrivateField(dialogComponent, "choiceButtonTemplate", choiceButton);
            SetPrivateField(dialogComponent, "cancelButton", cancelButton);
            SetPrivateField(dialogComponent, "cancelButtonText", cancelButtonText);
            
            return SavePrefab(root, "ChoiceDialog");
        }

        private GameObject CreateToastPrefab()
        {
            var root = new GameObject("Toast");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(300, 60);
            
            var canvasGroup = root.AddComponent<CanvasGroup>();
            
            // Фон
            var bgImage = root.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            
            // Кнопка на весь Toast
            var clickButton = root.AddComponent<Button>();
            clickButton.transition = Selectable.Transition.ColorTint;
            var colors = clickButton.colors;
            colors.highlightedColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            clickButton.colors = colors;
            clickButton.targetGraphic = bgImage;
            
            // Горизонтальный layout
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(15, 15, 10, 10);
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            
            // Иконка
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(root.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.color = Color.white;
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 32;
            iconLE.preferredHeight = 32;
            
            // Текст
            var textGO = CreateTextElement("Text", root.transform, "Toast message", 16);
            var messageText = textGO.GetComponent<TextMeshProUGUI>();
            messageText.alignment = TextAlignmentOptions.MidlineLeft;
            var textLE = textGO.AddComponent<LayoutElement>();
            textLE.flexibleWidth = 1;
            
            var toastComponent = root.AddComponent<ToastComponent>();
            SetPrivateField(toastComponent, "messageText", messageText);
            SetPrivateField(toastComponent, "iconImage", iconImg);
            SetPrivateField(toastComponent, "backgroundImage", bgImage);
            SetPrivateField(toastComponent, "clickButton", clickButton);
            
            return SavePrefab(root, "Toast");
        }

        private GameObject CreateTooltipPrefab()
        {
            var root = new GameObject("Tooltip");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(200, 80);
            
            var canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            
            // Фон
            var bgImage = root.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            
            // Content Size Fitter
            var csf = root.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Vertical Layout Group
            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 8, 8);
            vlg.spacing = 4;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            
            // Title
            var titleGO = CreateTextElement("Title", root.transform, "Заголовок", 16);
            var titleText = titleGO.GetComponent<TextMeshProUGUI>();
            titleText.alignment = TextAlignmentOptions.TopLeft;
            titleText.fontStyle = FontStyles.Bold;
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredWidth = 180;
            
            // Content
            var contentGO = CreateTextElement("Content", root.transform, "Описание тултипа", 14);
            var contentText = contentGO.GetComponent<TextMeshProUGUI>();
            contentText.alignment = TextAlignmentOptions.TopLeft;
            contentText.enableWordWrapping = true;
            var contentLE = contentGO.AddComponent<LayoutElement>();
            contentLE.preferredWidth = 180;
            
            var tooltipComponent = root.AddComponent<TooltipComponent>();
            SetPrivateField(tooltipComponent, "titleText", titleText);
            SetPrivateField(tooltipComponent, "contentText", contentText);
            SetPrivateField(tooltipComponent, "backgroundImage", bgImage);
            SetPrivateField(tooltipComponent, "layoutGroup", vlg);
            
            return SavePrefab(root, "Tooltip");
        }

        private GameObject CreateProgressPrefab()
        {
            var root = new GameObject("Progress");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(300, 80);
            
            var canvasGroup = root.AddComponent<CanvasGroup>();
            
            // Фон
            var bgImage = root.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            
            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 15, 15);
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            
            // Текст сообщения
            var textGO = CreateTextElement("Message", root.transform, "Loading...", 16);
            var messageText = textGO.GetComponent<TextMeshProUGUI>();
            var textLE = textGO.AddComponent<LayoutElement>();
            textLE.preferredHeight = 25;
            
            // Progress Bar Container
            var barContainerGO = new GameObject("ProgressBarContainer");
            barContainerGO.transform.SetParent(root.transform, false);
            var barContainerRect = barContainerGO.AddComponent<RectTransform>();
            var barContainerLE = barContainerGO.AddComponent<LayoutElement>();
            barContainerLE.preferredHeight = 20;
            
            // Progress Bar Background
            var barBg = barContainerGO.AddComponent<Image>();
            barBg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            
            // Progress Bar Fill
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(barContainerGO.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.5f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.6f, 1f, 1f);
            
            // Percent text
            var percentGO = CreateTextElement("Percent", root.transform, "50%", 14);
            var percentText = percentGO.GetComponent<TextMeshProUGUI>();
            var percentLE = percentGO.AddComponent<LayoutElement>();
            percentLE.preferredHeight = 20;
            
            var progressComponent = root.AddComponent<ProgressComponent>();
            SetPrivateField(progressComponent, "messageText", messageText);
            SetPrivateField(progressComponent, "percentText", percentText);
            SetPrivateField(progressComponent, "fillImage", fillImg);
            SetPrivateField(progressComponent, "backgroundImage", barBg);
            
            return SavePrefab(root, "Progress");
        }

        private GameObject CreateModalOverlayPrefab()
        {
            var root = new GameObject("ModalOverlay");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            
            var canvasGroup = root.AddComponent<CanvasGroup>();
            
            // Полупрозрачный фон
            var bgImage = root.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f);
            
            // Button для перехвата кликов
            var btn = root.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bgImage;
            
            var overlayComponent = root.AddComponent<ModalOverlayComponent>();
            
            return SavePrefab(root, "ModalOverlay");
        }

        #endregion

        #region UI Element Helpers

        private GameObject CreateDialogBase(string name, Vector2 size)
        {
            var root = new GameObject(name);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = size;
            
            var canvasGroup = root.AddComponent<CanvasGroup>();
            
            // Фон
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            // Заголовок
            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(root.transform, false);
            var headerRect = headerGO.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 0.85f);
            headerRect.anchorMax = Vector2.one;
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;
            
            var headerBg = headerGO.AddComponent<Image>();
            headerBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            
            // Title text
            var titleGO = CreateTextElement("Title", headerGO.transform, "Dialog Title", 20);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(15, 0);
            titleRect.offsetMax = new Vector2(-15, 0);
            titleGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
            
            // Content container
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(root.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = new Vector2(1, 0.85f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            
            return root;
        }

        private GameObject CreateTextElement(string name, Transform parent, string text, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            
            return go;
        }

        private GameObject CreateButtonsContainer(Transform parent)
        {
            var go = new GameObject("Buttons");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0.3f);
            rect.offsetMin = new Vector2(20, 10);
            rect.offsetMax = new Vector2(-20, -5);
            
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            
            return go;
        }

        private GameObject CreateButton(string name, Transform parent, string text, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            
            var img = go.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            colors.pressedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            btn.colors = colors;
            btn.targetGraphic = img;
            
            var textGO = CreateTextElement("Text", go.transform, text, 16);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            return go;
        }

        private GameObject CreateInputField(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            
            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            
            // Text Area
            var textAreaGO = new GameObject("Text Area");
            textAreaGO.transform.SetParent(go.transform, false);
            var textAreaRect = textAreaGO.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);
            
            // Mask для обрезки текста
            textAreaGO.AddComponent<RectMask2D>();
            
            // Placeholder
            var placeholderGO = CreateTextElement("Placeholder", textAreaGO.transform, "Введите текст...", 16);
            var placeholderRect = placeholderGO.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            var placeholderTMP = placeholderGO.GetComponent<TextMeshProUGUI>();
            placeholderTMP.alignment = TextAlignmentOptions.MidlineLeft;
            placeholderTMP.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            placeholderTMP.fontStyle = FontStyles.Italic;
            
            // Text
            var textGO = CreateTextElement("Text", textAreaGO.transform, "", 16);
            var textRectT = textGO.GetComponent<RectTransform>();
            textRectT.anchorMin = Vector2.zero;
            textRectT.anchorMax = Vector2.one;
            textRectT.offsetMin = Vector2.zero;
            textRectT.offsetMax = Vector2.zero;
            textGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
            
            // Input Field component
            var inputField = go.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = textGO.GetComponent<TextMeshProUGUI>();
            inputField.placeholder = placeholderTMP;
            
            return go;
        }

        #endregion
    }
}
