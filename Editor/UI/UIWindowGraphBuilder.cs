// Packages/com.protosystem.core/Editor/UI/UIWindowGraphBuilder.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Автоматически собирает UIWindowGraph из атрибутов при компиляции.
    /// </summary>
    [InitializeOnLoad]
    public static class UIWindowGraphBuilder
    {
        private const string RESOURCES_FOLDER = "Assets/Resources/ProtoSystem";
        
        static UIWindowGraphBuilder()
        {
            // Подписываемся на событие завершения компиляции
            EditorApplication.delayCall += OnDelayedInit;
        }

        private static void OnDelayedInit()
        {
            EditorApplication.delayCall -= OnDelayedInit;
            
            // Проверяем нужно ли пересобрать
            if (ShouldRebuild())
            {
                RebuildGraph();
            }
        }

        /// <summary>
        /// Проверяет нужно ли пересобрать граф
        /// </summary>
        private static bool ShouldRebuild()
        {
            // Не пересобираем в Play mode - UISystem сам построит граф с SceneInitializer
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            
            var graph = Resources.Load<UIWindowGraph>(UIWindowGraph.RESOURCE_PATH);
            
            // Нет графа — нужно создать
            if (graph == null) return true;
            
            // Можно добавить проверку хэша сборок для оптимизации
            // Пока просто пересобираем при каждой компиляции
            return true;
        }

        /// <summary>
        /// Принудительно пересобрать граф
        /// </summary>
        [MenuItem("ProtoSystem/UI/Rebuild Window Graph", priority = 100)]
        [MenuItem("ProtoSystem/UI/Rebuild Window Graph", priority = 100)]
        public static void RebuildGraph()
        {
            // Не пересобираем в Play mode!
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[UIWindowGraphBuilder] Skipping rebuild in Play mode - UISystem builds graph at runtime with SceneInitializer");
                return;
            }

            var graph = GetOrCreateGraph();

            graph.ClearForRebuild();

            int windowsFound = 0;
            int transitionsFound = 0;
            int prefabsFound = 0;

            // Сканируем все сборки
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var windowTypes = new List<(Type type, UIWindowAttribute attr)>();

            foreach (var assembly in assemblies)
            {
                // Пропускаем системные
                var name = assembly.FullName;
                if (name.StartsWith("System") || name.StartsWith("Unity") || 
                    name.StartsWith("mscorlib") || name.StartsWith("Mono") ||
                    name.StartsWith("netstandard") || name.StartsWith("Microsoft"))
                    continue;

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!typeof(UIWindowBase).IsAssignableFrom(type) || type.IsAbstract)
                            continue;

                        var windowAttr = (UIWindowAttribute)Attribute.GetCustomAttribute(type, typeof(UIWindowAttribute));
                        if (windowAttr != null && windowAttr.ShowInGraph)
                        {
                            windowTypes.Add((type, windowAttr));
                        }
                    }
                }
                catch (Exception)
                {
                    // Игнорируем ошибки рефлексии
                }
            }

            // Обрабатываем найденные окна
            foreach (var (type, windowAttr) in windowTypes)
            {
                // Ищем prefab
                var prefab = FindPrefabForWindow(type, windowAttr.WindowId);
                if (prefab != null) prefabsFound++;

                // Создаём определение окна
                var windowDef = new WindowDefinition
                {
                    id = windowAttr.WindowId,
                    prefab = prefab,
                    type = windowAttr.Type,
                    layer = windowAttr.Layer,
                    level = windowAttr.Level,
                    pauseGame = windowAttr.PauseGame,
                    hideBelow = windowAttr.HideBelow,
                    allowBack = windowAttr.AllowBack,
                    typeName = type.FullName
                };

                graph.AddWindow(windowDef);
                windowsFound++;

                // Собираем переходы
                var transitionAttrs = (UITransitionAttribute[])Attribute.GetCustomAttributes(type, typeof(UITransitionAttribute));
                foreach (var transAttr in transitionAttrs)
                {
                    graph.AddTransition(new TransitionDefinition
                    {
                        fromWindowId = windowAttr.WindowId,
                        toWindowId = transAttr.ToWindowId,
                        trigger = transAttr.Trigger,
                        animation = transAttr.Animation
                    });
                    transitionsFound++;
                }

                // Глобальные переходы
                var globalAttrs = (UIGlobalTransitionAttribute[])Attribute.GetCustomAttributes(type, typeof(UIGlobalTransitionAttribute));
                foreach (var globalAttr in globalAttrs)
                {
                    graph.AddTransition(new TransitionDefinition
                    {
                        fromWindowId = "",
                        toWindowId = globalAttr.ToWindowId,
                        trigger = globalAttr.Trigger,
                        animation = globalAttr.Animation
                    });
                    transitionsFound++;
                }
            }

            graph.FinalizeBuild();

            // Сканируем все сцены на наличие Initializers с дополнительными переходами
            ScanScenesForAdditionalTransitions(graph);

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();

            Debug.Log($"[UIWindowGraphBuilder] Rebuilt: {windowsFound} windows, {transitionsFound} transitions, {prefabsFound} prefabs found");
        }

        /// <summary>
        /// Сканирует открытые сцены и префабы на наличие UISceneInitializerBase с GetAdditionalTransitions()
        /// </summary>
        private static void ScanScenesForAdditionalTransitions(UIWindowGraph graph)
        {
            int transitionsAdded = 0;
            var processedTypes = new HashSet<System.Type>();

            // 1. Ищем в открытых сценах
            var sceneInitializers = UnityEngine.Object.FindObjectsByType<UISceneInitializerBase>(
                FindObjectsInactive.Include, 
                FindObjectsSortMode.None
            );

            foreach (var initializer in sceneInitializers)
            {
                var initType = initializer.GetType();
                if (processedTypes.Contains(initType)) continue;
                processedTypes.Add(initType);

                transitionsAdded += ProcessInitializerTransitions(initializer, graph);
            }

            // 2. Ищем в префабах (для случаев когда сцена не открыта)
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab == null) continue;

                var initializerInPrefab = prefab.GetComponent<UISceneInitializerBase>();
                if (initializerInPrefab == null) continue;

                var initType = initializerInPrefab.GetType();
                if (processedTypes.Contains(initType)) continue;
                processedTypes.Add(initType);

                transitionsAdded += ProcessInitializerTransitions(initializerInPrefab, graph);
            }

            if (transitionsAdded > 0)
            {
                Debug.Log($"[UIWindowGraphBuilder] Added {transitionsAdded} transitions from scene initializers");
            }
        }

        /// <summary>
        /// Обрабатывает переходы из одного initializer'а
        /// </summary>
        private static int ProcessInitializerTransitions(UISceneInitializerBase initializer, UIWindowGraph graph)
        {
            int count = 0;
            var transitions = initializer.GetAdditionalTransitions();
            if (transitions == null) return count;

            foreach (var transition in transitions)
            {
                // Проверяем что целевое окно существует
                if (graph.HasWindow(transition.toWindowId))
                {
                    var transitionDef = new TransitionDefinition
                    {
                        fromWindowId = transition.fromWindowId,
                        toWindowId = transition.toWindowId,
                        trigger = transition.trigger,
                        animation = transition.animation
                    };

                    graph.AddTransition(transitionDef, allowOverride: true);
                    count++;
                }
                else
                {
                    Debug.LogWarning($"[UIWindowGraphBuilder] Transition from initializer '{initializer.GetType().Name}' references unknown window: {transition.toWindowId}");
                }
            }

            return count;
        }

        /// <summary>
        /// Получить или создать граф
        /// </summary>
        private static UIWindowGraph GetOrCreateGraph()
        {
            var graph = AssetDatabase.LoadAssetAtPath<UIWindowGraph>(UIWindowGraph.ASSET_PATH);
            
            if (graph == null)
            {
                // Создаём папку если нет
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                    
                if (!AssetDatabase.IsValidFolder(RESOURCES_FOLDER))
                    AssetDatabase.CreateFolder("Assets/Resources", "ProtoSystem");

                // Создаём ассет
                graph = ScriptableObject.CreateInstance<UIWindowGraph>();
                AssetDatabase.CreateAsset(graph, UIWindowGraph.ASSET_PATH);
                AssetDatabase.SaveAssets();
                
                Debug.Log($"[UIWindowGraph] Created new graph at {UIWindowGraph.ASSET_PATH}");
            }

            return graph;
        }

        /// <summary>
        /// Ищет prefab для окна по разным стратегиям
        /// </summary>
        private static GameObject FindPrefabForWindow(Type windowType, string windowId)
        {
            // Стратегия 1: Ищем по имени класса
            var prefab = FindPrefabByName(windowType.Name);
            if (prefab != null) return prefab;
            
            // Стратегия 2: Ищем по WindowId
            prefab = FindPrefabByName(windowId);
            if (prefab != null) return prefab;
            
            // Стратегия 3: Ищем по имени без суффикса Window
            var nameWithoutSuffix = windowType.Name.Replace("Window", "");
            prefab = FindPrefabByName(nameWithoutSuffix);
            if (prefab != null) return prefab;

            return null;
        }

        /// <summary>
        /// Ищет prefab по имени в проекте
        /// </summary>
        private static GameObject FindPrefabByName(string name)
        {
            // Ищем в стандартных папках
            string[] searchFolders = new[]
            {
                "Assets/Prefabs/UI",
                "Assets/UI/Prefabs",
                "Assets/Resources/UI",
                "Assets/Prefabs",
                "Assets"
            };

            foreach (var folder in searchFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                var guids = AssetDatabase.FindAssets($"t:Prefab {name}", new[] { folder });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    
                    if (prefab != null)
                    {
                        // Проверяем что это действительно UI окно
                        var windowComponent = prefab.GetComponent<UIWindowBase>();
                        if (windowComponent != null)
                            return prefab;
                    }
                }
            }

            // Глобальный поиск
            var allGuids = AssetDatabase.FindAssets($"t:Prefab {name}");
            foreach (var guid in allGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null && prefab.GetComponent<UIWindowBase>() != null)
                    return prefab;
            }

            return null;
        }

        /// <summary>
        /// Открыть граф в Inspector
        /// </summary>
        [MenuItem("ProtoSystem/UI/Select Window Graph", priority = 101)]
        public static void SelectGraph()
        {
            var graph = GetOrCreateGraph();
            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);
        }

        /// <summary>
        /// Валидация графа
        /// </summary>
        [MenuItem("ProtoSystem/UI/Validate Window Graph", priority = 102)]
        public static void ValidateGraph()
        {
            var graph = GetOrCreateGraph();
            var errors = new List<string>();
            var warnings = new List<string>();

            // Проверка стартового окна
            if (string.IsNullOrEmpty(graph.startWindowId))
                warnings.Add("Start window ID is not set");
            else if (graph.GetWindow(graph.startWindowId) == null)
                errors.Add($"Start window '{graph.startWindowId}' not found");

            // Проверка окон
            var windowIds = new HashSet<string>();
            foreach (var window in graph.GetAllWindows())
            {
                if (string.IsNullOrEmpty(window.id))
                {
                    errors.Add("Window with empty ID found");
                    continue;
                }

                if (!windowIds.Add(window.id))
                    errors.Add($"Duplicate window ID: {window.id}");

                if (window.prefab == null)
                    warnings.Add($"Window '{window.id}' has no prefab");
            }

            // Проверка переходов
            foreach (var transition in graph.transitions.Concat(graph.globalTransitions))
            {
                if (string.IsNullOrEmpty(transition.toWindowId))
                    errors.Add($"Transition '{transition.trigger}' has no target");
                else if (graph.GetWindow(transition.toWindowId) == null)
                    errors.Add($"Transition target '{transition.toWindowId}' not found");
            }

            // Вывод результатов
            if (errors.Count == 0 && warnings.Count == 0)
            {
                Debug.Log("[UIWindowGraph] ✓ Validation passed!");
            }
            else
            {
                foreach (var error in errors)
                    Debug.LogError($"[UIWindowGraph] ✗ {error}");
                foreach (var warning in warnings)
                    Debug.LogWarning($"[UIWindowGraph] ⚠ {warning}");
            }
        }
    }
}
