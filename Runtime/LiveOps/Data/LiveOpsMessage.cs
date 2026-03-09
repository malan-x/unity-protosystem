// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsMessage.cs
using System;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Сообщение от разработчика игрокам (MOTD, новости, объявления).
    /// </summary>
    [Serializable]
    public class LiveOpsMessage
    {
        /// <summary>Уникальный идентификатор сообщения.</summary>
        public string id;

        /// <summary>Заголовок — локализован.</summary>
        public LocalizedString title = new();

        /// <summary>Текст сообщения — локализован.</summary>
        public LocalizedString body = new();

        /// <summary>Тип сообщения (info / warning / event).</summary>
        public string type;

        /// <summary>Дата публикации в UTC (ISO 8601).</summary>
        public string publishedAt;

        /// <summary>Дата истечения в UTC (ISO 8601). Null — бессрочно.</summary>
        public string expiresAt;
    }
}
