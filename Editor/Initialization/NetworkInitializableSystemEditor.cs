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
            
            // Рисуем стандартный инспектор
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);
            
            // Кнопки создания конфигов
            ConfigCreationUtility.DrawConfigCreationButtons(target, serializedObject);
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
