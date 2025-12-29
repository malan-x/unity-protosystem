// Packages/com.protosystem.core/Runtime/NetworkLobby/NetworkLobbyEvents.cs
namespace ProtoSystem
{
    /// <summary>
    /// События системы сетевого лобби
    /// </summary>
    public static partial class EventBus
    {
        public static partial class Lobby
        {
            // Подключение (10500-10509)
            /// <summary>Хост создан. Data: LobbyEventData</summary>
            public const int HostStarted = 10500;
            /// <summary>Подключение к хосту. Data: LobbyEventData</summary>
            public const int ClientConnecting = 10501;
            /// <summary>Подключен к хосту. Data: LobbyEventData</summary>
            public const int ClientConnected = 10502;
            /// <summary>Отключен от хоста. Data: LobbyEventData</summary>
            public const int ClientDisconnected = 10503;
            /// <summary>Ошибка подключения. Data: LobbyEventData</summary>
            public const int ConnectionFailed = 10504;

            // Игроки (10510-10519)
            /// <summary>Игрок присоединился. Data: PlayerEventData</summary>
            public const int PlayerJoined = 10510;
            /// <summary>Игрок вышел. Data: PlayerEventData</summary>
            public const int PlayerLeft = 10511;
            /// <summary>Игрок готов. Data: PlayerEventData</summary>
            public const int PlayerReady = 10512;
            /// <summary>Игрок не готов. Data: PlayerEventData</summary>
            public const int PlayerNotReady = 10513;
            /// <summary>Данные игрока обновлены. Data: PlayerEventData</summary>
            public const int PlayerDataChanged = 10514;

            // Лобби (10520-10529)
            /// <summary>Лобби создано. Data: LobbyEventData</summary>
            public const int LobbyCreated = 10520;
            /// <summary>Лобби закрыто. Data: LobbyEventData</summary>
            public const int LobbyClosed = 10521;
            /// <summary>Все игроки готовы. Data: null</summary>
            public const int AllPlayersReady = 10522;
            /// <summary>Игра начинается. Data: null</summary>
            public const int GameStarting = 10523;
            /// <summary>Игра началась. Data: null</summary>
            public const int GameStarted = 10524;
        }
    }
}

namespace ProtoSystem.NetworkLobby
{
    /// <summary>
    /// Данные события лобби
    /// </summary>
    public struct LobbyEventData
    {
        public string LobbyId;
        public string HostAddress;
        public ushort Port;
        public string ErrorMessage;
        public int MaxPlayers;
        public int CurrentPlayers;
    }

    /// <summary>
    /// Данные события игрока
    /// </summary>
    public struct PlayerEventData
    {
        public ulong ClientId;
        public string PlayerName;
        public bool IsReady;
        public bool IsHost;
        public int PlayerIndex;
    }
}
