// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsDevLog.cs
using System;

namespace ProtoSystem.LiveOps
{
    /// <summary>Dev Log — текущий фокус разработки с чеклистом задач.</summary>
    [Serializable]
    public class LiveOpsDevLog
    {
        public string id;
        public LocalizedString focus       = new();
        public LocalizedString title       = new();
        public LocalizedString description = new();
        public LiveOpsDevLogItem[] items   = Array.Empty<LiveOpsDevLogItem>();
        public string updatedAt;
    }

    [Serializable]
    public class LiveOpsDevLogItem
    {
        public LocalizedString name = new();
        /// <summary>Статус: "done", "wip", "todo"</summary>
        public string status = "todo";
        public bool IsDone => status == "done";
        public bool IsWip  => status == "wip";
    }
}
