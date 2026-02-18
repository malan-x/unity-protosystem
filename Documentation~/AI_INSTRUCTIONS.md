# ProtoSystem — AI Agent Instructions

> Инструкции для ИИ-ассистентов (Claude, GitHub Copilot, Cursor) по работе с ProtoSystem.

## Обзор фреймворка

ProtoSystem — модульный Unity фреймворк для прототипирования игр с:
- **EventBus** — Глобальная система событий
- **System Initialization** — DI с атрибутами `[Dependency]`
- **UISystem** — Граф-ориентированная UI навигация
- **SoundSystem** — Централизованное управление звуком
- **SettingsSystem** — Настройки в INI формате

---

## 1. EventBus

### Структура событий

События определяются в **проекте** (не в пакете):

```csharp
// Assets/ProjectName/Scripts/Events/EventIds.cs
namespace ProjectName
{
    public static class Evt
    {
        public enum EventType
        {
            // Добавлять ВСЕ события сюда для уникальности ID
            PlayerSpawned,
            EnemyKilled,
            DamageDealt,
        }

        public static class Player
        {
            public const int Spawned = (int)EventType.PlayerSpawned;
        }
        
        public static class Combat
        {
            public const int EnemyKilled = (int)EventType.EnemyKilled;
            public const int DamageDealt = (int)EventType.DamageDealt;
        }
    }
}
```

### Использование

```csharp
// Публикация
EventBus.Publish(Evt.Combat.DamageDealt, damageAmount);

// Подписка в MonoEventBus
public class MyComponent : MonoEventBus
{
    protected override void InitEvents()
    {
        AddEvent(Evt.Combat.DamageDealt, OnDamage);
    }
    
    private void OnDamage(object payload)
    {
        float damage = (float)payload;
    }
}
```

### IEventBus — для классов, которые не могут наследоваться от MonoEventBus

```csharp
// Например, UIWindowBase уже наследуется от MonoBehaviour
public class MyWindow : UIWindowBase, IEventBus
{
    public List<(int id, Action<object> action)> events { get; set; } = new();

    void Awake() => InitEvents();
    
    public void InitEvents()
    {
        AddEvent(Evt.Game.Started, OnGameStarted);
    }

    protected override void OnShow() => SubscribeEvents();
    protected override void OnHide() => UnsubscribeEvents();
    
    private void OnGameStarted(object payload) { }
}
```

### Правила

✅ **DO:**
- Использовать `Evt.Category.EventName` для всех событий
- Добавлять новые события в enum `EventType`
- Группировать события по категориям
- Использовать `IEventBus` когда нельзя наследоваться от `MonoEventBus`

❌ **DON'T:**
- Магические числа: `EventBus.Publish(1001, data)`
- Дублировать ID событий

---

## 2. System Initialization

### Базовые классы

| Класс | Использование |
|-------|--------------|
| `InitializableSystemBase` | Локальные системы |
| `NetworkInitializableSystem` | Сетевые системы (Netcode) |
| `MonoEventBus` | Компоненты с событиями |

### Создание системы

```csharp
public class MySystem : InitializableSystemBase
{
    // Обязательные свойства
    public override string SystemId => "my_system";
    public override string DisplayName => "My System";
    
    // Зависимости (инъектируются автоматически)
    [Dependency(required: true, description: "Обязательная система")]
    private OtherSystem otherSystem;
    
    [Dependency(required: false)]
    private OptionalSystem optionalSystem;
    
    // Отложенные зависимости (после основной инициализации)
    [PostDependency]
    private LateSystem lateSystem;
    
    // Подписка на события
    protected override void InitEvents()
    {
        AddEvent(Evt.Game.Started, OnGameStarted);
    }
    
    // Асинхронная инициализация
    public override async Task<bool> InitializeAsync()
    {
        ReportProgress(0.3f);
        await SomeAsyncWork();
        ReportProgress(1.0f);
        return true;
    }
}
```

### Сетевые системы

