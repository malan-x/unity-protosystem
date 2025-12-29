# SettingsSystem — Документация

## Обзор

`SettingsSystem` — модуль ProtoSystem для управления настройками игры с поддержкой:
- Сохранения в INI-файлы (Desktop) или PlayerPrefs (WebGL/Mobile)
- Автогенерации комментариев в INI
- Отслеживания изменений (dirty state)
- Событий через EventBus
- Миграции версий
- Кастомных секций

---

## Быстрый старт

### 1. Добавить SettingsSystem на сцену

```
GameObject → Create Empty → Add Component → SettingsSystem
```

### 2. Создать SettingsConfig

```
Assets → Create → ProtoSystem → Settings → Settings Config
```

### 3. Базовое использование

```csharp
// Получить систему
var settings = SettingsSystem.Instance;

// Чтение
float volume = settings.Audio.MasterVolume.Value;

// Изменение (публикует событие, не сохраняет)
settings.Audio.MasterVolume.Value = 0.5f;

// Проверка изменений
if (settings.HasUnsavedChanges())
{
    settings.ApplyAndSave();
}

// Откат изменений
settings.RevertAll();

// Сброс к дефолтам
settings.ResetAllToDefaults();
```

---

## Архитектура

```
SettingsSystem (MonoBehaviour)
├── Audio: AudioSettings
│   ├── MasterVolume: SettingValue<float>
│   ├── MusicVolume: SettingValue<float>
│   ├── SFXVolume: SettingValue<float>
│   ├── VoiceVolume: SettingValue<float>
│   └── Mute: SettingValue<bool>
├── Video: VideoSettings
│   ├── Monitor: SettingValue<int>
│   ├── Resolution: SettingValue<string>
│   ├── Fullscreen: SettingValue<string>
│   ├── VSync: SettingValue<bool>
│   ├── Quality: SettingValue<int>
│   └── TargetFrameRate: SettingValue<int>
├── Controls: ControlsSettings
│   ├── Sensitivity: SettingValue<float>
│   ├── InvertY: SettingValue<bool>
│   └── InvertX: SettingValue<bool>
├── Gameplay: GameplaySettings
│   ├── Language: SettingValue<string>
│   └── Subtitles: SettingValue<bool>
└── CustomSections: Dictionary<string, SettingsSection>
```

---

## SettingValue<T>

Обёртка значения с отслеживанием состояния:

```csharp
public class SettingValue<T>
{
    public T Value { get; set; }      // Текущее значение
    public T SavedValue { get; }      // Последнее сохранённое
    public T DefaultValue { get; }    // Значение по умолчанию
    public bool IsModified { get; }   // Есть несохранённые изменения
    
    public void MarkSaved();          // Пометить как сохранённое
    public void Revert();             // Откатить к SavedValue
    public void ResetToDefault();     // Сбросить к DefaultValue
    
    public string Serialize();        // Для INI: "0.5", "1", "text"
    public void Deserialize(string);  // Парсинг из INI
}
```

### Формат сериализации

| Тип | Формат | Пример |
|-----|--------|--------|
| `bool` | `0` / `1` | `Mute=0` |
| `int` | целое | `Quality=3` |
| `float` | `0.###` | `MasterVolume=0.75` |
| `string` | как есть | `Language=ru` |

---

## События EventBus

### ID событий (10100+)

```csharp
EventBus.Settings.Loaded         // 10100 - Настройки загружены
EventBus.Settings.Saved          // 10101 - Настройки сохранены
EventBus.Settings.Applied        // 10102 - Настройки применены
EventBus.Settings.Reverted       // 10103 - Изменения отменены
EventBus.Settings.ResetToDefaults // 10104 - Сброс к дефолтам
EventBus.Settings.Modified       // 10105 - Появились изменения

// Audio (10110-10114)
EventBus.Settings.Audio.MasterChanged
EventBus.Settings.Audio.MusicChanged
EventBus.Settings.Audio.SFXChanged
EventBus.Settings.Audio.VoiceChanged
EventBus.Settings.Audio.MuteChanged

// Video (10120-10125)
EventBus.Settings.Video.MonitorChanged
EventBus.Settings.Video.ResolutionChanged
EventBus.Settings.Video.FullscreenChanged
EventBus.Settings.Video.VSyncChanged
EventBus.Settings.Video.QualityChanged
EventBus.Settings.Video.FrameRateChanged

// Controls (10130-10132)
EventBus.Settings.Controls.SensitivityChanged
EventBus.Settings.Controls.InvertYChanged
EventBus.Settings.Controls.InvertXChanged

// Gameplay (10140-10141)
EventBus.Settings.Gameplay.LanguageChanged
EventBus.Settings.Gameplay.SubtitlesChanged
```

