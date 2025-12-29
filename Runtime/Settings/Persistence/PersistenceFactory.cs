// Packages/com.protosystem.core/Runtime/Settings/Persistence/PersistenceFactory.cs
using UnityEngine;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Фабрика для создания хранилища настроек в зависимости от платформы
    /// </summary>
    public static class PersistenceFactory
    {
        /// <summary>
        /// Создать хранилище настроек
        /// </summary>
        /// <param name="mode">Режим хранения</param>
        /// <param name="fileName">Имя INI файла (для File режима)</param>
        /// <param name="version">Версия схемы настроек</param>
        public static ISettingsPersistence Create(PersistenceMode mode, string fileName = "settings.ini", int version = 1)
        {
            if (mode == PersistenceMode.Auto)
            {
                mode = GetDefaultModeForPlatform();
            }

            return mode switch
            {
                PersistenceMode.PlayerPrefs => new PlayerPrefsPersistence(version),
                PersistenceMode.File => new IniPersistence(fileName, version),
                _ => new IniPersistence(fileName, version)
            };
        }

        /// <summary>
        /// Определить режим по умолчанию для текущей платформы
        /// </summary>
        private static PersistenceMode GetDefaultModeForPlatform()
        {
#if UNITY_WEBGL
            // WebGL не поддерживает файловую систему
            return PersistenceMode.PlayerPrefs;
#elif UNITY_IOS || UNITY_ANDROID
            // Мобильные платформы - PlayerPrefs проще и надёжнее
            return PersistenceMode.PlayerPrefs;
#else
            // Desktop платформы - INI файл читабельнее для пользователей
            return PersistenceMode.File;
#endif
        }

        /// <summary>
        /// Проверить, поддерживается ли файловое хранение на текущей платформе
        /// </summary>
        public static bool IsFileStorageSupported()
        {
#if UNITY_WEBGL
            return false;
#else
            return true;
#endif
        }
    }
}
