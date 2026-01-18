# UISystem — Документация

Граф-ориентированная система управления UI окнами с поддержкой паузы, курсора и стековой навигации.

## Архитектура

```
UISystem (синглтон)
├── UIWindowGraph (ScriptableObject) — граф окон и переходов
│   ├── windows[] — определения окон
│   ├── transitions[] — локальные переходы
│   └── globalTransitions[] — глобальные переходы
├── UINavigator — стековая навигация
├── UITimeManager — управление паузой (счётчик-based)
├── CursorManagerSystem — стек состояний курсора
└── UIWindowFactory — создание инстансов окон
```

## Быстрый старт

### 1. Создать окна с атрибутами

```csharp
[UIWindow("main_menu", WindowType.Normal, WindowLayer.Windows, Level = 0)]
[UITransition("play", "GameHUD")]
[UITransition("settings", "Settings")]
public class MainMenu : UIWindowBase { }
```

### 2. Создать префабы окон

**ProtoSystem → UI → Generate All Base Windows**

### 3. Собрать граф

**ProtoSystem → UI → Rebuild Window Graph**

Автоматически сканирует все классы с `[UIWindow]` атрибутом и строит граф переходов.

### 4. Визуализировать граф

**ProtoSystem → UI → Window Graph Viewer**

Интерактивный редактор графа с:
- Визуализацией нод и связей
- Кликабельными переходами
- Детальной информацией в инспекторе
- Проверкой достижимости окон

### 5. Настроить UISystem на сцене

1. Add Component → UISystem
2. UIWindowGraph создаётся автоматически в Resources/UIWindowGraph
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

## Структура префабов окон

> ⚠️ **ВАЖНО:** Префабы окон **НЕ должны** содержать свой собственный `Canvas`!

UISystem создаёт единый `UISystem_Canvas` с разделением на слои (`Layer_HUD`, `Layer_Windows`, `Layer_Modals` и т.д.). При открытии окна, UISystem инстанцирует префаб как дочерний объект соответствующего слоя.

### ❌ Неправильно (вложенный Canvas)

```
UISystem_Canvas (Canvas, CanvasScaler)
└── Layer_HUD
    └── MyGameHUD (Canvas, CanvasScaler) ← ПРОБЛЕМА!
        └── TopLeft
        └── Bottom
```

**Проблема:** Вложенный Canvas не наследует размер экрана от родителя. Элементы со stretch-anchors получат нулевую ширину/высоту.

### ✅ Правильно (RectTransform без Canvas)

```
UISystem_Canvas (Canvas, CanvasScaler)
└── Layer_HUD
    └── MyGameHUD (RectTransform, stretch) ← Правильно!
        └── TopLeft
        └── Bottom
```

### Правильная структура корня префаба

```csharp
// Корневой объект окна - обычный RectTransform
var rootGO = new GameObject("MyWindow");
var rootRect = rootGO.AddComponent<RectTransform>();

// Растягиваем по всему родителю (слою UISystem)
rootRect.anchorMin = Vector2.zero;
rootRect.anchorMax = Vector2.one;
rootRect.offsetMin = Vector2.zero;  // Left, Bottom = 0
rootRect.offsetMax = Vector2.zero;  // Right, Top = 0
rootRect.pivot = new Vector2(0.5f, 0.5f);

// Добавляем компонент окна
rootGO.AddComponent<MyWindowClass>();
```

### Checklist для префабов окон

- [ ] Корень префаба — `RectTransform` (не Canvas)
- [ ] `anchorMin = (0, 0)`, `anchorMax = (1, 1)` — растяжение по родителю
- [ ] `offsetMin = offsetMax = (0, 0)` — без отступов
- [ ] Компонент `UIWindowBase` (или наследник) на корне
- [ ] Дочерние элементы используют anchor-based позиционирование

## Уровни (Level)

| Level | Поведение |
|-------|-----------|
| 0 | **Главные окна** — взаимоисключающие (открытие одного закрывает другое) |
| 1+ | **Стековые окна** — накладываются, Back() возвращает к предыдущему |

**Пример:**
- MainMenu (Level 0) и GameHUD (Level 0) — не могут быть открыты одновременно
- PauseMenu (Level 1) открывается поверх GameHUD
- Settings (Level 1) открывается поверх PauseMenu

## Атрибуты

### UIWindow

```csharp
[UIWindow(
    "window_id",                          // Уникальный идентификатор
    WindowType.Normal,                    // Normal, Modal, Overlay
    WindowLayer.Windows,                  // Слой отрисовки
    Level = 1,                            // Уровень (0 = главные)
    PauseGame = true,                     // Ставить на паузу
    CursorMode = WindowCursorMode.Visible,// Режим курсора
    ShowInGraph = true                    // Показывать в графе (default: true)
)]
public class MyWindow : UIWindowBase { }
```

**ShowInGraph** — скрывает базовые/абстрактные окна из графа (например, `GameHUDWindow`).

### UITransition

