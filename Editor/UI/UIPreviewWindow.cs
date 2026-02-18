// Packages/com.protosystem.core/Editor/UI/UIPreviewWindow.cs
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Окно для итеративной разработки UI с Claude Code.
    /// Создаёт превью-сцену, базовый префаб окна и генерирует промпт.
    /// ProtoSystem → UI → Tools → UI Preview
    /// </summary>
    public class UIPreviewWindow : EditorWindow
    {
        // --- Режим ---
        private enum Mode
        {
            CreateNew,
            UpdateExisting
        }

        private Mode mode = Mode.CreateNew;

        // --- Настройки (Create New) ---
        private string windowClassName = "MyWindow";
        private string windowId = "my_window";
        private string scriptNamespace = "LastConvoy.UI";
        private string scriptOutputPath = "Assets/Scripts/UI/Windows";
        private string prefabOutputPath = "Assets/Prefabs/UI/Windows";

        // --- Настройки (Update Existing) ---
        private GameObject existingPrefab;

        // --- Общие ---
        private UnityEngine.Object mockupAsset;
        private string promptOutputPath = "Assets/Prompts";

        // --- Пути скриншотов ---
        private string triggerPath = "/tmp/take_screenshot";
        private string screenshotPath = "/tmp/ui_screenshot.png";

        // --- Состояние ---
        private string generatedPromptPath;
        private string launchCommand;
        private Vector2 scrollPosition;

        // --- Константы ---
        private const string PREVIEW_SCENE_PATH = "Assets/Scenes/UIPreview.unity";
        private const string PREF_PREFIX = "ProtoSystem.UIPreview.";

        [MenuItem("ProtoSystem/UI/Tools/UI Preview", priority = 300)]
        public static void ShowWindow()
        {
            var window = GetWindow<UIPreviewWindow>("UI Preview");
            window.minSize = new Vector2(400, 500);
        }

        private void OnEnable()
        {
            mode = (Mode)EditorPrefs.GetInt(PREF_PREFIX + "Mode", 0);
            windowClassName = EditorPrefs.GetString(PREF_PREFIX + "ClassName", windowClassName);
            windowId = EditorPrefs.GetString(PREF_PREFIX + "WindowId", windowId);
            scriptNamespace = EditorPrefs.GetString(PREF_PREFIX + "Namespace", scriptNamespace);
            prefabOutputPath = EditorPrefs.GetString(PREF_PREFIX + "PrefabPath", prefabOutputPath);
            scriptOutputPath = EditorPrefs.GetString(PREF_PREFIX + "ScriptPath", scriptOutputPath);
            promptOutputPath = EditorPrefs.GetString(PREF_PREFIX + "PromptPath", promptOutputPath);
            triggerPath = EditorPrefs.GetString(PREF_PREFIX + "TriggerPath", triggerPath);
            screenshotPath = EditorPrefs.GetString(PREF_PREFIX + "ScreenshotPath", screenshotPath);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetInt(PREF_PREFIX + "Mode", (int)mode);
            EditorPrefs.SetString(PREF_PREFIX + "ClassName", windowClassName);
            EditorPrefs.SetString(PREF_PREFIX + "WindowId", windowId);
            EditorPrefs.SetString(PREF_PREFIX + "Namespace", scriptNamespace);
            EditorPrefs.SetString(PREF_PREFIX + "PrefabPath", prefabOutputPath);
            EditorPrefs.SetString(PREF_PREFIX + "ScriptPath", scriptOutputPath);
            EditorPrefs.SetString(PREF_PREFIX + "PromptPath", promptOutputPath);
            EditorPrefs.SetString(PREF_PREFIX + "TriggerPath", triggerPath);
            EditorPrefs.SetString(PREF_PREFIX + "ScreenshotPath", screenshotPath);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // === Режим ===
            EditorGUILayout.LabelField("Режим", EditorStyles.boldLabel);
            mode = (Mode)EditorGUILayout.EnumPopup("Режим работы", mode);

            EditorGUILayout.Space(10);

            if (mode == Mode.CreateNew)
                DrawCreateNewMode();
            else
                DrawUpdateExistingMode();

            // === Общие настройки ===
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Мокап", EditorStyles.boldLabel);
            mockupAsset = EditorGUILayout.ObjectField("Файл мокапа", mockupAsset, typeof(UnityEngine.Object), false);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Скриншоты (Claude Code)", EditorStyles.boldLabel);
            triggerPath = EditorGUILayout.TextField("Триггер-файл", triggerPath);
            screenshotPath = EditorGUILayout.TextField("Скриншот", screenshotPath);

            EditorGUILayout.Space(5);
            promptOutputPath = DrawFolderField("Папка промптов", promptOutputPath);

            // === Автосохранение ===
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Автосохранение Play Mode → Prefab", EditorStyles.boldLabel);

            bool autoSave = EditorPrefs.GetBool("ProtoSystem.UIPreview.AutoSave", false);
            bool newAutoSave = EditorGUILayout.Toggle("Включить автосохранение", autoSave);
            if (newAutoSave != autoSave)
            {
                EditorPrefs.SetBool("ProtoSystem.UIPreview.AutoSave", newAutoSave);
            }

            if (newAutoSave)
            {
                EditorGUILayout.HelpBox(
                    "При выходе из Play Mode UI будет автоматически сохранён в prefab.\n" +
                    "Укажите имя объекта в сцене и путь к prefab.",
                    MessageType.Info
                );

                string targetObject = EditorPrefs.GetString("ProtoSystem.UIPreview.TargetObject", "MyWindow");
                string newTargetObject = EditorGUILayout.TextField("Объект в сцене", targetObject);
                if (newTargetObject != targetObject)
                {
                    EditorPrefs.SetString("ProtoSystem.UIPreview.TargetObject", newTargetObject);
                }

                // Автоматически определяем путь к prefab из режима
                string autoPrefabPath = "";
                if (mode == Mode.UpdateExisting && existingPrefab != null)
                {
                    autoPrefabPath = AssetDatabase.GetAssetPath(existingPrefab);
                }
                else if (mode == Mode.CreateNew && !string.IsNullOrEmpty(windowClassName))
                {
                    autoPrefabPath = $"{prefabOutputPath}/{windowClassName}.prefab";
                }

                if (!string.IsNullOrEmpty(autoPrefabPath))
                {
                    EditorGUILayout.LabelField("Целевой prefab", autoPrefabPath);
                    EditorPrefs.SetString("ProtoSystem.UIPreview.TargetPrefab", autoPrefabPath);
                }
                else
                {
                    EditorGUILayout.HelpBox("Укажите префаб выше", MessageType.Warning);
                }
            }

            EditorGUILayout.Space(15);

            // === Кнопки ===
            DrawActions();

            // === Результат ===
            DrawResult();

            EditorGUILayout.EndScrollView();
        }

        #region Mode-specific GUI

        private void DrawCreateNewMode()
        {
            EditorGUILayout.LabelField("Новое окно", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            windowClassName = EditorGUILayout.TextField("Имя класса", windowClassName);
            if (EditorGUI.EndChangeCheck())
                windowId = ToSnakeCase(windowClassName.Replace("Window", ""));

            windowId = EditorGUILayout.TextField("Window ID", windowId);
            scriptNamespace = EditorGUILayout.TextField("Namespace", scriptNamespace);

            EditorGUILayout.Space(5);
            prefabOutputPath = DrawFolderField("Папка префабов", prefabOutputPath);
            scriptOutputPath = DrawFolderField("Папка скриптов", scriptOutputPath);
        }

        private void DrawUpdateExistingMode()
        {
            EditorGUILayout.LabelField("Существующий префаб", EditorStyles.boldLabel);
            existingPrefab = (GameObject)EditorGUILayout.ObjectField("Префаб", existingPrefab, typeof(GameObject), false);

            if (existingPrefab != null)
            {
                var path = AssetDatabase.GetAssetPath(existingPrefab);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
                {
                    EditorGUILayout.HelpBox("Выберите ассет-префаб, не объект сцены.", MessageType.Warning);
                }
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Действия", EditorStyles.boldLabel);

            bool isPlayMode = EditorApplication.isPlaying;

            if (mode == Mode.CreateNew)
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(windowClassName) || isPlayMode))
                {
                    if (GUILayout.Button("🎬 Создать сцену + префаб + скрипт", GUILayout.Height(30)))
                    {
                        SavePrefs();
                        CreateAll();
                    }
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(existingPrefab == null || isPlayMode))
                {
                    if (GUILayout.Button("🎬 Создать/обновить превью-сцену", GUILayout.Height(30)))
                    {
                        SavePrefs();
                        CreatePreviewScene(existingPrefab);
                    }
                }
            }

            if (isPlayMode)
            {
                EditorGUILayout.HelpBox("Остановите Play Mode для создания сцены", MessageType.Warning);
            }

            EditorGUILayout.Space(5);

            bool canGeneratePrompt = mockupAsset != null &&
                (mode == Mode.CreateNew ? !string.IsNullOrEmpty(windowClassName) : existingPrefab != null);

            using (new EditorGUI.DisabledScope(!canGeneratePrompt))
            {
                if (GUILayout.Button("📝 Сгенерировать промпт", GUILayout.Height(30)))
                {
                    SavePrefs();
                    GeneratePrompt();
                }
            }

            EditorGUILayout.Space(5);

            // Кнопка ручного сохранения из Play Mode
            bool hasSnapshot = File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".claude/ui_hierarchy_snapshot.json"));
            string snapshotButtonText = hasSnapshot ? "💾 Применить снимок к префабу" : "💾 Применить снимок (нет данных)";

            using (new EditorGUI.DisabledScope(!hasSnapshot || EditorApplication.isPlaying))
            {
                if (GUILayout.Button(snapshotButtonText, GUILayout.Height(25)))
                {
                    ApplySnapshotManually();
                }
            }

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Выйдите из Play Mode для применения снимка", MessageType.Info);
            }
        }

        private void DrawResult()
        {
            if (string.IsNullOrEmpty(generatedPromptPath)) return;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Результат", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Промпт:", generatedPromptPath);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Команда запуска:");

            var style = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.TextArea(launchCommand, style, GUILayout.Height(40));

            if (GUILayout.Button("📋 Копировать команду"))
            {
                EditorGUIUtility.systemCopyBuffer = launchCommand;
                Debug.Log("[UIPreview] Команда скопирована в буфер обмена.");
            }
        }

        #endregion

        #region Creation

        private void CreateAll()
        {
            CreateWindowScript();
            CreateBasePrefab();

            // Загружаем созданный префаб для сцены
            var prefabPath = $"{prefabOutputPath}/{windowClassName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            CreatePreviewScene(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[UIPreview] Создано: сцена, префаб, скрипт для {windowClassName}");
        }

        private void CreateWindowScript()
        {
            EnsureFolder(scriptOutputPath);
            var path = $"{scriptOutputPath}/{windowClassName}.cs";

            if (File.Exists(path))
            {
                if (!EditorUtility.DisplayDialog("Скрипт существует",
                    $"{windowClassName}.cs уже существует. Перезаписать?", "Да", "Нет"))
                    return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"using UnityEngine;");
            sb.AppendLine($"using UnityEngine.UI;");
            sb.AppendLine($"using TMPro;");
            sb.AppendLine($"using ProtoSystem.UI;");
            sb.AppendLine();
            sb.AppendLine($"namespace {scriptNamespace}");
            sb.AppendLine($"{{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {windowClassName} — сгенерировано UIPreviewWindow");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    [UIWindow(\"{windowId}\", WindowType.Normal, WindowLayer.Windows)]");
            sb.AppendLine($"    public class {windowClassName} : UIWindowBase");
            sb.AppendLine($"    {{");
            sb.AppendLine($"        protected override void OnShow() {{ }}");
            sb.AppendLine($"        protected override void OnHide() {{ }}");
            sb.AppendLine($"    }}");
            sb.AppendLine($"}}");

            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[UIPreview] Скрипт создан: {path}");
        }

        private void CreateBasePrefab()
        {
            EnsureFolder(prefabOutputPath);
            var prefabPath = $"{prefabOutputPath}/{windowClassName}.prefab";

            var root = new GameObject(windowClassName);
            var rect = root.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1920, 1080);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            root.AddComponent<CanvasGroup>();

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Префаб существует",
                    $"{windowClassName}.prefab уже существует. Перезаписать?", "Да", "Нет"))
                {
                    DestroyImmediate(root);
                    return;
                }
                AssetDatabase.DeleteAsset(prefabPath);
            }

            // Сохраняем базовый префаб
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);

            Debug.Log($"[UIPreview] Префаб создан: {prefabPath}");

            // IMPORTANT: Add UIWindow component after compilation
            // This will be done automatically when the scene is loaded in Play Mode
            EditorApplication.delayCall += () =>
            {
                AddUIWindowComponentToPrefab(prefabPath);
            };
        }

        private void AddUIWindowComponentToPrefab(string prefabPath)
        {
            // Wait for script compilation
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += () => AddUIWindowComponentToPrefab(prefabPath);
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[UIPreview] Prefab not found: {prefabPath}");
                return;
            }

            // Try to find the UIWindow type
            string fullTypeName = $"{scriptNamespace}.{windowClassName}";
            System.Type windowType = null;

            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                windowType = assembly.GetType(fullTypeName);
                if (windowType != null) break;
            }

            if (windowType == null)
            {
                Debug.LogWarning($"[UIPreview] UIWindow type not found: {fullTypeName}. Component will need to be added manually.");
                return;
            }

            // Check if component already exists
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var existingComponent = instance.GetComponent(windowType);

            if (existingComponent == null)
            {
                instance.AddComponent(windowType);
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Debug.Log($"[UIPreview] Added {windowType.Name} component to prefab");
            }

            DestroyImmediate(instance);
        }

        private void CreatePreviewScene(GameObject prefab)
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Ошибка",
                    "Невозможно создать сцену в Play Mode.\nОстановите Play Mode и попробуйте снова.",
                    "OK");
                Debug.LogError("[UIPreview] Создание сцены заблокировано: Unity находится в Play Mode");
                return;
            }

            EnsureFolder(Path.GetDirectoryName(PREVIEW_SCENE_PATH));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var cameraGO = new GameObject("Main Camera");
            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            camera.orthographic = true;
            cameraGO.tag = "MainCamera";

            // EventSystem
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif

            // Canvas
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Инстанс префаба на Canvas
            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGO.transform);
                var instanceRect = instance.GetComponent<RectTransform>();
                if (instanceRect != null)
                {
                    instanceRect.anchorMin = Vector2.zero;
                    instanceRect.anchorMax = Vector2.one;
                    instanceRect.offsetMin = Vector2.zero;
                    instanceRect.offsetMax = Vector2.zero;
                }
            }

            // UIPreviewCapture
            var captureGO = new GameObject("UIPreviewCapture");
            var capture = captureGO.AddComponent<UIPreviewCapture>();
            SetField(capture, "triggerFilePath", triggerPath);
            SetField(capture, "screenshotPath", screenshotPath);

            EditorSceneManager.SaveScene(scene, PREVIEW_SCENE_PATH);
            Debug.Log($"[UIPreview] Сцена создана: {PREVIEW_SCENE_PATH}");
        }

        #endregion

        #region Prompt Generation

        private void GeneratePrompt()
        {
            EnsureFolder(promptOutputPath);

            var projectPath = Path.GetDirectoryName(Application.dataPath);

            // Определяем пути в зависимости от режима
            string prefabRelPath, scriptRelPath, displayName;

            if (mode == Mode.UpdateExisting)
            {
                prefabRelPath = AssetDatabase.GetAssetPath(existingPrefab);
                displayName = existingPrefab.name;

                // Ищем скрипт UIWindowBase-наследника на префабе
                var windowComponent = existingPrefab.GetComponent<UIWindowBase>();
                if (windowComponent != null)
                {
                    var script = MonoScript.FromMonoBehaviour(windowComponent);
                    scriptRelPath = AssetDatabase.GetAssetPath(script);
                }
                else
                {
                    scriptRelPath = null;
                }
            }
            else
            {
                prefabRelPath = $"{prefabOutputPath}/{windowClassName}.prefab";
                scriptRelPath = $"{scriptOutputPath}/{windowClassName}.cs";
                displayName = windowClassName;
            }

            var mockupRelPath = AssetDatabase.GetAssetPath(mockupAsset);
            var absoluteMockupPath = Path.Combine(projectPath, mockupRelPath).Replace("\\", "/");
            var absolutePrefabPath = Path.Combine(projectPath, prefabRelPath).Replace("\\", "/");
            var absoluteScriptPath = !string.IsNullOrEmpty(scriptRelPath)
                ? Path.Combine(projectPath, scriptRelPath).Replace("\\", "/")
                : "";

            // Путь к расширенному шаблону
            var templatePath = Path.Combine(projectPath, ".claude", "prompt_template_enhanced.md");

            string promptContent;

            // Проверяем наличие расширенного шаблона
            if (File.Exists(templatePath))
            {
                // Используем расширенный шаблон
                promptContent = File.ReadAllText(templatePath);

                // Заменяем плейсхолдеры
                promptContent = promptContent.Replace("{mockup_path}", absoluteMockupPath);
                promptContent = promptContent.Replace("{prefab_path}", absolutePrefabPath);
                promptContent = promptContent.Replace("{script_path}", absoluteScriptPath);
                promptContent = promptContent.Replace("{window_name}", displayName);

                // Извлекаем имя компонента из пути к скрипту
                var componentName = !string.IsNullOrEmpty(scriptRelPath)
                    ? Path.GetFileNameWithoutExtension(scriptRelPath).ToLower()
                    : displayName.ToLower();
                promptContent = promptContent.Replace("{component_name}", componentName);

                // Путь к логу Unity (Windows)
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Unity", "Editor", "Editor.log"
                ).Replace("\\", "/");
                promptContent = promptContent.Replace("{log_path}", logPath);

                Debug.Log($"[UIPreview] Использован расширенный шаблон: {templatePath}");
            }
            else
            {
                // Fallback на базовый prompt
                var sb = new StringBuilder();
                sb.AppendLine($"# Задача: привести UI окно {displayName} к виду мокапа");
                sb.AppendLine();
                sb.AppendLine($"## Файлы");
                sb.AppendLine($"- Мокап: {absoluteMockupPath}");
                sb.AppendLine($"- Префаб: {absolutePrefabPath}");

                if (!string.IsNullOrEmpty(scriptRelPath))
                {
                    sb.AppendLine($"- Скрипт: {absoluteScriptPath}");
                }

                sb.AppendLine();
                sb.AppendLine($"## Контекст");
                sb.AppendLine($"- Unity запущена в Play Mode на сцене {PREVIEW_SCENE_PATH}");
                sb.AppendLine($"- Префаб инстанцирован на Canvas");
                sb.AppendLine($"- Изменения в префабе подхватываются автоматически (Hot Reload)");

                if (!string.IsNullOrEmpty(scriptRelPath))
                    sb.AppendLine($"- Скрипт окна наследует UIWindowBase (ProtoSystem.UI)");

                sb.AppendLine($"- Для скриншота: создай файл `{triggerPath}`, жди появления `{screenshotPath}`");
                sb.AppendLine();
                sb.AppendLine($"## Правила");
                sb.AppendLine($"- Правь ТОЛЬКО префаб {displayName}.prefab — он привязан к сцене");
                sb.AppendLine($"- Используй RectTransform, Image, TextMeshProUGUI, Button, Slider и т.д.");

                if (!string.IsNullOrEmpty(scriptRelPath))
                    sb.AppendLine($"- Скрипт можно расширять (добавлять SerializeField)");

                sb.AppendLine($"- Не трогай сцену UIPreview.unity");
                sb.AppendLine();
                sb.AppendLine($"## Цикл");
                sb.AppendLine($"1. Проанализируй мокап. Внеси правки в префаб.");
                sb.AppendLine($"2. Сделай скриншот:");
                sb.AppendLine($"   ```bash");
                sb.AppendLine($"   touch {triggerPath} && while [ ! -f {screenshotPath} ]; do sleep 0.5; done");
                sb.AppendLine($"   ```");
                sb.AppendLine($"3. Сравни скриншот с мокапом. Оцени схожесть в %.");
                sb.AppendLine($"4. Условия остановки:");
                sb.AppendLine($"   - ≥95% и не растёт → стоп (успех)");
                sb.AppendLine($"   - Не растёт 3 итерации → стоп с отчётом");
                sb.AppendLine($"   - Иначе → вернуться к п.1");
                sb.AppendLine();
                sb.AppendLine($"## Лог");
                sb.AppendLine($"Веди таблицу: итерация | изменения | % схожести");

                promptContent = sb.ToString();

                Debug.LogWarning($"[UIPreview] Расширенный шаблон не найден: {templatePath}. Использован базовый prompt.");
            }

            var promptFileName = $"ui-preview-{displayName.ToLower()}.md";
            var promptPath = $"{promptOutputPath}/{promptFileName}";
            var absolutePromptPath = Path.Combine(projectPath, promptPath).Replace("\\", "/");

            File.WriteAllText(Path.Combine(projectPath, promptPath), promptContent);
            AssetDatabase.Refresh();

            generatedPromptPath = promptPath;
            launchCommand = $"claude --prompt-file \"{absolutePromptPath}\"";

            Debug.Log($"[UIPreview] Промпт создан: {promptPath}");
            Repaint();
        }

        #endregion

        #region Helpers

        private static string DrawFolderField(string label, string currentPath)
        {
            EditorGUILayout.BeginHorizontal();
            currentPath = EditorGUILayout.TextField(label, currentPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var selected = EditorUtility.OpenFolderPanel($"Выберите папку для {label}", currentPath, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    var dataPath = Application.dataPath;
                    if (selected.StartsWith(dataPath))
                        currentPath = "Assets" + selected.Substring(dataPath.Length);
                    else
                        currentPath = selected;
                }
            }
            EditorGUILayout.EndHorizontal();
            return currentPath;
        }

        private static string ToSnakeCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var sb = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                var c = input[i];
                if (char.IsUpper(c) && i > 0)
                    sb.Append('_');
                sb.Append(char.ToLower(c));
            }
            return sb.ToString();
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            path = path.Replace("\\", "/");

            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            var current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void SetField(Component component, string fieldName, object value)
        {
            var field = component.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(component, value);
                EditorUtility.SetDirty(component);
            }
        }

        private void ApplySnapshotManually()
        {
            string targetPrefabPath = EditorPrefs.GetString("ProtoSystem.UIPreview.TargetPrefab", "");

            if (string.IsNullOrEmpty(targetPrefabPath))
            {
                if (mode == Mode.UpdateExisting && existingPrefab != null)
                {
                    targetPrefabPath = AssetDatabase.GetAssetPath(existingPrefab);
                }
                else if (mode == Mode.CreateNew && !string.IsNullOrEmpty(windowClassName))
                {
                    targetPrefabPath = $"{prefabOutputPath}/{windowClassName}.prefab";
                }
            }

            if (string.IsNullOrEmpty(targetPrefabPath))
            {
                EditorUtility.DisplayDialog("Ошибка", "Не указан целевой prefab", "OK");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Ошибка", $"Prefab не найден: {targetPrefabPath}", "OK");
                return;
            }

            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string snapshotPath = Path.Combine(projectPath, ".claude/ui_hierarchy_snapshot.json");

            if (!File.Exists(snapshotPath))
            {
                EditorUtility.DisplayDialog("Ошибка", "Файл снимка не найден. Выйдите из Play Mode с включённым автосохранением.", "OK");
                return;
            }

            try
            {
                // Используем тот же код, что и в UIPreviewPlayModeSaver
                // Загружаем snapshot (без десериализации, просто передаём путь)
                System.Type saverType = System.Type.GetType("ProtoSystem.UI.UIPreviewPlayModeSaver, ProtoSystem.Core.Editor");
                if (saverType == null)
                {
                    Debug.LogError("UIPreviewPlayModeSaver type not found");
                    return;
                }

                // Вызываем статический метод через рефлексию (приватный метод)
                var method = saverType.GetMethod("ApplySnapshotToPrefab",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic);

                if (method != null)
                {
                    method.Invoke(null, null);
                }
                else
                {
                    // Fallback: делаем вручную
                    ApplySnapshotToPrefabDirect(snapshotPath, targetPrefabPath, prefab);
                }

                EditorUtility.DisplayDialog("Успех", $"Снимок применён к prefab:\n{targetPrefabPath}", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Ошибка", $"Не удалось применить снимок:\n{e.Message}", "OK");
                Debug.LogError(e);
            }
        }

        private void ApplySnapshotToPrefabDirect(string snapshotPath, string prefabPath, GameObject prefab)
        {
            // Простая реализация на случай если рефлексия не сработает
            Debug.LogWarning("Using direct snapshot application (reflection failed)");

            // Создаём временный объект из prefab
            GameObject tempInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            // Очищаем все дочерние объекты
            Transform root = tempInstance.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(root.GetChild(i).gameObject);
            }

            // Здесь должна быть десериализация и применение, но это сложно без доступа к классам
            // Лучше использовать метод из UIPreviewPlayModeSaver

            // Сохраняем обратно в prefab
            PrefabUtility.SaveAsPrefabAsset(tempInstance, prefabPath);
            DestroyImmediate(tempInstance);

            AssetDatabase.Refresh();
        }

        #endregion
    }
}
