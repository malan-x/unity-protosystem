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
        /// <summary>Получить список активных сообщений (MOTD/новости).</summary>
        Task<List<LiveOpsMessage>> FetchMessagesAsync();

        /// <summary>Получить список активных опросов.</summary>
        Task<List<LiveOpsPoll>> FetchPollsAsync();

        /// <summary>Отправить ответ на опрос.</summary>
        Task<bool> SubmitPollAnswerAsync(LiveOpsPollAnswer answer);

        /// <summary>Отправить аналитическое событие.</summary>
        Task<bool> SendEventAsync(LiveOpsEvent evt);

        /// <summary>Отправить фидбек от игрока.</summary>
        Task<bool> SubmitFeedbackAsync(LiveOpsFeedback feedback);
    }
}
