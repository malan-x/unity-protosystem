// Packages/com.protosystem.core/Runtime/Settings/Data/CustomSettingsSection.cs
using System;
using System.Collections.Generic;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Базовый класс для кастомных секций настроек проекта
    /// </summary>
    public abstract class CustomSettingsSection : SettingsSection
    {
        // Наследники переопределяют SectionName и SectionComment
    }

    /// <summary>
    /// Динамическая секция настроек (создаётся из конфига)
    /// </summary>
    public class DynamicSettingsSection : SettingsSection
    {
        private readonly string _sectionName;
        private readonly string _sectionComment;
        private readonly List<ISettingValue> _settings = new List<ISettingValue>();

        public override string SectionName => _sectionName;
        public override string SectionComment => _sectionComment;

        public DynamicSettingsSection(string name, string comment)
        {
            _sectionName = name;
            _sectionComment = comment;
        }

        /// <summary>
        /// Добавить строковую настройку
        /// </summary>
        public SettingValue<string> AddString(string key, string comment, int eventId, string defaultValue)
        {
            var setting = new SettingValue<string>(key, SectionName, comment, eventId, defaultValue);
            _settings.Add(setting);
            return setting;
        }

        /// <summary>
        /// Добавить целочисленную настройку
        /// </summary>
        public SettingValue<int> AddInt(string key, string comment, int eventId, int defaultValue)
        {
            var setting = new SettingValue<int>(key, SectionName, comment, eventId, defaultValue);
            _settings.Add(setting);
            return setting;
        }

        /// <summary>
        /// Добавить настройку с плавающей точкой
        /// </summary>
        public SettingValue<float> AddFloat(string key, string comment, int eventId, float defaultValue)
        {
            var setting = new SettingValue<float>(key, SectionName, comment, eventId, defaultValue);
            _settings.Add(setting);
            return setting;
        }

        /// <summary>
        /// Добавить булеву настройку
        /// </summary>
        public SettingValue<bool> AddBool(string key, string comment, int eventId, bool defaultValue)
        {
            var setting = new SettingValue<bool>(key, SectionName, comment, eventId, defaultValue);
            _settings.Add(setting);
            return setting;
        }

        public override IEnumerable<ISettingValue> GetAllSettings() => _settings;
    }
}
