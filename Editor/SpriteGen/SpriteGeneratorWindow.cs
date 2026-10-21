using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.SpriteGen.Editor
{
    public class SpriteGeneratorWindow : EditorWindow
    {
        private SpriteGenConfig _config;
        private GameObject _quickModel;
        private string _newConfigName = "NewSpriteGenConfig";
        private string _newConfigFolder = "Assets/SpriteGen/Configs";
        private Vector2 _scrollLeft;
        private Vector2 _scrollRight;
        private Texture2D _preview;
        private float _previewAngle;
        private bool _isGenerating;
        private float _progress;
        private string _progressMessage;

        private List<AnimationEntry> _pendingAnimations = new List<AnimationEntry>();

        private static readonly (string name, bool loop)[] AnimationPresets = {
            ("idle", true),
            ("walk", true),
            ("run", true),
            ("crawl", true),
            ("death", false),
            ("attack", false),
            ("other", true)
        };

        private bool _showCameraSettings = true;
        private bool _showAnimationSettings = true;
        private bool _showOutputSettings = true;

        [MenuItem("Window/ProtoSystem/Генератор спрайтов")]
        [MenuItem("ProtoSystem/ИИ студии/Генератор спрайтов")]
        public static void ShowWindow()
        {
            var window = GetWindow<SpriteGeneratorWindow>("Генератор спрайтов");
            window.minSize = new Vector2(700, 500);
        }

        /// <summary>Открыть окно с моделью (для кросс-ссылок из других студий).
        /// Если для модели уже есть SpriteGenConfig — подхватывает его.</summary>
        public static void Open(GameObject model)
        {
            ShowWindow();
            if (model == null) return;

            var window = GetWindow<SpriteGeneratorWindow>();
            window._quickModel = model;

            // Ищем существующий конфиг этой модели
            foreach (var guid in AssetDatabase.FindAssets("t:SpriteGenConfig"))
            {
                var cfg = AssetDatabase.LoadAssetAtPath<SpriteGenConfig>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (cfg != null && cfg.modelPrefab == model)
                {
                    window._config = cfg;
                    return;
                }
            }
            window._config = null;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            DrawLeftPanel();
            DrawRightPanel();

            EditorGUILayout.EndHorizontal();
        }

        // =====================================================================
        // LEFT PANEL
        // =====================================================================

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(380));
            _scrollLeft = EditorGUILayout.BeginScrollView(_scrollLeft);

            EditorGUILayout.LabelField("Sprite Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Модель
            _quickModel = (GameObject)EditorGUILayout.ObjectField(
                "3D Model", _quickModel, typeof(GameObject), false);

            EditorGUILayout.Space(3);

            // Конфиг
            _config = (SpriteGenConfig)EditorGUILayout.ObjectField(
                "Config", _config, typeof(SpriteGenConfig), false);

            // Синхронизация
            if (_config != null && _quickModel == null)
                _quickModel = _config.modelPrefab;

            // Нет конфига
            if (_config == null)
            {
                if (_quickModel != null)
                    DrawCreateConfigUI();
                else
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(
                        "Перетащите 3D модель в поле выше, чтобы начать.\nИли назначьте существующий Config.",
                        MessageType.Info);
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            // Синхронизация модели
            if (_quickModel != _config.modelPrefab)
            {
                Undo.RecordObject(_config, "Change model prefab");
                _config.modelPrefab = _quickModel;
                EditorUtility.SetDirty(_config);
            }

            EditorGUILayout.Space(5);

            var so = new SerializedObject(_config);
            so.Update();

            // Rig Type
            EditorGUILayout.PropertyField(so.FindProperty("rigType"), new GUIContent("Тип рига"));

            // Sprite Size
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Размер спрайта", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("spriteWidth"));
            EditorGUILayout.PropertyField(so.FindProperty("spriteHeight"));

            // Camera Angles
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Ракурсы камеры", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("anglePreset"));

            if (_config.anglePreset == CameraAnglePreset.Custom)
            {
                EditorGUILayout.PropertyField(so.FindProperty("customAngles"), true);
            }
            else
            {
                var angles = _config.GetAngles();
                EditorGUILayout.HelpBox($"{angles.Length} направлений", MessageType.None);
            }

            // Camera Settings
            _showCameraSettings = EditorGUILayout.Foldout(_showCameraSettings, "Камера", true);
            if (_showCameraSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(so.FindProperty("cameraDistance"));
                EditorGUILayout.PropertyField(so.FindProperty("cameraPitch"));
                EditorGUILayout.PropertyField(so.FindProperty("orthographic"));
                if (_config.orthographic)
                    EditorGUILayout.PropertyField(so.FindProperty("orthographicSize"));
                EditorGUI.indentLevel--;
            }

            // Animations
            EditorGUILayout.Space(3);
            _showAnimationSettings = EditorGUILayout.Foldout(_showAnimationSettings, "Анимации", true);
            if (_showAnimationSettings)
            {
                DrawAnimationPresetsUI(_config.animations, _config);
            }

            // Output Settings
            EditorGUILayout.Space(3);
            _showOutputSettings = EditorGUILayout.Foldout(_showOutputSettings, "Выходные настройки", true);
            if (_showOutputSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(so.FindProperty("generationMode"), new GUIContent("Режим генерации"));
                EditorGUILayout.PropertyField(so.FindProperty("backgroundColor"));
                EditorGUILayout.PropertyField(so.FindProperty("outputFolder"));

                if (_config.generationMode == GenerationMode.Individual)
                    EditorGUILayout.HelpBox("Каждая анимация генерируется по своим настройкам OutputMode.", MessageType.None);
                else
                    EditorGUILayout.HelpBox("Все отмеченные анимации будут собраны в один общий спрайтшит.", MessageType.None);

                EditorGUI.indentLevel--;
            }

            // Render Style
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Стиль рендера", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(so.FindProperty("stylePreset"), GUIContent.none);
            if (_config.stylePreset == null)
            {
                if (GUILayout.Button("Создать", GUILayout.Width(80)))
                    CreateAndAssignStylePreset();
            }
            EditorGUILayout.EndHorizontal();

            so.ApplyModifiedProperties();

            // Buttons
            EditorGUILayout.Space(10);
            DrawActionButtons();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtons()
        {
            bool hasModel = _config != null && _config.modelPrefab != null;

            // Preview Scene
            EditorGUI.BeginDisabledGroup(!hasModel);
            if (GUILayout.Button("Open Preview Scene", GUILayout.Height(25)))
                PreviewSceneGenerator.OpenPreviewScene(_config);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(3);

            // Update Preview
            EditorGUI.BeginDisabledGroup(!hasModel);
            if (GUILayout.Button("Update Preview", GUILayout.Height(25)))
                GeneratePreview();
            EditorGUI.EndDisabledGroup();

            // Generate
            EditorGUILayout.Space(3);
            EditorGUI.BeginDisabledGroup(!hasModel || _isGenerating);
            if (GUILayout.Button("Generate Sprites", GUILayout.Height(35)))
                GenerateAll();
            EditorGUI.EndDisabledGroup();

            if (_isGenerating)
            {
                EditorGUILayout.Space(5);
                var rect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(rect, _progress, _progressMessage ?? "Generating...");
            }
        }

        // =====================================================================
        // ANIMATION UI
        // =====================================================================

        private void DrawAnimationPresetsUI(List<AnimationEntry> entries, Object undoTarget)
        {
            EditorGUILayout.LabelField("Анимации", EditorStyles.boldLabel);

            // Кнопки-пресеты
            EditorGUILayout.BeginHorizontal();
            foreach (var (presetName, presetLoop) in AnimationPresets)
            {
                bool alreadyExists = entries.Exists(e => e.name == presetName);
                EditorGUI.BeginDisabledGroup(alreadyExists);
                if (GUILayout.Button(presetName, GUILayout.Height(22)))
                {
                    if (undoTarget != null) Undo.RecordObject(undoTarget, $"Add animation '{presetName}'");
                    entries.Add(new AnimationEntry(presetName, presetLoop));
                    if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // Список анимаций
            int removeIndex = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                DrawAnimationEntry(entries[i], undoTarget, ref removeIndex, i);
            }

            if (removeIndex >= 0)
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, "Remove animation");
                entries.RemoveAt(removeIndex);
                if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
            }

            if (GUILayout.Button("+ Добавить анимацию"))
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, "Add custom animation");
                entries.Add(new AnimationEntry("custom_" + entries.Count, true));
                if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
            }
        }

        private void DrawAnimationEntry(AnimationEntry entry, Object undoTarget, ref int removeIndex, int index)
        {
            // Затемнить если не выбрана для генерации
            if (!entry.generate)
                GUI.color = new Color(1f, 1f, 1f, 0.6f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUI.color = Color.white;

            // Строка 1: галочка, имя, клип, удалить
            EditorGUILayout.BeginHorizontal();

            bool newGenerate = EditorGUILayout.Toggle(entry.generate, GUILayout.Width(16));
            if (newGenerate != entry.generate)
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, "Toggle generate");
                entry.generate = newGenerate;
                if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
            }

            string newName = EditorGUILayout.TextField(entry.name, EditorStyles.boldLabel, GUILayout.Width(70));
            if (newName != entry.name)
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, "Rename animation");
                entry.name = newName;
                if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
            }

            var newClip = (AnimationClip)EditorGUILayout.ObjectField(
                entry.clip, typeof(AnimationClip), false);
            if (newClip != entry.clip)
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, "Change animation clip");
                entry.clip = newClip;
                if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
            }

            if (GUILayout.Button("X", GUILayout.Width(22), GUILayout.Height(18)))
                removeIndex = index;

            EditorGUILayout.EndHorizontal();

            // Строка 2: loop, output mode, pivot
            EditorGUILayout.BeginHorizontal();

            // Loop
            bool newLoop = EditorGUILayout.ToggleLeft("Loop", entry.loop, GUILayout.Width(50));
            if (newLoop != entry.loop)
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, "Toggle loop");
                entry.loop = newLoop;
                if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
            }

            // Output Mode
            var newOutputMode = (OutputMode)EditorGUILayout.EnumPopup(entry.outputMode, GUILayout.Width(100));
            if (newOutputMode != entry.outputMode)
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, "Change output mode");
                entry.outputMode = newOutputMode;
                if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
            }

            // Pivot
            EditorGUILayout.LabelField("Pivot", GUILayout.Width(30));
            var newPivot = (SpritePivot)EditorGUILayout.EnumPopup(entry.pivot, GUILayout.Width(85));
            if (newPivot != entry.pivot)
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, "Change pivot");
                entry.pivot = newPivot;
                if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
            }

            if (entry.pivot == SpritePivot.Custom)
            {
                var newCustomPivot = EditorGUILayout.Vector2Field("", entry.customPivot, GUILayout.Width(100));
                if (newCustomPivot != entry.customPivot)
                {
                    if (undoTarget != null) Undo.RecordObject(undoTarget, "Change custom pivot");
                    entry.customPivot = newCustomPivot;
                    if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
                }
            }

            EditorGUILayout.EndHorizontal();

            // Строка 3: режим кадров
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Кадры", GUILayout.Width(42));
            var newMode = (FrameSelectionMode)EditorGUILayout.EnumPopup(entry.frameMode, GUILayout.Width(70));
            if (newMode != entry.frameMode)
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, "Change frame mode");
                entry.frameMode = newMode;
                if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
            }

            if (entry.frameMode == FrameSelectionMode.Uniform)
            {
                int newFrames = EditorGUILayout.IntField(entry.frameCount, GUILayout.Width(35));
                if (newFrames != entry.frameCount)
                {
                    if (undoTarget != null) Undo.RecordObject(undoTarget, "Change frame count");
                    entry.frameCount = Mathf.Max(1, newFrames);
                    if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
                }
            }

            EditorGUILayout.EndHorizontal();

            // Custom frame times
            if (entry.frameMode == FrameSelectionMode.Custom)
            {
                DrawCustomFrameTimes(entry, undoTarget);
            }

            // Инфо о клипе
            if (entry.clip != null)
            {
                string loopLabel = entry.loop ? "loop" : "once";
                EditorGUILayout.LabelField(
                    $"  {entry.clip.length:F2}с  |  {entry.clip.frameRate} FPS  |  {loopLabel}",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCustomFrameTimes(AnimationEntry entry, Object undoTarget)
        {
            if (entry.customFrameTimes == null)
                entry.customFrameTimes = new float[] { 0f };

            float clipLen = entry.clip != null ? entry.clip.length : 1f;

            EditorGUILayout.BeginVertical();

            for (int j = 0; j < entry.customFrameTimes.Length; j++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  t{j}", GUILayout.Width(30));

                float newTime = EditorGUILayout.Slider(entry.customFrameTimes[j], 0f, clipLen);
                if (!Mathf.Approximately(newTime, entry.customFrameTimes[j]))
                {
                    if (undoTarget != null) Undo.RecordObject(undoTarget, "Change frame time");
                    entry.customFrameTimes[j] = newTime;
                    if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
                }

                if (GUILayout.Button("-", GUILayout.Width(20)) && entry.customFrameTimes.Length > 1)
                {
                    if (undoTarget != null) Undo.RecordObject(undoTarget, "Remove frame time");
                    var list = new List<float>(entry.customFrameTimes);
                    list.RemoveAt(j);
                    entry.customFrameTimes = list.ToArray();
                    if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("  + Добавить точку", GUILayout.Height(18)))
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, "Add frame time");
                var list = new List<float>(entry.customFrameTimes);
                list.Add(clipLen);
                entry.customFrameTimes = list.ToArray();
                if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
            }

            EditorGUILayout.EndVertical();
        }

        // =====================================================================
        // CREATE CONFIG UI
        // =====================================================================

        private void DrawCreateConfigUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Создать новый конфиг", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _newConfigName = EditorGUILayout.TextField("Название", _newConfigName);

            EditorGUILayout.BeginHorizontal();
            _newConfigFolder = EditorGUILayout.TextField("Папка", _newConfigFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string selected = EditorUtility.OpenFolderPanel("Выберите папку для конфига", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    string dataPath = Application.dataPath;
                    if (selected.StartsWith(dataPath))
                        _newConfigFolder = "Assets" + selected.Substring(dataPath.Length);
                    else
                        _newConfigFolder = selected;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // Анимации
            EditorGUILayout.Space(5);
            DrawAnimationPresetsUI(_pendingAnimations, null);

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Создать конфиг", GUILayout.Height(30)))
            {
                CreateNewConfig();
            }
        }

        private void CreateNewConfig()
        {
            string absoluteDir = Path.GetFullPath(_newConfigFolder);
            if (!Directory.Exists(absoluteDir))
                Directory.CreateDirectory(absoluteDir);

            string assetPath = Path.Combine(_newConfigFolder, _newConfigName + ".asset");

            if (AssetDatabase.LoadAssetAtPath<SpriteGenConfig>(assetPath) != null)
            {
                if (!EditorUtility.DisplayDialog("Конфиг уже существует",
                    $"Файл {assetPath} уже существует. Перезаписать?", "Да", "Нет"))
                    return;
            }

            var config = ScriptableObject.CreateInstance<SpriteGenConfig>();
            config.modelPrefab = _quickModel;
            config.outputFolder = "Assets/SpriteGen/Output";
            config.animations = new List<AnimationEntry>(_pendingAnimations);

            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _config = config;
            _pendingAnimations.Clear();
            EditorGUIUtility.PingObject(config);
            Debug.Log($"Конфиг создан: {assetPath}");
        }

        // =====================================================================
        // RIGHT PANEL
        // =====================================================================

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical();
            _scrollRight = EditorGUILayout.BeginScrollView(_scrollRight);

            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (_config != null)
            {
                _previewAngle = EditorGUILayout.Slider("Угол превью", _previewAngle, 0f, 360f);
            }

            if (_preview != null)
            {
                EditorGUILayout.Space(5);

                float maxSize = Mathf.Min(position.width - 400, position.height - 80);
                maxSize = Mathf.Max(maxSize, 128);

                float aspect = (float)_preview.width / _preview.height;
                float displayW, displayH;
                if (aspect >= 1f)
                {
                    displayW = maxSize;
                    displayH = maxSize / aspect;
                }
                else
                {
                    displayH = maxSize;
                    displayW = maxSize * aspect;
                }

                var rect = GUILayoutUtility.GetRect(displayW, displayH,
                    GUILayout.MaxWidth(displayW), GUILayout.MaxHeight(displayH));
                EditorGUI.DrawTextureTransparent(rect, _preview, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUILayout.HelpBox("Нажмите 'Update Preview' для рендера.", MessageType.None);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void CreateAndAssignStylePreset()
        {
            string folder = "Assets/SpriteGen/Output";
            if (_config != null && !string.IsNullOrEmpty(_config.outputFolder))
                folder = _config.outputFolder;

            if (!System.IO.Directory.Exists(folder))
                System.IO.Directory.CreateDirectory(folder);

            string baseName = (_config != null && _config.modelPrefab != null)
                ? _config.modelPrefab.name + "_StylePreset"
                : "RenderStylePreset";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.asset");

            var preset = ScriptableObject.CreateInstance<RenderStylePreset>();
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _config.stylePreset = preset;
            EditorUtility.SetDirty(_config);

            EditorGUIUtility.PingObject(preset);
            Selection.activeObject = preset;
        }

        // =====================================================================
        // GENERATION
        // =====================================================================

        private void GeneratePreview()
        {
            if (_config == null || _config.modelPrefab == null) return;

            if (_preview != null)
                DestroyImmediate(_preview);

            try
            {
                _preview = ModelRenderer.RenderModel(_config, _previewAngle);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Preview failed: {e.Message}");
            }

            Repaint();
        }

        private void GenerateAll()
        {
            if (_config == null || _config.modelPrefab == null) return;

            _isGenerating = true;
            _progress = 0f;
            _progressMessage = "Подготовка...";

            try
            {
                string basePath = _config.outputFolder;
                if (!basePath.StartsWith("Assets/"))
                    basePath = "Assets/" + basePath;

                string modelName = _config.modelPrefab.name;
                string outputDir = Path.Combine(basePath, modelName);

                var absoluteDir = Path.GetFullPath(outputDir);
                if (!Directory.Exists(absoluteDir))
                    Directory.CreateDirectory(absoluteDir);

                // Отдельные PNG (Single sprite) и спрайтшиты (Multiple) — раздельные списки
                var singleSpritePaths = new List<string>();
                var sheetCount = 0;
                var angles = _config.GetAngles();
                bool hasAnimations = _config.animations != null &&
                                     _config.animations.Exists(e => e != null && e.generate && e.clip != null);

                if (!hasAnimations)
                {
                    GenerateStaticSprites(angles, outputDir, modelName, singleSpritePaths);
                }
                else if (_config.generationMode == GenerationMode.CombinedSheet)
                {
                    sheetCount += GenerateCombinedSpritesheet(angles, outputDir, modelName);
                }
                else
                {
                    sheetCount += GenerateAnimatedSprites(angles, outputDir, modelName, singleSpritePaths);
                }

                AssetDatabase.Refresh();

                // Настроить только отдельные PNG как Single sprite
                bool isPixelArt = _config.stylePreset != null &&
                                  _config.stylePreset.style == RenderStyle.PixelArt;

                if (singleSpritePaths.Count > 0)
                    SpriteImportConfigurator.ConfigureBatch(singleSpritePaths.ToArray(), isPixelArt);

                _progress = 1f;
                _progressMessage = "Готово!";

                int totalFiles = singleSpritePaths.Count + sheetCount;
                Debug.Log($"Генерация завершена! {totalFiles} файл(ов) в {outputDir}");
                EditorUtility.RevealInFinder(absoluteDir);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Ошибка генерации: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                _isGenerating = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private void GenerateStaticSprites(
            float[] angles, string outputDir, string modelName,
            List<string> allSpritePaths)
        {
            var textures = new List<Texture2D>();
            var names = new List<string>();

            for (int i = 0; i < angles.Length; i++)
            {
                _progress = (float)i / angles.Length * 0.8f;
                _progressMessage = $"Рендер угла {angles[i]:F0}°...";

                var tex = ModelRenderer.RenderModel(_config, angles[i]);
                textures.Add(tex);

                string spriteName = $"{modelName}_angle{angles[i]:F0}";
                names.Add(spriteName);

                // Статичные спрайты — всегда сохраняем отдельные PNG
                string path = Path.Combine(outputDir, spriteName + ".png");
                ModelRenderer.SaveTextureAsPNG(tex, path);
                allSpritePaths.Add(path);
            }

            // И спрайтшит тоже
            {
                _progressMessage = "Упаковка спрайтшита...";
                _progress = 0.9f;

                var result = SpritesheetPacker.PackGrid(
                    textures.ToArray(), angles.Length,
                    _config.spriteWidth, _config.spriteHeight,
                    names.ToArray());

                string sheetPath = Path.Combine(outputDir, $"{modelName}_sheet.png");
                SpritesheetPacker.SaveSpritesheet(result, sheetPath);

                AssetDatabase.Refresh();

                bool isPixelArt = _config.stylePreset != null &&
                                  _config.stylePreset.style == RenderStyle.PixelArt;
                SpriteImportConfigurator.ConfigureAsSpritesheet(
                    sheetPath, result.spriteRects,
                    _config.spriteWidth, _config.spriteHeight, isPixelArt, SheetPivot(null));

                DestroyImmediate(result.atlas);
            }

            foreach (var tex in textures)
                DestroyImmediate(tex);
        }

        /// <summary>
        /// Пивот кадров листа. Вид сверху (TopDown) в игре крутят transform'ом, и модель стоит в центре кадра —
        /// пивот по центру: с «ногами» (низ кадра) туша при повороте описывала дугу, а коллайдер и прицел турелей
        /// уезжали на край спрайта. Остальные виды — пивот анимации (по умолчанию низ: ноги стоят на земле)
        /// </summary>
        private Vector2 SheetPivot(AnimationEntry entry)
        {
            if (_config.anglePreset == CameraAnglePreset.TopDown) return new Vector2(0.5f, 0.5f);
            return entry != null ? entry.GetPivotValue() : new Vector2(0.5f, 0f);
        }

        /// <returns>Количество созданных спрайтшитов</returns>
        private int GenerateAnimatedSprites(
            float[] angles, string outputDir, string modelName,
            List<string> singleSpritePaths)
        {
            var entriesToGenerate = _config.animations.FindAll(e => e != null && e.generate && e.clip != null);
            int totalAnims = entriesToGenerate.Count;
            int sheetCount = 0;

            if (totalAnims == 0)
            {
                Debug.LogWarning("Нет анимаций отмеченных для генерации.");
                return 0;
            }

            for (int a = 0; a < totalAnims; a++)
            {
                var entry = entriesToGenerate[a];
                string entryName = entry.name;
                _progressMessage = $"Захват '{entryName}'...";

                var frames = AnimationSampler.SampleAllAngles(_config, entry, p =>
                {
                    _progress = ((float)a / totalAnims + p / totalAnims) * 0.8f;
                });

                int actualFrameCount = entry.frameMode == FrameSelectionMode.Custom
                    ? entry.customFrameTimes.Length
                    : entry.frameCount;

                // Отдельные PNG
                if (entry.outputMode != OutputMode.Spritesheet)
                {
                    string animDir = Path.Combine(outputDir, entryName);
                    var absoluteAnimDir = Path.GetFullPath(animDir);
                    if (!Directory.Exists(absoluteAnimDir))
                        Directory.CreateDirectory(absoluteAnimDir);

                    foreach (var frame in frames)
                    {
                        string frameName = $"{modelName}_{entryName}_angle{frame.angle:F0}_f{frame.frameIndex:D2}";
                        string path = Path.Combine(animDir, frameName + ".png");
                        ModelRenderer.SaveTextureAsPNG(frame.texture, path);
                        singleSpritePaths.Add(path);
                    }
                }

                // Спрайтшит (настраивается отдельно, НЕ в batch)
                if (entry.outputMode != OutputMode.IndividualSprites)
                {
                    _progressMessage = $"Упаковка '{entryName}'...";

                    // Имена спрайтов в шите: model_anim_angle_frame
                    var sheetNames = new string[frames.Count];
                    for (int i = 0; i < frames.Count; i++)
                    {
                        var f = frames[i];
                        sheetNames[i] = $"{modelName}_{entryName}_angle{f.angle:F0}_f{f.frameIndex:D2}";
                    }

                    var result = SpritesheetPacker.PackAnimationGrid(
                        frames, angles, actualFrameCount,
                        _config.spriteWidth, _config.spriteHeight,
                        sheetNames);

                    string sheetPath = Path.Combine(outputDir, $"{modelName}_{entryName}_sheet.png");
                    SpritesheetPacker.SaveSpritesheet(result, sheetPath);

                    AssetDatabase.Refresh();

                    bool isPixelArt = _config.stylePreset != null &&
                                      _config.stylePreset.style == RenderStyle.PixelArt;
                    SpriteImportConfigurator.ConfigureAsSpritesheet(
                        sheetPath, result.spriteRects,
                        _config.spriteWidth, _config.spriteHeight, isPixelArt,
                        SheetPivot(entry));

                    DestroyImmediate(result.atlas);
                    sheetCount++;
                }

                foreach (var frame in frames)
                    DestroyImmediate(frame.texture);
            }

            return sheetCount;
        }

        /// <returns>Количество созданных спрайтшитов (всегда 1)</returns>
        private int GenerateCombinedSpritesheet(
            float[] angles, string outputDir, string modelName)
        {
            var entriesToGenerate = _config.animations.FindAll(e => e != null && e.generate && e.clip != null);
            int totalAnims = entriesToGenerate.Count;

            if (totalAnims == 0)
            {
                Debug.LogWarning("Нет анимаций отмеченных для генерации.");
                return 0;
            }

            int w = _config.spriteWidth;
            int h = _config.spriteHeight;

            // Собираем ВСЕ реальные кадры в плоский список (без пустых ячеек)
            var allTextures = new List<Texture2D>();
            var allNames = new List<string>();

            for (int a = 0; a < totalAnims; a++)
            {
                var entry = entriesToGenerate[a];
                _progressMessage = $"Захват '{entry.name}'...";

                var frames = AnimationSampler.SampleAllAngles(_config, entry, p =>
                {
                    _progress = ((float)a / totalAnims + p / totalAnims) * 0.7f;
                });

                foreach (var frame in frames)
                {
                    allTextures.Add(frame.texture);
                    allNames.Add($"{modelName}_{entry.name}_angle{frame.angle:F0}_f{frame.frameIndex:D2}");
                }
            }

            _progressMessage = "Упаковка общего спрайтшита...";
            _progress = 0.8f;

            // Упаковка в минимальный power-of-two атлас
            var result = SpritesheetPacker.PackGridPowerOfTwo(
                allTextures.ToArray(), w, h, allNames.ToArray());

            int atlasW = result.atlas.width;
            int atlasH = result.atlas.height;

            string sheetPath = Path.Combine(outputDir, $"{modelName}_combined_sheet.png");
            SpritesheetPacker.SaveSpritesheet(result, sheetPath);

            AssetDatabase.Refresh();

            bool isPixelArt = _config.stylePreset != null &&
                              _config.stylePreset.style == RenderStyle.PixelArt;
            SpriteImportConfigurator.ConfigureAsSpritesheet(
                sheetPath, result.spriteRects, w, h, isPixelArt, SheetPivot(null));

            _progress = 0.95f;

            int spriteCount = allTextures.Count;
            int cells = (atlasW / w) * (atlasH / h);
            float fill = spriteCount * 100f / Mathf.Max(1, cells);
            Debug.Log($"Общий спрайтшит: {atlasW}x{atlasH} ({spriteCount} спрайтов, заполнение {fill:F0}%)");

            DestroyImmediate(result.atlas);

            foreach (var tex in allTextures)
                DestroyImmediate(tex);

            return 1;
        }

        private void OnDestroy()
        {
            if (_preview != null)
                DestroyImmediate(_preview);
        }
    }
}
