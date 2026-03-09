// Packages/com.protosystem.core/Runtime/UI/Windows/LiveOps/CommunityPanelWindow.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        [SerializeField] private Button[]   ratingStars;
        [SerializeField] private TMP_Text   ratingAvgText;

        [Header("Type Badge")]
        [SerializeField] private TMP_Text   typeBadgeText;
        [SerializeField] private LocalizeTMP typeBadgeLocalize;

        [Header("Localization (static labels)")]
        [SerializeField] private LocalizeTMP sendButtonLocalize;
        [SerializeField] private LocalizeTMP placeholderLocalize;
        [SerializeField] private LocalizeTMP ratingLabelLocalize;

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
            SetVisible(goalRoot,     false);
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
                RefreshWishlist(stub.goal.ToMilestone());

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
            SetVisible(goalRoot,     _liveOpsSystem.IsWidgetVisible("wishlist"));
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
            StartCoroutine(AdjustCardsRootHeightDeferred());
        }

        private void ShowPollCard(LiveOpsPoll poll, string lang)
        {
            SetVisible(pollCard, true);
            if (pollQuestionText) pollQuestionText.text = poll.question.Get(lang);

            // Badge: single vs multi
            bool isMulti = poll.pollType == "multi";
            if (typeBadgeLocalize)
                typeBadgeLocalize.SetKey(
                    isMulti ? UIKeys.CommunityPanel.TypePollMulti : UIKeys.CommunityPanel.TypePoll,
                    isMulti ? UIKeys.CommunityPanel.Fallback.TypePollMulti : UIKeys.CommunityPanel.Fallback.TypePoll);
            else if (typeBadgeText)
                typeBadgeText.text = isMulti ? UIKeys.CommunityPanel.Fallback.TypePollMulti : UIKeys.CommunityPanel.Fallback.TypePoll;

            foreach (Transform child in pollOptionsContainer) Destroy(child.gameObject);

            bool hasVoted = System.Array.Exists(poll.options, o => o.selected);

            foreach (var opt in poll.options)
            {
                if (pollOptionPrefab == null) break;
                var go  = Instantiate(pollOptionPrefab, pollOptionsContainer);
                go.SetActive(true);
                var btn = go.GetComponent<Button>();
                if (btn) btn.interactable = true;

                // Label
                var labelT = FindChildRecursive(go.transform, "Label");
                if (labelT) labelT.GetComponent<TMP_Text>().text = opt.label.Get(lang);

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
                btn?.onClick.AddListener(async () =>
                {
                    if (_liveOpsSystem != null)
                        await _liveOpsSystem.SubmitPollAnswerAsync(pollId, new[] { optId });
                    else
                        ToggleStubPollSelection(pollId, optId, poll);
                });
            }
        }

        private void ShowAnnouncementCard(LiveOpsAnnouncement ann, string lang)
        {
            SetVisible(announcementCard, true);
            if (typeBadgeLocalize)
                typeBadgeLocalize.SetKey(UIKeys.CommunityPanel.TypeNews, UIKeys.CommunityPanel.Fallback.TypeNews);
            else if (typeBadgeText)
                typeBadgeText.text = UIKeys.CommunityPanel.Fallback.TypeNews;
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
            if (typeBadgeLocalize)
                typeBadgeLocalize.SetKey(UIKeys.CommunityPanel.TypeDevLog, UIKeys.CommunityPanel.Fallback.TypeDevLog);
            else if (typeBadgeText)
                typeBadgeText.text = UIKeys.CommunityPanel.Fallback.TypeDevLog;
            if (devLogFocusText) devLogFocusText.text = devLog.focus.Get(lang);
            if (devLogTitleText) devLogTitleText.text = devLog.title.Get(lang);

            foreach (Transform child in devLogItemsContainer) Destroy(child.gameObject);

            foreach (var item in devLog.items)
            {
                if (devLogItemPrefab == null) break;
                var go  = Instantiate(devLogItemPrefab, devLogItemsContainer);
                go.SetActive(true);
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

            GameObject activeCard = null;
            if (pollCard && pollCard.activeSelf) activeCard = pollCard;
            else if (announcementCard && announcementCard.activeSelf) activeCard = announcementCard;
            else if (devLogCard && devLogCard.activeSelf) activeCard = devLogCard;

            if (activeCard == null) return;

            Canvas.ForceUpdateCanvases();
            float cardH = LayoutUtility.GetPreferredHeight(activeCard.GetComponent<RectTransform>());
            float navH = 28f;
            float total = cardH + navH + 8f;

            var le = cardsRoot.GetComponent<LayoutElement>();
            if (le) le.preferredHeight = total;

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

            messageSendButton.interactable = false;
            if (_liveOpsSystem != null)
                await _liveOpsSystem.SubmitFeedbackAsync(text, "other", "general");
            messageInput.text = "";
            messageSendButton.interactable = true;
        }

        #endregion

        #region Goal

        private void RefreshWishlist(LiveOpsMilestoneData data)
        {
            var lang = stubConfig?.language ?? _liveOpsSystem?.Language ?? "en";
            if (goalFill)      goalFill.fillAmount    = data.Progress;
            if (goalCountText) goalCountText.text     = $"{data.current:N0} / {data.goal:N0}";
            if (goalDescText)  goalDescText.text      = data.description.Get(lang);
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
            var opt = System.Array.Find(poll.options, o => o.id == optId);
            if (opt == null) return;

            bool isSingle = poll.pollType == "single";
            if (isSingle)
            {
                foreach (var o in poll.options) o.selected = false;
                opt.selected = true;
            }
            else
            {
                opt.selected = !opt.selected;
            }

            // Simulate adding a vote
            opt.votes++;
            poll.votesTotal++;

            var lang = stubConfig?.language ?? "en";
            ShowPollCard(poll, lang);
            StartCoroutine(AdjustCardsRootHeightDeferred());
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
