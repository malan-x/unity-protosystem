// Packages/com.protosystem.core/Runtime/Settings/Persistence/IniPersistence.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Сохранение настроек в INI файл
    /// </summary>
    public class IniPersistence : ISettingsPersistence
    {
        private readonly string _fileName;
        private readonly int _version;
        private string _cachedPath;

        public IniPersistence(string fileName = "settings.ini", int version = 1)
        {
            _fileName = fileName;
            _version = version;
        }

        public string GetPath()
        {
            if (_cachedPath == null)
            {
                _cachedPath = Path.Combine(Application.persistentDataPath, _fileName);
            }
            return _cachedPath;
        }

        public bool Exists()
        {
            return File.Exists(GetPath());
        }

        public Dictionary<string, Dictionary<string, string>> Load()
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            string path = GetPath();

            if (!File.Exists(path))
            {
                Debug.Log($"[IniPersistence] Settings file not found: {path}");
                return result;
            }

            try
            {
                string currentSection = "";
                foreach (string line in File.ReadAllLines(path))
                {
                    string trimmedLine = line.Trim();

                    // Пропускаем пустые строки и комментарии
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";"))
                        continue;

                    // Заголовок секции [SectionName]
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        if (!result.ContainsKey(currentSection))
                        {
                            result[currentSection] = new Dictionary<string, string>();
                        }
                    }
                    else if (!string.IsNullOrEmpty(currentSection))
                    {
                        // Парсинг ключ=значение
                        int equalsIndex = trimmedLine.IndexOf('=');
                        if (equalsIndex > 0)
                        {
                            string key = trimmedLine.Substring(0, equalsIndex).Trim();
                            string value = trimmedLine.Substring(equalsIndex + 1).Trim();
                            result[currentSection][key] = value;
                        }
                    }
                }

                Debug.Log($"[IniPersistence] Loaded settings from: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IniPersistence] Failed to load settings: {ex.Message}");
            }

            return result;
        }

        public void Save(IEnumerable<SettingsSection> sections)
        {
            string path = GetPath();

            try
            {
                // Создаём директорию если нужно
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var writer = new StreamWriter(path))
                {
                    // Заголовок файла
                    writer.WriteLine("; ProtoSystem Settings");
                    writer.WriteLine($"; Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"; Version: {_version}");
                    writer.WriteLine();

                    foreach (var section in sections)
                    {
                        WriteSection(writer, section);
                    }
                }

                Debug.Log($"[IniPersistence] Settings saved to: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IniPersistence] Failed to save settings: {ex.Message}");
            }
        }

        private void WriteSection(StreamWriter writer, SettingsSection section)
        {
            // Комментарий секции
            if (!string.IsNullOrEmpty(section.SectionComment))
            {
                writer.WriteLine($"; === {section.SectionComment} ===");
            }

            // Заголовок секции
            writer.WriteLine($"[{section.SectionName}]");

            // Получаем комментарии для настроек
            var comments = section.GetComments();

            // Записываем настройки
            foreach (var setting in section.GetAllSettings().OrderBy(s => s.Key))
            {
                // Комментарий настройки
                if (comments.TryGetValue(setting.Key, out string comment))
                {
                    writer.WriteLine($"; {comment}");
                }

                // Значение настройки
                writer.WriteLine($"{setting.Key}={setting.Serialize()}");
            }

            writer.WriteLine();
        }

        public void Delete()
        {
            string path = GetPath();
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    Debug.Log($"[IniPersistence] Deleted settings file: {path}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[IniPersistence] Failed to delete settings: {ex.Message}");
                }
            }
        }
    }
}