```csharp
public class MyNetworkSystem : NetworkInitializableSystem
{
    public override string SystemId => "my_network_system";
    public override string DisplayName => "My Network System";
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer) { /* серверная логика */ }
        if (IsOwner) { /* логика владельца */ }
    }
    
    // Хелперы для событий
    void Example()
    {
        PublishEventServerOnly(Evt.Server.Event, data);
        PublishEventClientOnly(Evt.Client.Event, data);
        PublishEventIfLocalPlayer(Evt.Local.Event, data);
    }
}
```

### Правила

✅ **DO:**
- Использовать `[Dependency]` для внедрения зависимостей
- Вызывать `ReportProgress()` в `InitializeAsync()`
- Проверять `IsSpawned`, `IsServer`, `IsOwner` в сетевых системах

❌ **DON'T:**
- `FindObjectOfType<T>()` — использовать `SystemProvider.GetSystem<T>()`
- Циклические зависимости — использовать события или `[PostDependency]`
- Тяжёлая синхронная инициализация — использовать `async/await`

---

## 3. UISystem

### Архитектура

```
UISystem (синглтон)
├── UISystemConfig (ScriptableObject)
│   ├── windowPrefabs[] — префабы окон
│   ├── windowPrefabLabels[] — метки для автосканирования
│   └── common prefabs (dialogs/toast/tooltip/progress/modal overlay)
├── UIWindowGraph (ScriptableObject) — граф окон и переходов (собирается из атрибутов на классах окон)
├── UIWindowFactory — создание инстансов окон
├── UINavigator — стековая навигация
├── UITimeManager — управление паузой
└── CursorManagerSystem — состояние курсора
```

### Атрибут окна

```csharp
[UIWindow(
    "window_id",                          // Уникальный ID
    WindowType.Normal,                    // Normal, Modal, Overlay
    WindowLayer.Windows,                  // Background, HUD, Windows, Modals, Overlay
    Level = 0,                            // 0 = главные окна (взаимоисключающие)
    PauseGame = true,                     // Ставить игру на паузу
    CursorMode = WindowCursorMode.Visible // Locked, Visible, Hidden
)]
public class MyWindow : UIWindowBase
{
    protected override void OnBeforeShow() { }
    protected override void OnShow() { }
    protected override void OnBeforeHide() { }
    protected override void OnHide() { }
}
```

### Уровни окон (Level)

| Level | Поведение |
|-------|-----------|
| 0 | Главные окна (MainMenu, GameHUD). Взаимоисключающие — открытие одного закрывает другое |
| 1+ | Стековые окна. Накладываются поверх, Back() возвращает к предыдущему |

### Навигация

```csharp
// Открыть окно
UISystem.Open("settings");

// Рекомендуемый путь: переход по триггеру из [UITransition]
var result = UISystem.Navigate("settings");
if (result != NavigationResult.Success)
    Debug.LogWarning($"Navigate failed: {result}");

// Закрыть и вернуться
UISystem.Back();

// Закрыть конкретное окно (если это overlay, верхнее modal или текущее в стеке)
UISystem.Instance.Navigator.Close("window_id");

// Получить текущее окно
var current = UISystem.Instance.CurrentWindow;
```

> Примечание про данные/"context": в текущем API `UISystem.Open()`/`Navigate()` не принимают payload.
> Для передачи данных используйте состояние в системе/модели (DI) или EventBus и считывайте его в `OnShow()`.

### Создание кастомного окна

```csharp
[UIWindow("my_dialog", WindowType.Modal, WindowLayer.Modals,
    Level = 2, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
public class MyDialog : UIWindowBase
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    
    protected override void Awake()
    {
        base.Awake();
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        confirmButton.onClick.RemoveAllListeners();
        cancelButton.onClick.RemoveAllListeners();
    }
    
    private void OnConfirm()
    {
        // Логика подтверждения
        UISystem.Back();
    }
    
    private void OnCancel() => UISystem.Back();
}
```

### IUISceneInitializer

