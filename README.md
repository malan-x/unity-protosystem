# ProtoSystem Core

Модульный Unity фреймворк: событийная архитектура, атрибутивная инициализация систем, граф-based UI, звук, локализация, LiveOps, ИИ-генерация аудио и инструменты публикации.

## Возможности

### Ядро
- **EventBus** — Глобальная система событий с группировкой и автоподпиской
- **System Initialization** — Атрибутивное внедрение зависимостей и управление порядком инициализации
- **Network Support** — Встроенная поддержка Netcode for GameObjects (NetworkLobbySystem)

### UI Система
- **UISystem** — Граф-ориентированная навигация между окнами с атрибутами
- **UIWindowGraph** — Автоматическая сборка графа из атрибутов UIWindow и UITransition
- **Window Graph Viewer** — Интерактивный визуальный редактор графа UI
- **UINavigator** — Стековая навигация с историей (Back, Close)
- **Window Prefab Generator** — Автогенерация UI префабов из редактора
- **UI Toolkit Window Generator** — Генерация Toolkit-окон (UXML/USS/код) + предпросмотр UIPreviewWindow
- **UITimeManager** — Управление паузой игры для UI (счётчик-based)
- **CursorManagerSystem** — Стек состояний курсора

### Sound System
- **SoundManagerSystem** — Централизованное управление звуком с Provider Pattern
- **Sound Setup Wizard** — Создание всей инфраструктуры + 19 готовых UI звуков
- **Sound Library** — Центральное хранилище с валидацией и поиском
- **Sound Schemes** — Автоматические звуки для UI и игровых событий
- **Music System** — Кроссфейд, вертикальное микширование, параметры
- **Поддержка FMOD/Wwise** — Абстракция провайдеров

### AudioGen — ядро ИИ Аудио студии
- **5 движков генерации**: ComfyUI (Stable Audio Open — SFX/UI/эмбиенты, ACE-Step — музыка), ElevenLabs (SFX и TTS-озвучка), Qwen3-TTS (локальный TTS)
- **Контракт IAudioContentProvider** — игровые SO сами объявляют, какие звуки им нужны (сеты → сущности → колбэки клип/промпт/варианты)
- **Пайплайн ffmpeg** — конвертация в WAV, трим тишины, фейды, пост-фильтры (например, «рация»)
- **Варианты и паки** — история генераций с рецептами, базовые варианты, переключение наборов звуков одним кликом
- Окно студии остаётся в проекте — пакет даёт движки, очередь, пресеты стилей, профили и хранилище вариантов

### LiveOps
- **LiveOpsSystem** — Анонсы, события, опросы, рейтинг, фидбек, девлог, майлстоуны с сервера (PocketBase/HTTP-провайдеры)
- **CommunityPanel** — Готовое Toolkit-окно сообщества
- **LiveOps Dashboard** — Редактор конфигурации и контента в Unity

### Capture
- **CaptureSystem** — Скриншоты и реплей-буфер (встроенный энкодер + мост к Unity Recorder)
- **MultiLangCapture** — Автоматический захват скриншотов на всех языках (для сторов)

### Публикация и инструменты редактора
- **Build Publisher** — Сборки по флейворам, Steam-аплоад, патчноуты, пароли читов
- **TODO-лист** — Задачи проекта прямо в тулбаре редактора
- **MCP-интеграция** — Статус и настройка MCP for Unity в тулбаре
- **AI Translation** — Окно ИИ-перевода строк локализации (Claude)

### Дополнительные системы
- **GameSessionSystem** — Управление жизненным циклом игровой сессии (старт, пауза, рестарт, завершение)
- **LocalizationSystem** — Локализация с AI-оптимизированным экспортом/импортом (wrapper над Unity Localization)
- **SettingsSystem** — Управление настройками (INI формат)
- **EffectsManager** — Система визуальных эффектов
- **SceneFlowSystem** — Управление переходами между сценами
- **Logging / Compat** — Утилиты логирования и совместимости версий Unity

## Быстрый старт

См. [QUICKSTART.md](QUICKSTART.md) для быстрой интеграции.

