# Sound System

Централизованная система управления звуком с поддержкой Unity AudioSource, FMOD и Wwise.

## Особенности

- **Plug & Play** — Setup Wizard создаёт всю инфраструктуру + 19 готовых UI звуков
- **Provider Pattern** — абстракция бэкенда (Unity/FMOD/Wwise)
- **Sound Library** — централизованное хранилище звуков с быстрым Dictionary-кэшем
- **Sound Banks** — ленивая загрузка/выгрузка для оптимизации памяти
- **UI/Session Schemes** — автоматические звуки для UI и игровых событий
- **Валидация в Editor** — проверка существования ID, подсветка ошибок
- **Priority System** — отсечение низкоприоритетных звуков при превышении лимита
- **Cooldown Protection** — защита от спама одинаковых звуков
- **Music System** — кроссфейд, вертикальное микширование, параметры

## Быстрый старт

### 1. Sound Setup Wizard (рекомендуется)

**Tools → ProtoSystem → Sound → Sound Setup Wizard**

Wizard автоматически создаёт:
- `SoundManagerConfig.asset` — главный конфиг
- `SoundLibrary.asset` — библиотека с 19 готовыми UI звуками
- `MainAudioMixer.mixer` — миксер с группами (Master, Music, SFX, Voice, Ambient, UI)
- `UISoundScheme.asset` — схема UI звуков (уже настроена)
- `GameSessionSoundScheme.asset` — схема игровых звуков
- `Audio/` — папка с 19 сгенерированными .wav файлами

### 2. Добавить систему на сцену

Способ A — через меню компонентов:
**Hierarchy → ПКМ → ProtoSystem → Add Sound Manager System**

Способ B — вручную:
1. Создать GameObject
2. Добавить компонент `SoundManagerSystem`
3. Назначить `SoundManagerConfig`

### 3. Готово!

UI звуки работают сразу. Воспроизведение из кода:

```csharp
// Простое воспроизведение
SoundManagerSystem.Play("ui_click");
SoundManagerSystem.Play("explosion", transform.position);

// С параметрами
SoundManagerSystem.Play("footstep", position, volume: 0.8f, pitch: 1.1f);
```

## Структура конфигурации

```
Assets/YourProject/Settings/Sound/
├── SoundManagerConfig.asset    # Главный конфиг (обязателен)
├── SoundLibrary.asset          # Библиотека звуков (обязателен)
├── MainAudioMixer.mixer        # Audio Mixer (рекомендуется)
├── UISoundScheme.asset         # Схема UI звуков (опционально)
├── GameSessionSoundScheme.asset # Схема игровых звуков (опционально)
├── MusicConfig.asset           # Настройки музыки (опционально)
└── Audio/                      # Аудио файлы
    ├── ui_click.wav
    ├── ui_hover.wav
    └── ...
```

## Конфигурационные файлы

### SoundManagerConfig

Главный конфиг системы. Связывает все компоненты:

| Секция | Назначение | Обязательно |
|--------|------------|-------------|
| Provider | Выбор аудио-движка (Unity/FMOD/Wwise) | Да |
| Library | Ссылка на SoundLibrary | Да |
| Audio Mixer | Ссылка на AudioMixer для управления громкостью | Рекомендуется |
| Sound Schemes | UI и GameSession схемы | Опционально |
| Default Volumes | Начальные значения громкости | Опционально |
| Unity Provider | Размер пула AudioSource, лимиты | Опционально |
| Playback Control | Приоритеты и cooldown | Опционально |
| 3D Sound | Настройки пространственного звука | Опционально |

### SoundLibrary

Центральное хранилище всех звуков:

```csharp
// Структура записи
public class SoundEntry
{
    public string id;              // Уникальный ID (ui_click, sfx_explosion)
    public SoundCategory category; // Music, SFX, Voice, Ambient, UI
    public AudioClip clip;         // Unity AudioClip
    public float volume = 1f;
    public float pitch = 1f;
    public float pitchVariation;   // Рандомизация pitch
    public bool loop;
    public bool spatial;           // 3D звук
    public SoundPriority priority;
    public float cooldown;         // Минимальный интервал
    public string fmodEvent;       // Для FMOD провайдера
    public string bankId;          // Привязка к банку
}
```

**Особенности редактора:**
- Поиск и фильтрация по категории
- Статистика (количество, отсутствующие клипы)
- Валидация при добавлении
- Кнопка "Validate All"

### Sound Banks

Группы звуков для ленивой загрузки:

```csharp
// Загрузка банка
await SoundManagerSystem.LoadBankAsync("level_1_sounds");

// Выгрузка
SoundManagerSystem.UnloadBank("level_1_sounds");
```

