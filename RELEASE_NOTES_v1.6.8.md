# ProtoSystem v1.6.8 - Example UI Initializer (Code-First Approach)

## 📅 Дата: 2026-01-10

## 🎯 Главная идея

**ProtoSystem UI = программное создание интерфейсов минимумом кода!**

Создавайте UI flow декларативно, как в SwiftUI/Jetpack Compose.

## ✅ Исправления

### 1. UISystem Input System Support
**Проблема:** Compilation error при использовании Input System
```
CS0234: The type or namespace name 'InputSystem' does not exist
```

**Причина:** `#if` директивы в блоке `using` не работают во всех версиях Unity компилятора.

**Решение:** Используем **полные имена типов** вместо `using`:
```csharp
// НЕ используем using UnityEngine.InputSystem

// Используем полное имя типа:
#if ENABLE_INPUT_SYSTEM
if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
#endif
```

**Преимущества:**
- ✅ Работает в любой версии Unity
- ✅ Не требует `using` директиву
- ✅ Код компилируется даже без Input System пакета

### 2. Удалены ненужные методы создания префабов
Из ProjectSetupWizard.cs удалены методы создания UI элементов - ProtoSystem следует Code-First подходу.

## 🎯 Задача: "Create Example UI Initializer"

Создаёт **ExampleGameplayInitializer.cs** - готовый пример программной настройки UI.

### Что создаётся:

**Только .cs файл:**
```
Assets/{ProjectName}/Scripts/UI/ExampleGameplayInitializer.cs
```

### Структура файла (80 строк):

```csharp
// Нет using UnityEngine.InputSystem - используем полное имя!

public class ExampleGameplayInitializer : UISceneInitializerBase
{
    [SerializeField] private bool skipMainMenu = false;
    
    // Стартовое окно
    public override string StartWindowId => "MainMenu";
    
    // 6 строк = весь UI flow!
    public override IEnumerable<UITransitionDefinition> GetAdditionalTransitions()
    {
        // NOTE: Use ids from [UIWindow("...")] (graph ids), not prefab/class names.
        yield return new UITransitionDefinition("MainMenu", "Settings", "settings", TransitionAnimation.Fade);
        yield return new UITransitionDefinition("MainMenu", "GameHUD", "play", TransitionAnimation.SlideLeft);
        yield return new UITransitionDefinition("GameHUD", "PauseMenu", "pause", TransitionAnimation.None);
        // ...
    }
    
    // Поддержка обоих Input System - используем полное имя типа!
    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
#elif ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
#endif
            HandleEscape();
    }
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

**Или вручную.**

### 4. Play!

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

**✅ Минимум кода** - 6 строк = весь UI flow  
**✅ Версионный контроль** - видны изменения в diff  
**✅ Типизация** - IntelliSense, compile-time проверки  
**✅ Универсальная совместимость** - работает во всех версиях Unity

## 🔄 UI Flow пример

```csharp
// MainMenu → Settings
yield return new UITransitionDefinition("MainMenu", "Settings", "settings", TransitionAnimation.Fade);

// Использование:
UISystem.Instance.Navigate("settings");
```

## 🛠️ Расширение

### Добавить окно:
```csharp
yield return new UITransitionDefinition("GameHUD", "Shop", "open_shop", TransitionAnimation.Fade);
```

### Обработать input:
```csharp
private void Update()
{
#if ENABLE_INPUT_SYSTEM
    if (UnityEngine.InputSystem.Keyboard.current?.f1Key.wasPressedThisFrame == true)
#else
    if (Input.GetKeyDown(KeyCode.F1))
#endif
        _uiSystem.Navigate("help");
}
```

## 📝 Версия: 1.6.8

**Критическое исправление:**
- ✅ UISystem.cs - используется полное имя `UnityEngine.InputSystem.Keyboard`
- ✅ ExampleGameplayInitializer шаблон - используется полное имя
- ✅ Убраны `using` директивы для Input System

**Теперь работает в любой версии Unity и любой конфигурации!** 🚀
