// Packages/com.protosystem.core/Runtime/NetworkLobby/NetworkLobbySystem.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;

namespace ProtoSystem.NetworkLobby
{
    /// <summary>
    /// Система управления сетевым лобби.
    /// Интегрируется с UISystem для отображения UI лобби.
    /// </summary>
    [ProtoSystemComponent("Network Lobby", "Сетевое лобби для мультиплеера (Netcode)", "Network", "🌐", 30)]
    public class NetworkLobbySystem : InitializableSystemBase
    {
        public override string SystemId => "NetworkLobbySystem";
        public override string DisplayName => "Network Lobby System";
        public override string Description => "Управляет сетевым лобби: создание, подключение игроков, готовность и старт игры.";

        [Header("Configuration")]
        [SerializeField, InlineConfig] private NetworkLobbyConfig config;

        [Header("Network Settings")]
        [SerializeField] private string defaultAddress = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7777;
        [SerializeField] private int maxPlayers = 4;

        // Компонент для RPC
        private NetworkLobbyRPC _rpc;

        // Состояние
        private bool _isInLobby;
        private bool _isHost;
        private string _currentLobbyId;
        private readonly Dictionary<ulong, LobbyPlayer> _players = new();

        #region Singleton

        private static NetworkLobbySystem _instance;
        public static NetworkLobbySystem Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<NetworkLobbySystem>();
                return _instance;
            }
        }

        #endregion

        #region Properties

        public bool IsInLobby => _isInLobby;
        public bool IsHost => _isHost;
        public string LobbyId => _currentLobbyId;
        public IReadOnlyDictionary<ulong, LobbyPlayer> Players => _players;
        public int PlayerCount => _players.Count;

        public bool AllPlayersReady
        {
            get
            {
                if (_players.Count == 0) return false;
                foreach (var player in _players.Values)
                    if (!player.IsReady) return false;
                return true;
            }
        }

        public bool CanStartGame => _isHost && AllPlayersReady && _players.Count >= (config?.minPlayersToStart ?? 1);

        #endregion

        #region Initialization

        protected override void Awake()
        {
            base.Awake();
            _instance = this;
        }

        protected override void InitEvents()
        {
            AddEvent(EventBus.Lobby.PlayerReady, _ => CheckAllReady());
        }

        public override Task<bool> InitializeAsync()
        {
            LogMessage("Initializing Network Lobby System...");

            if (config == null)
                config = NetworkLobbyConfig.CreateDefault();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }

            LogMessage("Network Lobby System initialized");
            return Task.FromResult(true);
        }

        #endregion

        #region Host/Join

        public bool CreateLobby(string lobbyName = null)
        {
            if (_isInLobby)
            {
                LogWarning("Already in a lobby");
                return false;
            }

            if (NetworkManager.Singleton == null)
            {
                LogError("NetworkManager not found");
                return false;
            }

            try
            {
                bool success = NetworkManager.Singleton.StartHost();
                
                if (success)
                {
                    _isInLobby = true;
                    _isHost = true;
                    _currentLobbyId = lobbyName ?? Guid.NewGuid().ToString().Substring(0, 8);

                    // Создаём RPC компонент
                    SpawnRPCComponent();

                    var hostPlayer = new LobbyPlayer
                    {
                        ClientId = NetworkManager.Singleton.LocalClientId,
                        PlayerName = config?.defaultPlayerName ?? "Host",
                        IsHost = true,
                        IsReady = false,
                        PlayerIndex = 0
                    };
                    _players[hostPlayer.ClientId] = hostPlayer;

                    EventBus.Publish(EventBus.Lobby.HostStarted, new LobbyEventData
                    {
                        LobbyId = _currentLobbyId,
                        HostAddress = defaultAddress,
                        Port = defaultPort,
                        MaxPlayers = maxPlayers,
                        CurrentPlayers = 1
                    });

                    EventBus.Publish(EventBus.Lobby.LobbyCreated, new LobbyEventData
                    {
                        LobbyId = _currentLobbyId,
                        MaxPlayers = maxPlayers
                    });

                    LogMessage($"Lobby created: {_currentLobbyId}");
                    return true;
                }
                
                LogError("Failed to start host");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Failed to create lobby: {ex.Message}");
                return false;
            }
        }

        public bool JoinLobby(string address = null, ushort port = 0)
        {
            if (_isInLobby)
            {
                LogWarning("Already in a lobby");
                return false;
            }

            if (NetworkManager.Singleton == null)
            {
                LogError("NetworkManager not found");
                return false;
            }

            try
            {
                var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                if (transport != null)
                {
                    transport.ConnectionData.Address = address ?? defaultAddress;
                    transport.ConnectionData.Port = port > 0 ? port : defaultPort;
                }

                EventBus.Publish(EventBus.Lobby.ClientConnecting, new LobbyEventData
                {
                    HostAddress = address ?? defaultAddress,
                    Port = port > 0 ? port : defaultPort
                });

                bool success = NetworkManager.Singleton.StartClient();

                if (success)
                {
                    _isInLobby = true;
                    _isHost = false;
                    LogMessage($"Connecting to {address ?? defaultAddress}:{(port > 0 ? port : defaultPort)}");
                    return true;
                }
                
                LogError("Failed to start client");
                EventBus.Publish(EventBus.Lobby.ConnectionFailed, new LobbyEventData
                {
                    ErrorMessage = "Failed to start client"
                });
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Failed to join lobby: {ex.Message}");
                EventBus.Publish(EventBus.Lobby.ConnectionFailed, new LobbyEventData
                {
                    ErrorMessage = ex.Message
                });
                return false;
            }
        }

        public void LeaveLobby()
        {
            if (!_isInLobby) return;

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.Shutdown();

            _isInLobby = false;
            _isHost = false;
            _currentLobbyId = null;
            _players.Clear();
            _rpc = null;

            EventBus.Publish(EventBus.Lobby.LobbyClosed, new LobbyEventData());
            LogMessage("Left lobby");
        }

        #endregion

        #region Player Management

        public void SetReady(bool ready)
        {
            if (!_isInLobby || _rpc == null) return;

            var clientId = NetworkManager.Singleton.LocalClientId;
            
            if (_players.TryGetValue(clientId, out var player))
            {
                player.IsReady = ready;
                _players[clientId] = player;

                _rpc.SetReadyServerRpc(ready);

                EventBus.Publish(ready ? EventBus.Lobby.PlayerReady : EventBus.Lobby.PlayerNotReady,
                    new PlayerEventData
                    {
                        ClientId = clientId,
                        PlayerName = player.PlayerName,
                        IsReady = ready
                    });

                CheckAllReady();
            }
        }

        public void SetPlayerName(string name)
        {
            if (!_isInLobby || _rpc == null) return;

            var clientId = NetworkManager.Singleton.LocalClientId;
            
            if (_players.TryGetValue(clientId, out var player))
            {
                player.PlayerName = name;
                _players[clientId] = player;

                _rpc.SetPlayerNameServerRpc(name);

                EventBus.Publish(EventBus.Lobby.PlayerDataChanged, new PlayerEventData
                {
                    ClientId = clientId,
                    PlayerName = name
                });
            }
        }

        public void StartGame()
        {
            if (!_isHost)
            {
                LogWarning("Only host can start the game");
                return;
            }

            if (!CanStartGame)
            {
                LogWarning("Cannot start game: not all players ready or not enough players");
                return;
            }

            EventBus.Publish(EventBus.Lobby.GameStarting, null);

            if (!string.IsNullOrEmpty(config?.gameSceneName))
            {
                NetworkManager.Singleton.SceneManager.LoadScene(config.gameSceneName, 
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
            }

            EventBus.Publish(EventBus.Lobby.GameStarted, null);
            LogMessage("Game started");
        }

        public void KickPlayer(ulong clientId)
        {
            if (!_isHost)
            {
                LogWarning("Only host can kick players");
                return;
            }

            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                LogWarning("Cannot kick yourself");
                return;
            }

            NetworkManager.Singleton.DisconnectClient(clientId);
            LogMessage($"Kicked player {clientId}");
        }

        #endregion

        #region Internal

        private void SpawnRPCComponent()
        {
            var rpcObj = new GameObject("LobbyRPC");
            rpcObj.transform.SetParent(transform);
            _rpc = rpcObj.AddComponent<NetworkLobbyRPC>();
            _rpc.Initialize(this);
            
            var networkObject = rpcObj.AddComponent<NetworkObject>();
            networkObject.Spawn();
        }

        internal void OnPlayerDataUpdated(ulong clientId, string name, bool ready)
        {
            if (_players.TryGetValue(clientId, out var player))
            {
                player.PlayerName = name;
                player.IsReady = ready;
                _players[clientId] = player;

                EventBus.Publish(EventBus.Lobby.PlayerDataChanged, new PlayerEventData
                {
                    ClientId = clientId,
                    PlayerName = name,
                    IsReady = ready
                });
            }
        }

        internal void AddPlayer(ulong clientId, LobbyPlayer player)
        {
            _players[clientId] = player;
        }

        internal void RemovePlayer(ulong clientId)
        {
            _players.Remove(clientId);
        }

        private void OnClientConnected(ulong clientId)
        {
            if (_isHost)
            {
                var newPlayer = new LobbyPlayer
                {
                    ClientId = clientId,
                    PlayerName = $"Player {_players.Count + 1}",
                    IsHost = false,
                    IsReady = false,
                    PlayerIndex = _players.Count
                };
                _players[clientId] = newPlayer;

                EventBus.Publish(EventBus.Lobby.PlayerJoined, new PlayerEventData
                {
                    ClientId = clientId,
                    PlayerName = newPlayer.PlayerName,
                    IsHost = false,
                    PlayerIndex = newPlayer.PlayerIndex
                });

                LogMessage($"Player {clientId} joined");
            }
            else if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                EventBus.Publish(EventBus.Lobby.ClientConnected, new LobbyEventData
                {
                    HostAddress = defaultAddress,
                    Port = defaultPort
                });

                LogMessage("Connected to host");
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (_players.TryGetValue(clientId, out var player))
            {
                _players.Remove(clientId);

                EventBus.Publish(EventBus.Lobby.PlayerLeft, new PlayerEventData
                {
                    ClientId = clientId,
                    PlayerName = player.PlayerName
                });

                LogMessage($"Player {clientId} left");
            }

            if (clientId == NetworkManager.Singleton?.LocalClientId)
            {
                _isInLobby = false;
                _isHost = false;
                _currentLobbyId = null;
                _players.Clear();

                EventBus.Publish(EventBus.Lobby.ClientDisconnected, new LobbyEventData());
            }
        }

        private void CheckAllReady()
        {
            if (_isHost && AllPlayersReady)
            {
                EventBus.Publish(EventBus.Lobby.AllPlayersReady, null);
            }
        }

        #endregion

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            if (_instance == this)
                _instance = null;
        }
    }

    /// <summary>
    /// Данные игрока в лобби
    /// </summary>
    [Serializable]
    public struct LobbyPlayer
    {
        public ulong ClientId;
        public string PlayerName;
        public bool IsHost;
        public bool IsReady;
        public int PlayerIndex;
    }
}
