// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsConversationItem.cs
using System;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Элемент переписки игрока с разработчиком.
    /// Загружается через hook GET /api/messages/my.
    /// </summary>
    [Serializable]
    public class LiveOpsConversationItem
    {
        /// <summary>id записи в PocketBase.</summary>
        public string id;

        /// <summary>Текст сообщения игрока.</summary>
        public string message;

        /// <summary>Ответ разработчика. Пусто — ещё не отвечено.</summary>
        public string reply;

        /// <summary>Статус ответа: "" (нет), "sent" (написан), "delivered" (подтверждён).</summary>
        public string reply_status;

        /// <summary>Контекст (какая карточка была открыта при отправке).</summary>
        public string category;

        /// <summary>Время отправки (ISO 8601 UTC).</summary>
        public string timestamp;

        /// <summary>Локализованный ответ: {"ru":"...","en":"..."}. Заполняется из reply_localized JSON.</summary>
        [NonSerialized] public LocalizedString replyLocalized;
    }
}
