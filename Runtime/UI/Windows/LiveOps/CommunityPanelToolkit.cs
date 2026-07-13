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
        private const string PrefKeyLaunchCount   = "liveops_launch_count";

        #endregion

        #region Elements

        private readonly VisualElement _root;

        private VisualElement _expandedRoot;
        private VisualElement _panelBody;       // = cardsRoot в uGUI (вся верхняя панель)
        private VisualElement _pollCard, _annCard, _devLogCard;
        private Label  _pollQuestion;
        private VisualElement _pollOptions;
        private Label  _annTitle, _annBody;
        private Button _annUrlButton;
        private Label  _devLogFocus, _devLogTitle, _devLogDesc;
        private VisualElement _devLogItems;
        private Button _prevButton, _nextButton;
        private Label  _cardCounter, _typeBadge, _cardMeta;

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

        #endregion

        #region State (зеркало uGUI-версии)

        private List<LiveOpsPoll>         _polls         = new();
        private List<LiveOpsAnnouncement> _announcements = new();
        private LiveOpsDevLog             _devLog;
        private LiveOpsContentOrder       _contentOrder;

        private readonly List<string> _cardKeys = new();
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
            _pollCard         = _root.Q("cp-poll-card");
            _annCard          = _root.Q("cp-ann-card");
            _devLogCard       = _root.Q("cp-devlog-card");
            _pollQuestion     = _root.Q<Label>("cp-poll-question");
            _pollOptions      = _root.Q("cp-poll-options");
            _annTitle         = _root.Q<Label>("cp-ann-title");
            _annBody          = _root.Q<Label>("cp-ann-body");
            _annUrlButton     = _root.Q<Button>("cp-ann-url-button");
            _devLogFocus      = _root.Q<Label>("cp-devlog-focus");
            _devLogTitle      = _root.Q<Label>("cp-devlog-title");
            _devLogDesc       = _root.Q<Label>("cp-devlog-desc");
            _devLogItems      = _root.Q("cp-devlog-items");
            _prevButton       = _root.Q<Button>("cp-prev-button");
            _nextButton       = _root.Q<Button>("cp-next-button");
            _cardCounter      = _root.Q<Label>("cp-card-counter");
            _typeBadge        = _root.Q<Label>("cp-type-badge");
            _cardMeta         = _root.Q<Label>("cp-card-meta");

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

            _collapsedRow   = _root.Q("cp-collapsed-row");
            _expandButton   = _root.Q<Button>("cp-expand-button");
            _collapseButton = _root.Q<Button>("cp-collapse-button");
            _statusText     = _root.Q<Label>("cp-status-text");
            _summaryText    = _root.Q<Label>("cp-summary-text");

            _goalRoot  = _root.Q("cp-goal");
            _goalDesc  = _root.Q<Label>("cp-goal-desc");
            _goalFill  = _root.Q("cp-goal-fill");
            _goalCount = _root.Q<Label>("cp-goal-count");

            _ratingRoot    = _root.Q("cp-rating");
            _ratingLabel   = _root.Q<Label>("cp-rating-label");
            _ratingVersion = _root.Q<Label>("cp-rating-version");
            _ratingStars   = _root.Q<Button>("cp-rating-stars");
            _ratingValue   = _root.Q<Label>("cp-rating-value");
            _ratingAvg     = _root.Q<Label>("cp-rating-avg");
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
            if (_annUrlButton != null)   SetVisible(_annUrlButton, false);

            // Общий обход вверх/вниз по панели (см. OnPanelNavMove)
            _root.RegisterCallback<NavigationMoveEvent>(OnPanelNavMove);

            if (_messageInput != null)
            {
                _messageInput.maxLength = MessageMaxChars;
                _messageInput.RegisterValueChangedCallback(e => OnMessageInputChanged(e.newValue));
                _messageInput.RegisterCallback<FocusInEvent>(_ => TryShowVirtualKeyboard());

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
            _ratingStars.RegisterCallback<FocusInEvent>(_ =>
                SetRatingPreview(_userVote > 0 ? _userVote : 5));
            _ratingStars.RegisterCallback<FocusOutEvent>(_ => ResetRatingPreview());

            // Мышь: превью по позиции курсора, увод — откат к своему голосу
            _ratingStars.RegisterCallback<PointerMoveEvent>(e =>
                SetRatingPreview(StarFromPointer(e.position.x)));
            _ratingStars.RegisterCallback<PointerLeaveEvent>(_ => ResetRatingPreview());
        }

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
            int dir = evt.direction switch
            {
                NavigationMoveEvent.Direction.Up   => -1,
                NavigationMoveEvent.Direction.Down => +1,
                _ => 0
            };
            if (dir == 0) return;
            if (evt.target is not VisualElement focused) return;
            if (!MoveFocus(focused, dir)) return; // край панели — отдаём окну

            evt.StopPropagation();
