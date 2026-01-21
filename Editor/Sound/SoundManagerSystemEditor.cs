using UnityEngine;
using UnityEditor;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Кастомный редактор для SoundManagerSystem
    /// </summary>
    [CustomEditor(typeof(SoundManagerSystem))]
    public class SoundManagerSystemEditor : ProtoSystem.InitializableSystemEditor
    {
        private bool _showDebugFoldout = true;
        
        public override void OnInspectorGUI()
        {
            var system = (SoundManagerSystem)target;
            var configProp = serializedObject.FindProperty("config");
            
            // ===== HEADER =====
            DrawHeader(system, configProp);
            
            EditorGUILayout.Space(10);
            
            // Base inspector (config field, etc.)
            base.OnInspectorGUI();
            
            EditorGUILayout.Space(10);
            
            // Debug section
            _showDebugFoldout = EditorGUILayout.Foldout(_showDebugFoldout, "🔊 Runtime Debug", true, EditorStyles.foldoutHeader);
            if (_showDebugFoldout)
            {
                EditorGUI.indentLevel++;
                
                if (Application.isPlaying && system.IsInitialized)
                {
                    DrawRuntimeDebug(system);
                }
                else
                {
                    EditorGUILayout.HelpBox("Debug информация доступна в Play Mode после инициализации системы.", MessageType.Info);
                }
                
                EditorGUI.indentLevel--;
            }
        }
        
        private void DrawHeader(SoundManagerSystem system, SerializedProperty configProp)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("🔊 Sound Manager System", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Центральная система управления звуком. Воспроизводит звуки, музыку, управляет громкостью и snapshots.", EditorStyles.wordWrappedMiniLabel);
            
            EditorGUILayout.Space(5);
            
            // Status
            if (configProp.objectReferenceValue == null)
            {
                GUI.color = new Color(1f, 0.6f, 0.4f);
                EditorGUILayout.LabelField("⚠ Требуется конфиг", EditorStyles.boldLabel);
                GUI.color = Color.white;
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("🔧 Open Sound Setup Wizard", GUILayout.Height(26)))
                {
                    SoundSetupWizard.ShowWindow();
                }
            }
            else
            {
                var config = (SoundManagerConfig)configProp.objectReferenceValue;
                
                if (config.soundLibrary == null)
                {
                    GUI.color = new Color(1f, 0.8f, 0.4f);
                    EditorGUILayout.LabelField("⚠ В конфиге отсутствует Sound Library", EditorStyles.boldLabel);
                    GUI.color = Color.white;
                }
                else if (Application.isPlaying && system.IsInitialized)
                {
                    GUI.color = new Color(0.5f, 0.9f, 0.5f);
                    EditorGUILayout.LabelField("✓ Система активна", EditorStyles.boldLabel);
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = new Color(0.5f, 0.9f, 0.5f);
                    EditorGUILayout.LabelField("✓ Готов к работе", EditorStyles.boldLabel);
                    GUI.color = Color.white;
                }
                
                EditorGUILayout.Space(5);
                
                // Quick links
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("📝 Config", GUILayout.Height(22)))
                {
                    Selection.activeObject = config;
                }
                
                if (config.soundLibrary != null)
                {
                    if (GUILayout.Button("📚 Library", GUILayout.Height(22)))
                    {
                        Selection.activeObject = config.soundLibrary;
                    }
                }
                
                if (GUILayout.Button("🔧 Wizard", GUILayout.Height(22)))
                {
                    SoundSetupWizard.ShowWindow();
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawRuntimeDebug(SoundManagerSystem system)
        {
            var provider = system.Provider;
            
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            
            // Sound count with progress bar
            float soundUsage = (float)provider.ActiveSoundCount / provider.MaxSimultaneousSounds;
            EditorGUI.ProgressBar(
                EditorGUILayout.GetControlRect(GUILayout.Height(18)),
                soundUsage,
                $"Active Sounds: {provider.ActiveSoundCount} / {provider.MaxSimultaneousSounds}"
            );
            
            EditorGUILayout.LabelField("Muted", provider.IsMuted() ? "Yes" : "No");
            
            EditorGUILayout.Space(5);
            
            // Volumes
            EditorGUILayout.LabelField("Volumes", EditorStyles.boldLabel);
            DrawVolumeBar("Master", provider.GetVolume(SoundCategory.Master));
            DrawVolumeBar("Music", provider.GetVolume(SoundCategory.Music));
            DrawVolumeBar("SFX", provider.GetVolume(SoundCategory.SFX));
            DrawVolumeBar("UI", provider.GetVolume(SoundCategory.UI));
            
            EditorGUILayout.Space(5);
            
            // Controls
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("▶ Test Click", GUILayout.Height(24)))
            {
                SoundManagerSystem.Play("ui_click");
            }
            
            if (GUILayout.Button("▶ Test Success", GUILayout.Height(24)))
            {
                SoundManagerSystem.Play("ui_success");
            }
            
            if (GUILayout.Button("■ Stop All", GUILayout.Height(24)))
            {
                provider.StopAll();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawVolumeBar(string label, float value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(50));
            
            var rect = GUILayoutUtility.GetRect(100, 16, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(rect, value, $"{value:P0}");
            
            EditorGUILayout.EndHorizontal();
        }
    }
}
