// Packages/com.protosystem.core/Editor/Initialization/ConfigCreationUtility.cs
using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem
{
    /// <summary>
    /// Утилита для автоматического создания конфигов и контейнеров систем
    /// </summary>
    public static class ConfigCreationUtility
    {
        private static string _cachedProjectNamespace;
        
        /// <summary>
        /// Получает namespace проекта из EventIds файла
        /// </summary>
        public static string GetProjectNamespace()
        {
            if (!string.IsNullOrEmpty(_cachedProjectNamespace))
                return _cachedProjectNamespace;
            
            var eventBusInfo = EventBusEditorUtils.GetProjectEventBusInfo();
            if (eventBusInfo != null && eventBusInfo.Exists && !string.IsNullOrEmpty(eventBusInfo.Namespace))
            {
                _cachedProjectNamespace = eventBusInfo.Namespace;
                return _cachedProjectNamespace;
            }
            
            // Fallback - используем имя папки проекта
            return "Game";
        }
        
        /// <summary>
        /// Сбрасывает кэш namespace
        /// </summary>
        public static void InvalidateNamespaceCache()
        {
            _cachedProjectNamespace = null;
        }
        
        /// <summary>
        /// Рисует кнопки создания конфигов и контейнеров для пустых полей
        /// </summary>
        public static void DrawConfigCreationButtons(UnityEngine.Object target, SerializedObject serializedObject)
        {
            var targetType = target.GetType();
            var fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            foreach (var field in fields)
            {
                // Проверяем что это ScriptableObject
                if (!typeof(ScriptableObject).IsAssignableFrom(field.FieldType))
                    continue;
                
                // Проверяем что имя заканчивается на Config или Container
                bool isConfig = field.FieldType.Name.EndsWith("Config");
                bool isContainer = field.FieldType.Name.EndsWith("Container");
                
                if (!isConfig && !isContainer)
                    continue;
                
                // Проверяем что поле сериализуется
                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                    continue;
                
                // Получаем текущее значение
                var currentValue = field.GetValue(target) as ScriptableObject;
                
                if (currentValue == null)
                {
                    DrawAssetCreationButton(target, serializedObject, field, isContainer);
                }
            }
        }

        private static void DrawAssetCreationButton(UnityEngine.Object target, SerializedObject serializedObject, FieldInfo field, bool isContainer)
        {
            string projectNs = GetProjectNamespace();
            string systemFolder = GetSystemFolder(target.GetType());
            string subFolder = isContainer ? "Containers" : systemFolder;
            string expectedPath = $"Assets/{projectNs}/Settings/{subFolder}/{field.FieldType.Name}.asset";
            
            EditorGUILayout.BeginHorizontal();
            string assetType = isContainer ? "Контейнер" : "Конфиг";
            EditorGUILayout.HelpBox($"{assetType} '{field.Name}' не назначен\nПуть: {expectedPath}", MessageType.Warning);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            string buttonIcon = isContainer ? "📦" : "🔨";
            if (GUILayout.Button($"{buttonIcon} Создать {field.FieldType.Name}", GUILayout.Width(220), GUILayout.Height(24)))
            {
                CreateAsset(target, serializedObject, field, isContainer);
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        private static void CreateAsset(UnityEngine.Object target, SerializedObject serializedObject, FieldInfo field, bool isContainer)
        {
            var assetType = field.FieldType;
            var systemType = target.GetType();
            
            // Определяем путь: Assets/<ProjectNamespace>/Settings/<SubFolder>/
            string projectNs = GetProjectNamespace();
            string subFolder = isContainer ? "Containers" : GetSystemFolder(systemType);
            
            string folderPath = $"Assets/{projectNs}/Settings/{subFolder}";
            
            // Создаём директории
            EnsureDirectoryExists(folderPath);
            
            // Имя файла
            string fileName = $"{assetType.Name}.asset";
            string fullPath = $"{folderPath}/{fileName}";
            
            // Проверяем существование
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(fullPath) != null)
            {
                var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(fullPath);
                AssignAsset(target, serializedObject, field, existing);
                Debug.Log($"[ConfigCreation] Использован существующий ассет: {fullPath}");
                return;
            }
            
            // Создаём новый ScriptableObject
            var instance = ScriptableObject.CreateInstance(assetType);
            
            AssetDatabase.CreateAsset(instance, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Назначаем созданный ассет
            AssignAsset(target, serializedObject, field, instance);
            
            Debug.Log($"[ConfigCreation] Создан ассет: {fullPath}");
            EditorGUIUtility.PingObject(instance);
        }

        private static void AssignAsset(UnityEngine.Object target, SerializedObject serializedObject, FieldInfo field, ScriptableObject asset)
        {
            Undo.RecordObject(target, $"Assign {field.Name}");
            field.SetValue(target, asset);
            EditorUtility.SetDirty(target);
            serializedObject.Update();
        }

        private static string GetSystemFolder(Type systemType)
        {
            string name = systemType.Name;
            
            // EffectsManagerSystem -> EffectsManager -> Effects
            // UISystem -> UI
            // SettingsSystem -> Settings
            // CursorManager -> Cursor
            
            if (name.EndsWith("ManagerSystem"))
            {
                return name.Substring(0, name.Length - "ManagerSystem".Length);
            }
            
            if (name.EndsWith("System"))
            {
                return name.Substring(0, name.Length - "System".Length);
            }
            
            if (name.EndsWith("Manager"))
            {
                return name.Substring(0, name.Length - "Manager".Length);
            }
            
            return name;
        }

        private static void EnsureDirectoryExists(string path)
        {
            string[] parts = path.Split('/');
            string currentPath = parts[0];
            
            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = currentPath + "/" + parts[i];
                
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }
                
                currentPath = nextPath;
            }
        }
    }
}