Для добавления переходов и окон при запуске сцены:

```csharp
using System.Collections.Generic;
using UnityEngine;
using ProtoSystem.UI;

public class GameplayInitializer : UISceneInitializerBase
{
    // Window IDs are ids from [UIWindow("...")] attributes (graph ids), not prefab/class names.
    public override string StartWindowId => "GameHUD";

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
        yield return new UITransitionDefinition("GameHUD", "PauseMenu", "pause", TransitionAnimation.None);
        yield return new UITransitionDefinition("PauseMenu", "Settings", "settings", TransitionAnimation.Fade);
    }
}
```

Назначить в `UISystem.sceneInitializerComponent` в инспекторе.

### Генерация префабов

В Unity Editor: **ProtoSystem → UI → Generate Window → [тип]**

После генерации: **UISystemConfig → Scan & Add Prefabs**

---

## 4. UIWindowPrefabGenerator

### Хелперы для создания UI элементов

```csharp
// Доступны в UIWindowPrefabGenerator:
CreateWindowBase(name, size)                    // Базовое окно
CreateWindowBase(name, size, bgAlpha)           // С кастомной альфой
CreateText(name, parent, text, fontSize)        // TextMeshPro
CreateButton(name, parent, text, size)          // Кнопка
CreateSlider(name, parent, label)               // Слайдер
CreateSettingsSlider(name, parent, label)       // Слайдер с текстом значения
CreateToggle(name, parent, label)               // Чекбокс
CreateDropdown(name, parent, label)             // Выпадающий список
CreateScrollView(name, parent)                  // ScrollView
CreateSectionLabel(name, parent, text)          // Заголовок секции
```

### Структура Dropdown (важно!)

TMP_Dropdown требует строгую иерархию:

```
Dropdown
├── Label (TMP_Text) — caption
├── Arrow (Image)
└── Template (inactive!)
    └── Viewport
        └── Content
            └── Item (с Toggle!)
                ├── Item Background (Image)
                ├── Item Checkmark (Image)
                └── Item Label (TMP_Text)
```

**Критично:** Item должен иметь компонент `Toggle`, иначе ошибка:
> "The dropdown template is not valid. The template must have a child GameObject with a Toggle component serving as the item."

---

## 5. Sound System

### Быстрая настройка

**ProtoSystem → Sound → Sound Setup Wizard**

Wizard создаёт всё автоматически:
- SoundManagerConfig, SoundLibrary, AudioMixer
- 19 готовых UI звуков (процедурная генерация)
- UISoundScheme с настроенными ID

### API

```csharp
// Воспроизведение
SoundManagerSystem.Play("ui_click");
SoundManagerSystem.Play("explosion", transform.position);
SoundManagerSystem.Play("footstep", position, volume: 0.8f, pitch: 1.1f);

// Музыка
SoundManagerSystem.PlayMusic("battle_theme", fadeIn: 2f);
SoundManagerSystem.CrossfadeMusic("peaceful", duration: 3f);
SoundManagerSystem.StopMusic(fadeOut: 1f);

// Громкость
SoundManagerSystem.SetVolume(SoundCategory.Music, 0.5f);
SoundManagerSystem.SetVolume(SoundCategory.SFX, 1.0f);
SoundManagerSystem.SetMute(true);

// Snapshots
SoundManagerSystem.SetSnapshot(SoundSnapshotPreset.Underwater);
SoundManagerSystem.ClearSnapshot(SoundSnapshotPreset.Underwater);

// Банки (ленивая загрузка)
await SoundManagerSystem.LoadBankAsync("level_1_sounds");
SoundManagerSystem.UnloadBank("level_1_sounds");

// Музыкальные параметры
SoundManagerSystem.SetMusicParameter("intensity", 0.8f);
```

### Конфигурация

| Файл | Назначение | Обязательно |
|------|------------|-------------|
| SoundManagerConfig | Главный конфиг | Да |
| SoundLibrary | Хранилище звуков | Да |
| AudioMixer | Управление громкостью | Рекомендуется |
| UISoundScheme | Автозвуки для UI | Опционально |
| GameSessionSoundScheme | Автозвуки для игры | Опционально |

