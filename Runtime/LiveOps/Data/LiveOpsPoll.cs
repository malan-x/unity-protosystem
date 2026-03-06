// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsPoll.cs
using System;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Опрос с вариантами ответов.
    /// </summary>
    [Serializable]
    public class LiveOpsPoll
    {
        /// <summary>Уникальный идентификатор опроса.</summary>
        public string id;

        /// <summary>Вопрос опроса.</summary>
        public string question;

        /// <summary>Варианты ответов.</summary>
        public string[] options;

        /// <summary>Дата истечения в UTC (ISO 8601). Null — бессрочно.</summary>
        public string expiresAt;
    }

    /// <summary>
    /// Ответ игрока на опрос.
    /// </summary>
    [Serializable]
    public class LiveOpsPollAnswer
    {
        /// <summary>Идентификатор опроса.</summary>
        public string pollId;

        /// <summary>Индекс выбранного варианта.</summary>
        public int optionIndex;

        /// <summary>Анонимный идентификатор игрока.</summary>
        public string playerId;
    }
}
