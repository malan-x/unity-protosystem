// Packages/com.protosystem.core/Runtime/Settings/SettingsMigrator.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Мигратор настроек между версиями
    /// </summary>
    public class SettingsMigrator
    {
        /// <summary>
        /// Текущая версия схемы настроек
        /// </summary>
        public const int CURRENT_VERSION = 1;

        /// <summary>
        /// Делегат миграции
        /// </summary>
        public delegate Dictionary<string, Dictionary<string, string>> MigrationFunc(
            Dictionary<string, Dictionary<string, string>> data);

        /// <summary>
        /// Зарегистрированные миграции (версия -> функция)
        /// </summary>
        private readonly Dictionary<int, MigrationFunc> _migrations = new Dictionary<int, MigrationFunc>();

        public SettingsMigrator()
        {
            // Регистрируем встроенные миграции
            // RegisterMigration(1, MigrateV0ToV1);
        }

        /// <summary>
        /// Зарегистрировать миграцию для версии
        /// </summary>
        /// <param name="toVersion">Целевая версия</param>
        /// <param name="migration">Функция миграции</param>
        public void RegisterMigration(int toVersion, MigrationFunc migration)
        {
            _migrations[toVersion] = migration;
        }

        /// <summary>
        /// Выполнить миграцию данных до текущей версии
        /// </summary>
        /// <param name="data">Данные настроек</param>
        /// <param name="fromVersion">Версия загруженных данных</param>
        /// <returns>Мигрированные данные</returns>
        public Dictionary<string, Dictionary<string, string>> Migrate(
            Dictionary<string, Dictionary<string, string>> data, 
            int fromVersion)
        {
            if (fromVersion >= CURRENT_VERSION)
                return data;

            ProtoLogger.Log("SettingsSystem", LogCategory.Runtime, LogLevel.Info, $"Migrating settings from v{fromVersion} to v{CURRENT_VERSION}");

            var result = data;
            for (int version = fromVersion + 1; version <= CURRENT_VERSION; version++)
            {
                if (_migrations.TryGetValue(version, out var migration))
                {
                    try
                    {
                        result = migration(result);
                        ProtoLogger.Log("SettingsSystem", LogCategory.Runtime, LogLevel.Info, $"Successfully migrated to v{version}");
                    }
                    catch (Exception ex)
                    {
                        ProtoLogger.Log("SettingsSystem", LogCategory.Runtime, LogLevel.Errors, $"Migration to v{version} failed: {ex.Message}");
                        // Продолжаем с текущими данными
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Извлечь версию из загруженных данных
        /// </summary>
        public int ExtractVersion(Dictionary<string, Dictionary<string, string>> data)
        {
            // Версия может храниться в специальной секции Meta
            if (data.TryGetValue("Meta", out var meta))
            {
                if (meta.TryGetValue("Version", out var versionStr))
                {
                    if (int.TryParse(versionStr, out int version))
                    {
                        return version;
                    }
                }
            }

            // Если версии нет - считаем версию 0 (начальная)
            return 0;
        }

        #region Example Migrations (закомментированы)

        /*
        /// <summary>
        /// Пример миграции: v0 -> v1
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> MigrateV0ToV1(
            Dictionary<string, Dictionary<string, string>> data)
        {
            // Пример: переименование ключа
            if (data.TryGetValue("Audio", out var audio))
            {
                // Старый ключ "Volume" -> новый "MasterVolume"
                if (audio.TryGetValue("Volume", out var value))
                {
                    audio["MasterVolume"] = value;
                    audio.Remove("Volume");
                }
            }

            // Пример: добавление новой настройки со значением по умолчанию
            if (data.TryGetValue("Video", out var video))
            {
                if (!video.ContainsKey("TargetFrameRate"))
                {
                    video["TargetFrameRate"] = "-1";
                }
            }

            return data;
        }
        */

        #endregion
    }
}
