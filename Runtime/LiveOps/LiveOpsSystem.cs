// Packages/com.protosystem.core/Runtime/LiveOps/LiveOpsSystem.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Система LiveOps — связь с игроками без обновления билда.
    /// 
    /// Возможности:
    /// - Сообщения от разработчиков (MOTD/новости)
    /// - Аналитические события (offline-буфер + flush)
    /// - Опросы
    /// - Фидбек от игроков
    /// 
    /// Бэкенд подключается через ILiveOpsProvider:
    /// <code>
    /// liveOpsConfig.SetProvider(new MyPocketBaseProvider(serverUrl));
    /// </code>
    /// 
    /// Идентификатор игрока устанавливается до InitializeAsync():
    /// <code>
    /// liveOpsSystem.SetPlayerId(SteamFriends.GetPersonaName());
    /// // Если не вызван — используется анонимный GUID из PlayerPrefs
    /// </code>
    /// </summary>
    [ProtoSystemComponent("LiveOps", "Связь с игроками без обновления билда",
        "Core", "📡", 150)]
    public class LiveOpsSystem : InitializableSystemBase
    {
        #region InitializableSystemBase

        public override string SystemId => "live_ops";
        public override string DisplayName => "LiveOps System";
        public override string Description => "MOTD, аналитика, опросы и фидбек без обновления билда.";

        #endregion

        #region Serialized Fields

        [Header("Configuration")]
        [SerializeField, InlineConfig] private LiveOpsConfig config;

        #endregion

        #region State

        private ILiveOpsProvider _provider;
        private List<LiveOpsMessage>      _messages       = new();
        private List<LiveOpsPoll>         _polls          = new();
        private Queue<LiveOpsEvent>       _analyticsQueue = new();
        private float                     _fetchTimer;
        private string                    _playerId;
        private bool                      _playerIdOverridden;

        // Community Panel
        private LiveOpsPanelConfig        _panelConfig;
        private List<LiveOpsAnnouncement> _announcements  = new();
        private LiveOpsDevLog             _devLog;
        private LiveOpsRatingData         _rating;
        private LiveOpsPlayerContext      _playerContext  = new(0, 0);

        // Panel registration
        private ProtoSystem.UI.CommunityPanelWindow _panel;
        private bool _serverAvailable;
        private bool _hasData;

        #endregion

        #region Events

        public event Action<List<LiveOpsMessage>> OnMessagesUpdated;
        public event Action<List<LiveOpsPoll>>    OnPollsUpdated;

        #endregion

        #region Public API

        public IReadOnlyList<LiveOpsMessage>      Messages       => _messages;
        public IReadOnlyList<LiveOpsPoll>          Polls          => _polls;
        public IReadOnlyList<LiveOpsAnnouncement> Announcements  => _announcements;
        public LiveOpsDevLog                      DevLog         => _devLog;
        public LiveOpsRatingData                  Rating         => _rating;
        public LiveOpsPanelConfig                 PanelConfig    => _panelConfig;
        public string                             PlayerId       => _playerId;
        public string                             Language          => config != null ? config.defaultLanguage : "en";
        public bool                               IsServerAvailable => _serverAvailable;

        /// <summary>
        /// Установить идентификатор игрока до InitializeAsync().
        /// Например: SetPlayerId(SteamFriends.GetPersonaName()).
        /// Если не вызван — используется анонимный GUID из PlayerPrefs.
        /// </summary>
        public void SetPlayerId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _playerId = id;
            _playerIdOverridden = true;
        }

        /// <summary>
        /// Установить контекст игрока для проверки условий show_after.
        /// Вызывать до InitializeAsync() или при изменении данных.
        /// </summary>
        public void SetPlayerContext(LiveOpsPlayerContext ctx) => _playerContext = ctx;

        /// <summary>
        /// Проверить, должен ли виджет панели быть виден при текущем контексте.
        /// Ключи: "cards", "messages", "wishlist", "rating".
        /// </summary>
        public bool IsWidgetVisible(string widgetKey)
        {
            if (_panelConfig == null) return true;
            var def = widgetKey switch
            {
                "cards"    => _panelConfig.cards,
                "messages" => _panelConfig.messages,
                "wishlist" => _panelConfig.wishlist,
                "rating"   => _panelConfig.rating,
                _          => null
            };
            return def?.IsVisible(_playerContext) ?? true;
        }

        /// <summary>
        /// Зарегистрировать панель. Система сразу определяет её видимость:
        /// - сервер недоступен → панель скрывается;
        /// - есть данные → публикует всё через EventBus;
        /// - fetchOnPanelOpen → запускает обновление.
        /// </summary>
        public void RegisterPanel(ProtoSystem.UI.CommunityPanelWindow panel)
        {
            _panel = panel;
            if (!_serverAvailable)
            {
                panel.gameObject.SetActive(false);
                return;
            }
            panel.gameObject.SetActive(true);
            if (_hasData) PushAllDataToEventBus();
            if (config != null && config.fetchOnPanelOpen) _ = FetchAsync();
        }

        /// <summary>Отписать панель от системы.</summary>
        public void UnregisterPanel(ProtoSystem.UI.CommunityPanelWindow panel)
        {
            if (_panel == panel) _panel = null;
        }

        /// <summary>Принудительно запросить данные с сервера (например по кнопке в UI).</summary>
        public void TriggerFetch() => _ = FetchAsync();

        /// <summary>
        /// Отправить аналитическое событие.
        /// Если провайдер недоступен — событие попадает в offline-буфер.
        /// </summary>
        public void TrackEvent(string eventName, Dictionary<string, string> data = null)
        {
            if (config == null || !config.enableAnalytics) return;

            var evt = new LiveOpsEvent(eventName, _playerId, Application.version, data);

            if (_provider == null)
            {
                EnqueueAnalytics(evt);
                return;
            }

            _ = SendEventWithFallbackAsync(evt);
        }

        /// <summary>Отправить фидбек/сообщение от игрока.</summary>
        public async Task<bool> SubmitFeedbackAsync(string message, string category = "other", string tag = "general")
        {
            if (config == null || !config.enableFeedback || _provider == null) return false;
            var feedback = new LiveOpsFeedback(_playerId, Application.version, message, category, tag);
            return await _provider.SubmitFeedbackAsync(feedback);
        }

        /// <summary>Отправить ответ на опрос. Работает для single и multi.</summary>
        public async Task<bool> SubmitPollAnswerAsync(string pollId, string[] optionIds)
        {
            if (config == null || !config.enablePolls || _provider == null) return false;
            var answer = new LiveOpsPollAnswer { pollId = pollId, optionIds = optionIds, playerId = _playerId };
            return await _provider.SubmitPollAnswerAsync(answer);
        }

        /// <summary>Отправить или обновить оценку текущего билда.</summary>
        public async Task<LiveOpsRatingResult> SubmitRatingAsync(int score)
        {
            if (config == null || !config.enableRating || _provider == null) return null;
            var submit = new LiveOpsRatingSubmit { version = Application.version, score = score, playerId = _playerId };
            var result = await _provider.SubmitRatingAsync(submit);
            if (result != null && _rating != null)
            {
                _rating.avg      = result.avg;
                _rating.count    = result.count;
                _rating.userVote = score;
                EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Rating, _rating));
            }
            return result;
        }

        private void PushAllDataToEventBus()
        {
            if (_panelConfig   != null) EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.PanelConfig,    _panelConfig));
            if (_polls?.Count   > 0)    EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Polls,           _polls));
            if (_announcements?.Count > 0) EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Announcements, _announcements));
            if (_devLog         != null) EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.DevLog,         _devLog));
            if (_rating         != null) EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Rating,         _rating));
            if (_messages?.Count > 0)   EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Messages,       _messages));
        }

        public async Task FetchAsync()
        {
            if (_provider == null) return;

            // Panel config — управляет видимостью остальных виджетов
            var panelConfig = await _provider.FetchPanelConfigAsync();
            if (panelConfig != null)
            {
                _panelConfig = panelConfig;
                EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.PanelConfig, _panelConfig));
            }

            if (config.enableMessages)
            {
                var messages = await _provider.FetchMessagesAsync();
                if (messages != null)
                {
                    _messages = messages;
                    OnMessagesUpdated?.Invoke(_messages);
                    EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Messages, _messages));
                }
            }

            if (config.enablePolls)
            {
                var polls = await _provider.FetchPollsAsync();
                if (polls != null)
                {
                    _polls = polls;
                    OnPollsUpdated?.Invoke(_polls);
                    EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Polls, _polls));
                }
            }

            if (config.enableAnnouncements)
            {
                var ann = await _provider.FetchAnnouncementsAsync();
                if (ann != null)
                {
                    _announcements = ann;
                    EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Announcements, _announcements));
                }
            }

            if (config.enableDevLog)
            {
                var devLog = await _provider.FetchDevLogAsync();
                if (devLog != null)
                {
                    _devLog = devLog;
                    EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.DevLog, _devLog));
                }
            }

            if (config.enableRating)
            {
                var rating = await _provider.FetchRatingAsync(Application.version);
                if (rating != null)
                {
                    _rating = rating;
                    EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Rating, _rating));
                }
            }

            _hasData = true;
        }

        #endregion

        #region InitializableSystemBase Implementation

        protected override void InitEvents() { }

        public override async Task<bool> InitializeAsync()
        {
            ReportProgress(0.1f);

            if (config == null)
            {
                ProtoLogger.LogWarning(SystemId, "LiveOpsConfig не назначен — система отключена.");
                return true;
            }

            // Авто-провайдер: если проект не установил свой — используем DefaultHttpLiveOpsProvider
            _provider = config.GetProvider();
            if (_provider == null && !string.IsNullOrEmpty(config.serverUrl))
            {
                if (!_playerIdOverridden) _playerId = GetOrCreateAnonymousId();
                _provider = new DefaultHttpLiveOpsProvider(
                    config.serverUrl, config.projectId, _playerId, config.requestTimeoutSeconds);
                config.SetProvider(_provider);
                ProtoLogger.LogInit(SystemId, "DefaultHttpLiveOpsProvider установлен автоматически.");
            }

            if (!_playerIdOverridden)
                _playerId = GetOrCreateAnonymousId();

            ProtoLogger.LogInit(SystemId, $"PlayerId: {_playerId} | Lang: {Language} | Project: {config.projectId}");

            ReportProgress(0.3f);

            // Health check
            try
            {
                if (_provider is DefaultHttpLiveOpsProvider httpProvider)
                {
                    ProtoLogger.LogInit(SystemId, "Health check...");
                    var pingProvider = new DefaultHttpLiveOpsProvider(
                        config.serverUrl, config.projectId, _playerId, config.healthCheckTimeoutSeconds);
                    _serverAvailable = await pingProvider.PingAsync();
                    ProtoLogger.LogInit(SystemId, _serverAvailable
                        ? "Health check: OK"
                        : "Health check: сервер недоступен, панель скрыта");
                }
                else if (_provider != null)
                {
                    // Кастомный провайдер — считаем сервер доступным
                    _serverAvailable = true;
                }
            }
            catch (Exception ex)
            {
                _serverAvailable = false;
                ProtoLogger.LogWarning(SystemId, $"Health check failed: {ex.Message}");
            }

            ReportProgress(0.5f);

            try
            {
                if (_serverAvailable)
                {
                    await FetchAsync();
                    await FlushAnalyticsQueueAsync();
                }
                else if (_provider == null)
                {
                    ProtoLogger.LogWarning(SystemId, "ILiveOpsProvider не установлен и serverUrl пуст. Задайте config.serverUrl.");
                }
            }
            catch (Exception ex)
            {
                ProtoLogger.LogWarning(SystemId, $"Fetch failed: {ex.Message}");
            }

            // Подписка на открытие главного меню
            if (config.fetchOnMainMenuOpen && !string.IsNullOrEmpty(config.mainMenuWindowName))
                EventBus.Subscribe(Evt.UI.WindowOpened, OnWindowOpened);

            ReportProgress(1.0f);
            return true;
        }

        private void OnWindowOpened(object data)
        {
            if (data is string windowName && windowName == config?.mainMenuWindowName)
                _ = FetchAsync();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(Evt.UI.WindowOpened, OnWindowOpened);
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (config == null || config.fetchIntervalSeconds <= 0f || _provider == null) return;

            _fetchTimer += Time.deltaTime;
            if (_fetchTimer >= config.fetchIntervalSeconds)
            {
                _fetchTimer = 0f;
                _ = FetchAsync();
                _ = FlushAnalyticsQueueAsync();
            }
        }

        #endregion

        #region Private

        private async Task SendEventWithFallbackAsync(LiveOpsEvent evt)
        {
            bool sent = await _provider.SendEventAsync(evt);
            if (!sent) EnqueueAnalytics(evt);
        }

        private void EnqueueAnalytics(LiveOpsEvent evt)
        {
            if (_analyticsQueue.Count >= config.analyticsQueueLimit) return;
            _analyticsQueue.Enqueue(evt);
        }

        private async Task FlushAnalyticsQueueAsync()
        {
            while (_analyticsQueue.Count > 0)
            {
                var evt = _analyticsQueue.Peek();
                bool sent = await _provider.SendEventAsync(evt);
                if (sent) _analyticsQueue.Dequeue();
                else break;
            }
        }

        private static string GetOrCreateAnonymousId()
        {
            const string key = "proto_player_id";
            if (!PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.SetString(key, Guid.NewGuid().ToString());
                PlayerPrefs.Save();
            }
            return PlayerPrefs.GetString(key);
        }

        #endregion
    }
}
