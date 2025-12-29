// Packages/com.protosystem.core/Editor/Initialization/InitializableSystemEditor.cs
using UnityEngine;
using UnityEditor;

namespace ProtoSystem
{
    /// <summary>
    /// Редактор для InitializableSystemBase.
    /// Автоматически добавляет кнопки создания конфигов для пустых полей.
    /// </summary>
    [CustomEditor(typeof(InitializableSystemBase), true)]
    [CanEditMultipleObjects]
    public class InitializableSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Рисуем стандартный инспектор
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);
            
            // Кнопки создания конфигов
            ConfigCreationUtility.DrawConfigCreationButtons(target, serializedObject);
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
