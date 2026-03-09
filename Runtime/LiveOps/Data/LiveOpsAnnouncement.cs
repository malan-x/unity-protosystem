// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsAnnouncement.cs
using System;

namespace ProtoSystem.LiveOps
{
    /// <summary>Новость/объявление. Карточка типа "announcement" в панели.</summary>
    [Serializable]
    public class LiveOpsAnnouncement
    {
        public string id;
        public LocalizedString title = new();
        public LocalizedString body  = new();
        /// <summary>Ссылка на полный пост (Steam, сайт). Null — не показывать кнопку.</summary>
        public string url;
        public string publishedAt;
    }
}
