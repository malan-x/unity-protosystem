# ProtoSystem Settings - Документация

## Обзор

**SettingsSystem** — система управления настройками игры для ProtoSystem. Обеспечивает:
- Хранение настроек в INI файле (Desktop) или PlayerPrefs (WebGL/Mobile)
- Автоматическое применение видео настроек (`Screen.SetResolution`, `QualitySettings`)
- Интеграцию с EventBus для реактивного обновления UI
- Поддержку кастомных секций настроек
- Миграцию между версиями схемы настроек

---

## Быстрый старт

### 1. Создание конфига

1. В Unity: **Assets → Create → ProtoSystem → Settings → Settings Config**
2. Настройте значения по умолчанию в инспекторе
3. Добавьте `SettingsSystem` на GameObject в сцене
4. Назначьте созданный конфиг в поле `Config`

### 2. Использование в коде

```csharp
using ProtoSystem.Settings;

// Получение системы
var settings = SettingsSystem.Instance;
// или через SystemProvider:
// var settings = SystemProvider.Get<SettingsSystem>();

// Чтение настроек
float masterVolume = settings.Audio.MasterVolume.Value;
string resolution = settings.Video.Resolution.Value;
bool subtitles = settings.Gameplay.Subtitles.Value;

// Изменение настроек (автоматически публикует событие)
settings.Audio.MasterVolume.Value = 0.5f;
settings.Video.VSync.Value = true;

// Применение видео настроек
settings.Video.Apply();

// Сохранение в файл
settings.Save();

// Или применить и сохранить сразу
settings.ApplyAndSave();
```

### 3. Подписка на события

```csharp
using ProtoSystem;

// Подписка на изменение громкости
EventBus.Subscribe(EventBus.Settings.Audio.MasterChanged, OnMasterVolumeChanged);

private void OnMasterVolumeChanged(object data)
{
    var changed = (SettingChangedData<float>)data;
    Debug.Log($"Master volume: {changed.PreviousValue} → {changed.Value}");
    
    // Применяем к AudioMixer
    audioMixer.SetFloat("MasterVolume", Mathf.Log10(changed.Value) * 20);
}
```

---

## Архитектура

### Структура файлов

```
Runtime/Settings/
├── SettingsSystem.cs           # Главный класс системы
├── SettingsConfig.cs           # ScriptableObject конфигурации
├── SettingsEvents.cs           # События для EventBus
├── SettingsMigrator.cs         # Миграция версий
├── Data/
│   ├── SettingValue.cs         # Обёртка значения с отслеживанием изменений
│   ├── SettingsSection.cs      # Базовый класс секции
│   ├── AudioSettings.cs        # Секция [Audio]
│   ├── VideoSettings.cs        # Секция [Video]
│   ├── ControlsSettings.cs     # Секция [Controls]
│   ├── GameplaySettings.cs     # Секция [Gameplay]
│   └── CustomSettingsSection.cs # Базовый класс для кастомных секций
└── Persistence/
    ├── ISettingsPersistence.cs  # Интерфейс хранилища
    ├── IniPersistence.cs        # Хранение в INI файле
    ├── PlayerPrefsPersistence.cs # Хранение в PlayerPrefs
    └── PersistenceFactory.cs    # Фабрика выбора хранилища
```

### Поток данных

```
┌─────────────────────────────────────────────────────────────┐
│                      SettingsSystem                          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────┐ │
│  │  Audio   │  │  Video   │  │ Controls │  │   Gameplay   │ │
│  │ Settings │  │ Settings │  │ Settings │  │   Settings   │ │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └──────┬───────┘ │
│       │             │             │               │          │
│       └─────────────┴──────┬──────┴───────────────┘          │
│                            │                                 │
│                    ┌───────▼───────┐                        │
│                    │ Persistence   │                        │
│                    │ (INI/Prefs)   │                        │
│                    └───────┬───────┘                        │
│                            │                                 │
└────────────────────────────┼─────────────────────────────────┘
                             │
                    ┌────────▼────────┐
                    │  settings.ini   │
                    │  (или PlayerPrefs)
                    └─────────────────┘
```

---

## Секции настроек

