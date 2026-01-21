namespace ProtoSystem.Sound
{
    /// <summary>
    /// Категории звуков для раздельного управления громкостью
    /// </summary>
    public enum SoundCategory
    {
        /// <summary>Мастер-канал (влияет на всё)</summary>
        Master = 0,
        
        /// <summary>Фоновая музыка</summary>
        Music = 1,
        
        /// <summary>Звуковые эффекты</summary>
        SFX = 2,
        
        /// <summary>Голоса, диалоги</summary>
        Voice = 3,
        
        /// <summary>Фоновые звуки окружения</summary>
        Ambient = 4,
        
        /// <summary>Звуки интерфейса</summary>
        UI = 5
    }
}
