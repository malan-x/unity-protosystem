// Packages/com.protosystem.core/Editor/Initialization/InlineConfigSystemEditor.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem
{
    /// <summary>
    /// Базовый редактор с поддержкой inline-редактирования конфигов через атрибут [InlineConfig].
    /// Наследуйте от этого класса для систем, реализующих IInitializableSystem.
    /// </summary>
    public abstract class InlineConfigSystemEditor : UnityEditor.Editor
    {
        // Кэш inline Editor'ов: fieldName -> Editor
        private Dictionary<string, UnityEditor.Editor> _inlineEditors = new Dictionary<string, UnityEditor.Editor>();
        
        // Кэш полей с [InlineConfig]: fieldName -> (FieldInfo, InlineConfigAttribute)
        private Dictionary<string, (FieldInfo field, InlineConfigAttribute attr)> _inlineConfigFields;
        
        // Кэш ключей EditorPrefs
        private string _editorPrefsKeyPrefix;
        
        protected virtual void OnEnable()
        {
            CacheInlineConfigFields();
            BuildEditorPrefsKeyPrefix();
        }
        
        protected virtual void OnDisable()
        {
            ClearInlineEditors();
        }
        
        /// <summary>
        /// Кэширует поля с атрибутом [InlineConfig]
        /// </summary>
        private void CacheInlineConfigFields()
        {
            _inlineConfigFields = new Dictionary<string, (FieldInfo, InlineConfigAttribute)>();
            
            var type = target.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            foreach (var field in fields)
            {
                var attr = field.GetCustomAttribute<InlineConfigAttribute>();
                if (attr != null && typeof(ScriptableObject).IsAssignableFrom(field.FieldType))
                {
                    _inlineConfigFields[field.Name] = (field, attr);
                }
            }
        }
        
        /// <summary>
        /// Строит префикс ключа для EditorPrefs
        /// </summary>
        private void BuildEditorPrefsKeyPrefix()
        {
            var productGuid = PlayerSettings.productGUID.ToString();
            var sceneGuid = "";
            
            var component = target as Component;
            if (component != null && component.gameObject.scene.IsValid())
            {
                var scenePath = component.gameObject.scene.path;
                if (!string.IsNullOrEmpty(scenePath))
                {
                    sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                }
            }
            
            _editorPrefsKeyPrefix = $"InlineConfig_{productGuid}_{sceneGuid}_{target.GetType().Name}";
        }
        
        /// <summary>
        /// Получает ключ EditorPrefs для foldout состояния поля
        /// </summary>
        protected string GetFoldoutKey(string fieldName)
        {
            return $"{_editorPrefsKeyPrefix}_{fieldName}";
        }
        
        /// <summary>
        /// Очищает кэш inline Editor'ов
        /// </summary>
        private void ClearInlineEditors()
        {
            foreach (var editor in _inlineEditors.Values)
            {
                if (editor != null)
                {
                    DestroyImmediate(editor);
                }
            }
            _inlineEditors.Clear();
        }
        
        /// <summary>
        /// Рисует свойства, обрабатывая [InlineConfig] поля особым образом
        /// </summary>
        protected virtual void DrawPropertiesWithInlineConfigs()
        {
            var iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                
                // Пропускаем m_Script
                if (iterator.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator);
                    }
                    continue;
                }
                
                // Проверяем, есть ли [InlineConfig] для этого поля
                if (_inlineConfigFields != null && _inlineConfigFields.TryGetValue(iterator.name, out var configInfo))
                {
                    DrawInlineConfigField(iterator, configInfo.field, configInfo.attr);
                }
                else
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }
        }
        
        /// <summary>
        /// Рисует поле с inline-редактированием конфига
        /// </summary>
        protected virtual void DrawInlineConfigField(SerializedProperty property, FieldInfo fieldInfo, InlineConfigAttribute attr)
        {
            var configObject = property.objectReferenceValue as ScriptableObject;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            bool isExpanded = true;
            
            if (attr.Foldout && configObject != null)
            {
                var foldoutKey = GetFoldoutKey(property.name);
                isExpanded = EditorPrefs.GetBool(foldoutKey, true);
                
                // Foldout с названием поля
                var newExpanded = EditorGUILayout.Foldout(isExpanded, property.displayName, true);
                if (newExpanded != isExpanded)
                {
                    EditorPrefs.SetBool(foldoutKey, newExpanded);
                    isExpanded = newExpanded;
                }
                
                // ObjectField с отступом
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(property, GUIContent.none);
                EditorGUI.indentLevel--;
            }
            else
            {
                // Без foldout — просто поле
                EditorGUILayout.PropertyField(property);
            }
            
            // Inline редактор конфига
            if (configObject != null && isExpanded)
            {
                EditorGUILayout.Space(2);
                
                // Получаем или создаём Editor
                if (!_inlineEditors.TryGetValue(property.name, out var inlineEditor) || 
                    inlineEditor == null || 
                    inlineEditor.target != configObject)
                {
                    // Удаляем старый Editor
                    if (inlineEditor != null)
                    {
                        DestroyImmediate(inlineEditor);
                    }
                    
                    // Создаём новый
                    inlineEditor = CreateEditor(configObject);
                    _inlineEditors[property.name] = inlineEditor;
                }
                
                // Рисуем inline Editor с отступом
                EditorGUI.indentLevel++;
                
                EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 5, 0, 5) });
                inlineEditor.OnInspectorGUI();
                EditorGUILayout.EndVertical();
                
                EditorGUI.indentLevel--;
            }
            else if (configObject == null)
            {
                // Кнопка создания конфига
                DrawInlineConfigCreationButton(property, fieldInfo);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// Рисует кнопку создания конфига для inline поля
        /// </summary>
        protected virtual void DrawInlineConfigCreationButton(SerializedProperty property, FieldInfo fieldInfo)
        {
            var fieldType = fieldInfo.FieldType;
            var typeName = fieldType.Name;
            var isContainer = typeName.EndsWith("Container");
            var label = isContainer ? "Container" : "Config";
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUI.color = new Color(0.5f, 0.8f, 1f);
            if (GUILayout.Button($"🔨 Создать {label}", GUILayout.Width(150)))
            {
                CreateConfigAsset(property, fieldInfo);
            }
            GUI.color = Color.white;
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// Создаёт ScriptableObject конфиг и присваивает полю
        /// </summary>
        protected virtual void CreateConfigAsset(SerializedProperty property, FieldInfo fieldInfo)
        {
            var fieldType = fieldInfo.FieldType;
            var systemType = target.GetType();
            
            // Определяем путь
            var folder = GetConfigFolder(systemType);
            EnsureDirectoryExists(folder);
            
            var assetName = fieldType.Name;
            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");
            
            // Создаём ассет
            var asset = ScriptableObject.CreateInstance(fieldType);
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            
            // Присваиваем
            property.objectReferenceValue = asset;
            serializedObject.ApplyModifiedProperties();
            
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[ProtoSystem] Создан {fieldType.Name}: {assetPath}");
        }
        
        /// <summary>
        /// Определяет папку для конфигов системы
        /// </summary>
        protected virtual string GetConfigFolder(Type systemType)
        {
            var ns = systemType.Namespace ?? "Game";
            var topNamespace = ns.Split('.')[0];
            
            // Определяем модуль из namespace или имени типа
            var moduleName = systemType.Name.Replace("System", "");
            
            return $"Assets/{topNamespace}/Settings/{moduleName}";
        }
        
        /// <summary>
        /// Создаёт директорию если не существует
        /// </summary>
        protected void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                var current = parts[0];
                
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }
        
        /// <summary>
        /// Рисует заголовок системы. Переопределите для кастомного отображения.
        /// </summary>
        protected abstract void DrawSystemHeader(IInitializableSystem system);
        
        /// <summary>
        /// Рисует статус системы
        /// </summary>
        protected virtual void DrawSystemStatus(IInitializableSystem system)
        {
            // Проверяем наличие конфига
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
            else if (configProp == null || configProp.objectReferenceValue != null)
            {
                GUI.color = new Color(0.5f, 0.9f, 0.5f);
                EditorGUILayout.LabelField("✓ Готов к работе", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
        }
    }
}