### Audio (Аудио)

| Ключ | Тип | По умолчанию | Событие |
|------|-----|--------------|---------|
| MasterVolume | float | 1.0 | `Settings.Audio.MasterChanged` |
| MusicVolume | float | 0.8 | `Settings.Audio.MusicChanged` |
| SFXVolume | float | 1.0 | `Settings.Audio.SFXChanged` |
| VoiceVolume | float | 1.0 | `Settings.Audio.VoiceChanged` |
| Mute | bool | false | `Settings.Audio.MuteChanged` |

### Video (Видео)

| Ключ | Тип | По умолчанию | Событие |
|------|-----|--------------|---------|
| Monitor | int | 0 | `Settings.Video.MonitorChanged` |
| Resolution | string | "1920x1080" | `Settings.Video.ResolutionChanged` |
| Fullscreen | string | "FullScreenWindow" | `Settings.Video.FullscreenChanged` |
| VSync | bool | true | `Settings.Video.VSyncChanged` |
| Quality | int | auto | `Settings.Video.QualityChanged` |
| TargetFrameRate | int | -1 | `Settings.Video.FrameRateChanged` |

**Fullscreen режимы:** `ExclusiveFullScreen`, `FullScreenWindow`, `Windowed`

### Controls (Управление)

| Ключ | Тип | По умолчанию | Событие |
|------|-----|--------------|---------|
| Sensitivity | float | 1.0 | `Settings.Controls.SensitivityChanged` |
| InvertY | bool | false | `Settings.Controls.InvertYChanged` |
| InvertX | bool | false | `Settings.Controls.InvertXChanged` |

### Gameplay (Игра)

| Ключ | Тип | По умолчанию | Событие |
|------|-----|--------------|---------|
| Language | string | "auto" | `Settings.Gameplay.LanguageChanged` |
| Subtitles | bool | true | `Settings.Gameplay.SubtitlesChanged` |

---

## Кастомные секции

### Способ 1: Через SettingsConfig (без кода)

1. Откройте SettingsConfig в инспекторе
2. В разделе **Custom Sections** нажмите **+**
3. Заполните:
   - **Section Name**: имя секции (например, "MyGame")
   - **Comment**: описание для INI файла
   - **Settings**: список настроек

### Способ 2: Через код

```csharp
// Создаём кастомную секцию
public class MyGameSettings : CustomSettingsSection
{
    public override string SectionName => "MyGame";
    public override string SectionComment => "My game specific settings";

    public SettingValue<int> Difficulty { get; }
    public SettingValue<bool> Permadeath { get; }
    public SettingValue<float> GameSpeed { get; }

    public MyGameSettings()
    {
        // eventId можно задать свой или 0 если событие не нужно
        Difficulty = new SettingValue<int>(
            "Difficulty", SectionName,
            "0=Easy, 1=Normal, 2=Hard",
            eventId: 20000,  // Ваш ID события
            defaultValue: 1
        );

        Permadeath = new SettingValue<bool>(
            "Permadeath", SectionName,
            "Enable permadeath mode",
            eventId: 20001,
            defaultValue: false
        );

        GameSpeed = new SettingValue<float>(
            "GameSpeed", SectionName,
            "Game speed multiplier (0.5 - 2.0)",
            eventId: 20002,
            defaultValue: 1.0f
        );
    }
}

// Регистрация при старте игры
void Start()
{
    var mySettings = new MyGameSettings();
    SettingsSystem.Instance.RegisterSection(mySettings);
}
```

---

## INI файл

Пример сгенерированного файла:

