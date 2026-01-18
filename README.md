# ProtoSystem Core

Модульный Unity фреймворк для быстрого прототипирования с системой инициализации, событийной архитектурой и граф-based UI.

## Возможности

### Ядро
- **EventBus** — Глобальная система событий с группировкой и автоподпиской
- **System Initialization** — Атрибутивное внедрение зависимостей и управление порядком инициализации
- **Network Support** — Встроенная поддержка Netcode for GameObjects

### UI Система
- **UISystem** — Граф-ориентированная навигация между окнами с атрибутами
- **UIWindowGraph** — Автоматическая сборка графа из атрибутов UIWindow и UITransition
- **Window Graph Viewer** — Интерактивный визуальный редактор графа UI
- **UINavigator** — Стековая навигация с историей (Back, Close)
- **Window Prefab Generator** — Автогенерация UI префабов из редактора
- **UITimeManager** — Управление паузой игры для UI (счётчик-based)
- **CursorManagerSystem** — Стек состояний курсора

### Дополнительные системы
- **GameSessionSystem** — Управление жизненным циклом игровой сессии (старт, пауза, рестарт, завершение)
- **SettingsSystem** — Управление настройками (INI формат)
- **EffectsManager** — Система визуальных эффектов
- **SceneFlowSystem** — Управление переходами между сценами

## Быстрый старт

См. [QUICKSTART.md](QUICKSTART.md) для быстрой интеграции.

## Документация

- [ProtoSystem Guide](Documentation~/ProtoSystem-Guide.md) — Основная документация
- [UISystem Guide](Documentation~/UISystem.md) — Полная документация UI системы
- [UISystem Test Scenarios](Documentation~/UISystem_TestScenarios.md) — Тестовые сценарии
- [GameSession Guide](Documentation~/GameSession.md) — Система игровых сессий
- [SettingsSystem Guide](Documentation~/SettingsSystem.md) — Система настроек
- [AI Instructions](Documentation~/AI_INSTRUCTIONS.md) — Инструкции для ИИ-ассистентов
- [Changelog](CHANGELOG.md) — История изменений

## Установка

### Package Manager (Git URL)
```
https://github.com/your-repo/ProtoSystem.git
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
│   │   ├── Windows/       # Базовые классы окон
│   │   ├── Attributes/    # UIWindow, UITransition атрибуты
│   │   └── Graph/         # Граф переходов
│   ├── Settings/          # Система настроек
│   ├── Effects/           # Эффекты
│   ├── Cursor/            # Управление курсором
│   └── SceneFlow/         # Управление сценами
├── Editor/
│   ├── UI/                # UIWindowPrefabGenerator, Graph Viewer
│   ├── GameSession/       # Утилиты GameSession
│   └── Initialization/    # Инспекторы систем
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

## Примеры проектов

См. папку `/Tests` для примеров использования:
- `UISystemTests` — тесты навигации и графа
- `EventBusTests` — тесты событийной системы
- `InitializationTests` — тесты систем инициализации

## Лицензия

См. файл LICENSE.