## Документация

- [ProtoSystem Guide](Documentation~/ProtoSystem-Guide.md) — Основная документация
- [UISystem Guide](Documentation~/UISystem.md) — Полная документация UI системы
- [Sound System](Documentation~/Sound.md) — Звуковая система
- [UISystem Test Scenarios](Documentation~/UISystem_TestScenarios.md) — Тестовые сценарии
- [GameSession Guide](Documentation~/GameSession.md) — Система игровых сессий
- [Localization Guide](Documentation~/Localization.md) — Система локализации
- [SettingsSystem Guide](Documentation~/SettingsSystem.md) — Система настроек
- [LiveOps Guide](Documentation~/LiveOps.md) — LiveOps: клиент, контент, серверный контракт
- [AI Instructions](Documentation~/AI_INSTRUCTIONS.md) — Инструкции для ИИ-ассистентов
- [Changelog](CHANGELOG.md) — История изменений

## Установка

### Package Manager (Git URL)
```
https://github.com/malan-x/unity-protosystem.git
```

### Локально (Packages/)
Скопировать папку `com.protosystem.core` в `Packages/` проекта.

## Структура пакета

```
com.protosystem.core/
├── Runtime/
│   ├── EventBus/          # Система событий
│   ├── Initialization/    # Инициализация и DI
│   ├── GameSession/       # Управление игровыми сессиями
│   ├── UI/                # UI система
│   │   ├── Core/          # UISystem, UINavigator, UIWindowGraph
│   │   ├── Windows/       # Базовые классы окон (+ LiveOps CommunityPanel)
│   │   ├── Attributes/    # UIWindow, UITransition атрибуты
│   │   └── Graph/         # Граф переходов
│   ├── Sound/             # Звуковая система
│   │   ├── Config/        # Конфигурации
│   │   ├── Library/       # SoundLibrary, SoundBank
│   │   ├── Provider/      # ISoundProvider, UnitySoundProvider
│   │   └── Components/    # PlaySoundOn, MusicZone, etc.
│   ├── AudioGen/          # Контракт ИИ Аудио студии (IAudioContentProvider)
│   ├── Localization/      # Система локализации (Loc, PluralRules, LocalizeTMP)
│   ├── LiveOps/           # LiveOps: система, провайдеры, модели данных
│   ├── Capture/           # Скриншоты и реплей-буфер
│   ├── NetworkLobby/      # Сетевое лобби (NGO)
│   ├── Build/             # BuildFlavor (флейворы сборок в рантайме)
│   ├── Publishing/        # Читы, патчноуты (рантайм-часть)
│   ├── Settings/          # Система настроек
│   ├── Effects/           # Эффекты
│   ├── Cursor/            # Управление курсором
│   ├── SceneFlow/         # Управление сценами
│   ├── Logging/           # Логирование
│   └── Compat/            # Совместимость версий Unity
├── Editor/
│   ├── UI/                # Генераторы префабов/Toolkit-окон, Graph Viewer, предпросмотр
│   ├── Sound/             # Sound Setup Wizard, Editors
│   ├── AudioGen/          # Ядро ИИ Аудио студии: движки, очередь, стили, варианты, паки
│   ├── Localization/      # Окно ИИ-перевода (Claude)
│   ├── LiveOps/           # Редактор конфигурации LiveOps
│   ├── Capture/           # Окно захвата, мультиязычные скриншоты, Recorder-мост
│   ├── Build/ + Publishing/ # Build Publisher: флейворы, Steam-аплоад, читы
│   ├── MCP/               # Интеграция MCP for Unity в тулбаре
│   ├── GameSession/       # Утилиты GameSession
│   └── Initialization/    # Инспекторы систем, Setup Wizard
└── Documentation~/        # Документация
```

## Основные компоненты

### EventBus

```csharp
// Публикация события
EventBus.Publish(Evt.Combat.AttackPerformed, damage);

// Подписка в MonoEventBus
public class MyComponent : MonoEventBus
{
    protected override void InitEvents()
    {
        AddEvent(Evt.Combat.AttackPerformed, OnAttack);
    }
    
    private void OnAttack(object payload)
    {
        var damage = (float)payload;
        Debug.Log($"Attack dealt {damage} damage");
    }
}
```

