// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsWidgetConfig.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Конфигурация Community Panel с сервера (GET /config).
    /// Определяет: какие виджеты включены и при каких условиях показывать.
    /// </summary>
    [Serializable]
    public class LiveOpsPanelConfig
    {
        public LiveOpsWidgetDef cards    = new();
        public LiveOpsWidgetDef messages = new();
        public LiveOpsWidgetDef goal     = new();
        public LiveOpsWidgetDef rating   = new();

        public LiveOpsRatingMeta ratingMeta;
    }

    /// <summary>Определение одного виджета: включён ли и условия показа.</summary>
    [Serializable]
    public class LiveOpsWidgetDef
    {
        public bool enabled = true;

        /// <summary>null — показывать сразу без условий.</summary>
        public LiveOpsShowAfterCondition show_after;

        public bool IsVisible(LiveOpsPlayerContext ctx)
        {
            if (!enabled) return false;
            if (show_after == null) return true;
            return show_after.Evaluate(ctx);
        }
    }

    /// <summary>
    /// Условия для показа виджета. Все поля опциональны (0/null = не проверять).
    /// Проверка выполняется на клиенте — сервер только отдаёт пороги.
    /// </summary>
    [Serializable]
    public class LiveOpsShowAfterCondition
    {
        /// <summary>"AND" — все условия, "OR" — любое одно.</summary>
        public string @operator = "AND";

        /// <summary>Минимум запусков игры.</summary>
        public int launches;

        /// <summary>Минимум минут в игре.</summary>
        public int playtime_minutes;

        /// <summary>
        /// Произвольные условия по PlayerPrefs (строковое сравнение).
        /// Пример: { "key": "tutorial_complete", "value": "1" }
        /// </summary>
        public List<LiveOpsPlayerPrefsCondition> player_prefs = new();

        public bool Evaluate(LiveOpsPlayerContext ctx)
        {
            var results = new List<bool>();

            if (launches > 0)
                results.Add(ctx.launches >= launches);

            if (playtime_minutes > 0)
                results.Add(ctx.playtimeMinutes >= playtime_minutes);

            foreach (var pp in player_prefs)
                results.Add(PlayerPrefs.GetString(pp.key, "") == pp.value);

            if (results.Count == 0) return true;
            return @operator == "OR"
                ? results.Any(b => b)
                : results.All(b => b);
        }
    }

    /// <summary>Условие на значение ключа в PlayerPrefs.</summary>
    [Serializable]
    public class LiveOpsPlayerPrefsCondition
    {
        public string key;
        public string value;
    }

    /// <summary>
    /// Контекст игрока для проверки условий show_after.
    /// Заполняется в проекте и передаётся через LiveOpsSystem.SetPlayerContext().
    /// </summary>
    [Serializable]
    public class LiveOpsPlayerContext
    {
        public int launches;
        public int playtimeMinutes;

        public LiveOpsPlayerContext(int launches, int playtimeMinutes)
        {
            this.launches        = launches;
            this.playtimeMinutes = playtimeMinutes;
        }
    }
}
