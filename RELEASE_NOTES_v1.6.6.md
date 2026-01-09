# ProtoSystem v1.6.6 - Integration with Built-in EventBus

## 📅 Дата: 2026-01-09

## 🔄 Критическое исправление интеграции

### Проблема: Визард создавал дубликат EventBus

**Что было не так в v1.6.4-1.6.5:**
```
❌ Визард создавал собственный файл LastConvoyEventBus.cs
❌ Использовал другую архитектуру (EventCategory)
❌ Не интегрировался с встроенной UI панелью ProtoSystem
❌ Namespace не сохранялся в EditorPrefs
❌ Встроенная панель "EventBus проекта" показывала "файл не найден"
```

**Что исправлено в v1.6.6:**
```
✅ Визард использует встроенную функцию EventBusEditorUtils.CreateEventBusFile()
✅ Создаётся файл EventIds.[Namespace].cs (стандарт ProtoSystem)
✅ Правильная архитектура (enum EventType + const int)
✅ Namespace сохраняется в EditorPrefs
✅ Полная интеграция с UI панелью "EventBus проекта"
```

## 📋 Архитектура EventBus в ProtoSystem

### Встроенная система (правильная):

**Файл:** `EventIds.{Namespace}.cs`

**Структура:**
```csharp
namespace MyGame
{
    public static class Evt
    {
        // Enum для гарантии уникальности ID
        public enum EventType
        {
            Gameplay_PlayerSpawned,
            UI_WindowOpened
        }
        
        // Категории с const int
        public static class Gameplay
        {
            public const int PlayerSpawned = (int)EventType.Gameplay_PlayerSpawned;
        }
        
        public static class UI
        {
            public const int WindowOpened = (int)EventType.UI_WindowOpened;
        }
    }
}
```

**Использование:**
```csharp
using static ProtoSystem.EventBus;
using MyGame;

// Публикация
Publish(Evt.Gameplay.PlayerSpawned);

// Подписка
Subscribe(Evt.UI.WindowOpened, OnWindowOpened);
```

### Старый подход визарда (неправильный, удалён):

**Файл:** `{Namespace}EventBus.cs`

**Структура:**
```csharp
public static class MyGameEventBus
{
    public static readonly EventCategory PlayerSpawned = 
        new EventCategory("Gameplay.PlayerSpawned");
}
```

**Почему не работало:**
- ❌ EventCategory устарел
- ❌ Не интегрировался с UI панелью
- ❌ Дублировал функциональность

## 🔧 Для пользователей v1.6.4-1.6.5

### Если создали проект на старой версии визарда:

**Шаг 1 - Удалить старый файл:**
```bash
Assets/YourProject/Scripts/Events/YourProjectEventBus.cs → Delete
```

**Шаг 2 - Пересоздать через визард:**
1. `Tools → ProtoSystem → Project Setup Wizard`
2. **Reset Progress** (внизу)
3. Execute → "Generate EventBus File"

**Шаг 3 - Проверить результат:**
- ✅ Создан файл `EventIds.YourProject.cs`
- ✅ Namespace сохранён в EditorPrefs
- ✅ UI панель "EventBus проекта" видит файл

## ✅ Что теперь делает визард

### Generate EventBus File:

**1. Вызывает встроенную функцию:**
```csharp
string createdPath = EventBusEditorUtils.CreateEventBusFile(_namespace);
```

**2. Создаёт стандартный файл:**
```
Assets/{Namespace}/Scripts/Events/EventIds.{Namespace}.cs
```

**3. Сохраняет в EditorPrefs:**
```csharp
EditorPrefs.SetString(key, filePath);
```

**4. UI панель автоматически находит файл**

## 🎯 Преимущества нового подхода

| Аспект | Старый визард | Новый визард |
|--------|--------------|--------------|
| Архитектура | ❌ EventCategory | ✅ enum + const int |
| Интеграция | ❌ Дубликат | ✅ Встроенная функция |
| UI панель | ❌ Не видит файл | ✅ Полная интеграция |
| EditorPrefs | ❌ Не сохраняет | ✅ Сохраняет namespace |
| Стандарт ProtoSystem | ❌ Нет | ✅ Да |

## 📝 Правильное использование EventBus

### Добавление новых событий:

**В файле EventIds.{Namespace}.cs:**
```csharp
public enum EventType
{
    // Добавить в enum
    Gameplay_EnemyKilled,
}

public static class Gameplay
{
    public const int PlayerSpawned = (int)EventType.Gameplay_PlayerSpawned;
    public const int EnemyKilled = (int)EventType.Gameplay_EnemyKilled; // ← новое
}
```

**Использование:**
```csharp
using static ProtoSystem.EventBus;
using MyGame;

// Публикация
Publish(Evt.Gameplay.EnemyKilled, enemyData);

// Подписка
Subscribe(Evt.Gameplay.EnemyKilled, OnEnemyKilled);
```

## 🔗 Связанные изменения

### В коде визарда:

**Было:**
```csharp
private void CreateEventBus()
{
    // 70+ строк генерации шаблона
    string template = $@"namespace {_namespace}.Events ...";
    File.WriteAllText(path, template);
}
```

**Стало:**
```csharp
private void CreateEventBus()
{
    // Используем встроенную функцию
    string createdPath = EventBusEditorUtils.CreateEventBusFile(_namespace);
}
```

### UI панель "EventBus проекта":

Теперь правильно отображает:
- ✅ Namespace проекта (из EditorPrefs)
- ✅ Количество событий
- ✅ Кнопку "Создать EventBus файл" (если не найден)
- ✅ Информацию о существующем файле

## 🔄 История версий

**v1.6.6** - Интеграция с встроенной системой EventBus ✅
**v1.6.5** - (попытка исправить, но всё ещё дубликат)
**v1.6.4** - Попытка создать EventBus (неправильная архитектура)
**v1.6.3** - GUID-based assembly references
**v1.6.2** - (пропущена)
**v1.6.1** - ProjectSetupWizard, camera/lighting

## ⚠️ Важно

**Для новых проектов:**
- ✅ Просто используйте визард - всё будет правильно!

**Для существующих проектов на v1.6.4-1.6.5:**
- ⚠️ Удалите старый файл EventBus
- ✅ Пересоздайте через Reset Progress

**Assembly Definition:**
- ✅ Должен иметь GUID reference на ProtoSystem
- ✅ Иначе EventBusEditorUtils не найдётся

---

**Обновите до v1.6.6 для правильной интеграции!** 🎯