#if UNITY_2023_2_OR_NEWER
            focused.focusController?.IgnoreEvent(evt);
#else
            evt.PreventDefault();
#endif
        }

        /// <summary>Сместить фокус на соседний контрол по списку навигации. false — упёрлись в край.</summary>
        private bool MoveFocus(VisualElement from, int dir)
        {
            var order = FocusablesInOrder();
            int index = order.IndexOf(from);
            if (index < 0) return false;

            int next = index + dir;
            if (next < 0 || next >= order.Count) return false;

            order[next].Focus();
            return true;
        }

        // ── Поле ввода сообщения ──────────────────────────────────────────────
        // TextField съедает стрелки (каретка), поэтому вверх/вниз из него не выходили:
        // с кнопки «отправить» фокус попадал в поле — и застревал, до стрелок карточек
        // ‹/› было не добраться. Перехватываем ДО внутреннего input (TrickleDown):
        // NavigationMoveEvent — геймпад, KeyDownEvent — клавиатура.
        // Left/Right не трогаем: это движение каретки по тексту.

        private void OnInputNavMove(NavigationMoveEvent evt)
        {
            int dir = evt.direction switch
            {
                NavigationMoveEvent.Direction.Up   => -1,
                NavigationMoveEvent.Direction.Down => +1,
                _ => 0
            };
            if (dir == 0 || !MoveFocus(_messageInput, dir)) return;

            evt.StopPropagation();
#if UNITY_2023_2_OR_NEWER
            _messageInput.focusController?.IgnoreEvent(evt);
#else
            evt.PreventDefault();
#endif
        }

        private void OnInputKeyDown(KeyDownEvent evt)
        {
            int dir = evt.keyCode switch
            {
                KeyCode.UpArrow   => -1,
                KeyCode.DownArrow => +1,
                _ => 0
            };
            if (dir == 0 || !MoveFocus(_messageInput, dir)) return;

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
                }
                else
                {
                    Add(_prevButton);
                    Add(_nextButton);

                    // Интерактив текущей карточки: опции опроса или ссылка объявления
                    if (_pollOptions != null)
                        foreach (var option in _pollOptions.Children())
                            Add(option);
                    Add(_annUrlButton);

                    Add(_messageInput);
                    Add(_sendButton);
                    Add(_convButton);
                }
            }

            // Свёрнутая строка видна всегда: одна из кнопок активна по состоянию
            Add(_expandButton);
            Add(_collapseButton);

            Add(_ratingStars);

            return list;

            void Add(VisualElement ve)
            {
                if (IsNavigable(ve)) list.Add(ve);
            }
        }

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
            SetVisible(_goalRoot,   stub.showGoal);
            SetVisible(_ratingRoot, stub.showRating);

            _polls.Clear();
            _announcements.Clear();
            _devLog = null;

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

            if (stub.showGoal)   RefreshGoal(stub.goal.ToMilestone());
            if (stub.showRating) RefreshRating(stub.rating.ToRatingData());
        }

        #endregion

        #region EventBus

        private void OnLiveOpsDataUpdated(object payload)
        {
            if (payload is not LiveOpsDataPayload data) return;

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
                    RefreshGoal(milestone);
                    SetVisible(_goalRoot, true);
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
                if (_liveOpsSystem?.Milestone != null) RefreshGoal(_liveOpsSystem.Milestone);
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
            if (_liveOpsSystem == null) return;
            bool cardsVisible = _liveOpsSystem.IsWidgetVisible("cards") && _cardKeys.Count > 0;
            SetVisible(_panelBody,  cardsVisible);
            SetVisible(_messageRow, _liveOpsSystem.IsWidgetVisible("messages"));
            SetVisible(_goalRoot,   _liveOpsSystem.IsWidgetVisible("goal") && _liveOpsSystem.Milestone != null);
            SetVisible(_ratingRoot, _liveOpsSystem.IsWidgetVisible("rating") && _liveOpsSystem.Rating != null && _launchCount >= 2);
        }

        private void RefreshCardsVisibility()
        {
            if (_liveOpsSystem == null) return;
            SetVisible(_panelBody, _liveOpsSystem.IsWidgetVisible("cards") && _cardKeys.Count > 0);
        }

        #endregion

        #region Cards Carousel

        private void RebuildCardList()
        {
            _cardKeys.Clear();

            if (_contentOrder != null && _contentOrder.order.Length > 0)
            {
                foreach (var entry in _contentOrder.order)
                {
                    switch (entry.type)
                    {
                        case "poll":
                            if (_polls.Exists(p => p.id == entry.id))
                                _cardKeys.Add($"poll:{entry.id}");
                            break;
                        case "announcement":
                            if (_announcements.Exists(a => a.id == entry.id))
                                _cardKeys.Add($"announcement:{entry.id}");
                            break;
                        case "devlog":
                            if (_devLog != null && (_devLog.id == entry.id || string.IsNullOrEmpty(entry.id)))
                                _cardKeys.Add("devlog");
                            break;
                    }
                }
            }
            else
            {
                foreach (var poll in _polls)        _cardKeys.Add($"poll:{poll.id}");
                foreach (var ann in _announcements) _cardKeys.Add($"announcement:{ann.id}");
                if (_devLog != null)                _cardKeys.Add("devlog");
            }

            _currentCard = Mathf.Clamp(_currentCard, 0, Mathf.Max(0, _cardKeys.Count - 1));
        }

        private void ShowCard(int index)
        {
            SetVisible(_pollCard,   false);
            SetVisible(_annCard,    false);
            SetVisible(_devLogCard, false);

            if (_cardKeys.Count == 0) { UpdateCardCounter(0, 0); return; }

            index = Mathf.Clamp(index, 0, _cardKeys.Count - 1);
            _currentCard = index;
            var lang = _stubConfig?.language ?? _liveOpsSystem?.Language ?? "en";
            var key  = _cardKeys[index];

            if (key.StartsWith("poll:"))
            {
                var poll = _polls.Find(p => p.id == key.Substring(5));
                if (poll != null) ShowPollCard(poll, lang);
            }
            else if (key.StartsWith("announcement:"))
            {
                var ann = _announcements.Find(a => a.id == key.Substring(13));
                if (ann != null) ShowAnnouncementCard(ann, lang);
            }
            else if (key == "devlog" && _devLog != null)
            {
                ShowDevLogCard(_devLog, lang);
            }

            UpdateCardCounter(index + 1, _cardKeys.Count);
            MarkCardSeen(key);
            NotifyLayoutChanged("card_changed");
        }

        private void ShowPollCard(LiveOpsPoll poll, string lang)
        {
            SetVisible(_pollCard, true);
            if (_pollQuestion != null)
                _pollQuestion.text = L($"liveops.poll.{poll.id}.q", poll.question.Get(lang));

            if (_cardMeta != null)
                _cardMeta.text = poll.votesTotal > 0 ? $"{poll.votesTotal:N0} votes" : "";

            bool isMulti = poll.pollType == "multi";
            SetBadge(
                isMulti ? "type_poll_multi" : "type_poll",
                isMulti ? UIKeys.CommunityPanel.Fallback.TypePollMulti : UIKeys.CommunityPanel.Fallback.TypePoll);

            if (_pollOptions == null) return;

            // Голосование перерисовывает опции: Clear() уничтожает кнопки вместе с фокусом.
            // Запоминаем позицию, чтобы вернуть фокус на ту же строку опроса.
            int focusedOption = FocusedPollOptionIndex();
            _pollOptions.Clear();

            bool hasVoted = System.Array.Exists(poll.options, o => o.selected);

            for (int idx = 0; idx < poll.options.Length; idx++)
            {
                var opt = poll.options[idx];
                var optId = opt.id;

                var btn = new Button(() =>
                {
                    if (_liveOpsSystem != null) OptimisticPollVote(poll, optId);
                    else                        ToggleStubPollSelection(optId, poll);
                });
                btn.AddToClassList("cp-poll-option");
                btn.text = string.Empty;

                // FillBar — только после голосования, ширина = процент
                var fill = new VisualElement();
                fill.AddToClassList("cp-poll-fill");
                fill.pickingMode = PickingMode.Ignore;
                fill.style.display = hasVoted ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasVoted)
                    fill.style.width = Length.Percent(opt.Percent(poll.votesTotal));
                btn.Add(fill);

                var check = new Label(opt.selected ? "✓" : "");
                check.AddToClassList("cp-poll-check");
                check.pickingMode = PickingMode.Ignore;
                btn.Add(check);

                var label = new Label(L($"liveops.poll.{poll.id}.opt.{idx}", opt.label.Get(lang)));
                label.AddToClassList("cp-poll-label");
                label.pickingMode = PickingMode.Ignore;
                btn.Add(label);

                var pct = new Label(hasVoted ? $"{opt.Percent(poll.votesTotal):0}%" : "");
                pct.AddToClassList("cp-poll-pct");
                pct.pickingMode = PickingMode.Ignore;
                btn.Add(pct);

                _pollOptions.Add(btn);
            }

            RestorePollOptionFocus(focusedOption);
        }

        /// <summary>Индекс опции опроса, на которой сейчас фокус (-1 — фокус не в опросе).</summary>
        private int FocusedPollOptionIndex()
        {
            if (_pollOptions == null) return -1;
            if (_root?.focusController?.focusedElement is not VisualElement focused) return -1;

            int index = 0;
            foreach (var option in _pollOptions.Children())
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
        private void RestorePollOptionFocus(int index)
        {
            if (index < 0 || _pollOptions == null || _root?.panel == null) return;

            _root.schedule.Execute(() =>
            {
                int i = 0;
                foreach (var option in _pollOptions.Children())
                {
                    if (i++ != index) continue;
                    if (IsNavigable(option)) option.Focus();
                    return;
                }
            }).ExecuteLater(1);
        }

        private void ShowAnnouncementCard(LiveOpsAnnouncement ann, string lang)
        {
            SetVisible(_annCard, true);
            SetBadge("type_news", UIKeys.CommunityPanel.Fallback.TypeNews);
            if (_cardMeta != null) _cardMeta.text = "";
            if (_annTitle != null)
                _annTitle.text = L($"liveops.ann.{ann.id}.title", ann.title.Get(lang));
            if (_annBody != null)
                _annBody.text = L($"liveops.ann.{ann.id}.body", ann.body.Get(lang));

            if (_annUrlButton != null)
            {
                bool hasUrl = !string.IsNullOrEmpty(ann.url);
                SetVisible(_annUrlButton, hasUrl);
                _annUrlButton.text = L(UIKeys.CommunityPanel.ReadMore, UIKeys.CommunityPanel.Fallback.ReadMore);
                _annUrlClickUrl = ann.url;
            }
        }

        // Один постоянный обработчик URL-кнопки вместо RemoveAllListeners (uGUI)
        private string _annUrlClickUrl;
        private bool   _annUrlHandlerAttached;

        private void EnsureAnnUrlHandler()
        {
            if (_annUrlHandlerAttached || _annUrlButton == null) return;
            _annUrlHandlerAttached = true;
            _annUrlButton.clicked += () =>
            {
                if (!string.IsNullOrEmpty(_annUrlClickUrl))
                    Application.OpenURL(_annUrlClickUrl);
            };
        }

        private void ShowDevLogCard(LiveOpsDevLog devLog, string lang)
        {
            SetVisible(_devLogCard, true);
            SetBadge("type_devlog", UIKeys.CommunityPanel.Fallback.TypeDevLog);
            if (_cardMeta != null) _cardMeta.text = "";
            if (_devLogFocus != null)
                _devLogFocus.text = L("liveops.devlog.focus", devLog.focus.Get(lang));
            if (_devLogTitle != null)
                _devLogTitle.text = L("liveops.devlog.title", devLog.title.Get(lang));

            if (_devLogDesc != null)
            {
                string desc = devLog.description?.Get(lang);
                SetVisible(_devLogDesc, !string.IsNullOrEmpty(desc));
                if (!string.IsNullOrEmpty(desc))
                    _devLogDesc.text = L("liveops.devlog.description", desc);
            }

            if (_devLogItems == null) return;
            _devLogItems.Clear();

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

                _devLogItems.Add(row);
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

            // category = контекст (какая карточка сейчас открыта)
            var category = _cardKeys.Count > 0 && _currentCard < _cardKeys.Count
                ? _cardKeys[_currentCard]
                : "general";

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

            if (_goalFill != null)
                _goalFill.style.width = Length.Percent(Mathf.Clamp01(data.Progress) * 100f);
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

            int newCards = 0;
            foreach (var key in _cardKeys)
            {
                string cardId = ExtractCardId(key);
                if (!string.IsNullOrEmpty(cardId) && !_seenCardIds.Contains(cardId))
                    newCards++;
            }

            if (newCards > 0)
            {
                string template = L(UIKeys.CommunityPanel.NewCards, UIKeys.CommunityPanel.Fallback.NewCards);
                _summaryText.text = string.Format(template, newCards);
                _summaryText.style.color = new Color(1f, 0.85f, 0.3f); // яркий для новых
            }
            else if (_cardKeys.Count > 0)
            {
                _summaryText.text = L(UIKeys.CommunityPanel.AllSeen, UIKeys.CommunityPanel.Fallback.AllSeen);
                _summaryText.style.color = new Color(0.5f, 0.5f, 0.5f); // всё прочитано
            }
            else
            {
                _summaryText.text = "";
            }

            // Скрыть статус загрузки после получения данных
            if (_statusText != null) _statusText.text = "";
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
        private void OptimisticPollVote(LiveOpsPoll poll, string optId)
        {
            TogglePollOption(poll, optId);

            var lang = _liveOpsSystem != null ? _liveOpsSystem.Language : "en";
            ShowPollCard(poll, lang);

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
        private void ToggleStubPollSelection(string optId, LiveOpsPoll poll)
        {
            if (poll == null) return;
            TogglePollOption(poll, optId);
            ShowPollCard(poll, _stubConfig?.language ?? "en");
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
            EnsureAnnUrlHandler();

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

        private void SetBadge(string keySuffix, string fallback)
        {
            if (_typeBadge != null)
                _typeBadge.text = L("ui.community." + keySuffix, fallback);
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
