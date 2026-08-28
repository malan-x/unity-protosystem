using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ProtoSystem.LiveOps;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.Editor.LiveOps
{
    /// <summary>
    /// Окно A/B-экспериментов баланса: та же коллекция и те же роуты, что у
    /// веб-дашборда (/api/ab/experiments) — один источник правды, правь где
    /// удобнее. Сервер и проект берутся из LiveOpsConfig.
    ///
    /// Доступ — логин superuser'а PocketBase. Креды хранятся в EditorPrefs
    /// (пароль — обфусцированным, это машина разработчика, не билд), и при
    /// протухшем токене окно перелогинивается само — пароль вводится один раз.
    ///
    /// Формат: сервер отдаёт ?flat=1 — оверрайды и счётчики массивами пар,
    /// потому что JsonUtility не умеет словари.
    /// </summary>
    public class AbExperimentsWindow : EditorWindow
    {
        private const string TOKEN_KEY = "ProtoSystem.LiveOps.SuToken";
        private const string EMAIL_KEY = "ProtoSystem.LiveOps.SuEmail";
        private const string PASS_KEY  = "ProtoSystem.LiveOps.SuPass";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        // ── Модели (зеркало ответа сервера, flat-формат) ──

        [Serializable] public class KV { public string k; public float v; }
        [Serializable] public class Count { public string id; public int n; }

        [Serializable]
        public class Variant
        {
            public string id = "2";
            public int weight;          // корзины из 10000 (2000 = 20%)
            public int quota;
            public float fillChance = 1f;
            public KV[] overrides = Array.Empty<KV>();
        }

        [Serializable]
        public class Experiment
        {
            public string id;
            public string name;
            public string note;
            public bool active;
            public Variant[] variants = Array.Empty<Variant>();
            public Count[] counts = Array.Empty<Count>();
        }

        [Serializable] private class ListResponse { public Experiment[] experiments; }
        [Serializable] private class AuthResponse { public string token; }

        // ── Метрики по вариантам (конфиг наборов + агрегаты) ──

        [Serializable]
        public class MetricDef
        {
            public string key = "";
            public string label = "";
            public string type = "avg";     // pct / avg / sum / ratio
            public string field = "";
            public string field2 = "";      // ratio: знаменатель
            public string equals = "";      // pct: искомое значение поля
            public string format = "int";   // int / float1 / pct / min
            public bool enabled = true;
        }

        [Serializable]
        public class MetricSet
        {
            public string id = "main";
            public string name = "Основной";
            public MetricDef[] metrics = Array.Empty<MetricDef>();
        }

        [Serializable] private class MetricSetsResponse { public MetricSet[] sets; }

        [Serializable]
        public class StatRow
        {
            public string slice;
            public string variant;
            public int runs;
            public int players;
            public KV[] values = Array.Empty<KV>();
        }

        [Serializable] private class StatsResponse { public MetricDef[] metrics; public StatRow[] stats; }

        // ── Состояние ──

        private LiveOpsConfig _config;
        private string _email, _password = "", _token;
        private string _status = "";
        private bool _busy;
        private List<Experiment> _experiments = new List<Experiment>();
        private Vector2 _scroll;

        // Метрики
        private static readonly int[] DaysOptions = { 7, 30, 90 };
        private List<MetricSet> _metricSets = new List<MetricSet>();
        private string _currentSetId = "";
        private int _statsDaysIndex = 1;   // 30 дней
        private List<StatRow> _stats = new List<StatRow>();
        private MetricDef[] _statsMetrics = Array.Empty<MetricDef>();

        // Редактор наборов метрик: правит копию, «Сохранить» шлёт всё разом
        private bool _editingMetrics;
        private List<MetricSet> _editSets = new List<MetricSet>();
        private int _editSetIdx;

        // Форма редактирования. _editingId == null — форма скрыта
        private string _editingId;
        private string _formName = "", _formNote = "";
        private bool _formActive;
        private List<FormVariant> _formVariants = new List<FormVariant>();

        private class FormVariant
        {
            public string id = "2";
            public bool isQuota;
            public string value = "";      // % игроков или человек — по режиму
            public string chance = "";     // набор, % (только квота)
            public string overrides = "";  // «ключ=значение; …»
        }

        // ── Палитра (тёмный/светлый скин) ──

        private static bool Pro => EditorGUIUtility.isProSkin;
        private static Color CardBg     => Pro ? new Color(0.17f, 0.18f, 0.20f) : new Color(0.92f, 0.92f, 0.93f);
        private static Color CardBorder => Pro ? new Color(0.10f, 0.10f, 0.11f) : new Color(0.72f, 0.72f, 0.74f);
        private static Color ActiveTint => Pro ? new Color(0.20f, 0.30f, 0.22f) : new Color(0.84f, 0.93f, 0.85f);
        private static Color ZebraTint  => Pro ? new Color(1f, 1f, 1f, 0.03f) : new Color(0f, 0f, 0f, 0.03f);
        private static Color Green      => Pro ? new Color(0.42f, 0.85f, 0.50f) : new Color(0.10f, 0.55f, 0.20f);
        private static Color Dim        => Pro ? new Color(0.62f, 0.64f, 0.67f) : new Color(0.40f, 0.40f, 0.42f);
        private static Color ChipBg     => Pro ? new Color(0.28f, 0.32f, 0.44f) : new Color(0.75f, 0.79f, 0.92f);
        private static Color CodeBg     => Pro ? new Color(0.13f, 0.14f, 0.15f) : new Color(0.85f, 0.86f, 0.88f);
        private static Color RedBtn     => new Color(1f, 0.55f, 0.55f);

        private GUIStyle _titleStyle, _dimStyle, _chipStyle, _codeStyle, _headerStyle;

        private void BuildStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _dimStyle = new GUIStyle(EditorStyles.miniLabel);
            _dimStyle.normal.textColor = Dim;
            _chipStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Pro ? Color.white : Color.black },
            };
            _codeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                font = EditorStyles.miniLabel.font,
                normal = { textColor = Pro ? new Color(0.80f, 0.86f, 1f) : new Color(0.15f, 0.25f, 0.55f) },
            };
            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        }

        [MenuItem("ProtoSystem/LiveOps/A-B Эксперименты", false, 400)]
        public static void Open()
        {
            var w = GetWindow<AbExperimentsWindow>("A/B Эксперименты");
            w.minSize = new Vector2(620, 420);
        }

        private void OnEnable()
        {
            _token = EditorPrefs.GetString(TOKEN_KEY, "");
            _email = EditorPrefs.GetString(EMAIL_KEY, "");
            FindConfig();
            if (_config == null) return;
            if (IsLoggedIn) _ = Reload();
            else if (HasStoredPassword) _ = Login(LoadPassword());   // пароль вводится один раз
        }

        private bool IsLoggedIn => !string.IsNullOrEmpty(_token);
        private bool HasStoredPassword => !string.IsNullOrEmpty(_email) && !string.IsNullOrEmpty(EditorPrefs.GetString(PASS_KEY, ""));
        private string ServerUrl => _config != null ? _config.serverUrl.TrimEnd('/') : "";

        private void FindConfig()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LiveOpsConfig"))
            {
                _config = AssetDatabase.LoadAssetAtPath<LiveOpsConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (_config != null) return;
            }
        }

        // ── Хранение пароля: XOR + Base64. Не криптография, а защита от
        // случайного взгляда в реестр — окно и так живёт на машине владельца ──

        private static string Mask(string s)
        {
            var key = SystemInfo.deviceUniqueIdentifier;
            var bytes = Encoding.UTF8.GetBytes(s);
            for (int i = 0; i < bytes.Length; i++) bytes[i] ^= (byte)key[i % key.Length];
            return Convert.ToBase64String(bytes);
        }

        private static string Unmask(string s)
        {
            try
            {
                var key = SystemInfo.deviceUniqueIdentifier;
                var bytes = Convert.FromBase64String(s);
                for (int i = 0; i < bytes.Length; i++) bytes[i] ^= (byte)key[i % key.Length];
                return Encoding.UTF8.GetString(bytes);
            }
            catch { return ""; }
        }

        private void SavePassword(string password) => EditorPrefs.SetString(PASS_KEY, Mask(password));
        private string LoadPassword() => Unmask(EditorPrefs.GetString(PASS_KEY, ""));

        // ─────────────────────────────────────────────────────────────
        // GUI
        // ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            BuildStyles();

            if (_config == null)
            {
                EditorGUILayout.HelpBox("LiveOpsConfig не найден в проекте — без него неизвестны сервер и проект.", MessageType.Warning);
                if (GUILayout.Button("Искать снова")) FindConfig();
                return;
            }

            DrawToolbar();

            if (!IsLoggedIn) { DrawLogin(); DrawStatus(); return; }

            using (new EditorGUI.DisabledScope(_busy))
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                EditorGUILayout.Space(6);
                if (_editingId != null) DrawForm();
                foreach (var x in _experiments) DrawExperiment(x);
                if (_experiments.Count == 0 && _editingId == null)
                {
                    EditorGUILayout.Space(20);
                    var c = GUI.color; GUI.color = Dim;
                    EditorGUILayout.LabelField("Экспериментов нет — «+ Новый» в шапке.", EditorStyles.centeredGreyMiniLabel);
                    GUI.color = c;
                }
                EditorGUILayout.Space(10);
                if (_editingMetrics) DrawMetricsEditor();
                else DrawStats();
                EditorGUILayout.EndScrollView();
            }
            DrawStatus();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(_busy || !IsLoggedIn))
                {
                    if (GUILayout.Button("⟳ Обновить", EditorStyles.toolbarButton, GUILayout.Width(90))) _ = Reload();
                    if (_editingId == null &&
                        GUILayout.Button("+ Новый", EditorStyles.toolbarButton, GUILayout.Width(70)))
                        StartEdit(null);
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{ServerUrl.Replace("https://", "")}  ·  {_config.projectId}", _dimStyle);
                if (IsLoggedIn && GUILayout.Button("Выйти", EditorStyles.toolbarButton, GUILayout.Width(56)))
                {
                    _token = "";
                    EditorPrefs.DeleteKey(TOKEN_KEY);
                    EditorPrefs.DeleteKey(PASS_KEY);
                }
            }
        }

        private void DrawLogin()
        {
            EditorGUILayout.Space(20);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(340)))
                {
                    GUILayout.Label("Вход (superuser PocketBase)", _headerStyle);
                    EditorGUILayout.Space(4);
                    _email = EditorGUILayout.TextField("Email", _email);
                    _password = EditorGUILayout.PasswordField("Пароль", _password);
                    GUILayout.Label("Пароль запоминается — вход дальше автоматический.", _dimStyle);
                    EditorGUILayout.Space(4);
                    using (new EditorGUI.DisabledScope(_busy || string.IsNullOrEmpty(_email) || string.IsNullOrEmpty(_password)))
                        if (GUILayout.Button(_busy ? "Вход…" : "Войти", GUILayout.Height(26)))
                            _ = Login(_password);
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, _status.StartsWith("Ошибка") ? MessageType.Error : MessageType.Info);
        }

        // ── Карточка эксперимента ──

        private void DrawExperiment(Experiment x)
        {
            // Rect от BeginVertical в Layout-пассе нулевой, в Repaint — готовый:
            // фон рисуем в Repaint ДО контента, иначе он закрасит текст
            var card = EditorGUILayout.BeginVertical();
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(card.x + 6, card.y, card.width - 12, card.height), x.active ? ActiveTint : CardBg);
                EditorGUI.DrawRect(new Rect(card.x + 6, card.y, card.width - 12, 1), CardBorder);
                EditorGUI.DrawRect(new Rect(card.x + 6, card.yMax - 1, card.width - 12, 1), CardBorder);
                if (x.active)
                    EditorGUI.DrawRect(new Rect(card.x + 6, card.y, 3, card.height), Green);
            }
            GUILayout.Space(2);

            using (new EditorGUILayout.VerticalScope(new GUIStyle { padding = new RectOffset(12, 12, 8, 10) }))
            {
                // Шапка: имя + бейдж + заметка …… кнопки
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(x.name, _titleStyle, GUILayout.ExpandWidth(false));
                    if (x.active) DrawChip("ACTIVE", Green, Color.black);
                    if (!string.IsNullOrEmpty(x.note))
                        GUILayout.Label("· " + x.note, _dimStyle, GUILayout.ExpandWidth(false));
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(x.active ? "Выключить" : "Включить", EditorStyles.miniButtonLeft, GUILayout.Width(80)))
                        _ = SetActive(x, !x.active);
                    if (GUILayout.Button("Изменить", EditorStyles.miniButtonMid, GUILayout.Width(70)))
                        StartEdit(x);
                    var gc = GUI.contentColor; GUI.contentColor = RedBtn;
                    if (GUILayout.Button("Удалить", EditorStyles.miniButtonRight, GUILayout.Width(64)) &&
                        EditorUtility.DisplayDialog("Удалить эксперимент?",
                            $"«{x.name}» будет удалён. Назначения игроков останутся в истории.", "Удалить", "Отмена"))
                        _ = Delete(x);
                    GUI.contentColor = gc;
                }
                EditorGUILayout.Space(6);

                // Варианты: контроль первой строкой, зебра
                DrawVariantRow(0, "1", "контроль — все неназначенные", CountOf(x, "1"), null, true);
                for (int i = 0; i < x.variants.Length; i++)
                {
                    var v = x.variants[i];
                    string size = v.quota > 0
                        ? $"квота {CountOf(x, v.id)} / {v.quota}" + (v.fillChance < 1f ? $" · набор {Mathf.RoundToInt(v.fillChance * 100)}%" : "")
                        : $"вес {v.weight / 100f:0.#}% игроков";
                    DrawVariantRow(i + 1, v.id, size, CountOf(x, v.id), v.overrides, false);
                }
            }

            GUILayout.Space(2);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        private void DrawVariantRow(int index, string id, string size, int assigned, KV[] overrides, bool isControl)
        {
            var row = EditorGUILayout.BeginHorizontal(new GUIStyle { padding = new RectOffset(2, 2, 3, 3) });
            if (Event.current.type == EventType.Repaint && index % 2 == 1)
                EditorGUI.DrawRect(row, ZebraTint);

            DrawChip("вариант " + id, isControl ? CodeBg : ChipBg, Pro ? Color.white : Color.black, 74);
            GUILayout.Label(size, isControl ? _dimStyle : EditorStyles.miniLabel, GUILayout.Width(210));
            GUILayout.Label($"назначено {assigned}", _dimStyle, GUILayout.Width(100));

            if (isControl)
                GUILayout.Label("дефолтный баланс", _dimStyle);
            else if (overrides == null || overrides.Length == 0)
                GUILayout.Label("без оверрайдов — играет как «1»", _dimStyle);
            else
                GUILayout.Label(OverridesToText(overrides), _codeStyle);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawChip(string text, Color bg, Color fg, float width = 0)
        {
            var content = new GUIContent(text);
            if (width <= 0) width = _chipStyle.CalcSize(content).x + 12;
            var r = GUILayoutUtility.GetRect(width, 16, GUILayout.Width(width));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, bg);
                var s = new GUIStyle(_chipStyle);
                s.normal.textColor = fg;
                s.Draw(r, content, false, false, false, false);
            }
        }

        // ── Форма ──

        private void DrawForm()
        {
            var card = EditorGUILayout.BeginVertical();
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(card.x + 6, card.y, card.width - 12, card.height), CardBg);
                EditorGUI.DrawRect(new Rect(card.x + 6, card.y, 3, card.height), ChipBg);
            }
            using (new EditorGUILayout.VerticalScope(new GUIStyle { padding = new RectOffset(12, 12, 10, 10) }))
            {
                GUILayout.Label(_editingId == "" ? "Новый эксперимент" : $"Изменить: {_formName}", _headerStyle);
                EditorGUILayout.Space(4);
                _formName = EditorGUILayout.TextField("Имя", _formName);
                _formNote = EditorGUILayout.TextField("Заметка", _formNote);
                _formActive = EditorGUILayout.Toggle(new GUIContent("Активировать сразу",
                    "Включит этот эксперимент и выключит текущий активный"), _formActive);

                EditorGUILayout.Space(6);
                GUILayout.Label("Варианты («1» не задаётся — это контроль)", EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(2);

                int remove = -1;
                for (int i = 0; i < _formVariants.Count; i++)
                {
                    var fv = _formVariants[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("вариант", _dimStyle, GUILayout.Width(50));
                        fv.id = EditorGUILayout.TextField(fv.id, GUILayout.Width(34));
                        fv.isQuota = EditorGUILayout.Popup(fv.isQuota ? 1 : 0,
                            new[] { "вес, % игроков", "квота, человек" }, GUILayout.Width(118)) == 1;
                        fv.value = EditorGUILayout.TextField(fv.value, GUILayout.Width(54));
                        if (fv.isQuota)
                        {
                            GUILayout.Label(new GUIContent("набор,%",
                                "Шанс взять нового игрока в группу; меньше 100 — набор растянется по времени"),
                                _dimStyle, GUILayout.Width(48));
                            fv.chance = EditorGUILayout.TextField(fv.chance, GUILayout.Width(38));
                        }
                        GUILayout.Space(8);
                        GUILayout.Label(new GUIContent("оверрайды",
                            "Что вариант меняет в игре: «ключ=значение; …», ключи — из белого списка клиента"),
                            _dimStyle, GUILayout.Width(62));
                        fv.overrides = EditorGUILayout.TextField(fv.overrides);
                        var gc = GUI.contentColor; GUI.contentColor = RedBtn;
                        if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) remove = i;
                        GUI.contentColor = gc;
                    }
                }
                if (remove >= 0) _formVariants.RemoveAt(remove);

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ вариант", EditorStyles.miniButton, GUILayout.Width(80)))
                        _formVariants.Add(new FormVariant { id = NextFreeId() });
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Отмена", GUILayout.Width(80), GUILayout.Height(22))) _editingId = null;
                    if (GUILayout.Button("Сохранить", GUILayout.Width(110), GUILayout.Height(22))) _ = Save();
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        // ─────────────────────────────────────────────────────────────
        // Логика формы
        // ─────────────────────────────────────────────────────────────

        private void StartEdit(Experiment x)
        {
            _editingId = x?.id ?? "";
            _formName = x?.name ?? "";
            _formNote = x?.note ?? "";
            _formActive = x?.active ?? false;
            _formVariants.Clear();
            if (x != null)
                foreach (var v in x.variants)
                    _formVariants.Add(new FormVariant
                    {
                        id = v.id,
                        isQuota = v.quota > 0,
                        value = v.quota > 0 ? v.quota.ToString() : (v.weight / 100f).ToString("0.##"),
                        chance = v.quota > 0 && v.fillChance < 1f ? Mathf.RoundToInt(v.fillChance * 100).ToString() : "",
                        overrides = OverridesToText(v.overrides),
                    });
            if (_formVariants.Count == 0) _formVariants.Add(new FormVariant());
        }

        private string NextFreeId()
        {
            var used = new HashSet<string> { "1" };
            foreach (var fv in _formVariants) used.Add(fv.id?.Trim());
            for (int n = 2; ; n++)
                if (!used.Contains(n.ToString())) return n.ToString();
        }

        private static string OverridesToText(KV[] pairs)
        {
            if (pairs == null || pairs.Length == 0) return "";
            var sb = new StringBuilder();
            foreach (var p in pairs)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(p.k).Append('=').Append(p.v.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static KV[] OverridesFromText(string text)
        {
            var list = new List<KV>();
            foreach (var part in (text ?? "").Split(';'))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0) continue;
                var k = part.Substring(0, eq).Trim();
                var vs = part.Substring(eq + 1).Trim();
                if (k.Length > 0 && float.TryParse(vs, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float v))
                    list.Add(new KV { k = k, v = v });
            }
            return list.ToArray();
        }

        private static int CountOf(Experiment x, string id)
        {
            foreach (var c in x.counts)
                if (c.id == id) return c.n;
            return 0;
        }

        // ─────────────────────────────────────────────────────────────
        // Метрики по вариантам: таблица
        // ─────────────────────────────────────────────────────────────

        private void DrawStats()
        {
            var card = EditorGUILayout.BeginVertical();
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(card.x + 6, card.y, card.width - 12, card.height), CardBg);

            using (new EditorGUILayout.VerticalScope(new GUIStyle { padding = new RectOffset(12, 12, 10, 10) }))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Метрики по вариантам", _headerStyle);
                    GUILayout.FlexibleSpace();

                    // Селектор набора — когда наборов больше одного
                    if (_metricSets.Count > 1)
                    {
                        int setIdx = Mathf.Max(0, _metricSets.FindIndex(s => s.id == _currentSetId));
                        var names = new string[_metricSets.Count];
                        for (int i = 0; i < _metricSets.Count; i++) names[i] = _metricSets[i].name;
                        int newIdx = EditorGUILayout.Popup(setIdx, names, GUILayout.Width(140));
                        if (newIdx != setIdx)
                        {
                            _currentSetId = _metricSets[newIdx].id;
                            _ = LoadStats();
                        }
                    }

                    int daysIdx = EditorGUILayout.Popup(_statsDaysIndex,
                        new[] { "7 дней", "30 дней", "90 дней" }, GUILayout.Width(90));
                    if (daysIdx != _statsDaysIndex) { _statsDaysIndex = daysIdx; _ = LoadStats(); }

                    if (GUILayout.Button("⟳", EditorStyles.miniButton, GUILayout.Width(26))) _ = LoadStats();
                    if (GUILayout.Button("Настроить…", EditorStyles.miniButton, GUILayout.Width(86)))
                        StartMetricsEdit();
                }
                EditorGUILayout.Space(4);

                if (_stats.Count == 0)
                {
                    GUILayout.Label("Заездов ещё нет. Каждый run_end из игры (билд или редактор) " +
                                    "появится здесь с разбивкой по вариантам.", _dimStyle);
                }
                else
                {
                    var cols = new List<MetricDef>();
                    foreach (var m in _statsMetrics) if (m.enabled) cols.Add(m);

                    string lastSlice = null;
                    int rowIndex = 0;
                    foreach (var row in _stats)
                    {
                        if (row.slice != lastSlice)
                        {
                            lastSlice = row.slice;
                            rowIndex = 0;
                            EditorGUILayout.Space(6);
                            GUILayout.Label(SliceLabel(row.slice), EditorStyles.miniBoldLabel);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Label("вариант", _dimStyle, GUILayout.Width(64));
                                GUILayout.Label("игроков", _dimStyle, GUILayout.Width(56));
                                GUILayout.Label("заездов", _dimStyle, GUILayout.Width(56));
                                foreach (var m in cols)
                                    GUILayout.Label(m.label, _dimStyle, GUILayout.Width(92));
                            }
                        }

                        var r = EditorGUILayout.BeginHorizontal(new GUIStyle { padding = new RectOffset(0, 0, 2, 2) });
                        if (Event.current.type == EventType.Repaint && rowIndex % 2 == 1)
                            EditorGUI.DrawRect(r, ZebraTint);
                        rowIndex++;

                        DrawChip(row.variant, ChipBg, Pro ? Color.white : Color.black, 56);
                        GUILayout.Space(8);
                        GUILayout.Label(row.players.ToString(), EditorStyles.miniLabel, GUILayout.Width(56));
                        GUILayout.Label(row.runs.ToString(), EditorStyles.miniLabel, GUILayout.Width(56));
                        foreach (var m in cols)
                            GUILayout.Label(FormatMetric(ValueOf(row, m.key), m.format),
                                            EditorStyles.miniLabel, GUILayout.Width(92));
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        private string SliceLabel(string slice)
        {
            var project = _config.projectId;
            if (slice == project) return "Релизный билд";
            var suffix = slice.Length > project.Length + 1 ? slice.Substring(project.Length + 1) : slice;
            return suffix.Replace("demo", "Демо").Replace("playtest", "Плейтест").Replace("editor", "Unity Editor")
                         .Replace(".", " · ");
        }

        private static float ValueOf(StatRow row, string key)
        {
            foreach (var kv in row.values)
                if (kv.k == key) return kv.v;
            return 0f;
        }

        private static string FormatMetric(float v, string format)
        {
            switch (format)
            {
                case "pct":    return Mathf.RoundToInt(v * 100f) + "%";
                case "min":    return (v / 60f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " мин";
                case "float1": return v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                default:       return Mathf.RoundToInt(v).ToString();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Метрики по вариантам: редактор наборов
        // ─────────────────────────────────────────────────────────────

        private static readonly string[] MetricTypes = { "pct", "avg", "sum", "ratio" };
        private static readonly string[] MetricFormats = { "int", "float1", "pct", "min" };
        private static readonly string[] MetricFormatNames = { "целое", "0.0", "%", "сек → мин" };

        private void StartMetricsEdit()
        {
            _editSets.Clear();
            foreach (var s in _metricSets)
            {
                var copy = new MetricSet { id = s.id, name = s.name };
                var list = new List<MetricDef>();
                foreach (var m in s.metrics)
                    list.Add(new MetricDef { key = m.key, label = m.label, type = m.type, field = m.field,
                                             field2 = m.field2, equals = m.equals, format = m.format, enabled = m.enabled });
                copy.metrics = list.ToArray();
                _editSets.Add(copy);
            }
            if (_editSets.Count == 0) _editSets.Add(new MetricSet());
            _editSetIdx = Mathf.Max(0, _editSets.FindIndex(s => s.id == _currentSetId));
            _editingMetrics = true;
        }

        private void DrawMetricsEditor()
        {
            var card = EditorGUILayout.BeginVertical();
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(card.x + 6, card.y, card.width - 12, card.height), CardBg);
                EditorGUI.DrawRect(new Rect(card.x + 6, card.y, 3, card.height), ChipBg);
            }

            using (new EditorGUILayout.VerticalScope(new GUIStyle { padding = new RectOffset(12, 12, 10, 10) }))
            {
                GUILayout.Label("Наборы метрик", _headerStyle);
                GUILayout.Label("«Поле» — поле события run_end из игры. pct — доля заездов, где поле равно значению; " +
                                "avg — среднее; sum — сумма; ratio — отношение сумм двух полей. Новая метрика = " +
                                "новое поле в run_end + строка здесь.", _dimStyle);
                EditorGUILayout.Space(6);

                // Вкладки наборов
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = 0; i < _editSets.Count; i++)
                    {
                        var style = i == _editSetIdx ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                        var gc = GUI.backgroundColor;
                        if (i == _editSetIdx) GUI.backgroundColor = ChipBg;
                        if (GUILayout.Button(_editSets[i].name, style, GUILayout.Width(110)))
                            _editSetIdx = i;
                        GUI.backgroundColor = gc;
                    }
                    if (GUILayout.Button("+ набор", EditorStyles.miniButton, GUILayout.Width(70)))
                    {
                        _editSets.Add(new MetricSet { id = "set" + DateTime.UtcNow.Ticks.ToString("x"), name = "Новый набор" });
                        _editSetIdx = _editSets.Count - 1;
                    }
                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.Space(4);

                var cur = _editSets[_editSetIdx];
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("название набора", _dimStyle, GUILayout.Width(100));
                    cur.name = EditorGUILayout.TextField(cur.name, GUILayout.Width(180));
                    if (_editSets.Count > 1)
                    {
                        var gc = GUI.contentColor; GUI.contentColor = RedBtn;
                        if (GUILayout.Button("Удалить набор", EditorStyles.miniButton, GUILayout.Width(100)) &&
                            EditorUtility.DisplayDialog("Удалить набор?", $"«{cur.name}» будет удалён.", "Удалить", "Отмена"))
                        {
                            _editSets.RemoveAt(_editSetIdx);
                            _editSetIdx = 0;
                            GUI.contentColor = gc;
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndVertical();
                            return;   // список изменился — дорисуем в следующем кадре
                        }
                        GUI.contentColor = gc;
                    }
                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.Space(6);

                var metrics = new List<MetricDef>(cur.metrics);
                int remove = -1;
                for (int i = 0; i < metrics.Count; i++)
                {
                    var m = metrics[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        m.enabled = EditorGUILayout.Toggle(m.enabled, GUILayout.Width(18));
                        m.key = EditorGUILayout.TextField(m.key, GUILayout.Width(110));
                        m.label = EditorGUILayout.TextField(m.label, GUILayout.Width(100));
                        int t = Mathf.Max(0, Array.IndexOf(MetricTypes, m.type));
                        m.type = MetricTypes[EditorGUILayout.Popup(t, MetricTypes, GUILayout.Width(56))];
                        m.field = EditorGUILayout.TextField(m.field, GUILayout.Width(100));
                        if (m.type == "pct")
                            m.equals = EditorGUILayout.TextField(m.equals, GUILayout.Width(80));
                        else if (m.type == "ratio")
                            m.field2 = EditorGUILayout.TextField(m.field2, GUILayout.Width(80));
                        else
                            GUILayout.Space(84);
                        int f = Mathf.Max(0, Array.IndexOf(MetricFormats, m.format));
                        m.format = MetricFormats[EditorGUILayout.Popup(f, MetricFormatNames, GUILayout.Width(76))];
                        var gc = GUI.contentColor; GUI.contentColor = RedBtn;
                        if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) remove = i;
                        GUI.contentColor = gc;
                        GUILayout.FlexibleSpace();
                    }
                }
                if (remove >= 0) metrics.RemoveAt(remove);
                cur.metrics = metrics.ToArray();

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(18);
                    GUILayout.Label("вкл · ключ · подпись · агрегация · поле · значение/поле2 · формат", _dimStyle);
                }
                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ метрика", EditorStyles.miniButton, GUILayout.Width(80)))
                    {
                        var list = new List<MetricDef>(cur.metrics) { new MetricDef() };
                        cur.metrics = list.ToArray();
                    }
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Отмена", GUILayout.Width(80), GUILayout.Height(22))) _editingMetrics = false;
                    if (GUILayout.Button("Сохранить", GUILayout.Width(100), GUILayout.Height(22))) _ = SaveMetrics();
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        // ─────────────────────────────────────────────────────────────
        // Сеть
        // ─────────────────────────────────────────────────────────────

        private async Task Login(string password)
        {
            _busy = true; _status = "Вход…"; Repaint();
            try
            {
                var body = $"{{\"identity\":{Quote(_email)},\"password\":{Quote(password)}}}";
                var resp = await Http.PostAsync($"{ServerUrl}/api/collections/_superusers/auth-with-password",
                    new StringContent(body, Encoding.UTF8, "application/json"));
                var text = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode) throw new Exception($"{(int)resp.StatusCode}: {text}");
                _token = JsonUtility.FromJson<AuthResponse>(text)?.token ?? "";
                if (string.IsNullOrEmpty(_token)) throw new Exception("сервер не вернул токен");
                EditorPrefs.SetString(TOKEN_KEY, _token);
                EditorPrefs.SetString(EMAIL_KEY, _email);
                SavePassword(password);
                _password = "";
                _status = "";
                await Reload();
            }
            catch (Exception ex) { _status = $"Ошибка входа: {ex.Message}"; }
            finally { _busy = false; Repaint(); }
        }

        private async Task Reload()
        {
            _busy = true; _status = ""; Repaint();
            try
            {
                var text = await Send(HttpMethod.Get,
                    $"/api/ab/experiments?project={Uri.EscapeDataString(_config.projectId)}&flat=1", null);
                var parsed = JsonUtility.FromJson<ListResponse>(text);
                _experiments = new List<Experiment>(parsed?.experiments ?? Array.Empty<Experiment>());
            }
            catch (Exception ex) { _status = $"Ошибка: {ex.Message}"; }
            finally { _busy = false; Repaint(); }

            await LoadStats();
        }

        private async Task LoadStats()
        {
            try
            {
                // Наборы — отдельным запросом: селектор должен знать все
                var setsText = await Send(HttpMethod.Get,
                    $"/api/ab/metrics?project={Uri.EscapeDataString(_config.projectId)}", null);
                var sets = JsonUtility.FromJson<MetricSetsResponse>(setsText);
                _metricSets = new List<MetricSet>(sets?.sets ?? Array.Empty<MetricSet>());
                if (_metricSets.FindIndex(s => s.id == _currentSetId) < 0)
                    _currentSetId = _metricSets.Count > 0 ? _metricSets[0].id : "";

                var text = await Send(HttpMethod.Get,
                    $"/api/ab/stats?project={Uri.EscapeDataString(_config.projectId)}" +
                    $"&days={DaysOptions[_statsDaysIndex]}&set={Uri.EscapeDataString(_currentSetId)}&flat=1", null);
                var parsed = JsonUtility.FromJson<StatsResponse>(text);
                _stats = new List<StatRow>(parsed?.stats ?? Array.Empty<StatRow>());
                _statsMetrics = parsed?.metrics ?? Array.Empty<MetricDef>();
            }
            catch (Exception ex) { _status = $"Ошибка метрик: {ex.Message}"; }
            finally { Repaint(); }
        }

        private async Task SaveMetrics()
        {
            var sb = new StringBuilder("{\"project\":").Append(Quote(_config.projectId)).Append(",\"sets\":[");
            bool firstSet = true;
            foreach (var s in _editSets)
            {
                var valid = new List<MetricDef>();
                foreach (var m in s.metrics)
                    if (!string.IsNullOrWhiteSpace(m.key) && !string.IsNullOrWhiteSpace(m.field)) valid.Add(m);
                if (valid.Count == 0) continue;

                if (!firstSet) sb.Append(',');
                firstSet = false;
                sb.Append("{\"id\":").Append(Quote(s.id)).Append(",\"name\":").Append(Quote(s.name)).Append(",\"metrics\":[");
                for (int i = 0; i < valid.Count; i++)
                {
                    var m = valid[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"key\":").Append(Quote(m.key.Trim()))
                      .Append(",\"label\":").Append(Quote(string.IsNullOrWhiteSpace(m.label) ? m.key : m.label.Trim()))
                      .Append(",\"type\":").Append(Quote(m.type))
                      .Append(",\"field\":").Append(Quote(m.field.Trim()))
                      .Append(",\"field2\":").Append(Quote(m.type == "ratio" ? m.field2?.Trim() ?? "" : ""))
                      .Append(",\"equals\":").Append(Quote(m.type == "pct" ? m.equals?.Trim() ?? "" : ""))
                      .Append(",\"format\":").Append(Quote(m.format))
                      .Append(",\"enabled\":").Append(m.enabled ? "true" : "false")
                      .Append('}');
                }
                sb.Append("]}");
            }
            sb.Append("]}");
            if (firstSet) { _status = "Ошибка: нужен хотя бы один набор с метрикой (ключ и поле)"; return; }

            _busy = true; Repaint();
            try
            {
                await Send(HttpMethod.Post, "/api/ab/metrics", sb.ToString());
                _editingMetrics = false;
                _status = "";
            }
            catch (Exception ex) { _status = $"Ошибка сохранения метрик: {ex.Message}"; }
            finally { _busy = false; Repaint(); }

            await LoadStats();
        }

        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(_formName)) { _status = "Ошибка: имя эксперимента обязательно"; return; }

            var sb = new StringBuilder();
            var seen = new HashSet<string>();
            foreach (var fv in _formVariants)
            {
                var id = fv.id?.Trim() ?? "";
                if (!float.TryParse(fv.value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float value) || value <= 0)
                    continue;
                if (id == "" ) { _status = "Ошибка: у варианта не заполнено имя"; return; }
                if (id == "1") { _status = "Ошибка: имя «1» занято контрольной группой"; return; }
                if (!seen.Add(id)) { _status = "Ошибка: имена вариантов повторяются"; return; }

                float chancePct = 0;
                float.TryParse(fv.chance, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out chancePct);

                if (sb.Length > 0) sb.Append(',');
                sb.Append('{').Append("\"id\":").Append(Quote(id));
                if (fv.isQuota)
                {
                    sb.Append(",\"quota\":").Append(Mathf.RoundToInt(value));
                    sb.Append(",\"fillChance\":").Append(
                        (chancePct > 0 && chancePct < 100 ? chancePct / 100f : 1f)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                    sb.Append(",\"weight\":").Append(Mathf.RoundToInt(value * 100)); // % → корзины из 10000

                sb.Append(",\"overrides\":[");
                var pairs = OverridesFromText(fv.overrides);
                for (int i = 0; i < pairs.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"k\":").Append(Quote(pairs[i].k)).Append(",\"v\":")
                      .Append(pairs[i].v.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('}');
                }
                sb.Append("]}");
            }
            if (sb.Length == 0) { _status = "Ошибка: нужен хотя бы один вариант с именем и размером"; return; }

            var body = new StringBuilder("{");
            if (!string.IsNullOrEmpty(_editingId)) body.Append("\"id\":").Append(Quote(_editingId)).Append(',');
            body.Append("\"project\":").Append(Quote(_config.projectId))
                .Append(",\"name\":").Append(Quote(_formName.Trim()))
                .Append(",\"note\":").Append(Quote(_formNote?.Trim() ?? ""))
                .Append(",\"active\":").Append(_formActive ? "true" : "false")
                .Append(",\"variants\":[").Append(sb).Append("]}");

            _busy = true; Repaint();
            try
            {
                await Send(HttpMethod.Post, "/api/ab/experiments", body.ToString());
                _editingId = null;
                _status = "";
                await Reload();
            }
            catch (Exception ex) { _status = $"Ошибка сохранения: {ex.Message}"; _busy = false; Repaint(); }
        }

        private async Task SetActive(Experiment x, bool active)
        {
            // Сохраняем эксперимент как есть, меняя только active — сервер сам
            // погасит остальные активные этого проекта
            StartEdit(x);
            _formActive = active;
            await Save();
            _editingId = null;
        }

        private async Task Delete(Experiment x)
        {
            _busy = true; Repaint();
            try
            {
                await Send(HttpMethod.Post, "/api/ab/experiments/delete", $"{{\"id\":{Quote(x.id)}}}");
                await Reload();
            }
            catch (Exception ex) { _status = $"Ошибка удаления: {ex.Message}"; _busy = false; Repaint(); }
        }

        private async Task<string> Send(HttpMethod method, string path, string jsonBody)
        {
            for (int attempt = 0; ; attempt++)
            {
                var req = new HttpRequestMessage(method, ServerUrl + path);
                req.Headers.TryAddWithoutValidation("Authorization", _token);
                if (jsonBody != null) req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var resp = await Http.SendAsync(req);
                var text = await resp.Content.ReadAsStringAsync();

                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    // Токен протух — один тихий перелогин сохранёнными кредами
                    if (attempt == 0 && HasStoredPassword && await TryRelogin()) continue;
                    _token = "";
                    EditorPrefs.DeleteKey(TOKEN_KEY);
                    throw new Exception("токен протух — войдите заново");
                }
                if (!resp.IsSuccessStatusCode) throw new Exception($"{(int)resp.StatusCode}: {text}");
                return text;
            }
        }

        private async Task<bool> TryRelogin()
        {
            try
            {
                var body = $"{{\"identity\":{Quote(_email)},\"password\":{Quote(LoadPassword())}}}";
                var resp = await Http.PostAsync($"{ServerUrl}/api/collections/_superusers/auth-with-password",
                    new StringContent(body, Encoding.UTF8, "application/json"));
                if (!resp.IsSuccessStatusCode) return false;
                var text = await resp.Content.ReadAsStringAsync();
                var token = JsonUtility.FromJson<AuthResponse>(text)?.token;
                if (string.IsNullOrEmpty(token)) return false;
                _token = token;
                EditorPrefs.SetString(TOKEN_KEY, _token);
                return true;
            }
            catch { return false; }
        }

        private static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s ?? "")
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            return sb.Append('"').ToString();
        }
    }
}
