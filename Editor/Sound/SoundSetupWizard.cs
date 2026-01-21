using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Audio;
using ProtoSystem;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Wizard для создания полной звуковой инфраструктуры за один проход
    /// </summary>
    public class SoundSetupWizard : EditorWindow
    {
        // Пути
        private string _outputFolder;
        private string _configName = "SoundManagerConfig";
        private bool _pathInitialized;
        
        // Что создавать
        private bool _createSoundLibrary = true;
        private bool _createAudioMixer = true;
        private bool _createUIScheme = true;
        private bool _createSessionScheme = true;
        private bool _createMusicConfig = false;
        private bool _generateUISounds = true;
        
        // Базовые настройки
        private SoundProviderType _providerType = SoundProviderType.Unity;
        private int _poolSize = 24;
        private int _maxSimultaneous = 32;
        
        // Громкости
        private float _masterVolume = 1f;
        private float _musicVolume = 0.8f;
        private float _sfxVolume = 1f;
        private float _voiceVolume = 1f;
        private float _ambientVolume = 0.7f;
        private float _uiVolume = 1f;
        
        // Scroll
        private Vector2 _scrollPos;
        
        [MenuItem("Tools/ProtoSystem/Sound/Sound Setup Wizard", priority = 50)]
        public static void ShowWindow()
        {
            var window = GetWindow<SoundSetupWizard>("Sound Setup Wizard");
            window.minSize = new Vector2(450, 620);
            window.Show();
        }
        
        private void OnEnable()
        {
            InitializeDefaultPath();
        }
        
        private void InitializeDefaultPath()
        {
            if (_pathInitialized) return;
            
            var projectConfig = Resources.Load<ProjectConfig>("ProjectConfig");
            
            if (projectConfig != null && !string.IsNullOrEmpty(projectConfig.projectNamespace))
            {
                _outputFolder = $"Assets/{projectConfig.projectNamespace}/Settings/Sound";
            }
            else
            {
                _outputFolder = "Assets/Settings/Sound";
            }
            
            _pathInitialized = true;
        }
        
        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🔊 Sound Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Создаёт полную звуковую инфраструктуру:\n" +
                "• SoundManagerConfig + SoundLibrary + AudioMixer\n" +
                "• 15 готовых UI звуков (процедурная генерация)\n" +
                "• UISoundScheme с настроенными ID",
                MessageType.Info
            );
            
            EditorGUILayout.Space(15);
            
            // === Output Settings ===
            DrawSection("📁 Расположение", () =>
            {
                EditorGUILayout.BeginHorizontal();
                _outputFolder = EditorGUILayout.TextField("Папка", _outputFolder);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var path = EditorUtility.OpenFolderPanel("Выберите папку", "Assets", "Sound");
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (path.StartsWith(Application.dataPath))
                            _outputFolder = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                _configName = EditorGUILayout.TextField("Имя конфига", _configName);
            });
            
            // === What to Create ===
            DrawSection("📦 Что создать", () =>
            {
                EditorGUILayout.LabelField("Основное:", EditorStyles.miniLabel);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Toggle("SoundManagerConfig", true);
                EditorGUI.EndDisabledGroup();
                
                _createSoundLibrary = EditorGUILayout.Toggle("SoundLibrary", _createSoundLibrary);
                _createAudioMixer = EditorGUILayout.Toggle("AudioMixer", _createAudioMixer);
                
                EditorGUILayout.Space(5);
                
                // UI Sounds generation
                EditorGUI.BeginDisabledGroup(!_createSoundLibrary);
                _generateUISounds = EditorGUILayout.Toggle("Сгенерировать UI звуки", _generateUISounds);
                if (_generateUISounds && _createSoundLibrary)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.HelpBox(
                        "15 готовых звуков: click, hover, back, success, error...\n" +
                        "Процедурная генерация — можно заменить на свои.",
                        MessageType.None
                    );
                    EditorGUI.indentLevel--;
                }
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Схемы:", EditorStyles.miniLabel);
                _createUIScheme = EditorGUILayout.Toggle("UISoundScheme", _createUIScheme);
                _createSessionScheme = EditorGUILayout.Toggle("GameSessionSoundScheme", _createSessionScheme);
                _createMusicConfig = EditorGUILayout.Toggle("MusicConfig", _createMusicConfig);
            });
            
            // === Provider Settings ===
            DrawSection("🔌 Провайдер", () =>
            {
                _providerType = (SoundProviderType)EditorGUILayout.EnumPopup("Тип провайдера", _providerType);
                
                if (_providerType == SoundProviderType.Unity)
                {
                    _poolSize = EditorGUILayout.IntSlider("Размер пула AudioSource", _poolSize, 8, 64);
                    _maxSimultaneous = EditorGUILayout.IntSlider("Макс. одновременных звуков", _maxSimultaneous, 16, 128);
                }
                else if (_providerType == SoundProviderType.FMOD)
                {
                    EditorGUILayout.HelpBox("FMOD требует отдельный пакет интеграции.", MessageType.Info);
                }
            });
            
            // === Volume Settings ===
            DrawSection("🔊 Громкости по умолчанию", () =>
            {
                _masterVolume = EditorGUILayout.Slider("Master", _masterVolume, 0f, 1f);
                _musicVolume = EditorGUILayout.Slider("Music", _musicVolume, 0f, 1f);
                _sfxVolume = EditorGUILayout.Slider("SFX", _sfxVolume, 0f, 1f);
                _voiceVolume = EditorGUILayout.Slider("Voice", _voiceVolume, 0f, 1f);
                _ambientVolume = EditorGUILayout.Slider("Ambient", _ambientVolume, 0f, 1f);
                _uiVolume = EditorGUILayout.Slider("UI", _uiVolume, 0f, 1f);
            });
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            
            // === Preview ===
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Будет создано:", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField($"  📁 {_outputFolder}/");
            EditorGUILayout.LabelField($"      ├─ {_configName}.asset");
            if (_createSoundLibrary) EditorGUILayout.LabelField("      ├─ SoundLibrary.asset");
            if (_createAudioMixer) EditorGUILayout.LabelField("      ├─ MainAudioMixer.mixer");
            if (_createUIScheme) EditorGUILayout.LabelField("      ├─ UISoundScheme.asset");
            if (_createSessionScheme) EditorGUILayout.LabelField("      ├─ GameSessionSoundScheme.asset");
            if (_createMusicConfig) EditorGUILayout.LabelField("      ├─ MusicConfig.asset");
            if (_generateUISounds && _createSoundLibrary) EditorGUILayout.LabelField("      └─ Audio/ (15 .wav files)");
            
            EditorGUILayout.LabelField($"  Всего: ~{CountCreated()} файлов", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // === Create Button ===
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("🔨 Создать всё", GUILayout.Height(35)))
            {
                CreateAll();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.Space(10);
        }
        
        private void DrawSection(string title, System.Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            content();
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        private void CreateAll()
        {
            EnsureFolder(_outputFolder);
            
            string audioFolder = $"{_outputFolder}/Audio";
            
            // === Generate UI Sounds ===
            Dictionary<string, AudioClip> generatedClips = null;
            if (_generateUISounds && _createSoundLibrary)
            {
                ProceduralSoundGenerator.GenerateAllUISounds(audioFolder);
                AssetDatabase.Refresh();
                generatedClips = LoadGeneratedClips(audioFolder);
            }
            
            // === SoundLibrary ===
            SoundLibrary library = null;
            if (_createSoundLibrary)
            {
                var libraryPath = $"{_outputFolder}/SoundLibrary.asset";
                library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(libraryPath);
                if (library == null)
                {
                    library = ScriptableObject.CreateInstance<SoundLibrary>();
                    AssetDatabase.CreateAsset(library, libraryPath);
                }
                
                // Populate with UI sound entries
                if (_generateUISounds && generatedClips != null)
                {
                    PopulateSoundLibrary(library, generatedClips);
                    EditorUtility.SetDirty(library);
                }
            }
            
            // === AudioMixer ===
            AudioMixer mixer = null;
            if (_createAudioMixer)
            {
                var mixerPath = $"{_outputFolder}/MainAudioMixer.mixer";
                mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
                if (mixer == null)
                {
                    mixer = SoundMixerGenerator.CreateAudioMixerAt(mixerPath);
                }
            }
            
            // === UISoundScheme ===
            UISoundScheme uiScheme = null;
            if (_createUIScheme)
            {
                var uiSchemePath = $"{_outputFolder}/UISoundScheme.asset";
                uiScheme = AssetDatabase.LoadAssetAtPath<UISoundScheme>(uiSchemePath);
                if (uiScheme == null)
                {
                    uiScheme = ScriptableObject.CreateInstance<UISoundScheme>();
                    AssetDatabase.CreateAsset(uiScheme, uiSchemePath);
                }
                
                // Configure with default IDs
                ConfigureUISoundScheme(uiScheme);
                EditorUtility.SetDirty(uiScheme);
            }
            
            // === GameSessionSoundScheme ===
            GameSessionSoundScheme sessionScheme = null;
            if (_createSessionScheme)
            {
                var sessionSchemePath = $"{_outputFolder}/GameSessionSoundScheme.asset";
                sessionScheme = AssetDatabase.LoadAssetAtPath<GameSessionSoundScheme>(sessionSchemePath);
                if (sessionScheme == null)
                {
                    sessionScheme = ScriptableObject.CreateInstance<GameSessionSoundScheme>();
                    AssetDatabase.CreateAsset(sessionScheme, sessionSchemePath);
                }
            }
            
            // === MusicConfig ===
            MusicConfig musicConfig = null;
            if (_createMusicConfig)
            {
                var musicConfigPath = $"{_outputFolder}/MusicConfig.asset";
                musicConfig = AssetDatabase.LoadAssetAtPath<MusicConfig>(musicConfigPath);
                if (musicConfig == null)
                {
                    musicConfig = ScriptableObject.CreateInstance<MusicConfig>();
                    AssetDatabase.CreateAsset(musicConfig, musicConfigPath);
                }
            }
            
            // === SoundManagerConfig ===
            var configPath = $"{_outputFolder}/{_configName}.asset";
            var config = AssetDatabase.LoadAssetAtPath<SoundManagerConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SoundManagerConfig>();
                AssetDatabase.CreateAsset(config, configPath);
            }
            
            config.providerType = _providerType;
            config.soundLibrary = library;
            config.masterMixer = mixer;
            config.uiScheme = uiScheme;
            config.sessionScheme = sessionScheme;
            config.musicConfig = musicConfig;
            config.audioSourcePoolSize = _poolSize;
            config.maxSimultaneousSounds = _maxSimultaneous;
            
            config.defaultVolumes = new VolumeSettings
            {
                master = _masterVolume,
                music = _musicVolume,
                sfx = _sfxVolume,
                voice = _voiceVolume,
                ambient = _ambientVolume,
                ui = _uiVolume
            };
            
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            
            Debug.Log($"✅ Sound infrastructure created in '{_outputFolder}'");
            
            EditorUtility.DisplayDialog(
                "Sound Setup Complete",
                $"Созданы файлы в:\n{_outputFolder}\n\n" +
                "Следующие шаги:\n" +
                "1. Добавьте SoundManagerSystem на сцену\n" +
                "2. Назначьте созданный конфиг\n" +
                "3. UI звуки уже работают!",
                "OK"
            );
        }
        
        private Dictionary<string, AudioClip> LoadGeneratedClips(string folder)
        {
            var clips = new Dictionary<string, AudioClip>();

            string[] soundIds = {
                // Window sounds
                "ui_whoosh", "ui_close", "ui_modal_open", "ui_modal_close",
                // Button sounds
                "ui_click", "ui_hover", "ui_disabled",
                // Navigation
                "ui_navigate", "ui_back", "ui_tab",
                // Feedback
                "ui_success", "ui_error", "ui_warning", "ui_notification",
                // Controls
                "ui_slider", "ui_toggle_on", "ui_toggle_off", "ui_dropdown", "ui_select"
            };

            foreach (var id in soundIds)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{folder}/{id}.wav");
                if (clip != null)
                {
                    clips[id] = clip;
                }
            }

            return clips;
        }
        
        private void PopulateSoundLibrary(SoundLibrary library, Dictionary<string, AudioClip> clips)
        {
            library.coreEntries.Clear();

            // Window sounds
            AddEntry(library, "ui_whoosh", SoundCategory.UI, clips, volume: 0.5f);
            AddEntry(library, "ui_close", SoundCategory.UI, clips, volume: 0.4f);
            AddEntry(library, "ui_modal_open", SoundCategory.UI, clips, volume: 0.6f);
            AddEntry(library, "ui_modal_close", SoundCategory.UI, clips, volume: 0.5f);

            // Button sounds
            AddEntry(library, "ui_click", SoundCategory.UI, clips, volume: 0.7f);
            AddEntry(library, "ui_hover", SoundCategory.UI, clips, volume: 0.4f);
            AddEntry(library, "ui_disabled", SoundCategory.UI, clips, volume: 0.3f);

            // Navigation
            AddEntry(library, "ui_navigate", SoundCategory.UI, clips, volume: 0.5f);
            AddEntry(library, "ui_back", SoundCategory.UI, clips, volume: 0.6f);
            AddEntry(library, "ui_tab", SoundCategory.UI, clips, volume: 0.4f);

            // Feedback
            AddEntry(library, "ui_success", SoundCategory.UI, clips, volume: 0.6f);
            AddEntry(library, "ui_error", SoundCategory.UI, clips, volume: 0.7f);
            AddEntry(library, "ui_warning", SoundCategory.UI, clips, volume: 0.6f);
            AddEntry(library, "ui_notification", SoundCategory.UI, clips, volume: 0.5f);

            // Controls
            AddEntry(library, "ui_slider", SoundCategory.UI, clips, volume: 0.3f, cooldown: 0.05f);
            AddEntry(library, "ui_toggle_on", SoundCategory.UI, clips, volume: 0.5f);
            AddEntry(library, "ui_toggle_off", SoundCategory.UI, clips, volume: 0.5f);
            AddEntry(library, "ui_dropdown", SoundCategory.UI, clips, volume: 0.5f);
            AddEntry(library, "ui_select", SoundCategory.UI, clips, volume: 0.5f);
        }
        
        private void AddEntry(SoundLibrary library, string id, SoundCategory category, 
            Dictionary<string, AudioClip> clips, float volume = 1f, float cooldown = 0f)
        {
            var entry = new SoundEntry
            {
                id = id,
                category = category,
                clip = clips.GetValueOrDefault(id),
                volume = volume,
                pitch = 1f,
                pitchVariation = 0f,
                loop = false,
                spatial = false,
                priority = SoundPriority.Normal,
                cooldown = cooldown
            };
            
            library.coreEntries.Add(entry);
        }
        
        private void ConfigureUISoundScheme(UISoundScheme scheme)
        {
            // Window events
            scheme.windowOpen = "ui_whoosh";
            scheme.windowClose = "ui_close";
            scheme.modalOpen = "ui_modal_open";
            scheme.modalClose = "ui_modal_close";

            // Button events
            scheme.buttonClick = "ui_click";
            scheme.buttonHover = "ui_hover";
            scheme.buttonDisabled = "ui_disabled";

            // Navigation
            scheme.navigate = "ui_navigate";
            scheme.back = "ui_back";
            scheme.tabSwitch = "ui_tab";

            // Feedback
            scheme.success = "ui_success";
            scheme.error = "ui_error";
            scheme.warning = "ui_warning";
            scheme.notification = "ui_notification";

            // Controls
            scheme.sliderChange = "ui_slider";
            scheme.toggleOn = "ui_toggle_on";
            scheme.toggleOff = "ui_toggle_off";
            scheme.dropdownOpen = "ui_dropdown";
            scheme.dropdownSelect = "ui_select";
        }
        
        private int CountCreated()
        {
            int count = 1; // SoundManagerConfig
            if (_createSoundLibrary) count++;
            if (_createAudioMixer) count++;
            if (_createUIScheme) count++;
            if (_createSessionScheme) count++;
            if (_createMusicConfig) count++;
            if (_generateUISounds && _createSoundLibrary) count += 19; // Window + Button + Nav + Feedback + Controls
            return count;
        }
        
        private void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            
            var parts = path.Split('/');
            var current = parts[0];
            
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