**Когда использовать банки:**
- Много звуков (100+) и нужна оптимизация памяти
- Звуки привязаны к конкретным сценам/уровням
- Используете FMOD с банками

**Автозагрузка:**
- `loadOnStartup` — загрузить при старте игры
- `loadWithScenes` — загрузить при переходе на указанные сцены

### UISoundScheme

Автоматические звуки для UI событий:

| Категория | События |
|-----------|---------|
| Window | windowOpen, windowClose, modalOpen, modalClose |
| Button | buttonClick, buttonHover, buttonDisabled |
| Navigation | navigate, back, tabSwitch |
| Feedback | success, error, warning, notification |
| Controls | sliderChange, toggleOn, toggleOff, dropdownOpen, dropdownSelect |
| Snapshots | modalSnapshot, pauseSnapshot |

**Валидация в редакторе:**
- Подсвечивает ID которых нет в SoundLibrary
- Кнопка "Create X Missing" — создаёт отсутствующие записи

### GameSessionSoundScheme

Звуки для игровой сессии:

| Категория | Звуки |
|-----------|-------|
| Music | menuMusic, gameplayMusic, pauseMusic, victoryMusic, defeatMusic |
| Stingers | sessionStartStinger, victoryStinger, defeatStinger, checkpointStinger |
| Transitions | musicFadeTime, stingerDuckAmount, stingerDuckDuration |
| Snapshots | pauseSnapshot, gameOverSnapshot |

### MusicConfig

Расширенные настройки музыки:

- **Crossfade** — время и кривая перехода между треками
- **Vertical Layering** — слои музыки с параметрическим управлением
- **Parameters** — параметры для адаптивной музыки

```csharp
// Пример использования параметров
SoundManagerSystem.SetMusicParameter("intensity", 0.8f);
SoundManagerSystem.SetMusicParameter("danger", 1.0f);
```

## API

### Воспроизведение

```csharp
// Базовое
SoundManagerSystem.Play("sound_id");
SoundManagerSystem.Play("sound_id", position);
SoundManagerSystem.Play("sound_id", position, volume, pitch);

// С handle для управления
SoundHandle handle = SoundManagerSystem.Play("ambient_wind");
handle.Stop();
handle.SetVolume(0.5f);

// One-shot (без возврата handle)
SoundManagerSystem.PlayOneShot("ui_click");
```

### Музыка

```csharp
// Воспроизведение
SoundManagerSystem.PlayMusic("battle_theme");
SoundManagerSystem.PlayMusic("battle_theme", fadeIn: 2f);

// Кроссфейд
SoundManagerSystem.CrossfadeMusic("peaceful_theme", duration: 3f);

// Остановка
SoundManagerSystem.StopMusic();
SoundManagerSystem.StopMusic(fadeOut: 1f);

// Пауза
SoundManagerSystem.PauseMusic();
SoundManagerSystem.ResumeMusic();
```

### Громкость

```csharp
// Установка
SoundManagerSystem.SetVolume(SoundCategory.Master, 0.8f);
SoundManagerSystem.SetVolume(SoundCategory.Music, 0.5f);
SoundManagerSystem.SetVolume(SoundCategory.SFX, 1.0f);

// Получение
float volume = SoundManagerSystem.GetVolume(SoundCategory.Music);

// Mute
SoundManagerSystem.SetMute(true);
SoundManagerSystem.SetMute(false);
```

### Snapshots

```csharp
// Активация
SoundManagerSystem.SetSnapshot(SoundSnapshotPreset.Underwater);
SoundManagerSystem.SetSnapshot(SoundSnapshotPreset.Paused);

// Деактивация
SoundManagerSystem.ClearSnapshot(SoundSnapshotPreset.Underwater);

// Кастомный snapshot
SoundManagerSystem.SetSnapshot(new SoundSnapshotId("my_snapshot"));
```

### EventBus интеграция

```csharp
// Воспроизведение через события
EventBus.Publish(Evt.Sound.Play, "explosion");
EventBus.Publish(Evt.Sound.PlayAt, ("footstep", transform.position));
EventBus.Publish(Evt.Sound.PlayMusic, "battle_theme");
EventBus.Publish(Evt.Sound.StopMusic);

// Громкость
EventBus.Publish(Evt.Sound.SetVolume, (SoundCategory.Music, 0.5f));
```

## Компоненты

### PlaySoundOn

Универсальный триггер звука без кода:

**Triggers:**
- Lifecycle: Enable, Disable, Start, Destroy
- Physics: CollisionEnter/Exit, TriggerEnter/Exit
- UI: PointerEnter/Exit/Click/Down/Up
- Input: KeyDown/Up
- Custom: EventBus, Manual