```csharp
[UITransition(
    "trigger_name",                       // Имя триггера
    "target_window_id",                   // ID целевого окна
    TransitionAnimation.Fade              // Анимация (опционально)
)]
```

**Примеры:**
```csharp
[UIWindow("main_menu", WindowType.Normal, WindowLayer.Windows, Level = 0)]
[UITransition("play", "GameHUD")]           // Local: MainMenu → GameHUD
[UITransition("settings", "Settings")]      // Local: MainMenu → Settings
[UITransition("credits", "Credits")]        // Local: MainMenu → Credits
public class MainMenu : UIWindowBase { }
```

Для **глобальных переходов** (из любого окна):
```csharp
[UITransition("", "Loading")]  // Global: Any → Loading
```

## Создание окна

### Класс окна

```csharp
using UnityEngine;
using UnityEngine.UI;
using ProtoSystem.UI;

[UIWindow("my_window", WindowType.Normal, WindowLayer.Windows,
    Level = 1, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
[UITransition("back", "MainMenu")]
public class MyWindow : UIWindowBase
{
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text titleText;
    
    protected override void Awake()
    {
        base.Awake();
        closeButton?.onClick.AddListener(OnCloseClicked);
    }
    
    protected override void OnBeforeShow()
    {
        // Вызывается перед показом окна
        base.OnBeforeShow();
    }
    
    protected override void OnShow()
    {
        // Вызывается после завершения анимации показа
        base.OnShow();
    }
    
    protected override void OnBeforeHide()
    {
        // Вызывается перед скрытием окна
        base.OnBeforeHide();
    }
    
    protected override void OnHide()
    {
        // Вызывается после завершения анимации скрытия
        base.OnHide();
    }
    
    private void OnCloseClicked()
    {
        UISystem.Back();
    }
    
    public override void OnBackPressed()
    {
        // Обработка Back (Escape)
        UISystem.Back();
    }
}
```

### Префаб

1. Создать Canvas с компонентом окна
2. Назначить ссылки в инспекторе
3. Префаб находится автоматически если в нём есть UIWindowBase компонент

## Навигация

### Открытие окна по триггеру

```csharp
// По триггеру (из UITransition)
var result = UISystem.Navigate("play");        // Переход по триггеру "play"
if (result != NavigationResult.Success)
    Debug.LogWarning($"Navigate failed: {result}");
```

### Открытие окна напрямую (legacy)

```csharp
// Прямое открытие (не рекомендуется, используйте Navigate)
UISystem.Open("settings");

// Примечание: текущий API Open/Navigate не принимает context/payload.
// Для диалогов используйте UISystem.Instance.Dialog, для данных — состояние/события.
```

### Закрытие

```csharp
// Вернуться к предыдущему (стек)
UISystem.Back();

// Закрыть конкретное окно (overlay / верхнее modal / текущее в стеке)
UISystem.Instance.Navigator.Close("my_window");
```

### Текущее состояние

```csharp
var current = UISystem.Instance.CurrentWindow;
string windowId = current?.WindowId;

// Проверка открыто ли окно
bool isOpen = UISystem.Instance.Navigator.GetWindow("settings") != null;
```

## UISceneInitializerBase

Базовый класс для настройки UI при загрузке сцены:

```csharp
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
        yield return new UITransitionDefinition(
            "game_hud",
            "pause_menu",
            "pause",
            TransitionAnimation.Fade
        );
        
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

Назначить компонент в `UISystem.sceneInitializerComponent` в инспекторе.

## Граф окон (UIWindowGraph)

### Автоматическая сборка

**ProtoSystem → UI → Rebuild Window Graph**

Сканирует:
- Все классы с `[UIWindow]` атрибутом
- Все `[UITransition]` атрибуты на классах
- Все сцены с `UISceneInitializerBase` компонентами
- Все префабы с `UISceneInitializerBase` компонентами

### Визуализатор графа

**ProtoSystem → UI → Window Graph Viewer**

**Возможности:**
- **Интерактивные ноды** — клик для выбора, перетаскивание мышью
- **Цветовая кодировка:**
  - Зелёный — стартовое окно
  - Синий — выбранное окно
  - Красный — модальное окно
  - Серый полупрозрачный — недостижимое окно
- **Толстые яркие линии** (10px) с большими стрелками (12px)
- **Mindmap style** — линии выходят из разных точек нод
- **Кликабельные связи** — клик по линии/лейблу для просмотра информации
- **Инспектор:**
  - Для нод: ID, тип, слой, префаб, входящие/исходящие переходы
  - Для связей: направление, триггер, двусторонность, префабы окон
- **Зум и навигация** — колёсико мыши, средняя кнопка для панорамирования
- **Автоматическая группировка** по уровням
- **Проверка достижимости** — помечает недостижимые окна

**Горячие клавиши:**
- **Rebuild** — пересобрать граф
- **Arrange** — авторазмещение нод по уровням
- **Validate** — проверить корректность графа

### Валидация графа

**ProtoSystem → UI → Validate Window Graph**

Проверяет:
- Дублирующиеся ID окон
- Несуществующие целевые окна в переходах
- Отсутствующие префабы
- Недостижимые окна

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

| Окно | Level | PauseGame | CursorMode | ShowInGraph |
|------|-------|-----------|------------|-------------|
| MainMenu | 0 | false | Visible | true |
| GameHUD (base) | 0 | false | Locked | false |
| PauseMenu | 1 | true | Visible | true |
| Settings | 1 | true | Visible | true |
| GameOver | 1 | true | Visible | true |
| Statistics | 1 | true | Visible | true |
| Credits | 1 | false | Visible | true |
| Loading | Overlay | false | None | true |

**Примечание:** `GameHUDWindow` — базовый класс с `ShowInGraph = false`, его наследники (например, `KM_GameHUD`) показываются в графе.

## Диалоговые окна

### Встроенные методы

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
    onNo: () => Debug.Log("No"),
    title: "Confirm"
);

// Выбор (Choice)
UISystem.Instance.Dialog.Choice(
    "Choose option",
    new[] { "Option A", "Option B", "Option C" },
    (index) => Debug.Log($"Selected: {index}"),
    title: "Choice"
);

// Ввод текста
UISystem.Instance.Dialog.Input(
    "Enter your name:",
    (text) => Debug.Log($"Entered: {text}"),
    placeholder: "Name",
    title: "Input"
);
```

