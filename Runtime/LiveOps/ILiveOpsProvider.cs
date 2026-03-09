// Packages/com.protosystem.core/Runtime/LiveOps/ILiveOpsProvider.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Контракт провайдера LiveOps. Реализуется в проекте под конкретный бэкенд.
    /// 
    /// Пример использования в проекте:
    /// <code>
    /// public class MyProvider : ILiveOpsProvider
    /// {
    ///     public async Task&lt;List&lt;LiveOpsMessage&gt;&gt; FetchMessagesAsync() { ... }
    ///     // ...
    /// }
    /// 
    /// // В конфиге:
    /// liveOpsConfig.SetProvider(new MyProvider());
    /// </code>
    /// </summary>
    public interface ILiveOpsProvider
    {
        // ── Существующие методы ──────────────────────────────────────

        Task<List<LiveOpsMessage>> FetchMessagesAsync();
        Task<List<LiveOpsPoll>>    FetchPollsAsync();
        Task<bool>                 SubmitPollAnswerAsync(LiveOpsPollAnswer answer);
        Task<bool>                 SendEventAsync(LiveOpsEvent evt);
        Task<bool>                 SubmitFeedbackAsync(LiveOpsFeedback feedback);

        // ── Community Panel (новые методы, default = не реализовано) ──
        // Существующие провайдеры не ломаются — просто переопределите нужные.

        /// <summary>GET /config — конфигурация виджетов панели.</summary>
        Task<LiveOpsPanelConfig> FetchPanelConfigAsync() =>
            Task.FromResult<LiveOpsPanelConfig>(null);

        /// <summary>GET /announcements.</summary>
        Task<List<LiveOpsAnnouncement>> FetchAnnouncementsAsync() =>
            Task.FromResult<List<LiveOpsAnnouncement>>(null);

        /// <summary>GET /devlog.</summary>
        Task<LiveOpsDevLog> FetchDevLogAsync() =>
            Task.FromResult<LiveOpsDevLog>(null);

        /// <summary>GET /ratings?version={version}.</summary>
        Task<LiveOpsRatingData> FetchRatingAsync(string version) =>
            Task.FromResult<LiveOpsRatingData>(null);

        /// <summary>POST /ratings.</summary>
        Task<LiveOpsRatingResult> SubmitRatingAsync(LiveOpsRatingSubmit submit) =>
            Task.FromResult<LiveOpsRatingResult>(null);
    }
}
