// Packages/com.protosystem.core/Runtime/GameSession/SessionEndReason.cs
namespace ProtoSystem
{
    /// <summary>
    /// Причины завершения сессии.
    /// Проекты могут использовать значения >= 1000 для своих причин.
    /// </summary>
    public enum SessionEndReason
    {
        /// <summary>Причина не указана</summary>
        None = 0,
        
        // === Поражения (1-99) ===
        
        /// <summary>Смерть игрока</summary>
        PlayerDeath = 1,
        
        /// <summary>Время истекло</summary>
        TimeOut = 2,
        
        /// <summary>Цель уничтожена (база, объект защиты)</summary>
        ObjectiveDestroyed = 3,
        
        /// <summary>Ресурсы исчерпаны</summary>
        ResourcesDepleted = 4,
        
        // === Победы (100-199) ===
        
        /// <summary>Миссия выполнена</summary>
        MissionComplete = 100,
        
        /// <summary>Все враги уничтожены</summary>
        AllEnemiesDefeated = 101,
        
        /// <summary>Цель достигнута</summary>
        ObjectiveReached = 102,
        
        /// <summary>Босс побеждён</summary>
        BossDefeated = 103,
        
        // === Прочие (200-299) ===
        
        /// <summary>Игрок вышел</summary>
        PlayerQuit = 200,
        
        /// <summary>Отключение (мультиплеер)</summary>
        Disconnect = 201,
        
        /// <summary>Ручной рестарт</summary>
        ManualRestart = 202,
        
        /// <summary>Возврат в меню</summary>
        ReturnToMenu = 203
    }
}
