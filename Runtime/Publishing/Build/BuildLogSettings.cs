// Packages/com.protosystem.core/Runtime/Publishing/Build/BuildLogSettings.cs
using System;
using UnityEngine;

namespace ProtoSystem.Publishing
{
    /// <summary>
    /// Уровень детализации логов
    /// </summary>
    public enum LogDetailLevel
    {
        [Tooltip("Логи отключены")]
        None,
        
        [Tooltip("Только сообщение")]
        Compact,
        
        [Tooltip("Сообщение + стек вызовов")]
        Full
    }

    /// <summary>
    /// Настройки логирования при сборке
    /// </summary>
    [Serializable]
    public class BuildLogSettings
    {
        [Header("Уровни логов")]
        [Tooltip("Обычные логи (Debug.Log)")]
        public LogDetailLevel logLevel = LogDetailLevel.Compact;
        
        [Tooltip("Предупреждения (Debug.LogWarning)")]
        public LogDetailLevel warningLevel = LogDetailLevel.Full;
        
        [Tooltip("Ошибки (Debug.LogError)")]
        public LogDetailLevel errorLevel = LogDetailLevel.Full;
        
        [Tooltip("Исключения")]
        public LogDetailLevel exceptionLevel = LogDetailLevel.Full;
        
        [Tooltip("Assert")]
        public LogDetailLevel assertLevel = LogDetailLevel.Full;

        [Header("Вывод")]
        [Tooltip("Записывать логи в файл")]
        public bool logToFile = false;
        
        [Tooltip("Путь к файлу логов (относительно проекта)")]
        public string logFilePath = "Logs/build.log";
        
        [Tooltip("Максимальный размер файла логов (MB)")]
        public int maxLogFileSizeMB = 50;
        
        [Tooltip("Очищать файл перед новой сборкой")]
        public bool clearLogOnBuild = true;

        [Header("Фильтрация")]
        [Tooltip("Игнорировать логи содержащие эти строки")]
        public string[] ignorePatterns = new string[0];

        /// <summary>
        /// Преобразовать в StackTraceLogType для Unity
        /// </summary>
        public StackTraceLogType GetStackTraceType(LogType logType)
        {
            var level = GetDetailLevel(logType);
            
            return level switch
            {
                LogDetailLevel.None => StackTraceLogType.None,
                LogDetailLevel.Compact => StackTraceLogType.None,
                LogDetailLevel.Full => StackTraceLogType.ScriptOnly,
                _ => StackTraceLogType.ScriptOnly
            };
        }

        /// <summary>
        /// Получить уровень детализации для типа лога
        /// </summary>
        public LogDetailLevel GetDetailLevel(LogType logType)
        {
            return logType switch
            {
                LogType.Log => logLevel,
                LogType.Warning => warningLevel,
                LogType.Error => errorLevel,
                LogType.Exception => exceptionLevel,
                LogType.Assert => assertLevel,
                _ => LogDetailLevel.Compact
            };
        }

        /// <summary>
        /// Проверить, включен ли тип лога
        /// </summary>
        public bool IsEnabled(LogType logType)
        {
            return GetDetailLevel(logType) != LogDetailLevel.None;
        }

        /// <summary>
        /// Применить настройки к PlayerSettings
        /// </summary>
        public void ApplyToPlayerSettings()
        {
            // Применяем stack trace настройки
            Application.SetStackTraceLogType(LogType.Log, GetStackTraceType(LogType.Log));
            Application.SetStackTraceLogType(LogType.Warning, GetStackTraceType(LogType.Warning));
            Application.SetStackTraceLogType(LogType.Error, GetStackTraceType(LogType.Error));
            Application.SetStackTraceLogType(LogType.Exception, GetStackTraceType(LogType.Exception));
            Application.SetStackTraceLogType(LogType.Assert, GetStackTraceType(LogType.Assert));
        }

        /// <summary>
        /// Создать настройки по умолчанию
        /// </summary>
        public static BuildLogSettings CreateDefault()
        {
            return new BuildLogSettings();
        }

        /// <summary>
        /// Создать настройки для Release билда
        /// </summary>
        public static BuildLogSettings CreateRelease()
        {
            return new BuildLogSettings
            {
                logLevel = LogDetailLevel.None,
                warningLevel = LogDetailLevel.None,
                errorLevel = LogDetailLevel.Compact,
                exceptionLevel = LogDetailLevel.Compact,
                assertLevel = LogDetailLevel.None,
                logToFile = false
            };
        }

        /// <summary>
        /// Создать настройки для Debug билда
        /// </summary>
        public static BuildLogSettings CreateDebug()
        {
            return new BuildLogSettings
            {
                logLevel = LogDetailLevel.Full,
                warningLevel = LogDetailLevel.Full,
                errorLevel = LogDetailLevel.Full,
                exceptionLevel = LogDetailLevel.Full,
                assertLevel = LogDetailLevel.Full,
                logToFile = true,
                clearLogOnBuild = true
            };
        }
    }
}
