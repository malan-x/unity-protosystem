// Packages/com.protosystem.core/Editor/Publishing/Core/IPlatformPublisher.cs
using System;
using System.Threading.Tasks;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Результат операции публикации
    /// </summary>
    public class PublishResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string BuildId { get; set; }
        public string Error { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        public static PublishResult Ok(string message = "Success", string buildId = null)
        {
            return new PublishResult { Success = true, Message = message, BuildId = buildId };
        }
        
        public static PublishResult Fail(string error)
        {
            return new PublishResult { Success = false, Error = error };
        }
    }

    /// <summary>
    /// Прогресс операции
    /// </summary>
    public class PublishProgress
    {
        public float Progress { get; set; } // 0-1
        public string Status { get; set; }
        public bool IsIndeterminate { get; set; }
    }

    /// <summary>
    /// Интерфейс издателя на платформу
    /// </summary>
    public interface IPlatformPublisher
    {
        /// <summary>
        /// Идентификатор платформы
        /// </summary>
        string PlatformId { get; }
        
        /// <summary>
        /// Отображаемое название
        /// </summary>
        string DisplayName { get; }
        
        /// <summary>
        /// Поддерживается ли платформа
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Проверить готовность к публикации
        /// </summary>
        bool ValidateConfig(out string error);

        /// <summary>
        /// Загрузить билд на платформу
        /// </summary>
        Task<PublishResult> UploadAsync(string buildPath, string branch, string description, 
            IProgress<PublishProgress> progress = null);

        /// <summary>
        /// Опубликовать патчноуты/новости
        /// </summary>
        Task<PublishResult> PublishNewsAsync(PatchNotesEntry entry, IProgress<PublishProgress> progress = null);

        /// <summary>
        /// Установить билд как live
        /// </summary>
        Task<PublishResult> SetLiveAsync(string buildId, string branch, IProgress<PublishProgress> progress = null);
    }
}
