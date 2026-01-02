# UISystem — Документация

Граф-ориентированная система управления UI окнами с поддержкой паузы, курсора и стековой навигации.

## Архитектура

```
UISystem (синглтон)
├── UISystemConfig (ScriptableObject)
│   ├── windowPrefabs[] — зарегистрированные окна
│   ├── windowPrefabLabels[] — метки для автосканирования  
│   └── allowedTransitions[] — граф переходов
├── UINavigator — стековая навигация
├── UITimeManager — управление паузой (счётчик-based)
├── CursorManagerSystem — стек состояний курсора
└── UIWindowFactory — создание инстансов окон
```

## Быстрый старт

### 1. Создать UISystemConfig

**Create → ProtoSystem → UI System Config**

### 2. Сгенерировать базовые окна

**ProtoSystem → UI → Generate All Base Windows**

### 3. Добавить префабы в конфиг

В UISystemConfig нажать **Scan & Add Prefabs**

### 4. Настроить UISystem на сцене

1. Add Component → UISystem
2. Назначить UISystemConfig
3. Назначить sceneInitializerComponent (опционально)

## Типы окон

| Тип | Описание |
|-----|----------|
| `Normal` | Обычное окно с навигацией |
| `Modal` | Модальное, блокирует остальные |
| `Overlay` | Поверх всего, не блокирует |

## Слои (WindowLayer)

| Слой | SortOrder | Использование |
|------|-----------|---------------|
| Background | 0 | Фоновые элементы |
| HUD | 100 | Игровой интерфейс |
| Windows | 200 | Обычные окна |
| Modals | 300 | Модальные окна |
| Overlay | 400 | Поверх всего |

## Уровни (Level)

| Level | Поведение |
|-------|-----------|
| 0 | **Главные окна** — взаимоисключающие (открытие одного закрывает другое) |
| 1+ | **Стековые окна** — накладываются, Back() возвращает к предыдущему |

**Пример:**
- MainMenu (Level 0) и GameHUD (Level 0) — не могут быть открыты одновременно
- PauseMenu (Level 1) открывается поверх GameHUD
- Settings (Level 1) открывается поверх PauseMenu

## Атрибут UIWindow

```csharp
[UIWindow(
    "window_id",                          // Уникальный идентификатор
    WindowType.Normal,                    // Normal, Modal, Overlay
    WindowLayer.Windows,                  // Слой отрисовки
    Level = 1,                            // Уровень (0 = главные)
    PauseGame = true,                     // Ставить на паузу
    CursorMode = WindowCursorMode.Visible // Режим курсора
)]
public class MyWindow : UIWindowBase { }
```

## Создание окна

### Класс окна

```csharp
using UnityEngine;
using UnityEngine.UI;
using ProtoSystem.UI;

[UIWindow("my_window", WindowType.Normal, WindowLayer.Windows,
    Level = 1, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
public class MyWindow : UIWindowBase
{
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text titleText;
    
    protected override void OnOpened(object context)
    {
        // Вызывается при открытии
        if (context is string title)
            titleText.text = title;
            
        closeButton.onClick.AddListener(OnCloseClicked);
    }
    
    protected override void OnClosed()
    {
        // Вызывается при закрытии
        closeButton.onClick.RemoveAllListeners();
    }
    
    private void OnCloseClicked()
    {
        UISystem.Back();
    }
}
```

### Префаб

1. Создать Canvas с компонентом окна
2. Назначить ссылки в инспекторе
3. Добавить метку "UIWindow" или вручную в UISystemConfig

## Навигация

### Открытие окна

```csharp
// Простое открытие
UISystem.Open("settings");

// С контекстом
UISystem.Open("dialog", new DialogContext { Title = "Hello", Message = "World" });

// Проверка возможности перехода
if (UISystem.Instance.CanTransitionTo("settings"))
    UISystem.Open("settings");
```

### Закрытие

```csharp
// Вернуться к предыдущему (стек)
UISystem.Back();

// Закрыть верхнее модальное
UISystem.CloseTopModal();

// Закрыть конкретное окно
UISystem.Close("my_window");
```

### Текущее состояние

```csharp
var current = UISystem.Instance.CurrentWindow;
string windowId = current?.WindowId;

// Проверка открыто ли окно
bool isOpen = UISystem.Instance.IsWindowOpen("settings");
```

## IUISceneInitializer

Интерфейс для настройки UI при загрузке сцены:

