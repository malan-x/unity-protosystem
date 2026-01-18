// Packages/com.protosystem.core/Runtime/GameSession/GameSessionSystem.cs
using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
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
    /// </summary>
    [ProtoSystemComponent("Game Session", "Управление жизненным циклом игровой сессии", 
        "Core", "🎮", 100)]
    public class GameSessionSystem : NetworkInitializableSystem, IResettable
    {
        #region InitializableSystemBase Implementation
        
        public override string SystemId => "game_session";
        public override string DisplayName => "Game Session System";
        
        #endregion
        
        #region Serialized Fields
        
        [Header("Configuration")]
        [SerializeField] private GameSessionConfig config;
        
        #endregion
        
        #region Network Variables
        
        private NetworkVariable<int> _networkState = new NetworkVariable<int>(
            (int)SessionState.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
            
        private NetworkVariable<int> _networkEndReason = new NetworkVariable<int>(
            (int)SessionEndReason.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
            
        private NetworkVariable<bool> _networkIsVictory = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        
        #endregion
        
        #region State
        
        private SessionState _localState = SessionState.None;
        private SessionEndReason _endReason = SessionEndReason.None;
        private bool _isVictory;
        private SessionStats _stats = new SessionStats();
        private Coroutine _restartCoroutine;
        
        #endregion
        
        #region Properties
        
        /// <summary>Текущее состояние сессии</summary>
        public SessionState State => IsNetworkActive ? (SessionState)_networkState.Value : _localState;
        
        /// <summary>Причина завершения сессии</summary>
        public SessionEndReason EndReason => IsNetworkActive ? (SessionEndReason)_networkEndReason.Value : _endReason;
        
        /// <summary>Была ли победа</summary>
        public bool IsVictory => IsNetworkActive ? _networkIsVictory.Value : _isVictory;
        
        /// <summary>Статистика текущей сессии</summary>
        public SessionStats Stats => _stats;
        
        /// <summary>Конфигурация системы</summary>
        public GameSessionConfig Config => config;
        
        // Удобные проверки
        public bool IsPlaying => State == SessionState.Playing;
        public bool IsPaused => State == SessionState.Paused;
        public bool IsGameOver => State == SessionState.GameOver || State == SessionState.Victory;
        public bool IsReady => State == SessionState.Ready;
        public bool IsStarting => State == SessionState.Starting;
        
        private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        private bool IsServer => !IsNetworkActive || NetworkManager.Singleton.IsServer;
        private bool CanControl => !config.hostAuthoritative || IsServer;
        
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
            // Подписка на сетевые изменения
            _networkState.OnValueChanged += OnNetworkStateChanged;
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
            SetState(config.initialState);
            
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
            
            if (IsServer && IsNetworkActive)
            {
                StartSessionServerRpc();
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
            
            if (IsServer && IsNetworkActive)
            {
                RestartSessionServerRpc();
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
            
            if (IsServer && IsNetworkActive)
            {
                PauseSessionServerRpc();
            }
            else
            {
                SetState(SessionState.Paused);
                EventBus.Publish(EventBus.Session.Paused, null);
                Log("Session paused");
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
            
            if (IsServer && IsNetworkActive)
            {
                ResumeSessionServerRpc();
            }
            else
            {
                SetState(SessionState.Playing);
                EventBus.Publish(EventBus.Session.Resumed, null);
                Log("Session resumed");
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
            
            if (IsServer && IsNetworkActive)
            {
                EndSessionServerRpc((int)reason, isVictory);
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
            if (IsServer && IsNetworkActive)
            {
                ReturnToMenuServerRpc();
            }
            else
            {
                ReturnToMenuInternal();
            }
        }
        
        #endregion
        
        #region IResettable
        
        public void ResetState()
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
        
        #region Internal Methods
        
        private void StartSessionInternal()
        {
            SetState(SessionState.Starting);
            
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
            SetState(SessionState.Playing);
            _stats.StartTimer();
            
            EventBus.Publish(EventBus.Session.Started, null);
            Log("Session started");
        }
        
        private void RestartSessionInternal()
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
        
        private void EndSessionInternal(SessionEndReason reason, bool isVictory)
        {
            _stats.UpdateTime();
            _endReason = reason;
            _isVictory = isVictory;
            
            var finalState = isVictory ? SessionState.Victory : SessionState.GameOver;
            SetState(finalState);
            
            if (IsServer && IsNetworkActive)
            {
                _networkEndReason.Value = (int)reason;
                _networkIsVictory.Value = isVictory;
            }
            
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
        
        private void ReturnToMenuInternal()
        {
            _endReason = SessionEndReason.ReturnToMenu;
            
            // Сброс
            ResetAllSystems();
            _stats.FullReset();
            
            SetState(SessionState.Ready);
            
            EventBus.Publish(EventBus.Session.ReturnedToMenu, null);
            Log("Returned to menu");
        }
        
        private void SetState(SessionState newState)
        {
            var prevState = State;
            if (prevState == newState) return;
            
            if (IsServer && IsNetworkActive)
            {
                _networkState.Value = (int)newState;
            }
            else
            {
                _localState = newState;
            }
            
            var data = new SessionStateChangedData
            {
                PreviousState = prevState,
                NewState = newState
            };
            
            EventBus.Publish(EventBus.Session.StateChanged, data);
            OnStateChanged?.Invoke(prevState, newState);
            
            if (config.verboseLogging)
            {
                Log($"State: {prevState} → {newState}");
            }
        }
        
        private void ResetAllSystems()
        {
            EventBus.Publish(EventBus.Session.Reset, null);
            
            // Автоматический вызов ResetState для всех IResettable
            var manager = SystemInitializationManager.Instance;
            if (manager != null)
            {
                manager.ResetAllResettableSystems();
            }
        }
        
        private void OnNetworkStateChanged(int prev, int current)
        {
            if (!IsServer)
            {
                var prevState = (SessionState)prev;
                var newState = (SessionState)current;
                
                var data = new SessionStateChangedData
                {
                    PreviousState = prevState,
                    NewState = newState
                };
                
                EventBus.Publish(EventBus.Session.StateChanged, data);
                OnStateChanged?.Invoke(prevState, newState);
                
                if (config.verboseLogging)
                {
                    Log($"[Client] State: {prevState} → {newState}");
                }
            }
        }
        
        #endregion
        
        #region Network RPCs
        
        [ServerRpc(RequireOwnership = false)]
        private void StartSessionServerRpc()
        {
            StartSessionInternal();
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void RestartSessionServerRpc()
        {
            RestartSessionInternal();
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void PauseSessionServerRpc()
        {
            if (State == SessionState.Playing)
            {
                SetState(SessionState.Paused);
                PauseSessionClientRpc();
            }
        }
        
        [ClientRpc]
        private void PauseSessionClientRpc()
        {
            EventBus.Publish(EventBus.Session.Paused, null);
            Log("Session paused");
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void ResumeSessionServerRpc()
        {
            if (State == SessionState.Paused)
            {
                SetState(SessionState.Playing);
                ResumeSessionClientRpc();
            }
        }
        
        [ClientRpc]
        private void ResumeSessionClientRpc()
        {
            EventBus.Publish(EventBus.Session.Resumed, null);
            Log("Session resumed");
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void EndSessionServerRpc(int reason, bool isVictory)
        {
            EndSessionInternal((SessionEndReason)reason, isVictory);
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void ReturnToMenuServerRpc()
        {
            ReturnToMenuInternal();
            ReturnToMenuClientRpc();
        }
        
        [ClientRpc]
        private void ReturnToMenuClientRpc()
        {
            if (!IsServer)
            {
                _stats.FullReset();
                EventBus.Publish(EventBus.Session.ReturnedToMenu, null);
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
        
        private void OnDestroy()
        {
            _networkState.OnValueChanged -= OnNetworkStateChanged;
        }
        
        #endregion
        
        #region Debug
        
        private void Log(string message)
        {
            if (config != null && config.logEvents)
            {
                Debug.Log($"[GameSession] {message}");
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
        private void DebugPrintStats() => Debug.Log(_stats.ToString());
        
        #endregion
    }
}
