# UISystem — Документация

## Обзор

`UISystem` — модуль ProtoSystem для управления UI с поддержкой:
- Графа переходов между окнами
- Модальных окон и Back-навигации
- Диалогов (Confirm/Input/Choice)
- Тостов (уведомлений)
- Тултипов
- Визуального редактора графа

---

## Быстрый старт

### 1. Создать граф окон

```
Assets → Create → ProtoSystem → UI → Window Graph
```

### 2. Добавить UISystem на сцену

```
GameObject → Create Empty → Add Component → UISystem
```

Назначить:
- Window Graph
- UI System Config (опционально)

### 3. Создать окна с атрибутами

```csharp
[UIWindow("MainMenu", WindowType.Normal)]
[UITransition("Play", "GameHUD")]
[UITransition("Settings", "SettingsMenu")]
[UITransition("Quit", "ConfirmQuit")]
public class MainMenuWindow : UIWindowBase
{
    public Button playButton;
    public Button settingsButton;
    public Button quitButton;

    protected override void OnShow()
    {
        playButton.onClick.AddListener(() => Navigate("Play"));
        settingsButton.onClick.AddListener(() => Navigate("Settings"));
        quitButton.onClick.AddListener(() => Navigate("Quit"));
    }
}
```

### 4. Использование

```csharp
// Навигация по триггеру
UISystem.Navigate("Settings");

// Вернуться назад
UISystem.Back();

// Открыть окно напрямую
UISystem.Open("MainMenu");

// Диалоги
UISystem.Instance.Dialog.Confirm("Выйти?", 
    onYes: () => Application.Quit());

// Тосты
UISystem.Instance.Toast.ShowSuccess("Сохранено!");
```

---

## Архитектура

```
UISystem
├── UIWindowGraph (SO)     — Граф окон и переходов
├── UINavigator            — Управление навигацией
│   ├── WindowStack        — Стек обычных окон
│   └── ModalStack         — Стек модальных окон
├── UIWindowFactory        — Создание и пулинг окон
├── DialogBuilder          — Диалоги
├── ToastBuilder           — Уведомления
└── TooltipBuilder         — Тултипы
```

---

## Декларация окон через атрибуты

### [UIWindow] — определение окна

```csharp
[UIWindow("WindowId", WindowType.Normal, WindowLayer.Windows)]
```

| Параметр | Тип | Описание |
|----------|-----|----------|
| WindowId | string | Уникальный ID окна |
| Type | WindowType | Normal / Modal / Overlay |
| Layer | WindowLayer | Слой отображения |
| PauseGame | bool | Ставить Time.timeScale = 0 |
| HideBelow | bool | Скрывать окна ниже |
| AllowBack | bool | Разрешить закрытие через Back |

### [UITransition] — переход из окна

```csharp
[UITransition("TriggerName", "TargetWindowId")]
```

| Параметр | Тип | Описание |
|----------|-----|----------|
| Trigger | string | Имя триггера для Navigate() |
| ToWindowId | string | ID целевого окна |
| Animation | TransitionAnimation | Анимация перехода |

### [UIGlobalTransition] — глобальный переход

```csharp
[UIGlobalTransition("OpenSettings", "Settings")]
```

Доступен из любого окна.

---

## Типы окон

| Тип | Описание |
|-----|----------|
| Normal | Обычное окно, заменяет предыдущее в стеке |
| Modal | Блокирует взаимодействие с нижними окнами |
| Overlay | Отображается поверх без блокировки |

---

## Слои (Z-order)

| Слой | Значение | Описание |
|------|----------|----------|
| Background | 0 | Фоновые элементы |
| HUD | 100 | Игровой интерфейс |
| Windows | 200 | Обычные окна |
| Modals | 300 | Модальные окна |
| Tooltips | 400 | Тултипы |
| Notifications | 500 | Тосты |
| System | 1000 | Системные (Loading) |

---

## UIWindowBase — базовый класс окна

```csharp
public abstract class UIWindowBase : MonoBehaviour
{
    // Lifecycle
    protected virtual void OnBeforeShow() { }
    protected virtual void OnShow() { }
    protected virtual void OnBeforeHide() { }
    protected virtual void OnHide() { }
    protected virtual void OnFocus() { }
    protected virtual void OnBlur() { }

    // Навигация
    protected void Navigate(string trigger);
    protected void Close();

    // Свойства
    public WindowState State { get; }
    public string WindowId { get; }
    public WindowType WindowType { get; }
}
```

### Анимации

```csharp
[Header("Animation")]
[SerializeField] protected TransitionAnimation showAnimation = TransitionAnimation.Fade;
[SerializeField] protected TransitionAnimation hideAnimation = TransitionAnimation.Fade;
[SerializeField] protected float animationDuration = 0.25f;
```

---

## DialogBuilder — диалоги

### Confirm

```csharp
UISystem.Instance.Dialog.Confirm(
    message: "Удалить файл?",
    onYes: () => DeleteFile(),
    onNo: () => Debug.Log("Отменено"),
    title: "Подтверждение",
    yesText: "Удалить",
    noText: "Отмена"
);
```

### Alert

```csharp
UISystem.Instance.Dialog.Alert(
    message: "Операция завершена",
    onOk: () => Continue(),
    title: "Информация"
);
```

### Input

```csharp
UISystem.Instance.Dialog.Input(
    message: "Введите имя:",
    onSubmit: (name) => SaveName(name),
    onCancel: null,
    defaultValue: "Player",
    placeholder: "Имя игрока"
);
```

