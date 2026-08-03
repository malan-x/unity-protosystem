// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsTelemetryBatch.cs
using System;
using System.Collections.Generic;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Пачка событий телеметрии + контекст игрока.
    ///
    /// Отдельного heartbeat нет: сервер считает игрока онлайн, пока приходят
    /// события. Если событий нет дольше <c>telemetryTickSeconds</c>, система
    /// добавляет служебное событие <c>tick</c> — сервер учитывает его как
    /// признак жизни, но не считает игровым событием.
    /// </summary>
    [Serializable]
    public class LiveOpsTelemetryBatch
    {
        /// <summary>Идентификатор игрока (для Steam-сборок — SteamID64).</summary>
        public string playerId;

        /// <summary>Отображаемое имя (Steam persona).</summary>
        public string playerName;

        /// <summary>Версия игры.</summary>
        public string version;

        /// <summary>Язык интерфейса игрока.</summary>
        public string lang;

        /// <summary>Смещение часового пояса игрока от UTC в минутах (МСК = +180).</summary>
        public int tzOffsetMinutes;

        /// <summary>Накопленные события. Может быть пустым — тогда это просто признак жизни.</summary>
        public List<LiveOpsEvent> events = new();
    }
}
