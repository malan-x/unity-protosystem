# 📡 ProtoSystem EventBus - Правильное использование

## Что создаёт визард

Визард использует **встроенную функцию ProtoSystem** для создания EventBus файла:

```csharp
EventBusEditorUtils.CreateEventBusFile(namespace);
```

**Результат:**
- ✅ Файл: `Assets/{Namespace}/Scripts/Events/EventIds.{Namespace}.cs`
- ✅ Namespace сохраняется в EditorPrefs
- ✅ Интеграция с UI панелью "EventBus проекта"

## 📋 Структура EventIds файла

### Сгенерированный шаблон:

```csharp
namespace MyGame
{
    /// <summary>
    /// Короткий алиас для ID событий проекта
    /// </summary>
    public static class Evt
    {
        /// <summary>
        /// Перечисление всех событий для гарантии уникальности ID
        /// </summary>
        public enum EventType
        {
            // Добавляйте события сюда
        }
        
        // Категории событий будут здесь
    }
}
```

## ✏️ Добавление событий

### Шаг 1: Добавить в enum

```csharp
public enum EventType
{
    // Gameplay категория
    Gameplay_PlayerSpawned,
    Gameplay_PlayerDied,
    Gameplay_EnemyKilled,
    
    // UI категория
    UI_WindowOpened,
    UI_WindowClosed,
}
```

### Шаг 2: Создать статические классы категорий

```csharp
/// <summary>
/// Игровые события
/// </summary>
public static class Gameplay
{
    public const int PlayerSpawned = (int)EventType.Gameplay_PlayerSpawned;
    public const int PlayerDied = (int)EventType.Gameplay_PlayerDied;
    public const int EnemyKilled = (int)EventType.Gameplay_EnemyKilled;
}

/// <summary>
/// UI события
/// </summary>
public static class UI
{
    public const int WindowOpened = (int)EventType.UI_WindowOpened;
    public const int WindowClosed = (int)EventType.UI_WindowClosed;
}
```

## 🎯 Использование в коде

### Отправка события (Publish):

```csharp
using static ProtoSystem.EventBus;
using MyGame;

public class Player : MonoBehaviour
{
    void Start()
    {
        // Без данных
        Publish(Evt.Gameplay.PlayerSpawned);
        
        // С данными
        Publish(Evt.Gameplay.EnemyKilled, new EnemyData { id = 123 });
    }
}
```

### Подписка на события (Subscribe):

```csharp
using static ProtoSystem.EventBus;
using MyGame;
using ProtoSystem;

public class GameUISystem : InitializableSystemBase
{
    protected override void InitEvents()
    {
        // Подписка без данных
        Subscribe(Evt.Gameplay.PlayerSpawned, OnPlayerSpawned);
        
        // Подписка с данными
        Subscribe<EnemyData>(Evt.Gameplay.EnemyKilled, OnEnemyKilled);
    }
    
    private void OnPlayerSpawned()
    {
        Debug.Log("Player spawned!");
    }
    
    private void OnEnemyKilled(EnemyData data)
    {
        Debug.Log($"Enemy {data.id} killed!");
    }
}
```

### Отписка:

```csharp
// В OnDisable или OnDestroy
Unsubscribe(Evt.Gameplay.PlayerSpawned, OnPlayerSpawned);
Unsubscribe<EnemyData>(Evt.Gameplay.EnemyKilled, OnEnemyKilled);
```

## 🎨 UI панель "EventBus проекта"

После создания файла через визард, в Inspector на SystemInitializationManager появляется панель:

**Показывает:**
- ✅ Namespace проекта
- ✅ Путь к файлу EventIds
- ✅ Количество событий
- ✅ Количество категорий
- ✅ Кнопка "Открыть файл"

**Если файл не найден:**
- Поле ввода Namespace
- Кнопка "Создать EventBus файл"

## ⚙️ Как визард интегрируется

### В ProjectSetupWizard.cs:

```csharp
private void CreateEventBus()
{
    // Использует встроенную функцию
    string createdPath = EventBusEditorUtils.CreateEventBusFile(_namespace);
    
    if (!string.IsNullOrEmpty(createdPath))
    {
        Debug.Log($"✅ EventBus file created: {createdPath}");
    }
}
```

### Что делает EventBusEditorUtils:

1. Проверяет namespace
2. Создаёт путь: `Assets/{Namespace}/Scripts/Events/EventIds.{Namespace}.cs`
3. Генерирует шаблон через `GenerateEventBusTemplate()`
4. Сохраняет namespace в EditorPrefs
5. Обновляет AssetDatabase

## 💡 Лучшие практики

### 1. Именование событий:

```csharp
// ✅ Хорошо
Gameplay_PlayerSpawned
UI_WindowOpened
Combat_WeaponFired

// ❌ Плохо
Event1
Evt_A
Thing_Happened
```

### 2. Группировка по категориям:

```csharp
// Геймплей
public static class Gameplay { ... }

// Интерфейс
public static class UI { ... }

// Бой
public static class Combat { ... }

// Сеть
public static class Network { ... }
```

### 3. Типизация данных:

```csharp
// Создайте классы для данных
public class PlayerData
{
    public int id;
    public Vector3 position;
    public int health;
}

// Используйте в событиях
Publish(Evt.Gameplay.PlayerMoved, new PlayerData { ... });
Subscribe<PlayerData>(Evt.Gameplay.PlayerMoved, OnPlayerMoved);
```

## 🔄 Миграция со старой версии визарда

### Если у вас есть файл {Namespace}EventBus.cs:

**Шаг 1 - Удалить старый:**
```
Assets/{Namespace}/Scripts/Events/{Namespace}EventBus.cs → Delete
```

**Шаг 2 - Пересоздать через визард:**
1. `Tools → ProtoSystem → Project Setup Wizard`
2. **Reset Progress**
3. Execute → "Generate EventBus File"

**Шаг 3 - Обновить код:**
```csharp
// Было (EventCategory):
MonoEventBus.RaiseEvent(MyGameEventBus.PlayerSpawned);

// Стало (int ID):
using static ProtoSystem.EventBus;
Publish(Evt.Gameplay.PlayerSpawned);
```

## ❓ FAQ

**Q: Зачем enum EventType?**
A: Гарантирует уникальность ID событий в compile-time.

**Q: Можно ли использовать string вместо int?**
A: Нет, ProtoSystem EventBus использует int для производительности.

**Q: Где хранится namespace?**
A: В EditorPrefs с ключом специфичным для проекта.

**Q: Можно ли переименовать файл?**
A: Не рекомендуется, UI панель ищет файл по паттерну `EventIds.*.cs`.

**Q: Нужно ли вручную редактировать EventIds файл?**
A: Да, вы добавляете туда свои события по мере разработки.

---

**v1.6.6+** - Полная интеграция с встроенной системой EventBus! 🎯
