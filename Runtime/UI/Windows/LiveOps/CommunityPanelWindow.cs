// Packages/com.protosystem.core/Runtime/UI/Windows/LiveOps/CommunityPanelWindow.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Threading.Tasks;
using ProtoSystem;
using ProtoSystem.LiveOps;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Community Panel — виджет главного меню для связи с игроками.
    ///
    /// Отображает: карусель карточек (опросы, новости, devlog),
    /// поле сообщения, прогресс-бар вишлиста и рейтинг билда.
    ///
    /// Добавляется как дочерний компонент в MainMenuWindow:
    /// MainMenuWindow → CommunityPanelRoot → [этот компонент]
    ///
    /// Требует ссылки на LiveOpsSystem в Inspector.
    /// Подписывается на Evt.LiveOps.DataUpdated через EventBus.
    ///
    /// Для генерации префаба: ProtoSystem → UI → Tools → UI Generator → Community Panel
    /// </summary>
    public class CommunityPanelWindow : MonoEventBus
    {
        #region Inspector References

        [Header("System")]
        [Tooltip("Необязательно. Если задан — панель показывает stub-данные без обращения к серверу.")]
        [SerializeField] private LiveOpsStubConfig stubConfig;

        [Header("Cards")]
        [SerializeField] private GameObject cardsRoot;
        [SerializeField] private Button     cardPrevButton;
        [SerializeField] private Button     cardNextButton;
        [SerializeField] private TMP_Text   cardCounterText;

        [Header("Card — Poll")]
        [SerializeField] private GameObject pollCard;
        [SerializeField] private TMP_Text   pollQuestionText;
        [SerializeField] private Transform  pollOptionsContainer;
        [SerializeField] private GameObject pollOptionPrefab;

        [Header("Card — Announcement")]
        [SerializeField] private GameObject announcementCard;
        [SerializeField] private TMP_Text   announcementTitleText;
        [SerializeField] private TMP_Text   announcementBodyText;
        [SerializeField] private Button     announcementUrlButton;

        [Header("Card — DevLog")]
        [SerializeField] private GameObject devLogCard;
        [SerializeField] private TMP_Text   devLogFocusText;
        [SerializeField] private TMP_Text   devLogTitleText;
        [SerializeField] private Transform  devLogItemsContainer;
        [SerializeField] private GameObject devLogItemPrefab;

        [Header("DevLog Item Styles")]
        [SerializeField] private DevLogItemStyle doneStyle = new()
            { color = new Color(0.6f, 0.6f, 0.5f), strikethrough = true, underline = false };
        [SerializeField] private DevLogItemStyle wipStyle = new()
            { color = new Color(1f, 0.85f, 0.3f), strikethrough = false, underline = true };
        [SerializeField] private DevLogItemStyle todoStyle = new()
            { color = new Color(0.8f, 0.8f, 0.8f), strikethrough = false, underline = false };

        [Header("Message")]
        [SerializeField] private GameObject     messageRoot;
        [SerializeField] private TMP_InputField messageInput;
        [SerializeField] private Button         messageSendButton;
        [SerializeField] private TMP_Text       messageCharCountText;
        private const int MessageMaxChars = 120;

        [Header("Goal")]
        [SerializeField] private GameObject goalRoot;
        [SerializeField] private Image      goalFill;
        [SerializeField] private TMP_Text   goalCountText;
        [SerializeField] private TMP_Text   goalDescText;

        [Header("Rating")]
        [SerializeField] private GameObject ratingRoot;
        [SerializeField] private RectTransform ratingStarsArea;
        [SerializeField] private Image      ratingFillImage;
        [SerializeField] private TMP_Text   ratingValueText;
        [SerializeField] private TMP_Text   ratingAvgText;

        [Header("Type Badge & Meta")]
        [SerializeField] private TMP_Text   typeBadgeText;
        [SerializeField] private LocalizeTMP typeBadgeLocalize;
        [SerializeField] private TMP_Text   cardMetaText;
        [SerializeField] private LocalizeTMP cardMetaLocalize;

        [Header("Conversation")]
        [SerializeField] private Button     conversationButton;
        [SerializeField] private GameObject notificationBadge;
        [SerializeField] private TMP_Text   notificationCountText;
        [SerializeField] private GameObject   conversationRoot;
        [SerializeField] private Transform    conversationMessagesContainer;
        [SerializeField] private GameObject   conversationMessageItemPrefab;
        [SerializeField] private GameObject   conversationEmptyState;
        [SerializeField] private Button       conversationBackButton;

        [Header("Translation")]
        [SerializeField] private TMP_Text   translationLangLabel;
        [SerializeField] private Button     translationToggleButton;

        [Header("Localization (static labels)")]
        [SerializeField] private LocalizeTMP sendButtonLocalize;
        [SerializeField] private LocalizeTMP placeholderLocalize;
        [SerializeField] private LocalizeTMP ratingLabelLocalize;

        [Header("Collapsed / Expanded")]
        [SerializeField] private GameObject  collapsedRoot;
        [SerializeField] private GameObject  expandedRoot;
        [SerializeField] private Button      expandButton;
        [SerializeField] private Button      collapseButton;
        [SerializeField] private TMP_Text    summaryText;
        [SerializeField] private TMP_Text    statusText;
        [SerializeField] private CanvasGroup collapsedCanvasGroup;
        [SerializeField] private CanvasGroup expandedCanvasGroup;

        #endregion

        #region Constants

        private const string PrefKeyPanelExpanded  = "liveops_panel_expanded";
        private const string PrefKeySeenCards      = "liveops_seen_cards";
        private const string PrefKeyLaunchCount    = "liveops_launch_count";
        private const float  AnimationDuration     = 0.25f;

        #endregion

        #region State

        private List<LiveOpsPoll>         _polls         = new();
        private List<LiveOpsAnnouncement> _announcements = new();
        private LiveOpsDevLog             _devLog;
        private LiveOpsContentOrder       _contentOrder;

        private List<string> _cardKeys    = new();
        private int          _currentCard;
        private int          _userVote;

        // true = показывать локализованный ответ, false = оригинал
        private bool _showLocalizedReply = true;

        // Collapsed/expanded
        private bool _isExpanded;
        private bool _isAnimating;
        private bool _dataLoaded;
        private int  _launchCount;
        private HashSet<string> _seenCardIds = new();

        #endregion

        #region MonoEventBus

        protected override void InitEvents()
        {
            AddEvent(Evt.LiveOps.DataUpdated, OnLiveOpsDataUpdated);
        }

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();

            cardPrevButton?.onClick.AddListener(OnPrevCard);
            cardNextButton?.onClick.AddListener(OnNextCard);
            messageSendButton?.onClick.AddListener(OnSendMessage);
            messageInput?.onValueChanged.AddListener(OnMessageInputChanged);
            if (messageInput != null)
                messageInput.onSelect.AddListener(_ => TryShowVirtualKeyboard());

            conversationButton?.onClick.AddListener(OpenConversation);
            conversationBackButton?.onClick.AddListener(CloseConversation);
            translationToggleButton?.onClick.AddListener(CycleTranslationMode);

            expandButton?.onClick.AddListener(() => SetExpanded(true));
            collapseButton?.onClick.AddListener(() => SetExpanded(false));

            SetupRatingInput();
            UpdateRatingDisplay(0);

            SetVisible(cardsRoot,    false);
            SetVisible(messageRoot,  false);
            SetVisible(goalRoot,     false);
            SetVisible(ratingRoot,   false);
            SetVisible(notificationBadge, false);
            SetVisible(conversationRoot, false);

            // Launch count & initial state
            _launchCount = PlayerPrefs.GetInt(PrefKeyLaunchCount, 0) + 1;
            PlayerPrefs.SetInt(PrefKeyLaunchCount, _launchCount);
            PlayerPrefs.Save();

            _isExpanded = PlayerPrefs.GetInt(PrefKeyPanelExpanded, 0) == 1;
            LoadSeenCards();

            // Показать статус загрузки
            if (statusText) statusText.text = UIKeys.CommunityPanel.Fallback.Loading;

            // Начальная видимость (без анимации)
            ApplyExpandedState(animate: false);
        }

        private LiveOpsSystem      _liveOpsSystem;
        private LiveOpsStubConfig   _appliedStub;

        private void Start()
        {
            Debug.Log("[CommunityPanel] Start()");

            if (stubConfig != null)
            {
                ApplyStubConfig(stubConfig);
                return;
            }

            var mgr = SystemInitializationManager.Instance;
            Debug.Log($"[CommunityPanel] manager={mgr != null}, isInitialized={mgr?.IsInitialized}");

            if (mgr != null)
                _liveOpsSystem = mgr.GetSystem<LiveOpsSystem>();

            Debug.Log($"[CommunityPanel] GetSystem → {(_liveOpsSystem != null ? "OK" : "NULL")}");

            if (_liveOpsSystem != null)
            {
                _liveOpsSystem.RegisterPanel(this);
                _liveOpsSystem.OnUnreadCountChanged += UpdateNotificationBadge;
            }
            else
            {
                Debug.Log("[CommunityPanel] Запускаю WaitForLiveOpsSystem корутину");
                StartCoroutine(WaitForLiveOpsSystem());
            }
        }

        private IEnumerator WaitForLiveOpsSystem()
        {
            var mgr = SystemInitializationManager.Instance;
            if (mgr == null) yield break;

            Debug.Log("[CommunityPanel] Жду IsInitialized...");
            while (!mgr.IsInitialized)
                yield return null;

            Debug.Log("[CommunityPanel] Manager initialized, ищу LiveOpsSystem");
            _liveOpsSystem = mgr.GetSystem<LiveOpsSystem>();
            Debug.Log($"[CommunityPanel] GetSystem → {(_liveOpsSystem != null ? "OK" : "NULL")}");

            if (_liveOpsSystem != null)
            {
                _liveOpsSystem.RegisterPanel(this);
                _liveOpsSystem.OnUnreadCountChanged += UpdateNotificationBadge;
            }
            else
                Debug.LogWarning("[CommunityPanel] LiveOpsSystem не найден даже после инициализации!");
        }

        private void OnDestroy()
        {
            if (_liveOpsSystem != null)
            {
                _liveOpsSystem.UnregisterPanel(this);
                _liveOpsSystem.OnUnreadCountChanged -= UpdateNotificationBadge;
            }
        }

        // Хот-свап stub через изменение поля в Inspector в PlayMode
        // + ховер-превью рейтинга (отслеживание позиции мыши)
        private void Update()
        {
            if (stubConfig != _appliedStub)
            {
                _appliedStub = stubConfig;
                if (stubConfig != null)
                    ApplyStubConfig(stubConfig);
            }

            if (_ratingHovering && ratingStarsArea != null)
                UpdateRatingDisplay(RatingFromScreenPos(Input.mousePosition));
        }

        /// <summary>
        /// Применить stub-конфиг напрямую, минуя EventBus и LiveOpsSystem.
        /// Можно вызывать из кода в любой момент для хот-свапа.
        /// </summary>
        public void ApplyStubConfig(LiveOpsStubConfig stub)
        {
            if (stub == null) return;

            // Видимость виджетов
            SetVisible(cardsRoot,    stub.showCards);
            SetVisible(messageRoot,  stub.showMessages);
            SetVisible(goalRoot,     stub.showGoal);
            SetVisible(ratingRoot,   stub.showRating);

            // Карточки из единого списка
            _polls.Clear();
            _announcements.Clear();
            _devLog = null;

            if (stub.cards != null)
            {
                foreach (var entry in stub.cards)
                {
                    switch (entry.type)
                    {
                        case StubCardType.Poll:
                            _polls.Add(entry.poll.ToLiveOpsPoll());
                            break;
                        case StubCardType.Announcement:
                            _announcements.Add(entry.announcement.ToAnnouncement());
                            break;
                        case StubCardType.DevLog:
                            _devLog = entry.devLog.ToDevLog();
                            break;
                    }
                }
            }

            RebuildCardList();
            ShowCard(0);

            // Goal
            if (stub.showGoal)
                RefreshGoal(stub.goal.ToMilestone());

            // Рейтинг
            if (stub.showRating)
                RefreshRating(stub.rating.ToRatingData());
        }

        #endregion

        #region EventBus Handler

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
                    RebuildCardList();
                    ShowCard(_currentCard);
                    RefreshCardsVisibility();
                    _dataLoaded = true;
                    UpdateSummary();
                    break;

                case LiveOpsDataType.Announcements when data.Data is List<LiveOpsAnnouncement> ann:
                    _announcements = ann;
                    RebuildCardList();
                    ShowCard(_currentCard);
                    RefreshCardsVisibility();
                    _dataLoaded = true;
                    UpdateSummary();
                    break;

                case LiveOpsDataType.DevLog when data.Data is LiveOpsDevLog devLog:
                    _devLog = devLog;
                    RebuildCardList();
                    ShowCard(_currentCard);
                    RefreshCardsVisibility();
                    _dataLoaded = true;
                    UpdateSummary();
                    break;

                case LiveOpsDataType.ContentOrder when data.Data is LiveOpsContentOrder order:
                    _contentOrder = order;
                    RebuildCardList();
                    ShowCard(_currentCard);
                    RefreshCardsVisibility();
                    _dataLoaded = true;
                    UpdateSummary();
                    break;

                case LiveOpsDataType.Milestone when data.Data is LiveOpsMilestoneData milestone:
                    RefreshGoal(milestone);
                    SetVisible(goalRoot, true);
                    _dataLoaded = true;
                    UpdateSummary();
                    break;

                case LiveOpsDataType.Rating when data.Data is LiveOpsRatingData rating:
                    RefreshRating(rating);
                    // Рейтинг показываем только со 2-го запуска
                    SetVisible(ratingRoot, _launchCount >= 2);
                    _dataLoaded = true;
                    UpdateSummary();
                    break;
            }
        }

        #endregion

        #region Widget Visibility

        private void RefreshWidgetVisibility()
        {
            if (_liveOpsSystem == null) return;
            // Карточки показываем только если есть контент
            bool cardsVisible = _liveOpsSystem.IsWidgetVisible("cards") && _cardKeys.Count > 0;
            SetVisible(cardsRoot,    cardsVisible);
            SetVisible(messageRoot,  _liveOpsSystem.IsWidgetVisible("messages"));
            // Goal и Rating показываем только когда пришли данные
            SetVisible(goalRoot,     _liveOpsSystem.IsWidgetVisible("goal") && _liveOpsSystem.Milestone != null);
            SetVisible(ratingRoot,   _liveOpsSystem.IsWidgetVisible("rating") && _liveOpsSystem.Rating != null && _launchCount >= 2);
        }

        private void RefreshCardsVisibility()
        {
            if (_liveOpsSystem == null) return;
            SetVisible(cardsRoot, _liveOpsSystem.IsWidgetVisible("cards") && _cardKeys.Count > 0);
        }

        #endregion

        #region Cards Carousel

        private void RebuildCardList()
        {
            _cardKeys.Clear();

            if (_contentOrder != null && _contentOrder.order.Length > 0)
            {
                // Порядок задан сервером — добавляем только существующий контент в указанном порядке
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
                        // "goal" обрабатывается отдельно (не карточка карусели)
                    }
                }
            }
            else
            {
                // Порядок не задан — fallback: polls → announcements → devlog
                foreach (var poll in _polls)         _cardKeys.Add($"poll:{poll.id}");
                foreach (var ann in _announcements)  _cardKeys.Add($"announcement:{ann.id}");
                if (_devLog != null)                 _cardKeys.Add("devlog");
            }

            _currentCard = Mathf.Clamp(_currentCard, 0, Mathf.Max(0, _cardKeys.Count - 1));
        }

        private void ShowCard(int index)
        {
            SetVisible(pollCard,         false);
            SetVisible(announcementCard, false);
            SetVisible(devLogCard,       false);

            if (_cardKeys.Count == 0) { UpdateCardCounter(0, 0); return; }

            index = Mathf.Clamp(index, 0, _cardKeys.Count - 1);
            _currentCard = index;
            var lang = stubConfig?.language ?? _liveOpsSystem?.Language ?? "en";
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
            StartCoroutine(AdjustCardsRootHeightDeferred());
        }

        private void ShowPollCard(LiveOpsPoll poll, string lang)
        {
            SetVisible(pollCard, true);
            if (pollQuestionText)
                SetLocalized(pollQuestionText, $"liveops.poll.{poll.id}.q", poll.question.Get(lang));

            // Meta
            if (cardMetaText)
            {
                int total = poll.votesTotal;
                cardMetaText.text = total > 0 ? $"{total:N0} votes" : "";
            }

            // Badge: single vs multi
            bool isMulti = poll.pollType == "multi";
            SetBadge(
                isMulti ? "type_poll_multi" : "type_poll",
                isMulti ? UIKeys.CommunityPanel.Fallback.TypePollMulti : UIKeys.CommunityPanel.Fallback.TypePoll);

            foreach (Transform child in pollOptionsContainer) Destroy(child.gameObject);

            bool hasVoted = System.Array.Exists(poll.options, o => o.selected);

            for (int idx = 0; idx < poll.options.Length; idx++)
            {
                var opt = poll.options[idx];
                if (pollOptionPrefab == null) break;
                var go  = Instantiate(pollOptionPrefab, pollOptionsContainer);
                go.SetActive(true);
                var btn = go.GetComponent<Button>();
                if (btn) btn.interactable = true;

                // Label
                var labelT = FindChildRecursive(go.transform, "Label");
                if (labelT)
                {
                    var tmp = labelT.GetComponent<TMP_Text>();
                    if (tmp) SetLocalized(tmp, $"liveops.poll.{poll.id}.opt.{idx}", opt.label.Get(lang));
                }

                // Checkmark
                var checkmark = FindChildRecursive(go.transform, "Checkmark");
                if (checkmark) checkmark.gameObject.SetActive(opt.selected);

                // Pct text — only visible after voting
                var pctT = FindChildRecursive(go.transform, "Pct");
                if (pctT)
                {
                    pctT.gameObject.SetActive(hasVoted);
                    if (hasVoted)
                        pctT.GetComponent<TMP_Text>().text = $"{opt.Percent(poll.votesTotal):0}%";
                }

                // Fill bar — only visible after voting, width = percent
                var fillT = FindChildRecursive(go.transform, "FillBar");
                if (fillT)
                {
                    fillT.gameObject.SetActive(hasVoted);
                    if (hasVoted)
                    {
                        var fillRect = fillT.GetComponent<RectTransform>();
                        if (fillRect)
                        {
                            float pct = opt.Percent(poll.votesTotal) / 100f;
                            fillRect.anchorMin = Vector2.zero;
                            fillRect.anchorMax = new Vector2(pct, 1f);
                            fillRect.offsetMin = new Vector2(1, 1);
                            fillRect.offsetMax = new Vector2(-1, -1);
                        }
                    }
                }

                var optId  = opt.id;
                var pollId = poll.id;
                btn?.onClick.AddListener(() =>
                {
                    if (_liveOpsSystem != null)
                        OptimisticPollVote(poll, optId);
                    else
                        ToggleStubPollSelection(pollId, optId, poll);
                });
            }
        }

        private void ShowAnnouncementCard(LiveOpsAnnouncement ann, string lang)
        {
            SetVisible(announcementCard, true);
            SetBadge("type_news", UIKeys.CommunityPanel.Fallback.TypeNews);
            if (cardMetaText) cardMetaText.text = "";
            if (announcementTitleText)
                SetLocalized(announcementTitleText, $"liveops.ann.{ann.id}.title", ann.title.Get(lang));
            if (announcementBodyText)
                SetLocalized(announcementBodyText, $"liveops.ann.{ann.id}.body", ann.body.Get(lang));
            SetVisible(announcementUrlButton?.gameObject, !string.IsNullOrEmpty(ann.url));
            announcementUrlButton?.onClick.RemoveAllListeners();
            if (!string.IsNullOrEmpty(ann.url))
                announcementUrlButton?.onClick.AddListener(() => Application.OpenURL(ann.url));
        }

        /// <summary>Привязать TMP_Text к рантайм-ключу локализации с fallback.</summary>
        private static void SetLocalized(TMP_Text text, string locKey, string fallback)
        {
            if (!text.TryGetComponent<LocalizeTMP>(out var loc))
                loc = text.gameObject.AddComponent<LocalizeTMP>();
            loc.SetKey(locKey, fallback);
        }

        private void ShowDevLogCard(LiveOpsDevLog devLog, string lang)
        {
            SetVisible(devLogCard, true);
            SetBadge("type_devlog", UIKeys.CommunityPanel.Fallback.TypeDevLog);
            if (cardMetaText) cardMetaText.text = "";
            if (devLogFocusText)
                SetLocalized(devLogFocusText, "liveops.devlog.focus", devLog.focus.Get(lang));
            if (devLogTitleText)
                SetLocalized(devLogTitleText, "liveops.devlog.title", devLog.title.Get(lang));

            foreach (Transform child in devLogItemsContainer) Destroy(child.gameObject);

            for (int idx = 0; idx < devLog.items.Length; idx++)
            {
                var item = devLog.items[idx];
                if (devLogItemPrefab == null) break;
                var go  = Instantiate(devLogItemPrefab, devLogItemsContainer);
                go.SetActive(true);
                var txt = go.GetComponentInChildren<TMP_Text>();
                var tog = go.GetComponentInChildren<Toggle>();
                if (txt)
                {
                    SetLocalized(txt, $"liveops.devlog.item.{idx}", item.name.Get(lang));
                    ApplyDevLogItemStyle(txt, item);
                }
                if (tog) { tog.isOn = item.IsDone; tog.interactable = false; }
            }
        }

        private void ApplyDevLogItemStyle(TMP_Text txt, LiveOpsDevLogItem item)
        {
            var style = item.IsDone ? doneStyle : item.IsWip ? wipStyle : todoStyle;
            txt.color = style.color;

            var flags = txt.fontStyle;
            if (style.strikethrough) flags |= FontStyles.Strikethrough;
            else                     flags &= ~FontStyles.Strikethrough;
            if (style.underline)     flags |= FontStyles.Underline;
            else                     flags &= ~FontStyles.Underline;
            txt.fontStyle = flags;
        }

        private void OnPrevCard() =>
            ShowCard(_cardKeys.Count > 0 ? (_currentCard - 1 + _cardKeys.Count) % _cardKeys.Count : 0);

        private void OnNextCard() =>
            ShowCard(_cardKeys.Count > 0 ? (_currentCard + 1) % _cardKeys.Count : 0);

        private void UpdateCardCounter(int current, int total) =>
            cardCounterText.SafeSet(total > 0 ? $"{current}/{total}" : "");

        /// <summary>
        /// Пересчитать высоту CardsRoot по активной карточке.
        /// </summary>
        private IEnumerator AdjustCardsRootHeightDeferred()
        {
            // Wait for layout to settle after Instantiate
            yield return null;
            AdjustCardsRootHeight();
        }

        private void AdjustCardsRootHeight()
        {
            if (cardsRoot == null) return;

            // Force full layout rebuild from cards up to root
            Canvas.ForceUpdateCanvases();

            // Rebuild cards container (may have CSF)
            var cardsRect = cardsRoot.GetComponent<RectTransform>();
            if (cardsRect) LayoutRebuilder.ForceRebuildLayoutImmediate(cardsRect);

            // Rebuild poll options container if active
            if (pollCard && pollCard.activeSelf && pollOptionsContainer != null)
            {
                var optRect = pollOptionsContainer.GetComponent<RectTransform>();
                if (optRect) LayoutRebuilder.ForceRebuildLayoutImmediate(optRect);
            }

            // Rebuild card content parent (for CSF chain)
            GameObject activeCard = null;
            if (pollCard && pollCard.activeSelf) activeCard = pollCard;
            else if (announcementCard && announcementCard.activeSelf) activeCard = announcementCard;
            else if (devLogCard && devLogCard.activeSelf) activeCard = devLogCard;

            if (activeCard != null)
            {
                var cardParent = activeCard.transform.parent;
                if (cardParent != null)
                {
                    var parentRect = cardParent.GetComponent<RectTransform>();
                    if (parentRect) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                }
            }

            // Also handle package generator layout (CardsRoot with LayoutElement)
            var le = cardsRoot.GetComponent<LayoutElement>();
            if (le && activeCard != null)
            {
                float cardH = LayoutUtility.GetPreferredHeight(activeCard.GetComponent<RectTransform>());
                le.preferredHeight = cardH + 52f; // nav(28) + meta(16) + padding
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }

        #endregion

        #region Message

        private void OnMessageInputChanged(string value)
        {
            int len = value?.Length ?? 0;
            if (messageCharCountText) messageCharCountText.text = $"{len}/{MessageMaxChars}";
            if (messageSendButton) messageSendButton.interactable = len > 0 && len <= MessageMaxChars;
        }

        private async void OnSendMessage()
        {
            if (messageInput == null) return;
            var text = messageInput.text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            // category = контекст (какая карточка сейчас открыта)
            var category = _cardKeys.Count > 0 && _currentCard < _cardKeys.Count
                ? _cardKeys[_currentCard]
                : "general";

            messageSendButton.interactable = false;
            if (_liveOpsSystem != null)
            {
                await _liveOpsSystem.SubmitFeedbackAsync(text, category);
                // Обновить историю сообщений после отправки
                await _liveOpsSystem.FetchMyMessagesAsync();
            }
            messageInput.text = "";
            messageSendButton.interactable = true;
        }

        #endregion

        #region Goal

        private void RefreshGoal(LiveOpsMilestoneData data)
        {
            var lang = stubConfig?.language ?? _liveOpsSystem?.Language ?? "en";
            Debug.Log($"[CommunityPanel] RefreshGoal: {data.current}/{data.goal} Progress={data.Progress:F4}");
            if (goalFill)
            {
                var rt = goalFill.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = new Vector2(data.Progress, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            if (goalCountText) goalCountText.text = $"{data.current:N0} / {data.goal:N0}";
            if (goalDescText)
            {
                var title = data.title.Get(lang);
                var desc  = data.description.Get(lang);
                var text  = string.IsNullOrEmpty(title) ? desc : $"{title}\n{desc}";
                SetLocalized(goalDescText, "liveops.goal.desc", text);
            }
        }

        #endregion

        #region Rating

        private bool _ratingHovering;

        private void SetupRatingInput()
        {
            if (ratingStarsArea == null) return;

            // Image для raycast (может уже быть)
            if (!ratingStarsArea.TryGetComponent<Image>(out _))
            {
                var img = ratingStarsArea.gameObject.AddComponent<Image>();
                img.color = Color.clear;
            }

            var trigger = ratingStarsArea.gameObject.AddComponent<EventTrigger>();

            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener(_ => _ratingHovering = true);
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener(_ => { _ratingHovering = false; UpdateRatingDisplay(_userVote); });
            trigger.triggers.Add(exitEntry);

            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener(e => OnRatingClick((PointerEventData)e));
            trigger.triggers.Add(clickEntry);
        }

        private int RatingFromScreenPos(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                ratingStarsArea, screenPos, null, out var local);
            float norm = Mathf.Clamp01((local.x - ratingStarsArea.rect.xMin) / ratingStarsArea.rect.width);
            return Mathf.Clamp(Mathf.CeilToInt(norm * 10f), 1, 10);
        }

        private async void OnRatingClick(PointerEventData e)
        {
            int score = RatingFromScreenPos(e.position);
            if (score <= 0) return;
            _userVote = score;
            UpdateRatingDisplay(score);
            if (_liveOpsSystem != null)
                await _liveOpsSystem.SubmitRatingAsync(score);
        }

        private void RefreshRating(LiveOpsRatingData data)
        {
            _userVote = data.userVote;
            if (ratingAvgText) ratingAvgText.text = $"{data.avg:F1}";
            UpdateRatingDisplay(_userVote);
        }

        private void UpdateRatingDisplay(int value)
        {
            if (ratingFillImage) ratingFillImage.fillAmount = value / 10f;
            if (ratingValueText) ratingValueText.text = value > 0 ? value.ToString() : "—";
        }

        #endregion

        #region Conversation

        private bool _conversationOpen;

        private void UpdateNotificationBadge(int count)
        {
            Debug.Log($"[CommunityPanel] UpdateNotificationBadge({count})");
            SetVisible(notificationBadge, count > 0);
            if (notificationCountText) notificationCountText.text = count.ToString();
        }

        private const float ConversationMinHeight = 100f;
        private const float ConversationMaxHeight = 400f;
        private const float ConversationHeaderHeight = 38f;
        private const float ConversationMessageHeight = 70f;
        private const float ConversationPadding = 16f;

        private void OpenConversation()
        {
            Debug.Log("[CommunityPanel] OpenConversation()");
            _conversationOpen = true;

            // Скрыть основной контент
            SetVisible(cardsRoot,   false);
            SetVisible(messageRoot, false);
            SetVisible(goalRoot,    false);
            SetVisible(ratingRoot,  false);

            // Показать переписку
            SetVisible(conversationRoot, true);

            if (_liveOpsSystem != null)
                _liveOpsSystem.MarkAllRepliesRead();

            UpdateTranslationUI();
            RenderConversation();

            // Adaptive height based on message count
            AdjustConversationHeight();
        }

        private void AdjustConversationHeight()
        {
            if (conversationRoot == null) return;
            var le = conversationRoot.GetComponent<LayoutElement>();
            if (le == null) return;

            int messageCount = 0;
            var items = _liveOpsSystem != null ? _liveOpsSystem.MyMessages : null;
            if (items != null) messageCount = items.Count;

            float desiredHeight = ConversationHeaderHeight
                + messageCount * ConversationMessageHeight
                + ConversationPadding;
            desiredHeight = Mathf.Clamp(desiredHeight, ConversationMinHeight, ConversationMaxHeight);
            le.preferredHeight = desiredHeight;

            var rootRect = conversationRoot.GetComponent<RectTransform>();
            if (rootRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }

        private void CycleTranslationMode()
        {
            _showLocalizedReply = !_showLocalizedReply;
            Debug.Log($"[CommunityPanel] CycleTranslation → _showLocalizedReply={_showLocalizedReply}, lang={(_liveOpsSystem != null ? _liveOpsSystem.Language : "null")}");
            UpdateTranslationUI();
            RenderConversation();
        }

        private void UpdateTranslationUI()
        {
            string lang = _liveOpsSystem != null ? _liveOpsSystem.Language : "??";

            if (translationLangLabel != null)
                translationLangLabel.text = (lang ?? "??").ToUpper();

            if (translationToggleButton != null)
            {
                var btnText = translationToggleButton.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                    btnText.text = _showLocalizedReply ? lang?.ToUpper() ?? "??" : "Aa";
            }
        }

        /// <summary>Возвращает текст сообщения (всегда оригинал — игрок писал сам).</summary>
        private string GetDisplayMessage(LiveOpsConversationItem item)
        {
            if (item == null) return "";
            return item.message ?? "";
        }

        /// <summary>Возвращает текст ответа с учётом режима локализации.</summary>
        private string GetDisplayReply(LiveOpsConversationItem item)
        {
            if (item == null) return "";

            if (_showLocalizedReply && item.replyLocalized != null)
            {
                string lang = _liveOpsSystem != null ? _liveOpsSystem.Language : "en";
                string localized = item.replyLocalized.Get(lang);
                Debug.Log($"[CommunityPanel] GetDisplayReply: lang={lang}, localized='{localized}', reply='{item.reply}', translations={item.replyLocalized.translations?.Count ?? 0}");
                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }
            else
            {
                Debug.Log($"[CommunityPanel] GetDisplayReply: showLocalized={_showLocalizedReply}, replyLocalized={item.replyLocalized != null}, reply='{item.reply}'");
            }

            return item.reply ?? "";
        }

        private void CloseConversation()
        {
            Debug.Log("[CommunityPanel] CloseConversation()");
            _conversationOpen = false;
            SetVisible(conversationRoot, false);

            // Восстановить основной контент
            if (stubConfig != null)
            {
                ApplyStubConfig(stubConfig);
            }
            else
            {
                RefreshWidgetVisibility();
                ShowCard(_currentCard);
            }
        }

        private void RenderConversation()
        {
            if (conversationMessagesContainer == null) return;

            // Удалить старые сообщения, но не emptyState (он тоже child контейнера)
            for (int c = conversationMessagesContainer.childCount - 1; c >= 0; c--)
            {
                var child = conversationMessagesContainer.GetChild(c);
                if (conversationEmptyState != null && child.gameObject == conversationEmptyState)
                    continue;
                Destroy(child.gameObject);
            }

            var items = _liveOpsSystem != null ? _liveOpsSystem.MyMessages : null;
            bool empty = items == null || items.Count == 0;
            SetVisible(conversationEmptyState, empty);
            if (empty) return;

            // От новых к старым
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                if (conversationMessageItemPrefab == null) break;

                var go = Instantiate(conversationMessageItemPrefab, conversationMessagesContainer);
                go.SetActive(true);

                var msgText = FindChildRecursive(go.transform, "MessageText")?.GetComponent<TMP_Text>();
                if (msgText) msgText.text = GetDisplayMessage(item);

                var timeText = FindChildRecursive(go.transform, "TimestampText")?.GetComponent<TMP_Text>();
                if (timeText) timeText.text = FormatTimestamp(item.timestamp);

                bool hasReply = !string.IsNullOrEmpty(item.reply);
                var replyRow = FindChildRecursive(go.transform, "DevReplyRow");
                if (replyRow) replyRow.gameObject.SetActive(hasReply);

                var replyText = FindChildRecursive(go.transform, "ReplyText")?.GetComponent<TMP_Text>();
                if (replyText) replyText.text = GetDisplayReply(item);

                var catText = FindChildRecursive(go.transform, "CategoryText")?.GetComponent<TMP_Text>();
                if (catText) catText.text = FormatCategory(item.category);
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

            // Пометить текущие карточки как просмотренные при сворачивании
            if (!expanded) MarkCurrentCardsAsSeen();

            ApplyExpandedState(animate: true);
        }

        private void ApplyExpandedState(bool animate)
        {
            if (animate && gameObject.activeInHierarchy)
            {
                StartCoroutine(AnimateExpandCollapse(_isExpanded));
                return;
            }

            // Мгновенное переключение
            SetScaleVisible(collapsedRoot, !_isExpanded);
            SetScaleVisible(expandedRoot,   _isExpanded);

            UpdateSummary();

            // Рейтинг только со 2-го запуска
            if (!_isExpanded && ratingRoot)
                ratingRoot.SetActive(_launchCount >= 2 && ratingRoot.activeSelf);
        }

        private IEnumerator AnimateExpandCollapse(bool toExpanded)
        {
            _isAnimating = true;

            var hideGO = toExpanded ? collapsedRoot : expandedRoot;
            var showGO = toExpanded ? expandedRoot  : collapsedRoot;

            // Сначала включаем появляющийся блок со scale=0
            if (showGO) { showGO.SetActive(true); showGO.transform.localScale = new Vector3(1f, 0f, 1f); }

            float elapsed = 0f;
            while (elapsed < AnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / AnimationDuration);
                float smooth = t * t * (3f - 2f * t); // smoothstep

                // Скрываемый: scale.y 1→0
                if (hideGO) hideGO.transform.localScale = new Vector3(1f, 1f - smooth, 1f);
                // Появляющийся: scale.y 0→1
                if (showGO) showGO.transform.localScale = new Vector3(1f, smooth, 1f);

                yield return null;
            }

            // Финальное состояние
            SetScaleVisible(hideGO, false);
            SetScaleVisible(showGO, true);

            // Рейтинг только со 2-го запуска
            if (!toExpanded && ratingRoot)
                ratingRoot.SetActive(_launchCount >= 2 && ratingRoot.activeSelf);

            UpdateSummary();
            _isAnimating = false;
        }

        /// <summary>Показать/скрыть блок через scale.y (0=скрыт, 1=виден).</summary>
        private static void SetScaleVisible(GameObject go, bool visible)
        {
            if (!go) return;
            go.SetActive(visible);
            go.transform.localScale = visible ? Vector3.one : new Vector3(1f, 0f, 1f);
        }

        /// <summary>Обновить текст сводки: количество новых карточек.</summary>
        private void UpdateSummary()
        {
            if (summaryText == null) return;

            if (!_dataLoaded)
            {
                summaryText.text = "";
                return;
            }

            int totalCards = _cardKeys.Count;
            int newCards = 0;
            foreach (var key in _cardKeys)
            {
                string cardId = ExtractCardId(key);
                if (!string.IsNullOrEmpty(cardId) && !_seenCardIds.Contains(cardId))
                    newCards++;
            }

            // Локализованный текст: "{0} новых" / "{0} new"
            string template = Loc.IsReady
                ? Loc.Get(UIKeys.CommunityPanel.NewCards, UIKeys.CommunityPanel.Fallback.NewCards)
                : UIKeys.CommunityPanel.Fallback.NewCards;

            if (newCards > 0)
            {
                summaryText.text = string.Format(template, newCards);
                summaryText.color = new Color(1f, 0.85f, 0.3f); // яркий для новых
            }
            else if (totalCards > 0)
            {
                string allSeenText = Loc.IsReady
                    ? Loc.Get(UIKeys.CommunityPanel.AllSeen, UIKeys.CommunityPanel.Fallback.AllSeen)
                    : UIKeys.CommunityPanel.Fallback.AllSeen;
                summaryText.text = allSeenText;
                summaryText.color = new Color(0.5f, 0.5f, 0.5f); // серый — всё прочитано
            }
            else
            {
                summaryText.text = "";
            }

            // Скрыть статус загрузки после получения данных
            if (statusText) statusText.text = "";
        }

        /// <summary>Пометить все текущие карточки как просмотренные.</summary>
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

        /// <summary>Пометить карточку как просмотренную при пролистывании.</summary>
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
            // "poll:abc123" → "abc123", "announcement:xyz" → "xyz", "devlog" → "devlog"
            int colon = cardKey.IndexOf(':');
            return colon >= 0 ? cardKey.Substring(colon + 1) : cardKey;
        }

        // ── Seen cards persistence ──

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

        #region Helpers

        private void TryShowVirtualKeyboard()
        {
            if (messageInput == null) return;
            VirtualKeyboard.TryShow(messageInput.text, MessageMaxChars, result =>
            {
                if (result != null && messageInput != null)
                    messageInput.text = result;
            });
        }

        private static void SetVisible(GameObject go, bool visible)
        {
            if (go) go.SetActive(visible);
        }

        /// <summary>
        /// Установить текст badge по суффиксу ключа.
        /// Префикс берётся из текущего ключа LocalizeTMP (может быть lc.community.* или ui.community.*).
        /// </summary>
        private void SetBadge(string keySuffix, string fallback)
        {
            if (typeBadgeLocalize)
            {
                // Derive prefix from the key that generator set (e.g. "lc.community." or "ui.community.")
                string prefix = "ui.community.";
                var existingKey = GetLocalizeTMPKey(typeBadgeLocalize);
                if (!string.IsNullOrEmpty(existingKey))
                {
                    int lastDot = existingKey.LastIndexOf('.');
                    if (lastDot > 0) prefix = existingKey.Substring(0, lastDot + 1);
                }
                typeBadgeLocalize.SetKey(prefix + keySuffix, fallback);
            }
            else if (typeBadgeText)
                typeBadgeText.text = fallback;
        }

        private static string GetLocalizeTMPKey(LocalizeTMP loc)
        {
            if (loc == null) return null;
            var field = typeof(LocalizeTMP).GetField("key", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(loc) as string;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Тогл выбора опции опроса в stub-режиме (локальный, без сервера).
        /// </summary>
        private void ToggleStubPollSelection(string pollId, string optId, LiveOpsPoll poll)
        {
            if (poll == null) return;
            TogglePollOption(poll, optId);

            var lang = stubConfig?.language ?? "en";
            ShowPollCard(poll, lang);
            StartCoroutine(AdjustCardsRootHeightDeferred());
        }

        /// <summary>
        /// Optimistic UI: мгновенно обновляем состояние опроса и перерисовываем,
        /// затем отправляем на сервер в фоне. При ответе сервера FetchAsync обновит реальные данные.
        /// </summary>
        private void OptimisticPollVote(LiveOpsPoll poll, string optId)
        {
            // 1. Обновить локальное состояние
            TogglePollOption(poll, optId);

            // 2. Перерисовать карточку мгновенно
            var lang = _liveOpsSystem != null ? _liveOpsSystem.Language : "en";
            ShowPollCard(poll, lang);
            StartCoroutine(AdjustCardsRootHeightDeferred());

            // 3. Собрать выбранные option ids
            var selected = new List<string>();
            foreach (var o in poll.options)
                if (o.selected) selected.Add(o.id);

            // 4. Отправить на сервер в фоне
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
                Debug.LogWarning($"[CommunityPanel] Poll vote failed: {ex.Message}");
                // FetchAsync восстановит реальное состояние при следующем обновлении
            }
        }

        /// <summary>
        /// Переключить выбор опции с учётом single/multi режима и пересчитать голоса.
        /// </summary>
        private static void TogglePollOption(LiveOpsPoll poll, string optId)
        {
            var opt = System.Array.Find(poll.options, o => o.id == optId);
            if (opt == null) return;

            bool isSingle = poll.pollType == "single";
            bool wasSelected = opt.selected;

            if (isSingle)
            {
                // Снять все выделения, пересчитать голоса
                foreach (var o in poll.options)
                {
                    if (o.selected) { o.votes = Mathf.Max(0, o.votes - 1); poll.votesTotal = Mathf.Max(0, poll.votesTotal - 1); }
                    o.selected = false;
                }
                // Если кликнули не на уже выбранный — выбрать его
                if (!wasSelected)
                {
                    opt.selected = true;
                    opt.votes++;
                    poll.votesTotal++;
                }
            }
            else
            {
                // Multi: просто тогл
                opt.selected = !opt.selected;
                if (opt.selected) { opt.votes++; poll.votesTotal++; }
                else              { opt.votes = Mathf.Max(0, opt.votes - 1); poll.votesTotal = Mathf.Max(0, poll.votesTotal - 1); }
            }
        }

        #endregion
    }

    internal static class TMP_TextExtensions
    {
        internal static void SafeSet(this TMP_Text text, string value)
        {
            if (text) text.text = value;
        }
    }

    /// <summary>Настройки отображения элемента DevLog по статусу.</summary>
    [System.Serializable]
    public struct DevLogItemStyle
    {
        public Color color;
        public bool strikethrough;
        public bool underline;
    }
}
