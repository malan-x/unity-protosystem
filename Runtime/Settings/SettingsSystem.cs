// Packages/com.protosystem.core/Runtime/Settings/SettingsSystem.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Главная система управления настройками
    /// </summary>
    [ProtoSystemComponent("Settings System", "Настройки игры с персистентностью (INI/PlayerPrefs)", "Core", "⚙️", 5)]
    public class SettingsSystem : InitializableSystemBase
    {
        public override string SystemId => "SettingsSystem";
        public override string DisplayName => "Settings System";

        [Header("Configuration")]
        [SerializeField] private SettingsConfig config;

        /// <summary>Аудио настройки</summary>
        public AudioSettings Audio { get; private set; }

        /// <summary>Видео настройки</summary>
        public VideoSettings Video { get; private set; }

        /// <summary>Настройки управления</summary>
        public ControlsSettings Controls { get; private set; }

        /// <summary>Игровые настройки</summary>
        public GameplaySettings Gameplay { get; private set; }

        /// <summary>Кастомные секции</summary>
        private readonly Dictionary<string, SettingsSection> _customSections = new Dictionary<string, SettingsSection>();

        /// <summary>Все секции</summary>
        private readonly List<SettingsSection> _allSections = new List<SettingsSection>();

        /// <summary>Хранилище настроек</summary>
        private ISettingsPersistence _persistence;

        /// <summary>Мигратор версий</summary>
        private SettingsMigrator _migrator;

        /// <summary>Событие изменения настроек</summary>
        public event Action OnSettingsChanged;

        #region Singleton Access
        
        private static SettingsSystem _instance;
        
        /// <summary>
        /// Статический доступ к системе (опционально)
        /// </summary>
        public static SettingsSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<SettingsSystem>();
                }
                return _instance;
            }
        }

        #endregion

        #region Initialization

        protected override void Awake()
        {
            base.Awake();
            _instance = this;
        }

        protected override void InitEvents()
        {
            // Подписываемся на событие модификации для отслеживания изменений
            AddEvent(EventBus.Settings.Modified, OnSettingModified);
        }

        public override async Task<bool> InitializeAsync()
        {
            try
            {
                LogMessage("Initializing Settings System...");

                // Создаём конфиг если не задан
                if (config == null)
                {
                    config = SettingsConfig.CreateDefault();
                    LogWarning("SettingsConfig not assigned, using defaults");
                }

                // Инициализируем секции
                InitializeSections();

                // Создаём хранилище и мигратор
                _persistence = PersistenceFactory.Create(
                    config.persistenceMode,
                    config.fileName,
                    SettingsMigrator.CURRENT_VERSION
                );
                _migrator = new SettingsMigrator();

                // Загружаем настройки
                Load();

                // Применяем загруженные настройки
                ApplyAll();

                LogMessage("Settings System initialized successfully");
                EventBus.Publish(EventBus.Settings.Loaded, null);

                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize Settings System: {ex.Message}");
                return false;
            }
        }

        private void InitializeSections()
        {
            // Создаём стандартные секции
            Audio = new AudioSettings();
            Video = new VideoSettings();
            Controls = new ControlsSettings();
            Gameplay = new GameplaySettings();

            // Применяем дефолты из конфига
            Audio.SetDefaults(config.masterVolume, config.musicVolume, config.sfxVolume, config.voiceVolume);
            Video.SetDefaults(config.fullscreen, config.vSync, config.targetFrameRate, config.qualityLevel);
            Controls.SetDefaults(config.sensitivity, config.invertY, config.invertX);
            Gameplay.SetDefaults(config.language, config.subtitles);

            // Добавляем в общий список
            _allSections.Add(Audio);
            _allSections.Add(Video);
            _allSections.Add(Controls);
            _allSections.Add(Gameplay);

            // Создаём кастомные секции из конфига
            foreach (var customConfig in config.customSections)
            {
                var section = CreateCustomSection(customConfig);
                _customSections[customConfig.sectionName] = section;
                _allSections.Add(section);
            }
        }

        private DynamicSettingsSection CreateCustomSection(CustomSectionConfig config)
        {
            var section = new DynamicSettingsSection(config.sectionName, config.comment);

            foreach (var setting in config.settings)
            {
                switch (setting.type)
                {
                    case SettingType.String:
                        section.AddString(setting.key, setting.comment, setting.eventId, setting.defaultValue);
                        break;
                    case SettingType.Int:
                        section.AddInt(setting.key, setting.comment, setting.eventId, 
                            int.TryParse(setting.defaultValue, out int intVal) ? intVal : 0);
                        break;
                    case SettingType.Float:
                        section.AddFloat(setting.key, setting.comment, setting.eventId,
                            float.TryParse(setting.defaultValue, out float floatVal) ? floatVal : 0f);
                        break;
                    case SettingType.Bool:
                        section.AddBool(setting.key, setting.comment, setting.eventId,
                            setting.defaultValue == "1" || setting.defaultValue.ToLower() == "true");
                        break;
                }
            }

            return section;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Загрузить настройки из хранилища
        /// </summary>
        public void Load()
        {
            var data = _persistence.Load();

            if (data.Count > 0)
            {
                // Проверяем версию и мигрируем если нужно
                int version = _migrator.ExtractVersion(data);
                if (version < SettingsMigrator.CURRENT_VERSION)
                {
                    data = _migrator.Migrate(data, version);
                }

                // Десериализуем в секции
                foreach (var section in _allSections)
                {
                    if (data.TryGetValue(section.SectionName, out var sectionData))
                    {
                        section.Deserialize(sectionData);
                    }
                }
            }

            // Помечаем всё как сохранённое
            foreach (var section in _allSections)
            {
                section.MarkAllSaved();
            }

            LogMessage($"Settings loaded from: {_persistence.GetPath()}");
        }

        /// <summary>
        /// Сохранить настройки в хранилище
        /// </summary>
        public void Save()
        {
            _persistence.Save(_allSections);

            // Помечаем всё как сохранённое
            foreach (var section in _allSections)
            {
                section.MarkAllSaved();
            }

            EventBus.Publish(EventBus.Settings.Saved, null);
            LogMessage("Settings saved");
        }

        /// <summary>
        /// Применить все изменённые настройки
        /// </summary>
        public void ApplyAll()
        {
            foreach (var section in _allSections)
            {
                section.Apply();
            }

            EventBus.Publish(EventBus.Settings.Applied, null);
            LogMessage("All settings applied");
        }

        /// <summary>
        /// Применить настройки конкретной секции
        /// </summary>
        public void Apply(string sectionName)
        {
            var section = GetSection(sectionName);
            section?.Apply();
        }

        /// <summary>
        /// Применить и сохранить все настройки
        /// </summary>
        public void ApplyAndSave()
        {
            ApplyAll();
            Save();
        }

        /// <summary>
        /// Откатить все изменения
        /// </summary>
        public void RevertAll()
        {
            foreach (var section in _allSections)
            {
                section.Revert();
            }

            EventBus.Publish(EventBus.Settings.Reverted, null);
            LogMessage("All settings reverted");
        }

        /// <summary>
        /// Откатить изменения секции
        /// </summary>
        public void Revert(string sectionName)
        {
            var section = GetSection(sectionName);
            section?.Revert();
        }

        /// <summary>
        /// Сбросить все настройки к значениям по умолчанию
        /// </summary>
        public void ResetAllToDefaults()
        {
            foreach (var section in _allSections)
            {
                section.ResetToDefaults();
            }

            EventBus.Publish(EventBus.Settings.ResetToDefaults, null);
            LogMessage("All settings reset to defaults");
        }

        /// <summary>
        /// Сбросить секцию к значениям по умолчанию
        /// </summary>
        public void ResetToDefaults(string sectionName)
        {
            var section = GetSection(sectionName);
            section?.ResetToDefaults();
        }

        /// <summary>
        /// Проверить наличие несохранённых изменений
        /// </summary>
        public bool HasUnsavedChanges()
        {
            return _allSections.Any(s => s.HasUnsavedChanges());
        }

        /// <summary>
        /// Проверить наличие несохранённых изменений в секции
        /// </summary>
        public bool HasUnsavedChanges(string sectionName)
        {
            var section = GetSection(sectionName);
            return section?.HasUnsavedChanges() ?? false;
        }

        /// <summary>
        /// Получить секцию по имени
        /// </summary>
        public SettingsSection GetSection(string sectionName)
        {
            return _allSections.FirstOrDefault(s => s.SectionName == sectionName);
        }

        /// <summary>
        /// Получить кастомную секцию по имени
        /// </summary>
        public T GetCustomSection<T>(string sectionName) where T : SettingsSection
        {
            return GetSection(sectionName) as T;
        }

        /// <summary>
        /// Зарегистрировать кастомную секцию
        /// </summary>
        public void RegisterSection(SettingsSection section)
        {
            if (_customSections.ContainsKey(section.SectionName))
            {
                LogWarning($"Section '{section.SectionName}' already registered, replacing");
                _allSections.RemoveAll(s => s.SectionName == section.SectionName);
            }

            _customSections[section.SectionName] = section;
            _allSections.Add(section);

            // Загружаем данные для новой секции
            var data = _persistence.Load();
            if (data.TryGetValue(section.SectionName, out var sectionData))
            {
                section.Deserialize(sectionData);
                section.MarkAllSaved();
            }

            LogMessage($"Registered custom section: {section.SectionName}");
        }

        /// <summary>
        /// Получить все секции
        /// </summary>
        public IReadOnlyList<SettingsSection> GetAllSections() => _allSections.AsReadOnly();

        /// <summary>
        /// Получить путь к файлу настроек
        /// </summary>
        public string GetSettingsPath() => _persistence.GetPath();

        #endregion

        #region Event Handlers

        private void OnSettingModified(object data)
        {
            OnSettingsChanged?.Invoke();
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #endregion
    }
}
