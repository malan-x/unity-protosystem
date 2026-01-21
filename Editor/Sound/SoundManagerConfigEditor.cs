using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Audio;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Кастомный редактор для SoundManagerConfig
    /// </summary>
    [CustomEditor(typeof(SoundManagerConfig))]
    public class SoundManagerConfigEditor : UnityEditor.Editor
    {
        private bool _showProvider = true;
        private bool _showLibrary = true;
        private bool _showMixer = true;
        private bool _showSchemes = false;
        private bool _showVolumes = false;
        private bool _showUnityProvider = false;
        private bool _showPlayback = false;
        private bool _show3D = false;
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var config = (SoundManagerConfig)target;
            
            // ===== HEADER =====
            DrawHeader(config);
            
            EditorGUILayout.Space(10);
            
            // ===== SECTIONS =====
            
            // Provider
            _showProvider = DrawSection("🔌 Provider", _showProvider, 
                "Выбор аудио-движка (Unity, FMOD, Wwise)", () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("providerType"));
                
                var providerType = (SoundProviderType)serializedObject.FindProperty("providerType").enumValueIndex;
                
                if (providerType == SoundProviderType.FMOD)
                {
                    EditorGUILayout.HelpBox(
                        "FMOD provider requires separate integration package.",
                        MessageType.Info
                    );
                }
                else if (providerType == SoundProviderType.Wwise)
                {
                    EditorGUILayout.HelpBox(
                        "Wwise provider is not yet implemented.",
                        MessageType.Warning
                    );
                }
            });
            
            // Library (required)
            _showLibrary = DrawSection("📚 Library", _showLibrary,
                "Содержит все звуки проекта. Обязательно.", () =>
            {
                DrawAssetFieldWithCreate<SoundLibrary>("soundLibrary", "Sound Library", config, true);
            });
            
            // Audio Mixer (recommended)
            _showMixer = DrawSection("🎚 Audio Mixer", _showMixer,
                "Управление громкостью по категориям. Рекомендуется.", () =>
            {
                DrawMixerFieldWithCreate("masterMixer", "Master Mixer", config);
                
                if (config.masterMixer != null)
                {
                    if (!SoundMixerGenerator.ValidateMixer(config.masterMixer))
                    {
                        EditorGUILayout.HelpBox("Some exposed parameters are missing!", MessageType.Warning);
                    }
                    
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("mixerGroupNames"), true);
                }
            });
            
            // Schemes (optional)
            _showSchemes = DrawSection("🎨 Sound Schemes", _showSchemes,
                "Автоматизация звуков для UI и GameSession. Опционально.", () =>
            {
                DrawAssetFieldWithCreate<UISoundScheme>("uiScheme", "UI Scheme", config, false);
                DrawAssetFieldWithCreate<GameSessionSoundScheme>("sessionScheme", "Session Scheme", config, false);
                DrawAssetFieldWithCreate<MusicConfig>("musicConfig", "Music Config", config, false);
            });
            
            // Default Volumes
            _showVolumes = DrawSection("🔊 Default Volumes", _showVolumes,
                "Начальные значения громкости по категориям.", () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultVolumes"), true);
            });
            
            // Unity Provider Settings
            var providerTypeProp = serializedObject.FindProperty("providerType");
            if ((SoundProviderType)providerTypeProp.enumValueIndex == SoundProviderType.Unity)
            {
                _showUnityProvider = DrawSection("🎮 Unity Provider", _showUnityProvider,
                    "Настройки пула AudioSource для Unity провайдера.", () =>
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("audioSourcePoolSize"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("maxSimultaneousSounds"));
                });
            }
            
            // Playback Control
            _showPlayback = DrawSection("⚙ Playback Control", _showPlayback,
                "Приоритеты и cooldown для управления воспроизведением.", () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("priority"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cooldown"), true);
            });
            
            // 3D Sound
            _show3D = DrawSection("🌍 3D Sound Defaults", _show3D,
                "Настройки пространственного звука по умолчанию.", () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("default3DMinDistance"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("default3DMaxDistance"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("rolloffMode"));
            });
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawHeader(SoundManagerConfig config)
        {
            // Title
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🔊 Sound Manager Config", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            // Status indicator
            var status = GetConfigStatus(config);
            GUI.color = status.color;
            EditorGUILayout.LabelField(status.text, EditorStyles.miniLabel, GUILayout.Width(100));
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            // Description
            EditorGUILayout.LabelField(
                "Главный конфиг звуковой системы. Связывает библиотеку звуков, миксер и схемы воспроизведения.",
                EditorStyles.wordWrappedMiniLabel
            );
            
            EditorGUILayout.Space(5);
            
            // Action buttons
            EditorGUILayout.BeginHorizontal();
            
            if (config.soundLibrary == null)
            {
                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button("🔧 Open Setup Wizard", GUILayout.Height(24)))
                {
                    SoundSetupWizard.ShowWindow();
                }
                GUI.backgroundColor = Color.white;
            }
            
            if (GUILayout.Button("📖 Documentation", GUILayout.Height(24), GUILayout.Width(110)))
            {
                Application.OpenURL("https://github.com/your-repo/protosystem/wiki/Sound");
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private (string text, Color color) GetConfigStatus(SoundManagerConfig config)
        {
            int missing = 0;
            
            if (config.soundLibrary == null) missing++;
            // Mixer и Schemes опциональны, не считаем
            
            if (missing > 0)
                return ($"⚠ {missing} required", new Color(1f, 0.7f, 0.3f));
            
            if (config.masterMixer == null)
                return ("⚡ Basic", new Color(0.7f, 0.85f, 1f));
            
            return ("✓ Ready", new Color(0.5f, 0.9f, 0.5f));
        }
        
        private bool DrawSection(string title, bool isExpanded, string tooltip, System.Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header with foldout
            EditorGUILayout.BeginHorizontal();
            isExpanded = EditorGUILayout.Foldout(isExpanded, title, true, EditorStyles.foldoutHeader);
            EditorGUILayout.EndHorizontal();
            
            // Collapsed hint
            if (!isExpanded)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                EditorGUILayout.LabelField(tooltip, EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            
            // Content
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                content();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
            
            return isExpanded;
        }
        
        private void DrawAssetFieldWithCreate<T>(string propertyName, string label, SoundManagerConfig config, bool required) where T : ScriptableObject
        {
            var prop = serializedObject.FindProperty(propertyName);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            
            if (prop.objectReferenceValue == null)
            {
                if (GUILayout.Button("Create", GUILayout.Width(55)))
                {
                    CreateAsset<T>(propertyName, config);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            if (prop.objectReferenceValue == null && required)
            {
                EditorGUILayout.HelpBox($"{label} is required!", MessageType.Error);
            }
        }
        
        private void DrawMixerFieldWithCreate(string propertyName, string label, SoundManagerConfig config)
        {
            var prop = serializedObject.FindProperty(propertyName);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            
            if (prop.objectReferenceValue == null)
            {
                if (GUILayout.Button("Create", GUILayout.Width(55)))
                {
                    CreateMixer(propertyName, config);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void CreateAsset<T>(string propertyName, SoundManagerConfig config) where T : ScriptableObject
        {
            string directory = GetConfigDirectory(config);
            string typeName = typeof(T).Name;
            string fullPath = $"{directory}/{typeName}.asset";
            
            var existing = AssetDatabase.LoadAssetAtPath<T>(fullPath);
            if (existing != null)
            {
                serializedObject.FindProperty(propertyName).objectReferenceValue = existing;
                serializedObject.ApplyModifiedProperties();
                return;
            }
            
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, fullPath);
            
            serializedObject.FindProperty(propertyName).objectReferenceValue = asset;
            serializedObject.ApplyModifiedProperties();
            
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"✅ Created {typeName} at {fullPath}");
        }
        
        private void CreateMixer(string propertyName, SoundManagerConfig config)
        {
            string directory = GetConfigDirectory(config);
            string fullPath = $"{directory}/MainAudioMixer.mixer";
            
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(fullPath);
            if (existing != null)
            {
                serializedObject.FindProperty(propertyName).objectReferenceValue = existing;
                serializedObject.ApplyModifiedProperties();
                return;
            }
            
            var mixer = SoundMixerGenerator.CreateAudioMixerAt(fullPath);
            
            if (mixer != null)
            {
                serializedObject.FindProperty(propertyName).objectReferenceValue = mixer;
                serializedObject.ApplyModifiedProperties();
                
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }
        
        private string GetConfigDirectory(SoundManagerConfig config)
        {
            return Path.GetDirectoryName(AssetDatabase.GetAssetPath(config));
        }
    }
}
