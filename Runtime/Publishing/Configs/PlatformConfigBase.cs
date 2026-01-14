// Packages/com.protosystem.core/Runtime/Publishing/Configs/PlatformConfigBase.cs
using UnityEngine;

namespace ProtoSystem.Publishing
{
    /// <summary>
    /// Базовый класс конфигурации платформы публикации
    /// </summary>
    public abstract class PlatformConfigBase : ScriptableObject
    {
        [Header("Идентификация")]
        [Tooltip("Уникальный ID платформы")]
        public abstract string PlatformId { get; }
        
        [Tooltip("Отображаемое название")]
        public abstract string DisplayName { get; }

        [Header("Общие настройки")]
        [Tooltip("Платформа активна")]
        public bool enabled = true;
        
        [Tooltip("Описание/заметки")]
        [TextArea(2, 4)]
        public string notes;

        /// <summary>
        /// Проверить валидность конфигурации
        /// </summary>
        public abstract bool Validate(out string error);

        /// <summary>
        /// Получить статус конфигурации для отображения
        /// </summary>
        public virtual string GetStatusText()
        {
            if (!enabled) return "Disabled";
            if (!Validate(out var error)) return $"Error: {error}";
            return "Ready";
        }

        /// <summary>
        /// Получить цвет статуса
        /// </summary>
        public virtual Color GetStatusColor()
        {
            if (!enabled) return Color.gray;
            if (!Validate(out _)) return new Color(1f, 0.4f, 0.4f);
            return new Color(0.4f, 1f, 0.4f);
        }
    }
}
