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
        private List<LiveOpsMessage> _messages = new();
        private List<LiveOpsPoll> _polls = new();
        private Queue<LiveOpsEvent> _analyticsQueue = new();
        private float _fetchTimer;
        private string _playerId;
        private bool _playerIdOverridden;

        #endregion

        #region Events

        /// <summary>Вызывается после обновления сообщений.</summary>
        public event Action<List<LiveOpsMessage>> OnMessagesUpdated;

        /// <summary>Вызывается после обновления опросов.</summary>
        public event Action<List<LiveOpsPoll>> OnPollsUpdated;

        #endregion

        #region Public API

        /// <summary>Последний полученный список сообщений.</summary>
        public IReadOnlyList<LiveOpsMessage> Messages => _messages;

        /// <summary>Последний полученный список опросов.</summary>
        public IReadOnlyList<LiveOpsPoll> Polls => _polls;

        /// <summary>Текущий идентификатор игрока.</summary>
        public string PlayerId => _playerId;

        /// <summary>
        /// Установить идентификатор игрока до InitializeAsync().
        /// Например, ник из Steam: SetPlayerId(SteamFriends.GetPersonaName()).
        /// Если не вызван — используется анонимный GUID из PlayerPrefs.
        /// </summary>
        public void SetPlayerId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _playerId = id;
            _playerIdOverridden = true;
        }

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

        /// <summary>Отправить фидбек от игрока.</summary>
        public async Task<bool> SubmitFeedbackAsync(string message, string category = "other")
        {
            if (config == null || !config.enableFeedback || _provider == null) return false;

            var feedback = new LiveOpsFeedback(_playerId, Application.version, message, category);
            return await _provider.SubmitFeedbackAsync(feedback);
        }

        /// <summary>Отправить ответ на опрос.</summary>
        public async Task<bool> SubmitPollAnswerAsync(string pollId, int optionIndex)
        {
            if (config == null || !config.enablePolls || _provider == null) return false;

            var answer = new LiveOpsPollAnswer
            {
                pollId = pollId,
                optionIndex = optionIndex,
                playerId = _playerId
            };
            return await _provider.SubmitPollAnswerAsync(answer);
        }

        /// <summary>Принудительно обновить сообщения и опросы.</summary>
        public async Task FetchAsync()
        {
            if (_provider == null) return;

            if (config.enableMessages)
            {
                var messages = await _provider.FetchMessagesAsync();
                if (messages != null)
                {
                    _messages = messages;
                    OnMessagesUpdated?.Invoke(_messages);
                }
            }

            if (config.enablePolls)
            {
                var polls = await _provider.FetchPollsAsync();
                if (polls != null)
                {
                    _polls = polls;
                    OnPollsUpdated?.Invoke(_polls);
                }
            }
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

            _provider = config.GetProvider();

            // Если SetPlayerId() не вызван — используем анонимный GUID
            if (!_playerIdOverridden)
                _playerId = GetOrCreateAnonymousId();

            ProtoLogger.LogInit(SystemId, $"PlayerId: {_playerId}");

            ReportProgress(0.5f);

            if (_provider != null)
            {
                await FetchAsync();
                await FlushAnalyticsQueueAsync();
            }
            else
            {
                ProtoLogger.LogWarning(SystemId, "ILiveOpsProvider не установлен. Вызовите config.SetProvider() до инициализации.");
            }

            ReportProgress(1.0f);
            return true;
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
