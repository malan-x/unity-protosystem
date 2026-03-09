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
        public string id;

        /// <summary>Вопрос — локализован.</summary>
        public LocalizedString question = new();

        /// <summary>"single" — один ответ, "multi" — несколько.</summary>
        public string pollType = "single";

        /// <summary>Варианты ответов с количеством голосов.</summary>
        public LiveOpsPollOption[] options = Array.Empty<LiveOpsPollOption>();

        /// <summary>Суммарное количество голосов.</summary>
        public int votesTotal;

        /// <summary>
        /// Выбранные игроком варианты (id). Null — не голосовал.
        /// Заполняется сервером при наличии X-Steam-ID.
        /// </summary>
        public string[] userVote;

        /// <summary>Дата истечения (ISO 8601 UTC). Null — бессрочно.</summary>
        public string expiresAt;
    }

    /// <summary>Вариант ответа на опрос.</summary>
    [Serializable]
    public class LiveOpsPollOption
    {
        public string id;

        /// <summary>Текст варианта — локализован.</summary>
        public LocalizedString label = new();

        /// <summary>Количество голосов за этот вариант.</summary>
        public int votes;

        /// <summary>Выбран ли вариант текущим игроком (клиентское состояние).</summary>
        [NonSerialized] public bool selected;

        public float Percent(int totalVotes) =>
            totalVotes > 0 ? (float)votes / totalVotes * 100f : 0f;
    }

    /// <summary>Ответ игрока на опрос (POST /polls/{id}/vote).</summary>
    [Serializable]
    public class LiveOpsPollAnswer
    {
        public string pollId;

        /// <summary>Один или несколько id вариантов (single и multi).</summary>
        public string[] optionIds;

        public string playerId;
    }
}
