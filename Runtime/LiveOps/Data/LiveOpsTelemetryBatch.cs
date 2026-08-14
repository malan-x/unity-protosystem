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

        /// <summary>
        /// Среда запуска: "editor" или "player". Сервер держит их раздельно,
        /// чтобы прогоны разработчика в Unity Editor не перекашивали DAU
        /// и часы игры. Пустое значение трактуется как билд.
        /// </summary>
        public string env;

        /// <summary>
        /// Тип сборки: normal / demo / playtest (из <c>BuildInfo.Flavor</c>).
        /// Сервер держит их раздельно — у демо и плейтеста своя аудитория,
        /// смешивать их метрики с релизом смысла нет. Пустое значение
        /// трактуется как релизная сборка.
        /// </summary>
        public string build;

        /// <summary>
        /// Тег устройства: windows / linux / mac / steamdeck. По умолчанию
        /// выводится из платформы, но Steam Deck под Proton выглядит как
        /// Windows — его сообщает проект через <c>SetDeviceTag</c>.
        /// </summary>
        public string device;

        /// <summary>
        /// Конфигурация машины. Заполняется только в первом батче сессии:
        /// данные статичные, а на сервере это единственная запись на диск
        /// в обработчике приёма.
        /// </summary>
        public LiveOpsDeviceSpecs specs;

        /// <summary>Накопленные события. Может быть пустым — тогда это просто признак жизни.</summary>
        public List<LiveOpsEvent> events = new();
    }
}