## Отладка

### Логи

```csharp
// В UINavigator включён verbose logging
Debug.Log($"[UINavigator] Opened '{windowId}'");
Debug.Log($"[UINavigator] Back from '{windowId}'");
Debug.Log($"[UINavigator] Navigate via trigger '{trigger}' → '{targetId}'");
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
| Окно не открывается | Rebuild Window Graph, проверить префаб |
| Переход не работает | Добавить [UITransition] атрибут или GetAdditionalTransitions() |
| Курсор не блокируется | Проверить CursorMode в атрибуте, удалить legacy код |
| Пауза не снимается | Вызвать UITimeManager.ResetAllPauses() |
| Граф пустой | ProtoSystem → UI → Rebuild Window Graph |
| Недостижимые окна | Проверить в Graph Viewer, добавить переходы |
| **UI элементы имеют нулевой размер / неправильная позиция** | Убрать Canvas с префаба окна! См. раздел "Структура префабов окон" |
| Stretch-anchors не работают | Проверить что корень префаба = RectTransform (не Canvas), anchors = (0,0)-(1,1) |

## Best Practices

### 1. Используйте Navigate() вместо Open()

```csharp
// ✅ Хорошо - переход по триггеру
UISystem.Navigate("settings");

// ❌ Плохо - прямое открытие (устарело)
UISystem.Open("Settings");
```

### 2. Всегда определяйте переходы через атрибуты

```csharp
[UIWindow("main_menu", ...)]
[UITransition("play", "GameHUD")]        // ✅
[UITransition("settings", "Settings")]   // ✅
public class MainMenu : UIWindowBase { }
```

### 3. Используйте ShowInGraph для базовых классов

```csharp
// Базовый класс - скрыт из графа
[UIWindow("GameHUD", ..., ShowInGraph = false)]
public class GameHUDWindow : UIWindowBase { }

// Конкретная реализация - видна в графе
[UIWindow("KM_GameHUD", ...)]
public class KM_GameHUD : GameHUDWindow { }
```

### 4. Проверяйте граф визуально

После изменений окон/переходов:
1. **Rebuild Window Graph**
2. **Window Graph Viewer** — проверить визуально
3. **Validate** — проверить ошибки

### 5. Используйте UISceneInitializerBase для сцен-специфичных переходов

```csharp
public class BattleSceneInitializer : UISceneInitializerBase
{
    public override IEnumerable<UITransitionDefinition> GetAdditionalTransitions()
    {
        // Переходы только для боевой сцены
        yield return new UITransitionDefinition("battle_hud", "victory_screen", "win");
        yield return new UITransitionDefinition("battle_hud", "defeat_screen", "lose");
    }
}
```

## Миграция с UISystemConfig

Старая система использовала `UISystemConfig` + ручная регистрация окон.

### Текущая система (граф-based):

1. **Оставить** `UISystemConfig` — он хранит ссылки на prefab'ы окон и общие UI prefab'ы (dialog/toast/tooltip и т.д.)
2. **Добавить** атрибуты к классам окон (`[UIWindow]`, `[UITransition]`, опционально `[UIGlobalTransition]`)
3. **Rebuild** Window Graph в редакторе и/или убедиться, что `UISystemConfig.windowPrefabs` заполнен (Scan & Add Prefabs)
4. **Предпочитать** `Navigate()` вместо прямого `Open()`

```csharp
// Старый код
UISystem.Open("settings");

// Новый код
UISystem.Navigate("settings");
```

UIWindowGraph создаётся автоматически и содержит всю информацию о графе.

> Практический нюанс: в runtime `UISystem` в первую очередь строит граф из `UISystemConfig.windowPrefabs` (актуальные ссылки на prefab'ы).
> Если список пустой — пытается использовать `windowGraphOverride`, затем Resources-граф.