### Choice

```csharp
UISystem.Instance.Dialog.Choice(
    message: "Выберите сложность:",
    options: new[] { "Легко", "Нормально", "Сложно" },
    onSelect: (index) => SetDifficulty(index)
);
```

---

## ToastBuilder — уведомления

```csharp
var toast = UISystem.Instance.Toast;

// Простой тост
toast.Show("Сообщение");

// С длительностью
toast.Show("Сообщение", 5f);

// Типизированные
toast.ShowInfo("Информация");
toast.ShowSuccess("Успех!");
toast.ShowWarning("Внимание");
toast.ShowError("Ошибка");

// Скрыть
toast.Hide(toastId);
toast.HideAll();
```

---

## TooltipBuilder — тултипы

```csharp
var tooltip = UISystem.Instance.Tooltip;

// Показать
tooltip.Show("Подсказка");

// С позицией
tooltip.Show("Подсказка", Input.mousePosition);

// Обновить позицию (в Update)
tooltip.UpdatePosition(Input.mousePosition);

// Скрыть
tooltip.Hide();
```

---

## События EventBus

### Навигация (10200-10209)

```csharp
EventBus.UI.WindowOpened     // WindowEventData
EventBus.UI.WindowClosed     // WindowEventData
EventBus.UI.WindowFocused    // WindowEventData
EventBus.UI.WindowBlurred    // WindowEventData
EventBus.UI.NavigationCompleted // NavigationEventData
EventBus.UI.NavigationFailed    // NavigationEventData
EventBus.UI.BackPressed         // null
```

### Диалоги (10210-10219)

```csharp
EventBus.UI.DialogShown      // DialogEventData
EventBus.UI.DialogClosed     // DialogEventData
EventBus.UI.DialogConfirmed  // DialogEventData
EventBus.UI.DialogCancelled  // DialogEventData
```

### Toast (10220-10229)

```csharp
EventBus.UI.ToastShown       // ToastEventData
EventBus.UI.ToastHidden      // ToastEventData
```

### Tooltip (10230-10239)

```csharp
EventBus.UI.TooltipShown     // TooltipEventData
EventBus.UI.TooltipHidden    // TooltipEventData
```

---

## Визуальный редактор графа

Открыть: `ProtoSystem → UI Window Graph Editor`

### Возможности

- Визуальное отображение всех окон и переходов
- Окна из кода отмечены как `[Code]`
- Drag & Drop для позиционирования
- Создание окон и переходов через контекстное меню
- Валидация графа
- Auto Layout

### Цветовая схема

- **Синий** — Normal окна
- **Оранжевый** — Modal окна
- **Зелёный** — Overlay окна / глобальные переходы
- **Серый** — Окна из кода (только чтение)
- **Жёлтый** — Выбранный элемент

---

## UISystemConfig

```csharp
[CreateAssetMenu(menuName = "ProtoSystem/UI/System Config")]
public class UISystemConfig : ScriptableObject
{
    // Анимации
    public float defaultAnimationDuration = 0.25f;
    public TransitionAnimation defaultShowAnimation;
    public TransitionAnimation defaultHideAnimation;

    // Модальные окна
    public Color modalOverlayColor;
    public bool closeModalOnOverlayClick = true;

    // Toast
    public float defaultToastDuration = 3f;
    public int maxToasts = 3;
    public ToastPosition toastPosition;

    // Tooltip
    public float tooltipDelay = 0.5f;
    public Vector2 tooltipOffset;

    // Префабы
    public GameObject confirmDialogPrefab;
    public GameObject inputDialogPrefab;
    public GameObject choiceDialogPrefab;
    public GameObject toastPrefab;
    public GameObject tooltipPrefab;
}
```

---

## Пример полного окна

```csharp
[UIWindow("PauseMenu", WindowType.Modal, WindowLayer.Modals, PauseGame = true)]
[UITransition("Resume", "GameHUD")]
[UITransition("Settings", "SettingsMenu")]
[UITransition("Quit", "ConfirmQuit")]
public class PauseMenuWindow : UIWindowBase
{
    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    protected override void OnShow()
    {
        resumeButton.onClick.AddListener(() => Navigate("Resume"));
        settingsButton.onClick.AddListener(() => Navigate("Settings"));
        quitButton.onClick.AddListener(() => Navigate("Quit"));
    }

    protected override void OnHide()
    {
        resumeButton.onClick.RemoveAllListeners();
        settingsButton.onClick.RemoveAllListeners();
        quitButton.onClick.RemoveAllListeners();
    }
}
```

---

## Файловая структура

```
Runtime/UI/
├── UISystem.cs              # Главная система
├── UISystemConfig.cs        # Конфигурация
├── UIEnums.cs               # Перечисления
├── UIEvents.cs              # События EventBus
├── Attributes/
│   └── UIWindowAttribute.cs # Атрибуты
├── Core/
│   ├── UIWindowBase.cs      # Базовый класс окна
│   ├── UIWindowGraph.cs     # Граф переходов (SO)
│   ├── UINavigator.cs       # Управление навигацией
│   └── UIWindowFactory.cs   # Фабрика окон
└── Builders/
    ├── DialogBuilder.cs     # Диалоги
    ├── ToastBuilder.cs      # Тосты
    └── TooltipBuilder.cs    # Тултипы

Editor/UI/
└── UIWindowGraphEditor.cs   # Визуальный редактор
```
