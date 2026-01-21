using UnityEngine;
using UnityEditor;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Кастомный редактор для MusicConfig
    /// </summary>
    [CustomEditor(typeof(MusicConfig))]
    public class MusicConfigEditor : UnityEditor.Editor
    {
        private bool _showCrossfade = true;
        private bool _showLayers = false;
        private bool _showParameters = false;
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var config = (MusicConfig)target;
            
            // ===== HEADER =====
            DrawHeader(config);
            
            EditorGUILayout.Space(10);
            
            // Crossfade
            _showCrossfade = EditorGUILayout.Foldout(_showCrossfade, "🔀 Crossfade", true, EditorStyles.foldoutHeader);
            if (_showCrossfade)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Настройки плавного перехода между музыкальными треками.", MessageType.None);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultCrossfadeTime"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("crossfadeCurve"));
                EditorGUI.indentLevel--;
            }
            
            // Vertical Layering
            _showLayers = EditorGUILayout.Foldout(_showLayers, "📊 Vertical Layering", true, EditorStyles.foldoutHeader);
            if (_showLayers)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    "Динамическое микширование слоёв музыки.\n" +
                    "Каждый слой управляется параметром (intensity, danger и т.д.).\n" +
                    "Используйте для адаптивной музыки в геймплее.",
                    MessageType.None
                );
                EditorGUILayout.PropertyField(serializedObject.FindProperty("layers"), true);
                EditorGUI.indentLevel--;
            }
            
            // Parameters
            _showParameters = EditorGUILayout.Foldout(_showParameters, "🎚 Parameters", true, EditorStyles.foldoutHeader);
            if (_showParameters)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    "Параметры для управления музыкой из кода:\n" +
                    "SoundManagerSystem.SetMusicParameter(\"intensity\", 0.8f);",
                    MessageType.None
                );
                EditorGUILayout.PropertyField(serializedObject.FindProperty("parameters"), true);
                EditorGUI.indentLevel--;
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawHeader(MusicConfig config)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("🎵 Music Config", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Расширенные настройки музыкальной системы: кроссфейд, вертикальные слои и параметры для адаптивной музыки.", EditorStyles.wordWrappedMiniLabel);
            
            EditorGUILayout.Space(5);
            
            // Status
            int layerCount = config.layers?.Count ?? 0;
            int paramCount = config.parameters?.Count ?? 0;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Слоёв: {layerCount}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"Параметров: {paramCount}", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            
            if (layerCount == 0 && paramCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "Базовая конфигурация готова к работе.\n" +
                    "Добавьте слои и параметры для адаптивной музыки.",
                    MessageType.Info
                );
            }
            
            EditorGUILayout.EndVertical();
        }
    }
}
