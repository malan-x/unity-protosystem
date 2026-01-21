namespace ProtoSystem.Sound
{
    /// <summary>
    /// Приоритет звука для отсечения при превышении лимита
    /// </summary>
    public enum SoundPriority
    {
        /// <summary>Низкий приоритет — отсекается первым</summary>
        Low = 0,
        
        /// <summary>Обычный приоритет</summary>
        Normal = 128,
        
        /// <summary>Высокий приоритет</summary>
        High = 200,
        
        /// <summary>Критический — никогда не отсекается (UI, Voice)</summary>
        Critical = 255
    }
}