### Sound Entry

```csharp
public class SoundEntry
{
    public string id;              // Уникальный ID
    public SoundCategory category; // Music, SFX, Voice, Ambient, UI
    public AudioClip clip;
    public float volume = 1f;
    public float pitch = 1f;
    public float pitchVariation;
    public bool loop;
    public bool spatial;           // 3D звук
    public SoundPriority priority;
    public float cooldown;
}
```

### Компоненты

| Компонент | Назначение |
|-----------|------------|
| PlaySoundOn | Триггер звука без кода |
| MusicZone | Зона смены музыки |
| AmbientZone | 3D ambient с fade |
| SoundEmitter | Для Animator/UnityEvents |

### Сгенерированные UI звуки (19 шт)

| ID | Описание |
|----|----------|
| ui_whoosh | Открытие окна |
| ui_close | Закрытие окна |
| ui_modal_open | Открытие модального |
| ui_modal_close | Закрытие модального |
| ui_click | Клик кнопки |
| ui_hover | Наведение |
| ui_disabled | Неактивная кнопка |
| ui_navigate | Навигация |
| ui_back | Назад |
| ui_tab | Переключение вкладки |
| ui_success | Успех |
| ui_error | Ошибка |
| ui_warning | Предупреждение |
| ui_notification | Уведомление |
| ui_slider | Слайдер |
| ui_toggle_on | Toggle вкл |
| ui_toggle_off | Toggle выкл |
| ui_dropdown | Dropdown |
| ui_select | Выбор |

### Правила

✅ **DO:**
- Использовать Setup Wizard для быстрой настройки
- ID звуков: `category_name` (ui_click, sfx_explosion, music_battle)
- Проверять валидацию в редакторах
- Использовать банки для больших проектов (100+ звуков)

❌ **DON'T:**
- Хардкодить AudioClip в компонентах — использовать SoundLibrary
- Прямые вызовы AudioSource.Play() — использовать SoundManagerSystem
- Магические строки — определять константы для ID

---

## 6. Управление временем и курсором

### UITimeManager

```csharp
// Счётчик-based пауза (несколько окон могут запрашивать паузу)
UITimeManager.Instance.RequestPause();   // +1 к счётчику
UITimeManager.Instance.ReleasePause();   // -1 к счётчику
UITimeManager.Instance.ResetAllPauses(); // Сброс в 0, возврат времени

// Автоматически вызывается UINavigator при PauseGame = true
```

### CursorManagerSystem

```csharp
// Применить режим для окна (со стеком)
CursorManagerSystem.Instance.ApplyWindowCursorMode(WindowCursorMode.Visible);

// Восстановить предыдущее состояние
CursorManagerSystem.Instance.RestoreWindowCursorMode();

// Принудительно установить режим
CursorManagerSystem.Instance.ForceApplyCursorMode(WindowCursorMode.Locked);
```

---

## 7. Файловая организация проекта

```
Assets/ProjectName/
├── Scripts/
│   ├── Events/
│   │   └── EventIds.cs              # ID событий проекта
│   ├── Systems/
│   │   └── MySystem/
│   │       ├── MySystem.cs          # Основной класс
│   │       └── Commands/            # Команды системы
│   └── UI/
│       ├── Windows/
│       │   └── MyWindow.cs          # Кастомные окна
│       └── Initializers/
│           └── GameplayInitializer.cs
├── Settings/
│   ├── Sound/
│   │   ├── SoundManagerConfig.asset
│   │   ├── SoundLibrary.asset
│   │   └── Audio/
│   └── UI/
│       └── UISystemConfig.asset
├── Resources/
│   └── UI/
│       └── Prefabs/                 # UI префабы
└── Scenes/
```

---

## 8. Чеклист интеграции

### Новый проект

