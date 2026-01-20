// Packages/com.protosystem.core/Runtime/GameSession/IGameSessionNetworkSync.cs
namespace ProtoSystem
{
    /// <summary>
    /// Интерфейс для сетевой синхронизации GameSessionSystem.
    /// Позволяет GameSessionSystem работать без прямой зависимости от Netcode.
    /// </summary>
    public interface IGameSessionNetworkSync
    {
        /// <summary>Активна ли сеть</summary>
        bool IsNetworkActive { get; }
        
        /// <summary>Является ли текущий клиент сервером/хостом</summary>
        bool IsServer { get; }
        
        /// <summary>Запросить старт сессии (через RPC)</summary>
        void RequestStartSession();
        
        /// <summary>Запросить рестарт сессии (через RPC)</summary>
        void RequestRestartSession();
        
        /// <summary>Запросить паузу (через RPC)</summary>
        void RequestPauseSession();
        
        /// <summary>Запросить возобновление (через RPC)</summary>
        void RequestResumeSession();
        
        /// <summary>Запросить завершение сессии (через RPC)</summary>
        void RequestEndSession(SessionEndReason reason, bool isVictory);
        
        /// <summary>Запросить возврат в меню (через RPC)</summary>
        void RequestReturnToMenu();
    }
}
