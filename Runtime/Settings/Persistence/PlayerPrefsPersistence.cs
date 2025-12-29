// Packages/com.protosystem.core/Runtime/Settings/Persistence/PlayerPrefsPersistence.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Сохранение настроек в PlayerPrefs (для WebGL и мобильных платформ)
    /// </summary>
    public class PlayerPrefsPersistence : ISettingsPersistence
    {
        private const string PREFIX = "ProtoSettings_";
        private const string SECTIONS_KEY = PREFIX + "Sections";
        private const string VERSION_KEY = PREFIX + "Version";

        private readonly int _version;

        public PlayerPrefsPersistence(int version = 1)
        {
            _version = version;
        }

        public string GetPath()
        {
            return "PlayerPrefs";
        }

        public bool Exists()
        {
            return PlayerPrefs.HasKey(SECTIONS_KEY);
        }

        public Dictionary<string, Dictionary<string, string>> Load()
        {
            var result = new Dictionary<string, Dictionary<string, string>>();

            if (!Exists())
            {
                Debug.Log("[PlayerPrefsPersistence] No saved settings found in PlayerPrefs");
                return result;
            }

            try
            {
                // Получаем список секций
                string sectionsJson = PlayerPrefs.GetString(SECTIONS_KEY, "");
                if (string.IsNullOrEmpty(sectionsJson))
                    return result;

                string[] sectionNames = JsonUtility.FromJson<StringArray>(sectionsJson).items;

                foreach (string sectionName in sectionNames)
                {
                    result[sectionName] = new Dictionary<string, string>();

                    // Получаем список ключей секции
                    string keysJson = PlayerPrefs.GetString(GetSectionKeysKey(sectionName), "");
                    if (string.IsNullOrEmpty(keysJson))
                        continue;

                    string[] keys = JsonUtility.FromJson<StringArray>(keysJson).items;

                    foreach (string key in keys)
                    {
                        string value = PlayerPrefs.GetString(GetSettingKey(sectionName, key), "");
                        result[sectionName][key] = value;
                    }
                }

                Debug.Log($"[PlayerPrefsPersistence] Loaded settings from PlayerPrefs");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerPrefsPersistence] Failed to load settings: {ex.Message}");
            }

            return result;
        }

        public void Save(IEnumerable<SettingsSection> sections)
        {
            try
            {
                var sectionNames = new List<string>();

                foreach (var section in sections)
                {
                    sectionNames.Add(section.SectionName);
                    var keyNames = new List<string>();

                    foreach (var setting in section.GetAllSettings())
                    {
                        keyNames.Add(setting.Key);
                        PlayerPrefs.SetString(GetSettingKey(section.SectionName, setting.Key), setting.Serialize());
                    }

                    // Сохраняем список ключей секции
                    PlayerPrefs.SetString(
                        GetSectionKeysKey(section.SectionName),
                        JsonUtility.ToJson(new StringArray { items = keyNames.ToArray() })
                    );
                }

                // Сохраняем список секций
                PlayerPrefs.SetString(SECTIONS_KEY, JsonUtility.ToJson(new StringArray { items = sectionNames.ToArray() }));
                PlayerPrefs.SetInt(VERSION_KEY, _version);

                PlayerPrefs.Save();
                Debug.Log("[PlayerPrefsPersistence] Settings saved to PlayerPrefs");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerPrefsPersistence] Failed to save settings: {ex.Message}");
            }
        }

        public void Delete()
        {
            try
            {
                if (!Exists())
                    return;

                // Получаем список секций для удаления
                string sectionsJson = PlayerPrefs.GetString(SECTIONS_KEY, "");
                if (!string.IsNullOrEmpty(sectionsJson))
                {
                    string[] sectionNames = JsonUtility.FromJson<StringArray>(sectionsJson).items;

                    foreach (string sectionName in sectionNames)
                    {
                        // Получаем и удаляем ключи секции
                        string keysJson = PlayerPrefs.GetString(GetSectionKeysKey(sectionName), "");
                        if (!string.IsNullOrEmpty(keysJson))
                        {
                            string[] keys = JsonUtility.FromJson<StringArray>(keysJson).items;
                            foreach (string key in keys)
                            {
                                PlayerPrefs.DeleteKey(GetSettingKey(sectionName, key));
                            }
                        }
                        PlayerPrefs.DeleteKey(GetSectionKeysKey(sectionName));
                    }
                }

                PlayerPrefs.DeleteKey(SECTIONS_KEY);
                PlayerPrefs.DeleteKey(VERSION_KEY);
                PlayerPrefs.Save();

                Debug.Log("[PlayerPrefsPersistence] Deleted settings from PlayerPrefs");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerPrefsPersistence] Failed to delete settings: {ex.Message}");
            }
        }

        private string GetSectionKeysKey(string sectionName) => $"{PREFIX}{sectionName}_Keys";
        private string GetSettingKey(string sectionName, string key) => $"{PREFIX}{sectionName}_{key}";

        /// <summary>
        /// Вспомогательный класс для сериализации массива строк в JSON
        /// </summary>
        [Serializable]
        private class StringArray
        {
            public string[] items;
        }
    }
}
