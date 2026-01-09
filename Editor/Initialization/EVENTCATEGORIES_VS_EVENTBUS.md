# EventCategories vs EventBus - Сравнение

## ❌ Старый подход (EventCategories)

```csharp
using ProtoSystem;

namespace MyGame.Events
{
    public static class EventCategories
    {
        // Просто категории
        public static readonly EventCategory Core = new EventCategory("Core");
        public static readonly EventCategory Gameplay = new EventCategory("Gameplay");
        public static readonly EventCategory UI = new EventCategory("UI");
    }
}
```

**Проблемы:**
- ❌ Слишком абстрактно (Core, Gameplay - что конкретно?)
- ❌ Нет готовых событий
- ❌ Нет примеров использования
- ❌ Требует доработки для реального использования

## ✅ Новый подход (EventBus)

```csharp
using ProtoSystem;

namespace MyGame.Events
{
    /// <summary>
    /// Центральная шина событий проекта
    /// </summary>
    public static class MyGameEventBus
    {
        // ============================================================
        // ГОТОВЫЕ СОБЫТИЯ
        // ============================================================
        
        /// <summary>Игра полностью инициализирована</summary>
        public static readonly EventCategory GameInitialized = 
            new EventCategory("Core.GameInitialized");
        
        /// <summary>Игрок заспавнился</summary>
        public static readonly EventCategory PlayerSpawned = 
            new EventCategory("Gameplay.PlayerSpawned");
        
        /// <summary>Игрок умер</summary>
        public static readonly EventCategory PlayerDied = 
            new EventCategory("Gameplay.PlayerDied");
        
        /// <summary>Открыто окно</summary>
        public static readonly EventCategory WindowOpened = 
            new EventCategory("UI.WindowOpened");
        
        
        // ============================================================
        // ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ
        // ============================================================
        
        // Отправка:
        // MonoEventBus.RaiseEvent(MyGameEventBus.PlayerSpawned);
        
        // Подписка:
        // protected override void InitEvents()
        // {
        //     AddEvent(MyGameEventBus.PlayerSpawned, OnPlayerSpawned);
        // }
    }
}
```

**Преимущества:**
- ✅ Конкретные события (PlayerSpawned, WindowOpened)
- ✅ XML документация для IntelliSense
- ✅ Примеры использования прямо в коде
- ✅ Готово к использованию немедленно
- ✅ Служит документацией проекта

## 📊 Таблица сравнения

| Критерий | EventCategories | EventBus |
|----------|----------------|----------|
| **Готовность** | ❌ Требует доработки | ✅ Ready to use |
| **Примеры** | ❌ Нет | ✅ В комментариях |
| **Документация** | ❌ Минимальная | ✅ XML + примеры |
| **Специфичность** | ❌ Generic имена | ✅ Specific события |
| **IntelliSense** | ⚠️ Базовый | ✅ С описаниями |
| **Namespace** | ⚠️ Руками | ✅ Автоматически |
| **Масштабируемость** | ✅ Да | ✅ Да |

## 🎯 Когда что использовать?

### EventCategories (старый):
- Если нужна максимальная гибкость
- Если категории создаются динамически
- Если используете сторонние библиотеки

### EventBus (новый):
- ✅ **Для новых проектов** (рекомендуется)
- ✅ **Для командной работы** (единый стандарт)
- ✅ **Для быстрого старта** (готовые примеры)
- ✅ **Для документирования** (централизация)

## 🔄 Миграция с EventCategories на EventBus

### Шаг 1: Создать EventBus
```bash
Tools → ProtoSystem → Project Setup Wizard
Execute → "Generate EventBus File"
```

### Шаг 2: Заменить в коде
```csharp
// Было:
MonoEventBus.RaiseEvent(EventCategories.Gameplay);

// Стало:
MonoEventBus.RaiseEvent(MyGameEventBus.PlayerSpawned);
```

### Шаг 3: Удалить старый файл
```bash
Assets/MyProject/Scripts/Events/EventCategories.cs → Delete
```

## 💡 Лучшие практики

### 1. Группировка событий
```csharp
// ============================================================
// INVENTORY EVENTS
// ============================================================
public static readonly EventCategory ItemPickedUp = ...;
public static readonly EventCategory ItemDropped = ...;
```

### 2. XML документация
```csharp
/// <summary>Игрок подобрал предмет</summary>
/// <remarks>Используйте для обновления UI инвентаря</remarks>
public static readonly EventCategory ItemPickedUp = ...;
```

### 3. Семантические имена
```csharp
// ❌ Плохо
public static readonly EventCategory Event1 = ...;

// ✅ Хорошо
public static readonly EventCategory PlayerHealthChanged = ...;
```

---

**Используйте EventBus в новых проектах! 🚀**
