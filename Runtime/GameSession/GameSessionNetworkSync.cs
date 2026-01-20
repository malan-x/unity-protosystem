// Packages/com.protosystem.core/Runtime/GameSession/GameSessionNetworkSync.cs
using UnityEngine;
using Unity.Netcode;

namespace ProtoSystem
{
    /// <summary>
    /// Сетевая синхронизация для GameSessionSystem.
    /// Добавьте этот компонент на тот же GameObject что и GameSessionSystem для мультиплеера.
    /// 
    /// Синхронизирует:
    /// - Состояние сессии (State)
    /// - Причину завершения (EndReason)
    /// - Флаг победы (IsVictory)
    /// 
    /// Все команды идут через ServerRpc, состояние реплицируется через NetworkVariable.
    /// </summary>
    [RequireComponent(typeof(GameSessionSystem))]
    [AddComponentMenu("ProtoSystem/Game Session Network Sync")]
    public class GameSessionNetworkSync : NetworkBehaviour, IGameSessionNetworkSync
    {
        #region References
        
        private GameSessionSystem _session;
        
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
        
        #region IGameSessionNetworkSync
        
        public bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        
        public new bool IsServer => !IsNetworkActive || NetworkManager.Singleton.IsServer;
        
        public void RequestStartSession()
        {
            if (IsServer)
            {
                ExecuteStartSession();
            }
            else
            {
                StartSessionServerRpc();
            }
        }
        
        public void RequestRestartSession()
        {
            if (IsServer)
            {
                ExecuteRestartSession();
            }
            else
            {
                RestartSessionServerRpc();
            }
        }
        
        public void RequestPauseSession()
        {
            if (IsServer)
            {
                ExecutePauseSession();
            }
            else
            {
                PauseSessionServerRpc();
            }
        }
        
        public void RequestResumeSession()
        {
            if (IsServer)
            {
                ExecuteResumeSession();
            }
            else
            {
                ResumeSessionServerRpc();
            }
        }
        
        public void RequestEndSession(SessionEndReason reason, bool isVictory)
        {
            if (IsServer)
            {
                ExecuteEndSession(reason, isVictory);
            }
            else
            {
                EndSessionServerRpc((int)reason, isVictory);
            }
        }
        
        public void RequestReturnToMenu()
        {
            if (IsServer)
            {
                ExecuteReturnToMenu();
            }
            else
            {
                ReturnToMenuServerRpc();
            }
        }
        
        #endregion
        
        #region Unity Callbacks
        
        private void Awake()
        {
            _session = GetComponent<GameSessionSystem>();
        }
        
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            // Регистрируемся в GameSessionSystem
            _session.RegisterNetworkSync(this);
            
            // Подписываемся на изменения NetworkVariable
            _networkState.OnValueChanged += OnNetworkStateChanged;
            _networkEndReason.OnValueChanged += OnNetworkEndReasonChanged;
            _networkIsVictory.OnValueChanged += OnNetworkIsVictoryChanged;
            
            // Если мы клиент - синхронизируем начальное состояние
            if (!IsServer)
            {
                _session.SetStateInternal((SessionState)_networkState.Value);
                _session.SetEndDataInternal(
                    (SessionEndReason)_networkEndReason.Value,
                    _networkIsVictory.Value);
            }
            
            Debug.Log("[GameSessionNetworkSync] Spawned, registered with GameSessionSystem");
        }
        
        public override void OnNetworkDespawn()
        {
            _networkState.OnValueChanged -= OnNetworkStateChanged;
            _networkEndReason.OnValueChanged -= OnNetworkEndReasonChanged;
            _networkIsVictory.OnValueChanged -= OnNetworkIsVictoryChanged;
            
            _session.UnregisterNetworkSync(this);
            
            base.OnNetworkDespawn();
        }
        
        #endregion
        
        #region Server Execution
        
        private void ExecuteStartSession()
        {
            _session.StartSessionInternal();
            SyncStateToNetwork();
        }
        
        private void ExecuteRestartSession()
        {
            _session.RestartSessionInternal();
            SyncStateToNetwork();
        }
        
        private void ExecutePauseSession()
        {
            if (_session.State == SessionState.Playing)
            {
                _session.PauseSessionInternal();
                SyncStateToNetwork();
            }
        }
        
        private void ExecuteResumeSession()
        {
            if (_session.State == SessionState.Paused)
            {
                _session.ResumeSessionInternal();
                SyncStateToNetwork();
            }
        }
        
        private void ExecuteEndSession(SessionEndReason reason, bool isVictory)
        {
            _session.EndSessionInternal(reason, isVictory);
            SyncStateToNetwork();
            SyncEndDataToNetwork(reason, isVictory);
        }
        
        private void ExecuteReturnToMenu()
        {
            _session.ReturnToMenuInternal();
            SyncStateToNetwork();
            SyncEndDataToNetwork(SessionEndReason.ReturnToMenu, false);
            
            // Уведомляем клиентов о возврате в меню
            ReturnToMenuClientRpc();
        }
        
        private void SyncStateToNetwork()
        {
            _networkState.Value = (int)_session.State;
        }
        
        private void SyncEndDataToNetwork(SessionEndReason reason, bool isVictory)
        {
            _networkEndReason.Value = (int)reason;
            _networkIsVictory.Value = isVictory;
        }
        
        #endregion
        
        #region Network Variable Callbacks
        
        private void OnNetworkStateChanged(int prev, int current)
        {
            // Клиенты применяют состояние от сервера
            if (!IsServer)
            {
                var prevState = (SessionState)prev;
                var newState = (SessionState)current;
                
                _session.SetStateInternal(newState);
                
                Debug.Log($"[GameSessionNetworkSync] Client state sync: {prevState} → {newState}");
            }
        }
        
        private void OnNetworkEndReasonChanged(int prev, int current)
        {
            if (!IsServer)
            {
                _session.SetEndDataInternal((SessionEndReason)current, _networkIsVictory.Value);
            }
        }
        
        private void OnNetworkIsVictoryChanged(bool prev, bool current)
        {
            if (!IsServer)
            {
                _session.SetEndDataInternal((SessionEndReason)_networkEndReason.Value, current);
            }
        }
        
        #endregion
        
        #region Server RPCs
        
        [ServerRpc(RequireOwnership = false)]
        private void StartSessionServerRpc()
        {
            ExecuteStartSession();
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void RestartSessionServerRpc()
        {
            ExecuteRestartSession();
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void PauseSessionServerRpc()
        {
            ExecutePauseSession();
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void ResumeSessionServerRpc()
        {
            ExecuteResumeSession();
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void EndSessionServerRpc(int reason, bool isVictory)
        {
            ExecuteEndSession((SessionEndReason)reason, isVictory);
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void ReturnToMenuServerRpc()
        {
            ExecuteReturnToMenu();
        }
        
        #endregion
        
        #region Client RPCs
        
        [ClientRpc]
        private void ReturnToMenuClientRpc()
        {
            if (!IsServer)
            {
                // Клиенты сбрасывают свою статистику
                _session.Stats.FullReset();
            }
        }
        
        #endregion
    }
}
