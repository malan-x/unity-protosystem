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

        /// <summary>Закреплённый элемент (первый с pinned) или null.</summary>
        public LiveOpsContentOrderEntry Pinned =>
            order == null ? null : Array.Find(order, e => e != null && e.pinned);
    }

    /// <summary>Элемент порядка: тип контента + id записи.</summary>
    [Serializable]
    public class LiveOpsContentOrderEntry
    {
        /// <summary>"goal", "announcement", "poll", "devlog"</summary>
        public string type;
        public string id;

        /// <summary>
        /// Закреплён под панелью: стоит на месте цели (полоса под свёрнутой строкой,
        /// не сворачивается) вместо неё и в карусель не попадает. Закреплён максимум
        /// один элемент — так пишет дашборд. Клиенты без поддержки поле не читают,
        /// у них элемент остаётся обычной карточкой карусели.
        /// </summary>
        public bool pinned;
    }
}
