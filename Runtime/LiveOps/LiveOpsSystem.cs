// Packages/com.protosystem.core/Runtime/LiveOps/LiveOpsSystem.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

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
        // Телеметрия: события копятся и уходят пачкой (см. LiveOpsTelemetryBatch)
        private float                     _telemetryTimer;
        private float                     _sinceLastSend;
        private bool                      _telemetrySending;
        private bool                      _sessionStartSent;
        private string                    _deviceTag;
        private bool                      _specsSent;
        private string                    _playerId;
        private string                    _playerName;
        private bool                      _playerIdOverridden;

        /// <summary>
        /// Сколько ждать финальный id игрока (SetPlayerId из проекта), прежде чем
        /// отправить первую пачку. Steam инициализируется после LiveOps, и без этой
        /// паузы session_start уезжал под анонимным id, плодя «фантомных игроков».
        /// </summary>
        private const float PlayerIdGraceSeconds = 5f;

        private bool                      _awaitingPlayerId;
        private float                     _playerIdWait;

        // Community Panel
        private LiveOpsPanelConfig        _panelConfig;
        private string                    _notifyAt;
        private List<LiveOpsAnnouncement> _announcements  = new();
        private LiveOpsDevLog             _devLog;
        private LiveOpsRatingData         _rating;
        private LiveOpsMilestoneData      _milestone;
        private LiveOpsContentOrder       _contentOrder;
        private List<LiveOpsConversationItem> _myMessages = new();
        private int                       _unreadCount;
        private LiveOpsPlayerContext      _playerContext  = new(0, 0);
        // Panel registration
        private ILiveOpsPanel _panel;
        private bool _serverAvailable;
        private bool _hasData;

        #endregion

        #region Events

        public event Action<List<LiveOpsMessage>> OnMessagesUpdated;
        public event Action<List<LiveOpsPoll>>    OnPollsUpdated;
        public event Action<int>                  OnUnreadCountChanged;

        #endregion

        #region Public API

        public IReadOnlyList<LiveOpsMessage>      Messages       => _messages;
        public IReadOnlyList<LiveOpsPoll>          Polls          => _polls;
        public IReadOnlyList<LiveOpsAnnouncement> Announcements  => _announcements;
        public LiveOpsDevLog                      DevLog         => _devLog;
        public LiveOpsRatingData                  Rating         => _rating;
        public LiveOpsMilestoneData               Milestone      => _milestone;
        public LiveOpsPanelConfig                 PanelConfig    => _panelConfig;

        /// <summary>Метка «оповестить игроков» (ISO-время из дашборда); пусто — не запрашивалась.</summary>
        public string                             NotifyAt       => _notifyAt;
        public LiveOpsContentOrder                ContentOrder   => _contentOrder;
        public IReadOnlyList<LiveOpsConversationItem> MyMessages => _myMessages;
        public int                                UnreadReplyCount => _unreadCount;
        public string                             PlayerId       => _playerId;
        public string                             PlayerName     => _playerName;
        public string                             Language          => Loc.IsReady ? Loc.CurrentLanguage : (config != null ? config.defaultLanguage : "en");
        public bool                               IsServerAvailable => _serverAvailable;

        /// <summary>
        /// Установить идентификатор игрока. Обычно вызывается ПОСЛЕ InitializeAsync():
        /// Steam поднимается позже LiveOps (та у него в зависимостях), поэтому система
        /// сама придерживает первую пачку телеметрии и переклеивает уже накопленные
        /// события на этот id — см. <see cref="PlayerIdGraceSeconds"/>.
        /// Не вызван вовсе — остаётся анонимный id машины.
        /// </summary>
        public void SetPlayerId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            string previous = _playerId;
            _playerId = id;
            _playerIdOverridden = true;
            if (string.IsNullOrEmpty(previous) || previous == id) return;

            // События, поставленные в очередь до подстановки (session_start и ранние
            // ачивки), помечены анонимным id машины. Не переклеить их — и на сервере
            // появится «фантомный игрок»: та же сессия, но под вторым ключом, с одним
            // событием и нулевым временем. Именно так дублировались Steam-игроки.
            int moved = 0;
            foreach (var evt in _analyticsQueue)
            {
                if (evt == null || evt.playerId != previous) continue;
                evt.playerId = id;
                moved++;
            }

            // Провайдер подписывает этим id голоса и оценки («мой голос», «моя оценка»)
            if (_provider is DefaultHttpLiveOpsProvider httpProvider) httpProvider.SetPlayerId(id);
            else if (_provider is PocketBaseHttpLiveOpsProvider pbProvider) pbProvider.SetPlayerId(id);

            LiveOpsLog.Info($"[LiveOps] PlayerId уточнён: {previous} → {id}, переклеено событий: {moved}");
        }

        /// <summary>
        /// Переопределить тег устройства (по умолчанию — платформа: windows/linux/mac).
        ///
        /// Нужен для Steam Deck: под Proton он выглядит как WindowsPlayer, и
        /// отличить его можно только через Steamworks. Вызывать из проекта:
        /// <code>
        /// if (SteamUtils.IsSteamRunningOnSteamDeck()) liveOps.SetDeviceTag("steamdeck");
        /// </code>
        /// Тег попадает в разрезы дашборда: игроки, сессии и часы по устройствам.
        /// </summary>
        public void SetDeviceTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            _deviceTag = tag.Trim().ToLowerInvariant();
            _specsSent = false; // перешлём конфигурацию с уже уточнённым устройством
        }

        /// <summary>
        /// Установить отображаемое имя игрока (например SteamFriends.GetPersonaName()).
        /// Передаётся на сервер в заголовке X-Player-Name.
        /// </summary>
        public void SetPlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            _playerName = name;
            if (_provider is DefaultHttpLiveOpsProvider httpProv)
            {
                httpProv.SetPlayerName(name);
                LiveOpsLog.Info($"[LiveOps] PlayerName → '{name}' (DefaultHttp)");
            }
            else if (_provider is PocketBaseHttpLiveOpsProvider pbProv)
            {
                pbProv.SetPlayerName(name);
                LiveOpsLog.Info($"[LiveOps] PlayerName → '{name}' (PocketBase)");
            }
            else
            {
                Debug.LogWarning($"[LiveOps] PlayerName → '{name}' но провайдер = {(_provider == null ? "NULL" : _provider.GetType().Name)}");
            }
        }

        /// <summary>
        /// Установить контекст игрока для проверки условий show_after.
        /// Вызывать до InitializeAsync() или при изменении данных.
        /// </summary>
        public void SetPlayerContext(LiveOpsPlayerContext ctx) => _playerContext = ctx;

        /// <summary>
        /// Проверить, должен ли виджет панели быть виден при текущем контексте.
        /// Ключи: "cards", "messages", "goal", "rating".
        /// </summary>
        public bool IsWidgetVisible(string widgetKey)
        {
            if (_panelConfig == null) return true;
            var def = widgetKey switch
            {
                "cards"    => _panelConfig.cards,
                "messages" => _panelConfig.messages,
                "goal"     => _panelConfig.goal,
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
        public void RegisterPanel(ILiveOpsPanel panel)
        {
            _panel = panel;

            // Если система ещё не инициализирована — просто сохраняем ссылку.
            // InitializeAsync() сам управит панелью после завершения.
            if (!IsInitializedDependencies)
            {
                LiveOpsLog.Info($"[{SystemId}] RegisterPanel: ещё не инициализирована, сохраняю ссылку");
                return;
            }

            // Система уже готова — сразу управляем видимостью
            LiveOpsLog.Info($"[{SystemId}] RegisterPanel: initialized=true, serverAvailable={_serverAvailable}, hasData={_hasData}, panelConfig={(_panelConfig != null ? "OK" : "NULL")}");
            if (!_serverAvailable)
            {
                panel.SetPanelVisible(false);
                return;
            }
            panel.SetPanelVisible(true);
            if (_hasData) PushAllDataToEventBus();
            if (config != null && config.fetchOnPanelOpen) _ = SafeFetchAsync();
        }

        /// <summary>Отписать панель от системы.</summary>
        public void UnregisterPanel(ILiveOpsPanel panel)
        {
            if (_panel == panel) _panel = null;
        }

        /// <summary>Принудительно запросить данные с сервера (например по кнопке в UI).</summary>
        public void TriggerFetch() => _ = SafeFetchAsync();

        /// <summary>
        /// Отправить аналитическое событие.
        ///
        /// Событие не уходит немедленно: оно копится в буфере и отправляется
        /// пачкой раз в <c>telemetryFlushSeconds</c> — иначе один заезд
        /// превратился бы в очередь HTTP-запросов. Если провайдера нет или
        /// сеть недоступна, буфер работает как offline-очередь.
        ///
        /// Эти же события служат сигналом присутствия: сервер считает игрока
        /// онлайн, пока они приходят (см. server/telemetry.pb.js в дашборде).
        /// </summary>
        /// <summary>
        /// Вариант баланса игрока (A/B-группа). По умолчанию "1"; назначение
        /// приходит с сервера при старте (POST /api/ab/assign) и кэшируется
        /// для оффлайна. Каждый батч телеметрии уезжает с ним — агрегаты
        /// дашборда делятся по вариантам.
        /// </summary>
        public string Variant { get; set; } = "1";

        /// <summary>Вариант изменился (строка-вариант) — игра применяет свой баланс.</summary>
        public event Action<string> VariantChanged;

        /// <summary>
        /// Заезды проводит бот (авто-фармер), не человек. Сервер выносит такие
        /// сессии в отдельный срез статистики (суффикс ".bot" у project_id) —
        /// боты не искажают ни метрики баланса, ни DAU. Ставить ДО первого
        /// батча (сразу после инициализации).
        /// </summary>
        public bool BotMode { get; set; }

        private const string VARIANT_PREF_KEY = "ProtoSystem.AB.Variant";

        /// <summary>
        /// Спросить у сервера A/B-вариант игрока. Сервер назначает по конфигу
        /// эксперимента (веса-доли или квоты «ровно N игроков») и персистит
        /// решение — повторный вход стабилен. Оффлайн — кэш прошлого ответа,
        /// до первого контакта — "1" (дефолтный баланс).
        /// </summary>
        private async Task FetchAbVariantAsync()
        {
            // Кэш применяем сразу: если сеть упадёт ниже, играем прошлым назначением
            string cached = PlayerPrefs.GetString(VARIANT_PREF_KEY, "");
            if (!string.IsNullOrEmpty(cached))
            {
                LoadCachedOverrides();
                ApplyVariant(cached);
            }

            if (string.IsNullOrEmpty(config.serverUrl)) return;

            try
            {
                string url = config.serverUrl.TrimEnd('/') + "/api/ab/assign";
                string body = "{\"project\":\"" + config.projectId + "\",\"playerId\":\"" + _playerId + "\"}";

                using var req = new UnityWebRequest(url, "POST");
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = Mathf.Max(2, Mathf.RoundToInt(config.healthCheckTimeoutSeconds));

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result != UnityWebRequest.Result.Success) return;

                var resp = JsonUtility.FromJson<AbAssignResponse>(req.downloadHandler.text);
                if (resp == null || string.IsNullOrEmpty(resp.variant)) return;

                PlayerPrefs.SetString(VARIANT_PREF_KEY, resp.variant);
                SetOverrides(resp.overrides, persist: true);
                ApplyVariant(resp.variant);
                if (resp.variant != "1")
                    ProtoLogger.LogRuntime(SystemId, $"A/B: вариант {resp.variant}" +
                        (string.IsNullOrEmpty(resp.experiment) ? "" : $" (эксперимент '{resp.experiment}')"));
            }
            catch (Exception ex)
            {
                ProtoLogger.LogWarning(SystemId, $"A/B assign failed: {ex.Message}");
            }
        }

        private void ApplyVariant(string variant)
        {
            // Событие шлём и при том же варианте: оверрайды могли измениться
            // на сервере, а подписчики перечитывают их снапшотом по событию
            Variant = variant;
            try { VariantChanged?.Invoke(variant); }
            catch (Exception ex) { ProtoLogger.LogWarning(SystemId, $"VariantChanged handler: {ex.Message}"); }
        }

        [Serializable]
        private class AbAssignResponse
        {
            public string variant;
            public string experiment;
            public AbOverride[] overrides;
        }

        [Serializable]
        private class AbOverride { public string k; public float v; }

        [Serializable]
        private class AbOverrideList { public AbOverride[] items; }

        // ── Оверрайды баланса варианта ──
        // Сервер отдаёт вместе с назначением пары «ключ → число»; игра применяет
        // их по СВОЕМУ белому списку — так дашборд меняет баланс без обновления
        // билда. Кэшируются для оффлайна вместе с вариантом.
        private readonly Dictionary<string, float> _abOverrides = new Dictionary<string, float>();
        private const string OVERRIDES_PREF_KEY = "ProtoSystem.AB.Overrides";

        /// <summary>Оверрайд баланса из назначенного варианта, иначе дефолт.</summary>
        public float GetBalanceOverride(string key, float defaultValue)
            => _abOverrides.TryGetValue(key, out float v) ? v : defaultValue;

        /// <summary>Снимок оверрайдов (для зеркалирования в игру).</summary>
        public Dictionary<string, float> GetBalanceOverridesSnapshot()
            => new Dictionary<string, float>(_abOverrides);

        private void SetOverrides(AbOverride[] pairs, bool persist)
        {
            _abOverrides.Clear();
            if (pairs != null)
                foreach (var p in pairs)
                    if (p != null && !string.IsNullOrEmpty(p.k)) _abOverrides[p.k] = p.v;

            if (persist)
            {
                var wrap = new AbOverrideList { items = pairs ?? Array.Empty<AbOverride>() };
                PlayerPrefs.SetString(OVERRIDES_PREF_KEY, JsonUtility.ToJson(wrap));
            }
        }

        private void LoadCachedOverrides()
        {
            try
            {
                string raw = PlayerPrefs.GetString(OVERRIDES_PREF_KEY, "");
                if (string.IsNullOrEmpty(raw)) return;
                var wrap = JsonUtility.FromJson<AbOverrideList>(raw);
                SetOverrides(wrap?.items, persist: false);
            }
            catch (Exception) { }
        }

        public void TrackEvent(string eventName, Dictionary<string, string> data = null)
        {
            if (config == null || !config.enableAnalytics || string.IsNullOrEmpty(eventName)) return;

            EnqueueAnalytics(new LiveOpsEvent(eventName, _playerId, Application.version, data));

            // пачка набралась раньше таймера — отправляем сразу (но не пока ждём
            // уточнения id: эти события ещё переклеятся на него в SetPlayerId)
            if (_provider != null && _serverAvailable && !_awaitingPlayerId
                && _analyticsQueue.Count >= Mathf.Max(1, config.telemetryBatchLimit))
                _ = FlushTelemetryAsync();
        }

        /// <summary>Отправить фидбек/сообщение от игрока.</summary>
        public async Task<bool> SubmitFeedbackAsync(string message, string category = "other", string tag = "general")
        {
            if (config == null || !config.enableFeedback || _provider == null) return false;
            var feedback = new LiveOpsFeedback(_playerId, Application.version, message, Language, category, tag);
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

        /// <summary>Загрузить переписку текущего игрока.</summary>
        public async Task FetchMyMessagesAsync()
        {
            LiveOpsLog.Info($"[LiveOps] FetchMyMessagesAsync: provider={(_provider != null ? "OK" : "NULL")}, playerId={_playerId}");
            if (_provider == null) return;
            var items = await _provider.FetchMyMessagesAsync(_playerId);
            LiveOpsLog.Info($"[LiveOps] FetchMyMessagesAsync: got {(items != null ? items.Count.ToString() : "null")} items");
            if (items != null)
            {
                _myMessages = items;
                RecalcUnread();
                EventBus.Publish(Evt.LiveOps.DataUpdated,
                    new LiveOpsDataPayload(LiveOpsDataType.MyMessages, _myMessages));

                // Подтверждаем получение ответов со статусом "sent" → "delivered"
                var sentIds = new List<string>();
                foreach (var m in _myMessages)
                    if (!string.IsNullOrEmpty(m.reply) && m.reply_status == "sent")
                        sentIds.Add(m.id);

                if (sentIds.Count > 0)
                {
                    LiveOpsLog.Info($"[LiveOps] ConfirmReplies: {sentIds.Count} sent replies");
                    await _provider.ConfirmRepliesAsync(sentIds.ToArray());
                    foreach (var m in _myMessages)
                        if (sentIds.Contains(m.id))
                            m.reply_status = "delivered";
                }

            }
        }

        /// <summary>Пометить все ответы как прочитанные.</summary>
        public void MarkAllRepliesRead()
        {
            var readSet = GetReadMessageIds();
            foreach (var m in _myMessages)
                if (!string.IsNullOrEmpty(m.reply))
                    readSet.Add(m.id);
            SaveReadMessageIds(readSet);
            RecalcUnread();
        }

        private void PushAllDataToEventBus()
        {
            // Публикуем PanelConfig всегда — даже null: панель использует IsWidgetVisible(),
            // который при null возвращает true (показывать всё по умолчанию).
            EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.PanelConfig, _panelConfig));
            if (_contentOrder    != null) EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.ContentOrder,    _contentOrder));
            if (_polls?.Count   > 0)    EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Polls,           _polls));
            if (_announcements?.Count > 0) EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Announcements, _announcements));
            if (_devLog         != null) EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.DevLog,         _devLog));
            if (_rating         != null) EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Rating,         _rating));
            if (_milestone      != null) EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Milestone,     _milestone));
            if (_messages?.Count > 0)   EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Messages,       _messages));
            if (_myMessages?.Count > 0) EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.MyMessages,     _myMessages));
        }

        /// <summary>
        /// Регистрирует серверные переводы как рантайм-ключи в Loc для текущего языка.
        /// Вызывается после загрузки данных и при смене языка.
        /// </summary>
        private void PushLocalizationKeys()
        {
            var lang = Language;
            if (_milestone != null)
            {
                _milestone.title.RegisterInLoc("liveops.goal.title", lang);
                _milestone.unit.RegisterInLoc("liveops.goal.unit", lang);

                // Комбинированный ключ title+desc для UI
                var title = _milestone.title.Get(lang);
                var desc  = _milestone.description.Get(lang);
                var combined = string.IsNullOrEmpty(title) ? desc : $"{title}\n{desc}";
                if (!string.IsNullOrEmpty(combined))
                    Loc.Set("liveops.goal.desc", combined);
            }

            if (_announcements != null)
            {
                foreach (var ann in _announcements)
                {
                    ann.title.RegisterInLoc($"liveops.ann.{ann.id}.title", lang);
                    ann.body.RegisterInLoc($"liveops.ann.{ann.id}.body", lang);
                }
            }

            if (_devLog != null)
            {
                _devLog.focus.RegisterInLoc("liveops.devlog.focus", lang);
                _devLog.title.RegisterInLoc("liveops.devlog.title", lang);
                _devLog.description.RegisterInLoc("liveops.devlog.desc", lang);
                for (int idx = 0; idx < _devLog.items.Length; idx++)
                    _devLog.items[idx].name.RegisterInLoc($"liveops.devlog.item.{idx}", lang);
            }

            if (_polls != null)
            {
                foreach (var poll in _polls)
                {
                    poll.question.RegisterInLoc($"liveops.poll.{poll.id}.q", lang);
                    for (int idx = 0; idx < poll.options.Length; idx++)
                        poll.options[idx].label.RegisterInLoc($"liveops.poll.{poll.id}.opt.{idx}", lang);
                }
            }
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

            // Метка «оповестить игроков» из дашборда: панель сравнит её со своим
            // acknowledgement и подсветит карточки как непрочитанные (один раз)
            var notifyAt = await _provider.FetchNotifyAtAsync();
            if (!string.IsNullOrEmpty(notifyAt))
                _notifyAt = notifyAt;

            // Content order — порядок карточек в карусели
            var contentOrder = await _provider.FetchContentOrderAsync();
            if (contentOrder != null)
            {
                _contentOrder = contentOrder;
                EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.ContentOrder, _contentOrder));
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

            if (config.enableGoal)
            {
                var milestone = await _provider.FetchMilestoneAsync();
                if (milestone != null)
                {
                    _milestone = milestone;
                    EventBus.Publish(Evt.LiveOps.DataUpdated, new LiveOpsDataPayload(LiveOpsDataType.Milestone, _milestone));
                }
            }

            // Переписка игрока
            if (config.enableFeedback)
                await FetchMyMessagesAsync();

            _hasData = true;
            PushLocalizationKeys();
        }

        #endregion

        #region InitializableSystemBase Implementation

        protected override void InitEvents()
        {
            AddEvent(EventBus.Localization.LanguageChanged, OnLanguageChanged);
        }

        private void OnLanguageChanged(object _) => PushLocalizationKeys();

        public override async Task<bool> InitializeAsync()
        {
            ReportProgress(0.1f);

            if (config == null)
            {
                ProtoLogger.LogWarning(SystemId, "LiveOpsConfig не назначен — система отключена.");
                return true;
            }

            LiveOpsLog.Verbose = config.verboseLogging;

            // Авто-провайдер: если проект не установил свой — создаём по типу из конфига
            _provider = config.GetProvider();
            if (_provider == null && !string.IsNullOrEmpty(config.serverUrl))
            {
                if (!_playerIdOverridden) _playerId = GetOrCreateAnonymousId();
                _provider = config.CreateProvider(_playerId);
                config.SetProvider(_provider);
                ProtoLogger.LogInit(SystemId, $"{_provider.GetType().Name} установлен автоматически.");
            }

            if (!_playerIdOverridden)
                _playerId = GetOrCreateAnonymousId();

            // Передаём имя игрока в провайдер (если задано)
            if (!string.IsNullOrEmpty(_playerName))
            {
                if (_provider is DefaultHttpLiveOpsProvider httpProv)
                    httpProv.SetPlayerName(_playerName);
                else if (_provider is PocketBaseHttpLiveOpsProvider pbProv)
                    pbProv.SetPlayerName(_playerName);
            }

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
                    await FetchAbVariantAsync();
                    TrackSessionStart();

                    // Ждём, не уточнит ли проект id (Steam стартует после нас). Пачка
                    // уйдёт из Update — сразу после SetPlayerId или по истечении паузы
                    _awaitingPlayerId = !_playerIdOverridden;
                    if (!_awaitingPlayerId) await FlushTelemetryAsync();
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

            // Если панель уже зарегистрировалась до завершения инициализации — управляем ею
            if (_panel != null)
            {
                if (_serverAvailable)
                {
                    _panel.SetPanelVisible(true);
                    if (_hasData) PushAllDataToEventBus();
                }
                else
                {
                    _panel.SetPanelVisible(false);
                }
            }

            // Подписка на открытие главного меню
            if (config.fetchOnMainMenuOpen && !string.IsNullOrEmpty(config.mainMenuWindowName))
                EventBus.Subscribe(Evt.UI.WindowOpened, OnWindowOpened);

            ReportProgress(1.0f);
            return true;
        }

        private void OnWindowOpened(object data)
        {
            // Навигатор публикует WindowEventData; string — на случай ручных публикаций
            string windowName = data switch
            {
                ProtoSystem.UI.WindowEventData wed => wed.WindowId,
                string s => s,
                _ => null
            };

            if (!string.IsNullOrEmpty(windowName) && windowName == config?.mainMenuWindowName)
                _ = SafeFetchAsync();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(Evt.UI.WindowOpened, OnWindowOpened);
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (config == null || _provider == null || !_serverAvailable) return;

            if (config.fetchIntervalSeconds > 0f)
            {
                _fetchTimer += Time.deltaTime;
                if (_fetchTimer >= config.fetchIntervalSeconds)
                {
                    _fetchTimer = 0f;
                    _ = SafeFetchAsync();
                }
            }

            if (!config.enableAnalytics) return;

            // Первая пачка держится, пока проект не уточнит id игрока (Steam) или пока
            // не выйдет пауза: иначе session_start уедет под анонимным id машины
            if (_awaitingPlayerId)
            {
                _playerIdWait += Time.unscaledDeltaTime;
                if (!_playerIdOverridden && _playerIdWait < PlayerIdGraceSeconds) return;

                _awaitingPlayerId = false;
                _telemetryTimer = 0f;
                _ = FlushTelemetryAsync();
                return;
            }

            _telemetryTimer += Time.deltaTime;
            _sinceLastSend  += Time.deltaTime;

            if (_telemetryTimer < Mathf.Max(1f, config.telemetryFlushSeconds)) return;
            _telemetryTimer = 0f;

            // событий нет слишком долго — шлём пустую пачку как признак жизни
            bool needTick = config.telemetryTickSeconds > 0f && _sinceLastSend >= config.telemetryTickSeconds;
            if (_analyticsQueue.Count > 0 || needTick)
                _ = FlushTelemetryAsync(needTick);
        }

        private void OnApplicationQuit()
        {
            if (config == null || !config.enableAnalytics || _provider == null || !_serverAvailable) return;

            // Вышли, не дождавшись уточнения id (игра закрыта в первые секунды) —
            // лучше отправить события под анонимным id, чем потерять сессию
            _awaitingPlayerId = false;

            TrackEvent("session_end");

            // ВАЖНО: НЕ стартуем здесь новый веб-запрос. Игровой цикл уже сворачивается,
            // continuation после await не выполнится, req.timeout движком не отсчитается —
            // и UnityWebRequest остаётся с открытым сокетом, из-за чего процесс переживал
            // закрытие окна (в Steam игра числилась «Запущено», жалоба игрока 22.08).
            // Сессию закроет сервер по TTL, ровно как и раньше при недоставленном запросе.
            // Заодно обрываем то, что уже летит: провайдер отменяет свои запросы.
            (_provider as IDisposable)?.Dispose();
        }

        #endregion

        #region Private

        /// <summary>
        /// Fetch с защитой от исключений: вызывается fire-and-forget (_ = SafeFetchAsync()),
        /// где упавший Task иначе молча теряется вместе с ошибкой.
        /// </summary>
        private async Task SafeFetchAsync()
        {
            try
            {
                await FetchAsync();
                await FlushTelemetryAsync();
            }
            catch (Exception ex)
            {
                ProtoLogger.LogWarning(SystemId, $"Fetch failed: {ex.Message}");
            }
        }

        private void EnqueueAnalytics(LiveOpsEvent evt)
        {
            if (_analyticsQueue.Count >= config.analyticsQueueLimit) return;
            _analyticsQueue.Enqueue(evt);
        }

        /// <summary>
        /// Отправить накопленные события одной пачкой.
        /// <paramref name="force"/> = отправить, даже если событий нет: пустая
        /// пачка служит признаком жизни (tick), чтобы игрок не выпал из онлайна.
        /// </summary>
        private async Task FlushTelemetryAsync(bool force = false)
        {
            if (_provider == null || _telemetrySending) return;
            if (_analyticsQueue.Count == 0 && !force) return;

            var batch = new LiveOpsTelemetryBatch
            {
                playerId        = _playerId,
                playerName      = _playerName,
                version         = Application.version,
                lang            = Language,
                tzOffsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes,
                env             = Application.isEditor ? "editor" : "player",
                variant         = Variant,
                bot             = BotMode,
                build           = BuildInfo.Flavor.ToString().ToLowerInvariant(), // normal / demo / playtest
                device          = !string.IsNullOrEmpty(_deviceTag) ? _deviceTag : LiveOpsDeviceSpecs.PlatformTag(),
            };

            // Конфигурация машины статична — уходит один раз за сессию
            if (!_specsSent)
            {
                batch.specs = LiveOpsDeviceSpecs.Collect();
                _specsSent  = true;
            }

            int limit = Mathf.Max(1, config.telemetryBatchLimit);
            while (batch.events.Count < limit && _analyticsQueue.Count > 0)
                batch.events.Add(_analyticsQueue.Dequeue());

            _telemetrySending = true;
            bool sent = false;
            try
            {
                sent = await _provider.SendTelemetryAsync(batch);
            }
            catch (Exception ex)
            {
                ProtoLogger.LogWarning(SystemId, $"Telemetry send failed: {ex.Message}");
            }
            finally
            {
                _telemetrySending = false;
            }

            if (sent)
            {
                _sinceLastSend = 0f;
            }
            else
            {
                // сеть моргнула — возвращаем события в буфер (порядок не важен,
                // у каждого события свой timestamp), лишнее отсечёт лимит очереди
                foreach (var e in batch.events) EnqueueAnalytics(e);
                if (batch.specs != null) _specsSent = false; // конфигурация уйдёт со следующей пачкой
            }
        }

        /// <summary>Первое событие сессии — с него сервер начинает отсчёт времени игры.</summary>
        private void TrackSessionStart()
        {
            if (_sessionStartSent || config == null || !config.enableAnalytics) return;
            _sessionStartSent = true;
            TrackEvent("session_start", new Dictionary<string, string>
            {
                { "platform", Application.platform.ToString() },
            });
        }

        private static string GetOrCreateAnonymousId()
        {
            // Предпочитаем стабильный ID машины — одинаковый между сессиями
            var deviceId = UnityEngine.SystemInfo.deviceUniqueIdentifier;
            if (!string.IsNullOrEmpty(deviceId) && deviceId != UnityEngine.SystemInfo.unsupportedIdentifier)
                return deviceId;

            // Фоллбэк: PlayerPrefs GUID если deviceUniqueIdentifier недоступен
            const string key = "proto_player_id";
            if (!PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.SetString(key, Guid.NewGuid().ToString());
                PlayerPrefs.Save();
            }
            return PlayerPrefs.GetString(key);
        }

        // ── Unread tracking ──────────────────────────────────────────

        private void RecalcUnread()
        {
            var readSet = GetReadMessageIds();
            int count = 0;
            foreach (var m in _myMessages)
            {
                bool hasReply = !string.IsNullOrEmpty(m.reply);
                bool isRead = readSet.Contains(m.id);
                if (hasReply && !isRead)
                    count++;
                LiveOpsLog.Info($"[LiveOps] RecalcUnread: id={m.id}, reply={hasReply}, read={isRead}");
            }
            _unreadCount = count;
            LiveOpsLog.Info($"[LiveOps] RecalcUnread: total unread={_unreadCount}, subscribers={OnUnreadCountChanged?.GetInvocationList()?.Length ?? 0}");
            OnUnreadCountChanged?.Invoke(_unreadCount);
        }

        private const string ReadRepliesKey = "liveops_read_replies";

        private static System.Collections.Generic.HashSet<string> GetReadMessageIds()
        {
            var set = new System.Collections.Generic.HashSet<string>();
            var json = PlayerPrefs.GetString(ReadRepliesKey, "");
            if (string.IsNullOrEmpty(json) || json.Length <= 2) return set;
            // Ручной парсинг ["id1","id2"]
            json = json.Trim();
            if (json[0] != '[') return set;
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
                    set.Add(json.Substring(start, i - start));
                    if (i < json.Length) i++;
                }
                else i++;
            }
            return set;
        }

        private static void SaveReadMessageIds(System.Collections.Generic.HashSet<string> ids)
        {
            var sb = new System.Text.StringBuilder("[");
            bool first = true;
            foreach (var id in ids)
            {
                if (!first) sb.Append(',');
                sb.Append('"').Append(id).Append('"');
                first = false;
            }
            sb.Append(']');
            PlayerPrefs.SetString(ReadRepliesKey, sb.ToString());
            PlayerPrefs.Save();
        }

        #endregion
    }
}
