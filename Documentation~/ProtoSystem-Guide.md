# ProtoSystem Core — Руководство разработчика

## Обзор

ProtoSystem — это Unity-фреймворк для создания модульных, event-driven приложений с автоматической инициализацией систем. Он сочетает глобальный EventBus, attribute-driven dependency injection и асинхронную загрузку.

## Архитектура

### Основные компоненты

```
ProtoSystem Core (Package)
├── EventBus/           — Глобальная шина событий
├── Initialization/     — Система инициализации с DI
├── Network/            — Базовые классы для сетевых систем
└── Base/               — Базовые классы и интерфейсы

Project Code (Assets)
├── Events/             — Проектные события (EventIds.*.cs)
├── Systems/            — Игровые системы
└── Commands/           — Команды для сложных операций
```

## Основные принципы

### 1. Event-Driven Architecture

Все системы общаются через события, что обеспечивает слабую связанность:

```csharp
// Публикация события
EventBus.Publish(Evt.Категория.Событие, payload);

// Подписка в системе
protected override void InitEvents()
{
    AddEvent(Evt.Категория.Событие, OnEventHandler);
}
```

### 2. Dependency Injection через атрибуты

Системы автоматически получают зависимости:

```csharp
public class MySystem : InitializableSystemBase
{
    [Dependency] private OtherSystem otherSystem;      // Обязательная зависимость
    [PostDependency] private OptionalSystem optional;  // Опциональная (после основной инициализации)
}
```

### 3. Асинхронная инициализация

Каждая система инициализируется асинхронно с отчетом прогресса:

```csharp
public override async Task<bool> InitializeAsync()
{
    ReportProgress(0.3f);
    await LoadResources();
    ReportProgress(0.7f);
    await SetupComponents();
    ReportProgress(1.0f);
    return true;
}
```

### 4. Топологическая сортировка зависимостей

SystemInitializationManager автоматически:
- Анализирует граф зависимостей
- Определяет порядок инициализации
- Обнаруживает циклические зависимости

## Создание новой системы

### Шаг 1: Наследование от базового класса

```csharp
using ProtoSystem;

public class MyGameSystem : InitializableSystemBase
{
    public override string SystemId => "my_game_system";
    public override string DisplayName => "My Game System";
    
    // Зависимости
    [Dependency] private InputSystem inputSystem;
    
    protected override void InitEvents()
    {
        AddEvent(Evt.Ввод.Кнопка_нажата, OnButtonPressed);
    }
    
    public override async Task<bool> InitializeAsync()
    {
        ReportProgress(0.5f);
        // Инициализация...
        ReportProgress(1.0f);
        return true;
    }
    
    private void OnButtonPressed(object payload)
    {
        // Обработка события
    }
}
```

### Шаг 2: Для сетевых систем

```csharp
public class MyNetworkSystem : NetworkInitializableSystem
{
    public override string SystemId => "my_network_system";
    
    protected override void InitEvents()
    {
        AddEvent(Evt.Сеть.Игрок_подключился, OnPlayerConnected);
    }
    
    // Используйте хелперы для сетевых событий:
    // PublishEventServerOnly(eventId, payload)
    // PublishEventClientOnly(eventId, payload)
    // PublishEventIfLocalPlayer(eventId, payload)
}
```

### Шаг 3: Регистрация в сцене

1. Добавьте `SystemInitializationManager` в сцену
2. Добавьте систему в список через Inspector
3. Запустите "Анализировать зависимости"

## Проектные события (EventIds)

События определяются в отдельном файле проекта с использованием enum для гарантии уникальности ID:

```csharp
// Assets/YourProject/Scripts/Events/EventIds.YourProject.cs
namespace YourProject
{
    public static class Evt
    {
        /// <summary>
        /// Перечисление всех событий проекта для гарантии уникальности ID
        /// </summary>
        public enum EventType
        {
            // === ВАШИ КАТЕГОРИИ ===
            Событие_1,
            Событие_2,
            // Добавляйте новые события сюда
        }

        public static class МояКатегория
        {
            public const int Событие_1 = (int)EventType.Событие_1;
            public const int Событие_2 = (int)EventType.Событие_2;
        }
    }
}
```

**Важно:** Все новые события добавляйте в `EventType` enum — это гарантирует отсутствие дубликатов ID. Enum автоматически присваивает последовательные значения.

Использование:
```csharp
using YourProject;
using static ProtoSystem.EventBus;

// Теперь можно писать:
Publish(Evt.МояКатегория.Событие_1, data);
```

## Command Pattern

Для сложных операций (особенно сетевых) используйте команды:

```csharp
public interface ICommand
{
    void Execute();
    void Undo();
}

public class MyCommand : ICommand
{
    public void Execute()
    {
        // Выполнение операции
        EventBus.Publish(Evt.Категория.Операция_выполнена, data);
    }
    
    public void Undo()
    {
        // Откат операции
    }
}
```

## Facade Pattern

Скрывайте сложность за фасадами:

```csharp
public class MySystemFacade : InitializableSystemBase
{
    [Dependency] private StateManager stateManager;
    [Dependency] private CommandExecutor commandExecutor;
    
    // Простой API для внешнего использования
    public void DoSomething(int param)
    {
        var command = new DoSomethingCommand(param);
        commandExecutor.Execute(command);
    }
}
```

## Тестирование

Создавайте изолированные тестовые сцены:

```
Assets/Scenes/Tests/
├── TestMySystem.unity      — Тест отдельной системы
├── TestIntegration.unity   — Интеграционные тесты
```

В тестовой сцене добавьте только необходимые системы в SystemInitializationManager.

## Best Practices

1. **Один файл — одна система**: Каждая система в отдельном файле
2. **Папки по функционалу**: Commands/, Visual/, UI/ внутри системы
3. **Явные зависимости**: Используйте `[Dependency]` вместо FindObjectOfType
4. **Логирование**: Используйте `LogMessage/LogWarning/LogError` с `verboseLogging`
5. **Валидация**: Проверяйте IsSpawned/IsServer/IsClient для сетевых систем
6. **Прогресс**: Всегда вызывайте `ReportProgress()` в `InitializeAsync()`

## Типичные ошибки

| Ошибка | Причина | Решение |
|--------|---------|---------|
| Циклическая зависимость | A→B→A | Используйте события или `[PostDependency]` |
| Система не инициализирована | Не добавлена в Manager | Добавьте в SystemInitializationManager |
| События не приходят | Не вызван InitEvents | Проверьте наследование от базового класса |
| NullReferenceException на зависимости | Порядок инициализации | Проверьте граф зависимостей |

## Структура проекта

```
Assets/
├── YourProject/
│   ├── Scripts/
│   │   ├── Events/
│   │   │   └── EventIds.YourProject.cs
│   │   ├── Systems/
│   │   │   ├── Input/
│   │   │   ├── Audio/
│   │   │   └── Network/
│   │   ├── Commands/
│   │   └── Core/
│   └── Scenes/
│       └── Tests/
Packages/
└── com.protosystem.core/
```

## Полезные ссылки

- [EventBus API](./EventBus-API.md)
- [Initialization API](./Initialization-API.md)
- [Network Systems](./Network-Systems.md)