```csharp
// Ручной вызов
GetComponent<PlaySoundOn>().PlayManual();
```

### MusicZone

Зона автоматической смены музыки:

- Смена трека при входе игрока
- Опциональный snapshot
- Настраиваемый fade time
- Восстановление предыдущего трека при выходе

### AmbientZone

3D ambient звук с зоной:

- Автоматический fade in/out при входе/выходе
- Настраиваемая громкость и радиус
- Поддержка loop

### SoundEmitter

Точка воспроизведения для Animator/UnityEvents:

```csharp
// Вызов из Animator Event или UnityEvent
soundEmitter.Play();
soundEmitter.PlaySound("custom_id");
soundEmitter.Stop();
```

## Сгенерированные UI звуки

Setup Wizard создаёт 19 процедурно сгенерированных звуков:

| ID | Описание | Характеристики |
|----|----------|----------------|
| ui_whoosh | Открытие окна | Noise sweep, 80ms |
| ui_close | Закрытие окна | Descending 600→200Hz, 60ms |
| ui_modal_open | Открытие модального | Ascending с гармониками, 120ms |
| ui_modal_close | Закрытие модального | Descending с fade, 100ms |
| ui_click | Клик кнопки | Sharp 800Hz, 40ms |
| ui_hover | Наведение | Soft 600Hz, 25ms |
| ui_disabled | Неактивная кнопка | Muted 200Hz, 30ms |
| ui_navigate | Навигация | Quick blip 700Hz, 30ms |
| ui_back | Назад | Descending 800→400Hz, 60ms |
| ui_tab | Переключение вкладки | Sharp 900Hz, 20ms |
| ui_success | Успех | Ascending 600→1200Hz, 100ms |
| ui_error | Ошибка | Dissonant buzz, 150ms |
| ui_warning | Предупреждение | Double beep 500Hz |
| ui_notification | Уведомление | Bell 1000Hz + harmonics, 200ms |
| ui_slider | Слайдер | Tick 1200Hz, 10ms |
| ui_toggle_on | Toggle вкл | Ascending 600→900Hz, 40ms |
| ui_toggle_off | Toggle выкл | Descending 900→600Hz, 40ms |
| ui_dropdown | Dropdown открытие | Pop 500→700Hz, 30ms |
| ui_select | Выбор | Blip 800Hz, 25ms |

**Замена звуков:** просто замените .wav файлы в папке Audio/ на свои.

## Интеграция с FMOD

1. Установить FMOD for Unity
2. В `SoundManagerConfig` выбрать `Provider Type = FMOD`
3. В `SoundEntry` заполнить `FMOD Event` вместо `Clip`
4. В `SoundBank` указать `fmodBankPath`

Игровой код **не меняется** — провайдер абстрагирует детали.

## Расширение

### Custom Sound Processor

```csharp
public class OcclusionProcessor : ISoundProcessor
{
    public LayerMask occlusionLayers;
    
    public void ProcessActiveSound(ref ActiveSoundInfo sound, Vector3 listenerPos)
    {
        if (Physics.Linecast(listenerPos, sound.Position, occlusionLayers))
        {
            sound.VolumeMultiplier = 0.3f;
            sound.LowPassCutoff = 1000f;
        }
    }
}

// Подключение
SoundManagerSystem.SetSoundProcessor(new OcclusionProcessor());
```

## Структура файлов

```
Runtime/Sound/
├── SoundManagerSystem.cs       # Главный фасад
├── SoundCategory.cs            # Категории (Music, SFX, Voice, Ambient, UI)
├── SoundPriority.cs            # Приоритеты
├── SoundSnapshot.cs            # Snapshot ID и пресеты
├── SoundHandle.cs              # Handle играющего звука
├── Config/
│   ├── SoundManagerConfig.cs   # Главный конфиг
│   ├── VolumeSettings.cs
│   ├── PrioritySettings.cs
│   ├── CooldownSettings.cs
│   ├── UISoundScheme.cs
│   ├── GameSessionSoundScheme.cs
│   └── MusicConfig.cs
├── Library/
│   ├── SoundLibrary.cs
│   ├── SoundEntry.cs
│   └── SoundBank.cs
├── Provider/
│   ├── ISoundProvider.cs
│   ├── ISoundProcessor.cs
│   └── UnitySoundProvider.cs
├── Components/
│   ├── PlaySoundOn.cs
│   ├── MusicZone.cs
│   ├── AmbientZone.cs
│   └── SoundEmitter.cs
└── Attributes/
    └── SoundIdAttribute.cs

Editor/Sound/
├── SoundSetupWizard.cs             # Setup Wizard
├── SoundManagerSystemEditor.cs     # Редактор системы
├── SoundManagerConfigEditor.cs     # Редактор конфига
├── SoundLibraryEditor.cs           # Редактор библиотеки
├── SoundBankEditor.cs              # Редактор банков
├── UISoundSchemeEditor.cs          # Редактор UI схемы
├── GameSessionSoundSchemeEditor.cs # Редактор игровой схемы
├── MusicConfigEditor.cs            # Редактор музыки
├── SoundIdDrawer.cs                # PropertyDrawer для SoundId
├── PlaySoundOnEditor.cs
└── Generators/
    ├── SoundMixerGenerator.cs      # Генератор AudioMixer
    └── ProceduralSoundGenerator.cs # Генератор UI звуков
```

