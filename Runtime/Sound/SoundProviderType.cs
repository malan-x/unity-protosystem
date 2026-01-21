namespace ProtoSystem.Sound
{
    /// <summary>
    /// Тип аудио-провайдера
    /// </summary>
    public enum SoundProviderType
    {
        /// <summary>Встроенный Unity AudioSource</summary>
        Unity = 0,
        
        /// <summary>FMOD Studio</summary>
        FMOD = 1,
        
        /// <summary>Wwise (заглушка)</summary>
        Wwise = 2
    }
}