- [ ] Создать `EventIds.cs` с событиями проекта
- [ ] Добавить `SystemInitializationManager` на сцену
- [ ] Создать `UISystemConfig` (Create → ProtoSystem → UI System Config)
- [ ] Сгенерировать базовые UI префабы
- [ ] Настроить `UISystem` на сцене
- [ ] Добавить `IUISceneInitializer` для каждой сцены

### Новое окно

1. Создать класс с `[UIWindow]` атрибутом
2. Добавить в генератор или создать префаб вручную
3. Пересканировать префабы в UISystemConfig
4. Добавить переходы в `IUISceneInitializer` или вручную

### Новая система

1. Наследовать от `InitializableSystemBase` или `NetworkInitializableSystem`
2. Указать `SystemId` и `DisplayName`
3. Добавить `[Dependency]` для зависимостей
4. Реализовать `InitializeAsync()` с `ReportProgress()`
5. Подписаться на события в `InitEvents()`
6. Добавить на сцену и зарегистрировать в `SystemInitializationManager`

### Sound System

1. **ProtoSystem → Sound → Sound Setup Wizard**
2. Добавить `SoundManagerSystem` на сцену
3. Назначить `SoundManagerConfig`
4. Добавить свои звуки в `SoundLibrary`

---

## 9. Логирование (ProtoLogger)

### Обзор

ProtoSystem использует централизованную систему логирования `ProtoLogger` с:
- **Уровни** — флаги (можно комбинировать): `Errors`, `Warnings`, `Info`, `Verbose`
- **Категории** — типы сообщений: `Initialization`, `Dependencies`, `Events`, `Runtime`
- **Фильтры** — `All`, `Whitelist`, `Blacklist` по системам
- **Per-system настройки** — в инспекторе SystemInitializationManager

### API логирования

```csharp
public class MySystem : InitializableSystemBase
{
    public override string SystemId => "my_system";
    
    public override async Task<bool> InitializeAsync()
    {
        // Логирование с категорией и уровнем
        LogInfo(LogCategory.Initialization, "Начало инициализации");
        LogWarning(LogCategory.Initialization, "Что-то подозрительно");
        LogError(LogCategory.Initialization, "Критическая ошибка!");
        
        // Runtime логи
        LogInfo(LogCategory.Runtime, $"Обработано {count} объектов");
        
        // События
        LogInfo(LogCategory.Events, $"Получено событие {eventId}");
        
        // Зависимости
        LogInfo(LogCategory.Dependencies, "Зависимость разрешена");
        
        return true;
    }
}
```

### Уровни логирования (LogLevel) — ФЛАГИ

```csharp
[Flags]
public enum LogLevel
{
    None = 0,
    Errors = 1 << 0,      // Критические ошибки
    Warnings = 1 << 1,    // Предупреждения
    Info = 1 << 2,        // Информационные сообщения
    Verbose = 1 << 3,     // Подробные отладочные данные
    All = Errors | Warnings | Info | Verbose
}

// Можно комбинировать:
LogLevel level = LogLevel.Errors | LogLevel.Warnings;  // Только ошибки и предупреждения
LogLevel level = LogLevel.Errors | LogLevel.Info;      // Ошибки и инфо, без предупреждений
```

### Категории (LogCategory) — ФЛАГИ

```csharp
[Flags]
public enum LogCategory
{
    None = 0,
    Initialization = 1 << 0,  // Init: Инициализация системы
    Dependencies = 1 << 1,    // Dep: Разрешение зависимостей
    Events = 1 << 2,          // Event: Подписка/публикация событий
    Runtime = 1 << 3,         // Run: Runtime логика
    All = Initialization | Dependencies | Events | Runtime
}
```

### Настройка в инспекторе

В `SystemInitializationManager` → вкладка "📝 Логи":

1. **Глобальные настройки** (toolbar):
   - Кнопки уровней: `✓ Err`, `✓ Warn`, `✓ Info`
   - Кнопки категорий: `✓ Init`, `✓ Dep`, `✓ Event`, `✓ Run`
   - Tri-state: ✓ = все вкл, ○ = все выкл, ◐ = частично

