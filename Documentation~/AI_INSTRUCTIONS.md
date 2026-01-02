# ProtoSystem — AI Agent Instructions

> Инструкции для ИИ-ассистентов (Claude, GitHub Copilot, Cursor) по работе с ProtoSystem.

## Обзор фреймворка

ProtoSystem — модульный Unity фреймворк для прототипирования игр с:
- **EventBus** — Глобальная система событий
- **System Initialization** — DI с атрибутами `[Dependency]`
- **UISystem** — Граф-ориентированная UI навигация
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

### Правила

✅ **DO:**
- Использовать `Evt.Category.EventName` для всех событий
- Добавлять новые события в enum `EventType`
- Группировать события по категориям

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
│   └── allowedTransitions[] — граф переходов
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
    protected override void OnOpened(object context) { }
    protected override void OnClosed() { }
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
UISystem.Open("dialog", contextData);

// Закрыть и вернуться
UISystem.Back();

// Закрыть верхнее модальное
UISystem.CloseTopModal();

// Принудительно закрыть конкретное
UISystem.Close("window_id");

// Получить текущее окно
var current = UISystem.Instance.CurrentWindow;
```

### Создание кастомного окна

```csharp
[UIWindow("my_dialog", WindowType.Modal, WindowLayer.Modals,
    Level = 2, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
public class MyDialog : UIWindowBase
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    
    protected override void OnOpened(object context)
    {
        if (context is string title)
            titleText.text = title;
            
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);
    }
    
    protected override void OnClosed()
    {
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
public class GameplayInitializer : MonoBehaviour, IUISceneInitializer
{
    public string[] GetStartupWindows() => new[] { "game_hud" };
    
    public UITransition[] GetAdditionalTransitions() => new[]
    {
        new UITransition("game_hud", "pause_menu"),
        new UITransition("pause_menu", "settings"),
    };
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

## 5. Управление временем и курсором

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

## 6. Файловая организация проекта

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
├── Resources/
│   └── UI/
│       ├── Prefabs/                 # UI префабы
│       └── UISystemConfig.asset     # Конфигурация UI
└── Scenes/
```

---

## 7. Чеклист интеграции

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

---

## 8. Отладка

### EventBus
```csharp
EventBus.GetEventPath(eventId);  // Путь события для логов
```

### UISystem
```csharp
Debug.Log($"Current: {UISystem.Instance.CurrentWindow?.WindowId}");
Debug.Log($"Stack: {UISystem.Instance.Navigator.GetStackInfo()}");
```

### Системы
- Включить `verboseLogging` в инспекторе системы
- Кнопка "Анализировать зависимости" в `SystemInitializationManager`

---

## 9. Частые ошибки

| Ошибка | Причина | Решение |
|--------|---------|---------|
| "Dropdown template is not valid" | Item без Toggle | Пересоздать префаб через генератор |
| Окно не открывается | Нет в UISystemConfig | Scan & Add Prefabs |
| Курсор не блокируется | Конфликт систем | Удалить legacy код, использовать только CursorManagerSystem |
| Система не инициализируется | Не на сцене / циклическая зависимость | Добавить на сцену, проверить граф зависимостей |
| События не приходят | Неправильный ID / нет подписки | Проверить Evt enum, проверить InitEvents() |

---

## 10. Анти-паттерны

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

// Асинхронная инициализация
public override async Task<bool> InitializeAsync()
{
    await Task.Run(() => HeavyWork());
    return true;
}
```
