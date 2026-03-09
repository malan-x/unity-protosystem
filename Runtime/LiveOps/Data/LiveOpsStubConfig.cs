// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsStubConfig.cs
using System;
using UnityEngine;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Конфиг-заглушка для тестирования Community Panel без реального сервера.
    ///
    /// Использование:
    ///   1. Создать: Assets → Create → ProtoSystem → LiveOps Stub Config
    ///   2. Заполнить данными в Inspector
    ///   3. Назначить в поле Stub Config компонента CommunityPanelWindow
    ///
    /// При назначении StubConfig панель игнорирует LiveOpsSystem и EventBus,
    /// показывая данные напрямую из этого ассета.
    /// </summary>
    [CreateAssetMenu(fileName = "LiveOpsStub_New", menuName = "ProtoSystem/LiveOps Stub Config")]
    public class LiveOpsStubConfig : ScriptableObject
    {
        [Header("Language")]
        [Tooltip("Язык для LocalizedString.Get(). Обычно совпадает с LiveOpsConfig.defaultLanguage.")]
        public string language = "en";

        [Header("Panel Visibility")]
        public bool showCards    = true;
        public bool showMessages = true;
        public bool showGoal     = true;
        public bool showRating   = true;

        [Header("Cards (each entry is a poll, announcement, or devlog)")]
        public StubCardEntry[] cards = Array.Empty<StubCardEntry>();

        [Header("Goal (progress bar)")]
        public StubMilestoneData goal = new();

        [Header("Rating")]
        public StubRatingData rating = new();

        // ── Legacy compat (editor-only, kept for migration) ──────────
        [HideInInspector] public bool showWishlist;
        [HideInInspector] public bool hasPoll;
        [HideInInspector] public bool haAnnouncement;
        [HideInInspector] public bool hasDevLog;
        [HideInInspector] public StubPollData poll;
        [HideInInspector] public StubAnnouncementData announcement;
        [HideInInspector] public StubDevLogData devLog;
        [HideInInspector] public StubMilestoneData wishlist;
    }

    // ─── Card Entry ──────────────────────────────────────────────────────────

    public enum StubCardType { Poll, Announcement, DevLog }

    [Serializable]
    public class StubCardEntry
    {
        public StubCardType type = StubCardType.Poll;
        public StubPollData         poll         = new();
        public StubAnnouncementData announcement = new();
        public StubDevLogData       devLog       = new();
    }

    // ─── Вложенные данные ─────────────────────────────────────────────────────

    [Serializable]
    public class StubLocalizedString
    {
        [Tooltip("Простая строка без локализации — будет подставлена для любого языка.")]
        public string text;

        public LocalizedString ToLocalizedString() =>
            LocalizedString.FromRaw(text);
    }

    [Serializable]
    public class StubPollData
    {
        public string           id       = "stub_poll_1";
        public StubLocalizedString question = new() { text = "What feature should we focus on next?" };
        [Tooltip("single или multi")]
        public string           pollType = "single";
        public StubPollOption[] options  = new[]
        {
            new StubPollOption { id = "opt_1", label = "More content",    votes = 120 },
            new StubPollOption { id = "opt_2", label = "Better balance",  votes = 85  },
            new StubPollOption { id = "opt_3", label = "Bug fixes",       votes = 210 },
        };
        [Tooltip("id выбранного варианта (пусто = не голосовал)")]
        public string userVoteId = "";

        public LiveOpsPoll ToLiveOpsPoll()
        {
            int total = 0;
            var opts  = new LiveOpsPollOption[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                opts[i] = new LiveOpsPollOption
                {
                    id    = options[i].id,
                    label = LocalizedString.FromRaw(options[i].label),
                    votes = options[i].votes,
                };
                total += options[i].votes;
            }
            return new LiveOpsPoll
            {
                id         = id,
                question   = question.ToLocalizedString(),
                pollType   = pollType,
                options    = opts,
                votesTotal = total,
                userVote   = string.IsNullOrEmpty(userVoteId) ? null : new[] { userVoteId },
            };
        }
    }

    [Serializable]
    public class StubPollOption
    {
        public string id;
        public string label;
        public int    votes;
    }

    [Serializable]
    public class StubAnnouncementData
    {
        public string id    = "stub_ann_1";
        public string title = "Update 0.8 is coming!";
        [TextArea(3, 5)]
        public string body  = "We've been working hard on the next big update. Stay tuned for details!";
        public string url   = "https://store.steampowered.com";

        public LiveOpsAnnouncement ToAnnouncement() => new()
        {
            id          = id,
            title       = LocalizedString.FromRaw(title),
            body        = LocalizedString.FromRaw(body),
            url         = url,
            publishedAt = "2025-01-01T00:00:00Z",
        };
    }

    [Serializable]
    public class StubDevLogData
    {
        public string         id          = "stub_devlog_1";
        public string         focus       = "Currently working on";
        public string         title       = "Survival Mechanics";
        [TextArea(2, 4)]
        public string         description = "Implementing new hunger and thirst systems.";
        public StubDevLogItem[] items     = new[]
        {
            new StubDevLogItem { label = "Hunger system",    done = true  },
            new StubDevLogItem { label = "Thirst system",    done = true  },
            new StubDevLogItem { label = "Shelter building", done = false },
            new StubDevLogItem { label = "Weather effects",  done = false },
        };

        public LiveOpsDevLog ToDevLog()
        {
            var devItems = new LiveOpsDevLogItem[items.Length];
            for (int i = 0; i < items.Length; i++)
                devItems[i] = new LiveOpsDevLogItem
                    { label = LocalizedString.FromRaw(items[i].label), done = items[i].done };

            return new LiveOpsDevLog
            {
                id          = id,
                focus       = LocalizedString.FromRaw(focus),
                title       = LocalizedString.FromRaw(title),
                description = LocalizedString.FromRaw(description),
                items       = devItems,
                updatedAt   = "2025-01-01T00:00:00Z",
            };
        }
    }

    [Serializable]
    public class StubDevLogItem
    {
        public string label;
        public bool   done;
    }

    [Serializable]
    public class StubMilestoneData
    {
        public string description = "Wishlist on Steam";
        public int    current     = 3200;
        public int    goal        = 10000;
        public string unit        = "wishlists";

        public LiveOpsMilestoneData ToMilestone() => new()
        {
            description = LocalizedString.FromRaw(description),
            current     = current,
            goal        = goal,
            unit        = LocalizedString.FromRaw(unit),
        };
    }

    [Serializable]
    public class StubRatingData
    {
        public float avg      = 7.4f;
        public int   count    = 142;
        public int   userVote = 0;

        public LiveOpsRatingData ToRatingData() => new()
        {
            version  = "stub",
            avg      = avg,
            count    = count,
            userVote = userVote,
        };
    }
}
