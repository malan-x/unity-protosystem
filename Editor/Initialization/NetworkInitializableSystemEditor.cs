// Packages/com.protosystem.core/Editor/Initialization/NetworkInitializableSystemEditor.cs
using UnityEngine;
using UnityEditor;

namespace ProtoSystem
{
    /// <summary>
    /// Редактор для NetworkInitializableSystem.
    /// Автоматически добавляет кнопки создания конфигов для пустых полей.
    /// </summary>
    [CustomEditor(typeof(NetworkInitializableSystem), true)]
    [CanEditMultipleObjects]
    public class NetworkInitializableSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Используем интерфейс чтобы избежать зависимости от Netcode в Editor
            var system = target as IInitializableSystem;
            
            // Заголовок с описанием системы
            if (system != null)
            {
                DrawSystemHeader(system);
                EditorGUILayout.Space(5);
            }
            
            // Рисуем стандартный инспектор
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);
            
            // Кнопки создания конфигов
            ConfigCreationUtility.DrawConfigCreationButtons(target, serializedObject);
            
            serializedObject.ApplyModifiedProperties();
        }
        
        /// <summary>
        /// Рисует заголовок системы с описанием
        /// </summary>
        protected virtual void DrawSystemHeader(IInitializableSystem system)
        {
            var description = system.Description;
            if (string.IsNullOrEmpty(description))
                return;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Название системы
            EditorGUILayout.LabelField($"🌐 {system.DisplayName}", EditorStyles.boldLabel);
            
            // Описание
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            
            EditorGUILayout.Space(3);
            
            // Статус
            DrawSystemStatus(system);
            
            EditorGUILayout.EndVertical();
        }
        
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
