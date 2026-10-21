using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Студия иконок — единое окно всего арт-цеха (UI Toolkit, retained mode):
    /// «наборы» (иконки зданий, превью зданий…) вкладками и тегами, сетка-витрина,
    /// панель генерации (профили/модель/референсы), пикер вариантов с промптом
    /// и стилизацией. Обновляется по событиям стора/генератора — пер-кадрового кода нет.
    ///
    /// Вкладки собираются из ассетов: IconStudioConfig (центральный конфиг) → наборы
    /// (IconCollectionAsset) и группы; сущности приходят от провайдеров (IArtContentProvider).
    /// Окно не знает типов сущностей проекта: проектные надстройки подключаются через
    /// IconStudioExtensions.
    /// </summary>
    public class IconStudioWindow : EditorWindow
    {
        [MenuItem("ProtoSystem/ИИ студии/ИИ Арт студия")]
        public static void Open()
        {
            var w = GetWindow<IconStudioWindow>("ИИ Арт студия");
            w.titleContent = new GUIContent("ИИ Арт студия");   // и для уже открытого инстанса
            w.minSize = new Vector2(760, 420);
        }

        // ── Настройки окна (EditorPrefs, кэшированные) ──

        private static string _viewId;
        private static string ViewId
        {
            get => _viewId ??= EditorPrefs.GetString("IconStudio.View", "");
            set { _viewId = value; EditorPrefs.SetString("IconStudio.View", value); }
        }

        /// <summary>GUID выбранного IconStudioConfig (проектов-конфигов может быть несколько).</summary>
        private static string _configGuid;
        private static string ConfigGuid
        {
            get => _configGuid ??= EditorPrefs.GetString("IconStudio.Config", "");
            set { _configGuid = value; EditorPrefs.SetString("IconStudio.Config", value); }
        }

        // ── Конфиг студии: источник вкладок ──

        private List<IconStudioConfig> _allConfigs;

        private List<IconStudioConfig> AllConfigs()
        {
            if (_allConfigs != null)
            {
                _allConfigs.RemoveAll(c => c == null);
                return _allConfigs;
            }
            _allConfigs = new List<IconStudioConfig>();
            foreach (var guid in AssetDatabase.FindAssets("t:IconStudioConfig"))
                _allConfigs.Add(AssetDatabase.LoadAssetAtPath<IconStudioConfig>(
                    AssetDatabase.GUIDToAssetPath(guid)));
            return _allConfigs;
        }

        private IconStudioConfig ActiveConfig()
        {
            var configs = AllConfigs();
            if (configs.Count == 0) return null;
            foreach (var c in configs)
                if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c)) == ConfigGuid)
                    return c;
            return configs[0];
        }

        private static int? _tileSize;
        private static int TileSize
        {
            get => _tileSize ??= EditorPrefs.GetInt("IconStudio.TileSize", 96);
            set { _tileSize = value; EditorPrefs.SetInt("IconStudio.TileSize", value); }
        }

        private static int? _batchCount;
        private static int BatchCount
        {
            get => _batchCount ??= EditorPrefs.GetInt("IconStudio.Batch", 4);
            set { _batchCount = value; EditorPrefs.SetInt("IconStudio.Batch", value); }
        }

        private static bool? _showNames;
        private static bool ShowNames
        {
            get => _showNames ??= EditorPrefs.GetBool("IconStudio.Names", true);
            set { _showNames = value; EditorPrefs.SetBool("IconStudio.Names", value); }
        }

        private static float? _stylizeDenoise;
        private static float StylizeDenoise
        {
            get => _stylizeDenoise ??= EditorPrefs.GetFloat("IconStudio.Denoise", 0.6f);
            set { _stylizeDenoise = value; EditorPrefs.SetFloat("IconStudio.Denoise", value); }
        }

        // Профиль набора живёт В АССЕТЕ набора (SourceAsset.profile) — сериализуется,
        // шарится через VCS; EditorPrefs больше не используется.

        // ── Состояние ──

        private string _selectedEntityId;
        private string _promptTweak = "";
        private bool _refreshQueued;

        // Размеры растяжимой примерки 9-slice UI-элемента (переживают пересборку пикера)
        private int _uiPreviewW = 256;
        private int _uiPreviewH = 128;

        // Прокрутка ленты вариантов: без сохранения выбор варианта сбрасывал ленту в начало
        private Vector2 _laneScroll;
        private string _laneScrollEntity;

        // Редактируемый размер рендишена: клик по варианту/чипу назначает в него.
        // 0 — мастер (крупнейший размер набора). Сбрасывается при смене сущности.
        private int _editRenditionSize;

        // Множественное выделение вариантов (Ctrl/Shift): для групповых удалений/пометок.
        // Живёт в пределах одной сущности; ячейки ленты — для обновления рамок без пересборки.
        private readonly HashSet<string> _pickedVariantIds = new();
        private string _pickAnchorId;
        private readonly Dictionary<string, VisualElement> _laneCells = new();

        /// <summary>Варианты плиткой (перенос) вместо одной линии.</summary>
        private static bool? _variantsWrap;
        private static bool VariantsWrap
        {
            get => _variantsWrap ??= EditorPrefs.GetBool("IconStudio.VarWrap", false);
            set { _variantsWrap = value; EditorPrefs.SetBool("IconStudio.VarWrap", value); }
        }

        /// <summary>Размер ячейки варианта — слайдер уместен: это просмотр, а не игровые разрешения.</summary>
        private static int? _variantCellSize;
        private static int VariantCellSize
        {
            get => _variantCellSize ??= EditorPrefs.GetInt("IconStudio.VarCell", 96);
            set { _variantCellSize = value; EditorPrefs.SetInt("IconStudio.VarCell", value); }
        }

        // Элементы, которые пересобираются
        private VisualElement _grid;
        private VisualElement _pickerHost;
        private TwoPaneSplitView _vSplit;
        private VisualElement _leftPanel;
        private Label _queueLabel;
        private Button _queueCancel;
        private ProgressBar _progressBar;
        private DropdownField _packDropdown;
        private List<IconPack> _packChoices = new();

        /// <summary>Подложка под плитками/вариантами — прикинуть иконку на игровом фоне.</summary>
        private static Color? _tileBg;
        private static Color TileBg
        {
            get
            {
                if (_tileBg == null)
                {
                    var s = EditorPrefs.GetString("IconStudio.TileBg", "");
                    _tileBg = ColorUtility.TryParseHtmlString("#" + s, out var c)
                        ? c : new Color(0.12f, 0.12f, 0.12f);
                }
                return _tileBg.Value;
            }
            set
            {
                _tileBg = value;
                EditorPrefs.SetString("IconStudio.TileBg", ColorUtility.ToHtmlStringRGBA(value));
            }
        }

        // Списки моделей с сервера (общие на окно)
        private static List<string> _serverCheckpoints;
        private static List<string> _serverLoras;
        private static bool _modelsLoading;

        // ── Жизненный цикл ──

        private void CreateGUI()
        {
            BuildUI();

            IconVariantStore.Changed += OnStoreChanged;
            AiIconGenerator.QueueChanged += OnQueueChanged;
            IconGenAssetWatcher.AssetsChanged += OnAssetsChanged;
            if (_serverCheckpoints == null) FetchModelLists();
        }

        /// <summary>Полная пересборка окна (смена конфига студии).</summary>
        private void RebuildAll() => BuildUI();

        private void OnAssetsChanged() => _allConfigs = null;   // список конфигов мог измениться

        private void BuildUI()
        {
            rootVisualElement.Clear();

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.protosystem.core/Editor/IconGen/IconStudio.uss");
            if (uss != null) rootVisualElement.styleSheets.Add(uss);

            // Сплиттер: ширина левой панели тянется мышью (и запоминается Unity автоматически)
            var split = new TwoPaneSplitView(0, 320, TwoPaneSplitViewOrientation.Horizontal);
            rootVisualElement.Add(split);

            _leftPanel = new ScrollView { name = "left-panel" };
            _leftPanel.AddToClassList("left-panel");
            split.Add(_leftPanel);

            var main = new VisualElement { style = { flexGrow = 1 } };
            split.Add(main);

            main.Add(BuildToolbar());

            // Прогресс пачки — прямо в окне (дублирует фоновый Progress в статус-баре)
            _progressBar = new ProgressBar { lowValue = 0f, highValue = 1f };
            _progressBar.style.display = DisplayStyle.None;
            main.Add(_progressBar);

            // Вертикальный сплиттер: высота пикера тоже тянется мышью
            _vSplit = new TwoPaneSplitView(1, 320, TwoPaneSplitViewOrientation.Vertical)
                { style = { flexGrow = 1 } };
            main.Add(_vSplit);

            var gridScroll = new ScrollView();
            gridScroll.AddToClassList("grid-scroll");
            _grid = new VisualElement();
            _grid.AddToClassList("grid");
            gridScroll.Add(_grid);
            _vSplit.Add(gridScroll);

            _pickerHost = new ScrollView();
            _pickerHost.AddToClassList("picker");
            _vSplit.Add(_pickerHost);

            RebuildLeftPanel();
            RebuildGrid();
            RebuildPicker();
            UpdateQueueUI();
        }

        private void OnDisable()
        {
            IconVariantStore.Changed -= OnStoreChanged;
            AiIconGenerator.QueueChanged -= OnQueueChanged;
            IconGenAssetWatcher.AssetsChanged -= OnAssetsChanged;
            EditorApplication.update -= RefreshOnce;   // висящий одноразовый апдейт
            _refreshQueued = false;
        }

        /// <summary>
        /// Дебаунс: событий за один клик может прийти несколько — пересобираем раз.
        /// Одноразовый EditorApplication.update, а НЕ schedule панели и НЕ delayCall:
        /// планировщик окна не тикает у скрытой/пересозданной панели (флаг зависал,
        /// и клик по варианту переставал обновлять грид), а delayCall дренится только
        /// при обновлении GUI — в фоновом редакторе не срабатывает. update тикает всегда.
        /// </summary>
        private void OnStoreChanged()
        {
            if (_refreshQueued) return;
            _refreshQueued = true;
            EditorApplication.update += RefreshOnce;
        }

        private void RefreshOnce()
        {
            EditorApplication.update -= RefreshOnce;
            _refreshQueued = false;
            if (this == null || rootVisualElement == null) return;   // окно закрыли
            RebuildGrid();
            RebuildPicker();
            RefreshPackDropdown();
            if (Application.isPlaying) RefreshBaseWindow();
        }

        private void OnQueueChanged() => rootVisualElement.schedule.Execute(UpdateQueueUI);

        /// <summary>В плей-моде проект перерисовывает игровой UI (подписка в IconStudioExtensions).</summary>
        private static void RefreshBaseWindow() => IconStudioExtensions.RaisePlayModeArtChanged();

        // ── Виды (вкладки из IconStudioConfig: наборы и группы) ──

        private struct ViewDef
        {
            public string Id;      // GUID ассета вкладки (набор или группа)
            public string Title;
            public UnityEngine.Object Tab;
        }

        private List<ViewDef> Views()
        {
            var views = new List<ViewDef>();
            var config = ActiveConfig();

            if (config != null)
            {
                foreach (var tab in config.tabs)
                {
                    if (tab == null) continue;
                    views.Add(new ViewDef
                    {
                        Id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tab)),
                        Title = config.TabTitle(tab),
                        Tab = tab,
                    });
                }
            }
            else
            {
                // Конфига нет — fallback: все ассеты наборов проекта
                foreach (var guid in AssetDatabase.FindAssets("t:IconCollectionAsset"))
                {
                    var col = AssetDatabase.LoadAssetAtPath<IconCollectionAsset>(
                        AssetDatabase.GUIDToAssetPath(guid));
                    if (col == null) continue;
                    views.Add(new ViewDef { Id = guid, Title = col.ResolveTitle(), Tab = col });
                }
            }
            return views;
        }

        /// <summary>Активная вкладка (по ViewId; нет — первая).</summary>
        private ViewDef ActiveView()
        {
            var views = Views();
            if (views.Count == 0) return default;

            ViewDef active = views[0];
            foreach (var v in views)
                if (v.Id == ViewId) { active = v; break; }
            return active;
        }

        private List<IconCollection> ActiveCollections()
        {
            var result = new List<IconCollection>();
            var views = Views();
            if (views.Count == 0) return result;

            ViewDef active = ActiveView();

            var config = ActiveConfig();
            var assets = config != null
                ? config.CollectionsOfTab(active.Tab)
                : (active.Tab is IconCollectionAsset one
                    ? new List<IconCollectionAsset> { one } : new List<IconCollectionAsset>());

            foreach (var asset in assets)
            {
                var col = IconCollection.FromAsset(asset);
                if (col != null) result.Add(col);
            }
            return result;
        }

        /// <summary>Одиночный активный набор (для панели генерации); в группе — первый.</summary>
        private IconCollection ActiveCollection()
        {
            var list = ActiveCollections();
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>Сущности активной вкладки — область паков и «Сгенерировать все».</summary>
        private HashSet<string> ActiveEntityIds()
        {
            var ids = new HashSet<string>();
            foreach (var col in ActiveCollections())
            {
                var items = col.GetItems?.Invoke();
                if (items == null) continue;
                foreach (var item in items)
                    if (item != null && !string.IsNullOrEmpty(item.EntityId))
                        ids.Add(item.EntityId);
            }
            return ids;
        }

        // ── Тулбар ──

        private VisualElement BuildToolbar()
        {
            // Два этажа: контролов больше, чем влезает в одну строку — правый край
            // (галочки «1:1», имена) выталкивался за окно и был недоступен.
            // Верх — конфиг и лента вкладок, низ — действия и настройки сетки.
            var root = new VisualElement();
            var toolbar = new Toolbar();     // нижний этаж: действия
            var tabsBar = new Toolbar();     // верхний этаж: вкладки
            root.Add(tabsBar);
            root.Add(toolbar);

            // Несколько конфигов студии (разные наборы кампаний/стилей) — выбор первым
            var configs = AllConfigs();
            if (configs.Count > 1)
            {
                var configNames = new List<string>();
                int cur = 0;
                var active = ActiveConfig();
                for (int i = 0; i < configs.Count; i++)
                {
                    configNames.Add(configs[i].name);
                    if (configs[i] == active) cur = i;
                }
                var configDrop = new DropdownField(configNames, cur) { tooltip = "Конфигурация студии" };
                configDrop.RegisterValueChangedCallback(e =>
                {
                    int idx = configNames.IndexOf(e.newValue);
                    if (idx < 0) return;
                    ConfigGuid = AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(configs[idx]));
                    RebuildAll();
                });
                tabsBar.Add(configDrop);
            }

            // Лента вкладок: их больше, чем влезает в окно, поэтому лента ездит
            // горизонтально, а по бокам — кнопки листания. Порядок вкладок правится
            // из ПКМ-меню (он живёт в IconStudioConfig.tabs и общий для команды).
            var tabsScroll = new ScrollView(ScrollViewMode.Horizontal)
            {
                style = { flexGrow = 1, flexShrink = 1 },
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
            };
            tabsScroll.contentContainer.style.flexDirection = FlexDirection.Row;

            var scrollLeft = new ToolbarButton(() => ScrollTabs(tabsScroll, -1))
                { text = "‹", tooltip = "Листать вкладки влево" };
            var scrollRight = new ToolbarButton(() => ScrollTabs(tabsScroll, 1))
                { text = "›", tooltip = "Листать вкладки вправо" };
            scrollLeft.style.width = 22;
            scrollRight.style.width = 22;
            tabsBar.Add(scrollLeft);

            var viewList = Views();
            for (int vi = 0; vi < viewList.Count; vi++)
            {
                var view = viewList[vi];
                int index = vi;
                var toggle = new ToolbarToggle { text = view.Title, value = IsActiveView(view.Id) };
                toggle.RegisterValueChangedCallback(_ =>
                {
                    ViewId = view.Id;
                    ApplyCollectionProfile(ActiveCollection());
                    RebuildToolbarToggles(tabsScroll.contentContainer);
                    RebuildLeftPanel();
                    RebuildGrid();
                    RebuildPicker();
                    RefreshPackDropdown();   // паки — области вкладки, список меняется с ней
                });
                // ПКМ — перестановка: «нужные рядом» настраивается за пару кликов
                toggle.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Сдвинуть влево", _ => MoveTab(index, -1),
                        index > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                    evt.menu.AppendAction("Сдвинуть вправо", _ => MoveTab(index, 1),
                        index < viewList.Count - 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("В начало", _ => MoveTab(index, -index),
                        index > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                }));
                tabsScroll.Add(toggle);
            }

            tabsBar.Add(tabsScroll);
            tabsBar.Add(scrollRight);

            // Пак: выбор применяет; «(текущий выбор)» — когда выбор не совпадает ни с одним
            _packDropdown = new DropdownField { style = { width = 170 } };
            _packDropdown.RegisterValueChangedCallback(e => OnPackPicked(e.newValue));
            toolbar.Add(_packDropdown);
            RefreshPackDropdown();

            var savePack = new ToolbarButton(() =>
            {
                var pack = IconVariantStore.SavePackFromActive(
                    $"{ActiveView().Title} {System.DateTime.Now:dd.MM HH-mm}", ActiveEntityIds());
                Debug.Log($"[Студия] Сохранён пак «{pack.packName}» ({pack.selections.Count}).");
                RefreshPackDropdown();
            }) { text = "Сохранить пак", tooltip = "Снапшот выбора сущностей активной вкладки" };
            toolbar.Add(savePack);

            var batch = new DropdownField(new List<string> { "×1", "×2", "×4", "×6", "×10" },
                "×" + BatchCount) { tooltip = "Сколько вариантов генерить за клик «+»" };
            toolbar.Add(batch);

            var genAll = new ToolbarButton(() => EnqueueAll(onlyMissing: false))
            {
                text = $"Все ×{BatchCount}",
                tooltip = "Сгенерировать варианты для всех иконок активной вкладки"
            };
            toolbar.Add(genAll);

            batch.RegisterValueChangedCallback(e =>
            {
                BatchCount = int.Parse(e.newValue.TrimStart('×'));
                genAll.text = $"Все ×{BatchCount}";
            });

            _queueLabel = new Label { tooltip = "Клик — окно очереди (отмена отдельных генераций)" };
            _queueLabel.AddToClassList("queue-label");
            _queueLabel.RegisterCallback<ClickEvent>(_ => IconGenQueueWindow.Open());
            toolbar.Add(_queueLabel);
            _queueCancel = new Button(AiIconGenerator.CancelAll)
                { text = "✕", tooltip = "Отменить очередь генерации" };
            toolbar.Add(_queueCancel);

            // Чёткие размеры: иконки живут в конкретных разрешениях, промежуточные врут
            var sizes = new List<string> { "32", "48", "64", "96", "128", "192" };
            var size = new DropdownField(sizes,
                sizes.Contains(TileSize.ToString()) ? sizes.IndexOf(TileSize.ToString()) : 3)
                { tooltip = "Размер плиток (px — как в игре)" };
            size.RegisterValueChangedCallback(e => { TileSize = int.Parse(e.newValue); RebuildGrid(); });
            toolbar.Add(size);

            var bg = new ColorField { value = TileBg, showAlpha = true, showEyeDropper = true,
                style = { width = 40 }, tooltip = "Подложка под иконками (примерить игровой фон)" };
            bg.RegisterValueChangedCallback(e =>
            {
                TileBg = e.newValue;
                RebuildGrid();
                RebuildPicker();
            });
            toolbar.Add(bg);

            var names = new ToolbarToggle { text = "Имена", value = ShowNames };
            names.RegisterValueChangedCallback(e => { ShowNames = e.newValue; RebuildGrid(); });
            toolbar.Add(names);

            var more = new ToolbarMenu { text = "⋯" };
            BuildMoreMenu(more);
            toolbar.Add(more);

            return root;
        }

        private bool IsActiveView(string id)
        {
            var views = Views();
            string cur = ViewId;
            bool exists = false;
            foreach (var v in views)
                if (v.Id == cur) { exists = true; break; }
            if (!exists && views.Count > 0) cur = views[0].Id;
            return cur == id;
        }

        /// <summary>Пролистать ленту вкладок на «экран» (кнопки ‹ › по бокам панели).</summary>
        private static void ScrollTabs(ScrollView tabs, int dir)
        {
            float step = Mathf.Max(120f, tabs.resolvedStyle.width * 0.7f);
            float max = Mathf.Max(0f, tabs.contentContainer.resolvedStyle.width - tabs.resolvedStyle.width);
            tabs.scrollOffset = new Vector2(
                Mathf.Clamp(tabs.scrollOffset.x + dir * step, 0f, max), tabs.scrollOffset.y);
        }

        /// <summary>
        /// Переставить вкладку в IconStudioConfig.tabs (порядок в ассете = порядок в
        /// студии у всей команды). Работает только для вкладок из конфига: набор,
        /// найденный автопоиском, переставлять негде.
        /// </summary>
        private void MoveTab(int index, int delta)
        {
            var config = ActiveConfig();
            if (config == null) return;

            var so = new SerializedObject(config);
            var tabs = so.FindProperty("tabs");
            if (tabs == null || index < 0 || index >= tabs.arraySize) return;

            int target = Mathf.Clamp(index + delta, 0, tabs.arraySize - 1);
            if (target == index) return;

            tabs.MoveArrayElement(index, target);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            RebuildAll();
        }

        private void RebuildToolbarToggles(VisualElement togglesHost)
        {
            int i = 0;
            var views = Views();
            foreach (var child in togglesHost.Children())
            {
                if (child is ToolbarToggle t && i < views.Count)
                {
                    t.SetValueWithoutNotify(IsActiveView(views[i].Id));
                    i++;
                }
            }
        }

        private void BuildMoreMenu(ToolbarMenu menu)
        {
            menu.menu.AppendAction("Догенерировать недостающие (без вариантов)",
                _ => EnqueueAll(onlyMissing: true));
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("Пометить показанный набор неудачным", _ => MarkShownBad(true));
            menu.menu.AppendAction("Снять пометку с показанного набора", _ => MarkShownBad(false));
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("Чистка/Удалить картинки, кроме текущего пака", _ => CleanupExceptPack(false));
            menu.menu.AppendAction("Чистка/Удалить варианты, кроме текущего пака (НЕОБРАТИМО)",
                _ => CleanupExceptPack(true));
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("Настройки AI-генерации…",
                _ => SettingsService.OpenProjectSettings("Project/AI Icon Tools"));
            // Проектные пункты (переключатели паков в Play Mode и т.п.)
            foreach (var (label, action) in IconStudioExtensions.MoreMenuItems)
                menu.menu.AppendAction(label, _ => action());
        }

        // ── Пак ──

        private void RefreshPackDropdown()
        {
            if (_packDropdown == null) return;

            // Паки — области активной вкладки: пак иконок зданий не предлагать во вкладке UI
            var ids = ActiveEntityIds();
            _packChoices = IconVariantStore.PacksFor(ids);
            var names = new List<string>();
            foreach (var p in _packChoices)
                names.Add(p == null ? "(null)"
                    : (string.IsNullOrEmpty(p.packName) ? p.name : p.packName));
            names.Add("(текущий выбор)");
            _packDropdown.choices = names;

            var matched = IconVariantStore.PackMatchingCurrent(ids);
            int idx = names.Count - 1;
            for (int i = 0; i < _packChoices.Count; i++)
                if (_packChoices[i] == matched) { idx = i; break; }
            _packDropdown.SetValueWithoutNotify(names[idx]);
        }

        private void OnPackPicked(string label)
        {
            for (int i = 0; i < _packChoices.Count; i++)
            {
                var p = _packChoices[i];
                if (p == null) continue;
                string name = string.IsNullOrEmpty(p.packName) ? p.name : p.packName;
                if (name != label) continue;

                var map = IconVariantStore.ApplyPack(p);
                SyncSpritesFromMap(map);
                return;
            }
        }

        /// <summary>Все наборы активного конфига без дублей (группа может повторять набор).</summary>
        private List<IconCollection> AllConfigCollections()
        {
            var result = new List<IconCollection>();
            var seen = new HashSet<string>();
            var config = ActiveConfig();

            if (config != null)
            {
                foreach (var tab in config.tabs)
                    foreach (var asset in config.CollectionsOfTab(tab))
                        if (asset != null && seen.Add(asset.setId))
                        {
                            var c = IconCollection.FromAsset(asset);
                            if (c != null) result.Add(c);
                        }
            }
            else
            {
                foreach (var v in Views())
                    if (v.Tab is IconCollectionAsset asset && seen.Add(asset.setId))
                    {
                        var c = IconCollection.FromAsset(asset);
                        if (c != null) result.Add(c);
                    }
            }
            return result;
        }

        /// <summary>Разложить выбранные спрайты по сущностям всех наборов (иконки И превью).</summary>
        private void SyncSpritesFromMap(Dictionary<string, Sprite> map)
        {
            foreach (var col in AllConfigCollections())
                foreach (var item in col.GetItems())
                    if (map.TryGetValue(item.EntityId, out var sprite) && sprite != null)
                        item.SetSprite?.Invoke(sprite);
        }

        // ── Левая панель: генерация ──

        private void RebuildLeftPanel()
        {
            _leftPanel.Clear();
            var col = ActiveCollection();
            if (col == null)
            {
                _leftPanel.Add(new Label("Наборы не зарегистрированы."));
                return;
            }

            _leftPanel.Add(new Label("Генерация — " + col.Title) { name = "hdr" });
            _leftPanel.Q<Label>("hdr").AddToClassList("panel-header");

            // Профиль набора — сериализован в АССЕТЕ набора (шарится через VCS)
            var profiles = IconGenProfile.All();
            var profNames = new List<string>();
            foreach (var p in profiles) profNames.Add(p.name);
            string savedProf = col.SourceAsset != null && col.SourceAsset.profile != null
                ? col.SourceAsset.profile.name
                : (profNames.Count > 0 ? profNames[0] : "");

            var profDesc = new Label();
            profDesc.AddToClassList("profile-desc");

            var prof = new DropdownField("Профиль", profNames,
                profNames.Contains(savedProf) ? profNames.IndexOf(savedProf) : 0);
            prof.RegisterValueChangedCallback(e =>
            {
                if (col.SourceAsset != null)
                {
                    foreach (var p in profiles)
                        if (p.name == e.newValue)
                        {
                            Undo.RecordObject(col.SourceAsset, "Set collection profile");
                            col.SourceAsset.profile = p;
                            EditorUtility.SetDirty(col.SourceAsset);
                            AssetDatabase.SaveAssetIfDirty(col.SourceAsset);
                        }
                }
                ApplyCollectionProfile(col);
                profDesc.text = ProfileDescription(e.newValue);
                RebuildModelRows();
            });
            _leftPanel.Add(prof);
            profDesc.text = ProfileDescription(savedProf);
            _leftPanel.Add(profDesc);

            ApplyCollectionProfile(col);

            // Модель/LoRA (списки с сервера)
            _modelRowsHost = new VisualElement();
            _modelRowsHost.AddToClassList("panel-section");
            _leftPanel.Add(_modelRowsHost);
            RebuildModelRows();

            // Референсы стиля
            _refsHost = new VisualElement();
            _refsHost.AddToClassList("panel-section");
            _leftPanel.Add(_refsHost);
            RebuildRefs();

            // Хранение — из контракта (ассет либо PreferredStorageSize провайдера)
            bool fromContract = col.SourceAsset != null
                && (col.SourceAsset.storageSize.x <= 0 || col.SourceAsset.storageSize.y <= 0);
            _leftPanel.Add(new Label(
                $"Хранение: {col.StorageSize.x}×{col.StorageSize.y}" +
                (fromContract ? " (контракт провайдера)" : "") +
                $", рендер: {col.RenderSize.x}×{col.RenderSize.y}") { pickingMode = PickingMode.Ignore });
            _leftPanel.Children();
            _leftPanel[_leftPanel.childCount - 1].AddToClassList("hint");
        }

        private static string ProfileDescription(string profileName)
        {
            foreach (var p in IconGenProfile.All())
                if (p.name == profileName) return p.description ?? "";
            return "";
        }

        /// <summary>Применить профиль набора (из ассета) к глобальным настройкам генерации.</summary>
        private static void ApplyCollectionProfile(IconCollection col)
            => col?.SourceAsset?.profile?.Apply();

        private VisualElement _modelRowsHost;

        private void RebuildModelRows()
        {
            if (_modelRowsHost == null) return;
            _modelRowsHost.Clear();

            // Статус ComfyUI + запуск сервера
            AddComfyStatusRow();
            // Шаблон позитива + негатив стиля активного профиля — правятся прямо из студии
            AddStylePromptRows();
            // Пиксельная сетка стиля активного профиля — серверу не нужна, показываем всегда
            AddPixelGridRow();

            if (_serverCheckpoints == null)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(new Label(_modelsLoading
                    ? "Модели: загружаю список с ComfyUI…"
                    : $"Модель: {AiToolsSettings.Checkpoint} (сервер офлайн)"));
                row[0].AddToClassList("hint");
                if (!_modelsLoading)
                    row.Add(new Button(FetchModelLists) { text = "⟳" });
                _modelRowsHost.Add(row);
                return;
            }

            var ckpts = new List<string>(_serverCheckpoints);
            if (!ckpts.Contains(AiToolsSettings.Checkpoint)) ckpts.Insert(0, AiToolsSettings.Checkpoint);
            var ckpt = new DropdownField("Модель", ckpts, ckpts.IndexOf(AiToolsSettings.Checkpoint));
            ckpt.RegisterValueChangedCallback(e => AiToolsSettings.Checkpoint = e.newValue);
            _modelRowsHost.Add(ckpt);

            var loras = new List<string> { "(без LoRA)" };
            loras.AddRange(_serverLoras);
            string curLora = string.IsNullOrEmpty(AiToolsSettings.LoraName) ? "(без LoRA)" : AiToolsSettings.LoraName;
            if (!loras.Contains(curLora)) loras.Insert(1, curLora);
            var lora = new DropdownField("LoRA", loras, loras.IndexOf(curLora));
            lora.RegisterValueChangedCallback(e =>
            {
                AiToolsSettings.LoraName = e.newValue == "(без LoRA)" ? "" : e.newValue;
                RebuildModelRows();   // слайдер силы показывается только при выбранной LoRA
            });
            _modelRowsHost.Add(lora);

            // Коэффициент следования LoRA (иначе крутить только в ассете профиля)
            if (!string.IsNullOrEmpty(AiToolsSettings.LoraName))
            {
                var strength = new Slider("Сила LoRA", 0f, 1.5f)
                {
                    value = AiToolsSettings.LoraStrength,
                    showInputField = true,
                    tooltip = "Насколько жёстко LoRA ведёт стиль: 0.6 — мягко, 0.9–1.0 — строго",
                };
                strength.RegisterValueChangedCallback(e => AiToolsSettings.LoraStrength = e.newValue);
                _modelRowsHost.Add(strength);
            }
        }

        /// <summary>
        /// Статус ComfyUI (зелёный — жив, красный — нет, жёлтый — проверка/старт) + кнопка
        /// запуска сервера. Опрос идёт по таймеру, индикатор обновляется по событию.
        /// </summary>
        private void AddComfyStatusRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var dot = new VisualElement();
            dot.style.width = 10; dot.style.height = 10;
            dot.style.borderTopLeftRadius = 5; dot.style.borderTopRightRadius = 5;
            dot.style.borderBottomLeftRadius = 5; dot.style.borderBottomRightRadius = 5;
            dot.style.marginRight = 6;
            row.Add(dot);

            var label = new Label();
            label.style.flexGrow = 1;
            row.Add(label);

            var launchBtn = new Button(() => ComfyServerControl.Launch())
                { text = "Запустить", tooltip = "Поднять ComfyUI (файл запуска — Project Settings ▸ AI Icon Tools)" };
            row.Add(launchBtn);
            row.Add(new Button(ComfyServerControl.CheckNow) { text = "⟳", tooltip = "Проверить сервер сейчас" });

            void Refresh()
            {
                bool on = ComfyServerControl.IsOnline;
                bool busy = ComfyServerControl.Checking || ComfyServerControl.Launching;
                dot.style.backgroundColor = busy ? new Color(0.95f, 0.75f, 0.20f)
                                          : on ? new Color(0.35f, 0.80f, 0.35f)
                                               : new Color(0.85f, 0.30f, 0.30f);
                label.text = on ? "ComfyUI работает"
                           : ComfyServerControl.Launching ? "ComfyUI запускается…"
                           : ComfyServerControl.Checking ? "Проверяю сервер…"
                                                         : "ComfyUI не запущен";
                label.tooltip = AiToolsSettings.ComfyServer;
                // запускать нечего, если уже работает или путь не задан
                launchBtn.SetEnabled(!on && ComfyServerControl.CanLaunch);
                launchBtn.tooltip = ComfyServerControl.CanLaunch
                    ? "Поднять ComfyUI"
                    : "Укажите файл запуска: Project Settings ▸ AI Icon Tools ▸ «Запуск (.bat/.exe)»";
            }

            ComfyServerControl.Changed += Refresh;
            row.RegisterCallback<DetachFromPanelEvent>(_ => ComfyServerControl.Changed -= Refresh);
            // периодический опрос, пока панель жива
            row.schedule.Execute(() => ComfyServerControl.Poll()).Every(3000);
            Refresh();
            ComfyServerControl.CheckNow();

            _modelRowsHost.Add(row);
        }

        /// <summary>
        /// Позитив-шаблон и негатив СТИЛЯ активного профиля — правятся прямо из студии
        /// (пишутся в ассет стиля). Промпт сущности ниже — это только {subject}; шаблон
        /// оборачивает его, а негатив общий для стиля. Раньше правились только в Inspector.
        /// </summary>
        private void AddStylePromptRows()
        {
            var style = ActiveCollection()?.SourceAsset?.profile?.style;
            if (style == null) return;

            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var hint = new Label("Промпт/негатив стиля"); hint.AddToClassList("hint"); hint.style.flexGrow = 1;
            headerRow.Add(hint);
            headerRow.Add(new Button(() => StylePromptWindow.Open(style))
                { text = "⤢ развернуть", tooltip = "Открыть окно с большими полями для правки шаблона и негатива" });
            _modelRowsHost.Add(headerRow);

            var pos = new TextField("Шаблон стиля") { value = style.positiveTemplate, multiline = true };
            pos.style.whiteSpace = WhiteSpace.Normal;
            pos.tooltip = "Позитив-шаблон стиля. {subject} заменяется промптом сущности (поле «Промпт» ниже). " +
                          $"Пишется в стиль «{style.name}». Кнопка «развернуть» — крупнее.";
            pos.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(style, "Style positive");
                style.positiveTemplate = e.newValue;
                EditorUtility.SetDirty(style); AssetDatabase.SaveAssetIfDirty(style);
            });
            _modelRowsHost.Add(pos);

            var neg = new TextField("Негатив стиля") { value = style.negative, multiline = true };
            neg.style.whiteSpace = WhiteSpace.Normal;
            neg.tooltip = "Что модели НЕ рисовать (общий для стиля): помогает убрать градиент, освещение, " +
                          "мусор. Применяется и к обычной генерации, и к стилизации правок.";
            neg.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(style, "Style negative");
                style.negative = e.newValue;
                EditorUtility.SetDirty(style); AssetDatabase.SaveAssetIfDirty(style);
            });
            _modelRowsHost.Add(neg);
        }

        /// <summary>
        /// Квантование в пиксельную сетку — настройка СТИЛЯ активного профиля, правится
        /// прямо из студии (пишется в ассет стиля, шарится через VCS, попадает в рецепты).
        /// </summary>
        private void AddPixelGridRow()
        {
            var style = ActiveCollection()?.SourceAsset?.profile?.style;
            if (style == null) return;

            var choices = new List<string> { "выкл", "16", "24", "32", "48", "64", "96", "128" };
            string cur = style.pixelGrid <= 0 ? "выкл" : style.pixelGrid.ToString();
            if (!choices.Contains(cur)) choices.Insert(1, cur);

            var grid = new DropdownField("Пиксельная сетка", choices, choices.IndexOf(cur))
            {
                tooltip = "Итоговое разрешение пиксель-арта: результат квантуется в честную " +
                          $"сетку N px (жёсткие пиксели и альфа). Пишется в стиль «{style.name}».",
            };
            grid.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(style, "Set pixel grid");
                style.pixelGrid = e.newValue == "выкл" ? 0 : int.Parse(e.newValue);
                EditorUtility.SetDirty(style);
                AssetDatabase.SaveAssetIfDirty(style);
                RebuildModelRows();   // trim/палитра активны только при включённой сетке
            });
            _modelRowsHost.Add(grid);

            if (style.pixelGrid <= 0) return;   // постпроцесс без сетки не применяется

            var trim = new Toggle("Обрезать поля")
            {
                value = style.pixelTrim,
                tooltip = "Убрать прозрачные края перед квантованием — объект на весь кадр, " +
                          "при мелкой сетке на него приходится больше пикселей",
            };
            trim.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(style, "Set pixel trim");
                style.pixelTrim = e.newValue;
                EditorUtility.SetDirty(style);
                AssetDatabase.SaveAssetIfDirty(style);
            });
            _modelRowsHost.Add(trim);

            var colorChoices = new List<string> { "без ограничения", "6", "8", "12", "16", "24", "32" };
            string curCol = style.pixelColors <= 0 ? "без ограничения" : style.pixelColors.ToString();
            if (!colorChoices.Contains(curCol)) colorChoices.Insert(1, curCol);
            var colors = new DropdownField("Палитра, цветов", colorChoices, colorChoices.IndexOf(curCol))
            {
                tooltip = "Свести к N цветам (median-cut): «честный» пиксель-look, чёткие плашки " +
                          "в мелком размере. Обычно 8–16 на иконку.",
            };
            colors.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(style, "Set pixel colors");
                style.pixelColors = e.newValue == "без ограничения" ? 0 : int.Parse(e.newValue);
                EditorUtility.SetDirty(style);
                AssetDatabase.SaveAssetIfDirty(style);
            });
            _modelRowsHost.Add(colors);
        }

        private async void FetchModelLists()
        {
            _modelsLoading = true;
            RebuildModelRows();
            try
            {
                if (await ComfyUIClient.IsOnlineAsync())
                {
                    _serverCheckpoints = await ComfyUIClient.GetCheckpointsAsync();
                    _serverLoras = await ComfyUIClient.GetLorasAsync();
                }
            }
            catch { /* офлайн — покажем текущее значение */ }
            _modelsLoading = false;
            RebuildModelRows();
        }

        // ── Референсы стиля ──

        private VisualElement _refsHost;

        private void RebuildRefs()
        {
            if (_refsHost == null) return;
            _refsHost.Clear();

            var paths = AiToolsSettings.StyleRefAssetPaths;

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            header.Add(new Label($"Референсы стиля ({paths.Count})")
                { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } });
            header.Add(new Button(PasteStyleRef) { text = "+ буфер", tooltip = "Добавить эталон из буфера обмена" });
            var clear = new Button(() =>
            {
                AiToolsSettings.StyleRefAssetPaths = new List<string>();
                RebuildRefs();
            }) { text = "Сброс", tooltip = "Очистить список (файлы не удаляются)" };
            clear.SetEnabled(paths.Count > 0);
            header.Add(clear);
            _refsHost.Add(header);

            for (int i = 0; i < paths.Count; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.AddToClassList("ref-row");
                var field = new ObjectField { objectType = typeof(Sprite), allowSceneObjects = false };
                field.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]));
                field.RegisterValueChangedCallback(e =>
                {
                    var list = AiToolsSettings.StyleRefAssetPaths;
                    if (e.newValue == null) list.RemoveAt(index);
                    else list[index] = AssetDatabase.GetAssetPath(e.newValue);
                    AiToolsSettings.StyleRefAssetPaths = list;
                    RebuildRefs();
                });
                row.Add(field);
                row.Add(new Button(() =>
                {
                    var list = AiToolsSettings.StyleRefAssetPaths;
                    list.RemoveAt(index);
                    AiToolsSettings.StyleRefAssetPaths = list;
                    RebuildRefs();
                }) { text = "×", tooltip = "Убрать из списка" });
                _refsHost.Add(row);
            }

            if (paths.Count > 0)
            {
                var weight = new Slider("Сила", 0f, 1f) { value = AiToolsSettings.StyleRefWeight };
                weight.RegisterValueChangedCallback(e => AiToolsSettings.StyleRefWeight = e.newValue);
                _refsHost.Add(weight);

                var modes = new List<string> { "style transfer", "standard" };
                var mode = new DropdownField("Режим", modes,
                    Mathf.Max(0, modes.IndexOf(AiToolsSettings.StyleRefMode)));
                mode.tooltip = "style transfer — стиль без композиции (чище); standard — тянет и композицию";
                mode.RegisterValueChangedCallback(e => AiToolsSettings.StyleRefMode = e.newValue);
                _refsHost.Add(mode);

                if (paths.Count == 1)
                {
                    var hint = new Label("Один эталон тянет и композицию — добавьте 2–4.");
                    hint.AddToClassList("hint");
                    _refsHost.Add(hint);
                }
            }
        }

        private void PasteStyleRef()
        {
            string folder = IconVariantStore.StyleRefsFolder;
            string path = $"{folder}/StyleRef_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}.png";
            var sprite = ClipboardImageImporter.PasteAsSprite(path);
            if (sprite == null) return;
            var paths = AiToolsSettings.StyleRefAssetPaths;
            paths.Add(path);
            AiToolsSettings.StyleRefAssetPaths = paths;
            RebuildRefs();
        }

        // ── Сетка ──

        // Пер-набор режим «1:1 как в игре»: плитки в нативном размере показа (DisplaySize)
        private static bool NativeTiles(string colId)
            => EditorPrefs.GetBool("ProtoIcon.NativeTiles." + colId, false);
        private static void SetNativeTiles(string colId, bool value)
            => EditorPrefs.SetBool("ProtoIcon.NativeTiles." + colId, value);

        private void RebuildGrid()
        {
            if (_grid == null) return;
            _grid.Clear();

            var cols = ActiveCollections();
            bool sections = cols.Count > 1;

            foreach (var col in cols)
            {
                var items = col.GetItems();
                _grid.Add(MakeSectionHeader(col, items, showTitle: sections));

                bool native = NativeTiles(col.Id);
                var sizes = col.GetRenditionSizes?.Invoke();
                if (sizes != null && sizes.Count > 0)
                {
                    // Набор с рендишенами: под каждой сущностью — колонка её размеров
                    var desc = new List<int>(sizes);
                    desc.Sort((a, b) => b - a);   // крупные сверху
                    foreach (var item in items)
                        _grid.Add(MakeRenditionColumn(item, col, desc, native));
                }
                else
                {
                    foreach (var item in items)
                        _grid.Add(MakeTile(item, col, native));
                }
            }
        }

        /// <summary>
        /// Шапка набора в гриде: название (в группах), размеры контракта (одинаковы для
        /// всего набора) и тумблер «1:1 как в игре» — плитки в нативном размере показа.
        /// </summary>
        private VisualElement MakeSectionHeader(IconCollection col, List<IconItem> items, bool showTitle)
        {
            var row = new VisualElement();
            row.AddToClassList("section-header");
            row.style.width = Length.Percent(100);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            if (showTitle)
                row.Add(new Label(col.Title));

            string disp = DisplayRange(items);
            var info = new Label(
                $"хранение {col.StorageSize.x}×{col.StorageSize.y}" +
                (disp.Length > 0 ? $" · в игре {disp}" : "") +
                (string.IsNullOrEmpty(col.UsageNote) ? "" : $" · {col.UsageNote}"));
            info.AddToClassList("hint");
            info.style.marginLeft = showTitle ? 10 : 0;
            row.Add(info);

            row.Add(new VisualElement { style = { flexGrow = 1 } });

            // «Все ×N» для этой секции — сгенерить все её сущности сразу, а не по одной
            var genAll = new Button(() => EnqueueAll(col, onlyMissing: false))
            {
                text = $"Все ×{BatchCount}",
                tooltip = $"Сгенерировать варианты для всех иконок набора «{col.Title}»",
            };
            genAll.SetEnabled(items != null && items.Count > 0);
            row.Add(genAll);

            // Атлас-пайплайн этого набора: промпт → буфер / нарезать / нарезать + вырез фона
            var atlasCol = col;
            var atlas = new Button { text = "Атлас ▾", tooltip = "Спрайт-лист из буфера обмена" };
            atlas.clicked += () =>
            {
                var m = new GenericMenu();
                m.AddItem(new GUIContent("Промпт спрайт-листа → буфер"), false,
                    () => AtlasTools.CopyAtlasPrompt(atlasCol));
                m.AddItem(new GUIContent("Нарезать из буфера…"), false,
                    () => AtlasTools.OpenSliceWindow(atlasCol));
                m.AddItem(new GUIContent("Пиксель-арт из буфера…"), false,
                    () => PixelArtSliceWindow.Open(atlasCol));
                m.ShowAsContext();
            };
            atlas.SetEnabled(items != null && items.Count > 0);
            row.Add(atlas);

            var native = new Toggle("1:1 как в игре")
            {
                value = NativeTiles(col.Id),
                tooltip = "Плитки в нативном размере показа — видно, как иконка читается в игре",
            };
            native.SetEnabled(disp.Length > 0);   // провайдер не задал DisplaySize — не во что жать
            string colId = col.Id;
            native.RegisterValueChangedCallback(e => { SetNativeTiles(colId, e.newValue); RebuildGrid(); });
            row.Add(native);

            return row;
        }

        /// <summary>Диапазон размеров показа сущностей набора: "40×40" или "26×26–40×40".</summary>
        private static string DisplayRange(List<IconItem> items)
        {
            int minW = int.MaxValue, minH = int.MaxValue, maxW = 0, maxH = 0;
            if (items != null)
                foreach (var it in items)
                {
                    if (it == null || it.DisplaySize.x <= 0 || it.DisplaySize.y <= 0) continue;
                    minW = Mathf.Min(minW, it.DisplaySize.x); maxW = Mathf.Max(maxW, it.DisplaySize.x);
                    minH = Mathf.Min(minH, it.DisplaySize.y); maxH = Mathf.Max(maxH, it.DisplaySize.y);
                }
            if (maxW == 0) return "";
            return minW == maxW && minH == maxH
                ? $"{maxW}×{maxH}" : $"{minW}×{minH}–{maxW}×{maxH}";
        }

        private VisualElement MakeTile(IconItem item, IconCollection col, bool native)
        {
            var tile = new VisualElement();
            tile.AddToClassList("tile");
            if (item.EntityId == _selectedEntityId) tile.AddToClassList("tile--selected");
            if (!string.IsNullOrEmpty(item.UsageNote))
                tile.tooltip = "Используется: " + item.UsageNote;

            int h, w;
            if (native && item.DisplaySize.x > 0 && item.DisplaySize.y > 0)
            {
                w = item.DisplaySize.x;
                h = item.DisplaySize.y;
            }
            else
            {
                float aspect = col.StorageSize.y > 0 ? col.StorageSize.x / (float)col.StorageSize.y : 1f;
                h = TileSize;
                w = Mathf.RoundToInt(TileSize * Mathf.Clamp(aspect, 0.5f, 2.5f));
            }

            var history = IconVariantStore.Library.Find(item.EntityId);
            int vcount = history?.variants?.Count ?? 0;

            var top = new VisualElement();
            top.AddToClassList("tile__topbar");
            // В нативном режиме плитка может быть 26px — бейджу и «+N» нужно место
            top.style.width = native ? Mathf.Max(w, 64) : w;
            var badge = new Label(vcount > 0 ? vcount + " вар." : "—");
            badge.AddToClassList("tile__badge");
            top.Add(badge);
            var gen = new Button(() => EnqueueBatch(item, col))
                { text = "+" + BatchCount, tooltip = $"Сгенерировать ×{BatchCount} вариантов" };
            gen.AddToClassList("tile__gen");
            gen.SetEnabled(!string.IsNullOrWhiteSpace(item.GetPrompt?.Invoke()));
            top.Add(gen);
            tile.Add(top);

            var img = new Image { scaleMode = ScaleMode.ScaleToFit, sprite = item.GetSprite?.Invoke() };
            img.AddToClassList("tile__image");
            img.style.width = w;
            img.style.height = h;
            img.style.backgroundColor = TileBg;
            tile.Add(img);

            if (ShowNames)
            {
                var name = new Label(item.DisplayName);
                name.AddToClassList("tile__name");
                tile.Add(name);
            }

            var accent = new VisualElement
            {
                style =
                {
                    height = 2, width = w, marginTop = 1,
                    backgroundColor = item.Accent,
                }
            };
            tile.Add(accent);

            tile.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;   // «+N» не должен открывать пикер
                _selectedEntityId = _selectedEntityId == item.EntityId ? null : item.EntityId;
                RebuildGrid();
                RebuildPicker();
            });

            return tile;
        }

        /// <summary>
        /// Колонка сущности с рендишенами: заголовок (имя + счётчик + «+N») и плитки
        /// размеров сверху вниз (крупные первыми). native — каждый размер в реальных px.
        /// </summary>
        private VisualElement MakeRenditionColumn(IconItem item, IconCollection col,
                                                  List<int> sizesDesc, bool native)
        {
            var column = new VisualElement
                { style = { flexDirection = FlexDirection.Column, alignItems = Align.Center,
                            marginRight = 10, marginBottom = 8 } };

            var history = IconVariantStore.Library.Find(item.EntityId);
            int vcount = history?.variants?.Count ?? 0;

            var head = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var badge = new Label(vcount > 0 ? vcount + " вар." : "—");
            badge.AddToClassList("tile__badge");
            head.Add(badge);
            var gen = new Button(() => EnqueueBatch(item, col))
                { text = "+" + BatchCount, tooltip = $"Сгенерировать ×{BatchCount} вариантов" };
            gen.AddToClassList("tile__gen");
            gen.SetEnabled(!string.IsNullOrWhiteSpace(item.GetPrompt?.Invoke()));
            head.Add(gen);
            column.Add(head);

            foreach (int size in sizesDesc)
                column.Add(MakeRenditionTile(item, col, size, native));

            var name = new Label(item.DisplayName);
            name.AddToClassList("tile__name");
            if (!string.IsNullOrEmpty(item.UsageNote)) name.tooltip = "Используется: " + item.UsageNote;
            column.Add(name);

            var accent = new VisualElement
                { style = { height = 2, width = 64, marginTop = 1, backgroundColor = item.Accent } };
            column.Add(accent);

            return column;
        }

        /// <summary>Одна плитка размера сущности: спрайт рендишена (или пусто) + бейдж «Npx».</summary>
        private VisualElement MakeRenditionTile(IconItem item, IconCollection col, int size, bool native)
        {
            var tile = new VisualElement();
            tile.AddToClassList("tile");
            bool selected = item.EntityId == _selectedEntityId && size == EffectiveEditSize(col);
            if (selected) tile.AddToClassList("tile--selected");

            int px = native ? Mathf.Clamp(size, 8, 192) : TileSize;

            var badge = new Label($"{size}px") { style = { fontSize = 10 } };
            badge.AddToClassList("tile__badge");
            badge.style.width = Mathf.Max(px, 40);
            tile.Add(badge);

            var sprite = item.GetRenditionSprite?.Invoke(size);
            var img = new Image { scaleMode = ScaleMode.ScaleToFit, sprite = sprite };
            img.AddToClassList("tile__image");
            img.style.width = px;
            img.style.height = px;
            img.style.backgroundColor = sprite == null ? new Color(0, 0, 0, 0.35f) : TileBg;
            tile.Add(img);

            tile.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;
                _selectedEntityId = item.EntityId;
                _editRenditionSize = size;
                RebuildGrid();
                RebuildPicker();
            });

            return tile;
        }

        /// <summary>Редактируемый размер: _editRenditionSize, иначе мастер (крупнейший) набора.</summary>
        private int EffectiveEditSize(IconCollection col)
        {
            if (_editRenditionSize > 0) return _editRenditionSize;
            return MasterSize(col);
        }

        private static int MasterSize(IconCollection col)
        {
            var sizes = col?.GetRenditionSizes?.Invoke();
            int max = 0;
            if (sizes != null) foreach (var s in sizes) if (s > max) max = s;
            return max;
        }

        // ── Пикер ──

        /// <summary>
        /// Вставить картинку из буфера обмена как вариант сущности: не генерация — ручной
        /// вариант из любого внешнего источника. Становится активным, как сгенерированный:
        /// раньше AddVariant ставил ему метку «активный» в истории, а спрайт в игру не шёл
        /// (только по клику) — студия показывала вставленное, игра его не видела (10.10.2026).
        /// </summary>
        private void PasteVariantFromClipboard(IconItem item, IconCollection col)
        {
            string variantId = IconVariantStore.NewVariantId();
            string path = IconVariantStore.VariantPngPath(col.PngFolder, item.EntityId, variantId);

            var sprite = ClipboardImageImporter.PasteAsSprite(path, new ClipboardImageImporter.ImportOptions
            {
                maxSize = Mathf.Max(col.StorageSize.x, col.StorageSize.y),
                spriteBorder = item.SliceBorder,
            });
            if (sprite == null)
            {
                EditorUtility.DisplayDialog("Буфер", "В буфере обмена нет картинки.", "Ок");
                return;
            }

            var result = new IconGenResult
            {
                Subject = item.GetPrompt?.Invoke() ?? "",
                StyleName = "(вставлено из буфера)",
                PngAssetPath = path,
                GeneratedAtUtc = System.DateTime.UtcNow.ToString("o"),
            };
            if (item.UndoTarget != null) Undo.RecordObject(item.UndoTarget, "Paste variant");
            IconVariantStore.AddVariant(item.EntityId, variantId, sprite, result);
            // AddVariant делает вариант активным в истории — значит, и в игру он идёт сразу
            item.SetSprite?.Invoke(sprite);
            if (item.IsUiElement) IconVariantStore.PublishUiElement(item);
            RebuildGrid();
            RebuildPicker();
        }

        private void RebuildPicker()
        {
            if (_pickerHost == null) return;

            // Захватить прокрутку ленты ДО пересборки (принадлежит прошлой сущности —
            // _laneScrollEntity): выбор варианта не должен сбрасывать позицию
            var oldLane = _pickerHost.Q<ScrollView>("variants-lane");
            if (oldLane != null && !string.IsNullOrEmpty(_laneScrollEntity))
                _laneScroll = oldLane.scrollOffset;

            _pickerHost.Clear();
            if (string.IsNullOrEmpty(_selectedEntityId))
            {
                _laneScrollEntity = null;
                _vSplit?.CollapseChild(1);   // пикер закрыт — грид на всю высоту
                return;
            }
            _vSplit?.UnCollapse();

            // Найти элемент и его набор
            IconItem item = null;
            IconCollection col = null;
            foreach (var c in AllConfigCollections())
            {
                foreach (var it in c.GetItems())
                    if (it.EntityId == _selectedEntityId) { item = it; col = c; break; }
                if (item != null) break;
            }
            if (item == null)
            {
                _selectedEntityId = null;
                _vSplit?.CollapseChild(1);
                return;
            }

            var titleRow = new VisualElement();
            titleRow.AddToClassList("picker__row");
            string sizeNote = item.DisplaySize.x > 0
                ? $"  ·  в игре {item.DisplaySize.x}×{item.DisplaySize.y}" : "";
            var title = new Label($"{item.DisplayName} — {col.Title}{sizeNote}");
            title.AddToClassList("picker__title");
            title.style.flexGrow = 1;
            titleRow.Add(title);

            // Управление лентой: плиткой/в линию + размер ячеек (просмотр — слайдер уместен)
            var wrapToggle = new ToolbarToggle { text = "Плиткой", value = VariantsWrap,
                tooltip = "Варианты с переносом на строки вместо одной линии" };
            wrapToggle.RegisterValueChangedCallback(e => { VariantsWrap = e.newValue; RebuildPicker(); });
            titleRow.Add(wrapToggle);

            var cellSize = new SliderInt(48, 192) { value = VariantCellSize, style = { width = 100 },
                tooltip = "Размер ячеек вариантов" };
            cellSize.RegisterValueChangedCallback(e => { VariantCellSize = e.newValue; RebuildPicker(); });
            titleRow.Add(cellSize);

            titleRow.Add(new Button(() => EnqueueBatch(item, col))
                { text = $"+{BatchCount} ещё", tooltip = "Пачка вариантов в общую очередь" });
            titleRow.Add(new Button(() => PasteVariantFromClipboard(item, col))
                { text = "＋ буфер", tooltip = "Вставить картинку из буфера обмена как активный вариант " +
                                              "(без генерации — из любого внешнего источника; сразу идёт в игру)" });
            titleRow.Add(new Button(() => { _selectedEntityId = null; RebuildGrid(); RebuildPicker(); })
                { text = "✕" });
            _pickerHost.Add(titleRow);

            // Назначение: одна иконка может жить в нескольких окнах — тут это видно
            if (!string.IsNullOrEmpty(item.UsageNote))
            {
                var usage = new Label("Используется: " + item.UsageNote);
                usage.AddToClassList("hint");
                usage.style.whiteSpace = WhiteSpace.Normal;
                _pickerHost.Add(usage);
            }

            // Лента вариантов: прокрутка переживает пересборку (выбор варианта её не сбрасывает)
            var history = IconVariantStore.Library.Find(item.EntityId);
            int cs = VariantCellSize;
            float cellAspect = col.StorageSize.y > 0
                ? Mathf.Clamp(col.StorageSize.x / (float)col.StorageSize.y, 0.5f, 2.5f) : 1f;

            var lane = new ScrollView(VariantsWrap ? ScrollViewMode.Vertical : ScrollViewMode.Horizontal)
                { name = "variants-lane" };
            lane.AddToClassList("variants-lane");
            if (VariantsWrap)
            {
                lane.contentContainer.style.flexDirection = FlexDirection.Row;
                lane.contentContainer.style.flexWrap = Wrap.Wrap;
                lane.style.height = StyleKeyword.Auto;
                lane.style.maxHeight = Mathf.Min(3 * (cs + 8) + 16, 340);
            }
            else
            {
                lane.style.height = cs + 20;
            }

            // Смена сущности — выделение прошлой ленты и редактируемый размер не переносятся
            if (_laneScrollEntity != item.EntityId)
            {
                _pickedVariantIds.Clear();
                _pickAnchorId = null;
                _editRenditionSize = 0;   // → мастер новой сущности
            }
            _laneCells.Clear();

            if (history != null)
                foreach (var v in history.variants)
                {
                    if (v == null) continue;
                    var cell = MakeVariantCell(item, col, history, v);
                    cell.style.width = Mathf.RoundToInt(cs * cellAspect);
                    cell.style.height = cs;
                    if (VariantsWrap) cell.style.marginBottom = 6;
                    lane.Add(cell);
                }
            else
                lane.Add(new Label("Вариантов пока нет — нажмите «+N ещё»."));

            // Восстановить прокрутку после лейаута — только если это та же сущность
            if (_laneScrollEntity == item.EntityId)
            {
                var saved = _laneScroll;
                lane.RegisterCallback<GeometryChangedEvent>(_ => lane.scrollOffset = saved);
            }
            _laneScrollEntity = item.EntityId;

            // Delete по выделенному — как в любом файловом менеджере. Shift+Delete
            // сносит вариант целиком, обычный Delete — только картинку: рецепт дороже
            // диска, и потерять его случайным нажатием нельзя
            lane.focusable = true;
            lane.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace) return;
                if (_pickedVariantIds.Count == 0) return;

                var ids = new List<string>(_pickedVariantIds);
                if (evt.shiftKey) DeletePicked(item);
                else DeletePickedImages(item, ids, ids.Count);

                evt.StopPropagation();
                Repaint();
            });

            _pickerHost.Add(lane);

            // Рендишены — если набор их поддерживает (ресурсы, специалисты, эффекты).
            // Размеры общие для набора; ПКМ по варианту → «Рендишен → Npx» тоже назначает.
            if (col.GetRenditionSizes != null)
                _pickerHost.Add(BuildRenditionsPanel(item, col));

            // Примерка активного варианта. UI-элемент (панель/рамка) — растяжимое 9-slice
            // превью: видно, как рамка держится при любом размере. Иконка — виджет «как в игре».
            var activeSprite = history?.Active?.sprite;
            if (activeSprite == null) activeSprite = item.GetSprite?.Invoke();
            if (activeSprite != null)
            {
                if (item.IsUiElement)
                {
                    _pickerHost.Add(BuildStretchPreview(item, col, activeSprite));
                }
                else if (col.SourceAsset != null)
                {
                    var widget = col.SourceAsset.BuildWidgetPreview(activeSprite, item);
                    if (widget != null)
                    {
                        var wpHeader = new Label("Как в игре — активный вариант в виджете " +
                                                 "(реальные стили игры):");
                        wpHeader.AddToClassList("hint");
                        wpHeader.style.marginTop = 6;
                        _pickerHost.Add(wpHeader);
                        _pickerHost.Add(widget);
                    }
                }
            }

            // Промпт сущности (write-through в конфиг)
            var prompt = new TextField("Промпт") { value = item.GetPrompt?.Invoke() ?? "", multiline = true };
            prompt.AddToClassList("prompt-field");
            prompt.RegisterValueChangedCallback(e => item.SetPrompt?.Invoke(e.newValue));
            _pickerHost.Add(prompt);

            // Правка + стилизация
            var row = new VisualElement();
            row.AddToClassList("picker__row");

            var tweak = new TextField("Правка") { value = _promptTweak };
            tweak.tooltip = "Добавляется к промпту в пачках и стилизации (по-английски): " +
                            "например «brighter green», «exactly three bullets»";
            tweak.style.flexGrow = 1;
            tweak.RegisterValueChangedCallback(e => _promptTweak = e.newValue);
            row.Add(tweak);
            _pickerHost.Add(row);

            var styRow = new VisualElement();
            styRow.AddToClassList("picker__row");

            var profiles = IconGenProfile.All();
            var profNames = new List<string> { "(текущий профиль)" };
            foreach (var p in profiles) profNames.Add(p.name);
            var styProf = new DropdownField("Стилизовать в", profNames, 0) { style = { flexGrow = 1 } };
            styRow.Add(styProf);

            var denoise = new Slider(0.3f, 0.85f) { value = StylizeDenoise, style = { width = 120 } };
            denoise.tooltip = "Перерисовка: 0.4 — доводка, 0.6 — смена стиля, 0.8 — почти с нуля";
            denoise.RegisterValueChangedCallback(e => StylizeDenoise = e.newValue);
            styRow.Add(denoise);

            var active = history?.Active;
            var stylize = new Button(() =>
            {
                IconGenProfile target = null;
                foreach (var p in profiles)
                    if (p.name == styProf.value) target = p;
                EnqueueStylize(item, col, history.Active, target);
            }) { text = "Стилизовать", tooltip = "img2img активного варианта: композиция сохраняется" };
            stylize.SetEnabled(active?.sprite != null && !string.IsNullOrEmpty(active.pngAssetPath));
            styRow.Add(stylize);

            _pickerHost.Add(styRow);
        }

        private VisualElement MakeVariantCell(IconItem item, IconCollection col,
                                              EntityIconHistory history, IconVariant v)
        {
            var cell = new VisualElement();
            cell.AddToClassList("variant-cell");
            cell.style.backgroundColor = TileBg;
            if (v.id == history.activeVariantId) cell.AddToClassList("variant-cell--active");
            if (v.bad) cell.AddToClassList("variant-cell--bad");
            if (_pickedVariantIds.Contains(v.id)) cell.AddToClassList("variant-cell--picked");
            _laneCells[v.id] = cell;

            if (v.sprite != null)
            {
                var img = new Image { sprite = v.sprite, scaleMode = ScaleMode.ScaleToFit };
                img.style.flexGrow = 1;
                cell.Add(img);
            }
            else
            {
                // «нет картинки» и когда PNG снесли руками мимо студии: иначе ячейка
                // висела с многоточием, будто вариант ещё генерится
                var del = new Label(ImageMissing(v) ? "нет\nкартинки" : "…");
                del.AddToClassList("variant-cell__deleted");
                cell.Add(del);
            }

            cell.tooltip = "Двойной клик — сделать активным. Клик — выделить, Ctrl/Shift — несколько.\n" +
                "Delete — удалить картинки (рецепты живы), Shift+Delete — варианты целиком." +
                (v.recipe != null
                    ? $"\n\nseed {v.recipe.seed}\n{v.recipe.checkpoint}\n{v.recipe.styleName}" +
                      (string.IsNullOrEmpty(v.recipe.runId) ? "" : $"\nпрогон {v.recipe.runId}")
                    : "");

            // Множественное выделение: Ctrl — переключить, Shift — диапазон от якоря,
            // одиночный клик — только ВЫДЕЛИТЬ (не менять картинку — есть групповые операции),
            // ДВОЙНОЙ клик — сделать активным/назначить в размер.
            // Фокус на ячейке — чтобы Delete долетал до обработчика ленты (события
            // всплывают от сфокусированного элемента вверх)
            cell.focusable = true;

            cell.RegisterCallback<ClickEvent>(evt =>
            {
                cell.Focus();
                if (evt.ctrlKey || evt.commandKey)
                {
                    if (!_pickedVariantIds.Remove(v.id)) _pickedVariantIds.Add(v.id);
                    _pickAnchorId = v.id;
                    RefreshLanePickVisuals();
                    return;
                }
                if (evt.shiftKey)
                {
                    PickRange(history, _pickAnchorId, v.id);
                    RefreshLanePickVisuals();
                    return;
                }
                _pickedVariantIds.Clear();
                _pickedVariantIds.Add(v.id);
                _pickAnchorId = v.id;
                RefreshLanePickVisuals();
                if (evt.clickCount >= 2)   // двойной — активировать, одиночный — только выделить
                    AssignFromLaneClick(item, col, history, v);
            });

            cell.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                // Группа: клик по ячейке, входящей в множественное выделение
                if (_pickedVariantIds.Count > 1 && _pickedVariantIds.Contains(v.id))
                {
                    int n = _pickedVariantIds.Count;
                    evt.menu.AppendAction($"Пометить неудачными ({n})",
                        _ => MarkPicked(item, bad: true));
                    evt.menu.AppendAction($"Снять пометку «неудачный» ({n})",
                        _ => MarkPicked(item, bad: false));
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction($"Удалить картинки ({n}) — рецепты оставить",
                        _ => DeletePickedImages(item, new List<string>(_pickedVariantIds), n));
                    evt.menu.AppendAction($"Удалить выбранные ({n}) — картинки и рецепты",
                        _ => DeletePicked(item));
                    evt.menu.AppendSeparator();
                }

                evt.menu.AppendAction("Сделать активным",
                    _ => SelectVariant(item, history, v),
                    v.sprite != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                // Найти PNG варианта в проекте руками тяжело — выделяем его в Project
                evt.menu.AppendAction("Показать PNG в проекте",
                    _ =>
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<Sprite>(v.pngAssetPath) as Object
                                  ?? AssetDatabase.LoadAssetAtPath<Texture2D>(v.pngAssetPath);
                        if (obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
                    },
                    !string.IsNullOrEmpty(v.pngAssetPath) ? DropdownMenuAction.Status.Normal
                                                          : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction(v.bad ? "Снять пометку «неудачный»" : "Пометить неудачным",
                    _ => IconVariantStore.MarkSetBad(
                        new List<(string, string)> { (item.EntityId, v.id) }, !v.bad, item.DisplayName));
                // Доступно и когда PNG снесли руками в Project: флага imageDeleted тогда
                // нет, а картинки уже нет — рецепт по-прежнему её воспроизводит
                evt.menu.AppendAction("Восстановить картинку по рецепту",
                    _ => RestoreVariant(item, col, v),
                    ImageMissing(v) && v.recipe != null
                        ? DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Disabled);

                // Назначить этот вариант в размер набора: пункты по размерам + «свой…».
                var rsizes = col.GetRenditionSizes?.Invoke();
                if (item.SetRendition != null && v.sprite != null && rsizes != null)
                {
                    evt.menu.AppendSeparator();
                    var desc = new List<int>(rsizes);
                    desc.Sort((a, b) => b - a);
                    foreach (int size in desc)
                        evt.menu.AppendAction($"Рендишен → {size}px",
                            _ => AssignRendition(item, size, v.sprite));
                    evt.menu.AppendAction("Новый размер…",
                        _ => PromptAssignRendition(item, col, v.sprite));
                }

                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Удалить картинку (рецепт оставить)",
                    _ => DeletePickedImages(item, new List<string> { v.id }, 1),
                    ImageMissing(v) ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                evt.menu.AppendAction("Удалить вариант (картинку и рецепт)", _ =>
                {
                    if (EditorUtility.DisplayDialog("Удалить вариант?",
                            $"«{item.DisplayName}», seed {v.recipe?.seed}: картинка и рецепт будут " +
                            "удалены безвозвратно.", "Удалить", "Отмена"))
                    {
                        IconVariantStore.DeleteVariants(new List<(string, string)> { (item.EntityId, v.id) });
                        var sprite = IconVariantStore.Library.ActiveSprite(item.EntityId);
                        if (sprite != null) item.SetSprite?.Invoke(sprite);
                    }
                });
            }));

            return cell;
        }

        /// <summary>Выделить диапазон вариантов от якоря до кликнутого (в порядке ленты).</summary>
        private void PickRange(EntityIconHistory history, string fromId, string toId)
        {
            if (history?.variants == null) return;
            int a = -1, b = -1;
            for (int i = 0; i < history.variants.Count; i++)
            {
                var id = history.variants[i]?.id;
                if (id == fromId) a = i;
                if (id == toId) b = i;
            }
            if (b < 0) return;
            if (a < 0) a = b;
            if (a > b) (a, b) = (b, a);

            _pickedVariantIds.Clear();
            for (int i = a; i <= b; i++)
                if (history.variants[i] != null) _pickedVariantIds.Add(history.variants[i].id);
        }

        /// <summary>Обновить синие рамки выделения без пересборки ленты.</summary>
        private void RefreshLanePickVisuals()
        {
            foreach (var kv in _laneCells)
                kv.Value.EnableInClassList("variant-cell--picked", _pickedVariantIds.Contains(kv.Key));
        }

        // ── Рендишены: размеры ОБЩИЕ для набора, спрайт — у сущности под размер ──

        /// <summary>Назначить спрайт сущности в размер (Undo + сохранение ассета).</summary>
        private void AssignRendition(IconItem item, int size, Sprite sprite)
        {
            if (item.UndoTarget != null) Undo.RecordObject(item.UndoTarget, "Set icon rendition");
            item.SetRendition?.Invoke(size, sprite);
            if (item.UndoTarget != null)
            {
                EditorUtility.SetDirty(item.UndoTarget);
                AssetDatabase.SaveAssetIfDirty(item.UndoTarget);
            }
            if (Application.isPlaying) RefreshBaseWindow();
            RebuildGrid();
            RebuildPicker();
        }

        /// <summary>Добавить размер во ВЕСЬ набор; текущий вариант сущности кладём в него.</summary>
        private void AddRenditionSize(IconItem item, IconCollection col, int size)
        {
            if (size <= 0 || col?.AddRenditionSize == null) return;
            if (item.UndoTarget != null) Undo.RecordObject(item.UndoTarget, "Add rendition size");
            col.AddRenditionSize(size);
            var sprite = item.GetSprite?.Invoke();
            if (sprite != null) item.SetRendition?.Invoke(size, sprite);
            if (item.UndoTarget != null)
            {
                EditorUtility.SetDirty(item.UndoTarget);
                AssetDatabase.SaveAssetIfDirty(item.UndoTarget);
            }
            _editRenditionSize = size;
            RebuildGrid();
            RebuildPicker();
        }

        /// <summary>Убрать размер из НАБОРА (у всех сущностей).</summary>
        private void RemoveRenditionSize(IconCollection col, int size)
        {
            if (col?.RemoveRenditionSize == null) return;
            var undoTarget = col.SourceAsset?.provider;
            if (undoTarget != null) Undo.RecordObject(undoTarget, "Remove rendition size");
            col.RemoveRenditionSize(size);
            if (undoTarget != null)
            {
                EditorUtility.SetDirty(undoTarget);
                AssetDatabase.SaveAssetIfDirty(undoTarget);
            }
            if (_editRenditionSize == size) _editRenditionSize = 0;
            RebuildGrid();
            RebuildPicker();
        }

        /// <summary>ПКМ по варианту «свой размер…»: добавить размер в набор и назначить.</summary>
        private void PromptAssignRendition(IconItem item, IconCollection col, Sprite sprite)
            => RenditionSizePrompt.Show(size =>
            {
                if (col?.AddRenditionSize != null) col.AddRenditionSize(size);
                AssignRendition(item, size, sprite);
            });

        /// <summary>
        /// Панель рендишенов: политика выбора + кликабельные чипы размеров НАБОРА
        /// (подсветка редактируемого) с превью спрайта этой сущности + «+ размер».
        /// Размеры общие для всех сущностей набора.
        /// </summary>
        private VisualElement BuildRenditionsPanel(IconItem item, IconCollection col)
        {
            var panel = new VisualElement();
            panel.style.marginTop = 6;

            int edit = EffectiveEditSize(col);
            var header = new Label($"Рендишены набора (размеры общие для всех «{col.Title}»; " +
                                   $"кликом выбираешь, какой меняешь — сейчас {edit}px):");
            header.AddToClassList("hint");
            panel.Add(header);

            // Проектные настройки рендишенов (напр., политика выбора размера у библиотеки сущностей)
            foreach (var extra in IconStudioExtensions.RenditionPanelExtras)
            {
                var element = extra(col);
                if (element != null) panel.Add(element);
            }

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            var sizes = col.GetRenditionSizes?.Invoke();
            if (sizes != null)
            {
                var desc = new List<int>(sizes);
                desc.Sort((a, b) => b - a);
                foreach (int size in desc)
                    row.Add(RenditionChip(item, col, size, edit));
            }

            // «+ размер»: произвольное число — добавляется во весь набор
            var sizeField = new IntegerField { value = 32, isDelayed = true,
                style = { width = 52 }, tooltip = "Размер рендишена, px (для всех сущностей набора)" };
            row.Add(sizeField);
            row.Add(new Button(() => AddRenditionSize(item, col, sizeField.value))
                { text = "+ размер", tooltip = "Добавить размер во весь набор" });

            panel.Add(row);
            return panel;
        }

        /// <summary>
        /// Чип размера набора: превью спрайта этой сущности под размер, метка «Npx»,
        /// крестик (убрать размер из набора). Клик выбирает размер как редактируемый
        /// (подсветка) — понятно, куда пойдёт следующий назначенный вариант.
        /// </summary>
        private VisualElement RenditionChip(IconItem item, IconCollection col, int size, int editSize)
        {
            bool selected = size == editSize;
            var border = selected ? new Color(68f / 255f, 120f / 255f, 200f / 255f)
                                   : new Color(1, 1, 1, 0.12f);
            var chip = new VisualElement
            {
                style =
                {
                    alignItems = Align.Center, marginRight = 8, marginTop = 4,
                    paddingTop = 2, paddingBottom = 2, paddingLeft = 4, paddingRight = 4,
                    borderTopWidth = selected ? 2 : 1, borderBottomWidth = selected ? 2 : 1,
                    borderLeftWidth = selected ? 2 : 1, borderRightWidth = selected ? 2 : 1,
                    borderTopColor = border, borderBottomColor = border,
                    borderLeftColor = border, borderRightColor = border,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                },
            };

            var sprite = item.GetRenditionSprite?.Invoke(size);
            var img = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit,
                style = { width = 40, height = 40 } };
            img.style.backgroundColor = sprite == null ? new Color(0, 0, 0, 0.35f) : Color.clear;
            chip.Add(img);

            var label = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            label.Add(new Label($"{size}px") { style = { fontSize = 11 } });
            label.Add(new Button(() => RemoveRenditionSize(col, size))
                { text = "✕", tooltip = "Убрать размер из набора", style = { fontSize = 9, paddingLeft = 3, paddingRight = 3 } });
            chip.Add(label);

            chip.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;   // крестик не должен менять выбор
                _editRenditionSize = size;
                RebuildGrid();
                RebuildPicker();
            });
            return chip;
        }

        private void MarkPicked(IconItem item, bool bad)
        {
            var set = new List<(string, string)>();
            foreach (var id in _pickedVariantIds) set.Add((item.EntityId, id));
            IconVariantStore.MarkSetBad(set, bad, item.DisplayName);
        }

        private void DeletePicked(IconItem item)
        {
            int n = _pickedVariantIds.Count;
            if (!EditorUtility.DisplayDialog("Удалить выбранные варианты?",
                    $"«{item.DisplayName}»: {n} вариантов будут удалены безвозвратно " +
                    "(картинки и рецепты).", "Удалить", "Отмена"))
                return;

            var set = new List<(string, string)>();
            foreach (var id in _pickedVariantIds) set.Add((item.EntityId, id));
            IconVariantStore.DeleteVariants(set);
            _pickedVariantIds.Clear();

            var sprite = IconVariantStore.Library.ActiveSprite(item.EntityId);
            if (sprite != null) item.SetSprite?.Invoke(sprite);
        }

        /// <summary>
        /// Удалить КАРТИНКИ выбранных вариантов, рецепты оставить: вариант остаётся
        /// в ленте и восстанавливается по рецепту в тот же путь. Диск освобождается,
        /// история генерации — нет.
        /// </summary>
        private void DeletePickedImages(IconItem item, IEnumerable<string> ids, int count)
        {
            if (count <= 0) return;
            if (!EditorUtility.DisplayDialog("Удалить картинки?",
                    $"«{item.DisplayName}»: у {count} вариантов будут удалены PNG. " +
                    "Рецепты останутся — картинку можно восстановить по ним.",
                    "Удалить картинки", "Отмена"))
                return;

            var set = new List<(string, string)>();
            foreach (var id in ids) set.Add((item.EntityId, id));
            int n = IconVariantStore.DeleteVariantImages(set);

            var sprite = IconVariantStore.Library.ActiveSprite(item.EntityId);
            item.SetSprite?.Invoke(sprite);   // активная могла исчезнуть — снимаем и её
            Debug.Log($"[Студия] «{item.DisplayName}»: удалено картинок {n} (рецепты живы).");
        }

        /// <summary>
        /// Картинки варианта сейчас нет. Флаг imageDeleted ставит сама студия, но PNG
        /// могли снести руками в Project — тогда флага нет, а картинки уже нет, и
        /// восстановление по рецепту обязано остаться доступным.
        /// </summary>
        private static bool ImageMissing(IconVariant v)
        {
            if (v == null) return false;
            if (v.imageDeleted) return true;
            if (v.sprite != null) return false;
            return string.IsNullOrEmpty(v.pngAssetPath) ||
                   AssetDatabase.LoadAssetAtPath<Object>(v.pngAssetPath) == null;
        }

        /// <summary>
        /// Примерка + ручная настройка 9-slice UI-элемента. Точная рамка под конкретную
        /// картинку выставляется руками (авто-подбор ненадёжен): поля L/B/R/T пишут
        /// item.SliceBorder, направляющие поверх исходного спрайта показывают, где проходит
        /// срез, а растяжимое превью — держится ли рамка. «Применить в игру» публикует спрайт
        /// и синхронизирует -unity-slice в USS (иначе окно не увидит новую рамку).
        /// </summary>
        private VisualElement BuildStretchPreview(IconItem item, IconCollection col, Sprite sprite)
        {
            var wrap = new VisualElement();
            var b = item.SliceBorder;                 // локальная рабочая копия рамки
            Vector2Int store = col.StorageSize.x > 0 ? col.StorageSize : new Vector2Int(96, 96);
            bool hasUss = !string.IsNullOrEmpty(item.UssFile) && !string.IsNullOrEmpty(item.UssClass);

            // Наложение цвета хранится У СУЩНОСТИ (персист между переключениями картинок),
            // а НЕ читается из USS каждый раз — иначе галка сбрасывалась при возврате.
            const string TintProp = "-unity-background-image-tint-color";
            bool tintOn = item.TintEnabled;
            Color tintCol = item.TintColor == default ? Color.white : item.TintColor;
            // Фон превью — реальный background-color класса из USS (контекст «как в игре»).
            // Он же проступает сквозь прозрачный центр рамки → отвечает за читаемость текста.
            const string BgProp = "background-color";
            string bgStr = hasUss ? IconVariantStore.ReadUssProp(item.UssFile, item.UssClass, BgProp) : null;
            // Нет свойства в USS → фона нет (прозрачный): панель со скруглением/прозрачными краями
            // не должна получать прямоугольную подложку по умолчанию.
            Color sceneBg = IconVariantStore.TryParseUssColor(bgStr, out var bc) ? bc : Color.clear;
            Color curBg = sceneBg;   // рабочая копия фона (пишется в USS по «Применить»)

            // ── Растяжимое превью (обновляется при смене рамки/размера/цвета) ──
            var stage = new VisualElement();
            stage.style.backgroundColor = new Color(0.12f, 0.12f, 0.13f);
            stage.style.paddingTop = 10; stage.style.paddingBottom = 10;
            stage.style.paddingLeft = 10; stage.style.paddingRight = 10;
            stage.style.alignItems = Align.FlexStart;
            // подложка «как в игре»: реальный фон окна из USS (background-color), поверх — спрайт
            var panelBg = new VisualElement();
            panelBg.style.backgroundColor = sceneBg;
            var panel = new VisualElement();
            panel.style.backgroundImage = new StyleBackground(sprite);
            panel.style.width = _uiPreviewW;
            panel.style.height = _uiPreviewH;
            panelBg.Add(panel);
            stage.Add(panelBg);

            void ApplyTint()
            {
                panel.style.unityBackgroundImageTintColor = tintOn ? tintCol : Color.white;
            }
            ApplyTint();

            // ── Направляющие среза (перетаскиваемые мышью) поверх исходного спрайта 1:1 ──
            const float GUIDE = 200f;
            float gw = GUIDE, gh = GUIDE;
            if (store.x >= store.y) gh = GUIDE * store.y / store.x; else gw = GUIDE * store.x / store.y;
            var guideBox = new VisualElement();
            guideBox.style.width = gw; guideBox.style.height = gh;
            guideBox.style.backgroundColor = new Color(0.08f, 0.08f, 0.09f);
            guideBox.style.backgroundImage = new StyleBackground(sprite);
            guideBox.pickingMode = PickingMode.Position;   // ловит перетаскивание линий
            var gL = MakeGuide(true); var gR = MakeGuide(true);
            var gT = MakeGuide(false); var gB = MakeGuide(false);
            guideBox.Add(gL); guideBox.Add(gR); guideBox.Add(gT); guideBox.Add(gB);
            // числовые метки px у каждой линии (дублируют значение прямо на картинке)
            var mL = MakeGuideLabel(); var mR = MakeGuideLabel();
            var mT = MakeGuideLabel(); var mB = MakeGuideLabel();
            guideBox.Add(mL); guideBox.Add(mR); guideBox.Add(mT); guideBox.Add(mB);

            // ── Область-источник зеркала: ПРЯМОУГОЛЬНИК с двумя перемещаемыми углами
            //    (верхний-левый и нижний-правый). Двигая верхний-левый внутрь, пропускаем отступ
            //    панели от края кадра; зеркало отражает выделение, отсекая внешние поля/фон. ──
            // Постобработка — СВОЯ у активного варианта (обрезка/зеркало под конкретную картинку)
            IconVariantStore.GetActiveVariantPost(item.EntityId, out int pMode, out int pAxis,
                out RectInt pRect, out Color pFill, out int pFeather, out int pRadius);
            Color fillColor = pFill;        // цвет заливки (режим «Заливка центра»)
            int fillFeather = pFeather;     // размытие края заливки, px
            int fillRadius = pRadius;       // скругление углов заливки, px
            int selX0 = 0, selY0 = 0, selX1 = store.x / 2, selY1 = store.y / 2;
            if (pRect.width > 0 && pRect.height > 0)   // восстановить сохранённую область варианта
            {
                selX0 = pRect.x; selY0 = pRect.y;
                selX1 = pRect.x + pRect.width; selY1 = pRect.y + pRect.height;
            }
            var selCol = new Color(1f, 0.82f, 0.2f);
            var selRect = new VisualElement { pickingMode = PickingMode.Ignore };
            selRect.style.position = Position.Absolute;
            selRect.style.borderLeftWidth = 2; selRect.style.borderRightWidth = 2;
            selRect.style.borderTopWidth = 2; selRect.style.borderBottomWidth = 2;
            selRect.style.borderLeftColor = selCol; selRect.style.borderRightColor = selCol;
            selRect.style.borderTopColor = selCol; selRect.style.borderBottomColor = selCol;
            selRect.style.backgroundColor = new Color(1f, 0.82f, 0.2f, 0.1f);
            var hTL = MakeSelHandle(selCol); var hBR = MakeSelHandle(selCol);
            var selCap = new Label(); selCap.AddToClassList("hint");
            guideBox.Add(selRect); guideBox.Add(hTL); guideBox.Add(hBR);

            // Исходная текстура (readable) для живого превью зеркала — грузим из PNG активного варианта
            Texture2D srcTex = null;
            string spPath = AssetDatabase.GetAssetPath(sprite);
            if (!string.IsNullOrEmpty(spPath))
            {
                string abs = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath, "..", spPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
                if (System.IO.File.Exists(abs))
                {
                    srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    srcTex.LoadImage(System.IO.File.ReadAllBytes(abs));
                }
            }
            var mirrorImg = new Image { scaleMode = ScaleMode.ScaleToFit };
            mirrorImg.style.width = 130; mirrorImg.style.height = 130;
            mirrorImg.style.backgroundColor = new Color(0.08f, 0.08f, 0.09f);
            Texture2D lastMirror = null;

            DropdownField modeField = null;   // Оригинал/Обрезать/Зеркалить — создаётся ниже
            EnumField axisField = null;       // ось зеркала
            void UpdateSel()
            {
                float x0 = gw * selX0 / store.x, y0 = gh * selY0 / store.y;
                float x1 = gw * selX1 / store.x, y1 = gh * selY1 / store.y;
                selRect.style.left = x0; selRect.style.top = y0;
                selRect.style.width = x1 - x0; selRect.style.height = y1 - y0;
                hTL.style.left = x0 - 6; hTL.style.top = y0 - 6;
                hBR.style.left = x1 - 6; hBR.style.top = y1 - 6;
                int sw = selX1 - selX0, sh = selY1 - selY0;
                int mode = modeField != null ? modeField.index : pMode;
                int axis = axisField != null ? (int)(IconVariantStore.MirrorMode)axisField.value : pAxis;
                var rect = new RectInt(selX0, selY0, sw, sh);
                // подпись: как изменится размер спрайта
                string outSz = mode == 2
                    ? $"{(axis != 2 ? 2 * sw : sw)}×{(axis != 1 ? 2 * sh : sh)}"
                    : mode == 1 ? $"{sw}×{sh}" : $"{store.x}×{store.y}";
                string modeName = mode == 0 ? "оригинал" : mode == 1 ? "обрезка"
                    : mode == 2 ? "зеркало" : "заливка центра";
                selCap.text = $"{modeName} → спрайт {outSz}px";
                // персист настроек постобработки У АКТИВНОГО ВАРИАНТА (своя у каждого)
                IconVariantStore.SetActiveVariantPost(item.EntityId, mode, axis, rect, fillColor,
                    fillFeather, fillRadius);
                // живое превью выбранного режима
                if (srcTex != null)
                {
                    if (lastMirror != null) UnityEngine.Object.DestroyImmediate(lastMirror);
                    lastMirror = IconVariantStore.ApplyPostProcess(srcTex, mode, axis, rect, fillColor,
                        fillFeather, fillRadius);
                    mirrorImg.image = lastMirror;                             // результат 1:1
                    panel.style.backgroundImage = new StyleBackground(lastMirror); // растяжка «как в игре»
                }
            }
            // перетаскивание угла выделения
            void DragHandle(VisualElement handle, System.Action<int, int> setStore)
            {
                bool dragging = false;
                handle.RegisterCallback<PointerDownEvent>(e =>
                { dragging = true; handle.CapturePointer(e.pointerId); e.StopPropagation(); });
                handle.RegisterCallback<PointerMoveEvent>(e =>
                {
                    if (!dragging) return;
                    var lp = guideBox.WorldToLocal(e.position);
                    setStore(Mathf.RoundToInt(lp.x / gw * store.x), Mathf.RoundToInt(lp.y / gh * store.y));
                    UpdateSel();
                });
                handle.RegisterCallback<PointerUpEvent>(e =>
                { if (dragging) { dragging = false; handle.ReleasePointer(e.pointerId); } });
            }
            DragHandle(hTL, (px, py) =>
            { selX0 = Mathf.Clamp(px, 0, selX1 - 8); selY0 = Mathf.Clamp(py, 0, selY1 - 8); });
            DragHandle(hBR, (px, py) =>
            { selX1 = Mathf.Clamp(px, selX0 + 8, store.x); selY1 = Mathf.Clamp(py, selY0 + 8, store.y); });

            int maxL = Mathf.Max(2, store.x / 2 - 1), maxV = Mathf.Max(2, store.y / 2 - 1);
            IntegerField fL = null, fR = null, fT = null, fB = null;

            void Apply()
            {
                panel.style.unitySliceLeft = Mathf.RoundToInt(b.x);
                panel.style.unitySliceBottom = Mathf.RoundToInt(b.y);
                panel.style.unitySliceRight = Mathf.RoundToInt(b.z);
                panel.style.unitySliceTop = Mathf.RoundToInt(b.w);
                float lx = gw * b.x / store.x, rx = gw * (1f - b.z / store.x);
                float ty = gh * b.w / store.y, by = gh * (1f - b.y / store.y);
                gL.style.left = lx;             // L=x от левого края
                gR.style.left = rx;            // R=z от правого края
                gT.style.top = ty;            // T=w от верхнего края (top растёт вниз)
                gB.style.top = by;           // B=y от нижнего края
                fL?.SetValueWithoutNotify(Mathf.RoundToInt(b.x));
                fR?.SetValueWithoutNotify(Mathf.RoundToInt(b.z));
                fT?.SetValueWithoutNotify(Mathf.RoundToInt(b.w));
                fB?.SetValueWithoutNotify(Mathf.RoundToInt(b.y));
                // метки: у своей линии, тексты = px рамки
                mL.text = ((int)b.x).ToString(); mL.style.left = lx + 2; mL.style.top = 2;
                mR.text = ((int)b.z).ToString(); mR.style.left = rx - 20; mR.style.top = 2;
                mT.text = ((int)b.w).ToString(); mT.style.top = ty + 1; mT.style.left = 2;
                mB.text = ((int)b.y).ToString(); mB.style.top = by - 15; mB.style.left = 2;
            }

            // Перетаскивание: хватаем ближайшую к курсору линию и двигаем
            string drag = null;
            guideBox.RegisterCallback<PointerDownEvent>(e =>
            {
                float px = (float)e.localPosition.x, py = (float)e.localPosition.y;
                float dL = Mathf.Abs(px - gw * b.x / store.x);
                float dR = Mathf.Abs(px - gw * (1f - b.z / store.x));
                float dT = Mathf.Abs(py - gh * b.w / store.y);
                float dB = Mathf.Abs(py - gh * (1f - b.y / store.y));
                float m = Mathf.Min(Mathf.Min(dL, dR), Mathf.Min(dT, dB));
                if (m > 12f) return;   // мимо линий
                drag = m == dL ? "L" : m == dR ? "R" : m == dT ? "T" : "B";
                guideBox.CapturePointer(e.pointerId);
            });
            guideBox.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (drag == null) return;
                float px = (float)e.localPosition.x, py = (float)e.localPosition.y;
                if (drag == "L") b.x = Mathf.Clamp(Mathf.Round(px / gw * store.x), 0, maxL);
                else if (drag == "R") b.z = Mathf.Clamp(Mathf.Round((1f - px / gw) * store.x), 0, maxL);
                else if (drag == "T") b.w = Mathf.Clamp(Mathf.Round(py / gh * store.y), 0, maxV);
                else b.y = Mathf.Clamp(Mathf.Round((1f - py / gh) * store.y), 0, maxV);
                item.SetSliceBorder?.Invoke(b); Apply();
            });
            guideBox.RegisterCallback<PointerUpEvent>(e =>
            {
                if (drag != null) { drag = null; guideBox.ReleasePointer(e.pointerId); }
            });

            var header = new Label("Обрезка 9-slice: тяните зелёные линии мышью на картинке (или " +
                "впишите px ниже). Всё ВНУТРИ линий (рамка, углы) не тянется; центр между ними " +
                "растягивается — держите его плоским/тёмным.");
            header.AddToClassList("hint");
            header.style.marginTop = 6;
            header.style.whiteSpace = WhiteSpace.Normal;
            wrap.Add(header);

            // Поля рамки в px (синхронны с перетаскиванием линий)
            var borderRow = new VisualElement();
            borderRow.AddToClassList("picker__row");
            fL = BorderField("Слева", (int)b.x, 0, maxL, v => { b.x = v; item.SetSliceBorder?.Invoke(b); Apply(); });
            fR = BorderField("Справа", (int)b.z, 0, maxL, v => { b.z = v; item.SetSliceBorder?.Invoke(b); Apply(); });
            fT = BorderField("Сверху", (int)b.w, 0, maxV, v => { b.w = v; item.SetSliceBorder?.Invoke(b); Apply(); });
            fB = BorderField("Снизу", (int)b.y, 0, maxV, v => { b.y = v; item.SetSliceBorder?.Invoke(b); Apply(); });
            borderRow.Add(fL); borderRow.Add(fR); borderRow.Add(fT); borderRow.Add(fB);
            var publishBtn = new Button { text = "Применить в игру",
                tooltip = "Опубликовать активный вариант и синхронизировать USS (-unity-slice + цвет + фон)" };
            borderRow.Add(publishBtn);
            wrap.Add(borderRow);
            Apply();   // финальная синхронизация линий/полей/превью

            // ── Постобработка перед публикацией: режим отображения жёлтой области ──
            var mirrorRow = new VisualElement();
            mirrorRow.AddToClassList("picker__row");
            modeField = new DropdownField(new List<string> { "Оригинал", "Обрезать", "Зеркалить", "Заливка центра" },
                Mathf.Clamp(pMode, 0, 3))
                { style = { width = 140 },
                  tooltip = "Что делать со спрайтом перед публикацией (неразрушающе):\n" +
                            "Оригинал — весь кадр как есть.\nОбрезать — вырезать жёлтую область.\n" +
                            "Зеркалить — отразить жёлтую область (симметрия углов).\n" +
                            "Заливка центра — залить жёлтую область сплошным цветом (рамка снаружи цела)." };
            axisField = new EnumField((IconVariantStore.MirrorMode)Mathf.Clamp(pAxis, 0, 2))
                { style = { width = 110 }, tooltip = "Ось зеркала." };
            var fillField = new ColorField { value = fillColor, style = { width = 110 },
                tooltip = "Цвет заливки центра (плоский, без градиента)." };
            var featherField = new SliderInt("Размытие", 0, 40) { value = fillFeather,
                style = { width = 150 }, showInputField = true,
                tooltip = "Ширина мягкого перехода по краю заливки, px. 0 — резкий край." };
            var radiusField = new SliderInt("Скругление", 0, 48) { value = fillRadius,
                style = { width = 160 }, showInputField = true,
                tooltip = "Радиус скругления углов заливки, px — чтобы лечь под скруглённую рамку." };
            featherField.RegisterValueChangedCallback(e => { fillFeather = e.newValue; UpdateSel(); });
            radiusField.RegisterValueChangedCallback(e => { fillRadius = e.newValue; UpdateSel(); });
            void SyncModeControls()
            {
                axisField.style.display = modeField.index == 2 ? DisplayStyle.Flex : DisplayStyle.None;
                bool isFill = modeField.index == 3;
                fillField.style.display = isFill ? DisplayStyle.Flex : DisplayStyle.None;
                featherField.style.display = isFill ? DisplayStyle.Flex : DisplayStyle.None;
                radiusField.style.display = isFill ? DisplayStyle.Flex : DisplayStyle.None;
            }
            modeField.RegisterValueChangedCallback(_ => { SyncModeControls(); UpdateSel(); });
            axisField.RegisterValueChangedCallback(_ => UpdateSel());
            fillField.RegisterValueChangedCallback(e => { fillColor = e.newValue; UpdateSel(); });
            SyncModeControls();
            mirrorRow.Add(modeField);
            mirrorRow.Add(axisField);
            mirrorRow.Add(fillField);
            mirrorRow.Add(featherField);
            mirrorRow.Add(radiusField);
            mirrorRow.Add(new Button(() =>
            {
                int mode = modeField.index;
                int axis = (int)(IconVariantStore.MirrorMode)axisField.value;
                if (IconVariantStore.CreateVariantFromPost(item, mode, axis,
                        new RectInt(selX0, selY0, selX1 - selX0, selY1 - selY0), fillColor,
                        fillFeather, fillRadius))
                    RebuildPicker();
            })
            { text = "Создать вариант",
              tooltip = "Запечь текущую постобработку в новый вариант — чтобы применить следующую " +
                        "операцию поверх (залил → создал → обрезал → …). Исходник не меняется." });
            mirrorRow.Add(selCap);
            wrap.Add(mirrorRow);

            // Живое превью зеркального результата (обновляется при движении углов выделения)
            var mirrorPrevRow = new VisualElement();
            mirrorPrevRow.style.flexDirection = FlexDirection.Row;
            mirrorPrevRow.style.alignItems = Align.Center;
            var mpCap = new Label("результат постобработки →"); mpCap.AddToClassList("hint");
            mpCap.style.marginRight = 8;
            mirrorPrevRow.Add(mpCap);
            mirrorPrevRow.Add(mirrorImg);
            wrap.Add(mirrorPrevRow);

            // Освобождаем текстуры превью при закрытии панели (иначе утечка при пересборках)
            wrap.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (lastMirror != null) UnityEngine.Object.DestroyImmediate(lastMirror);
                if (srcTex != null) UnityEngine.Object.DestroyImmediate(srcTex);
            });

            UpdateSel();   // первичная отрисовка рамки выделения, подписи и превью

            // ── Удаление фона (второй этап, как у иконок): rembg → прозрачный вариант ──
            var bgCutRow = new VisualElement();
            bgCutRow.AddToClassList("picker__row");
            var cutBtn = new Button(() =>
            {
                if (IconVariantStore.RemoveBackgroundVariant(item, b))
                    RebuildPicker();
            })
            { text = "Удалить фон → новый вариант",
              tooltip = "rembg: вырезать фон и обрезать поля (панель станет прозрачной и впритык). " +
                        "Создаёт новый вариант, исходник не трогает." };
            cutBtn.SetEnabled(RembgProcessor.IsAvailable);
            bgCutRow.Add(cutBtn);
            if (!RembgProcessor.IsAvailable)
            {
                var n = new Label("rembg недоступен (Project Settings ▸ AI Icon Tools)");
                n.AddToClassList("hint");
                bgCutRow.Add(n);
            }
            wrap.Add(bgCutRow);

            // ── Наложение цвета (tint): хранится у сущности, применяется в USS по кнопке ──
            var tintRow = new VisualElement();
            tintRow.AddToClassList("picker__row");
            var tintToggle = new Toggle("Накладывать цвет") { value = tintOn,
                tooltip = "USS -unity-background-image-tint-color: умножает спрайт на цвет. " +
                          "БЕЛЫЙ = без изменений (умножение на 1) — берите тёмный для затемнения " +
                          "или оттенок для тонировки. Выкл — спрайт как есть." };
            var tintField = new ColorField("Цвет") { value = tintCol, style = { width = 160 } };
            tintField.SetEnabled(tintOn);
            tintToggle.RegisterValueChangedCallback(e =>
            {
                tintOn = e.newValue;
                // Белый tint визуально ничего не делает — при включении подставим заметный дефолт
                if (tintOn && tintCol.r > 0.98f && tintCol.g > 0.98f && tintCol.b > 0.98f)
                {
                    tintCol = new Color(0.5f, 0.5f, 0.5f, 1f);
                    tintField.SetValueWithoutNotify(tintCol);
                }
                tintField.SetEnabled(tintOn);
                item.SetTint?.Invoke(tintOn, tintCol);   // персист у сущности
                ApplyTint();
            });
            tintField.RegisterValueChangedCallback(e =>
            {
                tintCol = e.newValue;
                item.SetTint?.Invoke(tintOn, tintCol);   // персист у сущности
                ApplyTint();
            });
            tintRow.Add(tintToggle);
            tintRow.Add(tintField);
            wrap.Add(tintRow);

            // ── Фон под панелью (background-color класса): проступает сквозь прозрачный центр,
            //    задаёт читаемость текста. Тёмный фон + рамка с прозрачным центром = текст читается. ──
            var bgRow = new VisualElement();
            bgRow.AddToClassList("picker__row");
            var bgField = new ColorField("Фон под панелью") { value = curBg, style = { width = 200 },
                tooltip = "USS background-color класса. Виден там, где спрайт прозрачный. " +
                          "ПРОЗРАЧНЫЙ (alpha 0) — фон не ставится совсем: за скруглёнными углами " +
                          "панели будет фон окна, а не чёрный прямоугольник." };
            bgField.RegisterValueChangedCallback(e => { curBg = e.newValue; panelBg.style.backgroundColor = curBg; });
            bgRow.Add(bgField);
            if (hasUss) bgRow.Add(new Label($"→ .{item.UssClass}") { tooltip = "Пишется в USS-класс окна" });
            wrap.Add(bgRow);

            // ── Ссылки на ассеты: PNG активного варианта и опубликованный спрайт игры ──
            var assetRow = new VisualElement();
            assetRow.AddToClassList("picker__row");
            var srcField = new ObjectField("Спрайт варианта") { objectType = typeof(Sprite),
                value = sprite, style = { flexGrow = 1 },
                tooltip = "PNG активного варианта в проекте (найти вручную тяжело). " +
                          "Клик — подсветить в Project." };
            srcField.SetEnabled(false);
            assetRow.Add(srcField);
            assetRow.Add(new Button(() =>
            {
                if (sprite != null) { Selection.activeObject = sprite; EditorGUIUtility.PingObject(sprite); }
            })
            { text = "Показать", tooltip = "Выделить PNG варианта в Project" });
            if (!string.IsNullOrEmpty(item.ApplyToPath))
            {
                var pub = AssetDatabase.LoadAssetAtPath<Sprite>(item.ApplyToPath);
                var pubField = new ObjectField("В игре") { objectType = typeof(Sprite),
                    value = pub, style = { flexGrow = 1 },
                    tooltip = "Опубликованный спрайт, который использует игра (ApplyToPath)." };
                pubField.SetEnabled(false);
                assetRow.Add(pubField);
                assetRow.Add(new Button(() =>
                {
                    if (pub != null) { Selection.activeObject = pub; EditorGUIUtility.PingObject(pub); }
                })
                { text = "Показать", tooltip = "Выделить опубликованный спрайт в Project" });
            }
            wrap.Add(assetRow);

            // ── Прямая связь с USS-файлом окна: показать, куда публикуется рамка/цвет ──
            var ussRow = new VisualElement();
            ussRow.AddToClassList("picker__row");
            if (hasUss)
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(item.UssFile);
                var ussField = new ObjectField("USS игры") { objectType = typeof(StyleSheet),
                    value = sheet, style = { flexGrow = 1 } };
                ussField.SetEnabled(false);   // связь задаётся в коде провайдера; тут — для наглядности
                ussRow.Add(ussField);
                string clsList = item.UssClasses != null ? "." + string.Join(" .", item.UssClasses) : "." + item.UssClass;
                ussRow.Add(new Label(clsList) { tooltip = "USS-классы, использующие эту панель" });
                ussRow.Add(new Button(() => { if (sheet != null) { Selection.activeObject = sheet; EditorGUIUtility.PingObject(sheet); } })
                    { text = "Показать", tooltip = "Выделить USS-файл в проекте" });
            }
            else
            {
                var note = new Label("Не привязан к окну (USS-класс не задан) — публикация не влияет на игру.");
                note.AddToClassList("hint");
                note.style.whiteSpace = WhiteSpace.Normal;
                ussRow.Add(note);
            }
            wrap.Add(ussRow);

            // Публикация: спрайт + -unity-slice + наложение цвета — окно точно повторит примерку
            publishBtn.clicked += () =>
            {
                bool ok = IconVariantStore.PublishUiElement(item);   // PNG + -unity-slice во все классы
                if (ok)
                    foreach (var target in item.AllUssTargets())
                        foreach (var cls in target.Classes)
                        {
                            if (string.IsNullOrEmpty(cls)) continue;
                            IconVariantStore.WriteUssProp(target.File, cls, TintProp,
                                tintOn ? IconVariantStore.UssColor(tintCol) : null);   // null → убрать наложение
                            // Пишем цвет ВСЕГДА, в т.ч. прозрачный (rgba(...,0)): у Button простое
                            // удаление свойства возвращает фон встроенной темы Unity, а не убирает его.
                            IconVariantStore.WriteUssProp(target.File, cls, BgProp,
                                IconVariantStore.UssColor(curBg));
                        }
                AssetDatabase.SaveAssets();   // зафиксировать персист tint у сущности
                if (ok)
                    EditorUtility.DisplayDialog("Готово",
                        $"«{item.DisplayName}» опубликован. Рамка {b.x:0}/{b.y:0}/{b.z:0}/{b.w:0} px, " +
                        (tintOn ? "цвет накладывается." : "без наложения цвета.") +
                        (hasUss ? $"\nUSS .{item.UssClass} обновлён." : ""), "OK");
            };

            // Два вида рядом: направляющие (исходник) + растяжка
            var viewsRow = new VisualElement();
            viewsRow.style.flexDirection = FlexDirection.Row;
            viewsRow.style.alignItems = Align.FlexStart;
            var guideCol = new VisualElement();
            guideCol.style.marginRight = 12;
            var guideCap = new Label("тяните линии ↔"); guideCap.AddToClassList("hint");
            guideCol.Add(guideBox); guideCol.Add(guideCap);
            viewsRow.Add(guideCol);
            var stretchCol = new VisualElement();
            stretchCol.style.flexGrow = 1;
            stretchCol.Add(stage);

            var wSlider = new SliderInt("Ширина", 32, 1400) { value = _uiPreviewW };
            wSlider.RegisterValueChangedCallback(e => { _uiPreviewW = e.newValue; panel.style.width = e.newValue; });
            var hSlider = new SliderInt("Высота", 16, 600) { value = _uiPreviewH };
            hSlider.RegisterValueChangedCallback(e => { _uiPreviewH = e.newValue; panel.style.height = e.newValue; });

            // «Как в игре»: высота карточки берётся из реального USS (.building-card → height),
            // ширина оценивается по пропорции карточки при выбранном разрешении (reference 1080p).
            int gameH = 0;
            string hs = hasUss ? IconVariantStore.ReadUssProp(item.UssFile, item.UssClass, "height") : null;
            if (hs != null)
            {
                var mm = System.Text.RegularExpressions.Regex.Match(hs, @"\d+");
                if (mm.Success) gameH = int.Parse(mm.Value);
            }
            var resRow = new VisualElement();
            resRow.style.flexDirection = FlexDirection.Row; resRow.style.alignItems = Align.Center;
            var resField = new DropdownField(new List<string> { "FHD 1080p", "QHD 1440p", "4K 2160p" }, 0)
                { style = { width = 130 }, tooltip = "Разрешение экрана (панели масштабируются от 1080p)" };
            resRow.Add(resField);
            var asGame = new Button(() =>
            {
                float scale = resField.index == 1 ? 1440f / 1080f : resField.index == 2 ? 2160f / 1080f : 1f;
                if (gameH > 0)
                {
                    _uiPreviewH = Mathf.RoundToInt(gameH * scale);
                    panel.style.height = _uiPreviewH; hSlider.SetValueWithoutNotify(_uiPreviewH);
                    _uiPreviewW = Mathf.RoundToInt(_uiPreviewH * 5.4f);   // пропорция карточки (≈940×168)
                    panel.style.width = _uiPreviewW; wSlider.SetValueWithoutNotify(_uiPreviewW);
                }
            })
            { text = "Размер как в игре",
              tooltip = "Высота = USS .building-card height × масштаб разрешения; ширина — оценка карточки." };
            resRow.Add(asGame);
            if (gameH > 0)
                resRow.Add(new Label($"USS height: {gameH}px") { style = { marginLeft = 6 } });
            stretchCol.Add(resRow);
            stretchCol.Add(wSlider);
            stretchCol.Add(hSlider);
            viewsRow.Add(stretchCol);
            wrap.Add(viewsRow);

            // ── Превью «во всех элементах»: одна панель используется несколькими USS-классами
            //    разного размера — показываем, как ляжет в каждый (высота из USS класса). ──
            if (item.UssClasses != null && item.UssClasses.Length > 1 && sprite != null)
            {
                var allCap = new Label("Во всех элементах (высота из USS):"); allCap.AddToClassList("hint");
                allCap.style.marginTop = 8;
                wrap.Add(allCap);
                var allRow = new VisualElement();
                allRow.style.flexDirection = FlexDirection.Row;
                allRow.style.flexWrap = Wrap.Wrap;
                foreach (var cls in item.UssClasses)
                {
                    if (string.IsNullOrEmpty(cls)) continue;
                    int ch = 40;
                    string chs = hasUss ? IconVariantStore.ReadUssProp(item.UssFile, cls, "height") : null;
                    if (chs != null)
                    {
                        var cm = System.Text.RegularExpressions.Regex.Match(chs, @"\d+");
                        if (cm.Success) ch = Mathf.Clamp(int.Parse(cm.Value), 20, 200);
                    }
                    var cell = new VisualElement { style = { marginRight = 10, marginBottom = 6, alignItems = Align.FlexStart } };
                    var p = new VisualElement();
                    p.style.backgroundColor = curBg;
                    var pi = new VisualElement();
                    pi.style.backgroundImage = new StyleBackground(lastMirror != null ? lastMirror : sprite.texture);
                    pi.style.unitySliceLeft = Mathf.RoundToInt(b.x); pi.style.unitySliceBottom = Mathf.RoundToInt(b.y);
                    pi.style.unitySliceRight = Mathf.RoundToInt(b.z); pi.style.unitySliceTop = Mathf.RoundToInt(b.w);
                    if (tintOn) pi.style.unityBackgroundImageTintColor = tintCol;
                    pi.style.width = 220; pi.style.height = ch;
                    p.Add(pi); cell.Add(p);
                    var cap = new Label($".{cls} · {ch}px"); cap.AddToClassList("hint");
                    cell.Add(cap);
                    allRow.Add(cell);
                }
                wrap.Add(allRow);
            }
            return wrap;
        }

        // Зелёная направляющая среза (вертикальная — vertical=true, иначе горизонтальная)
        private static VisualElement MakeGuide(bool vertical)
        {
            var g = new VisualElement();
            g.style.position = Position.Absolute;
            g.style.backgroundColor = new Color(0.4f, 1f, 0.4f, 0.85f);
            if (vertical) { g.style.top = 0; g.style.bottom = 0; g.style.width = 1; }
            else { g.style.left = 0; g.style.right = 0; g.style.height = 1; }
            g.pickingMode = PickingMode.Ignore;
            return g;
        }

        // Ручка выделения области зеркала: квадрат, тянется мышью
        private static VisualElement MakeSelHandle(Color color)
        {
            var handle = new VisualElement { pickingMode = PickingMode.Position };
            handle.style.position = Position.Absolute;
            handle.style.width = 12; handle.style.height = 12;
            handle.style.backgroundColor = color;
            handle.style.borderTopWidth = 1; handle.style.borderBottomWidth = 1;
            handle.style.borderLeftWidth = 1; handle.style.borderRightWidth = 1;
            var dark = new Color(0.1f, 0.1f, 0.1f);
            handle.style.borderTopColor = dark; handle.style.borderBottomColor = dark;
            handle.style.borderLeftColor = dark; handle.style.borderRightColor = dark;
            return handle;
        }

        // Числовая метка px у направляющей: маленькая, зелёная на тёмной подложке (читается на картинке)
        private static Label MakeGuideLabel()
        {
            var l = new Label("0");
            l.style.position = Position.Absolute;
            l.style.fontSize = 10;
            l.style.color = new Color(0.5f, 1f, 0.5f);
            l.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            l.style.paddingLeft = 2; l.style.paddingRight = 2;
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        // Поле рамки: подпись + IntegerField с клампом, вызывает onChange
        private static IntegerField BorderField(string label, int value, int min, int max, System.Action<int> onChange)
        {
            var f = new IntegerField(label) { value = value };
            f.style.width = 92;
            f.style.marginRight = 6;
            f.RegisterValueChangedCallback(e =>
            {
                int v = Mathf.Clamp(e.newValue, min, max);
                if (v != e.newValue) f.SetValueWithoutNotify(v);
                onChange(v);
            });
            return f;
        }

        private void SelectVariant(IconItem item, EntityIconHistory history, IconVariant v)
        {
            if (v.sprite == null) return;
            IconVariantStore.SetActive(item.EntityId, v.id);
            item.SetSprite?.Invoke(v.sprite);
            // UI-элемент: активация = «применить в игру» — публикуем по пути USS с 9-slice рамкой
            if (item.IsUiElement)
                IconVariantStore.PublishUiElement(item);
        }

        /// <summary>
        /// Клик по варианту в ленте: у набора с рендишенами назначает в редактируемый
        /// размер (мастер → «сделать активным» с отметкой истории, иначе рендишен);
        /// у обычного набора — как раньше, активным.
        /// </summary>
        private void AssignFromLaneClick(IconItem item, IconCollection col,
                                         EntityIconHistory history, IconVariant v)
        {
            if (v.sprite == null) return;
            if (col?.GetRenditionSizes?.Invoke() != null)
            {
                int size = EffectiveEditSize(col);
                if (size == MasterSize(col)) SelectVariant(item, history, v);   // мастер + маркер активного
                else AssignRendition(item, size, v.sprite);
            }
            else
            {
                SelectVariant(item, history, v);
            }
        }

        // ── Действия генерации ──

        private IconGenProfile ResolveCollectionProfile(IconCollection col)
            => col?.SourceAsset != null ? col.SourceAsset.profile : null;

        /// <summary>Профиль генерит через облачный PixelLab — локальный ComfyUI не нужен.</summary>
        private static bool UsesPixelLab(IconGenProfile profile) =>
            profile != null && profile.style != null &&
            profile.style.engine != IconGenEngine.ComfyUI;

        /// <summary>Готовность движков перед постановкой в очередь (диалог при отказе).</summary>
        private static async System.Threading.Tasks.Task<bool> EnsureEngineReady(bool needComfy, bool needPixelLab)
        {
            if (needComfy && !await ComfyUIClient.IsOnlineAsync())
            {
                EditorUtility.DisplayDialog("ComfyUI не отвечает", "Запустите ComfyUI и повторите.", "Ок");
                return false;
            }
            if (needPixelLab && !PixelLabUiClient.HasKey)
            {
                EditorUtility.DisplayDialog("Нет ключа PixelLab",
                    "Создайте LocalOverrides/pixellab.json с {\"api_key\": \"...\"} (ключ — pixellab.ai/account).", "Ок");
                return false;
            }
            return true;
        }

        private void ApplyTweakAndProfile(ref IconGenRequest req, IconGenProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(_promptTweak))
                req.Prompt = req.Prompt.TrimEnd().TrimEnd('.') + ", " + _promptTweak.Trim() + ".";
            if (profile != null)
            {
                req.Style = profile.style;
                req.CheckpointOverride = profile.checkpoint;
                req.LoraOverride = profile.lora ?? "";
                req.LoraStrengthOverride = profile.loraStrength;
            }
        }

        private async void EnqueueBatch(IconItem item, IconCollection col)
        {
            var profile = ResolveCollectionProfile(col);
            bool pixelLab = UsesPixelLab(profile);
            if (!await EnsureEngineReady(!pixelLab, pixelLab)) return;
            string runId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var list = new List<IconGenRequest>();
            for (int i = 0; i < BatchCount; i++)
            {
                var req = IconVariantStore.BuildRequest(item, col, runId);
                req.Seed = Random.Range(1, 100_000_000);
                ApplyTweakAndProfile(ref req, profile);
                list.Add(req);
            }
            AiIconGenerator.Enqueue(list);
            Debug.Log($"[Студия] В очередь: {item.DisplayName} ({col.Title}) ×{BatchCount}, " +
                      $"профиль «{(profile != null ? profile.name : "текущий")}».");
        }

        /// <summary>
        /// Пачка на всю активную вкладку: каждая иконка ×BatchCount.
        /// onlyMissing — только сущности без единого живого варианта (догенерация).
        /// </summary>
        /// <summary>Вся активная вкладка (все её наборы). onlyMissing — только без вариантов.</summary>
        private void EnqueueAll(bool onlyMissing) => EnqueueAll(ActiveCollections(), ActiveView().Title, onlyMissing);

        /// <summary>Один набор (кнопка «Все» в шапке секции).</summary>
        private void EnqueueAll(IconCollection col, bool onlyMissing)
            => EnqueueAll(new List<IconCollection> { col }, col.Title, onlyMissing);

        private async void EnqueueAll(List<IconCollection> cols, string scopeTitle, bool onlyMissing)
        {
            bool anyComfy = false, anyPixelLab = false;
            foreach (var c in cols)
            {
                if (UsesPixelLab(ResolveCollectionProfile(c))) anyPixelLab = true;
                else anyComfy = true;
            }
            if (!await EnsureEngineReady(anyComfy, anyPixelLab)) return;

            var work = new List<(IconItem item, IconCollection col)>();
            foreach (var col in cols)
            {
                var items = col.GetItems?.Invoke();
                if (items == null) continue;
                foreach (var item in items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.GetPrompt?.Invoke())) continue;
                    if (onlyMissing && HasAliveVariant(item.EntityId)) continue;
                    work.Add((item, col));
                }
            }

            if (work.Count == 0)
            {
                EditorUtility.DisplayDialog("Нечего генерировать",
                    onlyMissing ? $"У всех иконок «{scopeTitle}» уже есть варианты."
                                : $"В «{scopeTitle}» нет иконок с промптами.", "Ок");
                return;
            }

            if (!EditorUtility.DisplayDialog($"Сгенерировать все — {scopeTitle}",
                $"{work.Count} иконок × {BatchCount} = {work.Count * BatchCount} генераций. Продолжить?",
                "Погнали", "Отмена"))
                return;

            var list = new List<IconGenRequest>();
            foreach (var (item, col) in work)
            {
                var profile = ResolveCollectionProfile(col);
                string runId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
                for (int i = 0; i < BatchCount; i++)
                {
                    var req = IconVariantStore.BuildRequest(item, col, runId);
                    req.Seed = Random.Range(1, 100_000_000);
                    ApplyTweakAndProfile(ref req, profile);
                    list.Add(req);
                }
            }
            AiIconGenerator.Enqueue(list);
            Debug.Log($"[Студия] В очередь «{scopeTitle}»: {work.Count} иконок ×{BatchCount}.");
        }

        private static bool HasAliveVariant(string entityId)
        {
            var history = IconVariantStore.Library.Find(entityId);
            if (history?.variants == null) return false;
            foreach (var v in history.variants)
                if (v != null && !v.imageDeleted) return true;
            return false;
        }

        private async void EnqueueStylize(IconItem item, IconCollection col,
                                          IconVariant source, IconGenProfile target)
        {
            if (source == null) return;
            var stylizeProfile = target ?? ResolveCollectionProfile(col);
            bool stylizePixelLab = UsesPixelLab(stylizeProfile);
            if (!await EnsureEngineReady(!stylizePixelLab, stylizePixelLab)) return;
            if (stylizePixelLab)
                Debug.LogWarning("[Студия] PixelLab UI не умеет img2img — стилизация уйдёт как обычная генерация по промпту.");

            var req = IconVariantStore.BuildRequest(item, col,
                System.Guid.NewGuid().ToString("N").Substring(0, 8));
            req.Seed = Random.Range(1, 100_000_000);
            req.InitImageAssetPath = source.pngAssetPath;
            req.Denoise = StylizeDenoise;
            req.DisplayName = item.DisplayName + " (стилизация)";
            ApplyTweakAndProfile(ref req, stylizeProfile);
            AiIconGenerator.Enqueue(req);
        }

        /// <summary>Перегенерировать удалённую картинку по рецепту — в тот же путь.</summary>
        private async void RestoreVariant(IconItem item, IconCollection col, IconVariant v)
        {
            bool restorePixelLab = v?.recipe != null &&
                AiIconGenerator.IsPixelLabCheckpoint(v.recipe.checkpoint);
            if (!await EnsureEngineReady(!restorePixelLab, restorePixelLab)) return;

            AiIconGenerator.Enqueue(new IconGenRequest
            {
                Prompt = v.recipe.subject,
                AssetPath = v.pngAssetPath,
                DisplayName = item.DisplayName + " (восстановление)",
                StorageSize = col.StorageSize,
                CutBackground = true,
                DirectRequest = IconVariantStore.RecipeToRequest(v.recipe),
                UndoTarget = IconVariantStore.Library,
                UndoLabel = "Restore variant image",
                OnDone = (sprite, _) =>
                {
                    v.sprite = sprite;
                    v.imageDeleted = false;
                    EditorUtility.SetDirty(IconVariantStore.Library);
                    AssetDatabase.SaveAssetIfDirty(IconVariantStore.Library);
                },
            });
        }

        // ── Пометка/чистка набора ──

        private void MarkShownBad(bool bad)
        {
            var set = new List<(string, string)>();
            foreach (var col in ActiveCollections())
                foreach (var item in col.GetItems())
                {
                    var active = IconVariantStore.Library.Find(item.EntityId)?.Active;
                    if (active != null) set.Add((item.EntityId, active.id));
                }
            if (set.Count == 0) return;
            IconVariantStore.MarkSetBad(set, bad, ViewId);
            Debug.Log($"[Студия] {(bad ? "Помечено" : "Снята пометка")}: {set.Count} вариантов.");
        }

        private void CleanupExceptPack(bool deleteRecipes)
        {
            var pack = IconVariantStore.PackMatchingCurrent(ActiveEntityIds());
            if (pack == null)
            {
                EditorUtility.DisplayDialog("Нет текущего пака",
                    "Текущий выбор не совпадает ни с одним паком. Сначала примените или " +
                    "сохраните пак — чистка идёт «вокруг» него.", "Ок");
                return;
            }

            string label = string.IsNullOrEmpty(pack.packName) ? pack.name : pack.packName;
            if (deleteRecipes)
            {
                if (!EditorUtility.DisplayDialog("Удалить варианты, кроме пака?",
                        $"У сущностей пака «{label}» будут НЕОБРАТИМО удалены все варианты, " +
                        "кроме выбранных в паке.", "Удалить", "Отмена")) return;
                int n = IconVariantStore.DeleteVariantsExceptPack(pack);
                Debug.Log($"[Студия] «{label}»: удалено вариантов {n}.");
            }
            else
            {
                if (!EditorUtility.DisplayDialog("Удалить картинки, кроме пака?",
                        $"У сущностей пака «{label}» будут удалены PNG всех вариантов, кроме " +
                        "выбранных в паке. Рецепты сохранятся.", "Удалить картинки", "Отмена")) return;
                int n = IconVariantStore.DeleteImagesExceptPack(pack);
                Debug.Log($"[Студия] «{label}»: удалено картинок {n} (рецепты живы).");
            }
        }

        // ── Очередь ──

        private void UpdateQueueUI()
        {
            if (_queueLabel == null) return;
            bool running = AiIconGenerator.IsRunning;
            _queueLabel.text = running ? $"⏳ {AiIconGenerator.Pending}" : "";
            _queueLabel.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
            if (_queueCancel != null)
                _queueCancel.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;

            if (_progressBar != null)
            {
                _progressBar.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
                if (running)
                {
                    int total = Mathf.Max(1, AiIconGenerator.Total);
                    _progressBar.value = AiIconGenerator.Done / (float)total;
                    _progressBar.title = $"{AiIconGenerator.Done + 1}/{total}: {AiIconGenerator.CurrentLabel}";
                }
            }
        }
    }
}
