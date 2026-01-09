# UISystem — Сценарии тестирования

## Предварительные условия

1. На сцене есть GameObject с `UISystem`
2. UIWindowGraph создан автоматически в `Resources/UIWindowGraph`
3. Созданы префабы окон с компонентами-наследниками `UIWindowBase`
4. Классы окон имеют атрибуты `[UIWindow]` и `[UITransition]`

---

## 1. Граф окон

### 1.1 Автоматическая сборка графа

**Шаги:**
1. Создать окна с атрибутами:
```csharp
[UIWindow("MainMenu", WindowType.Normal, WindowLayer.Windows, Level = 0)]
[UITransition("play", "GameHUD")]
[UITransition("settings", "Settings")]
public class MainMenu : UIWindowBase { }
```
2. ProtoSystem → UI → Rebuild Window Graph

**Ожидаемый результат:**
- UIWindowGraph содержит MainMenu
- Переходы "play" → GameHUD, "settings" → Settings добавлены
- Префабы найдены автоматически

### 1.2 Window Graph Viewer

**Шаги:**
1. ProtoSystem → UI → Window Graph Viewer
2. Кликнуть на ноду MainMenu

**Ожидаемый результат:**
- Граф отображается визуально
- Стартовое окно зелёное
- Клик на ноду → инспектор показывает детали
- Линии переходов с лейблами триггеров

### 1.3 Визуальная проверка переходов

**Шаги:**
1. В Graph Viewer кликнуть на линию перехода

**Ожидаемый результат:**
- Линия подсвечивается желтым
- Инспектор показывает:
  - Type: Local Transition
  - From: MainMenu
  - To: Settings
  - Trigger: settings
  - Bidirectional: Yes/No
  - Префабы обоих окон

### 1.4 Проверка недостижимых окон

**Шаги:**
1. Создать окно без переходов к нему
2. Rebuild Window Graph
3. Открыть Graph Viewer

**Ожидаемый результат:**
- Недостижимое окно полупрозрачное серое
- В инспекторе: Reachable: No

### 1.5 Глобальные переходы

**Шаги:**
1. Добавить `[UITransition("", "Loading")]` (пустая строка = глобальный)
2. Rebuild Graph

**Ожидаемый результат:**
- В графе желтые линии от края экрана к Loading
- Label: "loading"
- В инспекторе: Type: Global Transition

---

## 2. Навигация по триггерам

### 2.1 Navigate() по триггеру

**Шаги:**
1. Открыть MainMenu
2. Вызвать `UISystem.Navigate("play")`

**Ожидаемый результат:**
- MainMenu скрывается
- GameHUD открывается
- События: `WindowClosed` → `WindowOpened`

### 2.2 Navigate() с несуществующим триггером

**Шаги:**
1. Вызвать `UISystem.Navigate("nonexistent")`

**Ожидаемый результат:**
- Warning: "Navigation failed: Trigger 'nonexistent' not found"
- Текущее окно остаётся открытым

### 2.3 CanNavigate() проверка

**Шаги:**
```csharp
bool canPlay = UISystem.Instance.CanNavigate("play");
bool canInvalid = UISystem.Instance.CanNavigate("invalid");
```

**Ожидаемый результат:**
- `canPlay == true`
- `canInvalid == false`

### 2.4 Back навигация

**Шаги:**
1. MainMenu → Settings (Navigate("settings"))
2. Вызвать `UISystem.Back()`

**Ожидаемый результат:**
- Settings закрывается
- MainMenu появляется

### 2.5 Back на Level 0 окне

**Шаги:**
1. На MainMenu (Level = 0) вызвать `UISystem.Back()`

**Ожидаемый результат:**
- MainMenu остаётся открытым
- Нет закрытия (Level 0 = главные окна)

---

## 3. UISceneInitializerBase

### 3.1 Startup Windows

**Шаги:**
1. Создать инициализатор:
```csharp
public class GameplayInitializer : UISceneInitializerBase
{
    public override string[] GetStartupWindows() => new[] { "game_hud" };
}
```
2. Назначить в UISystem.sceneInitializerComponent
3. Запустить сцену

**Ожидаемый результат:**
- GameHUD автоматически открывается при старте

### 3.2 Additional Transitions

**Шаги:**
1. В инициализаторе:
```csharp
public override IEnumerable<UITransitionDefinition> GetAdditionalTransitions()
{
    yield return new UITransitionDefinition("game_hud", "pause_menu", "pause");
}
```
2. Rebuild Graph
3. Открыть Graph Viewer

**Ожидаемый результат:**
- Переход GameHUD → PauseMenu отображается в графе
- `UISystem.Navigate("pause")` работает

### 3.3 Scene-specific переходы

**Шаги:**
1. Создать разные инициализаторы для разных сцен
2. Проверить что переходы работают только в соответствующих сценах

**Ожидаемый результат:**
- В сцене A доступен переход "battle_end"
- В сцене B переход "battle_end" не работает

