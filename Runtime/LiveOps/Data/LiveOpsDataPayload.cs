// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsDataPayload.cs
namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Payload для EventBus события Evt.LiveOps.DataUpdated.
    /// Подписчик проверяет Type и кастит Data к нужному типу.
    /// </summary>
    public class LiveOpsDataPayload
    {
        /// <summary>
        /// Тип данных. Используй константы из LiveOpsDataType:
        /// "panel_config", "polls", "announcements", "devlog", "rating", "messages".
        /// </summary>
        public string Type;

        /// <summary>Данные. Кастировать согласно Type.</summary>
        public object Data;

        public LiveOpsDataPayload(string type, object data)
        {
            Type = type;
            Data = data;
        }
    }

    /// <summary>Константы типов для LiveOpsDataPayload.Type.</summary>
    public static class LiveOpsDataType
    {
        public const string PanelConfig   = "panel_config";
        public const string Polls         = "polls";
        public const string Announcements = "announcements";
        public const string DevLog        = "devlog";
        public const string Rating        = "rating";
        public const string Messages      = "messages";
    }
}
