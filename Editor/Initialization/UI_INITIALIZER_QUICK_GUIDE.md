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
    
    // UI Flow - 6 строк!
    public override IEnumerable<UITransitionDefinition> GetAdditionalTransitions()
    {
        // NOTE: используйте ids из [UIWindow("...")] (граф), а не имена prefab/классов.
        yield return new UITransitionDefinition("MainMenu", "Settings", "settings", Fade);
        yield return new UITransitionDefinition("MainMenu", "GameHUD", "play", SlideLeft);
        yield return new UITransitionDefinition("GameHUD", "PauseMenu", "pause", Instant);
        // ...
    }
    
    // Поддержка обоих Input System
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

---

## 🎨 Минимум кода = Максимум функциональности

**Одна строка = полный transition!**

```csharp
yield return new UITransitionDefinition("From", "To", "trigger", Fade);
```

---

## 🔧 Кастомизация

### Изменить стартовое окно:
```csharp
public override string StartWindowId => "GameHUD";
```

### Добавить transition:
```csharp
yield return new UITransitionDefinition("GameHUD", "Shop", "open_shop", Fade);
```

### Обработать input:
```csharp
#if ENABLE_INPUT_SYSTEM
if (UnityEngine.InputSystem.Keyboard.current?.f1Key.wasPressedThisFrame == true)
#else
if (Input.GetKeyDown(KeyCode.F1))
#endif
```

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
