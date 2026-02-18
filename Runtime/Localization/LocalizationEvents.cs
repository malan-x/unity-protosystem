// Packages/com.protosystem.core/Runtime/Localization/LocalizationEvents.cs
using System;

namespace ProtoSystem
{
    /// <summary>
    /// События системы локализации для EventBus.
    /// Номера 10700-10799 зарезервированы для Localization.
    /// </summary>
    public static partial class EventBus
    {
        public static partial class Localization
        {
            /// <summary>Язык изменён. Payload: LocaleChangedData</summary>
            public const int LanguageChanged = 10700;
            
            /// <summary>Система локализации готова. Payload: null</summary>
            public const int Ready = 10701;
            
            /// <summary>Таблица загружена. Payload: string tableName</summary>
            public const int TableLoaded = 10702;
        }
        
        /// <summary>Русский алиас</summary>
        public static partial class Локализация
        {
            public const int Язык_изменён = Localization.LanguageChanged;
            public const int Готова = Localization.Ready;
            public const int Таблица_загружена = Localization.TableLoaded;
        }
    }
    
    /// <summary>
    /// Данные события смены языка
    /// </summary>
    [Serializable]
    public struct LocaleChangedData
    {
        public string PreviousLanguage;
        public string NewLanguage;
    }
}
