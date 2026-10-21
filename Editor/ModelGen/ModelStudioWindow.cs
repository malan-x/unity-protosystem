using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoSystem.IconGen;
using ProtoSystem.IconGen.Editor;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.ModelGen.Editor
{
    /// <summary>
    /// ИИ 3D-студия: генерация 3D-моделей (Hunyuan3D локально / Meshy облако) по
    /// референсам арт-студии, варианты с рецептами, публикация GLB и рендер
    /// спрайт-листов (мост в 2D-пайплайн проекта — через ModelStudioExtensions).
    /// </summary>
    public class ModelStudioWindow : EditorWindow
    {
        private List<ModelContentSet> _sets = new();
        private ModelContentItem _selected;
        private string _selectedVariantId;

        private ModelEngine _engine = ModelEngine.Hunyuan3D;
        private int _seed = 42;
        private bool _randomSeed = true;
        private int _steps = 25;
        private float _guidance = 5f;
        private int _octree = 256;
        private int _maxFaces = 30000;
        private int _texSize = 1024;

        private int _sheetDirections = 8;
        private int _sheetCell = 256;
        private float _sheetElevation = 55f;

        private Vector2 _listScroll, _detailScroll;
        private bool _paramsFoldout;
        private bool? _serverOnline;
        private double _nextServerPing;
        private int? _meshyCredits;

        private GUIStyle _setHeader, _entityBtn, _entityBtnActive;
        private bool _stylesReady;

        // Интерактивный 3D-превью выбранного варианта
        private PreviewRenderUtility _preview;
        private GameObject _previewInstance;
        private string _previewPath;
        private Bounds _previewBounds;
        private Vector2 _orbit = new(20f, 55f);   // yaw, pitch
        private float _zoom = 1f;

        // Статистика загруженной в превью модели
        private int _statVerts, _statTris, _statMeshes;
        private long _statFileBytes;
        private readonly List<string> _statMaterials = new();
        private readonly List<string> _statTextures = new();
        private bool _statsFoldout = true;

        [MenuItem("ProtoSystem/ИИ студии/ИИ 3D студия")]
        public static void Open()
        {
            var w = GetWindow<ModelStudioWindow>("ИИ 3D студия");
            w.minSize = new Vector2(880, 520);
        }

        /// <summary>Открыть студию на конкретной сущности (переход из «Арт мобов» и др.).</summary>
        public static void OpenAt(string entityId)
        {
            Open();
            var w = GetWindow<ModelStudioWindow>();
            w.RefreshProviders();
            w._selected = w._sets.SelectMany(s => s.Items).FirstOrDefault(i => i.EntityId == entityId);
            w._selectedVariantId = ModelVariantStore.Library.Get(entityId)?.activeId;
            w.Repaint();
        }

        private void OnEnable()
        {
            RefreshProviders();
            AiModelGenerator.Changed += Repaint;
            ModelVariantStore.Changed += Repaint;
        }

        private void OnDisable()
        {
            AiModelGenerator.Changed -= Repaint;
            ModelVariantStore.Changed -= Repaint;
            CleanupPreview();
        }

        private void CleanupPreview()
        {
            if (_previewInstance != null) DestroyImmediate(_previewInstance);
            _previewInstance = null;
            _previewPath = null;
            _preview?.Cleanup();
            _preview = null;
        }

        private void RefreshProviders()
        {
            _sets.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                if (AssetDatabase.LoadMainAssetAtPath(path) is IModelContentProvider provider)
                {
                    try { _sets.AddRange(provider.GetModelSets()); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
            foreach (var set in _sets)
                foreach (var item in set.Items)
                    ModelVariantStore.EnsureBaseVariant(item);
            if (_selected != null)
                _selected = _sets.SelectMany(s => s.Items).FirstOrDefault(i => i.EntityId == _selected.EntityId);
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _setHeader = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            _entityBtn = new GUIStyle(EditorStyles.label) { padding = new RectOffset(10, 4, 3, 3) };
            _entityBtnActive = new GUIStyle(_entityBtn) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, .8f, .4f) } };
            _stylesReady = true;
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawEntityList();
            DrawDetails();
            EditorGUILayout.EndHorizontal();
            DrawFooter();
        }

        // ────────────────────────── UI: тулбар ──────────────────────────

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            // Только генерирующие движки: Imported — отметка происхождения варианта,
            // выбирать его как «чем генерировать» нечего
            _engine = (ModelEngine)EditorGUILayout.IntPopup((int)_engine,
                new[] { "Hunyuan3D", "Meshy" },
                new[] { (int)ModelEngine.Hunyuan3D, (int)ModelEngine.Meshy },
                EditorStyles.toolbarPopup, GUILayout.Width(110));

            if (_engine == ModelEngine.Hunyuan3D)
            {
                if (EditorApplication.timeSinceStartup > _nextServerPing)
                {
                    _nextServerPing = EditorApplication.timeSinceStartup + 10;
                    PingServer();
                }
                GUILayout.Label(_serverOnline switch
                {
                    true => "Comfy ● онлайн",
                    false => "Comfy ○ офлайн",
                    _ => "Comfy …",
                }, EditorStyles.toolbarButton, GUILayout.Width(110));
                if (_serverOnline == false && GUILayout.Button("Запустить", EditorStyles.toolbarButton))
                    LaunchComfy();
            }
            else
            {
                GUILayout.Label(_meshyCredits.HasValue ? $"Кредиты: {_meshyCredits}" : "Кредиты: ?",
                    EditorStyles.toolbarButton, GUILayout.Width(110));
                if (GUILayout.Button("⟳", EditorStyles.toolbarButton, GUILayout.Width(26)))
                    RefreshCredits();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Обновить", EditorStyles.toolbarButton)) RefreshProviders();
            if (GUILayout.Button("Настройки", EditorStyles.toolbarButton))
                SettingsService.OpenProjectSettings("Project/AI Model Tools");
            EditorGUILayout.EndHorizontal();
        }

        private async void PingServer()
        {
            _serverOnline = await Hy3DComfyClient.IsServerOnline(ModelAiSettings.ComfyServer);
            Repaint();
        }

        private async void RefreshCredits()
        {
            _meshyCredits = await MeshyClient.GetCreditsAsync(ModelAiSettings.MeshyApiKey);
            Repaint();
        }

        private void LaunchComfy()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ModelAiSettings.ComfyLaunch,
                    UseShellExecute = true,
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception e) { Debug.LogError("[ModelStudio] Запуск ComfyUI: " + e.Message); }
        }

        // ────────────────────────── UI: список ──────────────────────────

        private void DrawEntityList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(240));
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            foreach (var set in _sets)
            {
                GUILayout.Label(set.Title, _setHeader);
                foreach (var item in set.Items)
                {
                    var hist = ModelVariantStore.Library.Get(item.EntityId);
                    int count = hist?.variants.Count ?? 0;
                    bool published = !string.IsNullOrEmpty(item.GetPublishedModelPath?.Invoke());
                    string label = $"{(published ? "●" : "○")} {item.DisplayName}{(count > 0 ? $"  [{count}]" : "")}";
                    var style = _selected == item ? _entityBtnActive : _entityBtn;
                    if (GUILayout.Button(label, style))
                    {
                        _selected = item;
                        _selectedVariantId = hist?.activeId;
                        GUI.FocusControl(null);
                    }
                }
                GUILayout.Space(6);
            }
            if (_sets.Count == 0)
                EditorGUILayout.HelpBox("Нет провайдеров моделей. Нужен ScriptableObject проекта с интерфейсом " +
                                        "IModelContentProvider (ProtoSystem.ModelGen) — он отдаёт сущности студии.",
                                        MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            var line = GUILayoutUtility.GetRect(1, 1, GUILayout.Width(1), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(line, new Color(0, 0, 0, 0.35f));
        }

        // ────────────────────────── UI: детали ──────────────────────────

        private void DrawDetails()
        {
            EditorGUILayout.BeginVertical();
            if (_selected == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Выбери сущность слева", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            GUILayout.Label(_selected.DisplayName, new GUIStyle(EditorStyles.largeLabel) { fontStyle = FontStyle.Bold });
            if (!string.IsNullOrEmpty(_selected.UsageNote))
                EditorGUILayout.HelpBox(_selected.UsageNote, MessageType.None);

            // Промпт
            GUILayout.Label("Описание (промпт)", EditorStyles.boldLabel);
            string prompt = _selected.GetPrompt?.Invoke() ?? "";
            string newPrompt = EditorGUILayout.TextArea(prompt, GUILayout.MinHeight(44));
            if (newPrompt != prompt) _selected.SetPrompt?.Invoke(newPrompt);

            // Проектные надстройки под промптом (напр., «промпт из Арт мобов»)
            foreach (var extra in ModelStudioExtensions.EntityDetailsGUI)
            {
                try { extra(_selected); }
                catch (Exception e) { Debug.LogException(e); }
            }

            // Референс
            GUILayout.Space(4);
            GUILayout.Label("Референс (image-to-3D — концепт спереди/¾)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            string refPath = _selected.GetReferenceImagePath?.Invoke();
            var refTex = string.IsNullOrEmpty(refPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(refPath);
            var newTex = (Texture2D)EditorGUILayout.ObjectField(refTex, typeof(Texture2D), false, GUILayout.Width(200));
            if (newTex != refTex)
                _selected.SetReferenceImagePath?.Invoke(newTex != null ? AssetDatabase.GetAssetPath(newTex) : null);
            if (refTex != null)
            {
                var r = GUILayoutUtility.GetRect(96, 96, GUILayout.Width(96));
                EditorGUI.DrawTextureTransparent(r, refTex, ScaleMode.ScaleToFit);
            }
            EditorGUILayout.EndHorizontal();

            // Откуда взять референс. Штатный путь пайплайна: концепт рисуется в
            // ИИ Арт студии, оттуда же и берётся — «сначала картинка, потом объём»
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Вставить из буфера", GUILayout.Width(150)))
                PasteReferenceFromClipboard(_selected);
            if (GUILayout.Button("Взять из Арт студии…", GUILayout.Width(170)))
                ShowArtIconPicker(_selected);
            if (GUILayout.Button("Открыть Арт студию", GUILayout.Width(160)))
                IconStudioWindow.Open();
            EditorGUILayout.EndHorizontal();

            // Параметры
            _paramsFoldout = EditorGUILayout.Foldout(_paramsFoldout, "Параметры генерации", true);
            if (_paramsFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(_randomSeed))
                    _seed = EditorGUILayout.IntField("Сид", _seed);
                _randomSeed = GUILayout.Toggle(_randomSeed, "случайный", GUILayout.Width(90));
                EditorGUILayout.EndHorizontal();
                _steps    = EditorGUILayout.IntSlider("Шаги меша", _steps, 10, 50);
                _guidance = EditorGUILayout.Slider("Guidance", _guidance, 1f, 15f);
                _octree   = EditorGUILayout.IntPopup("Октодерево", _octree,
                    new[] { "192 (черновик)", "256", "384 (детально)" }, new[] { 192, 256, 384 });
                _maxFaces = EditorGUILayout.IntField("Макс. фейсов (общее)", _maxFaces);

                // Плотность сетки — свойство КОНКРЕТНОЙ модели, а не общей настройки:
                // метке глобуса хватает 2500, мобу нужна детализация. 0 — наследовать
                // общее значение выше, чтобы правка одной модели не задевала остальных.
                if (_selected.GetMaxFaces != null && _selected.SetMaxFaces != null)
                {
                    int own = _selected.GetMaxFaces();
                    int newOwn = EditorGUILayout.IntField(
                        new GUIContent("↳ у этой модели", "0 — брать общее значение выше"), own);
                    if (newOwn != own) _selected.SetMaxFaces(Mathf.Max(0, newOwn));
                    if (own <= 0)
                        EditorGUILayout.LabelField(" ", $"наследует {_maxFaces}", EditorStyles.miniLabel);
                }

                _texSize  = EditorGUILayout.IntPopup("Текстура", _texSize,
                    new[] { "512", "1024", "2048" }, new[] { 512, 1024, 2048 });
                EditorGUI.indentLevel--;
            }

            // Генерация
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            string genLabel = _engine == ModelEngine.Hunyuan3D ? "Сгенерировать (локально)" : "Сгенерировать (Meshy)";
            if (GUILayout.Button(genLabel, GUILayout.Height(28)))
                EnqueueGeneration(_selected);
            if (AiModelGenerator.Pending > 0 && GUILayout.Button("Отменить всё", GUILayout.Height(28), GUILayout.Width(110)))
                AiModelGenerator.CancelAll();
            EditorGUILayout.EndHorizontal();

            // Готовая модель со стороны: Meshy в браузере, чужой ассет, ручная сборка.
            // Кладём её вариантом — дальше те же публикация, превью и префаб
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Вставить модель из буфера",
                    "Скопируйте файл модели (.glb/.fbx/.obj) в проводнике — Ctrl+C — и нажмите здесь"),
                    GUILayout.Width(210)))
                PasteModelFromClipboard(_selected);
            if (GUILayout.Button("Взять файл с диска…", GUILayout.Width(170)))
            {
                string picked = EditorUtility.OpenFilePanel("Модель для " + _selected.DisplayName,
                    "", "glb,gltf,fbx,obj");
                if (!string.IsNullOrEmpty(picked)) ImportExternalModel(_selected, picked);
            }
            EditorGUILayout.EndHorizontal();

            DrawPreview3D();
            DrawVariants();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ────────────────────────── UI: 3D-превью ──────────────────────────

        private ModelVariant SelectedOrActiveVariant()
        {
            var hist = ModelVariantStore.Library.Get(_selected.EntityId);
            if (hist == null || hist.variants.Count == 0) return null;
            return hist.Find(_selectedVariantId) ?? hist.Active ?? hist.variants[^1];
        }

        private void EnsurePreviewFor(string assetPath)
        {
            if (_previewPath == assetPath && _preview != null && _previewInstance != null) return;

            if (_preview == null)
            {
                _preview = new PreviewRenderUtility();
                _preview.camera.clearFlags = CameraClearFlags.SolidColor;
                _preview.camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
                _preview.camera.fieldOfView = 30f;
                _preview.camera.nearClipPlane = 0.01f;
                _preview.camera.farClipPlane = 200f;
                _preview.lights[0].intensity = 1.3f;
                _preview.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                if (_preview.lights.Length > 1) _preview.lights[1].intensity = 0.6f;
                _preview.ambientColor = new Color(0.35f, 0.35f, 0.38f);
            }

            if (_previewInstance != null) DestroyImmediate(_previewInstance);
            _previewInstance = null;
            _previewPath = assetPath;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null) return;
            _previewInstance = _preview.InstantiatePrefabInScene(prefab);
            var renderers = _previewInstance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                _previewBounds = renderers[0].bounds;
                foreach (var r in renderers) _previewBounds.Encapsulate(r.bounds);
            }
            else _previewBounds = new Bounds(Vector3.zero, Vector3.one);

            CollectStats(assetPath);
        }

        private void CollectStats(string assetPath)
        {
            _statVerts = _statTris = _statMeshes = 0;
            _statMaterials.Clear();
            _statTextures.Clear();
            try { _statFileBytes = new FileInfo(Path.GetFullPath(assetPath)).Length; }
            catch { _statFileBytes = 0; }
            if (_previewInstance == null) return;

            var meshes = new HashSet<Mesh>();
            foreach (var mf in _previewInstance.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) meshes.Add(mf.sharedMesh);
            foreach (var smr in _previewInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) meshes.Add(smr.sharedMesh);
            foreach (var m in meshes)
            {
                _statMeshes++;
                _statVerts += m.vertexCount;
                for (int s = 0; s < m.subMeshCount; s++)
                    _statTris += (int)(m.GetIndexCount(s) / 3);
            }

            var mats = new HashSet<Material>();
            var texes = new HashSet<Texture>();
            foreach (var r in _previewInstance.GetComponentsInChildren<Renderer>(true))
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null || !mats.Add(mat)) continue;
                    _statMaterials.Add(mat.name + " — " + (mat.shader != null ? mat.shader.name : "?"));
                    foreach (var prop in mat.GetTexturePropertyNames())
                    {
                        var tex = mat.GetTexture(prop);
                        if (tex == null || !texes.Add(tex)) continue;
                        _statTextures.Add($"{prop.TrimStart('_')}: {tex.width}×{tex.height}");
                    }
                }
        }

        private void DrawPreview3D()
        {
            var variant = SelectedOrActiveVariant();
            if (variant == null) return;

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Превью (ЛКМ — вертеть, колесо — зум)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(variant.id == "original" ? "базовый" : variant.id, EditorStyles.miniLabel);
            if (GUILayout.Button("Сброс", EditorStyles.miniButton, GUILayout.Width(52)))
            { _orbit = new Vector2(20f, 55f); _zoom = 1f; }
            EditorGUILayout.EndHorizontal();

            var rect = GUILayoutUtility.GetRect(200, 280, GUILayout.ExpandWidth(true));
            var e = Event.current;

            // Управление
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag && e.button == 0)
                {
                    _orbit.x += e.delta.x * 0.7f;
                    _orbit.y = Mathf.Clamp(_orbit.y + e.delta.y * 0.7f, -89f, 89f);
                    e.Use(); Repaint();
                }
                else if (e.type == EventType.ScrollWheel)
                {
                    _zoom = Mathf.Clamp(_zoom * (1f - e.delta.y * 0.04f), 0.3f, 5f);
                    e.Use(); Repaint();
                }
            }

            EnsurePreviewFor(variant.assetPath);

            if (e.type == EventType.Repaint)
            {
                if (_preview == null || _previewInstance == null)
                {
                    EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.14f));
                    GUI.Label(rect, "модель не загрузилась", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    float radius = Mathf.Max(_previewBounds.extents.magnitude, 0.01f);
                    var rot = Quaternion.Euler(_orbit.y, _orbit.x, 0f);
                    _preview.camera.transform.position = _previewBounds.center + rot * Vector3.back * (radius * 3.2f / _zoom);
                    _preview.camera.transform.rotation = rot;

                    _preview.BeginPreview(rect, GUIStyle.none);
                    _preview.Render(true);
                    var tex = _preview.EndPreview();
                    GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
                }
            }

            DrawModelStats();
        }

        private void DrawModelStats()
        {
            string size = _statFileBytes > 0
                ? (_statFileBytes >= 1024 * 1024
                    ? (_statFileBytes / (1024f * 1024f)).ToString("0.0") + " МБ"
                    : (_statFileBytes / 1024f).ToString("0") + " КБ")
                : "?";
            var b = _previewBounds.size;
            _statsFoldout = EditorGUILayout.Foldout(_statsFoldout,
                $"Вершины: {_statVerts:N0} · Треугольники: {_statTris:N0} · Мешей: {_statMeshes} · " +
                $"Материалов: {_statMaterials.Count} · GLB: {size}", true);
            if (!_statsFoldout) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Габариты",
                $"{b.x:0.##} × {b.y:0.##} × {b.z:0.##}", EditorStyles.miniLabel);
            foreach (var m in _statMaterials)
                EditorGUILayout.LabelField("Материал", m, EditorStyles.miniLabel);
            foreach (var t in _statTextures)
                EditorGUILayout.LabelField("Текстура", t, EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        // ────────────────────────── UI: варианты ──────────────────────────

        private void DrawVariants()
        {
            var hist = ModelVariantStore.Library.Get(_selected.EntityId);
            GUILayout.Space(8);
            GUILayout.Label("Варианты", EditorStyles.boldLabel);
            if (hist == null || hist.variants.Count == 0)
            {
                EditorGUILayout.HelpBox("Вариантов пока нет — сгенерируй первый.", MessageType.None);
                return;
            }

            string published = _selected.GetPublishedModelPath?.Invoke();
            const int cell = 104;
            int perRow = Mathf.Max(1, (int)((position.width - 270) / (cell + 8)));
            int col = 0;
            EditorGUILayout.BeginHorizontal();
            foreach (var v in hist.variants.ToList())
            {
                if (col == perRow) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); col = 0; }
                DrawVariantCell(hist, v, published, cell);
                col++;
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(published))
                EditorGUILayout.LabelField("Опубликовано:", published, EditorStyles.miniLabel);

            // ── Финальный шаг: префаб в проект и ссылка в игровой конфиг ──
            var targetAsset = _selected.GetTargetAsset?.Invoke();
            string targetField = _selected.GetTargetField?.Invoke();
            bool hasSlot = targetAsset != null && !string.IsNullOrEmpty(targetField);

            EditorGUILayout.BeginHorizontal();
            // Кнопка ждала предварительной публикации — лишний шаг: если вариант
            // выбран, публикуем его сами и сразу делаем префаб
            bool canBuild = !string.IsNullOrEmpty(published) || SelectedVariant(hist) != null;
            using (new EditorGUI.DisabledScope(!canBuild))
            {
                if (GUILayout.Button(hasSlot ? "Создать префаб → в конфиг" : "Создать префаб",
                        GUILayout.Width(210)))
                    CreatePrefabAndAssign(_selected, hist);
            }
            // Мостик в генератор спрайтов: опубликованная модель → анимированные спрайт-листы
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(published)))
            {
                if (GUILayout.Button("Спрайты…", GUILayout.Width(90)))
                    ProtoSystem.SpriteGen.Editor.SpriteGeneratorWindow.Open(
                        AssetDatabase.LoadAssetAtPath<GameObject>(published));
            }
            if (hasSlot)
                EditorGUILayout.LabelField($"Слот: {targetAsset.name}.{targetField}", EditorStyles.miniLabel);
            else
                EditorGUILayout.LabelField("Слот не задан (ModelContent: targetAsset + targetField)",
                    EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVariantCell(ModelEntityHistory hist, ModelVariant v, string published, int cell)
        {
            var rect = GUILayoutUtility.GetRect(cell, cell + 30, GUILayout.Width(cell));
            var previewRect = new Rect(rect.x, rect.y, cell, cell);
            var labelRect = new Rect(rect.x, rect.y + cell, cell, 28);

            bool isActive = hist.activeId == v.id;
            bool isPublished = !string.IsNullOrEmpty(published) &&
                (v.assetPath == published || (isActive && published.EndsWith("/" + _selected.EntityId + ".glb")));
            bool isSelected = _selectedVariantId == v.id;

            EditorGUI.DrawRect(previewRect, new Color(0.13f, 0.13f, 0.15f));
            var asset = AssetDatabase.LoadMainAssetAtPath(v.assetPath);
            Texture2D thumb = asset != null ? AssetPreview.GetAssetPreview(asset) : null;
            if (thumb != null) GUI.DrawTexture(previewRect.Shrink(3), thumb, ScaleMode.ScaleToFit);
            else GUI.Label(previewRect, asset == null ? "нет файла" : "…", EditorStyles.centeredGreyMiniLabel);

            if (isSelected) DrawBorder(previewRect, new Color(1f, .8f, .3f), 2);
            else if (isPublished) DrawBorder(previewRect, new Color(.3f, .8f, .4f), 2);

            string mark = v.id == "original" ? "базовый"
                : v.IsAnimation ? "▶ " + v.animLabel
                : $"{v.engine} · {v.seed}";
            GUI.Label(labelRect, (isPublished ? "✔ " : "") + mark, EditorStyles.miniLabel);

            var e = Event.current;
            if (e.type == EventType.MouseDown && previewRect.Contains(e.mousePosition))
            {
                if (e.button == 0)
                {
                    if (e.clickCount == 2) { PublishVariant(v); }
                    else _selectedVariantId = v.id;
                    e.Use(); Repaint();
                }
                else if (e.button == 1)
                {
                    ShowVariantMenu(v);
                    e.Use();
                }
            }
        }

        private void ShowVariantMenu(ModelVariant v)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Опубликовать"), false, () => PublishVariant(v));
            menu.AddItem(new GUIContent("Показать в Project"), false, () =>
            {
                var a = AssetDatabase.LoadMainAssetAtPath(v.assetPath);
                if (a != null) EditorGUIUtility.PingObject(a);
            });
            menu.AddItem(new GUIContent($"Спрайт-лист ({_sheetDirections} напр., {_sheetCell}px)"), false, () => RenderSheet(v));

            menu.AddSeparator("");
            if (!v.IsAnimation)
                menu.AddItem(new GUIContent("Meshy: риггинг (гуманоид, ~5 кредитов)"), false, () => RigVariant(v));
            else
                menu.AddDisabledItem(new GUIContent("Meshy: риггинг (это уже анимация)"));
            string rigId = FindRigTaskId(v);
            if (!string.IsNullOrEmpty(rigId) && _animActionIds.Trim().Length > 0)
                menu.AddItem(new GUIContent($"Meshy: анимации [{_animActionIds}]"), false, () => AnimateVariant(v, rigId));
            else
                menu.AddDisabledItem(new GUIContent("Meshy: анимации (нужен риггинг и action id в футере)"));
            if (v.IsAnimation || HasClips(v.assetPath))
                menu.AddItem(new GUIContent("Анимированный спрайт-лист"), false, () => RenderAnimatedSheet(v));

            menu.AddSeparator("");
            if (v.id != "original")
                menu.AddItem(new GUIContent("Удалить вариант"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Удалить вариант?",
                        $"{_selected.EntityId}: {v.id}\nФайл будет удалён.", "Удалить", "Отмена"))
                        ModelVariantStore.DeleteVariant(_selected.EntityId, v);
                });
            else
                menu.AddDisabledItem(new GUIContent("Удалить вариант"));
            menu.ShowAsContext();
        }

        private void PublishVariant(ModelVariant v)
        {
            string dest = ModelVariantStore.Publish(_selected, v);
            if (dest != null)
            {
                _selectedVariantId = v.id;
                ShowNotification(new GUIContent("Опубликовано: " + Path.GetFileName(dest)));
            }
        }

        /// <summary>
        /// Последний шаг конвейера: опубликованная модель → префаб в проекте →
        /// ссылка в поле игрового конфига (контракт сущности: GetTargetAsset +
        /// GetTargetField). Без этого модель приходилось таскать руками, хотя
        /// студия и так знает, куда она предназначена.
        /// </summary>
        /// <summary>Выбранный (или активный) вариант истории — с ним и работаем.</summary>
        private ModelVariant SelectedVariant(ModelEntityHistory hist)
        {
            if (hist == null || hist.variants == null || hist.variants.Count == 0) return null;
            foreach (var v in hist.variants)
                if (v.id == _selectedVariantId) return v;
            foreach (var v in hist.variants)
                if (v.id == hist.activeId) return v;
            return hist.variants[hist.variants.Count - 1];
        }

        private void CreatePrefabAndAssign(ModelContentItem item, ModelEntityHistory hist)
        {
            string modelPath = item.GetPublishedModelPath?.Invoke();

            // Не опубликовано — публикуем ВЫБРАННЫЙ вариант сами: отдельный ручной
            // шаг здесь ничего не решает, а конвейер разрывает
            if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
            {
                var v = SelectedVariant(hist);
                if (v == null)
                {
                    EditorUtility.DisplayDialog("Создать префаб",
                        "Нет ни одного варианта модели — сначала сгенерируйте.", "Ладно");
                    return;
                }
                modelPath = ModelVariantStore.Publish(item, v);
                if (string.IsNullOrEmpty(modelPath))
                {
                    EditorUtility.DisplayDialog("Создать префаб", "Публикация варианта не удалась.", "Ладно");
                    return;
                }
                _selectedVariantId = v.id;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                EditorUtility.DisplayDialog("Создать префаб", "Модель не читается как GameObject: " + modelPath, "Ладно");
                return;
            }

            string folder = item.GetPrefabFolder?.Invoke();
            if (string.IsNullOrEmpty(folder)) folder = ModelVariantStore.DefaultPrefabsFolder;
            Directory.CreateDirectory(folder);

            // Инстанс модели → префаб-ассет. Нормализуем трансформ: модель
            // ставится на гекс кодом, ей нужен чистый нулевой корень
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = item.EntityId;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{item.EntityId}.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            DestroyImmediate(instance);

            if (prefab == null)
            {
                ShowNotification(new GUIContent("Префаб не создался"));
                return;
            }

            int tris = CountTriangles(prefab);
            string note = $"Префаб: {Path.GetFileName(prefabPath)} ({tris} тр.)";

            // Подстановка в поле конфига — то, ради чего всё и затевалось
            var target = item.GetTargetAsset?.Invoke();
            string field = item.GetTargetField?.Invoke();
            if (target != null && !string.IsNullOrEmpty(field))
            {
                var so = new SerializedObject(target);
                var prop = so.FindProperty(field);
                if (prop == null)
                    note += $" · поле «{field}» не найдено в {target.name}";
                else
                {
                    prop.objectReferenceValue = prefab;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssets();
                    note += $" → {target.name}.{field}";
                }
            }

            EditorGUIUtility.PingObject(prefab);
            ShowNotification(new GUIContent(note));
        }

        private static int CountTriangles(GameObject go)
        {
            int tris = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) tris += smr.sharedMesh.triangles.Length / 3;
            return tris;
        }

        private void RenderSheet(ModelVariant v)
        {
            string outPath = $"{ModelVariantStore.SheetsFolder}/{_selected.EntityId}_turntable.png";
            string res = ModelSpriteSheetRenderer.Render(v.assetPath, outPath,
                _sheetDirections, _sheetCell, _sheetElevation);
            if (res != null)
            {
                var a = AssetDatabase.LoadMainAssetAtPath(res);
                if (a != null) EditorGUIUtility.PingObject(a);
                ShowNotification(new GUIContent("Спрайт-лист готов"));
            }
        }

        // ────────────────────────── Meshy: риггинг и анимации ──────────────────────────

        private string _animActionIds = "";   // id из библиотеки Meshy, через запятую
        private float _rigHeight = 1.8f;

        private static bool HasClips(string assetPath)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (a is AnimationClip) return true;
            return false;
        }

        private string FindRigTaskId(ModelVariant v)
        {
            if (!string.IsNullOrEmpty(v.meshyRigTaskId)) return v.meshyRigTaskId;
            if (!string.IsNullOrEmpty(v.parentVariantId))
            {
                var hist = ModelVariantStore.Library.Get(_selected.EntityId);
                var parent = hist?.Find(v.parentVariantId);
                if (parent != null && !string.IsNullOrEmpty(parent.meshyRigTaskId)) return parent.meshyRigTaskId;
            }
            return null;
        }

        private async void RigVariant(ModelVariant v)
        {
            var item = _selected;
            var bytes = System.IO.File.ReadAllBytes(Path.GetFullPath(v.assetPath));
            var (rigId, walkGlb, error) = await MeshyClient.RigAsync(
                bytes, ModelAiSettings.MeshyApiKey, _rigHeight,
                (p, s) => { AiModelGeneratorProgressProxy(p, s); }, System.Threading.CancellationToken.None);
            if (error != null)
            {
                Debug.LogError("[ModelStudio] Риггинг: " + error);
                ShowNotification(new GUIContent("Риггинг: " + error));
                return;
            }
            v.meshyRigTaskId = rigId;
            if (walkGlb != null)
            {
                var res = new ModelGenResult
                {
                    Request = new ModelGenRequest { EntityId = item.EntityId, Engine = ModelEngine.Meshy },
                    GlbBytes = walkGlb,
                };
                var animVariant = ModelVariantStore.AddVariant(res, false);
                animVariant.animLabel = "walk";
                animVariant.parentVariantId = v.id;
            }
            EditorUtility.SetDirty(ModelVariantStore.Library);
            AssetDatabase.SaveAssetIfDirty(ModelVariantStore.Library);
            ShowNotification(new GUIContent("Риггинг готов (rig id сохранён)"));
            Repaint();
        }

        private async void AnimateVariant(ModelVariant v, string rigId)
        {
            var item = _selected;
            foreach (var part in _animActionIds.Split(','))
            {
                if (!int.TryParse(part.Trim(), out int actionId)) continue;
                var (glb, error) = await MeshyClient.AnimateAsync(
                    rigId, actionId, ModelAiSettings.MeshyApiKey,
                    (p, s) => { AiModelGeneratorProgressProxy(p, s); }, System.Threading.CancellationToken.None);
                if (error != null)
                {
                    Debug.LogError($"[ModelStudio] Анимация #{actionId}: {error}");
                    continue;
                }
                var res = new ModelGenResult
                {
                    Request = new ModelGenRequest { EntityId = item.EntityId, Engine = ModelEngine.Meshy },
                    GlbBytes = glb,
                };
                var animVariant = ModelVariantStore.AddVariant(res, false);
                animVariant.animLabel = "action_" + actionId;
                animVariant.parentVariantId = v.id;
            }
            EditorUtility.SetDirty(ModelVariantStore.Library);
            AssetDatabase.SaveAssetIfDirty(ModelVariantStore.Library);
            ShowNotification(new GUIContent("Анимации готовы"));
            Repaint();
        }

        private void AiModelGeneratorProgressProxy(float p, string label)
        {
            // риггинг/анимации идут мимо очереди — показываем в футере тем же полем
            _meshyOpProgress = p; _meshyOpLabel = label;
        }
        private float _meshyOpProgress = -1f;
        private string _meshyOpLabel;

        // ────────────────── референс: буфер обмена и Арт студия ──────────────────

        /// <summary>
        /// Вставить референс из буфера обмена. Unity умеет отдавать из буфера только
        /// текст (systemCopyBuffer), поэтому картинку достаём системой: PowerShell
        /// сохраняет её PNG-файлом, дальше обычный импорт ассета.
        /// </summary>
        private void PasteReferenceFromClipboard(ModelContentItem item)
        {
            string refFolder = ModelVariantStore.RefsFolder;
            Directory.CreateDirectory(refFolder);
            string assetPath = $"{refFolder}/{item.EntityId}_clip.png";
            string absPath = Path.GetFullPath(assetPath).Replace('\\', '/');

            string ps =
                "Add-Type -AssemblyName System.Windows.Forms,System.Drawing; " +
                "$img=[System.Windows.Forms.Clipboard]::GetImage(); " +
                $"if ($img -ne $null) {{ $img.Save('{absPath}', " +
                "[System.Drawing.Imaging.ImageFormat]::Png); Write-Output 'OK' } " +
                "else { Write-Output 'NOIMAGE' }";

            string result;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("powershell",
                    $"-NoProfile -STA -ExecutionPolicy Bypass -Command \"{ps}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                result = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(8000);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Вставка из буфера", "Не вышло: " + e.Message, "Ладно");
                return;
            }

            if (!result.Contains("OK") || !File.Exists(absPath))
            {
                EditorUtility.DisplayDialog("Вставка из буфера",
                    "В буфере нет картинки. Скопируйте изображение (Ctrl+C) и повторите.", "Ладно");
                return;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            item.SetReferenceImagePath?.Invoke(assetPath);
            Repaint();
        }

        /// <summary>Форматы, которые Unity импортирует как модель.</summary>
        private static readonly string[] ModelExtensions = { ".glb", ".gltf", ".fbx", ".obj" };

        /// <summary>
        /// Вставить готовую модель из буфера обмена — так же, как картинку-референс,
        /// только в буфере лежит не изображение, а файл: скачал GLB из Meshy,
        /// Ctrl+C в проводнике — и он становится вариантом сущности.
        /// </summary>
        private void PasteModelFromClipboard(ModelContentItem item)
        {
            const string ps =
                "Add-Type -AssemblyName System.Windows.Forms; " +
                "$f=[System.Windows.Forms.Clipboard]::GetFileDropList(); " +
                "if ($f -ne $null -and $f.Count -gt 0) { foreach ($p in $f) { Write-Output $p } } " +
                "else { $t=[System.Windows.Forms.Clipboard]::GetText(); if ($t) { Write-Output $t } }";

            string output;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("powershell",
                    $"-NoProfile -STA -ExecutionPolicy Bypass -Command \"{ps}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(8000);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Вставка модели", "Не вышло: " + e.Message, "Ладно");
                return;
            }

            // В буфере может лежать несколько файлов (или просто путь текстом) —
            // берём первый, который Unity умеет импортировать
            string picked = null;
            foreach (var raw in output.Split('\n'))
            {
                string p = raw.Trim().Trim('"');
                if (p.Length == 0) continue;
                string ext = Path.GetExtension(p).ToLowerInvariant();
                if (Array.IndexOf(ModelExtensions, ext) < 0 || !File.Exists(p)) continue;
                picked = p;
                break;
            }

            if (picked == null)
            {
                EditorUtility.DisplayDialog("Вставка модели",
                    "В буфере нет файла модели. Скопируйте .glb / .fbx / .obj в проводнике " +
                    "(Ctrl+C) и повторите.", "Ладно");
                return;
            }

            ImportExternalModel(item, picked);
        }

        /// <summary>Завести файл со стороны вариантом сущности и показать его.</summary>
        private void ImportExternalModel(ModelContentItem item, string path)
        {
            if (Path.GetExtension(path).ToLowerInvariant() == ".gltf" &&
                !EditorUtility.DisplayDialog("Формат .gltf",
                    "У .gltf геометрия и текстуры лежат отдельными файлами — копируется только " +
                    "сам .gltf, модель может приехать пустой. Надёжнее .glb или .fbx.",
                    "Всё равно вставить", "Отмена"))
                return;

            var variant = ModelVariantStore.ImportExternal(item, path);
            if (variant == null)
            {
                EditorUtility.DisplayDialog("Вставка модели",
                    "Unity не смог импортировать файл — подробности в консоли.", "Ладно");
                return;
            }

            _selectedVariantId = variant.id;
            Debug.Log($"[Студия 3D] {item.DisplayName}: вставлена модель {Path.GetFileName(path)} " +
                      $"→ вариант {variant.id}");
            Repaint();
        }

        /// <summary>
        /// Взять референсом иконку из ИИ Арт студии: штатный порядок пайплайна —
        /// сначала концепт картинкой, потом по нему объём. Меню строится из тех же
        /// наборов, что показывает арт-студия (IArtContentProvider).
        /// </summary>
        private void ShowArtIconPicker(ModelContentItem item)
        {
            var menu = new GenericMenu();
            int found = 0;

            // Провайдеры — те же, что у наборов Арт студии (IconCollectionAsset.provider)
            var providers = new List<IArtContentProvider>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(IconCollectionAsset)))
            {
                var col = AssetDatabase.LoadAssetAtPath<IconCollectionAsset>(AssetDatabase.GUIDToAssetPath(guid));
                var p = col != null ? col.Provider : null;
                if (p != null && !providers.Contains(p)) providers.Add(p);
            }

            foreach (var provider in providers)
            {
                foreach (var set in provider.GetArtSets())
                {
                    if (set?.Items == null) continue;
                    foreach (var art in set.Items)
                    {
                        var sprite = art.GetSprite?.Invoke();
                        if (sprite == null) continue;   // не сгенерировано — предлагать нечего

                        string path = AssetDatabase.GetAssetPath(sprite);
                        if (string.IsNullOrEmpty(path)) continue;

                        found++;
                        menu.AddItem(new GUIContent($"{set.Title}/{art.DisplayName}"), false, () =>
                        {
                            item.SetReferenceImagePath?.Invoke(path);
                            Repaint();
                        });
                    }
                }
            }

            if (found == 0)
                menu.AddDisabledItem(new GUIContent("В Арт студии пока нет сгенерированных иконок"));
            menu.ShowAsContext();
        }

        private void RenderAnimatedSheet(ModelVariant v)
        {
            string outPath = $"{ModelVariantStore.SheetsFolder}/{_selected.EntityId}_anim.png";
            string[] clipNames;
            string res = ModelSpriteSheetRenderer.RenderAnimated(v.assetPath, outPath,
                out clipNames, 8, _sheetCell, _sheetElevation);
            if (res == null) { ShowNotification(new GUIContent("В модели нет анимационных клипов")); return; }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(res);
            EditorGUIUtility.PingObject(tex);
            if (tex == null) return;

            // Куда отдать лист дальше — решает проект (напр., атлас моба в «Арт мобов»)
            foreach (var handler in ModelStudioExtensions.AnimatedSheetRendered)
            {
                string note = null;
                try { note = handler(_selected, tex, clipNames); }
                catch (Exception e) { Debug.LogException(e); }
                if (!string.IsNullOrEmpty(note)) ShowNotification(new GUIContent(note));
            }
        }

        // ────────────────────────── генерация ──────────────────────────

        private void EnqueueGeneration(ModelContentItem item)
        {
            string refPath = item.GetReferenceImagePath?.Invoke();
            string absRef = null;
            if (!string.IsNullOrEmpty(refPath))
                absRef = Path.GetFullPath(refPath);

            var req = new ModelGenRequest
            {
                EntityId = item.EntityId,
                Engine = _engine,
                Prompt = item.GetPrompt?.Invoke() ?? "",
                RefImagePath = absRef,
                Seed = _randomSeed ? UnityEngine.Random.Range(1, int.MaxValue) : _seed,
                Steps = _steps,
                Guidance = _guidance,
                OctreeRes = _octree,
                // Лимит сущности важнее общего поля окна: метке глобуса нужен
                // настоящий low-poly (1500–3000), мобу — детализация
                MaxFaces = item.GetMaxFaces != null && item.GetMaxFaces() > 0
                    ? item.GetMaxFaces() : _maxFaces,
                TextureSize = _texSize,
            };
            AiModelGenerator.Enqueue(req, result =>
            {
                if (!result.Ok)
                {
                    ShowNotification(new GUIContent("Ошибка: " + result.Error));
                    return;
                }
                var variant = ModelVariantStore.AddVariant(result);
                _selectedVariantId = variant.id;
                Repaint();
            });
        }

        // ────────────────────────── футер ──────────────────────────

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (AiModelGenerator.Pending > 0)
            {
                var r = GUILayoutUtility.GetRect(160, 16, GUILayout.Width(160));
                EditorGUI.ProgressBar(r, AiModelGenerator.CurrentProgress, AiModelGenerator.CurrentLabel);
                GUILayout.Label($"в очереди: {AiModelGenerator.Pending}", EditorStyles.miniLabel);
            }
            else if (_meshyOpProgress >= 0f && _meshyOpProgress < 1f)
            {
                var r = GUILayoutUtility.GetRect(160, 16, GUILayout.Width(160));
                EditorGUI.ProgressBar(r, _meshyOpProgress, _meshyOpLabel ?? "Meshy");
            }
            else GUILayout.Label("Очередь пуста", EditorStyles.miniLabel);

            GUILayout.Space(12);
            GUILayout.Label(new GUIContent("Анимации (action id):",
                "Id из библиотеки Meshy (docs.meshy.ai → Animation Library), через запятую"),
                EditorStyles.miniLabel);
            _animActionIds = EditorGUILayout.TextField(_animActionIds, GUILayout.Width(110));
            GUILayout.Label("рост, м", EditorStyles.miniLabel);
            _rigHeight = EditorGUILayout.FloatField(_rigHeight, GUILayout.Width(36));

            GUILayout.FlexibleSpace();
            GUILayout.Label("Спрайт-лист:", EditorStyles.miniLabel);
            _sheetDirections = EditorGUILayout.IntPopup(_sheetDirections,
                new[] { "4", "8", "16" }, new[] { 4, 8, 16 }, GUILayout.Width(44));
            _sheetCell = EditorGUILayout.IntPopup(_sheetCell,
                new[] { "128", "256", "512" }, new[] { 128, 256, 512 }, GUILayout.Width(52));
            GUILayout.Label("наклон", EditorStyles.miniLabel);
            _sheetElevation = EditorGUILayout.Slider(_sheetElevation, 15f, 90f, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawBorder(Rect r, Color c, int w)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, w), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - w, r.width, w), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, w, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - w, r.y, w, r.height), c);
        }
    }

    internal static class RectExt
    {
        public static Rect Shrink(this Rect r, float px) =>
            new(r.x + px, r.y + px, r.width - px * 2, r.height - px * 2);
    }
}
