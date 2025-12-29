// Packages/com.protosystem.core/Runtime/Settings/Data/SettingsSection.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Базовый класс для секции настроек
    /// </summary>
    public abstract class SettingsSection
    {
        /// <summary>
        /// Имя секции в INI файле (например "Audio", "Video")
        /// </summary>
        public abstract string SectionName { get; }

        /// <summary>
        /// Комментарий для секции в INI файле
        /// </summary>
        public abstract string SectionComment { get; }

        /// <summary>
        /// Кэш всех настроек секции
        /// </summary>
        private List<ISettingValue> _settingsCache;

        /// <summary>
        /// Получить все настройки секции через рефлексию
        /// </summary>
        public virtual IEnumerable<ISettingValue> GetAllSettings()
        {
            if (_settingsCache != null)
                return _settingsCache;

            _settingsCache = new List<ISettingValue>();

            // Находим все свойства типа ISettingValue
            var properties = GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => typeof(ISettingValue).IsAssignableFrom(p.PropertyType));

            foreach (var prop in properties)
            {
                if (prop.GetValue(this) is ISettingValue setting)
                {
                    _settingsCache.Add(setting);
                }
            }

            return _settingsCache;
        }

        /// <summary>
        /// Получить настройку по ключу
        /// </summary>
        public ISettingValue GetSetting(string key)
        {
            return GetAllSettings().FirstOrDefault(s => s.Key == key);
        }

        /// <summary>
        /// Есть ли несохранённые изменения
        /// </summary>
        public virtual bool HasUnsavedChanges()
        {
            return GetAllSettings().Any(s => s.IsModified);
        }

        /// <summary>
        /// Применить изменения (переопределяется в VideoSettings и т.д.)
        /// </summary>
        public virtual void Apply()
        {
            // По умолчанию ничего не делаем
            // Переопределяется в VideoSettings для Screen.SetResolution и т.д.
        }

        /// <summary>
        /// Сохранить все настройки (пометить как сохранённые)
        /// </summary>
        public virtual void MarkAllSaved()
        {
            foreach (var setting in GetAllSettings())
            {
                setting.MarkSaved();
            }
        }

        /// <summary>
        /// Откатить все изменения
        /// </summary>
        public virtual void Revert()
        {
            foreach (var setting in GetAllSettings())
            {
                setting.Revert();
            }
        }

        /// <summary>
        /// Сбросить к значениям по умолчанию
        /// </summary>
        public virtual void ResetToDefaults()
        {
            foreach (var setting in GetAllSettings())
            {
                setting.ResetToDefault();
            }
        }

        /// <summary>
        /// Сериализовать в словарь для INI
        /// </summary>
        public virtual Dictionary<string, string> Serialize()
        {
            var result = new Dictionary<string, string>();
            foreach (var setting in GetAllSettings())
            {
                result[setting.Key] = setting.Serialize();
            }
            return result;
        }

        /// <summary>
        /// Десериализовать из словаря INI
        /// </summary>
        public virtual void Deserialize(Dictionary<string, string> data)
        {
            foreach (var setting in GetAllSettings())
            {
                if (data.TryGetValue(setting.Key, out string value))
                {
                    setting.Deserialize(value);
                }
            }
        }

        /// <summary>
        /// Получить комментарии для настроек
        /// </summary>
        public virtual Dictionary<string, string> GetComments()
        {
            var result = new Dictionary<string, string>();
            foreach (var setting in GetAllSettings())
            {
                if (!string.IsNullOrEmpty(setting.Comment))
                {
                    result[setting.Key] = setting.Comment;
                }
            }
            return result;
        }
    }
}
