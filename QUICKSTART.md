# ProtoSystem — Quick Start Guide

Быстрая интеграция ProtoSystem в Unity проект.

## 1. Установка

Добавить пакет через Package Manager → Add package from git URL:
```
https://github.com/your-repo/ProtoSystem.git
```

Или скопировать папку `com.protosystem.core` в `Packages/`.

## 2. Базовая настройка

### 2.1 Создать EventIds

```csharp
// Assets/YourProject/Scripts/Events/EventIds.cs
namespace YourProject
{
    public static class Evt
    {
        public enum EventType
        {
            GameStarted,
            PlayerSpawned,
            EnemyKilled,
        }

        public static class Game
        {
            public const int Started = (int)EventType.GameStarted;
        }
        
        public static class Player
        {
            public const int Spawned = (int)EventType.PlayerSpawned;
        }
    }
}
```

### 2.2 Добавить SystemInitializationManager

1. Создать пустой GameObject "Systems"
2. Add Component → ProtoSystem → System Initialization Manager
3. В инспекторе нажать "Добавить ProtoSystem компоненты"

### 2.3 Настроить UISystem

#### Шаг 1: Создать окна с атрибутами

```csharp
using ProtoSystem.UI;

[UIWindow("main_menu", WindowType.Normal, WindowLayer.Windows, Level = 0)]
[UITransition("play", "GameHUD")]
[UITransition("settings", "Settings")]
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
}
```

#### Шаг 2: Сгенерировать префабы

**ProtoSystem → UI → Generate All Base Windows**

Создаст базовые окна: MainMenu, GameHUD, PauseMenu, Settings, GameOver, Statistics, Credits, Loading.

#### Шаг 3: Собрать граф

**ProtoSystem → UI → Rebuild Window Graph**

Автоматически:
- Сканирует все `[UIWindow]` атрибуты
- Собирает переходы из `[UITransition]`
- Находит префабы окон
- Создаёт UIWindowGraph в Resources/

#### Шаг 4: Проверить граф визуально

**ProtoSystem → UI → Window Graph Viewer**

- Интерактивный редактор графа
- Клик на ноду → информация об окне
- Клик на линию → информация о переходе
- Проверка достижимости окон

#### Шаг 5: Добавить UISystem на сцену

1. Add Component → UISystem
2. UIWindowGraph назначается автоматически из Resources/

## 3. Создание системы

```csharp
using ProtoSystem;
using YourProject;

public class GameSystem : InitializableSystemBase
{
    public override string SystemId => "game";
    public override string DisplayName => "Game System";
    
    protected override void InitEvents()
    {
        AddEvent(Evt.Player.Spawned, OnPlayerSpawned);
    }
    
    public override async Task<bool> InitializeAsync()
    {
        ReportProgress(0.5f);
        // Инициализация
        ReportProgress(1.0f);
        return true;
    }
    
    private void OnPlayerSpawned(object payload)
    {
        Debug.Log("Player spawned!");
    }
    
    public void StartGame()
    {
        EventBus.Publish(Evt.Game.Started, null);
    }
}
```

## 4. Создание UI окна

### Класс окна с атрибутами

```csharp
using ProtoSystem.UI;
using UnityEngine;
using UnityEngine.UI;

[UIWindow("my_dialog", WindowType.Modal, WindowLayer.Modals,
    Level = 2, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
[UITransition("back", "MainMenu")]
public class MyDialog : UIWindowBase
{
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text messageText;
    
    protected override void Awake()
    {
        base.Awake();
        closeButton?.onClick.AddListener(OnCloseClicked);
    }
    
    protected override void OnBeforeShow()
    {
        base.OnBeforeShow();
        // Подготовка перед показом
    }
    
    protected override void OnShow()
    {
        base.OnShow();
        // Действия после завершения анимации показа
    }
    
    private void OnCloseClicked()
    {
        UISystem.Back();
    }
    
    public override void OnBackPressed()
    {
        // Обработка Escape
        UISystem.Back();
    }
}
```

### Создание префаба

**Вариант 1: Автогенератор (для базовых окон)**
```csharp
// В Editor скрипте
UIWindowPrefabGenerator.GenerateMyDialog();
```

**Вариант 2: Вручную**
1. Создать Canvas с компонентом MyDialog
2. Настроить UI элементы
3. Сохранить в Assets/UI/Windows/

После создания префаба: **Rebuild Window Graph**

## 5. Навигация по графу

### Переходы по триггерам (рекомендуется)

```csharp
// Переход по триггеру
var result = UISystem.Navigate("play");        // MainMenu → GameHUD
if (result != NavigationResult.Success)
    Debug.LogWarning($"Navigate failed: {result}");

// Назад
UISystem.Back();
```

### Прямое открытие (legacy, не рекомендуется)

```csharp
// Прямое открытие окна
UISystem.Open("settings");
// Примечание: Open/Navigate не принимают context/payload в текущем API.

// Закрыть конкретное
UISystem.Instance.Navigator.Close("my_dialog");
```

## 6. Настройка переходов сцены

