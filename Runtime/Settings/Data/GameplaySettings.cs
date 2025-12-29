// Packages/com.protosystem.core/Runtime/Settings/Data/GameplaySettings.cs
using System.Globalization;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Секция игровых настроек
    /// </summary>
    public class GameplaySettings : SettingsSection
    {
        public override string SectionName => "Gameplay";
        public override string SectionComment => "Game-specific settings";

        /// <summary>Язык интерфейса ("auto" = системный)</summary>
        public SettingValue<string> Language { get; }
        
        /// <summary>Показывать субтитры</summary>
        public SettingValue<bool> Subtitles { get; }

        public GameplaySettings()
        {
            Language = new SettingValue<string>(
                "Language", SectionName,
                "Interface language ('auto' = system language)",
                EventBus.Settings.Gameplay.LanguageChanged,
                "auto"
            );

            Subtitles = new SettingValue<bool>(
                "Subtitles", SectionName,
                "Show subtitles (0/1)",
                EventBus.Settings.Gameplay.SubtitlesChanged,
                true
            );
        }

        /// <summary>
        /// Получить актуальный язык (с учётом "auto")
        /// </summary>
        public string GetResolvedLanguage()
        {
            if (Language.Value == "auto")
            {
                // Возвращаем системный язык
                return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            }
            return Language.Value;
        }

        /// <summary>
        /// Установить значения по умолчанию из конфига
        /// </summary>
        public void SetDefaults(string language, bool subtitles)
        {
            Language.SetDefaultValue(language);
            Subtitles.SetDefaultValue(subtitles);
        }
    }
}
