// Packages/com.protosystem.core/Runtime/UI/Tools/UIPreviewCapture.cs
using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProtoSystem.UI
{
    [Serializable]
    public class UIPreviewConfig
    {
        public string trigger_path = "/tmp/take_screenshot";
        public string screenshot_path = "/tmp/ui_screenshot.png";
        public float capture_delay = 0.5f;
        public float status_log_interval = 3f;
    }

    /// <summary>
    /// Компонент для сцены UIPreview.
    /// Следит за триггер-файлом и делает скриншот окна.
    /// Используется для итеративной разработки UI с Claude Code.
    /// Конфигурация читается из .claude/ui_preview_config.json
    /// </summary>
    public class UIPreviewCapture : MonoBehaviour
    {
        [Header("Config File")]
        [Tooltip("Путь к JSON конфигу относительно корня проекта")]
        [SerializeField] private string configPath = ".claude/ui_preview_config.json";

        [Header("Commands File")]
        [Tooltip("Путь к файлу команд относительно корня проекта")]
        [SerializeField] private string commandsPath = ".claude/ui_preview_commands.txt";

        [Tooltip("Путь к файлу результатов команд")]
        [SerializeField] private string commandOutputPath = ".claude/command_output.txt";

        [Tooltip("Интервал проверки команд (секунды)")]
        [SerializeField] private float commandCheckInterval = 0.5f;

        [Header("Fallback Settings (if config not found)")]
        [SerializeField] private string fallbackTriggerPath = "/tmp/take_screenshot";
        [SerializeField] private string fallbackScreenshotPath = "/tmp/ui_screenshot.png";
        [SerializeField] private float fallbackCaptureDelay = 0.5f;
        [SerializeField] private float fallbackStatusLogInterval = 3f;

        [Header("Runtime")]
        [Tooltip("Суперсэмплинг для качества скриншота")]
        [SerializeField] private int superSize = 1;

        private string _triggerFilePath;
        private string _screenshotPath;
        private float _captureDelay;
        private float _statusLogInterval;
        private float _pendingCaptureTime = -1f;
        private float _lastStatusLogTime = 0f;
        private float _lastConfigCheckTime = 0f;
        private float _lastCommandCheckTime = 0f;
        private const float CONFIG_RELOAD_INTERVAL = 5f;
        private System.IO.StreamWriter _outputWriter;

        private void Start()
        {
            LoadConfig();

            // ВАЖНО: UIWindowBase скрывает окна при Awake (alpha = 0)
            // Для корректных скриншотов делаем все окна видимыми
            ForceShowAllWindows();

            LogStatus("UIPreviewCapture started");
        }

        private void ForceShowAllWindows()
        {
            #if UNITY_EDITOR
            // Ищем все UIWindowBase компоненты в сцене
            var windows = FindObjectsOfType<UIWindowBase>();

            foreach (var window in windows)
            {
                var canvasGroup = window.GetComponent<CanvasGroup>();
                if (canvasGroup != null && canvasGroup.alpha < 0.1f)
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                    Debug.Log($"[UIPreviewCapture] Made window visible: {window.name}");
                }
            }
            #endif
        }

        private void Update()
        {
            // Проверка команд
            if (Time.unscaledTime - _lastCommandCheckTime >= commandCheckInterval)
            {
                _lastCommandCheckTime = Time.unscaledTime;
                CheckCommands();
            }

            // Периодическая перезагрузка конфига (каждые 5 секунд)
            if (Time.unscaledTime - _lastConfigCheckTime >= CONFIG_RELOAD_INTERVAL)
            {
                _lastConfigCheckTime = Time.unscaledTime;
                LoadConfig();
            }

            // Периодическое логирование состояния
            if (_statusLogInterval > 0 && Time.unscaledTime - _lastStatusLogTime >= _statusLogInterval)
            {
                _lastStatusLogTime = Time.unscaledTime;
                LogStatus("Heartbeat");
            }

            // Проверяем наличие триггер-файла
            if (File.Exists(_triggerFilePath))
            {
                // Удаляем триггер сразу, чтобы не обработать повторно
                File.Delete(_triggerFilePath);

                // Удаляем старый скриншот если есть
                if (File.Exists(_screenshotPath))
                    File.Delete(_screenshotPath);

                // Ставим захват с задержкой
                _pendingCaptureTime = Time.unscaledTime + _captureDelay;
                Debug.Log($"[UIPreviewCapture] Trigger detected. Screenshot in {_captureDelay}s...");
            }

            // Выполняем захват по таймеру
            if (_pendingCaptureTime > 0 && Time.unscaledTime >= _pendingCaptureTime)
            {
                _pendingCaptureTime = -1f;
                CaptureScreenshot();
            }
        }

        private void CheckCommands()
        {
            string fullCommandsPath = Path.Combine(Application.dataPath, "..", commandsPath);

            if (!File.Exists(fullCommandsPath))
                return;

            try
            {
                string[] commands = File.ReadAllLines(fullCommandsPath);

                // Удаляем файл сразу после чтения, чтобы команды не выполнялись повторно
                File.Delete(fullCommandsPath);

                foreach (string command in commands)
                {
                    string cmd = command.Trim();
                    if (string.IsNullOrEmpty(cmd) || cmd.StartsWith("#"))
                        continue;

                    ExecuteCommand(cmd);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UIPreviewCapture] Failed to read commands: {e.Message}");
            }
        }

        private void StartOutputCapture()
        {
            string fullOutputPath = Path.Combine(Application.dataPath, "..", commandOutputPath);
            try
            {
                var dir = Path.GetDirectoryName(fullOutputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                _outputWriter = new System.IO.StreamWriter(fullOutputPath, false, System.Text.Encoding.UTF8);
                _outputWriter.WriteLine($"=== Command Output | {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIPreviewCapture] Failed to create output file: {e.Message}");
            }
        }

        private void WriteOutput(string line)
        {
            if (_outputWriter != null)
            {
                _outputWriter.WriteLine(line);
            }
            Debug.Log(line);
        }

        private void EndOutputCapture()
        {
            if (_outputWriter != null)
            {
                _outputWriter.WriteLine("=== End Output ===");
                _outputWriter.Flush();
                _outputWriter.Close();
                _outputWriter = null;
            }
        }

        private void ExecuteCommand(string command)
        {
            Debug.Log($"[UIPreviewCapture] Executing command: {command}");

            StartOutputCapture();

            // Parse command with arguments
            string[] parts = command.Split(new[] { ' ' }, 2);
            string cmd = parts[0].ToLower();
            string args = parts.Length > 1 ? parts[1] : "";

            switch (cmd)
            {
                case "refresh":
                case "reload":
                    #if UNITY_EDITOR
                    Debug.Log("[UIPreviewCapture] Refreshing AssetDatabase...");
                    AssetDatabase.Refresh();
                    #endif
                    break;

                case "screenshot":
                case "capture":
                    Debug.Log("[UIPreviewCapture] Taking screenshot immediately...");
                    _pendingCaptureTime = Time.unscaledTime + 0.1f;
                    break;

                case "reload_config":
                case "config":
                    Debug.Log("[UIPreviewCapture] Reloading config...");
                    LoadConfig();
                    break;

                case "status":
                    LogStatus("Manual status check");
                    break;

                case "scene":
                case "hierarchy":
                    PrintSceneHierarchy();
                    break;

                case "inspect":
                    InspectGameObject(args);
                    break;

                case "add_component":
                    AddComponentToObject(args);
                    break;

                case "find":
                    FindGameObject(args);
                    break;

                case "save_snapshot":
                case "save_prefab":
                    SaveUISnapshot(args);
                    break;

                case "set_parent":
                case "reparent":
                    SetParent(args);
                    break;

                case "destroy":
                case "delete":
                    DestroyObject(args);
                    break;

                case "verify_structure":
                case "check_structure":
                    VerifyAndFixStructure(args);
                    break;

                default:
                    WriteOutput($"[UIPreviewCapture] Unknown command: {command}");
                    break;
            }

            EndOutputCapture();
        }

        private void PrintSceneHierarchy()
        {
            WriteOutput("=== Scene Hierarchy ===");
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                PrintGameObjectRecursive(root.transform, 0);
            }
            WriteOutput("=== End Hierarchy ===");
        }

        private void PrintGameObjectRecursive(Transform t, int depth)
        {
            string indent = new string(' ', depth * 2);
            string active = t.gameObject.activeSelf ? "✓" : "✗";
            var components = t.GetComponents<Component>();
            string componentList = string.Join(", ", System.Array.ConvertAll(components, c => c.GetType().Name));

            WriteOutput($"{indent}[{active}] {t.name} ({componentList})");

            for (int i = 0; i < t.childCount; i++)
            {
                PrintGameObjectRecursive(t.GetChild(i), depth + 1);
            }
        }

        private void InspectGameObject(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                WriteOutput("[UIPreviewCapture] inspect: No object name provided");
                return;
            }

            GameObject obj = GameObject.Find(name);
            if (obj == null)
            {
                WriteOutput($"[UIPreviewCapture] Object not found: {name}");
                return;
            }

            WriteOutput($"=== Inspecting: {name} ===");
            WriteOutput($"Active: {obj.activeSelf}");
            WriteOutput($"Layer: {LayerMask.LayerToName(obj.layer)}");
            WriteOutput($"Tag: {obj.tag}");

            var components = obj.GetComponents<Component>();
            WriteOutput($"Components ({components.Length}):");
            foreach (var comp in components)
            {
                if (comp == null) continue;
                WriteOutput($"  - {comp.GetType().FullName}");

                // Special handling for common components
                if (comp is RectTransform rt)
                {
                    WriteOutput($"    RectTransform: pos={rt.anchoredPosition}, size={rt.sizeDelta}, anchors=[{rt.anchorMin},{rt.anchorMax}]");
                }
                else if (comp is UnityEngine.UI.Image img)
                {
                    WriteOutput($"    Image: color={img.color}, sprite={img.sprite?.name ?? "null"}");
                }
            }

            WriteOutput($"Children: {obj.transform.childCount}");
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                WriteOutput($"  - {obj.transform.GetChild(i).name}");
            }
            WriteOutput("=== End Inspect ===");
        }

        private void FindGameObject(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                Debug.LogWarning("[UIPreviewCapture] find: No pattern provided");
                return;
            }

            Debug.Log($"=== Finding objects matching: {pattern} ===");
            var allObjects = GameObject.FindObjectsOfType<GameObject>();
            int count = 0;
            foreach (var obj in allObjects)
            {
                if (obj.name.ToLower().Contains(pattern.ToLower()))
                {
                    string path = GetGameObjectPath(obj.transform);
                    Debug.Log($"  - {path} ({obj.GetComponents<Component>().Length} components)");
                    count++;
                }
            }
            Debug.Log($"=== Found {count} objects ===");
        }

        private void AddComponentToObject(string args)
        {
            // Format: "ObjectName ComponentTypeName"
            string[] parts = args.Split(new[] { ' ' }, 2);
            if (parts.Length < 2)
            {
                Debug.LogWarning("[UIPreviewCapture] add_component: Format is 'ObjectName ComponentType'");
                return;
            }

            string objectName = parts[0];
            string componentType = parts[1];

            GameObject obj = GameObject.Find(objectName);
            if (obj == null)
            {
                Debug.LogWarning($"[UIPreviewCapture] Object not found: {objectName}");
                return;
            }

            // Try to find the type
            System.Type type = null;

            // Common namespaces to try
            string[] namespaces = {
                "LastConvoy.UI",
                "UnityEngine",
                "UnityEngine.UI",
                "TMPro",
                "ProtoSystem.UI"
            };

            foreach (string ns in namespaces)
            {
                string fullTypeName = string.IsNullOrEmpty(ns) ? componentType : $"{ns}.{componentType}";
                type = System.Type.GetType(fullTypeName);
                if (type == null)
                {
                    // Try to find in all loaded assemblies
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType(fullTypeName);
                        if (type != null) break;
                    }
                }
                if (type != null) break;
            }

            if (type == null)
            {
                Debug.LogWarning($"[UIPreviewCapture] Component type not found: {componentType}");
                return;
            }

            if (!typeof(Component).IsAssignableFrom(type))
            {
                Debug.LogWarning($"[UIPreviewCapture] Type is not a Component: {componentType}");
                return;
            }

            Component comp = obj.AddComponent(type);
            Debug.Log($"[UIPreviewCapture] Added {type.Name} to {objectName}");
        }

        private string GetGameObjectPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        private void LoadConfig()
        {
            string fullConfigPath = Path.Combine(Application.dataPath, "..", configPath);

            if (File.Exists(fullConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(fullConfigPath);
                    UIPreviewConfig config = JsonUtility.FromJson<UIPreviewConfig>(json);

                    _triggerFilePath = config.trigger_path;
                    _screenshotPath = config.screenshot_path;
                    _captureDelay = config.capture_delay;
                    _statusLogInterval = config.status_log_interval;

                    Debug.Log($"[UIPreviewCapture] Config loaded from {configPath}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UIPreviewCapture] Failed to load config: {e.Message}. Using fallback values.");
                    UseFallbackValues();
                }
            }
            else
            {
                Debug.LogWarning($"[UIPreviewCapture] Config not found at {fullConfigPath}. Using fallback values.");
                UseFallbackValues();
            }
        }

        private void UseFallbackValues()
        {
            _triggerFilePath = fallbackTriggerPath;
            _screenshotPath = fallbackScreenshotPath;
            _captureDelay = fallbackCaptureDelay;
            _statusLogInterval = fallbackStatusLogInterval;
        }

        private void LogStatus(string event_name)
        {
            var triggerExists = File.Exists(_triggerFilePath);
            var screenshotExists = File.Exists(_screenshotPath);
            var pending = _pendingCaptureTime > 0;

            Debug.Log($"[UIPreviewCapture] {event_name} | " +
                     $"PlayMode=TRUE | " +
                     $"Time={Time.unscaledTime:F1}s | " +
                     $"TriggerFile={(triggerExists ? "EXISTS" : "none")} | " +
                     $"Screenshot={(screenshotExists ? "EXISTS" : "none")} | " +
                     $"Pending={pending} | " +
                     $"TriggerPath={_triggerFilePath} | " +
                     $"Scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        }

        private void CaptureScreenshot()
        {
            // Создаём директорию если нужно
            var dir = Path.GetDirectoryName(_screenshotPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            ScreenCapture.CaptureScreenshot(_screenshotPath, superSize);
            Debug.Log($"[UIPreviewCapture] Screenshot saved: {_screenshotPath}");
        }

        private void SetParent(string args)
        {
            // Формат: childName parentName
            // Пример: Text BtnPrimary
            var parts = args.Split(new[] { ' ' }, 2);
            if (parts.Length < 2)
            {
                Debug.LogWarning("[UIPreviewCapture] set_parent: Invalid arguments. Usage: set_parent <childName> <parentName>");
                return;
            }

            string childName = parts[0].Trim();
            string parentName = parts[1].Trim();

            GameObject child = GameObject.Find(childName);
            GameObject parent = GameObject.Find(parentName);

            if (child == null)
            {
                Debug.LogWarning($"[UIPreviewCapture] set_parent: Child object '{childName}' not found");
                return;
            }

            if (parent == null)
            {
                Debug.LogWarning($"[UIPreviewCapture] set_parent: Parent object '{parentName}' not found");
                return;
            }

            child.transform.SetParent(parent.transform, false);
            Debug.Log($"[UIPreviewCapture] set_parent: '{childName}' is now child of '{parentName}'");
        }

        private void DestroyObject(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                Debug.LogWarning("[UIPreviewCapture] destroy: No object name provided");
                return;
            }

            // Найти ВСЕ объекты с таким именем (для удаления дубликатов)
            var allObjects = FindObjectsOfType<GameObject>();
            int destroyedCount = 0;

            foreach (var obj in allObjects)
            {
                if (obj.name == objectName)
                {
                    Debug.Log($"[UIPreviewCapture] destroy: Destroying '{obj.name}' at path '{GetGameObjectPath(obj.transform)}'");
                    DestroyImmediate(obj);
                    destroyedCount++;
                }
            }

            if (destroyedCount == 0)
            {
                Debug.LogWarning($"[UIPreviewCapture] destroy: Object '{objectName}' not found");
            }
            else
            {
                Debug.Log($"[UIPreviewCapture] destroy: Destroyed {destroyedCount} object(s) named '{objectName}'");
            }
        }

        private void VerifyAndFixStructure(string rootObjectName)
        {
            if (string.IsNullOrEmpty(rootObjectName))
            {
                rootObjectName = "MyWindow";
            }

            GameObject root = GameObject.Find(rootObjectName);
            if (root == null)
            {
                WriteOutput($"[UIPreviewCapture] verify_structure: Root object '{rootObjectName}' not found");
                return;
            }

            WriteOutput($"[UIPreviewCapture] === VERIFYING STRUCTURE: {rootObjectName} ===");

            int issuesFound = 0;
            int issuesFixed = 0;

            // 1. Найти и удалить дубликаты
            var duplicates = FindDuplicates(root.transform);
            issuesFound += duplicates.Count;
            foreach (var duplicate in duplicates)
            {
                WriteOutput($"[UIPreviewCapture] ✗ DUPLICATE: {duplicate.name} at {GetGameObjectPath(duplicate)}");
                DestroyImmediate(duplicate.gameObject);
                issuesFixed++;
            }

            // 2. Проверить структуру кнопок (Text должен быть внутри)
            var buttons = FindAllByName(root.transform, "BtnPrimary", "BtnSecondary");
            foreach (var button in buttons)
            {
                var textChild = button.Find("Text");
                if (textChild == null)
                {
                    WriteOutput($"[UIPreviewCapture] ✗ MISSING: Button '{button.name}' has no Text child!");
                    issuesFound++;

                    // Попробовать найти Text на том же уровне
                    var parentTransform = button.parent;
                    if (parentTransform != null)
                    {
                        for (int i = 0; i < parentTransform.childCount; i++)
                        {
                            var sibling = parentTransform.GetChild(i);
                            if (sibling.name == "Text" && sibling != button)
                            {
                                // Нашли Text рядом - переместить внутрь кнопки
                                sibling.SetParent(button, false);
                                WriteOutput($"[UIPreviewCapture] ✓ FIXED: Moved Text into {button.name}");
                                issuesFixed++;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    WriteOutput($"[UIPreviewCapture] ✓ OK: Button '{button.name}' has Text child");
                }
            }

            // 3. Итоги
            WriteOutput($"[UIPreviewCapture] === VERIFICATION COMPLETE ===");
            WriteOutput($"[UIPreviewCapture] Issues found: {issuesFound}");
            WriteOutput($"[UIPreviewCapture] Issues fixed: {issuesFixed}");

            if (issuesFound == 0)
            {
                WriteOutput($"[UIPreviewCapture] ✓✓✓ STRUCTURE IS CLEAN! ✓✓✓");
            }
            else if (issuesFixed == issuesFound)
            {
                WriteOutput($"[UIPreviewCapture] ✓ ALL ISSUES FIXED!");
            }
            else
            {
                WriteOutput($"[UIPreviewCapture] ✗ {issuesFound - issuesFixed} issues remain!");
            }
        }

        private System.Collections.Generic.List<Transform> FindDuplicates(Transform root)
        {
            var duplicates = new System.Collections.Generic.List<Transform>();
            var nameToTransforms = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Transform>>();

            // Собрать все объекты и сгруппировать по имени
            CollectAllChildren(root, nameToTransforms);

            // Найти дубликаты (оставить первый, остальные пометить для удаления)
            foreach (var kvp in nameToTransforms)
            {
                if (kvp.Value.Count > 1)
                {
                    // Оставляем первый, остальные - дубликаты
                    for (int i = 1; i < kvp.Value.Count; i++)
                    {
                        duplicates.Add(kvp.Value[i]);
                    }
                }
            }

            return duplicates;
        }

        private void CollectAllChildren(Transform parent, System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Transform>> nameToTransforms)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);

                // Пропускаем TMP SubMeshes
                if (child.name.StartsWith("TMP SubMeshUI"))
                    continue;

                // Добавляем в словарь
                if (!nameToTransforms.ContainsKey(child.name))
                {
                    nameToTransforms[child.name] = new System.Collections.Generic.List<Transform>();
                }
                nameToTransforms[child.name].Add(child);

                // Рекурсивно для детей
                CollectAllChildren(child, nameToTransforms);
            }
        }

        private System.Collections.Generic.List<Transform> FindAllByName(Transform root, params string[] names)
        {
            var results = new System.Collections.Generic.List<Transform>();
            FindAllByNameRecursive(root, names, results);
            return results;
        }

        private void FindAllByNameRecursive(Transform parent, string[] names, System.Collections.Generic.List<Transform> results)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);

                foreach (var name in names)
                {
                    if (child.name == name)
                    {
                        results.Add(child);
                        break;
                    }
                }

                FindAllByNameRecursive(child, names, results);
            }
        }

        private void SaveUISnapshot(string objectName)
        {
            #if UNITY_EDITOR
            if (string.IsNullOrEmpty(objectName))
            {
                objectName = UnityEditor.EditorPrefs.GetString("ProtoSystem.UIPreview.TargetObject", "MyWindow");
            }

            GameObject targetObject = GameObject.Find(objectName);
            if (targetObject == null)
            {
                Debug.LogWarning($"[UIPreviewCapture] save_snapshot: Object '{objectName}' not found");
                return;
            }

            try
            {
                // Вызываем статический метод из UIPreviewPlayModeSaver через рефлексию
                var saverType = System.Type.GetType("ProtoSystem.UI.UIPreviewPlayModeSaver, ProtoSystem.Core.Editor");
                if (saverType != null)
                {
                    var method = saverType.GetMethod("SaveHierarchySnapshot",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.NonPublic);

                    if (method != null)
                    {
                        method.Invoke(null, null);
                        Debug.Log($"[UIPreviewCapture] Snapshot saved via UIPreviewPlayModeSaver");
                        return;
                    }
                }

                // Fallback: сохраняем маркер
                string snapshotPath = ".claude/ui_hierarchy_snapshot_trigger.txt";
                string projectPath = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                string fullPath = Path.Combine(projectPath, snapshotPath);

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, $"{objectName}\n{System.DateTime.Now}");

                Debug.LogWarning($"[UIPreviewCapture] Created snapshot trigger marker. Snapshot will be saved on Play Mode exit.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UIPreviewCapture] Failed to save snapshot: {e.Message}");
            }
            #endif
        }
    }
}
