# ProtoSystem v1.6.8 - Example UI Initializer (Code-First Approach)

## 📅 Дата: 2026-01-10

## 🎯 Главная идея

**ProtoSystem UI = программное создание интерфейсов минимумом кода!**

Создавайте UI flow декларативно, как в SwiftUI/Jetpack Compose.

## ✅ Исправления

### UISystem Input System Support
```csharp
#if ENABLE_LEGACY_INPUT_MANAGER
    if (Input.GetKeyDown(KeyCode.Escape))
#elif ENABLE_INPUT_SYSTEM
    if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
#endif
```

## 🎯 Задача: "Create Example UI Windows"

Создаёт **ExampleGameplayInitializer.cs** - готовый пример программной настройки UI.

### Что создаётся:

**Только .cs файл:**
```
Assets/{ProjectName}/Scripts/UI/ExampleGameplayInitializer.cs
```

### Структура файла (80 строк):

```csharp
public class ExampleGameplayInitializer : UISceneInitializerBase
{
    [SerializeField] private bool skipMainMenu = false;
    
    // Стартовое окно
    public override string StartWindowId => "MainMenuWindow";
    
    // 6 строк = весь UI flow!
    public override IEnumerable<UITransitionDefinition> GetAdditionalTransitions()
    {
        yield return new UITransitionDefinition("MainMenuWindow", "SettingsWindow", "settings", Fade);
        yield return new UITransitionDefinition("MainMenuWindow", "GameHUDWindow", "start_game", SlideLeft);
        yield return new UITransitionDefinition("GameHUDWindow", "PauseMenuWindow", "pause", Instant);
        yield return new UITransitionDefinition("PauseMenuWindow", "GameHUDWindow", "resume", Instant);
        yield return new UITransitionDefinition("PauseMenuWindow", "MainMenuWindow", "quit", Fade);
        // ...
    }
    
    // Обработка навигации
    private void OnNavigated(NavigationEventData data) { }
    
    // Input handling (поддержка обоих Input System)
    private void HandleEscape() { }
}
```

## 📋 Использование

### 1. Запустить визард
```
Tools → ProtoSystem → Project Setup Wizard
Execute All Pending
```

### 2. Добавить initializer к UISystem

**Вариант A - через меню:**
```
SystemInitializationManager → UISystem
Scene Initializer Component → + Create → Create Example Initializer
```

**Вариант B - через Add Component:**
```
SystemInitializationManager → UISystem
Add Component → ExampleGameplayInitializer
```

### 3. Создать префабы окон

**Используя генератор:**
```
UISystem → Generate Base Windows
```

**Или вручную:**
- MainMenuWindow.prefab
- SettingsWindow.prefab
- CreditsWindow.prefab
- GameHUDWindow.prefab
- PauseMenuWindow.prefab

### 4. Play!
- Откроется MainMenuWindow
- UI flow работает из кода
- Escape обрабатывается автоматически

## 💡 Философия: Code-First

### Было (Inspector):
```
Prefabs → Окна → Настройка каждого → Transitions вручную → Кнопки → OnClick
```

### Стало (Code):
```csharp
// 1 строка = полный UI flow
yield return new UITransitionDefinition("From", "To", "trigger", Animation);
```

### Преимущества:

**✅ Минимум кода:**
- 6 строк = весь UI flow
- Читаемо, декларативно

**✅ Версионный контроль:**
- Diff видит изменения
- Нет конфликтов prefab'ов

**✅ Типизация:**
- IntelliSense автодополнение
- Compile-time проверки

**✅ DRY:**
- Переиспользуемые паттерны
- Шаблоны для разных проектов

## 🔄 UI Flow пример

```csharp
// MainMenu → Settings
yield return new UITransitionDefinition("MainMenuWindow", "SettingsWindow", "settings", Fade);

// Использование:
UISystem.Instance.Navigate("settings");
```

**Весь граф переходов в коде!**

## 🛠️ Расширение

### Добавить окно:
```csharp
yield return new UITransitionDefinition("GameHUD", "Shop", "open_shop", Fade);
```

### Добавить логику:
```csharp
private void OnNavigated(NavigationEventData data)
{
    if (data.ToWindowId == "Shop")
        LoadShopData();
}
```

### Обработать input:
```csharp
private void Update()
{
    if (Keyboard.current?.f1Key.wasPressedThisFrame == true)
        _uiSystem.Navigate("help");
}
```

## 📦 Что НЕ создаётся

**❌ Префабы окон** - создайте вручную или через генератор  
**❌ Спрайты** - создаются в задаче "Generate UI Sprites"  
**❌ Base prefabs** - создаются в задаче "Generate UI Prefabs"

**✅ Только .cs файл** - пример программной настройки!

## 🔍 Детектирование через рефлексию

UISystemEditor ищет класс:
- Наследует UISceneInitializerBase
- Содержит "ExampleGameplayInitializer" или "ExampleInitializer"
- Добавляет в меню "+ Create"

## 📝 Версия: 1.6.8

**ProtoSystem - Code-First UI Framework!** 🚀
