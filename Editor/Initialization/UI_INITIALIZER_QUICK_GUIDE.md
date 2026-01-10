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

Создастся: `Assets/{ProjectName}/Scripts/UI/ExampleGameplayInitializer.cs`

### 2. Добавить к UISystem

**Через меню:**
```
UISystem → Scene Initializer → + Create → Create Example Initializer
```

**Или через Add Component:**
```
UISystem → Add Component → ExampleGameplayInitializer
```

### 3. Создать окна

Используйте:
- `UISystem → Generate Base Windows`
- Или создайте префабы вручную

### 4. Play!

---

## 💻 Код примера

```csharp
public class ExampleGameplayInitializer : UISceneInitializerBase
{
    // Стартовое окно
    public override string StartWindowId => "MainMenuWindow";
    
    // UI Flow - 6 строк!
    public override IEnumerable<UITransitionDefinition> GetAdditionalTransitions()
    {
        yield return new UITransitionDefinition("MainMenuWindow", "SettingsWindow", "settings", Fade);
        yield return new UITransitionDefinition("MainMenuWindow", "GameHUDWindow", "start_game", SlideLeft);
        yield return new UITransitionDefinition("GameHUDWindow", "PauseMenuWindow", "pause", Instant);
        // ...
    }
}
```

---

## 🎨 Минимум кода = Максимум функциональности

### Вместо Inspector:
```
20 кликов → настройка окон → transitions → кнопки → events
```

### Пишем код:
```csharp
yield return new UITransitionDefinition("From", "To", "trigger", Fade);
```

**Одна строка = полный transition!**

---

## 🔧 Кастомизация

### Изменить стартовое окно:
```csharp
public override string StartWindowId => "GameHUDWindow";
```

### Добавить transition:
```csharp
yield return new UITransitionDefinition("GameHUD", "Shop", "open_shop", Fade);
```

### Обработать навигацию:
```csharp
private void OnNavigated(NavigationEventData data)
{
    if (data.ToWindowId == "Shop") LoadShopData();
}
```

---

## ✅ Поддержка Input System

Автоматически работает с обоими:
```csharp
#if ENABLE_LEGACY_INPUT_MANAGER
    Input.GetKeyDown(KeyCode.Escape)
#elif ENABLE_INPUT_SYSTEM
    Keyboard.current?.escapeKey.wasPressedThisFrame
#endif
```

---

## 📋 Окна для примера

Создайте префабы:
- **MainMenuWindow** - главное меню
- **SettingsWindow** - настройки
- **CreditsWindow** - титры
- **GameHUDWindow** - игровой HUD
- **PauseMenuWindow** - пауза

Или используйте `UISystem → Generate Base Windows`

---

## ⚠️ FAQ

### ExampleGameplayInitializer не появляется в меню?
✅ Перекомпилируйте проект (Ctrl+R)  
✅ Проверьте путь: `{ProjectName}/Scripts/UI/`

### Окна не открываются?
⚠️ Создайте префабы окон  
⚠️ Добавьте в UIWindowGraph

### Input System ошибка?
✅ Исправлено в v1.6.8

---

**v1.6.8** - Code-First UI из коробки! 🎯
