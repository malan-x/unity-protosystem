// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsRating.cs
using System;

namespace ProtoSystem.LiveOps
{
    /// <summary>Мета-данные рейтинга из /config (версия билда).</summary>
    [Serializable]
    public class LiveOpsRatingMeta
    {
        public string version;
    }

    /// <summary>Текущие данные рейтинга (GET /ratings?version=...).</summary>
    [Serializable]
    public class LiveOpsRatingData
    {
        public string version;
        public float avg;
        public int count;
        /// <summary>Оценка текущего игрока. 0 — не оценивал.</summary>
        public int userVote;
    }

    /// <summary>Запрос на отправку оценки (POST /ratings).</summary>
    [Serializable]
    public class LiveOpsRatingSubmit
    {
        public string version;
        /// <summary>Оценка от 1 до 10.</summary>
        public int score;
        public string playerId;
    }

    /// <summary>Ответ сервера после отправки оценки.</summary>
    [Serializable]
    public class LiveOpsRatingResult
    {
        public bool ok;
        public float avg;
        public int count;
    }
}
