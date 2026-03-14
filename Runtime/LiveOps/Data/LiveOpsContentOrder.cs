// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsContentOrder.cs
using System;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Порядок отображения контента в карусели Community Panel.
    /// Загружается из коллекции content_order в PocketBase.
    /// </summary>
    [Serializable]
    public class LiveOpsContentOrder
    {
        public LiveOpsContentOrderEntry[] order = Array.Empty<LiveOpsContentOrderEntry>();
    }

    /// <summary>Элемент порядка: тип контента + id записи.</summary>
    [Serializable]
    public class LiveOpsContentOrderEntry
    {
        /// <summary>"goal", "announcement", "poll", "devlog"</summary>
        public string type;
        public string id;
    }
}
