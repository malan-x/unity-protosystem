// Packages/com.protosystem.core/Runtime/GameSession/SessionState.cs
namespace ProtoSystem
{
    /// <summary>
    /// Состояния игровой сессии
    /// </summary>
    public enum SessionState
    {
        /// <summary>Система не инициализирована</summary>
        None = 0,
        
        /// <summary>Готова к старту (главное меню)</summary>
        Ready = 1,
        
        /// <summary>Идёт инициализация/сброс</summary>
        Starting = 2,
        
        /// <summary>Игра активна</summary>
        Playing = 3,
        
        /// <summary>На паузе (состояние, не timeScale)</summary>
        Paused = 4,
        
        /// <summary>Поражение</summary>
        GameOver = 5,
        
        /// <summary>Победа</summary>
        Victory = 6
    }
}
