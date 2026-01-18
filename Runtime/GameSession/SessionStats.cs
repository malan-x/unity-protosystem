// Packages/com.protosystem.core/Runtime/GameSession/SessionStats.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Статистика игровой сессии.
    /// Содержит базовое время сессии и гибкий словарь для проект-специфичных данных.
    /// </summary>
    [Serializable]
    public class SessionStats
    {
        /// <summary>Время сессии в секундах</summary>
        public float SessionTime { get; set; }
        
        /// <summary>Время начала сессии (Time.realtimeSinceStartup)</summary>
        public float StartTime { get; private set; }
        
        /// <summary>Количество рестартов в текущей сессии</summary>
        public int RestartCount { get; set; }
        
        /// <summary>Произвольные данные проекта</summary>
        private Dictionary<string, object> _customData = new Dictionary<string, object>();
        
        /// <summary>
        /// Получить значение по ключу
        /// </summary>
        public T Get<T>(string key, T defaultValue = default)
        {
            if (_customData.TryGetValue(key, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    Debug.LogWarning($"[SessionStats] Cannot convert '{key}' to {typeof(T).Name}");
                    return defaultValue;
                }
            }
            return defaultValue;
        }
        
        /// <summary>
        /// Установить значение по ключу
        /// </summary>
        public void Set<T>(string key, T value)
        {
            _customData[key] = value;
        }
        
        /// <summary>
        /// Инкрементировать числовое значение
        /// </summary>
        public void Increment(string key, int delta = 1)
        {
            var current = Get<int>(key, 0);
            Set(key, current + delta);
        }
        
        /// <summary>
        /// Инкрементировать float значение
        /// </summary>
        public void IncrementFloat(string key, float delta)
        {
            var current = Get<float>(key, 0f);
            Set(key, current + delta);
        }
        
        /// <summary>
        /// Проверить наличие ключа
        /// </summary>
        public bool HasKey(string key) => _customData.ContainsKey(key);
        
        /// <summary>
        /// Удалить ключ
        /// </summary>
        public bool Remove(string key) => _customData.Remove(key);
        
        /// <summary>
        /// Получить все ключи
        /// </summary>
        public IEnumerable<string> GetKeys() => _customData.Keys;
        
        /// <summary>
        /// Начать отсчёт времени сессии
        /// </summary>
        public void StartTimer()
        {
            StartTime = Time.realtimeSinceStartup;
        }
        
        /// <summary>
        /// Обновить время сессии (вызывать каждый кадр или периодически)
        /// </summary>
        public void UpdateTime()
        {
            if (StartTime > 0)
            {
                SessionTime = Time.realtimeSinceStartup - StartTime;
            }
        }
        
        /// <summary>
        /// Сбросить статистику
        /// </summary>
        public void Reset()
        {
            SessionTime = 0f;
            StartTime = 0f;
            _customData.Clear();
            // RestartCount не сбрасываем - это счётчик рестартов
        }
        
        /// <summary>
        /// Полный сброс включая счётчик рестартов
        /// </summary>
        public void FullReset()
        {
            Reset();
            RestartCount = 0;
        }
        
        /// <summary>
        /// Преобразовать в строку для отладки
        /// </summary>
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"SessionTime: {SessionTime:F1}s");
            sb.AppendLine($"RestartCount: {RestartCount}");
            foreach (var kvp in _customData)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
            return sb.ToString();
        }
    }
}
