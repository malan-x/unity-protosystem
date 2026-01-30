// Packages/com.protosystem.core/Runtime/GameSession/GameSessionSystem.cs
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Центральная система координации жизненного цикла игровой сессии.
    /// Управляет состояниями: Ready → Starting → Playing → Paused/GameOver/Victory.
    /// 
    /// Принципы:
    /// - Факты vs Решения: Системы публикуют факты, GameSessionSystem принимает решения
    /// - Событийная координация: Сброс через события, не прямые вызовы
    /// - Не управляет Time.timeScale (это делает UITimeManager)
    /// 
    /// Для мультиплеера добавьте GameSessionNetworkSync на тот же GameObject.
    /// </summary>
    [ProtoSystemComponent("Game Session", "Управление жизненным циклом игровой сессии", 
        "Core", "🎮", 100)]
    public class GameSessionSystem : InitializableSystemBase, IResettable
    {
        #region InitializableSystemBase Implementation
        
        public override string SystemId => "game_session";
        public override string DisplayName => "Game Session System";
        public override string Description => "Управляет жизненным циклом игровой сессии: старт, пауза, рестарт, завершение и статистика.";
        
        #endregion
        
        #region Serialized Fields
        
        [Header("Configuration")]
        [SerializeField, InlineConfig] private GameSessionConfig config;
        
        #endregion
        
        #region State
        
        private SessionState _state = SessionState.None;
        private SessionEndReason _endReason = SessionEndReason.None;
        private bool _isVictory;
        private SessionStats _stats = new SessionStats();
        private Coroutine _restartCoroutine;
        
        // Сетевой синхронизатор (опционально)
        private IGameSessionNetworkSync _networkSync;
        
        #endregion
        
        #region Properties
        
        /// <summary>Текущее состояние сессии</summary>
        public SessionState State => _state;
        
        /// <summary>Причина завершения сессии</summary>
        public SessionEndReason EndReason => _endReason;
        
        /// <summary>Была ли победа</summary>
        public bool IsVictory => _isVictory;
        
        /// <summary>Статистика текущей сессии</summary>
        public SessionStats Stats => _stats;
        
        /// <summary>Конфигурация системы</summary>
        public GameSessionConfig Config => config;
        
        /// <summary>Подключен ли сетевой синхронизатор</summary>
        public bool HasNetworkSync => _networkSync != null;
        
        /// <summary>Является ли текущий клиент сервером/хостом</summary>
        public bool IsServer => _networkSync == null || _networkSync.IsServer;
        
        /// <summary>Может ли текущий клиент управлять сессией</summary>
        public bool CanControl => _networkSync == null || !config.hostAuthoritative || _networkSync.IsServer;
        
        // Удобные проверки
        public bool IsPlaying => State == SessionState.Playing;
        public bool IsPaused => State == SessionState.Paused;
        public bool IsGameOver => State == SessionState.GameOver || State == SessionState.Victory;
        public bool IsReady => State == SessionState.Ready;
        public bool IsStarting => State == SessionState.Starting;
        
        #endregion
        
        #region Events
        
        /// <summary>Вызывается при изменении состояния</summary>
        public event Action<SessionState, SessionState> OnStateChanged;
        
        /// <summary>Вызывается при завершении сессии</summary>
        public event Action<SessionEndReason, bool> OnSessionEnded;
        
        #endregion
        
        #region Initialization
        
        protected override void InitEvents()
        {
            // Базовая система не требует подписок на события
        }
        
        public override async Task<bool> InitializeAsync()
        {
            ReportProgress(0.2f);
            
            // Загружаем конфиг если не назначен
            if (config == null)
            {
                config = Resources.Load<GameSessionConfig>("GameSessionConfig");
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<GameSessionConfig>();
                    LogWarning("GameSessionConfig not found, using defaults");
                }
            }
            
            ReportProgress(0.5f);
            
            // Устанавливаем начальное состояние
            SetStateInternal(config.initialState);
            
            ReportProgress(0.8f);
            
            // Автостарт если настроено
            if (config.autoStartSession)
            {
                StartSession();
            }
            
            ReportProgress(1f);
            LogMessage("GameSessionSystem initialized");
            
            return true;
        }
        
        #endregion
        
        #region Network Sync Registration
        
        /// <summary>
        /// Регистрирует сетевой синхронизатор.
        /// Вызывается автоматически из GameSessionNetworkSync.
        /// </summary>
        internal void RegisterNetworkSync(IGameSessionNetworkSync sync)
        {
            _networkSync = sync;
            Log("Network sync registered");
        }
        
        /// <summary>
        /// Отменяет регистрацию сетевого синхронизатора.
        /// </summary>
        internal void UnregisterNetworkSync(IGameSessionNetworkSync sync)
        {
            if (_networkSync == sync)
            {
                _networkSync = null;
                Log("Network sync unregistered");
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Запустить новую сессию.
        /// Публикует Session.Reset, затем Session.Started.
        /// </summary>
        public void StartSession()
        {
            if (!CanControl)
            {
                LogWarning("Only host can start session");
                return;
            }
            
            if (State == SessionState.Playing)
            {
                LogWarning("Session already playing");
                return;
            }
            
            if (_networkSync != null && _networkSync.IsNetworkActive)
            {
                _networkSync.RequestStartSession();
            }
            else
            {
                StartSessionInternal();
            }
        }
        
        /// <summary>
        /// Перезапустить сессию (soft reset).
        /// </summary>
        public void RestartSession()
        {
            if (!CanControl)
            {
                LogWarning("Only host can restart session");
                return;
            }
            
            if (_networkSync != null && _networkSync.IsNetworkActive)
            {
                _networkSync.RequestRestartSession();
            }
            else
            {
                RestartSessionInternal();
            }
        }
        
        /// <summary>
        /// Поставить на паузу (только меняет State, не timeScale).
        /// </summary>
        public void PauseSession()
        {
            if (State != SessionState.Playing)
            {
                LogWarning($"Cannot pause from state {State}");
                return;
            }
            
            if (_networkSync != null && _networkSync.IsNetworkActive)
            {
                _networkSync.RequestPauseSession();
            }
            else
            {
                PauseSessionInternal();
            }
        }
        
        /// <summary>
        /// Продолжить игру (только меняет State, не timeScale).
        /// </summary>
        public void ResumeSession()
        {
            if (State != SessionState.Paused)
            {
                LogWarning($"Cannot resume from state {State}");
                return;
            }
            
            if (_networkSync != null && _networkSync.IsNetworkActive)
            {
                _networkSync.RequestResumeSession();
            }
            else
            {
                ResumeSessionInternal();
            }
        }
        
        /// <summary>
        /// Завершить сессию с указанной причиной.
        /// </summary>
        public void EndSession(SessionEndReason reason, bool isVictory = false)
        {
            if (State == SessionState.GameOver || State == SessionState.Victory)
            {
                LogWarning("Session already ended");
                return;
            }
            
            if (_networkSync != null && _networkSync.IsNetworkActive)
            {
                _networkSync.RequestEndSession(reason, isVictory);
            }
            else
            {
                EndSessionInternal(reason, isVictory);
            }
        }
        
        /// <summary>
        /// Вернуться в главное меню.
        /// Публикует Session.Reset и переводит в состояние Ready.
        /// </summary>
        public void ReturnToMenu()
        {
            if (_networkSync != null && _networkSync.IsNetworkActive)
            {
                _networkSync.RequestReturnToMenu();
            }
            else
            {
                ReturnToMenuInternal();
            }
        }
        
        #endregion
        
        #region IResettable
        
        public void ResetState(object resetData = null)
        {
            _stats.Reset();
            _endReason = SessionEndReason.None;
            _isVictory = false;
            
            if (_restartCoroutine != null)
            {
                StopCoroutine(_restartCoroutine);
                _restartCoroutine = null;
            }
            
            Log("GameSessionSystem state reset");
        }
        
        #endregion
        
        #region Internal Methods (вызываются напрямую или через NetworkSync)
        
        /// <summary>
        /// Внутренний метод запуска сессии.
        /// Вызывается напрямую или через сетевой sync.
        /// </summary>
        internal void StartSessionInternal()
        {
            SetStateInternal(SessionState.Starting);
            
            // Сброс всех систем
            ResetAllSystems();
            
            // Запуск после задержки
            if (config.restartDelay > 0)
            {
                _restartCoroutine = StartCoroutine(StartAfterDelay());
            }
            else
            {
                CompleteStart();
            }
        }
        
        private IEnumerator StartAfterDelay()
        {
            yield return new WaitForSecondsRealtime(config.restartDelay);
            CompleteStart();
            _restartCoroutine = null;
        }
        
        private void CompleteStart()
        {
            SetStateInternal(SessionState.Playing);
            _stats.StartTimer();
            
            EventBus.Publish(EventBus.Session.Started, null);
            Log("Session started");
        }
        
        /// <summary>
        /// Внутренний метод рестарта сессии.
        /// </summary>
        internal void RestartSessionInternal()
        {
            EventBus.Publish(EventBus.Session.RestartRequested, null);
            
            if (config.trackRestarts)
            {
                _stats.RestartCount++;
            }
            
            // Если сессия активна - сначала завершаем
            if (State == SessionState.Playing || State == SessionState.Paused)
            {
                _endReason = SessionEndReason.ManualRestart;
            }
            
            StartSessionInternal();
            Log($"Session restarted (count: {_stats.RestartCount})");
        }
        
        /// <summary>
        /// Внутренний метод паузы.
        /// </summary>
        internal void PauseSessionInternal()
        {
            SetStateInternal(SessionState.Paused);
            EventBus.Publish(EventBus.Session.Paused, null);
            Log("Session paused");
        }
        
        /// <summary>
        /// Внутренний метод возобновления.
        /// </summary>
        internal void ResumeSessionInternal()
        {
            SetStateInternal(SessionState.Playing);
            EventBus.Publish(EventBus.Session.Resumed, null);
            Log("Session resumed");
        }
        
        /// <summary>
        /// Внутренний метод завершения сессии.
        /// </summary>
        internal void EndSessionInternal(SessionEndReason reason, bool isVictory)
        {
            _stats.UpdateTime();
            _endReason = reason;
            _isVictory = isVictory;
            
            var finalState = isVictory ? SessionState.Victory : SessionState.GameOver;
            SetStateInternal(finalState);
            
            var data = new SessionEndedData
            {
                FinalState = finalState,
                Reason = reason,
                IsVictory = isVictory,
                SessionTime = _stats.SessionTime,
                Stats = _stats
            };
            
            EventBus.Publish(EventBus.Session.Ended, data);
            OnSessionEnded?.Invoke(reason, isVictory);
            
            Log($"Session ended: {reason}, Victory: {isVictory}, Time: {_stats.SessionTime:F1}s");
        }
        
        /// <summary>
        /// Внутренний метод возврата в меню.
        /// </summary>
        internal void ReturnToMenuInternal()
        {
            _endReason = SessionEndReason.ReturnToMenu;
            
            // Сброс
            ResetAllSystems();
            _stats.FullReset();
            
            SetStateInternal(SessionState.Ready);
            
            EventBus.Publish(EventBus.Session.ReturnedToMenu, null);
            Log("Returned to menu");
        }
        
        /// <summary>
        /// Устанавливает состояние напрямую (используется sync для репликации).
        /// </summary>
        internal void SetStateInternal(SessionState newState)
        {
            var prevState = _state;
            if (prevState == newState) return;
            
            _state = newState;
            
            var data = new SessionStateChangedData
            {
                PreviousState = prevState,
                NewState = newState
            };
            
            EventBus.Publish(EventBus.Session.StateChanged, data);
            OnStateChanged?.Invoke(prevState, newState);
            
            if (config != null && config.verboseLogging)
            {
                Log($"State: {prevState} → {newState}");
            }
        }
        
        /// <summary>
        /// Устанавливает данные завершения (используется sync для репликации).
        /// </summary>
        internal void SetEndDataInternal(SessionEndReason reason, bool isVictory)
        {
            _endReason = reason;
            _isVictory = isVictory;
        }
        
        private void ResetAllSystems(object resetData = null)
        {
            EventBus.Publish(EventBus.Session.Reset, null);
            
            // Автоматический вызов ResetState для всех IResettable
            var manager = SystemInitializationManager.Instance;
            if (manager != null)
            {
                manager.ResetAllResettableSystems(resetData);
            }
        }
        
        #endregion
        
        #region Unity Callbacks
        
        private void Update()
        {
            // Обновляем время только когда играем
            if (State == SessionState.Playing)
            {
                _stats.UpdateTime();
            }
        }
        
        #endregion
        
        #region Debug
        
        private void Log(string message)
        {
            if (config != null && config.logEvents)
            {
                LogRuntime(message);
            }
        }
        
        [ContextMenu("Debug: Start Session")]
        private void DebugStartSession() => StartSession();
        
        [ContextMenu("Debug: Restart Session")]
        private void DebugRestartSession() => RestartSession();
        
        [ContextMenu("Debug: Pause")]
        private void DebugPause() => PauseSession();
        
        [ContextMenu("Debug: Resume")]
        private void DebugResume() => ResumeSession();
        
        [ContextMenu("Debug: End (Game Over)")]
        private void DebugGameOver() => EndSession(SessionEndReason.PlayerDeath, false);
        
        [ContextMenu("Debug: End (Victory)")]
        private void DebugVictory() => EndSession(SessionEndReason.MissionComplete, true);
        
        [ContextMenu("Debug: Return To Menu")]
        private void DebugReturnToMenu() => ReturnToMenu();
        
        [ContextMenu("Debug: Print Stats")]
        private void DebugPrintStats() => LogRuntime(_stats.ToString());
        
        #endregion
    }
}
