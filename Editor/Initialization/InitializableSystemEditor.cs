// Packages/com.protosystem.core/Editor/Initialization/InitializableSystemEditor.cs
using UnityEngine;
using UnityEditor;

namespace ProtoSystem
{
    /// <summary>
    /// Редактор для InitializableSystemBase.
    /// Поддерживает inline-редактирование конфигов через атрибут [InlineConfig].
    /// </summary>
    [CustomEditor(typeof(InitializableSystemBase), true)]
    [CanEditMultipleObjects]
    public class InitializableSystemEditor : InlineConfigSystemEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var system = target as IInitializableSystem;
            
            // Заголовок с описанием системы
            if (system != null)
            {
                DrawSystemHeader(system);
                EditorGUILayout.Space(5);
            }
            
            // Рисуем свойства с обработкой [InlineConfig]
            DrawPropertiesWithInlineConfigs();
            
            EditorGUILayout.Space(10);
            
            // Кнопки создания конфигов (для полей БЕЗ [InlineConfig])
            ConfigCreationUtility.DrawConfigCreationButtons(target, serializedObject);
            
            serializedObject.ApplyModifiedProperties();
        }
        
        /// <summary>
        /// Рисует заголовок системы с описанием
        /// </summary>
        protected override void DrawSystemHeader(IInitializableSystem system)
        {
            var description = system.Description;
            if (string.IsNullOrEmpty(description))
                return;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Название системы
            EditorGUILayout.LabelField($"🔧 {system.DisplayName}", EditorStyles.boldLabel);
            
            // Описание
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            
            EditorGUILayout.Space(3);
            
            // Статус
            DrawSystemStatus(system);
            
            EditorGUILayout.EndVertical();
        }
    }
}
