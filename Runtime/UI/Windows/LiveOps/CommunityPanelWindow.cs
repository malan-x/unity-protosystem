// Packages/com.protosystem.core/Runtime/UI/Windows/LiveOps/CommunityPanelWindow.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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

        [Header("Message")]
        [SerializeField] private GameObject     messageRoot;
        [SerializeField] private TMP_InputField messageInput;
        [SerializeField] private Button         messageSendButton;
        [SerializeField] private TMP_Text       messageCharCountText;
        private const int MessageMaxChars = 120;

        [Header("Wishlist")]
        [SerializeField] private GameObject wishlistRoot;
        [SerializeField] private Image      wishlistFill;
        [SerializeField] private TMP_Text   wishlistCountText;
        [SerializeField] private TMP_Text   wishlistDescText;

        [Header("Rating")]
        [SerializeField] private GameObject ratingRoot;
        [SerializeField] private Button[]   ratingStars;
        [SerializeField] private TMP_Text   ratingAvgText;

        #endregion

        #region State

        private List<LiveOpsPoll>         _polls         = new();
        private List<LiveOpsAnnouncement> _announcements = new();
        private LiveOpsDevLog             _devLog;

        private List<string> _cardKeys    = new();
        private int          _currentCard;

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

            for (int i = 0; i < ratingStars.Length; i++)
            {
                int star = i + 1;
                ratingStars[i]?.onClick.AddListener(() => OnRatingStarClicked(star));
            }

            SetVisible(cardsRoot,    false);
            SetVisible(messageRoot,  false);
            SetVisible(wishlistRoot, false);
            SetVisible(ratingRoot,   false);
        }

        private LiveOpsSystem      _liveOpsSystem;
        private LiveOpsStubConfig   _appliedStub;

        private void Start()
        {
            if (stubConfig != null)
            {
                ApplyStubConfig(stubConfig);
                return;
            }

            // Авто-резольв системы через менеджер
            _liveOpsSystem = SystemInitializationManager.Instance?.GetSystem<LiveOpsSystem>();
            if (_liveOpsSystem != null)
                _liveOpsSystem.RegisterPanel(this);
            else
                gameObject.SetActive(false); // система не готова
        }

        private void OnDestroy()
        {
            _liveOpsSystem?.UnregisterPanel(this);
        }

        // Хот-свап stub через изменение поля в Inspector в PlayMode
        private void Update()
        {
            if (stubConfig != _appliedStub)
            {
                _appliedStub = stubConfig;
                if (stubConfig != null)
                    ApplyStubConfig(stubConfig);
            }
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
            SetVisible(wishlistRoot, stub.showWishlist);
            SetVisible(ratingRoot,   stub.showRating);

            // Карточки
            _polls.Clear();
            _announcements.Clear();
            _devLog = null;

            if (stub.hasPoll)            _polls.Add(stub.poll.ToLiveOpsPoll());
            if (stub.haAnnouncement)     _announcements.Add(stub.announcement.ToAnnouncement());
            if (stub.hasDevLog)          _devLog = stub.devLog.ToDevLog();

            RebuildCardList();
            ShowCard(0);

            // Вишлист
            if (stub.showWishlist)
                RefreshWishlist(stub.wishlist.ToMilestone());

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
                case LiveOpsDataType.PanelConfig when data.Data is LiveOpsPanelConfig cfg:
                    RefreshWidgetVisibility(cfg);
                    if (cfg.wishlistData != null) RefreshWishlist(cfg.wishlistData);
                    break;

                case LiveOpsDataType.Polls when data.Data is List<LiveOpsPoll> polls:
                    _polls = polls;
                    RebuildCardList();
                    ShowCard(_currentCard);
                    break;

                case LiveOpsDataType.Announcements when data.Data is List<LiveOpsAnnouncement> ann:
                    _announcements = ann;
                    RebuildCardList();
                    ShowCard(_currentCard);
                    break;

                case LiveOpsDataType.DevLog when data.Data is LiveOpsDevLog devLog:
                    _devLog = devLog;
                    RebuildCardList();
                    ShowCard(_currentCard);
                    break;

                case LiveOpsDataType.Rating when data.Data is LiveOpsRatingData rating:
                    RefreshRating(rating);
                    break;
            }
        }

        #endregion

        #region Widget Visibility

        private void RefreshWidgetVisibility(LiveOpsPanelConfig cfg)
        {
            if (_liveOpsSystem == null) return;
            SetVisible(cardsRoot,    _liveOpsSystem.IsWidgetVisible("cards"));
            SetVisible(messageRoot,  _liveOpsSystem.IsWidgetVisible("messages"));
            SetVisible(wishlistRoot, _liveOpsSystem.IsWidgetVisible("wishlist"));
            SetVisible(ratingRoot,   _liveOpsSystem.IsWidgetVisible("rating"));
        }

        #endregion

        #region Cards Carousel

        private void RebuildCardList()
        {
            _cardKeys.Clear();
            foreach (var poll in _polls)         _cardKeys.Add($"poll:{poll.id}");
            foreach (var ann in _announcements)  _cardKeys.Add($"announcement:{ann.id}");
            if (_devLog != null)                 _cardKeys.Add("devlog");
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
        }

        private void ShowPollCard(LiveOpsPoll poll, string lang)
        {
            SetVisible(pollCard, true);
            if (pollQuestionText) pollQuestionText.text = poll.question.Get(lang);

            foreach (Transform child in pollOptionsContainer) Destroy(child.gameObject);

            foreach (var opt in poll.options)
            {
                if (pollOptionPrefab == null) break;
                var go  = Instantiate(pollOptionPrefab, pollOptionsContainer);
                var btn = go.GetComponentInChildren<Button>();
                var txt = go.GetComponentInChildren<TMP_Text>();
                if (txt) txt.text = opt.label.Get(lang);

                var optId  = opt.id;
                var pollId = poll.id;
                btn?.onClick.AddListener(async () =>
                {
                    if (_liveOpsSystem != null)
                        await _liveOpsSystem.SubmitPollAnswerAsync(pollId, new[] { optId });
                });
            }
        }

        private void ShowAnnouncementCard(LiveOpsAnnouncement ann, string lang)
        {
            SetVisible(announcementCard, true);
            if (announcementTitleText) announcementTitleText.text = ann.title.Get(lang);
            if (announcementBodyText)  announcementBodyText.text  = ann.body.Get(lang);
            SetVisible(announcementUrlButton?.gameObject, !string.IsNullOrEmpty(ann.url));
            announcementUrlButton?.onClick.RemoveAllListeners();
            if (!string.IsNullOrEmpty(ann.url))
                announcementUrlButton?.onClick.AddListener(() => Application.OpenURL(ann.url));
        }

        private void ShowDevLogCard(LiveOpsDevLog devLog, string lang)
        {
            SetVisible(devLogCard, true);
            if (devLogFocusText) devLogFocusText.text = devLog.focus.Get(lang);
            if (devLogTitleText) devLogTitleText.text = devLog.title.Get(lang);

            foreach (Transform child in devLogItemsContainer) Destroy(child.gameObject);

            foreach (var item in devLog.items)
            {
                if (devLogItemPrefab == null) break;
                var go  = Instantiate(devLogItemPrefab, devLogItemsContainer);
                var txt = go.GetComponentInChildren<TMP_Text>();
                var tog = go.GetComponentInChildren<Toggle>();
                if (txt) txt.text = item.label.Get(lang);
                if (tog) { tog.isOn = item.done; tog.interactable = false; }
            }
        }

        private void OnPrevCard() =>
            ShowCard(_cardKeys.Count > 0 ? (_currentCard - 1 + _cardKeys.Count) % _cardKeys.Count : 0);

        private void OnNextCard() =>
            ShowCard(_cardKeys.Count > 0 ? (_currentCard + 1) % _cardKeys.Count : 0);

        private void UpdateCardCounter(int current, int total) =>
            cardCounterText.SafeSet(total > 0 ? $"{current}/{total}" : "");

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

            messageSendButton.interactable = false;
            if (_liveOpsSystem != null)
                await _liveOpsSystem.SubmitFeedbackAsync(text, "other", "general");
            messageInput.text = "";
            messageSendButton.interactable = true;
        }

        #endregion

        #region Wishlist

        private void RefreshWishlist(LiveOpsMilestoneData data)
        {
            var lang = stubConfig?.language ?? _liveOpsSystem?.Language ?? "en";
            if (wishlistFill)      wishlistFill.fillAmount    = data.Progress;
            if (wishlistCountText) wishlistCountText.text     = $"{data.current:N0} / {data.goal:N0}";
            if (wishlistDescText)  wishlistDescText.text      = data.description.Get(lang);
        }

        #endregion

        #region Rating

        private void RefreshRating(LiveOpsRatingData data)
        {
            if (ratingAvgText) ratingAvgText.text = $"{data.avg:F1}";
            for (int i = 0; i < ratingStars.Length; i++)
            {
                var img = ratingStars[i]?.GetComponent<Image>();
                if (img) img.color = i < data.userVote ? Color.yellow : Color.white;
            }
        }

        private async void OnRatingStarClicked(int star)
        {
            for (int i = 0; i < ratingStars.Length; i++)
            {
                var img = ratingStars[i]?.GetComponent<Image>();
                if (img) img.color = i < star ? Color.yellow : Color.white;
            }
            if (ratingAvgText) ratingAvgText.text = $"{star}.0";
            if (_liveOpsSystem != null)
                await _liveOpsSystem.SubmitRatingAsync(star);
        }

        #endregion

        #region Helpers

        private static void SetVisible(GameObject go, bool visible)
        {
            if (go) go.SetActive(visible);
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
}
