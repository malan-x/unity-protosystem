using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Категории логов
    /// </summary>
    [Flags]
    public enum LogCategory
    {
        None = 0,
        Initialization = 1 << 0,  // Старт/финиш систем
        Dependencies = 1 << 1,    // Резолв зависимостей
        Events = 1 << 2,          // EventBus подписки/отписки
        Runtime = 1 << 3,         // Рантайм логи систем
        All = ~0
    }

    /// <summary>
    /// Уровни логирования (флаги - можно комбинировать)
    /// </summary>
    [Flags]
    public enum LogLevel
    {
        None = 0,
        Errors = 1 << 0,
        Warnings = 1 << 1,
        Info = 1 << 2,
        Verbose = 1 << 3,
        All = Errors | Warnings | Info | Verbose
    }

    /// <summary>
    /// Режим фильтрации систем
    /// </summary>
    public enum SystemFilterMode
    {
        All,        // Все системы
        Whitelist,  // Только указанные
        Blacklist   // Все кроме указанных
    }

    /// <summary>
    /// Настройки логирования для конкретной системы
    /// </summary>
    [Serializable]
    public class SystemLogOverride
    {
        public string systemId;
        public bool useGlobal = true;
        public LogLevel logLevel = LogLevel.Errors | LogLevel.Warnings | LogLevel.Info;
        public LogCategory logCategories = LogCategory.All;
    }

    /// <summary>
    /// Глобальные настройки логирования
    /// </summary>
    [Serializable]
    public class LogSettings
    {
        [Tooltip("Глобальный уровень логирования (флаги)")]
        public LogLevel globalLogLevel = LogLevel.Errors | LogLevel.Warnings | LogLevel.Info;

        [Tooltip("Активные категории логов")]
        public LogCategory enabledCategories = LogCategory.All;

        [Tooltip("Режим фильтрации систем")]
        public SystemFilterMode filterMode = SystemFilterMode.All;

        [Tooltip("Список систем для фильтрации (whitelist/blacklist)")]
        public List<string> filteredSystems = new List<string>();

        [Tooltip("Индивидуальные настройки для систем")]
        public List<SystemLogOverride> systemOverrides = new List<SystemLogOverride>();

        [Tooltip("Использовать цветную маркировку в консоли")]
        public bool useColors = true;

        /// <summary>
        /// Получить override для системы
        /// </summary>
        public SystemLogOverride GetOverride(string systemId)
        {
            return systemOverrides.Find(o => o.systemId == systemId);
        }

        /// <summary>
        /// Установить или создать override для системы
        /// </summary>
        public void SetOverride(string systemId, LogLevel level, LogCategory categories = LogCategory.All, bool useGlobal = false)
        {
            var existing = GetOverride(systemId);
            if (existing != null)
            {
                existing.logLevel = level;
                existing.logCategories = categories;
                existing.useGlobal = useGlobal;
            }
            else
            {
                systemOverrides.Add(new SystemLogOverride
                {
                    systemId = systemId,
                    logLevel = level,
                    logCategories = categories,
                    useGlobal = useGlobal
                });
            }
        }
    }

    /// <summary>
    /// Централизованный логгер для ProtoSystem
    /// </summary>
    public static class ProtoLogger
    {
        // Цвета для категорий (Rich Text)
        private static readonly Dictionary<LogCategory, string> CategoryColors = new Dictionary<LogCategory, string>
        {
            { LogCategory.Initialization, "#4CAF50" },  // Зелёный
            { LogCategory.Dependencies, "#FF9800" },    // Оранжевый
            { LogCategory.Events, "#2196F3" },          // Синий
            { LogCategory.Runtime, "#9C27B0" }          // Фиолетовый
        };

        // Префиксы категорий
        private static readonly Dictionary<LogCategory, string> CategoryPrefixes = new Dictionary<LogCategory, string>
        {
            { LogCategory.Initialization, "Init" },
            { LogCategory.Dependencies, "Dep" },
            { LogCategory.Events, "Event" },
            { LogCategory.Runtime, "Run" }
        };

        // Цвета для уровней
        private static readonly Dictionary<LogLevel, string> LevelColors = new Dictionary<LogLevel, string>
        {
            { LogLevel.Errors, "#F44336" },    // Красный
            { LogLevel.Warnings, "#FFC107" },  // Жёлтый
            { LogLevel.Info, "#FFFFFF" },      // Белый
            { LogLevel.Verbose, "#888888" }    // Серый
        };

        /// <summary>
        /// Текущие настройки (берутся из SystemInitializationManager)
        /// Если null — логируются только ошибки и предупреждения
        /// </summary>
        public static LogSettings Settings { get; set; }

        /// <summary>
        /// Проверяет, должен ли лог быть выведен
        /// </summary>
        public static bool ShouldLog(string systemId, LogCategory category, LogLevel level)
        {
            // Ошибки проходят ВСЕГДА (независимо от категорий)
            bool isError = (level & LogLevel.Errors) != 0;
            
            if (Settings == null) return isError;

            // 1. Проверяем per-system override
            var systemOverride = Settings.GetOverride(systemId);
            LogLevel effectiveLevel;
            LogCategory effectiveCategories;

            if (systemOverride != null && !systemOverride.useGlobal)
            {
                effectiveLevel = systemOverride.logLevel;
                effectiveCategories = systemOverride.logCategories;
            }
            else
            {
                effectiveLevel = Settings.globalLogLevel;
                effectiveCategories = Settings.enabledCategories;
            }

            // Если уровень None - ничего не логируем (даже ошибки)
            if (effectiveLevel == LogLevel.None) return false;

            // 2. Проверяем фильтр систем (ошибки обходят фильтр)
            if (!isError)
            {
                bool systemAllowed = Settings.filterMode switch
                {
                    SystemFilterMode.Whitelist => Settings.filteredSystems.Contains(systemId),
                    SystemFilterMode.Blacklist => !Settings.filteredSystems.Contains(systemId),
                    _ => true
                };

                if (!systemAllowed) return false;
            }

            // 3. Проверяем категорию (ошибки обходят фильтр категорий)
            if (!isError && (effectiveCategories & category) == 0) return false;

            // 4. Проверяем уровень (флаговая проверка)
            return (effectiveLevel & level) != 0;
        }

        /// <summary>
        /// Основной метод логирования
        /// </summary>
        public static void Log(string systemId, LogCategory category, LogLevel level, string message, UnityEngine.Object context = null)
        {
            if (!ShouldLog(systemId, category, level)) return;

            string formattedMessage = FormatMessage(systemId, category, level, message);

            switch (level)
            {
                case LogLevel.Errors:
                    Debug.LogError(formattedMessage, context);
                    break;
                case LogLevel.Warnings:
                    Debug.LogWarning(formattedMessage, context);
                    break;
                default:
                    Debug.Log(formattedMessage, context);
                    break;
            }
        }

        /// <summary>
        /// Форматирует сообщение с цветами
        /// </summary>
        private static string FormatMessage(string systemId, LogCategory category, LogLevel level, string message)
        {
            if (Settings?.useColors != true)
            {
                string prefix = CategoryPrefixes.GetValueOrDefault(category, "Log");
                return $"[{prefix}] [{systemId}] {message}";
            }

            string catColor = CategoryColors.GetValueOrDefault(category, "#FFFFFF");
            string catPrefix = CategoryPrefixes.GetValueOrDefault(category, "Log");
            string lvlColor = LevelColors.GetValueOrDefault(level, "#FFFFFF");

            return $"<color={catColor}>[{catPrefix}]</color> <color=#888>[{systemId}]</color> <color={lvlColor}>{message}</color>";
        }

        #region Shortcut Methods

        // === Initialization ===
        public static void LogInit(string systemId, string message, UnityEngine.Object context = null)
            => Log(systemId, LogCategory.Initialization, LogLevel.Info, message, context);

        public static void LogInitVerbose(string systemId, string message, UnityEngine.Object context = null)
            => Log(systemId, LogCategory.Initialization, LogLevel.Verbose, message, context);

        // === Dependencies ===
        public static void LogDep(string systemId, string message, UnityEngine.Object context = null)
            => Log(systemId, LogCategory.Dependencies, LogLevel.Info, message, context);

        public static void LogDepVerbose(string systemId, string message, UnityEngine.Object context = null)
            => Log(systemId, LogCategory.Dependencies, LogLevel.Verbose, message, context);

        // === Events ===
        public static void LogEvent(string systemId, string message, UnityEngine.Object context = null)
            => Log(systemId, LogCategory.Events, LogLevel.Info, message, context);

        public static void LogEventVerbose(string systemId, string message, UnityEngine.Object context = null)
            => Log(systemId, LogCategory.Events, LogLevel.Verbose, message, context);

        // === Runtime ===
        public static void LogRuntime(string systemId, string message, UnityEngine.Object context = null)
            => Log(systemId, LogCategory.Runtime, LogLevel.Info, message, context);

        public static void LogRuntimeVerbose(string systemId, string message, UnityEngine.Object context = null)
            => Log(systemId, LogCategory.Runtime, LogLevel.Verbose, message, context);

        // === Errors & Warnings (всегда Runtime категория) ===
        public static void LogError(string systemId, string message, UnityEngine.Object context = null)
            => Log(systemId, LogCategory.Runtime, LogLevel.Errors, message, context);

        public static void LogWarning(string systemId, string message, UnityEngine.Object context = null)
            => Log(systemId, LogCategory.Runtime, LogLevel.Warnings, message, context);

        #endregion

        #region Runtime Configuration

        /// <summary>
        /// Включить/выключить категорию в рантайме
        /// </summary>
        public static void SetCategoryEnabled(LogCategory category, bool enabled)
        {
            if (Settings == null) return;

            if (enabled)
                Settings.enabledCategories |= category;
            else
                Settings.enabledCategories &= ~category;
        }

        /// <summary>
        /// Установить глобальный уровень логирования
        /// </summary>
        public static void SetGlobalLogLevel(LogLevel level)
        {
            if (Settings != null)
                Settings.globalLogLevel = level;
        }

        /// <summary>
        /// Добавить систему в фильтр
        /// </summary>
        public static void AddSystemToFilter(string systemId)
        {
            if (Settings != null && !Settings.filteredSystems.Contains(systemId))
                Settings.filteredSystems.Add(systemId);
        }

        /// <summary>
        /// Убрать систему из фильтра
        /// </summary>
        public static void RemoveSystemFromFilter(string systemId)
        {
            Settings?.filteredSystems.Remove(systemId);
        }

        /// <summary>
        /// Установить режим фильтрации
        /// </summary>
        public static void SetFilterMode(SystemFilterMode mode)
        {
            if (Settings != null)
                Settings.filterMode = mode;
        }

        #endregion
    }
}
