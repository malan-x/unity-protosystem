# ⚡ Quick Guide - Example UI Initializer v1.6.8

## 🎯 Что это?

**ExampleGameplayInitializer.cs** - готовый пример программной настройки UI.

**Создаёт:** Только .cs файл (~80 строк кода)  
**НЕ создаёт:** Префабы окон (создайте вручную)

---

## 🚀 Быстрый старт

### 1. Запустить визард
```
Tools → ProtoSystem → Project Setup Wizard
Execute All Pending
```

### 2. Добавить к UISystem
```
UISystem → Scene Initializer → + Create → Create Example Initializer
```

### 3. Создать окна
```
UISystem → Generate Base Windows
```

### 4. Play!

---

## 💻 Код примера

```csharp
public class ExampleGameplayInitializer : UISceneInitializerBase
{
    // Стартовое окно
    public override string StartWindowId => "MainMenu";

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
    
    // UI Flow - 6 строк!
    public override IEnumerable<UITransitionDefinition> GetAdditionalTransitions()
    {
        // NOTE: используйте ids из [UIWindow("...")] (граф), а не имена prefab/классов.
        yield return new UITransitionDefinition("MainMenu", "Settings", "settings", TransitionAnimation.Fade);
        yield return new UITransitionDefinition("MainMenu", "GameHUD", "play", TransitionAnimation.SlideLeft);
        yield return new UITransitionDefinition("GameHUD", "PauseMenu", "pause", TransitionAnimation.None);
        // ...
    }
    
        // Back/Escape обрабатывается внутри UISystem и делегируется активному окну через OnBackPressed().
}
```

---

## 🎨 Минимум кода = Максимум функциональности

**Одна строка = полный transition!**

```csharp
yield return new UITransitionDefinition("From", "To", "trigger", TransitionAnimation.Fade);
```

---

## 🔧 Кастомизация

### Изменить стартовое окно:
```csharp
public override string StartWindowId => "GameHUD";
```

### Добавить transition:
```csharp
yield return new UITransitionDefinition("GameHUD", "Shop", "open_shop", TransitionAnimation.Fade);
```

### Обработать Back/Escape:
✅ Переопределите `OnBackPressed()` в нужном окне (например, `GameHUDWindow` открывает PauseMenu).

---

## ⚠️ Важно!

### Используйте полные имена типов для Input System:

**✅ Правильно:**
```csharp
UnityEngine.InputSystem.Keyboard.current
```

**❌ Неправильно:**
```csharp
using UnityEngine.InputSystem;  // НЕ работает во всех версиях
Keyboard.current
```

---

## 📋 FAQ

### Ошибка компиляции Input System?
✅ Используйте полное имя: `UnityEngine.InputSystem.Keyboard.current`

### ExampleGameplayInitializer не появляется в меню?
✅ Перекомпилируйте проект (Ctrl+R)

### Окна не открываются?
⚠️ Создайте префабы окон

---

**v1.6.8** - Code-First UI из коробки! 🎯