2. **Per-system настройки** (каждая система):
   - Чекбокс логирования
   - Кнопки уровней и категорий
   - Цвет логов в консоли (ColorPicker)

3. **Визуальные индикаторы**:
   - 📦 Синий фон — системы ProtoSystem
   - 🎮 Зелёный фон — кастомные системы проекта

### Методы InitializableSystemBase

```csharp
// Базовые методы (категория обязательна)
protected void LogInfo(LogCategory category, string message);
protected void LogWarning(LogCategory category, string message);
protected void LogError(LogCategory category, string message);

// С форматированием
LogInfo(LogCategory.Runtime, $"Player {playerId} joined at {position}");

// Условное логирование (проверяет настройки перед форматированием)
if (ProtoLogger.ShouldLog(SystemId, LogCategory.Runtime, LogLevel.Verbose))
{
    LogInfo(LogCategory.Runtime, ExpensiveDebugString());
}
```

### Прямой доступ к ProtoLogger

```csharp
// Основной метод (порядок: systemId, category, level, message)
ProtoLogger.Log("my_system", LogCategory.Runtime, LogLevel.Info, "Message");

// Shortcut методы (категория + уровень зафиксированы)
ProtoLogger.LogInit("my_system", "Initializing...");      // Initialization, Info
ProtoLogger.LogDep("my_system", "Dependency resolved");   // Dependencies, Info
ProtoLogger.LogEvent("my_system", "Event received");      // Events, Info
ProtoLogger.LogRuntime("my_system", "Processing...");     // Runtime, Info

// Ошибки и предупреждения (всегда Runtime категория)
ProtoLogger.LogError("my_system", "Critical error!");
ProtoLogger.LogWarning("my_system", "Something suspicious");

// Проверка перед логированием (для дорогих операций)
if (ProtoLogger.ShouldLog("my_system", LogCategory.Runtime, LogLevel.Verbose))
{
    ProtoLogger.Log("my_system", LogCategory.Runtime, LogLevel.Verbose, BuildExpensiveMessage());
}
```

### ⚠️ ОБЯЗАТЕЛЬНОЕ ТРЕБОВАНИЕ

**Все классы пакета ProtoSystem ДОЛЖНЫ использовать ProtoLogger вместо Debug.Log!**

Это относится к:
- Системы (`*System.cs`)
- Конфиги (`*Config.cs`) 
- Контейнеры (`*Container.cs`)
- Вспомогательные Runtime классы
- UI компоненты
- EventBus классы

**Исключения:** Editor код (`/Editor/`)

### Правила логирования

⚠️ **ВАЖНО: В системах ProtoSystem использовать ТОЛЬКО ProtoLogger!**

Все системы, наследующиеся от `InitializableSystemBase`, `NetworkInitializableSystem`, `MonoEventBus` должны использовать методы `LogInfo()`, `LogWarning()`, `LogError()` вместо `Debug.Log()`.

✅ **DO:**
- Использовать `LogInfo()`, `LogWarning()`, `LogError()` в системах
- Указывать правильную категорию для контекста
- Использовать `LogCategory.Initialization` в `InitializeAsync()`
- Использовать `LogCategory.Events` в обработчиках событий
- Использовать `LogCategory.Runtime` для игровой логики
- Проверять `ShouldLog()` перед дорогим форматированием

❌ **DON'T:**
- `Debug.Log()` / `Debug.LogWarning()` / `Debug.LogError()` в системах ProtoSystem — **ЗАПРЕЩЕНО**
- Логировать в tight loops без проверки `ShouldLog()`
- Использовать неправильную категорию (Events для Init и т.д.)

