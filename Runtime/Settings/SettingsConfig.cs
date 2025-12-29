// Packages/com.protosystem.core/Runtime/Settings/SettingsConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Конфигурация системы настроек (ScriptableObject)
    /// </summary>
    [CreateAssetMenu(fileName = "SettingsConfig", menuName = "ProtoSystem/Settings/Settings Config")]
    public class SettingsConfig : ScriptableObject
    {
        [Header("Persistence")]
        [Tooltip("Способ хранения настроек")]
        public PersistenceMode persistenceMode = PersistenceMode.Auto;

        [Tooltip("Имя файла настроек (для File режима)")]
        public string fileName = "settings.ini";

        [Header("Audio Defaults")]
        [Range(0f, 1f)]
        [Tooltip("Общая громкость по умолчанию")]
        public float masterVolume = 1f;

        [Range(0f, 1f)]
        [Tooltip("Громкость музыки по умолчанию")]
        public float musicVolume = 0.8f;

        [Range(0f, 1f)]
        [Tooltip("Громкость звуковых эффектов по умолчанию")]
        public float sfxVolume = 1f;

        [Range(0f, 1f)]
        [Tooltip("Громкость голоса/диалогов по умолчанию")]
        public float voiceVolume = 1f;

        [Header("Video Defaults")]
        [Tooltip("Режим окна по умолчанию")]
        public FullScreenMode fullscreen = FullScreenMode.FullScreenWindow;

        [Tooltip("Вертикальная синхронизация по умолчанию")]
        public bool vSync = true;

        [Tooltip("Целевой FPS (-1 = без ограничения)")]
        public int targetFrameRate = -1;

        [Tooltip("Уровень качества (-1 = автоопределение)")]
        public int qualityLevel = -1;

        [Header("Controls Defaults")]
        [Range(0.1f, 3f)]
        [Tooltip("Чувствительность мыши/камеры")]
        public float sensitivity = 1f;

        [Tooltip("Инвертировать ось Y")]
        public bool invertY = false;

        [Tooltip("Инвертировать ось X")]
        public bool invertX = false;

        [Header("Gameplay Defaults")]
        [Tooltip("Язык интерфейса ('auto' = системный)")]
        public string language = "auto";

        [Tooltip("Показывать субтитры")]
        public bool subtitles = true;

        [Header("Custom Sections")]
        [Tooltip("Дополнительные секции настроек")]
        public List<CustomSectionConfig> customSections = new List<CustomSectionConfig>();

        /// <summary>
        /// Создать конфиг с настройками по умолчанию
        /// </summary>
        public static SettingsConfig CreateDefault()
        {
            return CreateInstance<SettingsConfig>();
        }
    }

    /// <summary>
    /// Конфигурация кастомной секции
    /// </summary>
    [Serializable]
    public class CustomSectionConfig
    {
        [Tooltip("Имя секции в INI файле")]
        public string sectionName;

        [Tooltip("Комментарий для секции")]
        public string comment;

        [Tooltip("Настройки секции")]
        public List<CustomSettingConfig> settings = new List<CustomSettingConfig>();
    }

    /// <summary>
    /// Конфигурация кастомной настройки
    /// </summary>
    [Serializable]
    public class CustomSettingConfig
    {
        [Tooltip("Ключ настройки")]
        public string key;

        [Tooltip("Комментарий")]
        public string comment;

        [Tooltip("Тип значения")]
        public SettingType type;

        [Tooltip("Значение по умолчанию")]
        public string defaultValue;

        [Tooltip("ID события при изменении (0 = не публиковать)")]
        public int eventId;
    }

    /// <summary>
    /// Тип значения настройки
    /// </summary>
    public enum SettingType
    {
        String,
        Int,
        Float,
        Bool
    }
}
