// Packages/com.protosystem.core/Runtime/GameSession/GameSessionEvents.cs
using System;

namespace ProtoSystem
{
    /// <summary>
    /// События системы игровых сессий для EventBus.
    /// Номера 10600-10699 зарезервированы для GameSession.
    /// </summary>
    public static partial class EventBus
    {
        /// <summary>
        /// События игровой сессии (English)
        /// </summary>
        public static partial class Session
        {
            /// <summary>Сессия началась, можно играть</summary>
            public const int Started = 10600;
            
            /// <summary>Сессия завершена (победа/поражение)</summary>
            public const int Ended = 10601;
            
            /// <summary>Команда системам сбросить состояние</summary>
            public const int Reset = 10602;
            
            /// <summary>Сессия поставлена на паузу</summary>
            public const int Paused = 10603;
            
            /// <summary>Сессия возобновлена</summary>
            public const int Resumed = 10604;
            
            /// <summary>Состояние сессии изменилось</summary>
            public const int StateChanged = 10605;
            
            /// <summary>Возврат в главное меню</summary>
            public const int ReturnedToMenu = 10606;
            
            /// <summary>Запрос на рестарт</summary>
            public const int RestartRequested = 10607;
        }
        
        /// <summary>
        /// События игровой сессии (Русский алиас)
        /// </summary>
        public static partial class Сессия
        {
            /// <summary>Сессия началась, можно играть</summary>
            public const int Началась = Session.Started;
            
            /// <summary>Сессия завершена (победа/поражение)</summary>
            public const int Завершена = Session.Ended;
            
            /// <summary>Команда системам сбросить состояние</summary>
            public const int Сброс = Session.Reset;
            
            /// <summary>Сессия поставлена на паузу</summary>
            public const int Пауза = Session.Paused;
            
            /// <summary>Сессия возобновлена</summary>
            public const int Продолжена = Session.Resumed;
            
            /// <summary>Состояние сессии изменилось</summary>
            public const int Состояние_изменено = Session.StateChanged;
            
            /// <summary>Возврат в главное меню</summary>
            public const int Возврат_в_меню = Session.ReturnedToMenu;
            
            /// <summary>Запрос на рестарт</summary>
            public const int Запрос_рестарта = Session.RestartRequested;
        }
    }
    
    /// <summary>
    /// Данные события завершения сессии
    /// </summary>
    [Serializable]
    public struct SessionEndedData
    {
        public SessionState FinalState;
        public SessionEndReason Reason;
        public bool IsVictory;
        public float SessionTime;
        public SessionStats Stats;
    }
    
    /// <summary>
    /// Данные события изменения состояния сессии
    /// </summary>
    [Serializable]
    public struct SessionStateChangedData
    {
        public SessionState PreviousState;
        public SessionState NewState;
    }
}
