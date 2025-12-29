// Packages/com.protosystem.core/Runtime/Settings/Persistence/ISettingsPersistence.cs
using System.Collections.Generic;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Режим хранения настроек
    /// </summary>
    public enum PersistenceMode
    {
        /// <summary>Автовыбор: WebGL/Mobile → PlayerPrefs, Desktop → INI файл</summary>
        Auto,
        /// <summary>Всегда PlayerPrefs</summary>
        PlayerPrefs,
        /// <summary>Всегда INI файл</summary>
        File
    }

    /// <summary>
    /// Интерфейс для сохранения/загрузки настроек
    /// </summary>
    public interface ISettingsPersistence
    {
        /// <summary>
        /// Проверить существование сохранённых настроек
        /// </summary>
        bool Exists();

        /// <summary>
        /// Загрузить настройки
        /// </summary>
        /// <returns>Словарь секция -> (ключ -> значение)</returns>
        Dictionary<string, Dictionary<string, string>> Load();

        /// <summary>
        /// Сохранить настройки
        /// </summary>
        /// <param name="sections">Список секций для сохранения</param>
        void Save(IEnumerable<SettingsSection> sections);

        /// <summary>
        /// Удалить сохранённые настройки
        /// </summary>
        void Delete();

        /// <summary>
        /// Получить путь к файлу настроек (если применимо)
        /// </summary>
        string GetPath();
    }
}
