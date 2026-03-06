// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsFeedback.cs
using System;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Фидбек от игрока.
    /// </summary>
    [Serializable]
    public class LiveOpsFeedback
    {
        /// <summary>Анонимный идентификатор игрока.</summary>
        public string playerId;

        /// <summary>Версия игры.</summary>
        public string gameVersion;

        /// <summary>Текст фидбека.</summary>
        public string message;

        /// <summary>Категория (bug / suggestion / other).</summary>
        public string category;

        /// <summary>Время отправки в UTC (ISO 8601).</summary>
        public string timestamp;

        public LiveOpsFeedback(string playerId, string gameVersion, string message, string category = "other")
        {
            this.playerId = playerId;
            this.gameVersion = gameVersion;
            this.message = message;
            this.category = category;
            this.timestamp = DateTime.UtcNow.ToString("o");
        }
    }
}