```ini
; ProtoSystem Settings
; Generated: 2024-01-15 14:30:00
; Version: 1

; === Audio volume settings (0.0 - 1.0) ===
[Audio]
; Master volume (0.0 - 1.0)
MasterVolume=1.0
; Mute all audio (0/1)
Mute=0
; Music volume (0.0 - 1.0)
MusicVolume=0.8
; Sound effects volume (0.0 - 1.0)
SFXVolume=1.0
; Voice/dialogue volume (0.0 - 1.0)
VoiceVolume=1.0

; === Display and graphics settings ===
[Video]
; Window mode: ExclusiveFullScreen, FullScreenWindow, Windowed
Fullscreen=FullScreenWindow
; Monitor index (0 = primary)
Monitor=0
; Quality level (0-5)
Quality=3
; Screen resolution (WIDTHxHEIGHT)
Resolution=1920x1080
; Target FPS (-1 = unlimited)
TargetFrameRate=-1
; Vertical synchronization (0/1)
VSync=1

; === Input and control settings ===
[Controls]
; Invert X axis (0/1)
InvertX=0
; Invert Y axis (0/1)
InvertY=0
; Mouse/camera sensitivity (0.1 - 3.0)
Sensitivity=1.0

; === Game-specific settings ===
[Gameplay]
; Interface language ('auto' = system language)
Language=auto
; Show subtitles (0/1)
Subtitles=1
```

---

## API Reference

### SettingsSystem

```csharp
// Свойства секций
AudioSettings Audio { get; }
VideoSettings Video { get; }
ControlsSettings Controls { get; }
GameplaySettings Gameplay { get; }

// Загрузка/Сохранение
void Load();                      // Загрузить из файла
void Save();                      // Сохранить в файл
void ApplyAll();                  // Применить все изменения
void ApplyAndSave();              // Применить и сохранить

// Откат/Сброс
void RevertAll();                 // Откатить все изменения
void Revert(string sectionName);  // Откатить секцию
void ResetAllToDefaults();        // Сбросить всё к дефолтам
void ResetToDefaults(string sectionName);

// Проверки
bool HasUnsavedChanges();
bool HasUnsavedChanges(string sectionName);

// Кастомные секции
void RegisterSection(SettingsSection section);
SettingsSection GetSection(string sectionName);
T GetCustomSection<T>(string name) where T : SettingsSection;

// Утилиты
string GetSettingsPath();         // Путь к файлу настроек
```

### SettingValue<T>

```csharp
T Value { get; set; }             // Текущее значение
T SavedValue { get; }             // Сохранённое значение
T DefaultValue { get; }           // Значение по умолчанию
bool IsModified { get; }          // Есть ли изменения

void MarkSaved();                 // Пометить как сохранённое
void Revert();                    // Откатить к сохранённому
void ResetToDefault();            // Сбросить к дефолту
```

---

## События (EventBus)

Все события находятся в пространстве `EventBus.Settings.*`:

| Событие | ID | Описание |
|---------|-----|----------|
| Loaded | 10100 | Настройки загружены |
| Saved | 10101 | Настройки сохранены |
| Applied | 10102 | Настройки применены |
| Reverted | 10103 | Изменения отменены |
| ResetToDefaults | 10104 | Сброс к дефолтам |
| Modified | 10105 | Появились несохранённые изменения |

**Audio события:** 10110-10114
**Video события:** 10120-10125
**Controls события:** 10130-10132
**Gameplay события:** 10140-10141

---

## Миграция версий

При изменении структуры настроек между версиями игры:

```csharp
// В своём коде
var migrator = new SettingsMigrator();
migrator.RegisterMigration(2, data => {
    // Пример: переименование ключа
    if (data.TryGetValue("Audio", out var audio))
    {
        if (audio.TryGetValue("Volume", out var value))
        {
            audio["MasterVolume"] = value;
            audio.Remove("Volume");
        }
    }
    return data;
});
```

---

## Платформы

| Платформа | Хранилище по умолчанию | Путь |
|-----------|------------------------|------|
| Windows/Mac/Linux | INI файл | `%AppData%/../LocalLow/Company/Product/settings.ini` |
| WebGL | PlayerPrefs | — |
| iOS/Android | PlayerPrefs | — |

Можно переопределить через `SettingsConfig.persistenceMode`.

---

## Лучшие практики

1. **Применяйте видео настройки отдельно** — `Screen.SetResolution` может вызвать мерцание
2. **Сохраняйте при выходе** — `OnApplicationQuit` или явно в меню
3. **Используйте события** — не опрашивайте значения в Update
4. **Кастомные секции** — для игровых настроек создавайте отдельные секции
5. **Проверяйте изменения** — показывайте кнопку "Применить" только при `HasUnsavedChanges()`
