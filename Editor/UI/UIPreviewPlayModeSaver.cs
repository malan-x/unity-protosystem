// Packages/com.protosystem.core/Editor/UI/UIPreviewPlayModeSaver.cs
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Автоматически сохраняет изменения UI из Play Mode в prefab.
    /// Работает только если включена опция Auto Save в UIPreviewWindow.
    /// </summary>
    [InitializeOnLoad]
    public static class UIPreviewPlayModeSaver
    {
        private const string SAVE_PATH = ".claude/ui_hierarchy_snapshot.json";
        private const string PREF_AUTO_SAVE = "ProtoSystem.UIPreview.AutoSave";
        private const string PREF_TARGET_PREFAB = "ProtoSystem.UIPreview.TargetPrefab";
        private const string PREF_TARGET_OBJECT = "ProtoSystem.UIPreview.TargetObject";

        static UIPreviewPlayModeSaver()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Проверяем, включено ли автосохранение
            bool autoSave = EditorPrefs.GetBool(PREF_AUTO_SAVE, false);
            if (!autoSave)
                return;

            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Сохраняем иерархию перед выходом из Play Mode
                SaveHierarchySnapshot();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                // Применяем сохранённую иерархию к prefab
                ApplySnapshotToPrefab();
            }
        }

        private static void SaveHierarchySnapshot()
        {
            string targetObjectName = EditorPrefs.GetString(PREF_TARGET_OBJECT, "MyWindow");

            GameObject targetObject = GameObject.Find(targetObjectName);
            if (targetObject == null)
            {
                Debug.LogWarning($"[UIPreviewSaver] Target object '{targetObjectName}' not found in scene");
                return;
            }

            var snapshot = new UIHierarchySnapshot();
            snapshot.rootName = targetObject.name;
            snapshot.SerializeFromGameObject(targetObject);

            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.Combine(projectPath, SAVE_PATH);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, JsonUtility.ToJson(snapshot, true));

            Debug.Log($"[UIPreviewSaver] Saved hierarchy snapshot: {SAVE_PATH}");
        }

        private static void ApplySnapshotToPrefab()
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.Combine(projectPath, SAVE_PATH);

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[UIPreviewSaver] No snapshot found: {SAVE_PATH}");
                return;
            }

            string targetPrefabPath = EditorPrefs.GetString(PREF_TARGET_PREFAB, "");
            if (string.IsNullOrEmpty(targetPrefabPath))
            {
                Debug.LogWarning("[UIPreviewSaver] No target prefab path set");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[UIPreviewSaver] Prefab not found: {targetPrefabPath}");
                return;
            }

            try
            {
                string json = File.ReadAllText(fullPath);
                var snapshot = JsonUtility.FromJson<UIHierarchySnapshot>(json);

                // Создаём временный объект из prefab
                GameObject tempInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                // Очищаем все дочерние объекты
                Transform root = tempInstance.transform;
                for (int i = root.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
                }

                // Применяем snapshot
                snapshot.ApplyToGameObject(tempInstance);

                // Сохраняем обратно в prefab
                PrefabUtility.SaveAsPrefabAsset(tempInstance, targetPrefabPath);
                UnityEngine.Object.DestroyImmediate(tempInstance);

                AssetDatabase.Refresh();
                Debug.Log($"[UIPreviewSaver] Applied snapshot to prefab: {targetPrefabPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIPreviewSaver] Failed to apply snapshot: {e.Message}");
            }
        }

        [Serializable]
        private class UIHierarchySnapshot
        {
            public string rootName;
            public UIElementData[] elements;

            // Информация о компоненте UIWindow на корневом объекте
            public string windowComponentType;      // Полное имя типа (namespace + class)
            public string windowComponentAssembly;  // Имя сборки

            public void SerializeFromGameObject(GameObject root)
            {
                var elementsList = new System.Collections.Generic.List<UIElementData>();

                // Сохраняем информацию о UIWindowBase компоненте
                var windowComponent = root.GetComponent<UIWindowBase>();
                if (windowComponent != null)
                {
                    var componentType = windowComponent.GetType();
                    windowComponentType = componentType.FullName;
                    windowComponentAssembly = componentType.Assembly.GetName().Name;
                    Debug.Log($"[UIPreviewSaver] Found UIWindow component: {windowComponentType}");
                }

                // Начинаем с детей корневого объекта, не с самого корня
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    SerializeRecursive(root.transform.GetChild(i), "", elementsList);
                }

                elements = elementsList.ToArray();
            }

            private void SerializeRecursive(Transform t, string parentPath, System.Collections.Generic.List<UIElementData> list)
            {
                // Фильтруем автогенерируемые объекты Unity
                if (t.name.StartsWith("TMP SubMeshUI"))
                {
                    return; // Пропускаем TextMeshPro submeshes
                }

                // Сохраняем данные элемента
                var data = new UIElementData();
                data.name = t.name;
                data.parentPath = parentPath;
                data.localPosition = t.localPosition;
                data.localRotation = t.localRotation;
                data.localScale = t.localScale;

                var rectTransform = t.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    data.hasRectTransform = true;
                    data.anchorMin = rectTransform.anchorMin;
                    data.anchorMax = rectTransform.anchorMax;
                    data.anchoredPosition = rectTransform.anchoredPosition;
                    data.sizeDelta = rectTransform.sizeDelta;
                    data.pivot = rectTransform.pivot;
                }

                var image = t.GetComponent<Image>();
                if (image != null)
                {
                    data.hasImage = true;
                    data.imageColor = image.color;
                }

                var tmp = t.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    data.hasTextMeshPro = true;
                    data.text = tmp.text;
                    data.fontSize = tmp.fontSize;
                    data.textColor = tmp.color;
                    data.alignment = (int)tmp.alignment;
                    data.characterSpacing = tmp.characterSpacing;
                    data.fontStyle = (int)tmp.fontStyle;
                }

                list.Add(data);

                // Рекурсивно обрабатываем детей
                // Пути относительно корня prefab (без имени корня в начале)
                string currentPath = string.IsNullOrEmpty(parentPath) ? t.name : parentPath + "/" + t.name;
                for (int i = 0; i < t.childCount; i++)
                {
                    SerializeRecursive(t.GetChild(i), currentPath, list);
                }
            }

            public void ApplyToGameObject(GameObject root)
            {
                if (elements == null || elements.Length == 0)
                    return;

                foreach (var element in elements)
                {
                    // Находим родителя
                    Transform parent = root.transform;
                    if (!string.IsNullOrEmpty(element.parentPath))
                    {
                        // Пути теперь относительно корня, без имени корня в начале
                        Transform foundParent = root.transform.Find(element.parentPath);
                        if (foundParent != null)
                        {
                            parent = foundParent;
                        }
                        else
                        {
                            Debug.LogWarning($"[UIPreviewSaver] Parent not found: {element.parentPath} for element {element.name}");
                        }
                    }

                    // Создаём новый объект
                    GameObject obj = new GameObject(element.name);
                    obj.transform.SetParent(parent, false); // Автоматически конвертирует Transform → RectTransform

                    // RectTransform УЖЕ СУЩЕСТВУЕТ после SetParent к RectTransform родителю
                    RectTransform rect = obj.GetComponent<RectTransform>();
                    if (rect == null)
                    {
                        // Если по какой-то причине нет RectTransform, добавляем
                        rect = obj.AddComponent<RectTransform>();
                    }

                    // Настраиваем Transform
                    rect.localPosition = element.localPosition;
                    rect.localRotation = element.localRotation;
                    rect.localScale = element.localScale;

                    // Настраиваем RectTransform параметры
                    if (element.hasRectTransform)
                    {
                        rect.anchorMin = element.anchorMin;
                        rect.anchorMax = element.anchorMax;
                        rect.anchoredPosition = element.anchoredPosition;
                        rect.sizeDelta = element.sizeDelta;
                        rect.pivot = element.pivot;
                    }

                    // Image
                    if (element.hasImage)
                    {
                        var image = obj.AddComponent<Image>();
                        image.color = element.imageColor;
                        image.raycastTarget = false;
                    }

                    // TextMeshProUGUI
                    if (element.hasTextMeshPro)
                    {
                        var tmp = obj.AddComponent<TextMeshProUGUI>();
                        tmp.text = element.text;
                        tmp.fontSize = element.fontSize;
                        tmp.color = element.textColor;
                        tmp.alignment = (TextAlignmentOptions)element.alignment;
                        tmp.characterSpacing = element.characterSpacing;
                        tmp.fontStyle = (FontStyles)element.fontStyle;
                        tmp.raycastTarget = false;
                    }
                }

                // Добавляем UIWindow компонент если он был сохранён
                if (!string.IsNullOrEmpty(windowComponentType))
                {
                    AddWindowComponent(root);
                }
            }

            private void AddWindowComponent(GameObject root)
            {
                // Удаляем старые UIWindowBase компоненты
                var existingComponents = root.GetComponents<UIWindowBase>();
                foreach (var comp in existingComponents)
                {
                    if (comp != null)
                        UnityEngine.Object.DestroyImmediate(comp);
                }

                // Загружаем тип компонента
                System.Type componentType = null;
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    componentType = assembly.GetType(windowComponentType);
                    if (componentType != null)
                        break;
                }

                if (componentType == null)
                {
                    Debug.LogError($"[UIPreviewSaver] Component type not found: {windowComponentType}");
                    return;
                }

                // Добавляем компонент
                var component = root.AddComponent(componentType);
                Debug.Log($"[UIPreviewSaver] Added component: {componentType.Name}");

                // Исправляем CanvasGroup alpha (UIWindowBase устанавливает в 0 при Awake)
                var canvasGroup = root.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f; // В prefab должен быть видимым
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }

                // Auto-wire SerializeField ссылки
                AutoWireFields(component, root);
            }

            private void AutoWireFields(Component component, GameObject root)
            {
                var componentType = component.GetType();
                var fields = componentType.GetFields(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public
                );

                int wiredCount = 0;

                foreach (var field in fields)
                {
                    // Пропускаем поля без SerializeField
                    if (!field.IsPublic && field.GetCustomAttributes(typeof(SerializeField), false).Length == 0)
                        continue;

                    // Пытаемся найти объект по имени поля
                    if (TryWireField(field, component, root))
                        wiredCount++;
                }

                Debug.Log($"[UIPreviewSaver] Auto-wired {wiredCount} fields for {componentType.Name}");
            }

            private bool TryWireField(System.Reflection.FieldInfo field, Component component, GameObject root)
            {
                var fieldType = field.FieldType;

                // Массивы (например, TextMeshProUGUI[] statValues)
                if (fieldType.IsArray)
                {
                    return TryWireArrayField(field, component, root);
                }

                // Генерируем варианты имён для поиска
                var nameVariants = GenerateNameVariants(field.Name);

                // Пробуем найти объект по различным вариантам имени
                Transform found = null;
                string matchedName = null;

                foreach (var variant in nameVariants)
                {
                    found = FindChildByName(root.transform, variant);
                    if (found != null)
                    {
                        matchedName = variant;
                        break;
                    }
                }

                if (found == null)
                    return false;

                // Получаем нужный компонент
                object value = null;

                if (fieldType == typeof(GameObject))
                {
                    value = found.gameObject;
                }
                else if (fieldType == typeof(Transform) || fieldType == typeof(RectTransform))
                {
                    value = found;
                }
                else if (typeof(Component).IsAssignableFrom(fieldType))
                {
                    value = found.GetComponent(fieldType);
                }

                if (value != null)
                {
                    field.SetValue(component, value);
                    Debug.Log($"[UIPreviewSaver]   {field.Name} → {matchedName} ({fieldType.Name})");
                    return true;
                }

                return false;
            }

            private string[] GenerateNameVariants(string fieldName)
            {
                var variants = new System.Collections.Generic.List<string>();

                // Убираем префикс _
                if (fieldName.StartsWith("_"))
                    fieldName = fieldName.Substring(1);

                // Вариант 1: Базовое преобразование (убираем суффиксы типов)
                string baseName = fieldName;
                baseName = baseName.Replace("Text", "");
                baseName = baseName.Replace("Button", "");
                baseName = baseName.Replace("Container", "");
                baseName = baseName.Replace("Grid", "");
                baseName = baseName.Replace("Image", "");
                baseName = baseName.Replace("Rect", "");

                // Первая буква в верхний регистр
                if (baseName.Length > 0)
                    baseName = char.ToUpper(baseName[0]) + baseName.Substring(1);

                variants.Add(baseName);

                // Вариант 2: Для кнопок - пробуем с префиксом Btn
                if (fieldName.Contains("Button") || fieldName.Contains("button"))
                {
                    variants.Add("Btn" + baseName);

                    // Специальные случаи для стандартных кнопок
                    if (fieldName.ToLower().Contains("retry"))
                        variants.Add("BtnPrimary");
                    if (fieldName.ToLower().Contains("base") || fieldName.ToLower().Contains("back"))
                        variants.Add("BtnSecondary");
                    if (fieldName.ToLower().Contains("confirm") || fieldName.ToLower().Contains("ok"))
                        variants.Add("BtnPrimary");
                    if (fieldName.ToLower().Contains("cancel"))
                        variants.Add("BtnSecondary");
                }

                // Вариант 3: Исходное имя с заглавной буквы (без удаления суффиксов)
                string originalWithCaps = char.ToUpper(fieldName[0]) + fieldName.Substring(1);
                if (!variants.Contains(originalWithCaps))
                    variants.Add(originalWithCaps);

                // Вариант 4: Для Grid - пробуем с суффиксом
                if (fieldName.ToLower().Contains("grid"))
                {
                    string withGrid = baseName + "Grid";
                    if (!variants.Contains(withGrid))
                        variants.Add(withGrid);
                }

                // Вариант 5: Для Title - пробуем Text (resultTitleText → ResultText)
                if (fieldName.ToLower().Contains("title"))
                {
                    string withText = baseName.Replace("Title", "Text");
                    if (!variants.Contains(withText))
                        variants.Add(withText);
                }

                // Вариант 6: Для полей с Text в конце - пробуем найти объект с таким же именем
                if (fieldName.EndsWith("Text") || fieldName.EndsWith("text"))
                {
                    // resultText → ResultText (оставляем Text)
                    string nameWithText = fieldName;
                    if (nameWithText.StartsWith("_"))
                        nameWithText = nameWithText.Substring(1);
                    nameWithText = char.ToUpper(nameWithText[0]) + nameWithText.Substring(1);
                    if (!variants.Contains(nameWithText))
                        variants.Add(nameWithText);
                }

                return variants.ToArray();
            }

            private bool TryWireArrayField(System.Reflection.FieldInfo field, Component component, GameObject root)
            {
                var elementType = field.FieldType.GetElementType();
                var fieldName = field.Name;

                // Примеры: statValues → StatCell1/Value, StatCell2/Value...
                //          statLabels → StatCell1/Label, StatCell2/Label...

                string baseName = FieldNameToObjectName(fieldName);

                // Ищем элементы с паттерном: StatCell1, StatCell2... или похожими
                var elements = new System.Collections.Generic.List<object>();

                // Пробуем найти через паттерн "BaseName{N}"
                for (int i = 1; i <= 10; i++) // максимум 10 элементов
                {
                    string cellName = baseName.Replace("s", "") + i; // statValues → StatValue1, StatValue2
                    Transform cell = FindChildByName(root.transform, cellName);

                    if (cell == null)
                    {
                        // Пробуем другой вариант: StatCell1, StatCell2
                        cellName = baseName.Replace("Values", "Cell").Replace("Labels", "Cell") + i;
                        cell = FindChildByName(root.transform, cellName);
                    }

                    if (cell == null)
                        break;

                    // Для statValues ищем "Value" внутри StatCell
                    // Для statLabels ищем "Label" внутри StatCell
                    Transform target = cell;

                    if (fieldName.Contains("Value"))
                        target = cell.Find("Value") ?? cell;
                    else if (fieldName.Contains("Label"))
                        target = cell.Find("Label") ?? cell;

                    object value = null;
                    if (elementType == typeof(GameObject))
                        value = target.gameObject;
                    else if (typeof(Component).IsAssignableFrom(elementType))
                        value = target.GetComponent(elementType);

                    if (value != null)
                        elements.Add(value);
                    else
                        break;
                }

                if (elements.Count > 0)
                {
                    var array = System.Array.CreateInstance(elementType, elements.Count);
                    for (int i = 0; i < elements.Count; i++)
                        array.SetValue(elements[i], i);

                    field.SetValue(component, array);
                    Debug.Log($"[UIPreviewSaver]   {field.Name}[{elements.Count}] → auto-wired");
                    return true;
                }

                return false;
            }

            private Transform FindChildByName(Transform parent, string name)
            {
                // Рекурсивный поиск по всей иерархии
                if (parent.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return parent;

                for (int i = 0; i < parent.childCount; i++)
                {
                    var found = FindChildByName(parent.GetChild(i), name);
                    if (found != null)
                        return found;
                }

                return null;
            }

            private string FieldNameToObjectName(string fieldName)
            {
                // resultTitleText → ResultTitle
                // retryButton → BtnPrimary или Retry
                // contentContainer → Content

                // Убираем префиксы _
                if (fieldName.StartsWith("_"))
                    fieldName = fieldName.Substring(1);

                // Убираем суффиксы типов
                fieldName = fieldName.Replace("Text", "");
                fieldName = fieldName.Replace("Button", "");
                fieldName = fieldName.Replace("Container", "");
                fieldName = fieldName.Replace("Grid", "");
                fieldName = fieldName.Replace("Image", "");

                // Первая буква в верхний регистр
                if (fieldName.Length > 0)
                    fieldName = char.ToUpper(fieldName[0]) + fieldName.Substring(1);

                return fieldName;
            }
        }

        [Serializable]
        private class UIElementData
        {
            public string name;
            public string parentPath;

            // Transform
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;

            // RectTransform
            public bool hasRectTransform;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector2 pivot;

            // Image
            public bool hasImage;
            public Color imageColor;

            // TextMeshProUGUI
            public bool hasTextMeshPro;
            public string text;
            public float fontSize;
            public Color textColor;
            public int alignment;
            public float characterSpacing;
            public int fontStyle;
        }
    }
}