```csharp
using ProtoSystem.UI;
using System.Collections.Generic;

public class GameplayInitializer : UISceneInitializerBase
{
    // Window IDs are ids from [UIWindow("...")] attributes (graph ids), not prefab/class names.
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
    
    // Дополнительные переходы для этой сцены
    public override IEnumerable<UITransitionDefinition> GetAdditionalTransitions()
    {
        // GameHUD → PauseMenu
        yield return new UITransitionDefinition(
            "game_hud",
            "pause_menu",
            "pause",
            TransitionAnimation.Fade
        );
        
        // PauseMenu → Settings
        yield return new UITransitionDefinition(
            "pause_menu",
            "settings",
            "settings",
            TransitionAnimation.Slide
        );
        
        // Глобальный переход (из любого окна)
        yield return new UITransitionDefinition(
            "",
            "loading",
            "loading",
            TransitionAnimation.Fade
        );
    }
}
```

Назначить в `UISystem.sceneInitializerComponent` в инспекторе.

## 7. Работа с графом

### Визуализация

**ProtoSystem → UI → Window Graph Viewer**

- **Zoom:** Колёсико мыши
- **Pan:** Средняя кнопка мыши / ЛКМ на пустом месте
- **Select Node:** Клик по ноде → инспектор справа
- **Select Connection:** Клик по линии → информация о переходе
- **Drag Node:** ЛКМ перетаскивание ноды

### Валидация

**ProtoSystem → UI → Validate Window Graph**

Проверяет:
- Дублирующиеся ID окон
- Несуществующие целевые окна
- Отсутствующие префабы
- Недостижимые окна

### Обновление графа

После изменений окон/переходов:
1. **ProtoSystem → UI → Rebuild Window Graph**
2. **ProtoSystem → UI → Window Graph Viewer** — проверить визуально

## 8. Диалоговые окна

```csharp
// Сообщение
UISystem.Instance.Dialog.Message(
    "Hello World!",
    onClose: () => Debug.Log("Closed"),
    title: "Info"
);

// Подтверждение
UISystem.Instance.Dialog.Confirm(
    "Are you sure?",
    onYes: () => Debug.Log("Yes"),
    onNo: () => Debug.Log("No")
);

// Выбор
UISystem.Instance.Dialog.Choice(
    "Choose option",
    new[] { "A", "B", "C" },
    (index) => Debug.Log($"Selected: {index}")
);

// Ввод текста
UISystem.Instance.Dialog.Input(
    "Enter name:",
    (text) => Debug.Log($"Name: {text}"),
    placeholder: "Player Name"
);
```

## 9. Управление паузой и курсором

### Автоматическое управление

Через атрибут окна:
```csharp
[UIWindow("pause_menu", ..., 
    PauseGame = true,                      // Ставит игру на паузу
    CursorMode = WindowCursorMode.Visible  // Показывает курсор
)]
```

### Ручное управление

```csharp
// Пауза
UITimeManager.Instance.RequestPause();   // +1 к счётчику
UITimeManager.Instance.ReleasePause();   // -1 от счётчика
UITimeManager.Instance.ResetAllPauses(); // Сброс

// Курсор
CursorManagerSystem.Instance.ApplyWindowCursorMode(WindowCursorMode.Locked);
CursorManagerSystem.Instance.RestoreWindowCursorMode();
```

## 10. Чеклист

**Базовая настройка:**
- [ ] EventIds.cs создан с событиями проекта
- [ ] SystemInitializationManager на сцене
- [ ] Системы добавлены и зарегистрированы

**UISystem:**
- [ ] Классы окон с атрибутами [UIWindow] и [UITransition]
- [ ] Префабы окон созданы
- [ ] ProtoSystem → UI → Rebuild Window Graph выполнен
- [ ] Window Graph Viewer проверен визуально
- [ ] UISystem на сцене
- [ ] UISceneInitializerBase настроен для каждой сцены

**Проверка:**
- [ ] Navigate() работает
- [ ] Back() работает
- [ ] Escape = Back
- [ ] PauseGame работает
- [ ] CursorMode переключается
- [ ] Все окна достижимы (проверка в Graph Viewer)

## Полезные ссылки

- [README.md](README.md) — Обзор пакета
- [UISystem.md](Documentation~/UISystem.md) — Полная документация UI
- [UISystem_TestScenarios.md](Documentation~/UISystem_TestScenarios.md) — Тестовые сценарии
- [AI_INSTRUCTIONS.md](Documentation~/AI_INSTRUCTIONS.md) — Инструкции для ИИ
- [CHANGELOG.md](CHANGELOG.md) — История изменений

## Советы

### Best Practices

1. **Используйте Navigate() вместо Open()** — переходы по триггерам чётко определены
2. **Всегда Rebuild Graph** после изменения атрибутов окон
3. **Проверяйте визуально** — Graph Viewer помогает увидеть недостижимые окна
4. **ShowInGraph = false** для базовых классов — скрывает их из графа

### Частые проблемы

| Проблема | Решение |
|----------|---------|
| Navigate() не работает | Rebuild Window Graph |
| Окно не открывается | Проверить префаб в Resources |
| Курсор не блокируется | Проверить CursorMode в атрибуте |
| Пауза не снимается | UITimeManager.ResetAllPauses() |
| Граф пустой | Rebuild Window Graph |
