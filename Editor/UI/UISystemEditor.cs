// Packages/com.protosystem.core/Editor/UI/UISystemEditor.cs
using System.Linq;
using UnityEngine;
using UnityEditor;
using ProtoSystem.Compat;
using ProtoSystem.Editor.UI;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Кастомный редактор для UISystem
    /// </summary>
    [CustomEditor(typeof(UISystem))]
    public class UISystemEditor : UnityEditor.Editor
    {
        private bool _showStartupSection = true;
        private bool _showCanvasSection = true;
        private bool _showBakingSection = true;
        private int _bakeWindowIndex;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var system = target as UISystem;
            
            // === Заголовок с описанием ===
            DrawSystemHeader(system);
            
            EditorGUILayout.Space(5);
            
            // === Configuration ===
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            
            var configProp = serializedObject.FindProperty("config");
            DrawFieldWithCreateButton(configProp, "Config", CreateConfig);
            
            // Показываем список prefab'ов если есть конфиг
            if (configProp.objectReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                var configObj = new SerializedObject(configProp.objectReferenceValue);
                var prefabsProp = configObj.FindProperty("windowPrefabs");
                if (prefabsProp != null)
                {
                    EditorGUILayout.PropertyField(prefabsProp, new GUIContent("Window Prefabs"), true);
                    
                    // Кнопка Scan & Add Prefabs
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("🔍 Scan & Add Prefabs", GUILayout.Height(22), GUILayout.Width(160)))
                    {
                        ScanAndAddPrefabs(configProp.objectReferenceValue as UISystemConfig);
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    
                    if (configObj.hasModifiedProperties)
                        configObj.ApplyModifiedProperties();
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(5);
            
            // === Scene Initializer ===
            EditorGUILayout.LabelField("Scene Initializer", EditorStyles.boldLabel);
            
            var initializerProp = serializedObject.FindProperty("sceneInitializerComponent");
            
            EditorGUILayout.BeginHorizontal();
            
            // Кастомное поле с валидацией - фильтруем по UISceneInitializerBase
            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUILayout.ObjectField(
                new GUIContent("Initializer"), 
                initializerProp.objectReferenceValue, 
                typeof(UISceneInitializerBase), 
                true);
            
            if (EditorGUI.EndChangeCheck())
            {
                initializerProp.objectReferenceValue = newValue;
            }
            
            if (initializerProp.objectReferenceValue == null)
            {
                if (GUILayout.Button("+ Create", GUILayout.Width(70)))
                {
                    ShowCreateInitializerMenu();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox(
                "Scene Initializer определяет какие окна открывать при старте и дополнительные переходы. " +
                "Если не указан — используются настройки Startup ниже.", 
                MessageType.Info);
            
            EditorGUILayout.Space(5);
            
            // === Graph Override ===
            EditorGUILayout.LabelField("Graph Override (optional)", EditorStyles.boldLabel);
            
            var graphProp = serializedObject.FindProperty("windowGraphOverride");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(graphProp, new GUIContent("Graph Override"));
            
            if (graphProp.objectReferenceValue != null)
            {
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = graphProp.objectReferenceValue;
                    EditorGUIUtility.PingObject(graphProp.objectReferenceValue);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox(
                "Если указан — используется этот граф.\n" +
                "Если пусто — граф строится из Config.windowPrefabs + Initializer.\n" +
                "Если и Config пуст — загружается из Resources.", 
                MessageType.None);
            
            EditorGUILayout.Space(5);
            
            // === Startup (collapsible) ===
            _showStartupSection = EditorGUILayout.Foldout(_showStartupSection, "Startup (if no Initializer)", true);
            if (_showStartupSection)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("autoOpenStartWindow"), 
                    new GUIContent("Auto Open Start Window"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("overrideStartWindowId"),
                    new GUIContent("Override Start Window ID"));
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(5);
            
            // === Canvas Settings (collapsible) ===
            _showCanvasSection = EditorGUILayout.Foldout(_showCanvasSection, "Canvas Settings", true);
            if (_showCanvasSection)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("createCanvas"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("canvasSortOrder"));
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(5);

            DrawBakingSection();

            EditorGUILayout.Space(10);

            // === Actions ===
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("🔄 Rebuild Graph", GUILayout.Height(25)))
            {
                UIWindowGraphBuilder.RebuildGraph();
            }
            
            if (GUILayout.Button("🗺️ Open Viewer", GUILayout.Height(25)))
            {
                UIWindowGraphViewer.ShowWindow();
            }
            
            if (GUILayout.Button("✓ Validate", GUILayout.Height(25)))
            {
                UIWindowGraphBuilder.ValidateGraph();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Кнопка генерации базовых окон
            if (GUILayout.Button("🎨 Generate Base Windows", GUILayout.Height(25)))
            {
                UIWindowPrefabGenerator.GenerateAllBaseWindows();
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Чем закрыть 3D-мир на старте: UISystem поднимается в общей очереди инициализации и
        /// открывает стартовое окно только в конце — до этого камера рисует голую сцену.
        /// </summary>
        private void DrawBakingSection()
        {
            _showBakingSection = EditorGUILayout.Foldout(_showBakingSection, "Запекание в сцену", true);
            if (!_showBakingSection) return;

            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox(
                "Стартовое окно открывается только в конце инициализации — до этого игрок видит " +
                "голую 3D-сцену. Лечится так: окно заставки (Splash) делаем стартовым в графе и " +
                "запекаем в сцену — оно рисуется с первого кадра, а после кадров само уходит в меню.",
                MessageType.Info);

            if (GUILayout.Button("Создать префаб окна заставки"))
                BakeTools.CreateSplashPrefab();

            EditorGUILayout.Space(6);

            // ── Запечённое окно ──
            EditorGUILayout.LabelField("Запечённое окно", EditorStyles.boldLabel);

            var graph = Resources.Load<UIWindowGraph>(BakeTools.GraphResource);
            if (graph == null)
            {
                EditorGUILayout.HelpBox("Не найден UIWindowGraph.", MessageType.Warning);
                EditorGUI.indentLevel--;
                return;
            }

            var ids = graph.windows
                .Where(w => w != null && w.prefab != null && !string.IsNullOrEmpty(w.id))
                .Select(w => w.id)
                .OrderBy(id => id)
                .ToArray();

            if (ids.Length == 0)
            {
                EditorGUILayout.HelpBox("В графе нет окон с префабами.", MessageType.Warning);
                EditorGUI.indentLevel--;
                return;
            }

            var baked = BakeTools.FindBakedWindow();
            if (baked != null)
            {
                EditorGUILayout.ObjectField("В сцене", baked, typeof(UIWindowBase), true);

                if (!(baked is SplashWindow))
                {
                    EditorGUILayout.HelpBox(
                        "У этого окна до Show() не вызывается OnBuildUI: кнопки ни на что не " +
                        "подписаны, данные не подтянуты. Игрок увидит живое на вид окно и нажмёт " +
                        "мёртвую кнопку. Для перекрытия старта запекайте окно заставки.",
                        MessageType.Warning);
                }

                if (GUILayout.Button("Убрать запечённое окно"))
                    BakeTools.UnbakeWindow(baked);

                EditorGUI.indentLevel--;
                return;
            }

            _bakeWindowIndex = Mathf.Clamp(_bakeWindowIndex, 0, ids.Length - 1);

            // По умолчанию подставляем стартовое окно графа
            if (_bakeWindowIndex == 0)
            {
                int startIndex = System.Array.IndexOf(ids, graph.startWindowId);
                if (startIndex >= 0) _bakeWindowIndex = startIndex;
            }

            _bakeWindowIndex = EditorGUILayout.Popup("Окно", _bakeWindowIndex, ids);

            string selectedId = ids[_bakeWindowIndex];
            if (selectedId != graph.startWindowId)
            {
                EditorGUILayout.HelpBox(
                    $"'{selectedId}' не стартовое окно графа (сейчас '{graph.startWindowId}') — " +
                    "UISystem погасит его при инициализации.",
                    MessageType.Warning);
            }

            if (GUILayout.Button("Запечь в текущую сцену"))
                BakeTools.BakeWindow(graph.GetWindow(selectedId));

            EditorGUI.indentLevel--;
        }

        private void DrawFieldWithCreateButton(SerializedProperty prop, string label, System.Func<Object> createFunc)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            
            if (prop.objectReferenceValue == null)
            {
                if (GUILayout.Button("🔨 Create", GUILayout.Width(80)))
                {
                    var obj = createFunc();
                    if (obj != null)
                    {
                        prop.objectReferenceValue = obj;
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSystemHeader(UISystem system)
        {
            if (system == null || string.IsNullOrEmpty(system.Description))
                return;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField($"🖼️ {system.DisplayName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(system.Description, EditorStyles.wordWrappedMiniLabel);
            
            EditorGUILayout.Space(3);
            
            // Статус
            var configProp = serializedObject.FindProperty("config");
            
            if (configProp != null && configProp.objectReferenceValue == null)
            {
                GUI.color = new Color(1f, 0.6f, 0.4f);
                EditorGUILayout.LabelField("⚠ Требуется конфиг", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
            else if (Application.isPlaying && system.IsInitializedDependencies)
            {
                GUI.color = new Color(0.5f, 0.9f, 0.5f);
                EditorGUILayout.LabelField("✓ Система активна", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(0.5f, 0.9f, 0.5f);
                EditorGUILayout.LabelField("✓ Готов к работе", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
            
            EditorGUILayout.EndVertical();
        }

        private void ShowCreateInitializerMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Create Default Initializer"), false, () =>
            {
                CreateInitializerOnTarget<DefaultUISceneInitializer>();
            });

            // Поиск ExampleGameplayInitializer через рефлексию
            var exampleType = FindExampleInitializerType();
            if (exampleType != null)
            {
                menu.AddItem(new GUIContent($"Create Example Initializer ({exampleType.Name})"), false, () =>
                {
                    CreateInitializerOnTargetByType(exampleType);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Create Example Initializer (not found)"));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Create Custom (Script)"), false, () =>
            {
                EditorUtility.DisplayDialog("Create Custom Initializer",
                    "Create a new script that inherits from UISceneInitializerBase:\n\n" +
                    "public class MySceneInitializer : UISceneInitializerBase\n" +
                    "{\n" +
                    "    public override void Initialize(UISystem uiSystem)\n" +
                    "    {\n" +
                    "        base.Initialize(uiSystem);\n" +
                    "        // Your custom logic\n" +
                    "    }\n" +
                    "}", "OK");
            });

            menu.ShowAsContext();
        }

        private System.Type FindExampleInitializerType()
        {
            // Ищем класс с названием *ExampleGameplayInitializer* или *ExampleInitializer*
            var assemblies = UnityVersionCompat.GetLoadedAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (typeof(UISceneInitializerBase).IsAssignableFrom(type) && 
                            !type.IsAbstract &&
                            (type.Name.Contains("ExampleGameplayInitializer") || type.Name.Contains("ExampleInitializer")))
                        {
                            return type;
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        private void CreateInitializerOnTargetByType(System.Type initializerType)
        {
            var uiSystem = target as UISystem;
            if (uiSystem == null) return;

            var existing = uiSystem.GetComponent(initializerType);
            if (existing != null)
            {
                serializedObject.FindProperty("sceneInitializerComponent").objectReferenceValue = existing as UISceneInitializerBase;
                serializedObject.ApplyModifiedProperties();
                return;
            }

            var component = uiSystem.gameObject.AddComponent(initializerType) as UISceneInitializerBase;
            serializedObject.FindProperty("sceneInitializerComponent").objectReferenceValue = component;
            serializedObject.ApplyModifiedProperties();

            Debug.Log($"[UISystem] Created {initializerType.Name} on {uiSystem.name}");
        }

        private void CreateInitializerOnTarget<T>() where T : UISceneInitializerBase
        {
            var uiSystem = target as UISystem;
            if (uiSystem == null) return;

            var existing = uiSystem.GetComponent<T>();
            if (existing != null)
            {
                serializedObject.FindProperty("sceneInitializerComponent").objectReferenceValue = existing;
                serializedObject.ApplyModifiedProperties();
                return;
            }

            var component = uiSystem.gameObject.AddComponent<T>();
            serializedObject.FindProperty("sceneInitializerComponent").objectReferenceValue = component;
            serializedObject.ApplyModifiedProperties();
            
            Debug.Log($"[UISystem] Created {typeof(T).Name} on {uiSystem.name}");
        }

        private UISystemConfig CreateConfig()
        {
            string folderPath = "Assets/Resources/ProtoSystem";
            
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets/Resources", "ProtoSystem");

            string assetPath = $"{folderPath}/UISystemConfig.asset";
            
            var existing = AssetDatabase.LoadAssetAtPath<UISystemConfig>(assetPath);
            if (existing != null)
            {
                EditorGUIUtility.PingObject(existing);
                return existing;
            }

            var config = ScriptableObject.CreateInstance<UISystemConfig>();
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[UISystem] Created config at {assetPath}");
            EditorGUIUtility.PingObject(config);
            
            return config;
        }

        private void ScanAndAddPrefabs(UISystemConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[UISystemEditor] Config is null");
                return;
            }

            if (config.windowPrefabLabels == null || config.windowPrefabLabels.Count == 0)
            {
                Debug.LogWarning("[UISystemEditor] No labels configured for scanning in UISystemConfig");
                return;
            }

            // Очищаем список перед сканированием
            config.windowPrefabs.Clear();

            int addedCount = 0;
            int skippedCount = 0;
            var existingGuids = new System.Collections.Generic.HashSet<string>();

            // Ищем по каждой метке
            foreach (var label in config.windowPrefabLabels)
            {
                if (string.IsNullOrWhiteSpace(label)) continue;
                
                // Ищем все prefab'ы с этой меткой
                string[] guids = AssetDatabase.FindAssets($"l:{label} t:Prefab");
                
                foreach (var guid in guids)
                {
                    if (existingGuids.Contains(guid))
                    {
                        skippedCount++;
                        continue;
                    }
                    
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    
                    if (prefab == null) continue;
                    
                    // Проверяем что это UI Window
                    var windowComponent = prefab.GetComponent<UIWindowBase>();
                    if (windowComponent == null)
                    {
                        Debug.LogWarning($"[UISystemEditor] Prefab '{prefab.name}' has label '{label}' but no UIWindowBase component - skipped");
                        continue;
                    }
                    
                    config.windowPrefabs.Add(prefab);
                    existingGuids.Add(guid);
                    addedCount++;
                    
                    Debug.Log($"[UISystemEditor] Added: {prefab.name} (label: {label})");
                }
            }

            if (addedCount > 0)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[UISystemEditor] Scan complete. Added: {addedCount}, Skipped (duplicates): {skippedCount}, Total: {config.windowPrefabs.Count}");
        }
    }
}