---

## 4. Модальные окна

### 4.1 Открытие модального

**Шаги:**
1. Создать `[UIWindow("ConfirmQuit", WindowType.Modal)]`
2. Navigate("confirm_quit")

**Ожидаемый результат:**
- Модальное поверх текущего
- Текущее остается видимым но заблокированным

### 4.2 Закрытие модального через Back()

**Шаги:**
1. При открытом модальном вызвать `UISystem.Back()`

**Ожидаемый результат:**
- Модальное закрывается
- Предыдущее окно восстанавливает интерактивность

### 4.3 Стек модальных

**Шаги:**
1. Navigate("modal1")
2. Navigate("modal2") из modal1

**Ожидаемый результат:**
- Modal2 поверх Modal1
- Back() закрывает Modal2 → возвращает к Modal1
- Back() снова → закрывает Modal1

---

## 5. Управление паузой

### 5.1 PauseGame атрибут

**Шаги:**
1. Создать `[UIWindow("Pause", ..., PauseGame = true)]`
2. Navigate("pause")

**Ожидаемый результат:**
- `UITimeManager.Instance.IsPaused == true`
- `Time.timeScale == 0`

### 5.2 Снятие паузы

**Шаги:**
1. Закрыть окно с PauseGame = true

**Ожидаемый результат:**
- `UITimeManager.Instance.IsPaused == false`
- `Time.timeScale == 1`

### 5.3 Счётчик паузы

**Шаги:**
1. Открыть Pause1 (PauseGame = true)
2. Открыть Modal1 (PauseGame = true)
3. Закрыть Modal1

**Ожидаемый результат:**
- После шага 2: `PauseRequestCount == 2`
- После шага 3: `PauseRequestCount == 1`, игра на паузе
- Закрыть Pause1: `PauseRequestCount == 0`, игра идёт

### 5.4 ResetAllPauses()

**Шаги:**
1. Открыть несколько окон с паузой
2. `UITimeManager.Instance.ResetAllPauses()`

**Ожидаемый результат:**
- `PauseRequestCount == 0`
- Игра продолжается

---

## 6. Управление курсором

### 6.1 CursorMode атрибут

**Шаги:**
1. GameHUD: `CursorMode = WindowCursorMode.Locked`
2. PauseMenu: `CursorMode = WindowCursorMode.Visible`
3. Navigate("game_hud") → Navigate("pause")

**Ожидаемый результат:**
- В GameHUD: курсор заблокирован, невидим
- В PauseMenu: курсор видим, свободен

### 6.2 Стек курсора

**Шаги:**
1. Открыть GameHUD (Locked)
2. Открыть PauseMenu (Visible)
3. Back() к GameHUD

**Ожидаемый результат:**
- После Back: курсор снова Locked

### 6.3 Режимы курсора

**Тестировать все режимы:**
- `None` — не меняет курсор
- `Locked` — Cursor.lockState = Locked, visible = false
- `Visible` — lockState = None, visible = true
- `Hidden` — lockState = None, visible = false
- `Confined` — lockState = Confined, visible = true

---

## 7. Базовые классы окон

### 7.1 GameHUDWindow наследование

**Шаги:**
1. Создать `KM_GameHUD : GameHUDWindow`
2. `[UIWindow("KM_GameHUD", ...)]` на наследнике
3. `[UIWindow("GameHUD", ..., ShowInGraph = false)]` на базовом
4. Rebuild Graph

**Ожидаемый результат:**
- В графе виден KM_GameHUD
- GameHUDWindow не показан (ShowInGraph = false)
- Доступны методы базового класса (SetStamina, SetHealth)

### 7.2 Защищённые поля базового класса

**Шаги:**
1. В KM_GameHUD обратиться к `staminaFill`, `healthText`

**Ожидаемый результат:**
- Поля доступны (protected)
- Можно переопределять методы

---

## 8. Lifecycle методы

### 8.1 OnBeforeShow / OnAfterShow

**Шаги:**
1. Добавить логи в методы
2. Navigate к окну

**Ожидаемый результат:**
```
OnBeforeShow() — перед анимацией
[Анимация показа]
OnAfterShow() — после анимации
```

### 8.2 OnBeforeHide / OnAfterHide

**Шаги:**
1. Back() или Navigate к другому окну

**Ожидаемый результат:**
```
OnBeforeHide() — перед анимацией
[Анимация скрытия]
OnAfterHide() — после анимации
```

### 8.3 OnBackPressed()

**Шаги:**
1. Переопределить `OnBackPressed()` в окне
2. Нажать Escape

**Ожидаемый результат:**
- OnBackPressed() вызван
- Можно предотвратить закрытие

---

## 9. Диалоговые окна

### 9.1 Message диалог

**Шаги:**
```csharp
UISystem.Instance.Dialog.Message(
    "Hello World!",
    onClose: () => Debug.Log("Closed"),
    title: "Info"
);
```

