# ProtoSystem v1.6.4 - EventBus Generation & Settings Fix

## 📅 Дата: 2026-01-09

## 🔧 Критические исправления

### 1. EventCategories.cs заменён на EventBus файл

**Было:**
- ❌ Создавался `EventCategories.cs` с категориями
- ❌ Без примеров использования
- ❌ Избыточная абстракция

**Стало:**
- ✅ Создаётся `[ProjectName]EventBus.cs`
- ✅ Готовые события с примерами
- ✅ Комментарии как использовать
- ✅ Namespace автоматически подставляется

**Пример сгенерированного файла:**
```csharp
namespace MyGame.Events
{
    public static class MyGameEventBus
    {
        // Готовые события
        public static readonly EventCategory PlayerSpawned = 
            new EventCategory("Gameplay.PlayerSpawned");
        
        // Примеры использования в комментариях
        // MonoEventBus.RaiseEvent(MyGameEventBus.PlayerSpawned);
    }
}
```

### 2. Project Name сбрасывался после Execute

**Проблема:**
- После выполнения задач настройки терялись
- Project Name возвращался к "MyGame"

**Решение:**
- `SaveSettings()` вызывается после каждой задачи
- Настройки сохраняются в EditorPrefs немедленно

### 3. Namespace не подставляется автоматически

**Проблема:**
- В EventBus окне namespace был пустой
- Пользователь должен был вводить вручную

**Решение:**
- EventBus файл генерируется с правильным namespace
- Файл называется `[ProjectName]EventBus.cs` для ясности
- Готов к использованию сразу после создания

## 📦 Что изменилось

### Структура проекта после Setup:

**Было:**
```
Scripts/
└── Events/
    └── EventCategories.cs  ← общие категории
```

**Стало:**
```
Scripts/
└── Events/
    └── MyGameEventBus.cs  ← готовые события проекта
```

### Содержимое EventBus файла:

```csharp
using ProtoSystem;

namespace MyGame.Events
{
    public static class MyGameEventBus
    {
        // ИНИЦИАЛИЗАЦИЯ
        public static readonly EventCategory GameInitialized = 
            new EventCategory("Core.GameInitialized");
        
        // ГЕЙМПЛЕЙ
        public static readonly EventCategory PlayerSpawned = 
            new EventCategory("Gameplay.PlayerSpawned");
        public static readonly EventCategory PlayerDied = 
            new EventCategory("Gameplay.PlayerDied");
        
        // UI
        public static readonly EventCategory WindowOpened = 
            new EventCategory("UI.WindowOpened");
        public static readonly EventCategory WindowClosed = 
            new EventCategory("UI.WindowClosed");
        
        // ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ:
        // 
        // Отправка:
        // MonoEventBus.RaiseEvent(MyGameEventBus.PlayerSpawned);
        //
        // Подписка:
        // AddEvent(MyGameEventBus.PlayerSpawned, OnPlayerSpawned);
    }
}
```

## ✅ Преимущества нового подхода

| Аспект | EventCategories | EventBus |
|--------|----------------|----------|
| Готовность | ❌ Только категории | ✅ Готовые события |
| Примеры | ❌ Нет | ✅ В комментариях |
| Именование | ❌ Generic | ✅ Специфичное (MyGameEventBus) |
| Namespace | ❌ Руками | ✅ Автоматически |
| Integration | ❌ Требует работы | ✅ Ready to use |

## 🎯 Для пользователей

### Если использовали старый визард (v1.6.1-1.6.3):

**Удалите старый файл:**
```bash
Scripts/Events/EventCategories.cs  ← можно удалить
```

**Пересоздайте через визард:**
1. `Tools → ProtoSystem → Project Setup Wizard`
2. Execute → "Generate EventBus File"
3. Получите `[YourProject]EventBus.cs` с примерами

### Для новых проектов:

Просто используйте визард - получите готовый EventBus файл! 🎉

## 📝 Примеры использования сгенерированного EventBus

### Отправка события:
```csharp
using MyGame.Events;

public class PlayerController : MonoBehaviour
{
    void Start()
    {
        MonoEventBus.RaiseEvent(MyGameEventBus.PlayerSpawned);
    }
}
```

### Подписка на событие:
```csharp
using MyGame.Events;
using ProtoSystem;

public class GameUISystem : InitializableSystemBase
{
    protected override void InitEvents()
    {
        AddEvent(MyGameEventBus.PlayerSpawned, OnPlayerSpawned);
        AddEvent(MyGameEventBus.WindowOpened, OnWindowOpened);
    }
    
    private void OnPlayerSpawned()
    {
        Debug.Log("Player spawned - update UI!");
    }
    
    private void OnWindowOpened()
    {
        // Handle window opened
    }
}
```

## 🔄 История версий

**v1.6.4** - EventBus generation, settings fix
**v1.6.3** - GUID-based assembly references
**v1.6.2** - (skipped)
**v1.6.1** - ProjectSetupWizard with camera/lighting
**v1.6.0** - Initial release

## 🎁 Бонус

EventBus файл теперь служит:
- 📚 Документацией событий проекта
- 🎯 Единой точкой для всех событий
- 💡 Примером для новых разработчиков
- ✨ Готовым к использованию кодом

---

**Обновите до v1.6.4** для улучшенного experience! 🚀