```csharp
public class GameplayInitializer : MonoBehaviour, IUISceneInitializer
{
    // Окна для автоматического открытия при старте
    public string[] GetStartupWindows() => new[] { "game_hud" };
    
    // Дополнительные переходы для этой сцены
    public UITransition[] GetAdditionalTransitions() => new[]
    {
        new UITransition("game_hud", "pause_menu"),
        new UITransition("pause_menu", "settings"),
        new UITransition("pause_menu", "main_menu", allowOverride: true),
    };
}
```

Назначить компонент в `UISystem.sceneInitializerComponent` в инспекторе.

## Управление паузой

UITimeManager использует счётчик запросов:

```csharp
// Автоматически при PauseGame = true в атрибуте

// Ручное управление
UITimeManager.Instance.RequestPause();   // +1
UITimeManager.Instance.ReleasePause();   // -1
UITimeManager.Instance.ResetAllPauses(); // Сброс

// Свойства
bool isPaused = UITimeManager.Instance.IsPaused;
int count = UITimeManager.Instance.PauseRequestCount;
```

## Управление курсором

CursorManagerSystem использует стек состояний:

```csharp
// Автоматически при CursorMode в атрибуте

// Ручное управление
CursorManagerSystem.Instance.ApplyWindowCursorMode(WindowCursorMode.Visible);
CursorManagerSystem.Instance.RestoreWindowCursorMode();
CursorManagerSystem.Instance.ForceApplyCursorMode(WindowCursorMode.Locked);
```

| Режим | Описание |
|-------|----------|
| `None` | Не менять |
| `Locked` | Заблокирован в центре, невидим |
| `Visible` | Видимый, свободный |
| `Hidden` | Невидимый, но свободный |
| `Confined` | Ограничен окном игры |

## Генератор префабов

### Меню

**ProtoSystem → UI → Generate Window → [тип]**

Доступные типы:
- MainMenu, GameHUD, PauseMenu, Settings
- GameOver, Statistics, Credits, Loading

### Программно

```csharp
// В Editor скрипте
UIWindowPrefabGenerator.GenerateSettings();
UIWindowPrefabGenerator.GenerateAllBaseWindows();
```

### Хелперы генератора

```csharp
CreateWindowBase(name, size)              // Базовое окно
CreateWindowBase(name, size, bgAlpha)     // С альфой фона
CreateText(name, parent, text, fontSize)  // TextMeshPro
CreateButton(name, parent, text, size)    // Кнопка
CreateSlider(name, parent, label)         // Слайдер
CreateSettingsSlider(name, parent, label) // Слайдер + текст значения
CreateToggle(name, parent, label)         // Чекбокс
CreateDropdown(name, parent, label)       // Выпадающий список
CreateScrollView(name, parent)            // ScrollView
CreateSectionLabel(name, parent, text)    // Заголовок секции
```

## Базовые окна

| Окно | Level | PauseGame | CursorMode |
|------|-------|-----------|------------|
| MainMenu | 0 | false | Visible |
| GameHUD | 0 | false | Locked |
| PauseMenu | 1 | true | Visible |
| Settings | 1 | true | Visible |
| GameOver | 1 | true | Visible |
| Statistics | 1 | true | Visible |
| Credits | 1 | false | Visible |
| Loading | Overlay | false | None |

## Отладка

### Логи

```csharp
// В UINavigator включён verbose logging
Debug.Log($"[UINavigator] Opened '{windowId}'");
Debug.Log($"[UINavigator] Back from '{windowId}'");
```

### Проверка стека

```csharp
var nav = UISystem.Instance.Navigator;
Debug.Log($"Stack depth: {nav.StackDepth}");
Debug.Log($"Current: {nav.CurrentWindow?.WindowId}");
```

### Частые проблемы

| Проблема | Решение |
|----------|---------|
| Окно не открывается | Проверить UISystemConfig, Scan & Add Prefabs |
| Переход не работает | Добавить в allowedTransitions или IUISceneInitializer |
| Курсор не блокируется | Проверить CursorMode в атрибуте, удалить legacy код |
| Пауза не снимается | Вызвать UITimeManager.ResetAllPauses() |
| Dropdown не работает | Пересоздать префаб (Item должен иметь Toggle) |

## Миграция с OnGUI

1. Создать класс окна с `[UIWindow]` атрибутом
2. Перенести логику отрисовки в UGUI/TextMeshPro
3. Удалить `OnGUI()` метод
4. Добавить в UISystemConfig
5. Использовать `UISystem.Open()` вместо флагов
