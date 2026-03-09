// Packages/com.protosystem.core/Editor/UI/UIGeneratorWindow.cs
using UnityEngine;
using UnityEngine.Audio;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using ProtoSystem.Editor.Sound;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Окно для управления генерацией UI элементов и спрайтов.
    /// Поддерживает два режима: стилизованные префабы (с UIStyleConfiguration) и стандартные.
    /// </summary>
    public class UIGeneratorWindow : EditorWindow
    {
        // Режим генерации
        private enum GenerationMode
        {
            Styled,     // С UIStyleConfiguration — кастомные цвета, закруглённые углы
            Standard    // Без конфига — базовые Unity UI элементы
        }
        
        private GenerationMode generationMode = GenerationMode.Styled;
        
        // Конфигурация стиля (для Styled режима)
        private UIStyleConfiguration selectedConfig;
        private UIStylePreset selectedPreset = UIStylePreset.Modern;
        
        // Настройки генерации
        private string outputPath = "Assets/UI/Generated";
        private Vector2 scrollPosition;
        
        private bool generateSprites = true;
        private bool generatePrefabs = true;
        private bool overwriteWithoutPrompt = true;
        
        // Звуковая интеграция
        private bool soundIntegration = true;
        private bool hoverSounds = false;
        
        // Настройки аудио для Settings окна
        private AudioMixer audioMixer;
        private List<ExposedAudioParameter> audioParameters = new List<ExposedAudioParameter>();
        private bool audioSettingsFoldout = true;
        
        // Предпросмотр
        private Sprite previewCheckmark;
        private Sprite previewArrowDown;
        private Sprite previewButton;
        
        // Foldouts
        private bool styleFoldout = true;
        private bool settingsFoldout = true;
        private bool soundFoldout = true;
        private bool previewFoldout = false;

        [MenuItem("ProtoSystem/UI/Tools/UI Generator", priority = 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<UIGeneratorWindow>("UI Generator");
            window.minSize = new Vector2(450, 750);
            window.Show();
        }

        private void OnEnable()
        {
            LoadPreferences();
            
            if (selectedConfig == null && generationMode == GenerationMode.Styled)
            {
                selectedConfig = FindOrCreateDefaultConfig();
            }
            
            // Пытаемся найти AudioMixer автоматически
            if (audioMixer == null)
            {
                audioMixer = FindAudioMixer();
                if (audioMixer != null)
                {
                    RefreshAudioParameters();
                }
            }
        }
        
        private void OnDisable()
        {
            SavePreferences();
        }
        
        private void LoadPreferences()
        {
            outputPath = EditorPrefs.GetString("ProtoSystem.UIGenerator.OutputPath", "Assets/UI/Generated");
            soundIntegration = EditorPrefs.GetBool("ProtoSystem.UIGenerator.SoundIntegration", true);
            hoverSounds = EditorPrefs.GetBool("ProtoSystem.UIGenerator.HoverSounds", false);
            generationMode = (GenerationMode)EditorPrefs.GetInt("ProtoSystem.UIGenerator.Mode", 0);
            
            // Загрузка AudioMixer по GUID
            string mixerGuid = EditorPrefs.GetString("ProtoSystem.UIGenerator.AudioMixerGuid", "");
            if (!string.IsNullOrEmpty(mixerGuid))
            {
                string mixerPath = AssetDatabase.GUIDToAssetPath(mixerGuid);
                if (!string.IsNullOrEmpty(mixerPath))
                {
                    audioMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
                    if (audioMixer != null)
                    {
                        RefreshAudioParameters();
                        LoadAudioParameterPreferences();
                    }
                }
            }
        }
        
        private void SavePreferences()
        {
            EditorPrefs.SetString("ProtoSystem.UIGenerator.OutputPath", outputPath);
            EditorPrefs.SetBool("ProtoSystem.UIGenerator.SoundIntegration", soundIntegration);
            EditorPrefs.SetBool("ProtoSystem.UIGenerator.HoverSounds", hoverSounds);
            EditorPrefs.SetInt("ProtoSystem.UIGenerator.Mode", (int)generationMode);
            
            // Сохранение AudioMixer по GUID
            if (audioMixer != null)
            {
                string mixerPath = AssetDatabase.GetAssetPath(audioMixer);
                string mixerGuid = AssetDatabase.AssetPathToGUID(mixerPath);
                EditorPrefs.SetString("ProtoSystem.UIGenerator.AudioMixerGuid", mixerGuid);
                SaveAudioParameterPreferences();
            }
            else
            {
                EditorPrefs.DeleteKey("ProtoSystem.UIGenerator.AudioMixerGuid");
            }
        }
        
        private void LoadAudioParameterPreferences()
        {
            foreach (var param in audioParameters)
            {
                param.enabled = EditorPrefs.GetBool($"ProtoSystem.UIGenerator.AudioParam.{param.name}.Enabled", true);
                param.displayName = EditorPrefs.GetString($"ProtoSystem.UIGenerator.AudioParam.{param.name}.DisplayName", 
                    ExposedAudioParameter.GetDefaultDisplayName(param.name));
            }
        }
        
        private void SaveAudioParameterPreferences()
        {
            foreach (var param in audioParameters)
            {
                EditorPrefs.SetBool($"ProtoSystem.UIGenerator.AudioParam.{param.name}.Enabled", param.enabled);
                EditorPrefs.SetString($"ProtoSystem.UIGenerator.AudioParam.{param.name}.DisplayName", param.displayName);
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawModeSelection();
            EditorGUILayout.Space(10);
            
            if (generationMode == GenerationMode.Styled)
            {
                DrawStyleConfiguration();
                EditorGUILayout.Space(10);
            }
            
            DrawOutputSettings();
            EditorGUILayout.Space(10);
            
            DrawSoundIntegration();
            EditorGUILayout.Space(10);
            
            DrawAudioSettingsConfiguration();
            EditorGUILayout.Space(10);

            DrawCommunityPanelSection();
            EditorGUILayout.Space(10);
            
            if (generationMode == GenerationMode.Styled)
            {
                DrawPreview();
                EditorGUILayout.Space(10);
            }
            
            DrawGenerateButtons();
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUILayout.Label("🎨 UI Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField(
                "Генератор UI префабов для ProtoSystem.\n" +
                "Создаёт готовые окна: MainMenu, Settings, Pause, GameOver и др.",
                EditorStyles.wordWrappedLabel
            );
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawModeSelection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUILayout.Label("📋 Режим генерации", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // Styled mode
            bool isStyled = generationMode == GenerationMode.Styled;
            bool newStyled = EditorGUILayout.ToggleLeft(
                new GUIContent("🎨 Стилизованные префабы", 
                    "Использует UIStyleConfiguration для кастомных цветов, " +
                    "закруглённых углов и современного внешнего вида"),
                isStyled
            );
            
            if (newStyled && !isStyled)
            {
                generationMode = GenerationMode.Styled;
            }
            
            // Standard mode  
            bool isStandard = generationMode == GenerationMode.Standard;
            bool newStandard = EditorGUILayout.ToggleLeft(
                new GUIContent("📦 Стандартные префабы", 
                    "Базовые Unity UI элементы без кастомных стилей. " +
                    "Подходит для быстрого прототипирования или собственной стилизации"),
                isStandard
            );
            
            if (newStandard && !isStandard)
            {
                generationMode = GenerationMode.Standard;
            }
            
            // Подсказка
            EditorGUILayout.Space(5);
            if (generationMode == GenerationMode.Styled)
            {
                EditorGUILayout.HelpBox(
                    "Стилизованные префабы включают:\n" +
                    "• Кастомные цвета из UIStyleConfiguration\n" +
                    "• Закруглённые углы (9-slice спрайты)\n" +
                    "• Hover-эффекты на кнопках\n" +
                    "• Современный тёмный дизайн",
                    MessageType.None
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Стандартные префабы включают:\n" +
                    "• Базовые Unity UI компоненты\n" +
                    "• Минимальная стилизация\n" +
                    "• Легко кастомизировать вручную",
                    MessageType.None
                );
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawStyleConfiguration()
        {
            styleFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(styleFoldout, "🎨 Конфигурация стиля");
            
            if (styleFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUI.BeginChangeCheck();
                selectedConfig = (UIStyleConfiguration)EditorGUILayout.ObjectField(
                    "Style Config",
                    selectedConfig,
                    typeof(UIStyleConfiguration),
                    false
                );
                
                if (EditorGUI.EndChangeCheck() && selectedConfig != null)
                {
                    selectedPreset = selectedConfig.stylePreset;
                }

                if (GUILayout.Button("Создать новую конфигурацию", GUILayout.Height(25)))
                {
                    CreateNewConfiguration();
                }

                if (selectedConfig != null)
                {
                    EditorGUILayout.Space(5);
                    
                    EditorGUI.BeginChangeCheck();
                    selectedPreset = (UIStylePreset)EditorGUILayout.EnumPopup("Preset", selectedPreset);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(selectedConfig, "Change UI Style Preset");
                        selectedConfig.ApplyPreset(selectedPreset);
                        EditorUtility.SetDirty(selectedConfig);
                    }

                    DrawConfigPreview();
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Выберите или создайте конфигурацию стиля для генерации стилизованных префабов.",
                        MessageType.Warning
                    );
                }
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawConfigPreview()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Предпросмотр цветов:", EditorStyles.miniBoldLabel);
            
            GUI.enabled = false;
            EditorGUILayout.ColorField("Background", selectedConfig.backgroundColor);
            EditorGUILayout.ColorField("Accent", selectedConfig.accentColor);
            EditorGUILayout.ColorField("Text", selectedConfig.textColor);
            EditorGUILayout.ColorField("Border", selectedConfig.borderColor);
            GUI.enabled = true;
        }

        private void DrawOutputSettings()
        {
            settingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(settingsFoldout, "⚙️ Настройки генерации");
            
            if (settingsFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.BeginHorizontal();
                outputPath = EditorGUILayout.TextField("Путь сохранения", outputPath);
                
                if (GUILayout.Button("Обзор...", GUILayout.Width(80)))
                {
                    string path = EditorUtility.OpenFolderPanel("Выберите папку для генерации", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (path.StartsWith(Application.dataPath))
                        {
                            outputPath = "Assets" + path.Substring(Application.dataPath.Length);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                if (generationMode == GenerationMode.Styled)
                {
                    generateSprites = EditorGUILayout.ToggleLeft(
                        new GUIContent("Генерировать спрайты", "9-slice спрайты для кнопок, панелей и контролов"),
                        generateSprites
                    );
                }
                
                generatePrefabs = EditorGUILayout.ToggleLeft(
                    new GUIContent("Генерировать префабы окон", "MainMenu, Settings, Pause, GameOver и др."),
                    generatePrefabs
                );
                
                overwriteWithoutPrompt = EditorGUILayout.ToggleLeft(
                    new GUIContent("Перезаписывать без запроса", "Автоматически заменять существующие файлы"),
                    overwriteWithoutPrompt
                );
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawSoundIntegration()
        {
            soundFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(soundFoldout, "🔊 Звуковая интеграция");
            
            if (soundFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                soundIntegration = EditorGUILayout.ToggleLeft(
                    new GUIContent("Добавить звуковые компоненты", 
                        "Добавляет PlaySoundOn, UIToggleSound, UISliderSound на UI элементы"),
                    soundIntegration
                );
                
                EditorGUI.BeginDisabledGroup(!soundIntegration);
                EditorGUI.indentLevel++;
                
                hoverSounds = EditorGUILayout.ToggleLeft(
                    new GUIContent("Hover звуки на кнопках", 
                        "Добавляет звук при наведении на кнопки (ui_hover)"),
                    hoverSounds
                );
                
                EditorGUI.indentLevel--;
                EditorGUI.EndDisabledGroup();
                
                if (soundIntegration)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(
                        "Компоненты звука:\n" +
                        "• Button → PlaySoundOn (ui_click" + (hoverSounds ? ", ui_hover" : "") + ")\n" +
                        "• Toggle → UIToggleSound (ui_toggle_on/off)\n" +
                        "• Slider → UISliderSound (ui_slider)\n" +
                        "• Dropdown → PlaySoundOn (ui_dropdown, ui_select)\n\n" +
                        "Требуется SoundManagerSystem с настроенной SoundLibrary.",
                        MessageType.None
                    );
                }
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawAudioSettingsConfiguration()
        {
            audioSettingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(audioSettingsFoldout, "🎚️ Аудио каналы в Settings");
            
            if (audioSettingsFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.LabelField("Слайдеры громкости в окне Settings:", EditorStyles.miniLabel);
                EditorGUILayout.Space(5);
                
                // Поле AudioMixer
                EditorGUI.BeginChangeCheck();
                audioMixer = (AudioMixer)EditorGUILayout.ObjectField(
                    new GUIContent("Audio Mixer", "Укажите AudioMixer для автоматического определения каналов"),
                    audioMixer,
                    typeof(AudioMixer),
                    false
                );
                
                if (EditorGUI.EndChangeCheck())
                {
                    RefreshAudioParameters();
                }
                
                if (audioMixer == null)
                {
                    EditorGUILayout.Space(5);
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.HelpBox(
                        "Укажите AudioMixer для автоматического определения каналов громкости.",
                        MessageType.Info
                    );
                    
                    if (GUILayout.Button("Найти", GUILayout.Width(60), GUILayout.Height(38)))
                    {
                        audioMixer = FindAudioMixer();
                        if (audioMixer != null)
                        {
                            RefreshAudioParameters();
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Не найдено", 
                                "AudioMixer не найден в проекте.\n\n" +
                                "Создайте через:\nProtoSystem → Sound → Sound Setup Wizard", 
                                "OK");
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.Space(5);
                    
                    if (audioParameters.Count == 0)
                    {
                        EditorGUILayout.HelpBox(
                            "AudioMixer не содержит exposed параметров громкости.\n" +
                            "Добавьте параметры (ПКМ → Expose 'Volume' to script).",
                            MessageType.Warning
                        );
                    }
                    else
                    {
                        // Кнопки выбора
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Выбрать все", EditorStyles.miniButtonLeft))
                        {
                            foreach (var p in audioParameters) p.enabled = true;
                        }
                        if (GUILayout.Button("Снять все", EditorStyles.miniButtonMid))
                        {
                            foreach (var p in audioParameters) p.enabled = false;
                        }
                        if (GUILayout.Button("Обновить", EditorStyles.miniButtonRight))
                        {
                            RefreshAudioParameters();
                        }
                        EditorGUILayout.EndHorizontal();
                        
                        EditorGUILayout.Space(5);
                        
                        // Таблица параметров
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("", GUILayout.Width(20));
                        EditorGUILayout.LabelField("Параметр", EditorStyles.miniLabel, GUILayout.Width(120));
                        EditorGUILayout.LabelField("Отображаемое имя", EditorStyles.miniLabel);
                        EditorGUILayout.EndHorizontal();
                        
                        foreach (var param in audioParameters)
                        {
                            EditorGUILayout.BeginHorizontal();
                            
                            param.enabled = EditorGUILayout.Toggle(param.enabled, GUILayout.Width(20));
                            
                            EditorGUI.BeginDisabledGroup(!param.enabled);
                            EditorGUILayout.LabelField(param.name, GUILayout.Width(120));
                            param.displayName = EditorGUILayout.TextField(param.displayName);
                            EditorGUI.EndDisabledGroup();
                            
                            EditorGUILayout.EndHorizontal();
                        }
                        
                        EditorGUILayout.Space(5);
                        
                        int enabledCount = audioParameters.FindAll(p => p.enabled).Count;
                        EditorGUILayout.LabelField($"Выбрано каналов: {enabledCount}", EditorStyles.centeredGreyMiniLabel);
                    }
                }
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawPreview()
        {
            previewFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(previewFoldout, "👁️ Предпросмотр спрайтов");
            
            if (previewFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                if (generateSprites && selectedConfig != null)
                {
                    EditorGUILayout.LabelField("После генерации здесь будут показаны примеры спрайтов:", EditorStyles.miniLabel);
                    
                    EditorGUILayout.BeginHorizontal();
                    DrawSpritePreview("Checkmark", previewCheckmark);
                    DrawSpritePreview("Arrow Down", previewArrowDown);
                    DrawSpritePreview("Button", previewButton);
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("Выберите конфигурацию для предпросмотра", EditorStyles.centeredGreyMiniLabel);
                }
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawSpritePreview(string label, Sprite sprite)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            
            EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel);
            
            Rect rect = GUILayoutUtility.GetRect(80, 80, GUILayout.Width(80), GUILayout.Height(80));
            
            if (sprite != null)
            {
                GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawCommunityPanelSection()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("📡 Community Panel", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Префаб панели связи с игроками (опросы, новости, devlog, рейтинг).", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(3);
            if (GUILayout.Button("Сгенерировать CommunityPanelWindow", GUILayout.Height(28)))
            {
                SavePreferences();
                ApplyGeneratorSettings();
                UIWindowPrefabGenerator.OverwriteWithoutPrompt = overwriteWithoutPrompt;
                UIWindowPrefabGenerator.GenerateCommunityPanel();
                ClearGeneratorSettings();
            }
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Сгенерировать Stub-конфиги (4 шт.)", GUILayout.Height(24)))
            {
                SavePreferences();
                ApplyGeneratorSettings();
                UIWindowPrefabGenerator.OverwriteWithoutPrompt = overwriteWithoutPrompt;
                UIWindowPrefabGenerator.GenerateLiveOpsStubs();
                ClearGeneratorSettings();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawGenerateButtons()
        {
            EditorGUILayout.Space(10);
            
            // Проверка готовности
            bool canGenerate = generationMode == GenerationMode.Standard || selectedConfig != null;
            
            GUI.enabled = canGenerate;
            
            // Главная кнопка
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };
            
            string modeLabel = generationMode == GenerationMode.Styled ? "стилизованные" : "стандартные";
            
            if (GUILayout.Button($"🚀 Генерировать ВСЁ ({modeLabel})", buttonStyle))
            {
                GenerateAll();
            }
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            if (generationMode == GenerationMode.Styled)
            {
                if (GUILayout.Button("Только спрайты", GUILayout.Height(30)))
                {
                    GenerateSprites();
                }
            }
            
            if (GUILayout.Button("Только префабы", GUILayout.Height(30)))
            {
                GeneratePrefabs();
            }
            
            EditorGUILayout.EndHorizontal();
            
            GUI.enabled = true;
            
            if (!canGenerate && generationMode == GenerationMode.Styled)
            {
                EditorGUILayout.HelpBox("Выберите UIStyleConfiguration для генерации стилизованных префабов.", MessageType.Warning);
            }
        }
        
        private void RefreshAudioParameters()
        {
            if (audioMixer == null)
            {
                audioParameters.Clear();
                return;
            }
            
            var newParams = AudioMixerUtility.GetExposedParameters(audioMixer);
            
            // Сохраняем состояние существующих параметров
            var oldState = new Dictionary<string, (bool enabled, string displayName)>();
            foreach (var p in audioParameters)
            {
                oldState[p.name] = (p.enabled, p.displayName);
            }
            
            audioParameters = newParams;
            
            // Восстанавливаем состояние
            foreach (var p in audioParameters)
            {
                if (oldState.TryGetValue(p.name, out var state))
                {
                    p.enabled = state.enabled;
                    p.displayName = state.displayName;
                }
            }
        }
        
        private AudioMixer FindAudioMixer()
        {
            // Ищем MainAudioMixer или любой AudioMixer
            string[] guids = AssetDatabase.FindAssets("MainAudioMixer t:AudioMixer");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            }
            
            guids = AssetDatabase.FindAssets("t:AudioMixer");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            }
            
            return null;
        }

        private void GenerateAll()
        {
            SavePreferences();
            ApplyGeneratorSettings();
            
            if (generationMode == GenerationMode.Styled)
            {
                if (selectedConfig == null)
                {
                    EditorUtility.DisplayDialog("Ошибка", "Выберите конфигурацию стиля!", "OK");
                    return;
                }
                GenerateStyled();
            }
            else
            {
                GenerateStandard();
            }
        }
        
        private void GenerateStyled()
        {
            EditorUtility.DisplayProgressBar("UI Generator", "Генерация стилизованных UI...", 0f);

            try
            {
                if (generateSprites)
                {
                    EditorUtility.DisplayProgressBar("UI Generator", "Генерация спрайтов...", 0.3f);
                    UIIconGenerator.GenerateAllSprites(selectedConfig, outputPath);
                }

                if (generatePrefabs)
                {
                    EditorUtility.DisplayProgressBar("UI Generator", "Генерация префабов...", 0.7f);
                    UIWindowPrefabGenerator.OverwriteWithoutPrompt = overwriteWithoutPrompt;
                    UIWindowPrefabGenerator.GenerateWithSprites(selectedConfig, outputPath);
                }

                LoadPreviewSprites();
                
                EditorUtility.DisplayProgressBar("UI Generator", "Пересборка графа...", 0.95f);
                UIWindowGraphBuilder.RebuildGraph();
                
                ShowSuccessDialog("Стилизованные UI");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Ошибка", $"Ошибка генерации: {ex.Message}", "OK");
                Debug.LogError($"[UIGeneratorWindow] Error: {ex}");
            }
            finally
            {
                ClearGeneratorSettings();
                EditorUtility.ClearProgressBar();
            }
        }
        
        private void GenerateStandard()
        {
            EditorUtility.DisplayProgressBar("UI Generator", "Генерация стандартных UI...", 0f);

            try
            {
                EditorUtility.DisplayProgressBar("UI Generator", "Генерация префабов...", 0.5f);
                
                UIWindowPrefabGenerator.OverwriteWithoutPrompt = overwriteWithoutPrompt;
                UIWindowPrefabGenerator.RememberOutputPath(outputPath);
                UIWindowPrefabGenerator.GenerateAllBaseWindows();
                
                EditorUtility.DisplayProgressBar("UI Generator", "Пересборка графа...", 0.95f);
                UIWindowGraphBuilder.RebuildGraph();
                
                ShowSuccessDialog("Стандартные UI");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Ошибка", $"Ошибка генерации: {ex.Message}", "OK");
                Debug.LogError($"[UIGeneratorWindow] Error: {ex}");
            }
            finally
            {
                ClearGeneratorSettings();
                EditorUtility.ClearProgressBar();
            }
        }

        private void GenerateSprites()
        {
            if (selectedConfig == null)
            {
                EditorUtility.DisplayDialog("Ошибка", "Выберите конфигурацию стиля!", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("UI Generator", "Генерация спрайтов...", 0.5f);

            try
            {
                UIIconGenerator.GenerateAllSprites(selectedConfig, outputPath);
                LoadPreviewSprites();
                
                EditorUtility.DisplayDialog("Успех", $"Спрайты сгенерированы!\nПуть: {outputPath}", "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Ошибка", $"Ошибка генерации: {ex.Message}", "OK");
                Debug.LogError($"[UIGeneratorWindow] Error: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void GeneratePrefabs()
        {
            SavePreferences();
            ApplyGeneratorSettings();
            
            if (generationMode == GenerationMode.Styled)
            {
                if (selectedConfig == null)
                {
                    EditorUtility.DisplayDialog("Ошибка", "Выберите конфигурацию стиля!", "OK");
                    return;
                }
                
                EditorUtility.DisplayProgressBar("UI Generator", "Генерация стилизованных префабов...", 0.5f);

                try
                {
                    UIWindowPrefabGenerator.OverwriteWithoutPrompt = overwriteWithoutPrompt;
                    UIWindowPrefabGenerator.GenerateWithSprites(selectedConfig, outputPath);
                    
                    UIWindowGraphBuilder.RebuildGraph();
                    ShowSuccessDialog("Стилизованные префабы");
                }
                catch (System.Exception ex)
                {
                    EditorUtility.DisplayDialog("Ошибка", $"Ошибка генерации: {ex.Message}", "OK");
                    Debug.LogError($"[UIGeneratorWindow] Error: {ex}");
                }
                finally
                {
                    ClearGeneratorSettings();
                    EditorUtility.ClearProgressBar();
                }
            }
            else
            {
                GenerateStandard();
            }
        }
        
        private void ApplyGeneratorSettings()
        {
            // Звуковые настройки
            UIWindowPrefabGenerator.SoundIntegrationEnabled = soundIntegration;
            UIWindowPrefabGenerator.HoverSoundsEnabled = hoverSounds;
            
            // Аудио каналы для Settings
            UIWindowPrefabGenerator.AudioMixerForSettings = audioMixer;
            UIWindowPrefabGenerator.AudioParametersForSettings = audioParameters.FindAll(p => p.enabled);
        }
        
        private void ClearGeneratorSettings()
        {
            UIWindowPrefabGenerator.SoundIntegrationEnabled = false;
            UIWindowPrefabGenerator.HoverSoundsEnabled = false;
            UIWindowPrefabGenerator.AudioMixerForSettings = null;
            UIWindowPrefabGenerator.AudioParametersForSettings = null;
        }
        
        private void ShowSuccessDialog(string what)
        {
            string soundInfo = soundIntegration ? "\n✓ Звуковые компоненты добавлены" : "";
            
            int audioChannels = audioParameters?.FindAll(p => p.enabled).Count ?? 0;
            string audioInfo = audioChannels > 0 ? $"\n✓ Аудио каналов в Settings: {audioChannels}" : "";
            
            EditorUtility.DisplayDialog(
                "Генерация завершена",
                $"{what} сгенерированы!{soundInfo}{audioInfo}\n\nПуть: {outputPath}",
                "OK"
            );
        }

        private void LoadPreviewSprites()
        {
            previewCheckmark = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_CHECKBOX, "Checkmark");
            previewArrowDown = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_ICON, "ArrowDown");
            previewButton = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_BUTTON, "ButtonBackground");
            
            Repaint();
        }

        private UIStyleConfiguration FindOrCreateDefaultConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:UIStyleConfiguration");
            
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<UIStyleConfiguration>(path);
            }

            return UIStyleConfiguration.CreateDefault();
        }

        private void CreateNewConfiguration()
        {
            string projectName = EditorPrefs.GetString("ProtoSystem.Setup.MyGame.ProjectName", "");
            string defaultFolder = "Assets";

            if (!string.IsNullOrEmpty(projectName))
            {
                string rootFolder = EditorPrefs.GetString($"ProtoSystem.Setup.{projectName}.RootFolder", "");
                if (!string.IsNullOrEmpty(rootFolder))
                {
                    string configsFolder = $"{rootFolder}/Resources/UI/Configs";
                    if (AssetDatabase.IsValidFolder(configsFolder))
                    {
                        defaultFolder = configsFolder;
                    }
                    else if (AssetDatabase.IsValidFolder(rootFolder))
                    {
                        defaultFolder = rootFolder;
                    }
                }
            }

            if (defaultFolder == "Assets")
            {
                var guids = AssetDatabase.FindAssets("t:Folder Configs");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.Contains("/Resources/UI/Configs"))
                    {
                        defaultFolder = path;
                        break;
                    }
                }
            }

            string savePath = EditorUtility.SaveFilePanelInProject(
                "Создать UI Style Configuration",
                "UIStyleConfig",
                "asset",
                "Выберите место для сохранения конфигурации",
                defaultFolder
            );

            if (!string.IsNullOrEmpty(savePath))
            {
                var config = UIStyleConfiguration.CreateDefault();
                AssetDatabase.CreateAsset(config, savePath);
                AssetDatabase.SaveAssets();

                selectedConfig = config;
                EditorGUIUtility.PingObject(config);
            }
        }
    }
}
