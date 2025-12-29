# UISystem — Сценарии тестирования

## Предварительные условия

1. На сцене есть GameObject с `UISystem`
2. Создан и назначен `UIWindowGraph`
3. Созданы префабы окон с компонентами-наследниками `UIWindowBase`

---

## 1. Навигация

### 1.1 Открытие стартового окна

**Шаги:**
1. Создать окно MainMenu с атрибутом `[UIWindow("MainMenu")]`
2. Добавить префаб в граф
3. Установить `startWindowId = "MainMenu"`
4. Запустить игру

**Ожидаемый результат:**
- MainMenu автоматически открывается
- `UISystem.CurrentWindow.WindowId == "MainMenu"`
- Событие `WindowOpened` опубликовано

### 1.2 Переход по триггеру

**Шаги:**
1. Добавить `[UITransition("OpenSettings", "Settings")]` к MainMenu
2. Вызвать `UISystem.Navigate("OpenSettings")`

**Ожидаемый результат:**
- MainMenu скрывается
- Settings показывается
- `NavigationResult.Success`
- События: `WindowBlurred` → `WindowOpened`

### 1.3 Невалидный переход

**Шаги:**
1. Вызвать `UISystem.Navigate("NonExistentTrigger")`

**Ожидаемый результат:**
- `NavigationResult.TriggerNotFound`
- Warning в консоли
- Событие `NavigationFailed`

### 1.4 Back навигация

**Шаги:**
1. Открыть MainMenu → Settings
2. Вызвать `UISystem.Back()`

**Ожидаемый результат:**
- Settings скрывается
- MainMenu показывается
- `UISystem.CanGoBack` обновляется

### 1.5 Back на стартовом окне

**Шаги:**
1. На MainMenu вызвать `UISystem.Back()`

**Ожидаемый результат:**
- `NavigationResult.StackEmpty`
- Окно не закрывается

---

## 2. Модальные окна

### 2.1 Открытие модального

**Шаги:**
1. Создать окно `[UIWindow("ConfirmQuit", WindowType.Modal)]`
2. Открыть его

**Ожидаемый результат:**
- Модальное поверх текущего
- Текущее получает `OnBlur()`
- `UISystem.HasModal == true`

### 2.2 Закрытие модального

**Шаги:**
1. При открытом модальном вызвать `UISystem.Back()`

**Ожидаемый результат:**
- Модальное закрывается
- Предыдущее получает `OnFocus()`
- `UISystem.HasModal == false`

### 2.3 Стек модальных

**Шаги:**
1. Открыть Modal1
2. Из Modal1 открыть Modal2

**Ожидаемый результат:**
- Modal2 поверх Modal1
- Modal1 получает `OnBlur()`
- Back закрывает Modal2, возвращает к Modal1

### 2.4 Пауза игры

**Шаги:**
1. Создать `[UIWindow("Pause", WindowType.Modal, PauseGame = true)]`
2. Открыть его

**Ожидаемый результат:**
- `Time.timeScale == 0`
- После закрытия: `Time.timeScale == 1`

---

## 3. Анимации

### 3.1 Fade анимация

**Шаги:**
1. Установить `showAnimation = Fade`
2. Открыть окно

**Ожидаемый результат:**
- CanvasGroup.alpha плавно меняется 0 → 1
- Длительность = `animationDuration`

### 3.2 Slide анимация

**Шаги:**
1. Установить `showAnimation = SlideRight`
2. Открыть окно

**Ожидаемый результат:**
- Окно въезжает справа
- Позиция анимируется

### 3.3 Scale анимация

**Шаги:**
1. Установить `showAnimation = Scale`
2. Открыть окно

**Ожидаемый результат:**
- Окно масштабируется 0.8 → 1
- Alpha одновременно 0 → 1

---

## 4. Диалоги

### 4.1 Confirm диалог

**Шаги:**
```csharp
bool confirmed = false;
UISystem.Instance.Dialog.Confirm(
    "Удалить?",
    onYes: () => confirmed = true,
    onNo: () => confirmed = false
);
```
1. Нажать "Yes"

**Ожидаемый результат:**
- `confirmed == true`
- Диалог закрылся
- Событие `DialogConfirmed`

### 4.2 Input диалог

**Шаги:**
```csharp
string result = "";
UISystem.Instance.Dialog.Input(
    "Введите имя:",
    onSubmit: (value) => result = value,
    defaultValue: "Player"
);
```
1. Ввести "TestName"
2. Нажать OK

**Ожидаемый результат:**
- `result == "TestName"`
- Диалог закрылся

### 4.3 Choice диалог

**Шаги:**
```csharp
int selected = -1;
UISystem.Instance.Dialog.Choice(
    "Выберите:",
    new[] { "A", "B", "C" },
    onSelect: (index) => selected = index
);
```
1. Выбрать "B"

**Ожидаемый результат:**
- `selected == 1`

---

## 5. Toast уведомления

### 5.1 Показ тоста

**Шаги:**
```csharp
UISystem.Instance.Toast.Show("Тест", 3f);
```

**Ожидаемый результат:**
- Toast появляется
- Исчезает через 3 секунды
- События: `ToastShown` → `ToastHidden`

