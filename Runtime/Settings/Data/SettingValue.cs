// Packages/com.protosystem.core/Runtime/Settings/Data/SettingValue.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Интерфейс для нетипизированного доступа к настройкам
    /// </summary>
    public interface ISettingValue
    {
        string Key { get; }
        string Section { get; }
        string Comment { get; }
        int EventId { get; }
        bool IsModified { get; }
        
        object GetValue();
        void SetValue(object value);
        string Serialize();
        void Deserialize(string value);
        
        void MarkSaved();
        void Revert();
        void ResetToDefault();
    }

    /// <summary>
    /// Обёртка для значения настройки с отслеживанием изменений
    /// </summary>
    public class SettingValue<T> : ISettingValue
    {
        /// <summary>
        /// Глобальный флаг для подавления событий (используется при загрузке настроек)
        /// </summary>
        public static bool SuppressEvents { get; set; } = false;
        
        public string Key { get; }
        public string Section { get; }
        public string Comment { get; }
        public int EventId { get; }

        private T _value;
        private T _savedValue;
        private T _defaultValue;
        private readonly Func<T, T, bool> _equalityComparer;

        public T Value
        {
            get => _value;
            set
            {
                if (AreEqual(_value, value)) return;

                var prev = _value;
                _value = value;

                // Не публикуем события если они подавлены (при загрузке)
                if (SuppressEvents) return;

                // Публикуем событие изменения если задан EventId
                if (EventId > 0)
                {
                    EventBus.Publish(EventId, new SettingChangedData<T>
                    {
                        Section = Section,
                        Key = Key,
                        Value = value,
                        PreviousValue = prev
                    });
                }

                // Публикуем общее событие модификации
                EventBus.Publish(EventBus.Settings.Modified, new SettingChangedData
                {
                    Section = Section,
                    Key = Key,
                    Value = value,
                    PreviousValue = prev
                });
            }
        }

        public T SavedValue => _savedValue;
        public T DefaultValue => _defaultValue;
        public bool IsModified => !AreEqual(_value, _savedValue);

        /// <summary>
        /// Создаёт настройку с указанными параметрами
        /// </summary>
        /// <param name="key">Ключ в INI файле</param>
        /// <param name="section">Секция в INI файле</param>
        /// <param name="comment">Комментарий для INI файла</param>
        /// <param name="eventId">ID события при изменении (0 = не публиковать)</param>
        /// <param name="defaultValue">Значение по умолчанию</param>
        /// <param name="equalityComparer">Кастомный компаратор (опционально)</param>
        public SettingValue(
            string key,
            string section,
            string comment,
            int eventId,
            T defaultValue,
            Func<T, T, bool> equalityComparer = null)
        {
            Key = key;
            Section = section;
            Comment = comment;
            EventId = eventId;
            _defaultValue = defaultValue;
            _value = defaultValue;
            _savedValue = defaultValue;
            _equalityComparer = equalityComparer;
        }

        /// <summary>
        /// Сравнение значений
        /// </summary>
        private bool AreEqual(T a, T b)
        {
            if (_equalityComparer != null)
                return _equalityComparer(a, b);

            // Для float используем погрешность
            if (typeof(T) == typeof(float))
            {
                return Mathf.Abs((float)(object)a - (float)(object)b) < 0.0001f;
            }

            return EqualityComparer<T>.Default.Equals(a, b);
        }

        /// <summary>
        /// Отметить значение как сохранённое
        /// </summary>
        public void MarkSaved()
        {
            _savedValue = _value;
        }

        /// <summary>
        /// Откатить к сохранённому значению
        /// </summary>
        public void Revert()
        {
            Value = _savedValue;
        }

        /// <summary>
        /// Сбросить к значению по умолчанию
        /// </summary>
        public void ResetToDefault()
        {
            Value = _defaultValue;
        }

        /// <summary>
        /// Установить значение по умолчанию (для конфигурации)
        /// </summary>
        public void SetDefaultValue(T value)
        {
            _defaultValue = value;
        }

        #region ISettingValue Implementation

        object ISettingValue.GetValue() => _value;

        void ISettingValue.SetValue(object value)
        {
            if (value is T typedValue)
                Value = typedValue;
            else
                ProtoLogger.Log("SettingsSystem", LogCategory.Runtime, LogLevel.Errors, $"Cannot set value of type {value?.GetType()} to setting {Key} of type {typeof(T)}");
        }

        public string Serialize()
        {
            return _value switch
            {
                bool b => b ? "1" : "0",
                float f => f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                int i => i.ToString(),
                _ => _value?.ToString() ?? ""
            };
        }

        public void Deserialize(string value)
        {
            try
            {
                object result = typeof(T) switch
                {
                    Type t when t == typeof(bool) => value == "1" || value.ToLower() == "true",
                    Type t when t == typeof(int) => int.Parse(value),
                    Type t when t == typeof(float) => ParseFloat(value),
                    Type t when t == typeof(string) => value,
                    _ => throw new NotSupportedException($"Type {typeof(T)} is not supported for deserialization")
                };

                _value = (T)result;
                _savedValue = (T)result;
            }
            catch (Exception ex)
            {
                ProtoLogger.Log("SettingsSystem", LogCategory.Runtime, LogLevel.Warnings, $"Failed to deserialize '{value}' for {Key}: {ex.Message}. Using default.");
                _value = _defaultValue;
                _savedValue = _defaultValue;
            }
        }

        private float ParseFloat(string value)
        {
            // Поддержка разных форматов
            if (float.TryParse(value, System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out float result))
                return result;
            if (float.TryParse(value.Replace(",", "."), 
                System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out result))
                return result;
            return _defaultValue is float f ? f : 0f;
        }

        #endregion

        public static implicit operator T(SettingValue<T> setting) => setting.Value;
        
        public override string ToString() => $"{Section}.{Key} = {Value} (saved: {SavedValue}, default: {DefaultValue})";
    }
}