### Подписка на события

```csharp
// В InitEvents() системы
AddEvent(EventBus.Settings.Audio.MasterChanged, OnMasterVolumeChanged);

private void OnMasterVolumeChanged(object data)
{
    var changed = (SettingChangedData<float>)data;
    Debug.Log($"Volume: {changed.PreviousValue} → {changed.Value}");
    
    // Применить к AudioMixer
    audioMixer.SetFloat("MasterVolume", Mathf.Log10(changed.Value) * 20);
}
```

---

## INI-файл

### Расположение

- **Desktop**: `Application.persistentDataPath/settings.ini`
- **WebGL/Mobile**: PlayerPrefs

### Формат

```ini
; ProtoSystem Settings
; Generated: 2025-12-26 12:00:00
; Version: 1

; === Audio volume settings (0.0 - 1.0) ===
[Audio]
; Master volume (0.0 - 1.0)
MasterVolume=0.8
; Music volume (0.0 - 1.0)
MusicVolume=0.6
; Sound effects volume (0.0 - 1.0)
SFXVolume=1
; Voice/dialogue volume (0.0 - 1.0)
VoiceVolume=1
; Mute all audio (0/1)
Mute=0

; === Display and graphics settings ===
[Video]
; Monitor index (0 = primary)
Monitor=0
; Screen resolution (WIDTHxHEIGHT)
Resolution=1920x1080
; Window mode: ExclusiveFullScreen, FullScreenWindow, Windowed
Fullscreen=FullScreenWindow
; Vertical synchronization (0/1)
VSync=1
; Quality level (0-5)
Quality=3
; Target FPS (-1 = unlimited)
TargetFrameRate=-1
```

---

## VideoSettings — автоприменение

`VideoSettings.Apply()` автоматически вызывает Unity API:

```csharp
// Разрешение + режим окна
Screen.SetResolution(width, height, fullscreenMode);

// VSync
QualitySettings.vSyncCount = vsync ? 1 : 0;

// Качество
QualitySettings.SetQualityLevel(quality, true);

// FPS
Application.targetFrameRate = fps;
```

### Получение доступных опций

```csharp
string[] resolutions = VideoSettings.GetAvailableResolutions();
// ["2560x1440", "1920x1080", "1280x720", ...]

string[] modes = VideoSettings.GetFullscreenModes();
// ["FullScreenWindow", "ExclusiveFullScreen", "Windowed"]

string[] quality = VideoSettings.GetQualityLevels();
// ["Very Low", "Low", "Medium", "High", "Very High", "Ultra"]
```

---

## Кастомные секции

### Через SettingsConfig (Inspector)

1. В `SettingsConfig` → Custom Sections → Add
2. Указать `sectionName`, `comment`
3. Добавить настройки с типами

### Программно

```csharp
public class MyGameSettings : CustomSettingsSection
{
    public override string SectionName => "MyGame";
    public override string SectionComment => "Game-specific settings";
    
    public SettingValue<int> Difficulty { get; }
    public SettingValue<bool> Permadeath { get; }
    
    public MyGameSettings()
    {
        Difficulty = new SettingValue<int>(
            "Difficulty", SectionName,
            "0=Easy, 1=Normal, 2=Hard",
            12000, // Кастомный EventId
            1      // Default
        );
        
        Permadeath = new SettingValue<bool>(
            "Permadeath", SectionName,
            "Enable permadeath mode",
            12001,
            false
        );
    }
}

// Регистрация
SettingsSystem.Instance.RegisterSection(new MyGameSettings());

// Использование
var mySettings = SettingsSystem.Instance.GetCustomSection<MyGameSettings>("MyGame");
int difficulty = mySettings.Difficulty.Value;
```

