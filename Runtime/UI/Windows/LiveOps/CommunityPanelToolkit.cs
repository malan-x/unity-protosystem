// Packages/com.protosystem.core/Runtime/UI/Windows/LiveOps/CommunityPanelToolkit.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using ProtoSystem.LiveOps;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Community Panel на UI Toolkit — контроллер над VisualElement-деревом,
    /// порт CommunityPanelWindow (uGUI). Не MonoBehaviour: окно-хозяин
    /// (например, главное меню) инстанциирует шаблон CommunityPanel.uxml
    /// в свой контейнер и создаёт контроллер в OnBuildUI:
    /// <code>
    /// _communityPanel?.Dispose();
    /// communityPanelTemplate.CloneTree(slot);
    /// _communityPanel = new CommunityPanelToolkit(slot, communityStubConfig);
    /// </code>
    /// и обязательно вызывает Dispose() в OnHide/OnDestroy (дерево окна
    /// пересоздаётся при каждом Show — контроллер одноразовый).
    ///
    /// Данные — через Evt.LiveOps.DataUpdated; видимостью управляет
    /// LiveOpsSystem через ILiveOpsPanel (RegisterPanel).
    /// Стилизация — CommunityPanel.uss (переменные --cp-*), проект
    /// переопределяет их в USS окна-хозяина.
    /// </summary>
    public class CommunityPanelToolkit : ILiveOpsPanel
    {
        #region Constants

        private const int   MessageMaxChars     = 120;
        private const int   AnimationDurationMs = 250;

        // Те же ключи PlayerPrefs, что у uGUI-версии — состояние общее для бэкендов
        private const string PrefKeyPanelExpanded = "liveops_panel_expanded";
        private const string PrefKeySeenCards     = "liveops_seen_cards";
        private const string PrefKeyNotifyAck     = "liveops_notify_ack";
        private const string PrefKeyLaunchCount   = "liveops_launch_count";

        #endregion

        #region Elements

        private readonly VisualElement _root;

        private VisualElement _expandedRoot;
        private VisualElement _panelBody;       // = cardsRoot в uGUI (вся верхняя панель)
        private VisualElement _cardHeader, _cardContent, _cardSeparator;
        private Button _prevButton, _nextButton;
        private Label  _cardCounter;

        // Карточка карусели и закреплённая под панелью — одна вёрстка, общий код отрисовки
        private readonly CardView _cards  = new();
        private readonly CardView _pinned = new();

        private VisualElement _messageRow;
        private TextField _messageInput;
        private Label  _inputPlaceholder, _charCount;
        private Button _sendButton, _convButton;
        private VisualElement _notificationBadge;
        private Label  _notificationCount;

        private VisualElement _conversationRoot;
        private Button _convBackButton, _translationToggle;
        private Label  _convTitle, _translationLang, _convEmpty;
        private VisualElement _convMessages;
        private ScrollView _convScroll;

        private VisualElement _collapsedRow;
        private Button _expandButton, _collapseButton;
        private Label  _statusText, _summaryText;

        private VisualElement _goalRoot, _goalFill;
        private Label  _goalDesc, _goalCount;

        private VisualElement _ratingRoot;
        private Button _ratingStars;
        private Label  _ratingLabel, _ratingVersion, _ratingValue, _ratingAvg;
        private readonly List<Label> _starLabels = new();
        private int _ratingPreview;
        private bool _pointerOverStars;   // курсор/палец над звёздами — фокус не сбрасывает выбор

        /// <summary>
        /// Элементы, в которые рисуется карточка. Два набора: карусель (cp-*) и
        /// закреплённая под панелью (cp-pinned-*); имена внутри наборов совпадают.
        /// </summary>
        private sealed class CardView
        {
            public VisualElement root;           // контейнер набора (у карусели не нужен)
            public VisualElement pollCard, annCard, devLogCard;
            public Label  pollQuestion;
            public VisualElement pollOptions;
            public Label  annTitle, annBody;
            public Button annUrlButton;
            public string annUrl;                // ссылка показанной новости
            public Label  devLogFocus, devLogTitle, devLogDesc;
            public VisualElement devLogItems;
            public Label  meta;
            public Label  badge;                 // бейдж типа — только у карусели
            public string pollLayout;            // опрос и варианты, под которые собраны кнопки (см. ShowPollCard)
        }

        #endregion

        #region State (зеркало uGUI-версии)

        private List<LiveOpsPoll>         _polls         = new();
        private List<LiveOpsAnnouncement> _announcements = new();
        private LiveOpsDevLog             _devLog;
        private LiveOpsContentOrder       _contentOrder;
        private LiveOpsMilestoneData      _milestone;

        private readonly List<string> _cardKeys = new();
        private string _pinnedKey;   // ключ закреплённой карточки ("poll:id" и т.п.), null — нет
        private int  _currentCard;
        private int  _userVote;
        private bool _showLocalizedReply = true;

        private bool _isExpanded;
        private bool _isAnimating;
        private bool _dataLoaded;
        private bool _conversationOpen;
        private int  _launchCount;
        private readonly HashSet<string> _seenCardIds = new();

        private LiveOpsSystem     _liveOpsSystem;
        private LiveOpsStubConfig _stubConfig;
        private IVisualElementScheduledItem _connectPoll;
        private bool _disposed;

        #endregion

        #region Lifecycle

        public CommunityPanelToolkit(VisualElement host, LiveOpsStubConfig stubConfig = null)
        {
            _root = host.Q("community-panel") ?? host;
            _stubConfig = stubConfig;

            BindElements();
            RegisterCallbacks();

            // Начальная видимость (как Awake в uGUI)
            SetVisible(_panelBody,         false);
            SetVisible(_messageRow,        false);
            SetVisible(_pinned.root,       false);
            SetVisible(_goalRoot,          false);
            SetVisible(_ratingRoot,        false);
            SetVisible(_notificationBadge, false);
            SetVisible(_conversationRoot,  false);

            _launchCount = PlayerPrefs.GetInt(PrefKeyLaunchCount, 0) + 1;
            PlayerPrefs.SetInt(PrefKeyLaunchCount, _launchCount);
            PlayerPrefs.Save();

            _isExpanded = PlayerPrefs.GetInt(PrefKeyPanelExpanded, 0) == 1;
            LoadSeenCards();

            ApplyStaticTexts();
            if (_ratingVersion != null) _ratingVersion.text = $"v{Application.version}";
            if (_statusText != null)
                _statusText.text = L(UIKeys.CommunityPanel.Loading, UIKeys.CommunityPanel.Fallback.Loading);

            ApplyExpandedState(animate: false);

            EventBus.Subscribe(Evt.LiveOps.DataUpdated, OnLiveOpsDataUpdated);
            EventBus.Subscribe(EventBus.Localization.Ready, OnLanguageChanged);
            EventBus.Subscribe(EventBus.Localization.LanguageChanged, OnLanguageChanged);

            // Как Start() в uGUI: stub → напрямую, иначе подключение к системе
            if (_stubConfig != null)
                ApplyStubConfig(_stubConfig);
            else
                ConnectToSystem();
        }

        /// <summary>Отписаться от событий и системы. Обязательно при пересоздании дерева окна.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            EventBus.Unsubscribe(Evt.LiveOps.DataUpdated, OnLiveOpsDataUpdated);
            EventBus.Unsubscribe(EventBus.Localization.Ready, OnLanguageChanged);
            EventBus.Unsubscribe(EventBus.Localization.LanguageChanged, OnLanguageChanged);

            _connectPoll?.Pause();
            _connectPoll = null;

            if (_liveOpsSystem != null)
            {
                _liveOpsSystem.UnregisterPanel(this);
                _liveOpsSystem.OnUnreadCountChanged -= UpdateNotificationBadge;
                _liveOpsSystem = null;
            }
        }

        /// <summary>ILiveOpsPanel: LiveOpsSystem управляет видимостью панели.</summary>
        public void SetPanelVisible(bool visible) =>
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        private void ConnectToSystem()
        {
            var mgr = SystemInitializationManager.Instance;
            LiveOpsLog.Info($"[CommunityPanelToolkit] Connect: manager={mgr != null}, isInitialized={mgr?.IsInitialized}");

            if (mgr != null && mgr.IsInitialized)
            {
                AttachSystem(mgr.GetSystem<LiveOpsSystem>());
                return;
            }

            // Аналог WaitForLiveOpsSystem-корутины: опрос через планировщик панели
            _connectPoll = _root.schedule.Execute(() =>
            {
                var m = SystemInitializationManager.Instance;
                if (m == null || !m.IsInitialized) return;
                _connectPoll?.Pause();
                _connectPoll = null;
                AttachSystem(m.GetSystem<LiveOpsSystem>());
            }).Every(200);
        }

        private void AttachSystem(LiveOpsSystem system)
        {
            if (_disposed) return;
            LiveOpsLog.Info($"[CommunityPanelToolkit] GetSystem → {(system != null ? "OK" : "NULL")}");

            if (system == null)
            {
                Debug.LogWarning("[CommunityPanelToolkit] LiveOpsSystem не найден!");
                return;
            }

            _liveOpsSystem = system;
            _liveOpsSystem.RegisterPanel(this);
            _liveOpsSystem.OnUnreadCountChanged += UpdateNotificationBadge;
        }

        #endregion

        #region Bind

        private void BindElements()
        {
            _expandedRoot     = _root.Q("cp-expanded");
            _panelBody        = _root.Q("cp-panel-body");
            _cardHeader       = _root.Q("cp-card-header");
            _cardContent      = _root.Q("cp-card-content");
            _cardSeparator    = _panelBody?.Q(className: "cp-separator");
            _prevButton       = _root.Q<Button>("cp-prev-button");
            _nextButton       = _root.Q<Button>("cp-next-button");
            _cardCounter      = _root.Q<Label>("cp-card-counter");

            BindCardView(_cards, "cp-");
            _cards.meta   = _root.Q<Label>("cp-card-meta");
            _cards.badge  = _root.Q<Label>("cp-type-badge");

            // Шаблон проекта без блока cp-pinned — root == null: закреплённая карточка
            // остаётся в карусели, как у клиентов без поддержки закрепа
            BindCardView(_pinned, "cp-pinned-");
            _pinned.root  = _root.Q("cp-pinned");
            _pinned.meta  = _root.Q<Label>("cp-pinned-meta");

            _messageRow       = _root.Q("cp-message-row");
            _messageInput     = _root.Q<TextField>("cp-message-input");
            _inputPlaceholder = _root.Q<Label>("cp-input-placeholder");
            _charCount        = _root.Q<Label>("cp-char-count");
            _sendButton       = _root.Q<Button>("cp-send-button");
            _convButton       = _root.Q<Button>("cp-conv-button");
            _notificationBadge = _root.Q("cp-notification-badge");
            _notificationCount = _root.Q<Label>("cp-notification-count");

            _conversationRoot  = _root.Q("cp-conversation");
            _convBackButton    = _root.Q<Button>("cp-conv-back-button");
            _convTitle         = _root.Q<Label>("cp-conv-title");
            _translationLang   = _root.Q<Label>("cp-translation-lang");
            _translationToggle = _root.Q<Button>("cp-translation-toggle");
            _convEmpty         = _root.Q<Label>("cp-conv-empty");
            _convMessages      = _root.Q("cp-conv-messages");
            _convScroll        = _root.Q<ScrollView>("cp-conv-scroll");

            _collapsedRow   = _root.Q("cp-collapsed-row");
            _expandButton   = _root.Q<Button>("cp-expand-button");
            _collapseButton = _root.Q<Button>("cp-collapse-button");
            _statusText     = _root.Q<Label>("cp-status-text");
            _summaryText    = _root.Q<Label>("cp-summary-text");

            _goalRoot  = _root.Q("cp-goal");
            _goalDesc  = _root.Q<Label>("cp-goal-desc");
            _goalFill  = _root.Q("cp-goal-fill");
            _goalCount = _root.Q<Label>("cp-goal-count");

            ApplyGoalArt();   // градиент заливки + свечение на её конце (текстуры из кода)

            _ratingRoot    = _root.Q("cp-rating");
            _ratingLabel   = _root.Q<Label>("cp-rating-label");
            _ratingVersion = _root.Q<Label>("cp-rating-version");
            _ratingStars   = _root.Q<Button>("cp-rating-stars");
            _ratingValue   = _root.Q<Label>("cp-rating-value");
            _ratingAvg     = _root.Q<Label>("cp-rating-avg");
        }

        /// <summary>Элементы карточек набора: prefix + "poll-card", "ann-title" и т.д.</summary>
        private void BindCardView(CardView v, string prefix)
        {
            v.pollCard     = _root.Q(prefix + "poll-card");
            v.pollQuestion = _root.Q<Label>(prefix + "poll-question");
            v.pollOptions  = _root.Q(prefix + "poll-options");
            v.annCard      = _root.Q(prefix + "ann-card");
            v.annTitle     = _root.Q<Label>(prefix + "ann-title");
            v.annBody      = _root.Q<Label>(prefix + "ann-body");
            v.annUrlButton = _root.Q<Button>(prefix + "ann-url-button");
            v.devLogCard   = _root.Q(prefix + "devlog-card");
            v.devLogFocus  = _root.Q<Label>(prefix + "devlog-focus");
            v.devLogTitle  = _root.Q<Label>(prefix + "devlog-title");
            v.devLogDesc   = _root.Q<Label>(prefix + "devlog-desc");
            v.devLogItems  = _root.Q(prefix + "devlog-items");
        }

        private void RegisterCallbacks()
        {
            // Button.clicked — работает и от геймпадного Submit (в отличие от ClickEvent)
            if (_prevButton != null)     _prevButton.clicked     += OnPrevCard;
            if (_nextButton != null)     _nextButton.clicked     += OnNextCard;
            if (_sendButton != null)     _sendButton.clicked     += OnSendMessage;
            if (_convButton != null)     _convButton.clicked     += OpenConversation;
            if (_convBackButton != null) _convBackButton.clicked += CloseConversation;
            if (_translationToggle != null) _translationToggle.clicked += CycleTranslationMode;
            if (_expandButton != null)   _expandButton.clicked   += () => SetExpanded(true);
            if (_collapseButton != null) _collapseButton.clicked += () => SetExpanded(false);

            // Ссылка новости: один постоянный обработчик на набор, URL — из показанной карточки
            foreach (var view in new[] { _cards, _pinned })
            {
                var v = view;
                if (v.annUrlButton == null) continue;
                SetVisible(v.annUrlButton, false);
                v.annUrlButton.clicked += () =>
                {
                    if (!string.IsNullOrEmpty(v.annUrl))
                        Application.OpenURL(v.annUrl);
                };
            }

            // Общий обход вверх/вниз по панели (см. OnPanelNavMove)
            _root.RegisterCallback<NavigationMoveEvent>(OnPanelNavMove);

            if (_messageInput != null)
            {
                _messageInput.maxLength = MessageMaxChars;
                _messageInput.RegisterValueChangedCallback(e => OnMessageInputChanged(e.newValue));

                // Поле ввода — ДВА разных состояния, и их нельзя смешивать:
                //   фокус НА поле  — навигация проходит мимо, набор не начат, клавиатуры нет;
                //   набор В поле   — по явному Submit (A / Enter / клик мышью).
                // Раньше фокус сразу проваливался внутрь (delegatesFocus) и по FocusIn поднималась
                // системная клавиатура. На Steam Deck она вылезала просто при проходе фокуса вверх,
                // а после её закрытия фокус терялся — до кнопок переключения новостей было не дойти.
                _messageInput.delegatesFocus = false;

                _messageInput.RegisterCallback<NavigationSubmitEvent>(OnInputSubmit);

                var textInput = _messageInput.Q(TextField.textInputUssName);
                if (textInput != null)
                {
                    // Клавиатуру поднимаем только когда набор реально начался
                    textInput.RegisterCallback<FocusInEvent>(_ => TryShowVirtualKeyboard());

                    // Выйти из набора обратно на поле, не закрывая панель/окно
                    textInput.RegisterCallback<NavigationCancelEvent>(OnInputCancel);
                    textInput.RegisterCallback<KeyDownEvent>(OnInputEscape, TrickleDown.TrickleDown);
                }

                // Выход стрелками вверх/вниз из поля ввода (TextField иначе съедает их)
                _messageInput.RegisterCallback<NavigationMoveEvent>(OnInputNavMove, TrickleDown.TrickleDown);
                _messageInput.RegisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
            }
            OnMessageInputChanged("");

            BuildRatingStars();
        }

        /// <summary>
        /// Полоска звёзд — один фокусируемый контрол: влево/вправо выбирают 1–10
        /// (фокус не уходит), Submit/клик ставит оценку, вверх/вниз — обычная
        /// навигация к другим элементам. Мышь — ховер-превью по позиции курсора.
        /// </summary>
        private void BuildRatingStars()
        {
            if (_ratingStars == null) return;
            _ratingStars.text = string.Empty;
            _ratingStars.Clear();
            _starLabels.Clear();

            for (int i = 1; i <= 10; i++)
            {
                var star = new Label("★");
                star.AddToClassList("cp-star");
                star.pickingMode = PickingMode.Ignore;
                _starLabels.Add(star);
                _ratingStars.Add(star);
            }

            _ratingStars.clicked += () => SubmitRating(_ratingPreview);

            // Геймпад/клавиатура: влево/вправо — значение; вверх/вниз всплывут до корня
            // панели, где их обработает общий OnPanelNavMove
            _ratingStars.RegisterCallback<NavigationMoveEvent>(OnRatingNavMove);
            // Стартовое значение (свой голос или 5) — только для геймпада/клавиатуры.
            // Кнопка получает фокус на НАЖАТИИ мыши, между наведением и отпусканием:
            // без проверки клик без движения мыши отправлял 5, какую звезду ни нажми.
            _ratingStars.RegisterCallback<FocusInEvent>(_ =>
            {
                if (_pointerOverStars) return;
                SetRatingPreview(_userVote > 0 ? _userVote : 5);
            });
            _ratingStars.RegisterCallback<FocusOutEvent>(_ => ResetRatingPreview());

            // Мышь/тач: превью по позиции указателя, увод — откат к своему голосу.
            // Нажатие тоже берёт звезду под указателем (тап без движения, порядок
            // нажатия и фокуса не важен); TrickleDown — раньше Clickable кнопки.
            _ratingStars.RegisterCallback<PointerEnterEvent>(_ => _pointerOverStars = true);
            _ratingStars.RegisterCallback<PointerDownEvent>(e =>
            {
                _pointerOverStars = true;
                SetRatingPreview(StarFromPointer(e.position.x));
            }, TrickleDown.TrickleDown);
            _ratingStars.RegisterCallback<PointerMoveEvent>(e =>
                SetRatingPreview(StarFromPointer(e.position.x)));
            _ratingStars.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _pointerOverStars = false;
                ResetRatingPreview();
            });
        }

        #region Арт полосы цели (градиент + свечение)

        // USS не умеет linear-gradient, а класть спрайты в пакет ради двух полосок не хочется —
        // строим текстуры процедурно один раз на домен. Проект может переопределить вид,
        // задав свой background-image на .cp-goal-fill / .cp-goal-glow.
        private static Texture2D _goalGradientTex;
        private static Texture2D _goalGlowTex;

        private static readonly Color GoalFillFrom = new(0.62f, 0.24f, 0.06f);  // тёмная латунь
        private static readonly Color GoalFillTo   = new(1.00f, 0.66f, 0.25f);  // яркий янтарь

        private void ApplyGoalArt()
        {
            if (_goalFill != null)
                _goalFill.style.backgroundImage = new StyleBackground(GetGoalGradient());

            var glow = _goalFill?.Q("cp-goal-glow");
            if (glow != null)
                glow.style.backgroundImage = new StyleBackground(GetGoalGlow());
        }

        /// <summary>Горизонтальный градиент 64×1: слева тускло, справа — ярко.</summary>
        private static Texture2D GetGoalGradient()
        {
            if (_goalGradientTex != null) return _goalGradientTex;

            const int width = 64;
            var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                name = "cp-goal-gradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int x = 0; x < width; x++)
                tex.SetPixel(x, 0, Color.Lerp(GoalFillFrom, GoalFillTo, x / (float)(width - 1)));

            tex.Apply();
            return _goalGradientTex = tex;
        }

        /// <summary>Мягкий блик 32×32: яркий центр, прозрачные края (свечение на конце заливки).</summary>
        private static Texture2D GetGoalGlow()
        {
            if (_goalGlowTex != null) return _goalGlowTex;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "cp-goal-glow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);          // 0 в центре, 1 на краю
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;                                            // мягче спад
                    tex.SetPixel(x, y, new Color(1f, 0.82f, 0.45f, a));
                }
            }

            tex.Apply();
            return _goalGlowTex = tex;
        }

        #endregion

        private void OnRatingNavMove(NavigationMoveEvent evt)
        {
            int delta;
            switch (evt.direction)
            {
                case NavigationMoveEvent.Direction.Left:  delta = -1; break;
                case NavigationMoveEvent.Direction.Right: delta = +1; break;

                // Вверх/вниз с рейтинга не обрабатываем здесь — событие всплывёт до корня
                // панели, где его подхватит общий OnPanelNavMove (симметричный обход).
                default: return;
            }

            SetRatingPreview(Mathf.Clamp(_ratingPreview + delta, 1, 10));
            ConsumeRatingNav(evt);
        }

        private void ConsumeRatingNav(NavigationMoveEvent evt)
        {
            evt.StopPropagation();
#if UNITY_2023_2_OR_NEWER
            _ratingStars.focusController?.IgnoreEvent(evt);
#else
            evt.PreventDefault();
#endif
        }

        #region Навигация внутри панели (вверх/вниз по порядку дерева)

        /// <summary>
        /// Up/Down внутри панели — явный обход видимых focusable по порядку дерева.
        ///
        /// Штатная навигация UITK пространственная и на этой вёрстке работает плохо:
        /// полоска рейтинга шире соседей, кнопки лежат в разных секциях (карточки,
        /// строка ввода, свёрнутая строка, рейтинг) — переходы находились через раз
        /// и были несимметричны (вниз с рейтинга уводило, обратно вверх — нет).
        ///
        /// На КРАЯХ панели событие НЕ перехватываем: фокус должен уметь уйти в окно-хозяин.
        /// </summary>
        private void OnPanelNavMove(NavigationMoveEvent evt)
        {
            var dir = evt.direction switch
            {
                NavigationMoveEvent.Direction.Up    => NavDir.Up,
                NavigationMoveEvent.Direction.Down  => NavDir.Down,
                NavigationMoveEvent.Direction.Left  => NavDir.Left,
                NavigationMoveEvent.Direction.Right => NavDir.Right,
                _ => NavDir.None
            };
            if (dir == NavDir.None) return;
            if (evt.target is not VisualElement focused) return;
            if (!MoveFocus(focused, dir)) return; // соседа нет — край панели, отдаём окну

            evt.StopPropagation();
#if UNITY_2023_2_OR_NEWER
            focused.focusController?.IgnoreEvent(evt);
#else
            evt.PreventDefault();
#endif
        }

        private enum NavDir { None, Up, Down, Left, Right }

        /// <summary>
        /// Сместить фокус на соседний контрол ПО ГЕОМЕТРИИ. false — соседа в эту сторону нет
        /// (край панели: событие отдаём окну-хозяину, чтобы фокус мог уйти в меню).
        ///
        /// Соседей ищем сами, а не полагаемся на FocusController: его правила непрозрачны, он
        /// цепляет служебные внутренности (скроллеры, input внутри TextField) и уводит фокус
        /// «в никуда». Здесь всё явно: вверх/вниз — ближайшая строка, влево/вправо — сосед
        /// в текущей строке.
        /// </summary>
        private bool MoveFocus(VisualElement from, NavDir dir)
        {
            var controls = FocusablesInOrder();

            int index = IndexOfOwner(controls, from);
            if (index < 0) return false;

            var origin = controls[index];
            var target = FindNeighbor(origin, controls, dir);
            if (target == null) return false;

            FocusControl(target);
            return true;
        }

        /// <summary>
        /// Ближайший сосед в направлении dir.
        /// Строка — контролы с близким центром по Y (допуск = половина высоты контрола).
        /// Влево/вправо ищем только в своей строке, вверх/вниз — в ближайшей другой строке,
        /// и там берём контрол с самым близким X (так фокус не «телепортируется» через панель).
        /// </summary>
        private static VisualElement FindNeighbor(VisualElement origin, List<VisualElement> controls, NavDir dir)
        {
            Rect from = origin.worldBound;
            float fromX = from.center.x;
            float fromY = from.center.y;
            float rowTolerance = Mathf.Max(from.height * 0.5f, 4f);

            VisualElement best = null;
            float bestPrimary = float.MaxValue;   // расстояние вдоль направления
            float bestSecondary = float.MaxValue; // отклонение поперёк

            foreach (var candidate in controls)
            {
                if (candidate == origin) continue;

                Rect rect = candidate.worldBound;
                if (rect.width <= 0f || rect.height <= 0f) continue;   // не в layout

                float dx = rect.center.x - fromX;
                float dy = rect.center.y - fromY;

                float primary, secondary;

                switch (dir)
                {
                    case NavDir.Left:
                    case NavDir.Right:
                        // Только своя строка: иначе «влево» прыгало бы в соседнюю секцию
                        if (Mathf.Abs(dy) > rowTolerance) continue;
                        if (dir == NavDir.Left && dx >= -1f) continue;
                        if (dir == NavDir.Right && dx <= 1f) continue;

                        primary = Mathf.Abs(dx);
                        secondary = Mathf.Abs(dy);
                        break;

                    case NavDir.Up:
                    case NavDir.Down:
                        // Другая строка (внутри своей строки вертикали нет)
                        if (Mathf.Abs(dy) <= rowTolerance) continue;
                        if (dir == NavDir.Up && dy >= 0f) continue;
                        if (dir == NavDir.Down && dy <= 0f) continue;

                        primary = Mathf.Abs(dy);
                        secondary = Mathf.Abs(dx);
                        break;

                    default:
                        continue;
                }

                // Ближайшая строка важнее совпадения по X: строку выбираем первой,
                // и только внутри неё — самый близкий по горизонтали контрол
                bool better = primary < bestPrimary - 1f ||
                              (Mathf.Abs(primary - bestPrimary) <= 1f && secondary < bestSecondary);

                if (!better) continue;

                best = candidate;
                bestPrimary = primary;
                bestSecondary = secondary;
            }

            return best;
        }

        /// <summary>Фокус + подкрутить ленту переписки, если контрол внутри неё.</summary>
        private void FocusControl(VisualElement control)
        {
            control.Focus();

            if (_convScroll != null && _convScroll.Contains(control))
                _convScroll.ScrollTo(control);
        }

        /// <summary>
        /// Индекс контрола, которому принадлежит сфокусированный элемент.
        ///
        /// Точное сравнение здесь не годится: составные контролы (TextField) держат фокус
        /// на ВНУТРЕННЕМ элементе ввода, и событие навигации приходит от него, а не от самого
        /// TextField. С IndexOf такой элемент в списке не находился, панель считала это своим
        /// краем и отпускала фокус в окно-хозяин — с геймпада фокус «пропадал» из поля ввода,
        /// а следующее нажатие вверх/вниз уводило в рейтинг.
        /// </summary>
        private static int IndexOfOwner(List<VisualElement> order, VisualElement focused)
        {
            if (focused == null) return -1;

            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] == focused || order[i].Contains(focused))
                    return i;
            }
            return -1;
        }

        // ── Поле ввода сообщения ──────────────────────────────────────────────
        // TextField съедает стрелки (каретка), поэтому вверх/вниз из него не выходили:
        // с кнопки «отправить» фокус попадал в поле — и застревал, до стрелок карточек
        // ‹/› было не добраться. Перехватываем ДО внутреннего input (TrickleDown):
        // NavigationMoveEvent — геймпад, KeyDownEvent — клавиатура.
        // Left/Right не трогаем: это движение каретки по тексту.

        private void OnInputNavMove(NavigationMoveEvent evt)
        {
            var dir = evt.direction switch
            {
                NavigationMoveEvent.Direction.Up    => NavDir.Up,
                NavigationMoveEvent.Direction.Down  => NavDir.Down,
                NavigationMoveEvent.Direction.Left  => NavDir.Left,
                NavigationMoveEvent.Direction.Right => NavDir.Right,
                _ => NavDir.None
            };
            if (dir == NavDir.None || !MoveFocus(_messageInput, dir)) return;

            evt.StopPropagation();
#if UNITY_2023_2_OR_NEWER
            _messageInput.focusController?.IgnoreEvent(evt);
#else
            evt.PreventDefault();
#endif
        }

        /// <summary>
        /// Submit на поле ввода — начать набор: фокус уходит внутрь, и уже это поднимает
        /// системную клавиатуру (Steam Deck). Пока фокус просто стоит на поле, клавиатуры нет.
        /// </summary>
        private void OnInputSubmit(NavigationSubmitEvent evt)
        {
            var textInput = _messageInput?.Q(TextField.textInputUssName);
            if (textInput == null) return;

            textInput.Focus();

            evt.StopPropagation();
#if UNITY_2023_2_OR_NEWER
            _messageInput.focusController?.IgnoreEvent(evt);
#else
            evt.PreventDefault();
#endif
        }

        /// <summary>
        /// Cancel во время набора — выйти обратно на поле, а НЕ отдать Back окну
        /// (иначе панель/окно закрылись бы прямо из-под набора текста).
        /// </summary>
        private void OnInputCancel(NavigationCancelEvent evt)
        {
            if (_messageInput == null) return;

            _messageInput.Focus();   // delegatesFocus=false → фокус останется на самом поле

            evt.StopPropagation();
#if UNITY_2023_2_OR_NEWER
            _messageInput.focusController?.IgnoreEvent(evt);
#else
            evt.PreventDefault();
#endif
        }

        /// <summary>Escape во время набора — то же, что Cancel (клавиатурный путь).</summary>
        private void OnInputEscape(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || _messageInput == null) return;

            _messageInput.Focus();

            evt.StopPropagation();
            evt.PreventDefault();
        }

        private void OnInputKeyDown(KeyDownEvent evt)
        {
            // Стрелки влево/вправо не трогаем: пока идёт набор, это движение каретки по тексту
            var dir = evt.keyCode switch
            {
                KeyCode.UpArrow   => NavDir.Up,
                KeyCode.DownArrow => NavDir.Down,
                _ => NavDir.None
            };
            if (dir == NavDir.None || !MoveFocus(_messageInput, dir)) return;

            evt.StopPropagation();
            evt.PreventDefault();
        }

        /// <summary>
        /// Порядок навигации: ЯВНЫЙ список контролов сверху вниз, зависящий от состояния
        /// (свёрнута / развёрнута / открыта переписка).
        ///
        /// Обходить дерево через Query&lt;VisualElement&gt;() нельзя: focusable — не только наши
        /// кнопки, но и служебные внутренности (скроллеры ScrollView, input внутри TextField).
        /// Фокус уходил на них, и с виду просто «пропадал» — выше кнопки «свернуть» было
        /// не пройти.
        /// </summary>
        private List<VisualElement> FocusablesInOrder()
        {
            var list = new List<VisualElement>();
            if (_root == null) return list;

            if (_isExpanded)
            {
                if (_conversationOpen)
                {
                    Add(_convBackButton);
                    Add(_translationToggle);

                    // Сообщения ленты — тоже контролы: их выделяют и по ним ходят вверх/вниз
                    // (лента подкручивается за фокусом, см. FocusControl)
                    if (_convMessages != null)
                        foreach (var message in _convMessages.Children())
                            Add(message);
                }
                else
                {
                    Add(_prevButton);
                    Add(_nextButton);

                    // Интерактив текущей карточки: опции опроса или ссылка объявления
                    AddCardControls(_cards);

                    Add(_messageInput);
                    Add(_sendButton);
                    Add(_convButton);
                }
            }

            // Свёрнутая строка видна всегда: одна из кнопок активна по состоянию
            Add(_expandButton);
            Add(_collapseButton);

            // Закреплённая карточка — под свёрнутой строкой, видна в обоих состояниях
            if (!_conversationOpen && IsDisplayed(_pinned.root))
                AddCardControls(_pinned);

            Add(_ratingStars);

            return list;

            void Add(VisualElement ve)
            {
                if (IsNavigable(ve)) list.Add(ve);
            }

            void AddCardControls(CardView v)
            {
                if (v.pollOptions != null)
                    foreach (var option in v.pollOptions.Children())
                        Add(option);
                Add(v.annUrlButton);
            }
        }

        private static bool IsDisplayed(VisualElement ve)
            => ve != null && ve.resolvedStyle.display != DisplayStyle.None;

        private static bool IsNavigable(VisualElement ve)
            => ve != null
               && ve.focusable
               && ve.enabledInHierarchy
               && ve.resolvedStyle.display != DisplayStyle.None
               && ve.resolvedStyle.visibility == Visibility.Visible;

        /// <summary>
        /// Вход в панель извне (окно-хозяин: Tab на группу или «влево» из меню).
        /// Фокус на первый видимый контрол — в свёрнутом виде это кнопка «развернуть».
        /// </summary>
        public void FocusEntry()
        {
            var order = FocusablesInOrder();
            if (order.Count > 0) order[0].Focus();
        }

        /// <summary>
        /// Удержать фокус в панели после смены режима (свернуть/развернуть, вход/выход
        /// из переписки): контрол, на котором стоял фокус, прячется — и фокус улетал в
        /// никуда, приходилось заново добираться табами.
        ///
        /// Перехватываем ТОЛЬКО если фокус был внутри панели (мышью её могли переключить,
        /// пока фокус в меню — тогда красть его нельзя).
        /// Отложено на кадр: до этого display ещё не переключён.
        /// </summary>
        private void KeepFocusInPanel(VisualElement preferred)
        {
            if (_root?.panel == null) return;

            var focused = _root.focusController?.focusedElement as VisualElement;
            if (focused == null || !_root.Contains(focused)) return; // фокус не наш — не воруем

            _root.schedule.Execute(() =>
            {
                if (IsNavigable(preferred)) preferred.Focus();
                else FocusEntry();
            }).ExecuteLater(1);
        }

        private void KeepFocusAfterExpandToggle()
            => KeepFocusInPanel(_isExpanded ? (VisualElement)_collapseButton : _expandButton);

        #endregion

        /// <summary>Индекс звезды под курсором (координата панели).</summary>
        private int StarFromPointer(float panelX)
        {
            for (int i = 0; i < _starLabels.Count; i++)
                if (panelX <= _starLabels[i].worldBound.xMax)
                    return i + 1;
            return 10;
        }

        private void SetRatingPreview(int value)
        {
            _ratingPreview = value;
            UpdateRatingDisplay(value);
        }

        private void ResetRatingPreview()
        {
            _ratingPreview = _userVote;
            UpdateRatingDisplay(_userVote);
        }

        #endregion

        #region Stub

        /// <summary>
        /// Применить stub-конфиг напрямую, минуя EventBus и LiveOpsSystem
        /// (превью без сервера). Можно вызывать в любой момент.
        /// </summary>
        public void ApplyStubConfig(LiveOpsStubConfig stub)
        {
            if (stub == null) return;
            _stubConfig = stub;

            SetVisible(_panelBody,  stub.showCards);
            SetVisible(_messageRow, stub.showMessages);
            SetVisible(_ratingRoot, stub.showRating);

            _polls.Clear();
            _announcements.Clear();
            _devLog = null;
            _contentOrder = BuildStubContentOrder(stub.cards);

            if (stub.cards != null)
            {
                foreach (var entry in stub.cards)
                {
                    switch (entry.type)
                    {
                        case StubCardType.Poll:         _polls.Add(entry.poll.ToLiveOpsPoll()); break;
                        case StubCardType.Announcement: _announcements.Add(entry.announcement.ToAnnouncement()); break;
                        case StubCardType.DevLog:       _devLog = entry.devLog.ToDevLog(); break;
                    }
                }
            }

            RebuildCardList();
            ShowCard(0);
            RefreshCarouselParts();

            _milestone = stub.showGoal ? stub.goal.ToMilestone() : null;
            if (_milestone != null) RefreshGoal(_milestone);
            RefreshSlot();

            if (stub.showRating) RefreshRating(stub.rating.ToRatingData());
        }

        /// <summary>
        /// Порядок из stub-карточек — только если какая-то закреплена; иначе null,
        /// и карусель собирается по типам, как раньше.
        /// </summary>
        private static LiveOpsContentOrder BuildStubContentOrder(StubCardEntry[] cards)
        {
            if (cards == null || !System.Array.Exists(cards, c => c != null && c.pinned)) return null;

            var entries = new List<LiveOpsContentOrderEntry>();
            foreach (var c in cards)
            {
                if (c == null) continue;
                switch (c.type)
                {
                    case StubCardType.Poll:
                        entries.Add(new LiveOpsContentOrderEntry { type = "poll", id = c.poll.id, pinned = c.pinned });
                        break;
                    case StubCardType.Announcement:
                        entries.Add(new LiveOpsContentOrderEntry { type = "announcement", id = c.announcement.id, pinned = c.pinned });
                        break;
                    case StubCardType.DevLog:
                        entries.Add(new LiveOpsContentOrderEntry { type = "devlog", id = c.devLog.id, pinned = c.pinned });
                        break;
                }
            }
            return new LiveOpsContentOrder { order = entries.ToArray() };
        }

        #endregion

        #region EventBus

        private void OnLiveOpsDataUpdated(object payload)
        {
            if (payload is not LiveOpsDataPayload data) return;

            // Система ещё инициализируется (панель ждёт её в ConnectToSystem): язык не
            // известен — карточки вышли бы на английском, нет конфига виджетов и состояния
            // опросов. Подключаясь, RegisterPanel отдаст всё разом — без перерисовки на глазах
            if (_liveOpsSystem == null && _stubConfig == null) return;

            switch (data.Type)
            {
                case LiveOpsDataType.PanelConfig:
                    RefreshWidgetVisibility();
                    break;

                case LiveOpsDataType.Polls when data.Data is List<LiveOpsPoll> polls:
                    _polls = polls;
                    OnCardsDataChanged();
                    break;

                case LiveOpsDataType.Announcements when data.Data is List<LiveOpsAnnouncement> ann:
                    _announcements = ann;
                    OnCardsDataChanged();
                    break;

                case LiveOpsDataType.DevLog when data.Data is LiveOpsDevLog devLog:
                    _devLog = devLog;
                    OnCardsDataChanged();
                    break;

                case LiveOpsDataType.ContentOrder when data.Data is LiveOpsContentOrder order:
                    _contentOrder = order;
                    OnCardsDataChanged();
                    break;

                case LiveOpsDataType.Milestone when data.Data is LiveOpsMilestoneData milestone:
                    _milestone = milestone;
                    RefreshGoal(milestone);
                    RefreshSlot();   // место цели может быть занято закреплённой карточкой
                    _dataLoaded = true;
                    UpdateSummary();
                    break;

                case LiveOpsDataType.Rating when data.Data is LiveOpsRatingData rating:
                    RefreshRating(rating);
                    // Рейтинг показываем только со 2-го запуска
                    SetVisible(_ratingRoot, _launchCount >= 2);
                    _dataLoaded = true;
                    UpdateSummary();
                    break;
            }

            NotifyLayoutChanged("data_updated");
        }

        private void OnCardsDataChanged()
        {
            RebuildCardList();
            ShowCard(_currentCard);
            RefreshCardsVisibility();
            RefreshSlot();
            _dataLoaded = true;
            UpdateSummary();
        }

        private void OnLanguageChanged(object _)
        {
            ApplyStaticTexts();
            if (_dataLoaded)
            {
                ShowCard(_currentCard);
                UpdateSummary();
                if (_milestone != null) RefreshGoal(_milestone);
                RefreshSlot();   // перерисовать закреплённую карточку на новом языке
            }
            if (_conversationOpen)
            {
                UpdateTranslationUI();
                RenderConversation();
            }
        }

        #endregion

        #region Widget Visibility

        private void RefreshWidgetVisibility()
        {
            RefreshSlot();
            if (_liveOpsSystem == null) return;
            RefreshCardsVisibility();
            SetVisible(_messageRow, _liveOpsSystem.IsWidgetVisible("messages"));
            SetVisible(_ratingRoot, _liveOpsSystem.IsWidgetVisible("rating") && _liveOpsSystem.Rating != null && _launchCount >= 2);
        }

        private void RefreshCardsVisibility()
        {
            RefreshCarouselParts();
            if (_liveOpsSystem == null) return;

            // Всё содержимое закреплено — карусель пуста, но тело панели остаётся
            // ради строки сообщения разработчикам (она внутри тела)
            bool hasContent = _cardKeys.Count > 0 || _pinnedKey != null;
            SetVisible(_panelBody, _liveOpsSystem.IsWidgetVisible("cards") && hasContent);
        }

        /// <summary>Шапка, карточка и разделитель карусели — только когда в ней есть карточки.</summary>
        private void RefreshCarouselParts()
        {
            bool any = _cardKeys.Count > 0;
            SetVisible(_cardHeader,    any);
            SetVisible(_cardContent,   any);
            SetVisible(_cards.meta,    any);
            SetVisible(_cardSeparator, any);
        }

        /// <summary>Виджет панели включён (stub — флаги ассета; без системы — да).</summary>
        private bool WidgetVisible(string key)
        {
            if (_stubConfig != null)
                return key switch
                {
                    "cards" => _stubConfig.showCards,
                    "goal"  => _stubConfig.showGoal,
                    _       => true
                };
            return _liveOpsSystem == null || _liveOpsSystem.IsWidgetVisible(key);
        }

        #endregion

        #region Slot: закреплённая карточка или цель

        /// <summary>
        /// Полоса под свёрнутой строкой — одно место: карточка, закреплённая в дашборде,
        /// иначе цель. Пока открыта переписка, полоса скрыта целиком.
        /// </summary>
        private void RefreshSlot()
        {
            bool pinnedShown = !_conversationOpen
                               && _pinnedKey != null
                               && WidgetVisible("cards")
                               && ShowCardIn(_pinned, _pinnedKey);
            SetVisible(_pinned.root, pinnedShown);

            if (pinnedShown)
            {
                // Пустая строка мета (у новости и девлога) дала бы лишний отступ снизу
                SetVisible(_pinned.meta, !string.IsNullOrEmpty(_pinned.meta?.text));
                // Карточка на виду — снятая с закрепа не всплывёт в карусели «новой»
                MarkCardSeen(_pinnedKey);
            }

            SetVisible(_goalRoot, !_conversationOpen && !pinnedShown
                                  && _milestone != null && WidgetVisible("goal"));
        }

        /// <summary>
        /// Ключ закреплённой карточки — если она среди загруженных (активных, не истёкших)
        /// и в шаблоне есть блок cp-pinned. Иначе null: элемент остаётся в карусели.
        /// Опрос, за который игрок голосовал и с тех пор начинал заезд, тоже остаётся
        /// в карусели (LiveOpsSystem.PollLeftSlot) — первым, там можно переголосовать.
        /// </summary>
        private string ResolvePinnedKey()
        {
            var e = _contentOrder?.Pinned;
            if (e == null || _pinned.root == null) return null;

            switch (e.type)
            {
                case "poll":
                    var poll = _polls.Find(p => p.id == e.id);
                    if (poll == null || (_liveOpsSystem != null && _liveOpsSystem.PollLeftSlot(poll))) return null;
                    return $"poll:{e.id}";
                case "announcement":
                    return _announcements.Exists(a => a.id == e.id) ? $"announcement:{e.id}" : null;
                case "devlog":
                    return _devLog != null && (_devLog.id == e.id || string.IsNullOrEmpty(e.id)) ? "devlog" : null;
                default:
                    return null;
            }
        }

        #endregion

        #region Cards Carousel

        private void RebuildCardList()
        {
            _cardKeys.Clear();
            // Закреплённая карточка стоит под панелью — в карусель её не берём
            _pinnedKey = ResolvePinnedKey();

            if (_contentOrder != null && _contentOrder.order.Length > 0)
            {
                foreach (var entry in _contentOrder.order)
                {
                    switch (entry.type)
                    {
                        case "poll":
                            if (_polls.Exists(p => p.id == entry.id))
                                AddKey($"poll:{entry.id}");
                            break;
                        case "announcement":
                            if (_announcements.Exists(a => a.id == entry.id))
                                AddKey($"announcement:{entry.id}");
                            break;
                        case "devlog":
                            if (_devLog != null && (_devLog.id == entry.id || string.IsNullOrEmpty(entry.id)))
                                AddKey("devlog");
                            break;
                    }
                }
            }
            else
            {
                foreach (var poll in _polls)        AddKey($"poll:{poll.id}");
                foreach (var ann in _announcements) AddKey($"announcement:{ann.id}");
                if (_devLog != null)                AddKey("devlog");
            }

            _currentCard = Mathf.Clamp(_currentCard, 0, Mathf.Max(0, _cardKeys.Count - 1));

            void AddKey(string key)
            {
                if (key != _pinnedKey) _cardKeys.Add(key);
            }
        }

        private void ShowCard(int index)
        {
            if (_cardKeys.Count == 0) { HideCards(_cards); UpdateCardCounter(0, 0); return; }

            index = Mathf.Clamp(index, 0, _cardKeys.Count - 1);
            _currentCard = index;
            var key = _cardKeys[index];

            ShowCardIn(_cards, key);

            UpdateCardCounter(index + 1, _cardKeys.Count);
            MarkCardSeen(key);
            NotifyLayoutChanged("card_changed");
        }

        /// <summary>Нарисовать карточку по ключу в набор. false — данных по ключу нет, набор пуст.</summary>
        private bool ShowCardIn(CardView view, string key)
        {
            HideCards(view);
            var lang = CurrentLang;

            if (key.StartsWith("poll:"))
            {
                var poll = _polls.Find(p => p.id == key.Substring(5));
                if (poll == null) return false;
                ShowPollCard(view, poll, lang);
                return true;
            }
            if (key.StartsWith("announcement:"))
            {
                var ann = _announcements.Find(a => a.id == key.Substring(13));
                if (ann == null) return false;
                ShowAnnouncementCard(view, ann, lang);
                return true;
            }
            if (key == "devlog" && _devLog != null)
            {
                ShowDevLogCard(view, _devLog, lang);
                return true;
            }
            return false;
        }

        private static void HideCards(CardView view)
        {
            SetVisible(view.pollCard,   false);
            SetVisible(view.annCard,    false);
            SetVisible(view.devLogCard, false);
        }

        private string CurrentLang => _stubConfig?.language ?? _liveOpsSystem?.Language ?? "en";

        private void ShowPollCard(CardView view, LiveOpsPoll poll, string lang)
        {
            SetVisible(view.pollCard, true);
            if (view.pollQuestion != null)
                view.pollQuestion.text = L($"liveops.poll.{poll.id}.q", poll.question.Get(lang));

            if (view.meta != null)
                view.meta.text = poll.votesTotal > 0 ? $"{poll.votesTotal:N0} votes" : "";

            bool isMulti = poll.pollType == "multi";
            SetBadge(view,
                isMulti ? "type_poll_multi" : "type_poll",
                isMulti ? UIKeys.CommunityPanel.Fallback.TypePollMulti : UIKeys.CommunityPanel.Fallback.TypePoll);

            if (view.pollOptions == null) return;

            // Голос и перезапросы (после голоса, при входе в меню, по таймеру) меняют только
            // отметки и проценты — кнопки обновляем на месте. Пересоздание (Clear) снимало
            // фокус, который поставил клик: рамка 2px → 1px → снова 2px кадром позже,
            // и после каждого голоса панель дёргалась. Собираем заново, только если сменился
            // опрос или набор вариантов
            var layout = new System.Text.StringBuilder(poll.id);
            foreach (var o in poll.options) layout.Append('|').Append(o.id);
            string layoutKey = layout.ToString();
            if (view.pollLayout != layoutKey || view.pollOptions.childCount != poll.options.Length)
                BuildPollOptions(view, poll, layoutKey);

            bool hasVoted = System.Array.Exists(poll.options, o => o.selected);
            int idx = 0;
            foreach (var btn in view.pollOptions.Children())
            {
                var opt = poll.options[idx];
                FillPollOption(btn, opt, L($"liveops.poll.{poll.id}.opt.{idx}", opt.label.Get(lang)),
                               hasVoted, poll.votesTotal);
                idx++;
            }
        }

        /// <summary>Пересоздать кнопки вариантов (пустые — заполняет FillPollOption).</summary>
        private void BuildPollOptions(CardView view, LiveOpsPoll poll, string layoutKey)
        {
            view.pollLayout = layoutKey;

            // Clear() уничтожает кнопки вместе с фокусом — вернём его на ту же строку
            int focusedOption = FocusedPollOptionIndex(view);
            view.pollOptions.Clear();

            string pollId = poll.id;
            foreach (var opt in poll.options)
            {
                string optId = opt.id;
                var btn = new Button(() =>
                {
                    // Кнопки переживают перезапрос, а он приносит новые объекты опросов —
                    // голосуем в актуальном, а не в том, с которым собирали кнопки
                    var current = _polls.Find(p => p.id == pollId) ?? poll;
                    if (_liveOpsSystem != null) OptimisticPollVote(view, current, optId);
                    else                        ToggleStubPollSelection(view, optId, current);
                });
                btn.AddToClassList("cp-poll-option");
                btn.text = string.Empty;

                AddPart(btn, new VisualElement(), "cp-poll-fill");   // полоса — после голосования, ширина = процент
                AddPart(btn, new Label(), "cp-poll-check");
                AddPart(btn, new Label(), "cp-poll-label");
                AddPart(btn, new Label(), "cp-poll-pct");
                view.pollOptions.Add(btn);
            }

            RestorePollOptionFocus(view, focusedOption);

            static void AddPart(VisualElement parent, VisualElement part, string className)
            {
                part.AddToClassList(className);
                part.pickingMode = PickingMode.Ignore;
                parent.Add(part);
            }
        }

        /// <summary>Отметка, подпись, полоса и процент варианта.</summary>
        private static void FillPollOption(VisualElement btn, LiveOpsPollOption opt, string label,
                                           bool hasVoted, int votesTotal)
        {
            float percent = opt.Percent(votesTotal);

            if (btn.Q<Label>(className: "cp-poll-check") is { } check)
                check.text = opt.selected ? "✓" : "";
            if (btn.Q<Label>(className: "cp-poll-label") is { } text)
                text.text = label;
            if (btn.Q<Label>(className: "cp-poll-pct") is { } pct)
                pct.text = hasVoted ? $"{percent:0}%" : "";
            if (btn.Q(className: "cp-poll-fill") is { } fill)
            {
                fill.style.display = hasVoted ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasVoted) fill.style.width = Length.Percent(percent);
            }
        }

        /// <summary>Индекс опции опроса набора, на которой сейчас фокус (-1 — фокус не в этом опросе).</summary>
        private int FocusedPollOptionIndex(CardView view)
        {
            if (view.pollOptions == null) return -1;
            if (_root?.focusController?.focusedElement is not VisualElement focused) return -1;

            int index = 0;
            foreach (var option in view.pollOptions.Children())
            {
                if (option == focused || option.Contains(focused)) return index;
                index++;
            }
            return -1;
        }

        /// <summary>
        /// Вернуть фокус на опцию опроса после перерисовки (кнопки — новые объекты).
        /// Отложено на кадр: до этого новые элементы ещё без layout, Focus() не сработает.
        /// </summary>
        private void RestorePollOptionFocus(CardView view, int index)
        {
            if (index < 0 || view.pollOptions == null || _root?.panel == null) return;

            _root.schedule.Execute(() =>
            {
                int i = 0;
                foreach (var option in view.pollOptions.Children())
                {
                    if (i++ != index) continue;
                    if (IsNavigable(option)) option.Focus();
                    return;
                }
            }).ExecuteLater(1);
        }

        private void ShowAnnouncementCard(CardView view, LiveOpsAnnouncement ann, string lang)
        {
            SetVisible(view.annCard, true);
            SetBadge(view, "type_news", UIKeys.CommunityPanel.Fallback.TypeNews);
            if (view.meta != null) view.meta.text = "";
            if (view.annTitle != null)
                view.annTitle.text = L($"liveops.ann.{ann.id}.title", ann.title.Get(lang));
            if (view.annBody != null)
                view.annBody.text = L($"liveops.ann.{ann.id}.body", ann.body.Get(lang));

            if (view.annUrlButton != null)
            {
                bool hasUrl = !string.IsNullOrEmpty(ann.url);
                SetVisible(view.annUrlButton, hasUrl);
                view.annUrlButton.text = L(UIKeys.CommunityPanel.ReadMore, UIKeys.CommunityPanel.Fallback.ReadMore);
                view.annUrl = ann.url;
            }
        }

        private void ShowDevLogCard(CardView view, LiveOpsDevLog devLog, string lang)
        {
            SetVisible(view.devLogCard, true);
            SetBadge(view, "type_devlog", UIKeys.CommunityPanel.Fallback.TypeDevLog);
            if (view.meta != null) view.meta.text = "";
            if (view.devLogFocus != null)
                view.devLogFocus.text = L("liveops.devlog.focus", devLog.focus.Get(lang));
            if (view.devLogTitle != null)
                view.devLogTitle.text = L("liveops.devlog.title", devLog.title.Get(lang));

            if (view.devLogDesc != null)
            {
                string desc = devLog.description?.Get(lang);
                SetVisible(view.devLogDesc, !string.IsNullOrEmpty(desc));
                if (!string.IsNullOrEmpty(desc))
                    view.devLogDesc.text = L("liveops.devlog.description", desc);
            }

            if (view.devLogItems == null) return;
            view.devLogItems.Clear();

            for (int idx = 0; idx < devLog.items.Length; idx++)
            {
                var item = devLog.items[idx];

                var row = new VisualElement();
                row.AddToClassList("cp-devlog-item");

                var box = new Label(item.IsDone ? "✓" : "");
                box.AddToClassList("cp-devlog-box");
                row.Add(box);

                var label = new Label(L($"liveops.devlog.item.{idx}", item.name.Get(lang)));
                label.AddToClassList("cp-devlog-label");
                label.AddToClassList(item.IsDone ? "cp-devlog-done"
                                   : item.IsWip  ? "cp-devlog-wip"
                                                 : "cp-devlog-todo");
                row.Add(label);

                view.devLogItems.Add(row);
            }
        }

        private void OnPrevCard() =>
            ShowCard(_cardKeys.Count > 0 ? (_currentCard - 1 + _cardKeys.Count) % _cardKeys.Count : 0);

        private void OnNextCard() =>
            ShowCard(_cardKeys.Count > 0 ? (_currentCard + 1) % _cardKeys.Count : 0);

        private void UpdateCardCounter(int current, int total)
        {
            if (_cardCounter != null)
                _cardCounter.text = total > 0 ? $"{current}/{total}" : "";
        }

        #endregion

        #region Message

        private void OnMessageInputChanged(string value)
        {
            int len = value?.Length ?? 0;
            if (_charCount != null) _charCount.text = $"{len}/{MessageMaxChars}";
            _sendButton?.SetEnabled(len > 0 && len <= MessageMaxChars);
            if (_inputPlaceholder != null)
                _inputPlaceholder.style.display = len == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private async void OnSendMessage()
        {
            if (_messageInput == null) return;
            var text = _messageInput.value?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            // category = контекст (какая карточка сейчас открыта; карусель пуста —
            // закреплённая под панелью, она тогда единственная на виду)
            var category = _cardKeys.Count > 0 && _currentCard < _cardKeys.Count
                ? _cardKeys[_currentCard]
                : _pinnedKey ?? "general";

            _sendButton?.SetEnabled(false);
            if (_liveOpsSystem != null)
            {
                await _liveOpsSystem.SubmitFeedbackAsync(text, category);
                await _liveOpsSystem.FetchMyMessagesAsync();
            }
            if (_disposed) return;
            _messageInput.value = "";
            _sendButton?.SetEnabled(true);
        }

        private void TryShowVirtualKeyboard()
        {
            if (_messageInput == null) return;
            VirtualKeyboard.TryShow(_messageInput.value, MessageMaxChars, result =>
            {
                if (result != null && !_disposed && _messageInput != null)
                    _messageInput.value = result;
            });
        }

        #endregion

        #region Goal

        private void RefreshGoal(LiveOpsMilestoneData data)
        {
            var lang = _stubConfig?.language ?? _liveOpsSystem?.Language ?? "en";
            LiveOpsLog.Info($"[CommunityPanelToolkit] RefreshGoal: {data.current}/{data.goal} Progress={data.Progress:F4}");

            float progress = Mathf.Clamp01(data.Progress);
            if (_goalFill != null)
            {
                _goalFill.style.width = Length.Percent(progress * 100f);

                // Блик на конце заливки: при нулевом прогрессе он висел бы в пустоте у левого края
                var glow = _goalFill.Q("cp-goal-glow");
                if (glow != null)
                    glow.style.display = progress > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_goalCount != null)
                _goalCount.text = $"{data.current:N0} / {data.goal:N0}";
            if (_goalDesc != null)
            {
                var title = data.title.Get(lang);
                var desc  = data.description.Get(lang);
                var text  = string.IsNullOrEmpty(title) ? desc : $"{title}\n{desc}";
                _goalDesc.text = L("liveops.goal.desc", text);
            }
        }

        #endregion

        #region Rating

        private async void SubmitRating(int score)
        {
            if (score <= 0) return;
            _userVote = score;
            UpdateRatingDisplay(score);
            if (_liveOpsSystem != null)
                await _liveOpsSystem.SubmitRatingAsync(score);
        }

        private void RefreshRating(LiveOpsRatingData data)
        {
            _userVote = data.userVote;
            if (_ratingAvg != null) _ratingAvg.text = $"{data.avg:F1}";
            UpdateRatingDisplay(_userVote);
        }

        private void UpdateRatingDisplay(int value)
        {
            for (int i = 0; i < _starLabels.Count; i++)
                _starLabels[i].EnableInClassList("cp-star-on", i < value);
            if (_ratingValue != null)
                _ratingValue.text = value > 0 ? value.ToString() : "—";
        }

        #endregion

        #region Conversation

        private void UpdateNotificationBadge(int count)
        {
            LiveOpsLog.Info($"[CommunityPanelToolkit] UpdateNotificationBadge({count})");
            SetVisible(_notificationBadge, count > 0);
            if (_notificationCount != null) _notificationCount.text = count.ToString();
        }

        private void OpenConversation()
        {
            LiveOpsLog.Info("[CommunityPanelToolkit] OpenConversation()");
            _conversationOpen = true;

            SetVisible(_panelBody,  false);
            SetVisible(_pinned.root, false);
            SetVisible(_goalRoot,   false);
            SetVisible(_ratingRoot, false);
            SetVisible(_conversationRoot, true);

            _liveOpsSystem?.MarkAllRepliesRead();

            UpdateTranslationUI();
            RenderConversation();
            NotifyLayoutChanged("conversation_open");

            // Кнопка «Сообщения» спряталась вместе с телом панели — увести фокус на «назад»
            KeepFocusInPanel(_convBackButton);
        }

        private void CloseConversation()
        {
            LiveOpsLog.Info("[CommunityPanelToolkit] CloseConversation()");
            _conversationOpen = false;
            SetVisible(_conversationRoot, false);

            if (_stubConfig != null)
            {
                ApplyStubConfig(_stubConfig);
            }
            else
            {
                RefreshWidgetVisibility();
                ShowCard(_currentCard);
            }
            NotifyLayoutChanged("conversation_close");

            // Вернуться туда, откуда уходили — на кнопку «Сообщения»
            KeepFocusInPanel(_convButton);
        }

        private void CycleTranslationMode()
        {
            _showLocalizedReply = !_showLocalizedReply;
            UpdateTranslationUI();
            RenderConversation();
        }

        private void UpdateTranslationUI()
        {
            string lang = _liveOpsSystem != null ? _liveOpsSystem.Language : "??";
            if (_translationLang != null)
                _translationLang.text = (lang ?? "??").ToUpper();
            if (_translationToggle != null)
                _translationToggle.text = _showLocalizedReply ? lang?.ToUpper() ?? "??" : "Aa";
        }

        private string GetDisplayReply(LiveOpsConversationItem item)
        {
            if (item == null) return "";

            if (_showLocalizedReply && item.replyLocalized != null)
            {
                string lang = _liveOpsSystem != null ? _liveOpsSystem.Language : "en";
                string localized = item.replyLocalized.Get(lang);
                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }
            return item.reply ?? "";
        }

        private void RenderConversation()
        {
            if (_convMessages == null) return;
            _convMessages.Clear();

            var items = _liveOpsSystem != null ? _liveOpsSystem.MyMessages : null;
            bool empty = items == null || items.Count == 0;
            SetVisible(_convEmpty, empty);
            if (_convEmpty != null && empty)
                _convEmpty.text = "No messages yet";
            if (empty) return;

            // От новых к старым
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];

                var row = new VisualElement();
                row.AddToClassList("cp-conv-item");
                row.focusable = true;   // сообщения выделяются и участвуют в навигации панели

                var msg = new Label(item.message ?? "");
                msg.AddToClassList("cp-conv-msg");
                row.Add(msg);

                var metaRow = new VisualElement();
                metaRow.style.flexDirection = FlexDirection.Row;
                metaRow.style.justifyContent = Justify.SpaceBetween;

                var time = new Label(FormatTimestamp(item.timestamp));
                time.AddToClassList("cp-conv-time");
                metaRow.Add(time);

                var cat = new Label(FormatCategory(item.category));
                cat.AddToClassList("cp-conv-cat");
                metaRow.Add(cat);
                row.Add(metaRow);

                if (!string.IsNullOrEmpty(item.reply))
                {
                    var replyRow = new VisualElement();
                    replyRow.AddToClassList("cp-conv-reply-row");
                    var reply = new Label(GetDisplayReply(item));
                    reply.AddToClassList("cp-conv-reply");
                    replyRow.Add(reply);
                    row.Add(replyRow);
                }

                _convMessages.Add(row);
            }
        }

        private static string FormatTimestamp(string iso)
        {
            if (System.DateTime.TryParse(iso, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
            return iso ?? "";
        }

        private static string FormatCategory(string cat)
        {
            if (string.IsNullOrEmpty(cat) || cat == "general") return "";
            int colon = cat.IndexOf(':');
            return colon > 0 ? cat.Substring(0, colon) : cat;
        }

        #endregion

        #region Collapsed / Expanded

        private void SetExpanded(bool expanded)
        {
            if (_isAnimating || _isExpanded == expanded) return;
            _isExpanded = expanded;
            PlayerPrefs.SetInt(PrefKeyPanelExpanded, expanded ? 1 : 0);
            PlayerPrefs.Save();

            if (!expanded) MarkCurrentCardsAsSeen();

            ApplyExpandedState(animate: true);
            KeepFocusAfterExpandToggle();
        }

        private void ApplyExpandedState(bool animate)
        {
            if (_expandedRoot == null) return;

            if (animate && _root.panel != null)
            {
                AnimateExpandCollapse(_isExpanded);
                return;
            }

            // Мгновенное переключение
            _expandedRoot.style.display = _isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            _expandedRoot.style.maxHeight = StyleKeyword.None;
            FinishExpandedStateChange();
        }

        private void AnimateExpandCollapse(bool toExpanded)
        {
            _isAnimating = true;
            var er = _expandedRoot;
            UpdateButtons();

            if (toExpanded)
            {
                // Показать невидимым, дождаться layout, измерить натуральную высоту
                er.style.visibility = Visibility.Hidden;
                er.style.maxHeight = StyleKeyword.None;
                er.style.display = DisplayStyle.Flex;

                EventCallback<GeometryChangedEvent> onGeometry = null;
                onGeometry = _ =>
                {
                    er.UnregisterCallback(onGeometry);
                    float h = Mathf.Max(er.resolvedStyle.height, 1f);
                    er.style.maxHeight = 0;
                    er.style.visibility = Visibility.Visible;
                    er.experimental.animation
                        .Start(0f, h, AnimationDurationMs, (e, v) => e.style.maxHeight = v)
                        .OnCompleted(() =>
                        {
                            er.style.maxHeight = StyleKeyword.None;
                            FinishExpandedStateChange();
                        });
                };
                er.RegisterCallback(onGeometry);
            }
            else
            {
                float h = Mathf.Max(er.resolvedStyle.height, 1f);
                er.experimental.animation
                    .Start(h, 0f, AnimationDurationMs, (e, v) => e.style.maxHeight = v)
                    .OnCompleted(() =>
                    {
                        er.style.display = DisplayStyle.None;
                        er.style.maxHeight = StyleKeyword.None;
                        FinishExpandedStateChange();
                    });
            }
        }

        private void FinishExpandedStateChange()
        {
            _isAnimating = false;
            UpdateButtons();
            UpdateSummary();
            NotifyLayoutChanged(_isExpanded ? "expanded" : "collapsed");
        }

        /// <summary>Развёрнуто: только CollapseButton. Свёрнуто: всё кроме CollapseButton.</summary>
        private void UpdateButtons()
        {
            SetVisible(_expandButton,   !_isExpanded);
            SetVisible(_collapseButton, _isExpanded);
            SetVisible(_summaryText,    !_isExpanded);
            SetVisible(_statusText,     !_isExpanded);
        }

        private void UpdateSummary()
        {
            if (_summaryText == null) return;

            if (!_dataLoaded)
            {
                _summaryText.text = "";
                return;
            }

            // Кнопка «Оповестить игроков» в дашборде могла попросить перечитать
            ApplyNotifyReset();

            int newCards = 0;
            foreach (var key in _cardKeys)
            {
                string cardId = ExtractCardId(key);
                if (!string.IsNullOrEmpty(cardId) && !_seenCardIds.Contains(cardId))
                    newCards++;
            }

            // Общее число сообщений видно всегда: без него свёрнутая панель с
            // прочитанными карточками выглядела пустой, хотя контент внутри есть
            int total = _cardKeys.Count;
            string totalText = total > 0
                ? string.Format(L(UIKeys.CommunityPanel.TotalCards, UIKeys.CommunityPanel.Fallback.TotalCards), total)
                : "";

            if (newCards > 0)
            {
                string template = L(UIKeys.CommunityPanel.NewCards, UIKeys.CommunityPanel.Fallback.NewCards);
                _summaryText.text = string.Format(template, newCards)
                    + (totalText.Length > 0 ? " · " + totalText : "");
                _summaryText.style.color = new Color(1f, 0.85f, 0.3f); // яркий для новых
            }
            else if (total > 0)
            {
                _summaryText.text = totalText;
                _summaryText.style.color = new Color(0.5f, 0.5f, 0.5f); // всё прочитано
            }
            else
            {
                _summaryText.text = "";
            }

            // Скрыть статус загрузки после получения данных
            if (_statusText != null) _statusText.text = "";
        }

        /// <summary>
        /// Метка «оповестить игроков» из дашборда: если она новее локального
        /// acknowledgement — один раз помечаем все карточки непрочитанными
        /// (существующий механизм «N новых» сам подсветит панель).
        /// </summary>
        private void ApplyNotifyReset()
        {
            var notifyAt = _liveOpsSystem != null ? _liveOpsSystem.NotifyAt : null;
            if (string.IsNullOrEmpty(notifyAt)) return;
            if (PlayerPrefs.GetString(PrefKeyNotifyAck, "") == notifyAt) return;

            _seenCardIds.Clear();
            SaveSeenCards();
            PlayerPrefs.SetString(PrefKeyNotifyAck, notifyAt);
            PlayerPrefs.Save();
        }

        private void MarkCurrentCardsAsSeen()
        {
            bool changed = false;
            foreach (var key in _cardKeys)
            {
                string cardId = ExtractCardId(key);
                if (!string.IsNullOrEmpty(cardId) && _seenCardIds.Add(cardId))
                    changed = true;
            }
            if (changed) SaveSeenCards();
        }

        private void MarkCardSeen(string cardKey)
        {
            string cardId = ExtractCardId(cardKey);
            if (!string.IsNullOrEmpty(cardId) && _seenCardIds.Add(cardId))
            {
                SaveSeenCards();
                UpdateSummary();
            }
        }

        private static string ExtractCardId(string cardKey)
        {
            // "poll:abc123" → "abc123", "devlog" → "devlog"
            int colon = cardKey.IndexOf(':');
            return colon >= 0 ? cardKey.Substring(colon + 1) : cardKey;
        }

        private void LoadSeenCards()
        {
            _seenCardIds.Clear();
            var json = PlayerPrefs.GetString(PrefKeySeenCards, "");
            if (string.IsNullOrEmpty(json) || json.Length <= 2) return;
            json = json.Trim();
            if (json[0] != '[') return;
            int i = 1;
            while (i < json.Length)
            {
                while (i < json.Length && (json[i] == ' ' || json[i] == ',' || json[i] == '\n')) i++;
                if (i >= json.Length || json[i] == ']') break;
                if (json[i] == '"')
                {
                    i++;
                    int start = i;
                    while (i < json.Length && json[i] != '"') { if (json[i] == '\\') i++; i++; }
                    _seenCardIds.Add(json.Substring(start, i - start));
                    if (i < json.Length) i++;
                }
                else i++;
            }
        }

        private void SaveSeenCards()
        {
            var sb = new System.Text.StringBuilder("[");
            bool first = true;
            foreach (var id in _seenCardIds)
            {
                if (!first) sb.Append(',');
                sb.Append('"').Append(id).Append('"');
                first = false;
            }
            sb.Append(']');
            PlayerPrefs.SetString(PrefKeySeenCards, sb.ToString());
            PlayerPrefs.Save();
        }

        #endregion

        #region Poll Voting

        /// <summary>
        /// Optimistic UI: мгновенно обновляем состояние опроса и перерисовываем,
        /// затем отправляем на сервер в фоне.
        /// </summary>
        private void OptimisticPollVote(CardView view, LiveOpsPoll poll, string optId)
        {
            bool votedBefore = System.Array.Exists(poll.options, o => o.selected);
            TogglePollOption(poll, optId);
            // Первый голос: закреплённый опрос стоит на месте до старта заезда, потом уходит в карусель
            if (!votedBefore && System.Array.Exists(poll.options, o => o.selected))
                _liveOpsSystem.NotePollVoted(poll.id);

            // Перерисовать тот набор, где голосовали: закреплённый опрос не должен
            // всплыть в карусели поверх её текущей карточки
            ShowPollCard(view, poll, CurrentLang);
            // Первый голос в опросе: строка «N votes» у закреплённого появляется сразу,
            // а не с ответом сервера (пустую её прячет RefreshSlot)
            if (view == _pinned)
                SetVisible(_pinned.meta, !string.IsNullOrEmpty(_pinned.meta?.text));

            var selected = new List<string>();
            foreach (var o in poll.options)
                if (o.selected) selected.Add(o.id);

            _ = SubmitPollInBackground(poll.id, selected.ToArray());
        }

        private async Task SubmitPollInBackground(string pollId, string[] optionIds)
        {
            try
            {
                bool ok = await _liveOpsSystem.SubmitPollAnswerAsync(pollId, optionIds);
                if (ok)
                    await _liveOpsSystem.FetchAsync();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CommunityPanelToolkit] Poll vote failed: {ex.Message}");
            }
        }

        /// <summary>Тогл выбора опции в stub-режиме (локальный, без сервера).</summary>
        private void ToggleStubPollSelection(CardView view, string optId, LiveOpsPoll poll)
        {
            if (poll == null) return;
            TogglePollOption(poll, optId);
            ShowPollCard(view, poll, _stubConfig?.language ?? "en");
        }

        /// <summary>Переключить выбор опции с учётом single/multi и пересчитать голоса.</summary>
        private static void TogglePollOption(LiveOpsPoll poll, string optId)
        {
            var opt = System.Array.Find(poll.options, o => o.id == optId);
            if (opt == null) return;

            bool isSingle = poll.pollType == "single";
            bool wasSelected = opt.selected;

            if (isSingle)
            {
                foreach (var o in poll.options)
                {
                    if (o.selected) { o.votes = Mathf.Max(0, o.votes - 1); poll.votesTotal = Mathf.Max(0, poll.votesTotal - 1); }
                    o.selected = false;
                }
                if (!wasSelected)
                {
                    opt.selected = true;
                    opt.votes++;
                    poll.votesTotal++;
                }
            }
            else
            {
                opt.selected = !opt.selected;
                if (opt.selected) { opt.votes++; poll.votesTotal++; }
                else              { opt.votes = Mathf.Max(0, opt.votes - 1); poll.votesTotal = Mathf.Max(0, poll.votesTotal - 1); }
            }
        }

        #endregion

        #region Helpers

        /// <summary>Локализация с fallback (аналог LocalizeTMP: рантайм-ключи liveops.* и ui.community.*).</summary>
        private static string L(string key, string fallback) =>
            Loc.IsReady ? Loc.Get(key, fallback) : fallback;

        private void ApplyStaticTexts()
        {
            if (_expandButton != null)
                _expandButton.text = L(UIKeys.CommunityPanel.Expand, UIKeys.CommunityPanel.Fallback.Expand);
            if (_collapseButton != null)
                _collapseButton.text = L(UIKeys.CommunityPanel.Collapse, UIKeys.CommunityPanel.Fallback.Collapse);
            if (_sendButton != null)
                _sendButton.text = L(UIKeys.CommunityPanel.SendButton, UIKeys.CommunityPanel.Fallback.SendButton);
            if (_inputPlaceholder != null)
                _inputPlaceholder.text = L(UIKeys.CommunityPanel.Placeholder, UIKeys.CommunityPanel.Fallback.Placeholder);
            if (_convTitle != null)
                _convTitle.text = L(UIKeys.CommunityPanel.ConvTitle, UIKeys.CommunityPanel.Fallback.ConvTitle);
            if (_ratingLabel != null)
                _ratingLabel.text = L(UIKeys.CommunityPanel.RatingLabel, UIKeys.CommunityPanel.Fallback.RatingLabel);
        }

        private static void SetBadge(CardView view, string keySuffix, string fallback)
        {
            if (view.badge != null)
                view.badge.text = L("ui.community." + keySuffix, fallback);
        }

        private static void SetVisible(VisualElement e, bool visible)
        {
            if (e != null) e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Уведомить окно-хозяина об изменении layout (перестройка фокус-навигации и т.п.).</summary>
        private void NotifyLayoutChanged(string reason)
        {
            EventBus.Publish(Evt.LiveOps.LayoutChanged, reason);
        }

        #endregion
    }
}