### System Initialization

```csharp
public class MySystem : InitializableSystemBase
{
    [Dependency] private OtherSystem dependency;
    
    public override string SystemId => "my_system";
    public override string DisplayName => "My System";
    
    protected override void InitEvents()
    {
        AddEvent(Evt.Game.Started, OnGameStarted);
    }
    
    public override async Task<bool> InitializeAsync()
    {
        ReportProgress(0.5f);
        // Логика инициализации
        ReportProgress(1.0f);
        return true;
    }
    
    private void OnGameStarted(object payload)
    {
        Debug.Log("Game started!");
    }
}
```

### UISystem — Граф-based навигация

#### Определение окон с атрибутами

```csharp
[UIWindow("main_menu", WindowType.Normal, WindowLayer.Windows, Level = 0)]
[UITransition("play", "GameHUD")]
[UITransition("settings", "Settings")]
[UITransition("credits", "Credits")]
public class MainMenu : UIWindowBase
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    
    protected override void Awake()
    {
        base.Awake();
        playButton?.onClick.AddListener(() => UISystem.Navigate("play"));
        settingsButton?.onClick.AddListener(() => UISystem.Navigate("settings"));
    }
    
    public override void OnBackPressed()
    {
        // Обработка Escape на главном меню
        UISystem.Instance.Dialog.Confirm(
            "Quit game?",
            onYes: () => Application.Quit()
        );
    }
}
```

#### Навигация по триггерам

```csharp
// Переход по триггеру (рекомендуется)
UISystem.Navigate("play");        // MainMenu → GameHUD
UISystem.Navigate("settings");    // MainMenu → Settings

// Навигация назад
UISystem.Back();
```

#### Настройка сцен-специфичных переходов

```csharp
public class GameplayInitializer : UISceneInitializerBase
{
    public override string StartWindowId => "game_hud";

    public override IEnumerable<string> StartupWindowOrder
    {
        get { yield return StartWindowId; }
    }

    public override void Initialize(UISystem uiSystem)
    {
        var navigator = uiSystem.Navigator;
        foreach (var windowId in StartupWindowOrder)
            navigator.Open(windowId);
    }
    
    public override IEnumerable<UITransitionDefinition> GetAdditionalTransitions()
    {
        yield return new UITransitionDefinition(
            "game_hud",
            "pause_menu",
            "pause",
            TransitionAnimation.Fade
        );
    }
}
```

### Sound System

#### Быстрая настройка

**ProtoSystem → Sound → Sound Setup Wizard**

Wizard создаёт всё автоматически: конфиги, библиотеку, миксер и 19 готовых UI звуков.

#### Использование

```csharp
// Воспроизведение звуков
SoundManagerSystem.Play("ui_click");
SoundManagerSystem.Play("explosion", transform.position);

// Музыка
SoundManagerSystem.PlayMusic("battle_theme", fadeIn: 2f);
SoundManagerSystem.CrossfadeMusic("peaceful", 3f);
SoundManagerSystem.StopMusic(fadeOut: 1f);

// Громкость
SoundManagerSystem.SetVolume(SoundCategory.Music, 0.5f);
SoundManagerSystem.SetMute(true);

// Snapshots
SoundManagerSystem.SetSnapshot(SoundSnapshotPreset.Underwater);

// Банки (ленивая загрузка)
await SoundManagerSystem.LoadBankAsync("Level1");
SoundManagerSystem.UnloadBank("Level1");
```

### Localization System

#### Быстрая настройка

**ProtoSystem → Localization → Setup Wizard**

#### Использование

```csharp
// Простой ключ
string text = Loc.Get("menu.play");  // → "ИГРАТЬ"

// С fallback
string text = Loc.Get("menu.play", "PLAY");

// Из конкретной таблицы
string name = Loc.Get("Items", "sword.name");

// Множественное число
string msg = Loc.GetPlural("enemies.killed", 5);  // → "убито 5 врагов"

// Вложенная ссылка
string msg = Loc.Get("found.item", ("item", Loc.Ref("Items", itemId)));

// Смена языка
Loc.SetLanguage("en");
```

