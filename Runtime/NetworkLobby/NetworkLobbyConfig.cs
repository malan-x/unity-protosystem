// Packages/com.protosystem.core/Runtime/NetworkLobby/NetworkLobbyConfig.cs
using UnityEngine;

namespace ProtoSystem.NetworkLobby
{
    /// <summary>
    /// Конфигурация системы сетевого лобби
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkLobbyConfig", menuName = "ProtoSystem/NetworkLobby/Config")]
    public class NetworkLobbyConfig : ScriptableObject
    {
        [Header("Network Settings")]
        [Tooltip("Адрес по умолчанию")]
        public string defaultAddress = "127.0.0.1";
        
        [Tooltip("Порт по умолчанию")]
        public ushort defaultPort = 7777;
        
        [Tooltip("Максимум игроков")]
        public int maxPlayers = 4;
        
        [Tooltip("Минимум игроков для старта")]
        public int minPlayersToStart = 1;

        [Header("Player Settings")]
        [Tooltip("Имя игрока по умолчанию")]
        public string defaultPlayerName = "Player";
        
        [Tooltip("Автоматически помечать хоста как готового")]
        public bool autoReadyHost = false;

        [Header("Scene Settings")]
        [Tooltip("Сцена лобби")]
        public string lobbySceneName = "Lobby";
        
        [Tooltip("Игровая сцена")]
        public string gameSceneName = "Game";

        [Header("UI")]
        [Tooltip("ID окна лобби в UISystem")]
        public string lobbyWindowId = "LobbyWindow";
        
        [Tooltip("ID окна подключения")]
        public string joinWindowId = "JoinWindow";

        [Header("Timeouts")]
        [Tooltip("Таймаут подключения (сек)")]
        public float connectionTimeout = 10f;
        
        [Tooltip("Таймаут готовности (сек, 0 = без лимита)")]
        public float readyTimeout = 0f;

        public static NetworkLobbyConfig CreateDefault()
        {
            return CreateInstance<NetworkLobbyConfig>();
        }
    }
}