```csharp
// ❌ НЕПРАВИЛЬНО — не использовать в системах ProtoSystem!
Debug.Log("Система инициализирована");
Debug.LogWarning("Что-то пошло не так");
Debug.LogError("Критическая ошибка");

// ✅ ПРАВИЛЬНО — использовать ProtoLogger
LogInfo(LogCategory.Initialization, "Система инициализирована");
LogWarning(LogCategory.Initialization, "Что-то пошло не так");
LogError(LogCategory.Initialization, "Критическая ошибка");
```

### Пример системы с логированием

```csharp
public class InventorySystem : InitializableSystemBase
{
    public override string SystemId => "inventory";
    public override string DisplayName => "Inventory System";
    
    [Dependency(required: true)]
    private PlayerSystem _playerSystem;
    
    protected override void InitEvents()
    {
        LogInfo(LogCategory.Events, "Подписка на события инвентаря");
        AddEvent(Evt.Inventory.ItemAdded, OnItemAdded);
        AddEvent(Evt.Inventory.ItemRemoved, OnItemRemoved);
    }
    
    public override async Task<bool> InitializeAsync()
    {
        LogInfo(LogCategory.Initialization, "Загрузка данных инвентаря...");
        ReportProgress(0.3f);
        
        await LoadInventoryData();
        
        LogInfo(LogCategory.Initialization, $"Загружено {_items.Count} предметов");
        ReportProgress(1.0f);
        return true;
    }
    
    private void OnItemAdded(object payload)
    {
        var item = (ItemData)payload;
        LogInfo(LogCategory.Events, $"Добавлен предмет: {item.Name}");
    }
    
    public void UseItem(string itemId)
    {
        LogInfo(LogCategory.Runtime, $"Использование предмета: {itemId}");
        // ...
    }
}
```

---

## 10. Отладка

### EventBus
```csharp
EventBus.GetEventPath(eventId);  // Путь события для логов
```

### UISystem
```csharp
Debug.Log($"Current: {UISystem.Instance.CurrentWindow?.WindowId}");
Debug.Log($"Stack: {UISystem.Instance.Navigator.GetStackInfo()}");
```

### SoundSystem
- Runtime Debug секция в инспекторе SoundManagerSystem
- Кнопки "Test Click", "Test Success", "Stop All"
- Progress bars для громкости и активных звуков

### Системы
- Включить `verboseLogging` в инспекторе системы
- Кнопка "Анализировать зависимости" в `SystemInitializationManager`

---

## 11. Частые ошибки

| Ошибка | Причина | Решение |
|--------|---------|---------|
| "Dropdown template is not valid" | Item без Toggle | Пересоздать префаб через генератор |
| Окно не открывается | Нет в UISystemConfig | Scan & Add Prefabs |
| Курсор не блокируется | Конфликт систем | Удалить legacy код, использовать только CursorManagerSystem |
| Система не инициализируется | Не на сцене / циклическая зависимость | Добавить на сцену, проверить граф зависимостей |
| События не приходят | Неправильный ID / нет подписки | Проверить Evt enum, проверить InitEvents() |
| Звук не воспроизводится | ID не в библиотеке | Проверить SoundLibrary, использовать валидацию в редакторе |

---

## 12. Localization System (ProtoLocalization)

### Обзор

Wrapper над Unity Localization Package. Работает с `#if PROTO_HAS_LOCALIZATION` — без пакета возвращает fallback/ключи.

### API

```csharp
// Простой ключ (таблица по умолчанию)
Loc.Get("menu.play")                    // → "ИГРАТЬ"
Loc.Get("menu.play", "PLAY")            // с fallback
Loc.Get("Items", "sword.name")          // из таблицы Items

// Переменные
Loc.Get("kill.msg", ("enemy", name), ("count", 5))

// Множественное число (авто-.one/.few/.other)
Loc.GetPlural("enemies.killed", count)

// Вложенная локализованная ссылка
Loc.Get("found.item", ("item", Loc.Ref("Items", dynamicKey)))

// Язык
Loc.SetLanguage("en");
Loc.CurrentLanguage;  // "ru"
Loc.IsReady;
```

### События