### 5.2 Типизированные тосты

**Шаги:**
```csharp
toast.ShowInfo("Info");
toast.ShowSuccess("Success");
toast.ShowWarning("Warning");
toast.ShowError("Error");
```

**Ожидаемый результат:**
- Разные стили/цвета для разных типов

### 5.3 Лимит тостов

**Шаги:**
1. Установить `maxToasts = 3`
2. Показать 5 тостов подряд

**Ожидаемый результат:**
- Видно только 3 последних
- Первые 2 автоматически скрыты

### 5.4 Позиции тостов

**Шаги:**
1. Проверить все `ToastPosition`: TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight

**Ожидаемый результат:**
- Тосты появляются в соответствующих углах/центрах

---

## 6. Tooltip

### 6.1 Показ тултипа

**Шаги:**
```csharp
UISystem.Instance.Tooltip.Show("Подсказка");
```

**Ожидаемый результат:**
- Тултип появляется после `tooltipDelay`
- Позиция рядом с курсором

### 6.2 Следование за курсором

**Шаги:**
```csharp
void Update()
{
    UISystem.Instance.Tooltip.UpdatePosition(Input.mousePosition);
}
```

**Ожидаемый результат:**
- Тултип следует за курсором

### 6.3 Ограничение экраном

**Шаги:**
1. Показать тултип у края экрана

**Ожидаемый результат:**
- Тултип не выходит за границы экрана
- Автоматически смещается

---

## 7. Атрибуты и граф

### 7.1 Сбор атрибутов

**Шаги:**
1. Создать классы с `[UIWindow]` и `[UITransition]`
2. Открыть UI Window Graph Editor

**Ожидаемый результат:**
- Все окна из кода отображаются
- Помечены как `[Code]`
- Переходы отрисованы

### 7.2 Валидация графа

**Шаги:**
1. В редакторе нажать "Validate"

**Ожидаемый результат:**
- Отчёт об ошибках/предупреждениях
- Проверка: все переходы ведут к существующим окнам
- Проверка: стартовое окно существует

### 7.3 Auto Layout

**Шаги:**
1. Нажать "Auto Layout"

**Ожидаемый результат:**
- Узлы расположены по уровням от стартового
- Нет пересечений

---

## 8. Edge Cases

### 8.1 Повторное открытие того же окна

**Шаги:**
1. MainMenu открыто
2. Вызвать `UISystem.Open("MainMenu")`

**Ожидаемый результат:**
- `NavigationResult.AlreadyOnWindow`
- Ничего не происходит

### 8.2 Escape для Back

**Шаги:**
1. Открыть несколько окон
2. Нажать Escape

**Ожидаемый результат:**
- Back выполняется
- Модальные закрываются первыми

### 8.3 Окно без prefab

**Шаги:**
1. В графе добавить окно без prefab
2. Попытаться открыть

**Ожидаемый результат:**
- Error в консоли
- `NavigationResult.WindowNotFound`

### 8.4 Reset навигации

**Шаги:**
1. Открыть: MainMenu → Settings → SubSettings → Modal
2. Вызвать `UISystem.Reset()`

**Ожидаемый результат:**
- Все окна закрыты кроме MainMenu
- Модальные закрыты
- Стек очищен

---

## 9. Lifecycle окна

### 9.1 Порядок вызовов при открытии

**Шаги:**
1. Добавить логи в OnBeforeShow, OnShow

**Ожидаемый результат:**
```
OnBeforeShow (State = Showing)
[Анимация]
OnShow (State = Visible)
```

### 9.2 Порядок вызовов при закрытии

**Ожидаемый результат:**
```
OnBeforeHide (State = Hiding)
[Анимация]
OnHide (State = Hidden)
```

### 9.3 Focus/Blur

**Шаги:**
1. MainMenu открыто
2. Открыть Settings
3. Закрыть Settings

**Ожидаемый результат:**
```
MainMenu: OnBlur (State = Blurred)
Settings: OnShow
Settings: OnHide
MainMenu: OnFocus (State = Visible)
```

---

## 10. Пул окон

### 10.1 Повторное использование

**Шаги:**
1. Открыть Settings
2. Закрыть Settings
3. Открыть Settings снова

**Ожидаемый результат:**
- Второй раз используется тот же экземпляр
- Нет Instantiate

### 10.2 Лимит пула

**Шаги:**
1. Открыть/закрыть Settings 10 раз

**Ожидаемый результат:**
- В пуле максимум 3 экземпляра
- Лишние уничтожаются

---

## Чек-лист

- [ ] Стартовое окно открывается
- [ ] Navigate() по триггеру работает
- [ ] Back() возвращает к предыдущему
- [ ] Модальные блокируют нижние окна
- [ ] PauseGame работает
- [ ] Анимации проигрываются
- [ ] Диалоги Confirm/Input/Choice работают
- [ ] Toast показываются/скрываются
- [ ] Tooltip следует за курсором
- [ ] Escape = Back
- [ ] Граф валидируется
- [ ] Атрибуты собираются из кода
- [ ] Визуальный редактор отображает граф
