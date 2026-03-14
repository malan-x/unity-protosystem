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

        /// <summary>Язык клиента.</summary>
        public string lang;

        /// <summary>Текст фидбека.</summary>
        public string message;

        /// <summary>Категория — контекст (какая новость/опрос открыт).</summary>
        public string category;

        /// <summary>
        /// Тег — пометка (bug / idea / thanks / other).
        /// </summary>
        public string tag;

        /// <summary>Время отправки в UTC (ISO 8601).</summary>
        public string timestamp;

        public LiveOpsFeedback(string playerId, string gameVersion, string message,
                               string lang = "en", string category = "other", string tag = "general")
        {
            this.playerId    = playerId;
            this.gameVersion = gameVersion;
            this.message     = message;
            this.lang        = lang;
            this.category    = category;
            this.tag         = tag;
            this.timestamp   = DateTime.UtcNow.ToString("o");
        }
    }
}
