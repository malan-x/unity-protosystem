// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsEvent.cs
using System;
using System.Collections.Generic;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Аналитическое событие для отправки на сервер.
    /// </summary>
    [Serializable]
    public class LiveOpsEvent
    {
        /// <summary>Название события (например: "session_start", "level_complete").</summary>
        public string name;

        /// <summary>Анонимный идентификатор игрока.</summary>
        public string playerId;

        /// <summary>Версия игры.</summary>
        public string gameVersion;

        /// <summary>Время события в UTC (ISO 8601).</summary>
        public string timestamp;

        /// <summary>Произвольные параметры события.</summary>
        public Dictionary<string, string> data;

        public LiveOpsEvent(string name, string playerId, string gameVersion, Dictionary<string, string> data = null)
        {
            this.name = name;
            this.playerId = playerId;
            this.gameVersion = gameVersion;
            this.timestamp = DateTime.UtcNow.ToString("o");
            this.data = data ?? new Dictionary<string, string>();
        }
    }
}