**Ожидаемый результат:**
- Показан MessageDialogWindow
- Кнопка OK закрывает диалог
- onClose вызван

### 9.2 Confirm диалог

**Шаги:**
```csharp
bool result = false;
UISystem.Instance.Dialog.Confirm(
    "Are you sure?",
    onYes: () => result = true,
    onNo: () => result = false
);
```
1. Нажать Yes

**Ожидаемый результат:**
- `result == true`
- Диалог закрыт

### 9.3 Choice диалог

**Шаги:**
```csharp
int selected = -1;
UISystem.Instance.Dialog.Choice(
    "Choose:",
    new[] { "A", "B", "C" },
    (index) => selected = index
);
```
1. Выбрать "B"

**Ожидаемый результат:**
- `selected == 1`

### 9.4 Input диалог

**Шаги:**
```csharp
string name = "";
UISystem.Instance.Dialog.Input(
    "Enter name:",
    (text) => name = text,
    placeholder: "Name"
);
```
1. Ввести "TestName"
2. Нажать OK

**Ожидаемый результат:**
- `name == "TestName"`

---

## 10. Edge Cases

### 10.1 Переход к тому же окну

**Шаги:**
1. MainMenu открыто
2. Navigate("main_menu") (к себе)

**Ожидаемый результат:**
- Ничего не происходит
- Warning в логах (опционально)

### 10.2 Цикличные переходы

**Шаги:**
1. A → B → C → A
2. Navigate по кругу

**Ожидаемый результат:**
- Переходы работают корректно
- Стек не переполняется

### 10.3 Отсутствующий префаб

**Шаги:**
1. Добавить окно в код, но не создавать префаб
2. Rebuild Graph
3. Navigate к окну

**Ожидаемый результат:**
- Error: "Window prefab not found: X"
- В Graph Viewer окно помечено красным ⚠

### 10.4 Escape обработка

**Шаги:**
1. Открыть GameHUD → PauseMenu → Settings
2. Нажать Escape

**Ожидаемый результат:**
- Back() выполнен
- Settings → PauseMenu
- Ещё раз Escape → PauseMenu → GameHUD

---

## 11. Валидация графа

### 11.1 Проверка дубликатов ID

**Шаги:**
1. Создать два окна с одинаковым ID
2. ProtoSystem → UI → Validate Window Graph

**Ожидаемый результат:**
- Error: "Duplicate window ID: X"

### 11.2 Проверка переходов

**Шаги:**
1. Добавить `[UITransition("test", "NonExistentWindow")]`
2. Validate

**Ожидаемый результат:**
- Warning: "Transition to unknown window: NonExistentWindow"

### 11.3 Проверка префабов

**Шаги:**
1. Окно есть в коде, префаб отсутствует
2. Validate

**Ожидаемый результат:**
- Warning: "Window X has no prefab"

---

## 12. Генератор префабов

### 12.1 Generate All Base Windows

**Шаги:**
1. ProtoSystem → UI → Generate All Base Windows

**Ожидаемый результат:**
- Созданы префабы: MainMenu, GameHUD, PauseMenu, Settings, GameOver, Statistics, Credits, Loading
- Префабы в Assets/UI/Windows/Generated/

### 12.2 Регенерация окна

**Шаги:**
1. Изменить код KM_GameHUDWindow (добавить поле)
2. KM → UI → Generate/Game HUD

**Ожидаемый результат:**
- Префаб обновлён
- Новое поле назначено через SetField()

---

## Чек-лист

**Граф:**
- [ ] Rebuild Window Graph работает
- [ ] Graph Viewer отображает все окна
- [ ] Клик на ноду → инспектор
- [ ] Клик на линию → информация о переходе
- [ ] Недостижимые окна помечены
- [ ] Validate Graph проверяет ошибки

**Навигация:**
- [ ] Navigate() по триггеру работает
- [ ] Back() возвращает к предыдущему
- [ ] CanNavigate() корректно проверяет
- [ ] Level 0 окна взаимоисключающие

**Атрибуты:**
- [ ] [UIWindow] собирается в граф
- [ ] [UITransition] создаёт переходы
- [ ] ShowInGraph = false скрывает окна
- [ ] UISceneInitializerBase добавляет переходы

**Управление:**
- [ ] PauseGame работает со счётчиком
- [ ] CursorMode переключает режимы
- [ ] Стеки паузы и курсора работают

**Lifecycle:**
- [ ] OnBeforeShow/AfterShow вызываются
- [ ] OnBeforeHide/AfterHide вызываются
- [ ] OnBackPressed обрабатывает Escape

**Диалоги:**
- [ ] Message, Confirm, Choice, Input работают
- [ ] Диалоги правильно закрываются

**Edge Cases:**
- [ ] Отсутствующий префаб не крашит
- [ ] Цикличные переходы работают
- [ ] Дубликаты ID валидируются