## Troubleshooting

### Звук не воспроизводится

1. Проверьте что `SoundManagerSystem` есть на сцене и инициализирован
2. Проверьте что ID существует в `SoundLibrary`
3. Проверьте что `AudioClip` назначен
4. Проверьте громкость категории (`SoundManagerSystem.GetVolume()`)

### "X ID не найдено в Sound Library"

В редакторе UISoundScheme/GameSessionSoundScheme:
1. Нажмите "Create X Missing" — создаст записи в библиотеке
2. Назначьте AudioClip для созданных записей

### Звуки не генерируются при Setup

1. Проверьте права на запись в папку
2. Перезапустите Setup Wizard
3. Вручную запустите: `ProceduralSoundGenerator.GenerateAllUISounds(path)`

### AudioMixer не создаётся

AudioMixer создаётся копированием шаблона. Если не работает:
1. Проверьте наличие шаблона: `Packages/com.protosystem.core/Runtime/Sound/Templates/`
2. Создайте AudioMixer вручную в Unity

---

## Интеграция с SettingsSystem

### Автоматическая синхронизация громкости

`SoundManagerSystem` автоматически интегрируется с `SettingsSystem`:

1. **При инициализации** — запрашивает текущие значения из `SettingsSystem.Audio`
2. **При изменении настроек** — подписан на события `EventBus.Settings.Audio.*`

### Порядок инициализации

```
[Startup - приоритеты]
  SettingsSystem (5) 
    → Load() [события подавлены]
    → ApplyAll()
  
  SoundManagerSystem (12)
    → ApplySettingsFromSettingsSystem()  // Запрашивает настройки
    → Подписывается на события

[Runtime]
  Пользователь меняет громкость в UI
    → settings.Audio.MasterVolume = 0.5f
    → EventBus.Settings.Audio.MasterChanged
    → SoundManager.OnMasterVolumeChanged()
    → SetVolume(SoundCategory.Master, 0.5f)
```

### Зависимость от SettingsSystem

```csharp
// SoundManagerSystem.cs
[Dependency(required: false, description: "Интеграция с системой настроек")]
private Settings.SettingsSystem _settingsSystem;

private void ApplySettingsFromSettingsSystem()
{
    if (_settingsSystem?.Audio == null)
    {
        LogMessage("SettingsSystem not available, using default volumes");
        return;
    }
    
    var audio = _settingsSystem.Audio;
    SetVolume(SoundCategory.Master, audio.MasterVolume);
    SetVolume(SoundCategory.Music, audio.MusicVolume);
    SetVolume(SoundCategory.SFX, audio.SFXVolume);
}
```

### События настроек

SoundManager подписан на события из `EventBus.Settings.Audio`:

| Событие | ID | Действие |
|---------|-----|----------|
| MasterChanged | 10110 | `SetVolume(Master, value)` |
| MusicChanged | 10111 | `SetVolume(Music, value)` |
| SFXChanged | 10112 | `SetVolume(SFX, value)` |

**Важно:** Обработчики проверяют `IsInitialized` — события до инициализации игнорируются.

### Кастомная интеграция

Если нужна своя логика применения настроек:

```csharp
public class MySoundSystem : InitializableSystemBase
{
    [Dependency(required: false)]
    private Settings.SettingsSystem _settings;
    
    protected override void InitEvents()
    {
        // Подписка на runtime изменения
        AddEvent(EventBus.Settings.Audio.MasterChanged, OnVolumeChanged);
    }
    
    public override async Task<bool> InitializeAsync()
    {
        // Начальные значения
        if (_settings?.Audio != null)
        {
            ApplyVolume(_settings.Audio.MasterVolume);
        }
        return true;
    }
    
    private void OnVolumeChanged(object payload)
    {
        // Проверка инициализации обязательна!
        if (!IsInitialized) return;
        
        if (payload is SettingChangedData<float> data)
            ApplyVolume(data.Value);
    }
}
```
