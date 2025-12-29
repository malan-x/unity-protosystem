// Packages/com.protosystem.core/Runtime/Settings/SettingsEvents.cs
using System;

namespace ProtoSystem
{
    /// <summary>
    /// События системы настроек для EventBus
    /// Номера начинаются с 10100 чтобы не пересекаться с пользовательскими событиями
    /// </summary>
    public static partial class EventBus
    {
        public static partial class Settings
        {
            /// <summary>Настройки загружены из файла</summary>
            public const int Loaded = 10100;
            /// <summary>Настройки сохранены в файл</summary>
            public const int Saved = 10101;
            /// <summary>Настройки применены</summary>
            public const int Applied = 10102;
            /// <summary>Изменения отменены</summary>
            public const int Reverted = 10103;
            /// <summary>Сброс к значениям по умолчанию</summary>
            public const int ResetToDefaults = 10104;
            /// <summary>Появились несохранённые изменения</summary>
            public const int Modified = 10105;

            public static class Audio
            {
                public const int MasterChanged = 10110;
                public const int MusicChanged = 10111;
                public const int SFXChanged = 10112;
                public const int VoiceChanged = 10113;
                public const int MuteChanged = 10114;
            }

            public static class Video
            {
                public const int MonitorChanged = 10120;
                public const int ResolutionChanged = 10121;
                public const int FullscreenChanged = 10122;
                public const int VSyncChanged = 10123;
                public const int QualityChanged = 10124;
                public const int FrameRateChanged = 10125;
            }

            public static class Controls
            {
                public const int SensitivityChanged = 10130;
                public const int InvertYChanged = 10131;
                public const int InvertXChanged = 10132;
            }

            public static class Gameplay
            {
                public const int LanguageChanged = 10140;
                public const int SubtitlesChanged = 10141;
            }
        }
    }

    /// <summary>
    /// Данные события изменения настройки
    /// </summary>
    public struct SettingChangedData<T>
    {
        public string Section;
        public string Key;
        public T Value;
        public T PreviousValue;
    }

    /// <summary>
    /// Данные события изменения настройки (нетипизированная версия)
    /// </summary>
    public struct SettingChangedData
    {
        public string Section;
        public string Key;
        public object Value;
        public object PreviousValue;
    }
}