#### Компонент LocalizeTMP

Добавьте на GameObject с TMP_Text — текст обновится автоматически при смене языка.

### Window Graph Viewer

Интерактивный редактор графа UI окон:

**ProtoSystem → UI → Window Graph Viewer**

**Возможности:**
- Визуализация всех окон и переходов
- Кликабельные ноды и связи
- Детальная информация в инспекторе
- Проверка достижимости окон
- Drag & drop для позиционирования
- Zoom и панорамирование

**Цветовая кодировка:**
- 🟢 Зелёный — стартовое окно
- 🔵 Синий — выбранное окно
- 🔴 Красный — модальное окно
- ⚫ Серый полупрозрачный — недостижимое окно

### Генерация UI префабов

В Unity Editor: **ProtoSystem → UI → Generate Window → [тип окна]**

Доступные генераторы:
- MainMenu, PauseMenu, Settings
- GameHUD, GameOver, Statistics
- Credits, Loading

После генерации: **ProtoSystem → UI → Rebuild Window Graph**

### Диалоговые окна

```csharp
// Сообщение
UISystem.Instance.Dialog.Message("Hello World!");

// Подтверждение
UISystem.Instance.Dialog.Confirm(
    "Are you sure?",
    onYes: () => Debug.Log("Yes"),
    onNo: () => Debug.Log("No")
);

// Выбор
UISystem.Instance.Dialog.Choice(
    "Choose option",
    new[] { "Option A", "Option B", "Option C" },
    (index) => Debug.Log($"Selected: {index}")
);

// Ввод текста
UISystem.Instance.Dialog.Input(
    "Enter name:",
    (text) => Debug.Log($"Name: {text}"),
    placeholder: "Player Name"
);
```

## Workflow UISystem

### 1. Создание окна

```csharp
[UIWindow("my_window", WindowType.Modal, WindowLayer.Modals,
    Level = 2, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
[UITransition("back", "MainMenu")]
public class MyWindow : UIWindowBase
{
    // Реализация окна
}
```

### 2. Создание префаба

Вручную или через генератор:
```csharp
// В Editor скрипте
UIWindowPrefabGenerator.GenerateMyWindow();
```

### 3. Сборка графа

**ProtoSystem → UI → Rebuild Window Graph**

Автоматически:
- Сканирует все классы с `[UIWindow]`
- Собирает переходы из `[UITransition]`
- Находит префабы окон
- Создаёт/обновляет UIWindowGraph

### 4. Визуальная проверка

**ProtoSystem → UI → Window Graph Viewer**

- Проверить что все окна достижимы
- Убедиться что переходы корректны
- Кликнуть на связи для просмотра деталей

### 5. Использование

```csharp
// В коде игры
UISystem.Navigate("my_window");
```

## Зависимости

- Unity 2021.3+
- Netcode for GameObjects 2.4.4
- TextMeshPro

## Интеграция с ИИ

Пакет включает инструкции для ИИ-ассистентов. Для автозагрузки в GitHub Copilot:

```bash
cp Packages/com.protosystem.core/Documentation~/AI_INSTRUCTIONS.md .github/copilot-instructions.md
```

## Best Practices

1. **Используйте атрибуты** — `[UIWindow]` и `[UITransition]` для определения окон и переходов
2. **Navigate() > Open()** — переходы по триггерам чётче определяют граф
3. **Rebuild Graph** — после изменений в атрибутах
4. **Проверяйте визуально** — Graph Viewer помогает увидеть недостижимые окна
5. **UISceneInitializerBase** — для сцен-специфичных переходов
6. **Sound Setup Wizard** — для быстрой настройки звуковой системы

## Примеры проектов

См. папку `/Tests` для примеров использования:
- `UISystemTests` — тесты навигации и графа
- `EventBusTests` — тесты событийной системы
- `InitializationTests` — тесты систем инициализации

## Лицензия

См. файл LICENSE.