```csharp
EventBus.Localization.LanguageChanged  // payload: LocaleChangedData
EventBus.Localization.Ready            // payload: null
EventBus.Localization.TableLoaded      // payload: string tableName
```

### Компонент LocalizeTMP

Добавить на GameObject с TMP_Text. Обновляется автоматически при смене языка.

```csharp
[RequireComponent(typeof(TMP_Text))]
public class LocalizeTMP : MonoBehaviour, IEventBus
{
    [SerializeField] private string table = "UI";
    [SerializeField] private string key;
    [SerializeField] private string fallback;
}
```

### Локализация ScriptableObject

Уникальные SO (Вариант B):
```csharp
public string titleKey;      // "credits.section.dev"
public string titleFallback; // "РАЗРАБОТКА"
// Использование: Loc.Get(titleKey, titleFallback)
```

Массовые SO (Вариант C):
```csharp
public string id = "railgun";  // Ключ = "weapon.{id}.name"
public string GetName() => Loc.Get($"weapon.{id}.name", nameFallback);
```

### Правила

✅ **DO:**
- Использовать `Loc.Get()` для всех отображаемых строк
- Именование ключей: `section.element.modifier`
- Для plural: отдельные ключи `.one`, `.few`, `.other`
- `LocalizeTMP` для статичных UI текстов
- Подписка на `Evt.Localization.LanguageChanged` для динамического текста

❌ **DON'T:**
- Хардкодить строки в UI
- Использовать Smart Strings ICU для plural forms
- Обращаться к Unity Localization API напрямую — использовать `Loc.*`

### AI Translation Workflow

**ProtoSystem → Localization → AI Translation**

1. **Export:** Выбрать таблицу, source/target язык → JSON
2. **Передать AI:** JSON содержит instructions, context, maxLength
3. **Validate:** Проверка переменных `{var}`, длины, пропущенных
4. **Import:** Запись переводов в StringTable

**StringMetadataDatabase** — опциональный SO с контекстом/тегами для каждого ключа:
```csharp
var meta = metadataDB.Find("menu.play");
meta.context;   // "Кнопка главного меню"
meta.maxLength; // 20
meta.tags;      // ["ui", "button"]
```

---

## 13. Анти-паттерны

❌ **Избегать:**

```csharp
// Магические числа для событий
EventBus.Publish(1001, data);

// FindObjectOfType вместо DI
var system = FindObjectOfType<MySystem>();

// Прямое управление Time.timeScale
Time.timeScale = 0;  // Использовать UITimeManager

// Прямое управление курсором
Cursor.lockState = CursorLockMode.Locked;  // Использовать CursorManagerSystem

// Хардкод AudioClip
audioSource.PlayOneShot(myClip);  // Использовать SoundManagerSystem

// Debug.Log в системах ProtoSystem — ЗАПРЕЩЕНО!
Debug.Log("Initialized");        // Использовать LogInfo()
Debug.LogWarning("Warning");     // Использовать LogWarning()
Debug.LogError("Error");         // Использовать LogError()

// Синхронная тяжёлая инициализация
public override Task<bool> InitializeAsync()
{
    HeavyWork();  // Блокирует поток!
    return Task.FromResult(true);
}
```

✅ **Правильно:**

```csharp
// Именованные события
EventBus.Publish(Evt.Combat.DamageDealt, data);

// Dependency Injection
[Dependency] private MySystem mySystem;

// UITimeManager для паузы
UITimeManager.Instance.RequestPause();

// CursorManagerSystem для курсора
CursorManagerSystem.Instance.ApplyWindowCursorMode(WindowCursorMode.Visible);

// SoundManagerSystem для звука
SoundManagerSystem.Play("ui_click");

// ProtoLogger для логирования в системах
LogInfo(LogCategory.Initialization, "Initialized");
LogWarning(LogCategory.Runtime, "Warning");
LogError(LogCategory.Runtime, "Error");

// Асинхронная инициализация
public override async Task<bool> InitializeAsync()
{
    await Task.Run(() => HeavyWork());
    return true;
}
```
