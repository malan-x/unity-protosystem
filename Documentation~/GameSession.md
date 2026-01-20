# GameSessionSystem - Документация

Центральная система координации жизненного цикла игровой сессии для ProtoSystem.

## Обзор

GameSessionSystem решает типичные проблемы:
- **Разрозненное управление состоянием** — единая точка координации
- **Дублирование логики GameOver** — система принимает решения, другие публикуют факты
- **Сложность рестарта** — soft reset через события без перезагрузки сцены
- **Отсутствие единой точки входа** — понятный API для UI

## Архитектура

Система разделена на два компонента:

| Компонент | Назначение | Зависимости |
|-----------|------------|-------------|
| **GameSessionSystem** | Вся логика сессии | Нет (чистый C#) |
| **GameSessionNetworkSync** | Сетевая синхронизация | Unity Netcode |

**Одиночная игра:**
```
[GameSessionSystem] — работает автономно
```

**Мультиплеер:**
```
[GameSessionSystem] + [GameSessionNetworkSync] — на одном GameObject
```

## Принципы

1. **Факты vs Решения** — Системы публикуют факты ("игрок умер"), GameSessionSystem принимает решения ("это Game Over")
2. **Событийная координация** — Сброс состояния через события, не прямые вызовы
3. **Не управляет Time.timeScale** — Это делает UITimeManager через атрибуты окон

## Быстрый старт

### 1. Добавить систему

В SystemInitializationManager добавить GameSessionSystem.

### 2. Для мультиплеера — добавить NetworkSync

На тот же GameObject добавить `GameSessionNetworkSync`:
```
GameObject
├── GameSessionSystem
└── GameSessionNetworkSync (только для мультиплеера)
```

### 3. Создать конфиг (опционально)

**ProtoSystem → Game Session → Create Config**

### 4. Использовать API

```csharp
// Получить систему
var session = SystemInitializationManager.Instance.GetSystem<GameSessionSystem>();

// Старт сессии
session.StartSession();

// Пауза (только состояние, не timeScale)
session.PauseSession();
session.ResumeSession();

// Завершение
session.EndSession(SessionEndReason.PlayerDeath, isVictory: false);
session.EndSession(SessionEndReason.MissionComplete, isVictory: true);

// Рестарт
session.RestartSession();

// Возврат в меню
session.ReturnToMenu();
```

## Состояния сессии

```
┌──────┐     ┌───────────┐     ┌─────────────┐     ┌──────────┐
│ None │────▶│   Ready   │────▶│  Starting   │────▶│  Playing │
└──────┘     └───────────┘     └─────────────┘     └────┬─────┘
                  ▲                                     │
                  │         ┌──────────┐                │
                  │         │  Paused  │◀───────────────┤
                  │         └────┬─────┘                │
                  │              │                      ▼
                  │              │              ┌────────────┐
                  └──────────────┴──────────────│  GameOver  │
                                                │  Victory   │
                                                └────────────┘
```

| Состояние | Описание |
|-----------|----------|
| `None` | Система не инициализирована |
| `Ready` | Готова к старту (главное меню) |
| `Starting` | Идёт инициализация/сброс |
| `Playing` | Игра активна |
| `Paused` | На паузе |
| `GameOver` | Поражение |
| `Victory` | Победа |

## События

### English

```csharp
EventBus.Session.Started        // Сессия началась
EventBus.Session.Ended          // Сессия завершена
EventBus.Session.Reset          // Команда сброса
EventBus.Session.Paused         // Пауза
EventBus.Session.Resumed        // Продолжение
EventBus.Session.StateChanged   // Состояние изменилось
EventBus.Session.ReturnedToMenu // Возврат в меню
EventBus.Session.RestartRequested // Запрос рестарта
```

### Русский (алиас)

```csharp
EventBus.Сессия.Началась
EventBus.Сессия.Завершена
EventBus.Сессия.Сброс
EventBus.Сессия.Пауза
EventBus.Сессия.Продолжена
EventBus.Сессия.Состояние_изменено
EventBus.Сессия.Возврат_в_меню
EventBus.Сессия.Запрос_рестарта
```

### Данные событий

```csharp
// При завершении сессии
struct SessionEndedData
{
    SessionState FinalState;
    SessionEndReason Reason;
    bool IsVictory;
    float SessionTime;
    SessionStats Stats;
}

// При изменении состояния
struct SessionStateChangedData
{
    SessionState PreviousState;
    SessionState NewState;
}
```

## IResettable

Интерфейс для систем с поддержкой мягкого сброса:

```csharp
public class MySystem : InitializableSystemBase, IResettable
{
    private List<Enemy> _enemies = new();
    private float _timer;
    
    public void ResetState()
    {
        // 1. Уничтожить созданные объекты
        foreach (var enemy in _enemies)
            if (enemy != null) Destroy(enemy.gameObject);
        _enemies.Clear();
        
        // 2. Сбросить переменные
        _timer = 0f;
        
        // 3. Пересоздать начальные объекты если нужно
        SpawnInitialEnemies();
    }
}
```

**Автоматический вызов:** SystemInitializationManager.ResetAllResettableSystems() вызывается при Session.Reset.

## SessionStats

Гибкая статистика с произвольными данными:

```csharp
var stats = session.Stats;

// Базовые поля
float time = stats.SessionTime;
int restarts = stats.RestartCount;

// Произвольные данные
stats.Set("enemies_killed", 42);
stats.Set("score", 12500);
stats.Increment("deaths");
stats.IncrementFloat("damage_dealt", 150.5f);

int killed = stats.Get<int>("enemies_killed", 0);
```

## Конфигурация

**GameSessionConfig** (ScriptableObject):

| Параметр | По умолчанию | Описание |
|----------|--------------|----------|
| autoStartSession | false | Автостарт после инициализации |
| initialState | Ready | Начальное состояние |
| restartDelay | 0.1s | Задержка между сбросом и стартом |
| trackRestarts | true | Считать рестарты |
| hostAuthoritative | true | Только хост управляет (при NetworkSync) |
| logEvents | true | Логировать события |
| verboseLogging | false | Подробное логирование |

## Сетевая синхронизация

### Настройка

Добавьте `GameSessionNetworkSync` на тот же GameObject:

```csharp
// В коде или через инспектор
gameObject.AddComponent<GameSessionNetworkSync>();
```

### Как работает

1. **GameSessionSystem** — содержит всю логику
2. **GameSessionNetworkSync** — перехватывает вызовы API и:
   - На сервере: выполняет напрямую + синхронизирует NetworkVariable
   - На клиенте: отправляет ServerRpc → сервер выполняет → NetworkVariable обновляется → клиенты получают

### Синхронизируемые данные

| Данные | Тип | Направление |
|--------|-----|-------------|
| State | NetworkVariable<int> | Server → All |
| EndReason | NetworkVariable<int> | Server → All |
| IsVictory | NetworkVariable<bool> | Server → All |

### Проверки

```csharp
var session = GetSystem<GameSessionSystem>();

// Есть ли сетевой синхронизатор
if (session.HasNetworkSync) { ... }

// Является ли текущий клиент сервером
if (session.IsServer) { ... }

// Может ли текущий клиент управлять
if (session.CanControl) { ... }
```

## Интеграция с UI

```csharp
[UIWindow("game_over", WindowType.Modal, WindowLayer.Modals, PauseGame = true)]
public class GameOverScreen : UIWindowBase
{
    protected override void InitEvents()
    {
        AddEvent(EventBus.Session.Ended, OnSessionEnded);
    }
    
    private void OnSessionEnded(object payload)
    {
        var data = (SessionEndedData)payload;
        if (!data.IsVictory)
        {
            UISystem.Navigate("game_over");
        }
    }
    
    private void OnRestartClicked()
    {
        UISystem.Back();
        var session = SystemInitializationManager.Instance.GetSystem<GameSessionSystem>();
        session.RestartSession();
    }
}
```

## Паттерн "Факты vs Решения"

**Неправильно** — система сама решает что это GameOver:
```csharp
public class PlayerSystem : InitializableSystemBase
{
    private void OnPlayerDeath()
    {
        EventBus.Publish(EventBus.Session.Ended, ...); // ❌
    }
}
```

**Правильно** — система публикует факт, GameSessionSystem решает:
```csharp
public class PlayerSystem : InitializableSystemBase
{
    private void OnPlayerDeath()
    {
        EventBus.Publish(EventBus.Player.Died, data); // ✅
    }
}

// В проекте — подписка на факты и принятие решений
public class MyGameRules : MonoEventBus
{
    protected override void InitEvents()
    {
        AddEvent(EventBus.Player.Died, OnPlayerDied);
    }
    
    private void OnPlayerDied(object payload)
    {
        var session = SystemInitializationManager.Instance.GetSystem<GameSessionSystem>();
        session.EndSession(SessionEndReason.PlayerDeath, false);
    }
}
```

## Debug меню

Контекстное меню на компоненте GameSessionSystem:
- Debug: Start Session
- Debug: Restart Session
- Debug: Pause / Resume
- Debug: End (Game Over)
- Debug: End (Victory)
- Debug: Return To Menu
- Debug: Print Stats

## Причины завершения

```csharp
public enum SessionEndReason
{
    None = 0,
    
    // Поражения (1-99)
    PlayerDeath = 1,
    TimeOut = 2,
    ObjectiveDestroyed = 3,
    ResourcesDepleted = 4,
    
    // Победы (100-199)
    MissionComplete = 100,
    AllEnemiesDefeated = 101,
    ObjectiveReached = 102,
    BossDefeated = 103,
    
    // Прочие (200-299)
    PlayerQuit = 200,
    Disconnect = 201,
    ManualRestart = 202,
    ReturnToMenu = 203
}
```

Проекты могут использовать значения >= 1000 для своих причин.

## Файловая структура

```
Runtime/GameSession/
├── GameSessionSystem.cs        # Основная логика (без сети)
├── GameSessionNetworkSync.cs   # Сетевая синхронизация (Netcode)
├── IGameSessionNetworkSync.cs  # Интерфейс для синхронизации
├── GameSessionConfig.cs        # Конфигурация
├── GameSessionEvents.cs        # События EventBus
├── SessionState.cs             # Enum состояний
├── SessionEndReason.cs         # Enum причин завершения
├── SessionStats.cs             # Статистика
└── IResettable.cs              # Интерфейс сброса
```

## Миграция

### Минимальная интеграция
1. Добавить GameSessionSystem в менеджер
2. Заменить прямые `EventBus.Publish(GameOver)` на `session.EndSession()`

### Для мультиплеера
1. Добавить GameSessionNetworkSync на тот же GameObject
2. Убедиться что NetworkObject присутствует

### Полная интеграция
1. Реализовать IResettable во всех системах с состоянием
2. Использовать паттерн "Факты vs Решения"
3. Использовать RestartSession() вместо перезагрузки сцены
