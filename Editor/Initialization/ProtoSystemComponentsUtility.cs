// Packages/com.protosystem.core/Editor/Initialization/ProtoSystemComponentsUtility.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem
{
    /// <summary>
    /// Информация о компоненте ProtoSystem
    /// </summary>
    public class ProtoSystemComponentInfo
    {
        public Type Type { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Icon { get; set; }
        public int Order { get; set; }
        public bool ExistsInScene { get; set; }
        public bool ExistsInManager { get; set; }
        public MonoBehaviour SceneInstance { get; set; }
    }

    /// <summary>
    /// Утилита для работы с компонентами ProtoSystem
    /// </summary>
    public static class ProtoSystemComponentsUtility
    {
        private static List<ProtoSystemComponentInfo> _cachedComponents;
        private static double _lastCacheTime;
        private const double CacheLifetime = 5.0; // секунд

        /// <summary>
        /// Получить все компоненты ProtoSystem с их статусами
        /// </summary>
        public static List<ProtoSystemComponentInfo> GetAllComponents(SystemInitializationManager manager = null)
        {
            // Проверяем кэш
            if (_cachedComponents != null && EditorApplication.timeSinceStartup - _lastCacheTime < CacheLifetime)
            {
                // Обновляем только статусы
                UpdateComponentStatuses(_cachedComponents, manager);
                return _cachedComponents;
            }

            _cachedComponents = CollectComponents();
            _lastCacheTime = EditorApplication.timeSinceStartup;
            
            UpdateComponentStatuses(_cachedComponents, manager);
            return _cachedComponents;
        }

        /// <summary>
        /// Сбросить кэш
        /// </summary>
        public static void InvalidateCache()
        {
            _cachedComponents = null;
        }

        /// <summary>
        /// Собрать все типы с атрибутом ProtoSystemComponent
        /// </summary>
        private static List<ProtoSystemComponentInfo> CollectComponents()
        {
            var result = new List<ProtoSystemComponentInfo>();

            // Ищем во всех загруженных сборках
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Пропускаем системные сборки
                if (assembly.FullName.StartsWith("System") || 
                    assembly.FullName.StartsWith("mscorlib") ||
                    assembly.FullName.StartsWith("Unity.") && !assembly.FullName.Contains("ProtoSystem"))
                    continue;

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        var attr = type.GetCustomAttribute<ProtoSystemComponentAttribute>();
                        if (attr != null)
                        {
                            result.Add(new ProtoSystemComponentInfo
                            {
                                Type = type,
                                DisplayName = attr.DisplayName,
                                Description = attr.Description,
                                Category = attr.Category,
                                Icon = attr.Icon,
                                Order = attr.Order
                            });
                        }
                    }
                }
                catch
                {
                    // Пропускаем сборки с ошибками загрузки типов
                }
            }

            return result.OrderBy(c => c.Order).ThenBy(c => c.DisplayName).ToList();
        }

        /// <summary>
        /// Обновить статусы компонентов (есть в сцене/менеджере)
        /// </summary>
        private static void UpdateComponentStatuses(List<ProtoSystemComponentInfo> components, SystemInitializationManager manager)
        {
            // Собираем типы из менеджера
            var managerTypes = new HashSet<Type>();
            if (manager != null)
            {
                foreach (var entry in manager.Systems)
                {
                    if (entry.ExistingSystemObject != null)
                    {
                        managerTypes.Add(entry.ExistingSystemObject.GetType());
                    }
                    else if (!string.IsNullOrEmpty(entry.systemTypeName))
                    {
                        var type = Type.GetType(entry.systemTypeName);
                        if (type != null) managerTypes.Add(type);
                    }
                }
            }

            // Собираем экземпляры из сцены
            var sceneInstances = new Dictionary<Type, MonoBehaviour>();
            foreach (var mb in GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                var type = mb.GetType();
                if (!sceneInstances.ContainsKey(type))
                {
                    sceneInstances[type] = mb;
                }
            }

            // Обновляем статусы
            foreach (var component in components)
            {
                component.ExistsInManager = managerTypes.Contains(component.Type);
                component.ExistsInScene = sceneInstances.ContainsKey(component.Type);
                component.SceneInstance = sceneInstances.TryGetValue(component.Type, out var instance) ? instance : null;
            }
        }

        /// <summary>
        /// Создать компонент в сцене как дочерний объект менеджера
        /// </summary>
        public static MonoBehaviour CreateComponentInScene(Type componentType, Transform parent = null)
        {
            if (componentType == null) return null;

            // Создаём GameObject
            var go = new GameObject(componentType.Name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {componentType.Name}");

            // Устанавливаем родителя
            if (parent != null)
            {
                go.transform.SetParent(parent);
            }

            // Добавляем компонент
            var component = go.AddComponent(componentType) as MonoBehaviour;

            Selection.activeGameObject = go;
            
            InvalidateCache();
            return component;
        }

        /// <summary>
        /// Добавить компонент в менеджер
        /// </summary>
        public static void AddToManager(SystemInitializationManager manager, ProtoSystemComponentInfo componentInfo)
        {
            if (manager == null || componentInfo == null) return;

            Undo.RecordObject(manager, $"Add {componentInfo.DisplayName} to Manager");

            var entry = new SystemEntry
            {
                systemName = componentInfo.DisplayName.Replace(" ", ""),
                enabled = true,
                useExistingObject = componentInfo.SceneInstance != null,
                verboseLogging = true
            };

            if (componentInfo.SceneInstance != null)
            {
                entry.ExistingSystemObject = componentInfo.SceneInstance;
            }
            else
            {
                entry.systemTypeName = componentInfo.Type.AssemblyQualifiedName;
            }

            manager.Systems.Add(entry);
            
            EditorUtility.SetDirty(manager);
            InvalidateCache();
        }

        /// <summary>
        /// Создать и добавить компонент
        /// </summary>
        public static void CreateAndAddToManager(SystemInitializationManager manager, ProtoSystemComponentInfo componentInfo)
        {
            if (manager == null || componentInfo == null) return;

            // Создаём в сцене как дочерний объект менеджера
            var instance = CreateComponentInScene(componentInfo.Type, manager.transform);
            
            if (instance != null)
            {
                // Обновляем информацию
                componentInfo.SceneInstance = instance;
                componentInfo.ExistsInScene = true;

                // Добавляем в менеджер
                AddToManager(manager, componentInfo);
            }
        }
    }
}
