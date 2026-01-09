# 🎯 Quick Fix Guide - EventBus & Settings

## Что исправлено в v1.6.4+

### ✅ 1. EventCategories → EventBus

**Было:**
```csharp
// EventCategories.cs
public static class EventCategories {
    public static readonly EventCategory Core = new EventCategory("Core");
}
```

**Стало:**
```csharp
// MyGameEventBus.cs
public static class MyGameEventBus {
    // Готовое событие
    public static readonly EventCategory PlayerSpawned = 
        new EventCategory("Gameplay.PlayerSpawned");
    
    // Пример использования в комментариях ↓
    // MonoEventBus.RaiseEvent(MyGameEventBus.PlayerSpawned);
}
```

### ✅ 2. Project Name сохраняется

- После Execute настройки больше не сбрасываются
- Можно продолжить работу в любой момент

### ✅ 3. Namespace автоматически

EventBus файл создаётся с правильным namespace:
```csharp
namespace YourProjectName.Events
{
    public static class YourProjectNameEventBus
    {
        // ...
    }
}
```

## 📋 Как использовать сгенерированный EventBus

### Отправка события:
```csharp
using MyGame.Events;

MonoEventBus.RaiseEvent(MyGameEventBus.PlayerSpawned);
```

### Подписка в системе:
```csharp
using MyGame.Events;

public class MySystem : InitializableSystemBase
{
    protected override void InitEvents()
    {
        AddEvent(MyGameEventBus.PlayerSpawned, OnPlayerSpawned);
    }
    
    private void OnPlayerSpawned()
    {
        Debug.Log("Player spawned!");
    }
}
```

## 🔧 Для существующих проектов

### Если у вас есть EventCategories.cs:

1. **Удалите** старый файл:
   ```
   Assets/YourProject/Scripts/Events/EventCategories.cs
   ```

2. **Создайте** новый через визард:
   - `Tools → ProtoSystem → Project Setup Wizard`
   - Execute → "Generate EventBus File"

3. **Обновите** ссылки в коде:
   ```csharp
   // Было:
   EventCategories.Core
   
   // Стало:
   MyGameEventBus.GameInitialized
   ```

## 💡 Преимущества EventBus

| Фича | Описание |
|------|----------|
| ✅ Готовые события | PlayerSpawned, WindowOpened и т.д. |
| ✅ Примеры в коде | Комментарии показывают как использовать |
| ✅ Специфичное имя | MyGameEventBus вместо EventCategories |
| ✅ Документация | Служит справочником событий проекта |

## 🎯 События "из коробки"

Визард генерирует:
- **Системные:** GameInitialized, GameShutdown
- **Геймплей:** PlayerSpawned, PlayerDied
- **UI:** WindowOpened, WindowClosed

Добавляйте свои по аналогии! 🚀

---

**v1.6.4+** - EventBus generation included
