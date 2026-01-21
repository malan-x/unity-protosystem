using UnityEngine;
using UnityEditor;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Кастомный редактор для SoundBank
    /// </summary>
    [CustomEditor(typeof(SoundBank))]
    public class SoundBankEditor : UnityEditor.Editor
    {
        private bool _showSounds = true;
        private bool _showAutoLoad = false;
        private bool _showFMOD = false;
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var bank = (SoundBank)target;
            
            // ===== HEADER =====
            DrawHeader(bank);
            
            EditorGUILayout.Space(10);
            
            // Identification
            EditorGUILayout.LabelField("Identification", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bankId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
            
            EditorGUILayout.Space(10);
            
            // Sounds
            _showSounds = EditorGUILayout.Foldout(_showSounds, "🎵 Sounds", true, EditorStyles.foldoutHeader);
            if (_showSounds)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Звуки в этом банке. Загружаются/выгружаются вместе.", MessageType.None);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("entries"), true);
                EditorGUI.indentLevel--;
            }
            
            // Auto-loading
            _showAutoLoad = EditorGUILayout.Foldout(_showAutoLoad, "⚡ Auto-loading", true, EditorStyles.foldoutHeader);
            if (_showAutoLoad)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    "Настройки автоматической загрузки банка.\n" +
                    "• loadOnStartup — загрузить при старте игры\n" +
                    "• loadWithScenes — загрузить при переходе на указанные сцены",
                    MessageType.None
                );
                EditorGUILayout.PropertyField(serializedObject.FindProperty("loadOnStartup"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("loadWithScenes"), true);
                EditorGUI.indentLevel--;
            }
            
            // FMOD
            _showFMOD = EditorGUILayout.Foldout(_showFMOD, "🔊 FMOD Integration", true, EditorStyles.foldoutHeader);
            if (_showFMOD)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Опционально. Путь к FMOD банку для провайдера FMOD.", MessageType.None);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fmodBankPath"));
                EditorGUI.indentLevel--;
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawHeader(SoundBank bank)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("📦 Sound Bank", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Группа звуков для ленивой загрузки/выгрузки. Используйте для оптимизации памяти — " +
                "звуки загружаются только когда нужны (например, при переходе на определённую сцену).",
                EditorStyles.wordWrappedMiniLabel
            );
            
            EditorGUILayout.Space(5);
            
            // Stats
            int soundCount = bank.entries?.Count ?? 0;
            int missingClips = 0;
            
            if (bank.entries != null)
            {
                foreach (var entry in bank.entries)
                {
                    if (entry.clip == null && string.IsNullOrEmpty(entry.fmodEvent))
                        missingClips++;
                }
            }
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Звуков: {soundCount}", GUILayout.Width(80));
            
            if (missingClips > 0)
            {
                GUI.color = new Color(1f, 0.8f, 0.4f);
                EditorGUILayout.LabelField($"⚠ {missingClips} без AudioClip", GUILayout.Width(120));
                GUI.color = Color.white;
            }
            else if (soundCount > 0)
            {
                GUI.color = new Color(0.5f, 0.9f, 0.5f);
                EditorGUILayout.LabelField("✓ Все звуки настроены", GUILayout.Width(140));
                GUI.color = Color.white;
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            // Usage info
            if (soundCount == 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "Как использовать Sound Bank:\n\n" +
                    "1. Добавьте этот банк в SoundLibrary (секция Sound Banks)\n" +
                    "2. Добавьте звуки в секцию Sounds\n" +
                    "3. Настройте автозагрузку или загружайте вручную:\n" +
                    "   SoundManagerSystem.LoadBank(\"bank_id\");\n" +
                    "   SoundManagerSystem.UnloadBank(\"bank_id\");",
                    MessageType.Info
                );
            }
            
            EditorGUILayout.EndVertical();
        }
    }
}
