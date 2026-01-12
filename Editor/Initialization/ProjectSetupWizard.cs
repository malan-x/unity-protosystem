using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProtoSystem;
using ProtoSystem.UI;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Визард первичной настройки проекта на ProtoSystem
    /// </summary>
    public class ProjectSetupWizard : EditorWindow
    {
        private enum ProjectType { Single, Multiplayer }
        private enum CameraType { ThreeD, TwoD }
        private enum RenderPipeline { Standard, URP, HDRP }
        
        // Настройки проекта
        private ProjectType _projectType = ProjectType.Single;
        private CameraType _cameraType = CameraType.ThreeD;
        private RenderPipeline _renderPipeline = RenderPipeline.Standard;
        private bool _autoDetectPipeline = true;
        
        private string _projectName = "MyGame";
        private string _namespace = "MyGame";
        private string _rootFolder = "Assets/MyGame";
        
        // Задачи
        private List<SetupTask> _tasks;
        private Vector2 _scrollPos;
        
        // Стили
        private GUIStyle _headerStyle;
        private GUIStyle _taskStyle;
        private GUIStyle _completedStyle;
        
        [MenuItem("Tools/ProtoSystem/Project Setup Wizard", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<ProjectSetupWizard>("ProtoSystem Setup");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }
        
        private void OnEnable()
        {
            LoadSettings();
            InitializeTasks();
        }
        
        private void InitializeStyles()
        {
            if (_headerStyle != null) return;
            
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 10, 10)
            };
            
            _taskStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 5, 5),
                margin = new RectOffset(0, 0, 2, 2)
            };
            
            _completedStyle = new GUIStyle(_taskStyle);
            _completedStyle.normal.textColor = Color.green;
        }
        
        private void OnGUI()
        {
            InitializeStyles();
            
            EditorGUILayout.LabelField("ProtoSystem Project Setup", _headerStyle);
            EditorGUILayout.Space(10);
            
            DrawProjectSettings();
            EditorGUILayout.Space(10);
            
            DrawTasksList();
            EditorGUILayout.Space(10);
            
            DrawActionButtons();
        }
        
        private void DrawProjectSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Project Settings", EditorStyles.boldLabel);

            // Тип проекта
            var newType = (ProjectType)EditorGUILayout.EnumPopup("Project Type", _projectType);
            if (newType != _projectType)
            {
                _projectType = newType;
                InitializeTasks(); // Пересоздать задачи
            }

            // Тип камеры
            var newCameraType = (CameraType)EditorGUILayout.EnumPopup("Camera Type", _cameraType);
            if (newCameraType != _cameraType)
            {
                _cameraType = newCameraType;
            }

            // Render Pipeline
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Render Pipeline", GUILayout.Width(146));

            if (_autoDetectPipeline)
            {
                _renderPipeline = DetectRenderPipeline();
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.EnumPopup(_renderPipeline);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                _renderPipeline = (RenderPipeline)EditorGUILayout.EnumPopup(_renderPipeline);
            }

            _autoDetectPipeline = GUILayout.Toggle(_autoDetectPipeline, "Auto", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Имя проекта
            var newName = EditorGUILayout.TextField("Project Name", _projectName);
            if (newName != _projectName)
            {
                _projectName = newName;
                _namespace = MakeValidNamespace(_projectName);
                _rootFolder = $"Assets/{_projectName}";
            }

            // Namespace
            _namespace = EditorGUILayout.TextField("Namespace", _namespace);

            // Root Folder
            EditorGUILayout.BeginHorizontal();
            _rootFolder = EditorGUILayout.TextField("Root Folder", _rootFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("Select Root Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                {
                    _rootFolder = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
        
        private void DrawTasksList()
        {
            EditorGUILayout.LabelField("Setup Tasks", EditorStyles.boldLabel);
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            foreach (var task in _tasks)
            {
                DrawTask(task);
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawTask(SetupTask task)
        {
            var style = task.IsCompleted ? _completedStyle : _taskStyle;
            
            EditorGUILayout.BeginVertical(style);
            EditorGUILayout.BeginHorizontal();
            
            // Галочка
            var icon = task.IsCompleted ? "✅" : "⬜";
            GUILayout.Label(icon, GUILayout.Width(25));
            
            // Название
            EditorGUILayout.LabelField(task.Name, EditorStyles.boldLabel);
            
            // Кнопка Execute
            GUI.enabled = !task.IsCompleted;
            if (GUILayout.Button("Execute", GUILayout.Width(80)))
            {
                ExecuteTask(task);
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            // Описание
            if (!string.IsNullOrEmpty(task.Description))
            {
                EditorGUILayout.LabelField(task.Description, EditorStyles.wordWrappedMiniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Execute All Pending", GUILayout.Height(30)))
            {
                ExecuteAllPending();
            }
            
            if (GUILayout.Button("Reset Progress", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Reset Progress", 
                    "This will reset all task completion status. Continue?", "Yes", "No"))
                {
                    ResetProgress();
                }
            }
            
            if (GUILayout.Button("Close", GUILayout.Height(30)))
            {
                Close();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void InitializeTasks()
        {
            _tasks = new List<SetupTask>
            {
                new SetupTask("Create Folder Structure", 
                    "Create Scripts, Prefabs, Scenes, Resources folders", 
                    TaskType.CreateFolders),

                new SetupTask("Generate Assembly Definition", 
                    "Create .asmdef with necessary references", 
                    TaskType.CreateAsmdef),

                new SetupTask("Create ProjectConfig", 
                    "ScriptableObject with project namespace", 
                    TaskType.CreateProjectConfig),

                new SetupTask("Generate EventBus File", 
                    "Create EventBus static class for project events", 
                    TaskType.CreateEventBus),

                new SetupTask("Generate UI Sprites", 
                    "Generate default UI button/panel sprites", 
                    TaskType.GenerateUISprites),

                new SetupTask("Generate UI Prefabs", 
                    "Create base window/panel/button prefabs", 
                    TaskType.GenerateUIPrefabs),

                new SetupTask("Create UIWindowGraph", 
                    "Create UIWindowGraph ScriptableObject asset", 
                    TaskType.CreateUIWindowGraph),

                new SetupTask("Create Example UI Initializer", 
                    "Generate ExampleGameplayInitializer.cs - code-first UI setup example", 
                    TaskType.CreateExampleUIWindows),

                new SetupTask("Create Bootstrap Scene", 
                    "Setup scene with managers", 
                    TaskType.CreateBootstrapScene)
            };

            // Добавляем задачи для мультиплеера
            if (_projectType == ProjectType.Multiplayer)
            {
                _tasks.Add(new SetupTask("Add Netcode References", 
                    "Add Unity.Netcode.Runtime to asmdef", 
                    TaskType.AddNetcodeReferences));

                _tasks.Add(new SetupTask("Setup NetworkManager", 
                    "Add NetworkManager to Bootstrap scene", 
                    TaskType.SetupNetworkManager));
            }

            // Загружаем статусы
            LoadTaskStatuses();
        }
        
        private void ExecuteTask(SetupTask task)
        {
            try
            {
                switch (task.Type)
                {
                    case TaskType.CreateFolders:
                        CreateFolderStructure();
                        break;
                    case TaskType.CreateAsmdef:
                        CreateAssemblyDefinition();
                        break;
                    case TaskType.CreateProjectConfig:
                        CreateProjectConfig();
                        break;
                    case TaskType.CreateEventBus:
                        CreateEventBus();
                        break;
                    case TaskType.GenerateUISprites:
                        GenerateUISprites();
                        break;
                    case TaskType.GenerateUIPrefabs:
                        GenerateUIPrefabs();
                        break;
                    case TaskType.CreateUIWindowGraph:
                        CreateUIWindowGraph();
                        break;
                    case TaskType.CreateExampleUIWindows:
                        CreateExampleUIWindows();
                        break;
                    case TaskType.CreateBootstrapScene:
                        CreateBootstrapScene();
                        break;
                    case TaskType.AddNetcodeReferences:
                        AddNetcodeReferences();
                        break;
                    case TaskType.SetupNetworkManager:
                        SetupNetworkManager();
                        break;
                }

                task.IsCompleted = true;
                SaveTaskStatus(task);
                SaveSettings(); // Сохранить настройки после каждой задачи

                Debug.Log($"✅ Task completed: {task.Name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Task failed: {task.Name}\n{ex.Message}");
                EditorUtility.DisplayDialog("Task Failed", $"{task.Name} failed:\n{ex.Message}", "OK");
            }
        }
        
        private void ExecuteAllPending()
        {
            var pending = _tasks.Where(t => !t.IsCompleted).ToList();
            
            if (pending.Count == 0)
            {
                EditorUtility.DisplayDialog("All Done", "All tasks are already completed!", "OK");
                return;
            }
            
            if (!EditorUtility.DisplayDialog("Execute All", 
                $"Execute {pending.Count} pending tasks?", "Yes", "No"))
                return;
            
            foreach (var task in pending)
            {
                ExecuteTask(task);
            }
            
            EditorUtility.DisplayDialog("Complete", "All tasks executed!", "OK");
        }
        
        // ==================== TASK IMPLEMENTATIONS ====================
        
        private void CreateFolderStructure()
        {
            var folders = new[]
            {
                $"{_rootFolder}/Scripts/Systems",
                $"{_rootFolder}/Scripts/Events",
                $"{_rootFolder}/Scripts/Configs",
                $"{_rootFolder}/Scripts/UI",
                $"{_rootFolder}/Prefabs/UI",
                $"{_rootFolder}/Scenes",
                $"{_rootFolder}/Resources/UI/Sprites",
                $"{_rootFolder}/Resources/UI/Prefabs"
            };
            
            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    var parts = folder.Split('/');
                    var current = parts[0];
                    
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var next = $"{current}/{parts[i]}";
                        if (!AssetDatabase.IsValidFolder(next))
                        {
                            AssetDatabase.CreateFolder(current, parts[i]);
                        }
                        current = next;
                    }
                }
            }
            
            AssetDatabase.Refresh();
        }
        
        private void CreateAssemblyDefinition()
        {
            var asmdefPath = $"{_rootFolder}/Scripts/{_namespace}.asmdef";

            var references = new List<string>
            {
                "GUID:f0916efc0967ba241b646b3544bfe86b", // ProtoSystem.Runtime
                "Unity.TextMeshPro",
                "Unity.InputSystem" // Опциональная ссылка на Input System
            };

            if (_projectType == ProjectType.Multiplayer)
            {
                references.Add("Unity.Netcode.Runtime");
            }

            // Ручная сериализация JSON для корректного формата
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"    \"name\": \"{_namespace}\",");
            sb.AppendLine($"    \"rootNamespace\": \"{_namespace}\",");
            sb.Append("    \"references\": [");
            for (int i = 0; i < references.Count; i++)
            {
                sb.Append($"\"{references[i]}\"");
                if (i < references.Count - 1) sb.Append(", ");
            }
            sb.AppendLine("],");
            sb.AppendLine("    \"includePlatforms\": [],");
            sb.AppendLine("    \"excludePlatforms\": [],");
            sb.AppendLine("    \"allowUnsafeCode\": false,");
            sb.AppendLine("    \"overrideReferences\": false,");
            sb.AppendLine("    \"precompiledReferences\": [],");
            sb.AppendLine("    \"autoReferenced\": true,");
            sb.AppendLine("    \"defineConstraints\": [],");
            sb.AppendLine("    \"versionDefines\": [");
            sb.AppendLine("        {");
            sb.AppendLine("            \"name\": \"com.unity.inputsystem\",");
            sb.AppendLine("            \"expression\": \"\",");
            sb.AppendLine("            \"define\": \"PROTO_HAS_INPUT_SYSTEM\"");
            sb.AppendLine("        }");
            sb.AppendLine("    ],");
            sb.AppendLine("    \"noEngineReferences\": false");
            sb.AppendLine("}");

            File.WriteAllText(asmdefPath, sb.ToString());
            AssetDatabase.Refresh();
        }
        
        private void CreateProjectConfig()
        {
            var config = ScriptableObject.CreateInstance<ProjectConfig>();
            config.projectNamespace = _namespace;
            
            var path = $"{_rootFolder}/Resources/ProjectConfig.asset";
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
        }
        
                private void CreateEventBus()
                {
                    // Используем встроенную функцию ProtoSystem
                    string createdPath = EventBusEditorUtils.CreateEventBusFile(_namespace);

                    if (!string.IsNullOrEmpty(createdPath))
                    {
                        Debug.Log($"✅ EventBus file created: {createdPath}");
                    }
                    else
                    {
                        Debug.LogError("❌ Failed to create EventBus file");
                    }
                }
        
        private void CreateUIWindowGraph()
        {
            // Путь: Assets/Resources/ProtoSystem/UIWindowGraph.asset
            const string RESOURCE_PATH = "Assets/Resources/ProtoSystem";
            const string ASSET_PATH = "Assets/Resources/ProtoSystem/UIWindowGraph.asset";
            
            // Проверка существования
            if (AssetDatabase.LoadAssetAtPath<UIWindowGraph>(ASSET_PATH) != null)
            {
                Debug.Log("UIWindowGraph already exists");
                return;
            }
            
            // Создать директории
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            if (!AssetDatabase.IsValidFolder("Assets/Resources/ProtoSystem"))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "ProtoSystem");
            }
            
            // Создать asset
            var graph = ScriptableObject.CreateInstance<UIWindowGraph>();
            graph.startWindowId = ""; // Пустой по умолчанию
            
            AssetDatabase.CreateAsset(graph, ASSET_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"✅ UIWindowGraph created: {ASSET_PATH}");
        }
        
        private void GenerateUISprites()
        {
            var outputPath = $"{_rootFolder}/Resources/UI/Sprites";

            // Генерируем базовые спрайты вручную
            var sprites = new[]
            {
                ("Button_Default", new Color(0.8f, 0.8f, 0.8f, 1f)),
                ("Panel_Default", new Color(0.2f, 0.2f, 0.2f, 0.9f)),
                ("Window_Default", new Color(0.15f, 0.15f, 0.15f, 0.95f))
            };

            foreach (var (name, color) in sprites)
            {
                var texture = CreateSimpleTexture(128, 128, color);
                var spritePath = $"{outputPath}/{name}.png";

                var bytes = texture.EncodeToPNG();
                File.WriteAllBytes(spritePath, bytes);

                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.Refresh();

            // Настраиваем текстуры как UI спрайты с border
            foreach (var (name, _) in sprites)
            {
                var assetPath = $"{outputPath}/{name}.png";
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;

                    // Настройка border для 9-slice
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteBorder = new Vector4(8, 8, 8, 8);
                    importer.SetTextureSettings(settings);

                    importer.SaveAndReimport();
                }
            }
        }

        private Texture2D CreateSimpleTexture(int width, int height, Color color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }
        
        private void GenerateUIPrefabs()
        {
            var spritePath = $"{_rootFolder}/Resources/UI/Sprites";
            var prefabPath = $"{_rootFolder}/Resources/UI/Prefabs";
            
            // Загружаем спрайты
            var buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{spritePath}/Button_Default.png");
            var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{spritePath}/Panel_Default.png");
            
            // Создаем Button префаб
            CreateButtonPrefab(buttonSprite, $"{prefabPath}/DefaultButton.prefab");
            
            // Создаем Panel префаб
            CreatePanelPrefab(panelSprite, $"{prefabPath}/DefaultPanel.prefab");
            
            AssetDatabase.SaveAssets();
        }
        
        private void CreateButtonPrefab(Sprite sprite, string path)
        {
            var go = new GameObject("Button");
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            
            var button = go.AddComponent<Button>();
            
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform);
            var text = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = "Button";
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = Color.white;
            
            var rectTransform = go.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160, 30);
            
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            
            PrefabUtility.SaveAsPrefabAsset(go, path);
            DestroyImmediate(go);
        }
        
        private void CreatePanelPrefab(Sprite sprite, string path)
        {
            var go = new GameObject("Panel");
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            
            var rectTransform = go.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(400, 300);
            
            PrefabUtility.SaveAsPrefabAsset(go, path);
            DestroyImmediate(go);
        }
        
        private void CreateBootstrapScene()
        {
            // Создаём новую сцену
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // === Main Camera ===
            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            var cam = camera.AddComponent<Camera>();
            camera.AddComponent<AudioListener>();

            // Настройка камеры в зависимости от типа
            if (_cameraType == CameraType.TwoD)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                camera.transform.position = new Vector3(0, 0, -10);
                cam.clearFlags = CameraClearFlags.SolidColor;
            }
            else // ThreeD
            {
                cam.orthographic = false;
                cam.fieldOfView = 60f;
                camera.transform.position = new Vector3(0, 1, -10);
            }

            // Настройка в зависимости от pipeline
            var detectedPipeline = _autoDetectPipeline ? DetectRenderPipeline() : _renderPipeline;

            switch (detectedPipeline)
            {
                case RenderPipeline.Standard:
                    cam.clearFlags = _cameraType == CameraType.ThreeD ? 
                        CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.49f, 0.67f, 0.85f);
                    break;

                case RenderPipeline.URP:
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.02f, 0.02f, 0.02f);
                    break;

                case RenderPipeline.HDRP:
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                    break;
            }

            // === Lighting (только для 3D) ===
            if (_cameraType == CameraType.ThreeD)
            {
                var light = new GameObject("Directional Light");
                var lightComp = light.AddComponent<Light>();
                lightComp.type = LightType.Directional;

                // Интенсивность зависит от pipeline
                lightComp.intensity = detectedPipeline == RenderPipeline.HDRP ? 130000f : 1f;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            // === SystemInitializationManager ===
            var manager = new GameObject("SystemInitializationManager");
            manager.AddComponent<SystemInitializationManager>();

            // === EventSystem с правильным Input Module ===
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();

            // Проверяем наличие Input System пакета
            bool hasInputSystem = System.Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem") != null;

            if (hasInputSystem)
            {
                // Используем InputSystemUIInputModule для нового Input System
                var inputSystemModule = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSystemModule != null)
                {
                    eventSystem.AddComponent(inputSystemModule);
                }
                else
                {
                    // Fallback на старый модуль
                    eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                    Debug.LogWarning("InputSystemUIInputModule not found, using StandaloneInputModule as fallback");
                }
            }
            else
            {
                // Используем старый StandaloneInputModule
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Сохранение сцены
            var scenePath = $"{_rootFolder}/Scenes/Bootstrap.unity";
            var scenesFolder = $"{_rootFolder}/Scenes";

            if (!AssetDatabase.IsValidFolder(scenesFolder))
            {
                AssetDatabase.CreateFolder(_rootFolder, "Scenes");
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();
        }
        
        private void SetupCanvasStructure()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Bootstrap")
            {
                EditorUtility.DisplayDialog("Wrong Scene", 
                    "Please open Bootstrap scene first!", "OK");
                return;
            }
            
            // Canvas
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            
            // Panels
            var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{_rootFolder}/Resources/UI/Sprites/Panel_Default.png");
            
            CreatePanel("Background", canvasGO.transform, panelSprite, 0);
            CreatePanel("GameUI", canvasGO.transform, panelSprite, 1);
            CreatePanel("Overlay", canvasGO.transform, panelSprite, 2);
            
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        
        private void CreatePanel(string name, Transform parent, Sprite sprite, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(1, 1, 1, 0.1f);
            
            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortOrder;
            
            go.AddComponent<GraphicRaycaster>();
            
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
        }
        
        private void CreateExampleUIWindows()
        {
            // Проверка существования папки Scripts
            string scriptsFolder = $"{_rootFolder}/Scripts";
            if (!AssetDatabase.IsValidFolder(scriptsFolder))
            {
                AssetDatabase.CreateFolder(_rootFolder, "Scripts");
            }

            // Создание только ExampleGameplayInitializer.cs
            CreateExampleInitializerScript();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("✅ ExampleGameplayInitializer.cs created!");
        }
        
                                private void CreateExampleInitializerScript()
                                {
                                    string scriptPath = $"{_rootFolder}/Scripts/UI/ExampleGameplayInitializer.cs";
                                    string scriptFolder = $"{_rootFolder}/Scripts/UI";

                                    // Создать папку если не существует
                                    if (!AssetDatabase.IsValidFolder(scriptFolder))
                                    {
                                        string scriptsFolder = $"{_rootFolder}/Scripts";
                                        if (!AssetDatabase.IsValidFolder(scriptsFolder))
                                            AssetDatabase.CreateFolder(_rootFolder, "Scripts");
                                        AssetDatabase.CreateFolder(scriptsFolder, "UI");
                                    }

                                    // Проверить существование
                                    if (File.Exists(scriptPath))
                                    {
                                        Debug.LogWarning("ExampleGameplayInitializer.cs already exists!");
                                        return;
                                    }

                                    string template = $@"// {scriptPath}
                        using System.Collections.Generic;
                        using UnityEngine;
                        using ProtoSystem.UI;

                        namespace {_namespace}.UI
                        {{
                            /// <summary>
                            /// Пример инициализатора UI для {_projectName}.
                            /// Демонстрирует программную настройку UI flow с использованием ProtoSystem.
                            /// </summary>
                            [AddComponentMenu(""{_projectName}/UI/Example Gameplay Initializer"")]
                            public class ExampleGameplayInitializer : UISceneInitializerBase
                            {{
                                [Header(""Settings"")]
                                [SerializeField] private bool skipMainMenu = false;

                                private UINavigator _navigator;
                                private bool _gameStarted = false;

                                public override string StartWindowId => skipMainMenu ? ""GameHUD"" : ""MainMenu"";

                                public override IEnumerable<string> StartupWindowOrder
                                {{
                                    get
                                    {{
                                        yield return skipMainMenu ? ""GameHUD"" : ""MainMenu"";
                                    }}
                                }}

                                public override void Initialize(UISystem uiSystem)
                                {{
                                    _navigator = uiSystem.Navigator;
                                    _navigator.OnNavigated += OnNavigated;

                                    foreach (var windowId in StartupWindowOrder)
                                    {{
                                        var result = uiSystem.Navigator.Open(windowId);
                                        if (result == NavigationResult.Success && windowId == ""GameHUD"")
                                            OnGameStarted();
                                    }}
                                }}

                                private void OnNavigated(NavigationEventData data)
                                {{
                                    if (data.ToWindowId == ""GameHUD"" && data.Result == NavigationResult.Success)
                                        OnGameStarted();
                                    else if (data.ToWindowId == ""MainMenu"")
                                        _gameStarted = false;
                                }}

#endif

                                    if (_navigator != null)
                                        _navigator.OnNavigated -= OnNavigated;
                                }}

                                private void HandleEscape()
                                {{
                                    var current = _uiSystem.Navigator.CurrentWindow;
                                    if (current?.WindowId == ""GameHUD"")
                                        _uiSystem.Navigator.Navigate(""pause"");
                                    else if (current?.WindowId == ""PauseMenu"")
                                        UISystem.Back();
                                    else if (current?.WindowId != ""MainMenu"")
                                        UISystem.Back();
                                }}

                                private void OnDestroy()
                                {{
                                    if (UISystem.Instance?.Navigator != null)
                                        UISystem.Instance.Navigator.OnNavigated -= OnNavigated;
                                }}
                            }}
                        }}
                        ";

                                    File.WriteAllText(scriptPath, template, System.Text.Encoding.UTF8);
                                }
        
        private void AddNetcodeReferences()
        {
            // Уже добавлено в CreateAssemblyDefinition
            Debug.Log("Netcode references already added in asmdef");
        }
        
        private void SetupNetworkManager()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Bootstrap")
            {
                EditorUtility.DisplayDialog("Wrong Scene", 
                    "Please open Bootstrap scene first!", "OK");
                return;
            }

            // Используем рефлексию для добавления NetworkManager (может отсутствовать)
            var networkManagerType = System.Type.GetType("Unity.Netcode.NetworkManager, Unity.Netcode.Runtime");

            if (networkManagerType == null)
            {
                EditorUtility.DisplayDialog("Netcode Not Found", 
                    "Unity Netcode for GameObjects package is not installed.\n" +
                    "Install it via Package Manager first.", "OK");
                return;
            }

            var nmGO = new GameObject("NetworkManager");
            nmGO.AddComponent(networkManagerType);

            Debug.Log("NetworkManager added. Configure transport settings in Inspector.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        
        // ==================== SETTINGS ====================
        
        private string GetPrefsKey(string key) => $"ProtoSystem.Setup.{_projectName}.{key}";
        
        private void LoadSettings()
        {
            _projectName = EditorPrefs.GetString(GetPrefsKey("ProjectName"), "MyGame");
            _namespace = EditorPrefs.GetString(GetPrefsKey("Namespace"), "MyGame");
            _rootFolder = EditorPrefs.GetString(GetPrefsKey("RootFolder"), "Assets/MyGame");
            _projectType = (ProjectType)EditorPrefs.GetInt(GetPrefsKey("ProjectType"), 0);
            _cameraType = (CameraType)EditorPrefs.GetInt(GetPrefsKey("CameraType"), 0);
            _renderPipeline = DetectRenderPipeline();
            _autoDetectPipeline = EditorPrefs.GetBool(GetPrefsKey("AutoDetectPipeline"), true);
        }
        
        private void SaveSettings()
        {
            EditorPrefs.SetString(GetPrefsKey("ProjectName"), _projectName);
            EditorPrefs.SetString(GetPrefsKey("Namespace"), _namespace);
            EditorPrefs.SetString(GetPrefsKey("RootFolder"), _rootFolder);
            EditorPrefs.SetInt(GetPrefsKey("ProjectType"), (int)_projectType);
            EditorPrefs.SetInt(GetPrefsKey("CameraType"), (int)_cameraType);
            EditorPrefs.SetBool(GetPrefsKey("AutoDetectPipeline"), _autoDetectPipeline);
        }
        
        private void LoadTaskStatuses()
        {
            foreach (var task in _tasks)
            {
                task.IsCompleted = EditorPrefs.GetBool(GetPrefsKey($"Task.{task.Type}"), false);
            }
        }
        
        private void SaveTaskStatus(SetupTask task)
        {
            EditorPrefs.SetBool(GetPrefsKey($"Task.{task.Type}"), task.IsCompleted);
        }
        
        private void ResetProgress()
        {
            foreach (var task in _tasks)
            {
                task.IsCompleted = false;
                EditorPrefs.DeleteKey(GetPrefsKey($"Task.{task.Type}"));
            }
        }
        
        private string MakeValidNamespace(string input)
        {
            if (string.IsNullOrEmpty(input)) return "MyGame";

            var result = new string(input.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            if (char.IsDigit(result[0])) result = "_" + result;

            return result;
        }

        private RenderPipeline DetectRenderPipeline()
        {
            var currentPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;

            if (currentPipeline == null)
                return RenderPipeline.Standard;

            var pipelineType = currentPipeline.GetType().Name;

            if (pipelineType.Contains("Universal") || pipelineType.Contains("URP"))
                return RenderPipeline.URP;

            if (pipelineType.Contains("HDRenderPipeline") || pipelineType.Contains("HDRP"))
                return RenderPipeline.HDRP;

            return RenderPipeline.Standard;
        }
        
        private void OnDestroy()
        {
            SaveSettings();
        }
    }
    
    // ==================== SUPPORTING CLASSES ====================
    
    internal enum TaskType
    {
        CreateFolders,
        CreateAsmdef,
        CreateProjectConfig,
        CreateEventBus,
        GenerateUISprites,
        GenerateUIPrefabs,
        CreateUIWindowGraph,
        CreateExampleUIWindows,
        CreateBootstrapScene,
        AddNetcodeReferences,
        SetupNetworkManager
    }
    
    internal class SetupTask
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public TaskType Type { get; set; }
        public bool IsCompleted { get; set; }
        
        public SetupTask(string name, string description, TaskType type)
        {
            Name = name;
            Description = description;
            Type = type;
            IsCompleted = false;
        }
    }
}