---

## Миграция версий

При изменении схемы настроек:

```csharp
// В SettingsMigrator.cs
public const int CURRENT_VERSION = 2; // Увеличить

public SettingsMigrator()
{
    RegisterMigration(2, MigrateV1ToV2);
}

private Dictionary<...> MigrateV1ToV2(Dictionary<...> data)
{
    // Переименование ключа
    if (data.TryGetValue("Audio", out var audio))
    {
        if (audio.TryGetValue("Volume", out var val))
        {
            audio["MasterVolume"] = val;
            audio.Remove("Volume");
        }
    }
    
    // Добавление нового ключа со значением по умолчанию
    if (data.TryGetValue("Video", out var video))
    {
        if (!video.ContainsKey("HDR"))
            video["HDR"] = "0";
    }
    
    return data;
}
```

---

## API Reference

### SettingsSystem

| Метод | Описание |
|-------|----------|
| `Load()` | Загрузить из файла |
| `Save()` | Сохранить в файл |
| `ApplyAll()` | Применить все настройки |
| `Apply(section)` | Применить секцию |
| `ApplyAndSave()` | Применить + сохранить |
| `RevertAll()` | Откатить все изменения |
| `Revert(section)` | Откатить секцию |
| `ResetAllToDefaults()` | Сбросить всё к дефолтам |
| `ResetToDefaults(section)` | Сбросить секцию |
| `HasUnsavedChanges()` | Есть ли несохранённые изменения |
| `GetSection(name)` | Получить секцию по имени |
| `RegisterSection(section)` | Зарегистрировать кастомную секцию |
| `GetSettingsPath()` | Путь к файлу настроек |

### SettingsConfig (ScriptableObject)

| Поле | Описание |
|------|----------|
| `persistenceMode` | Auto / PlayerPrefs / File |
| `fileName` | Имя INI файла |
| `masterVolume` и др. | Значения по умолчанию |
| `customSections` | Кастомные секции |

---

## Интеграция с UI

```csharp
// Slider для громкости
volumeSlider.value = settings.Audio.MasterVolume.Value;
volumeSlider.onValueChanged.AddListener(v => {
    settings.Audio.MasterVolume.Value = v;
});

// Dropdown для разрешения
var resolutions = VideoSettings.GetAvailableResolutions();
resolutionDropdown.options = resolutions.Select(r => new TMP_Dropdown.OptionData(r)).ToList();
resolutionDropdown.value = Array.IndexOf(resolutions, settings.Video.Resolution.Value);
resolutionDropdown.onValueChanged.AddListener(i => {
    settings.Video.Resolution.Value = resolutions[i];
});

// Кнопки Apply/Cancel
applyButton.onClick.AddListener(() => settings.ApplyAndSave());
cancelButton.onClick.AddListener(() => settings.RevertAll());
resetButton.onClick.AddListener(() => {
    settings.ResetAllToDefaults();
    RefreshUI(); // Обновить UI
});
```

---

## Файловая структура

```
Runtime/Settings/
├── SettingsSystem.cs           # Главная система
├── SettingsConfig.cs           # ScriptableObject конфиг
├── SettingsEvents.cs           # EventBus события
├── SettingsMigrator.cs         # Миграция версий
├── Data/
│   ├── SettingValue.cs         # Обёртка значения
│   ├── SettingsSection.cs      # Базовый класс секции
│   ├── AudioSettings.cs        # [Audio]
│   ├── VideoSettings.cs        # [Video] + автоприменение
│   ├── ControlsSettings.cs     # [Controls]
│   ├── GameplaySettings.cs     # [Gameplay]
│   └── CustomSettingsSection.cs # Для кастомных секций
└── Persistence/
    ├── ISettingsPersistence.cs # Интерфейс
    ├── IniPersistence.cs       # Desktop (INI файл)
    ├── PlayerPrefsPersistence.cs # WebGL/Mobile
    └── PersistenceFactory.cs   # Автовыбор
```
