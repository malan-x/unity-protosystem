# GameSessionSystem - Документация

Центральная система координации жизненного цикла игровой сессии для ProtoSystem.

## Обзор

GameSessionSystem решает типичные проблемы:
- **Разрозненное управление состоянием** — единая точка координации
- **Дублирование логики GameOver** — система принимает решения, другие публикуют факты
- **Сложность рестарта** — soft reset через события без перезагрузки сцены
- **Отсутствие единой точки входа** — понятный API для UI

## Принципы

1. **Факты vs Решения** — Системы публикуют факты ("игрок умер"), GameSessionSystem принимает решения ("это Game Over")
2. **Событийная координация** — Сброс состояния через события, не прямые вызовы
3. **Не управляет Time.timeScale** — Это делает UITimeManager через атрибуты окон

## Быстрый старт

### 1. Добавить систему

В SystemInitializationManager добавить GameSessionSystem или через меню:
**GameObject → Component → ProtoSystem**

### 2. Создать конфиг (опционально)

**ProtoSystem → Game Session → Create Config**

Или через **Create → ProtoSystem → Game Session Config**

### 3. Использовать API

```csharp
// Получить систему через SystemProvider
var session = SystemInitializationManager.Instance.SystemProvider.GetSystem<GameSessionSystem>();

// Или напрямую через менеджер
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
| logEvents | true | Логировать события |
| verboseLogging | false | Подробное логирование |
| syncOverNetwork | true | Синхронизация по сети |
| hostAuthoritative | true | Только хост управляет сессией |

## Сетевая синхронизация

Состояние сессии автоматически синхронизируется:
- NetworkVariable для состояния, причины завершения, флага победы
- ServerRpc для команд
- ClientRpc для уведомлений

```csharp
// Клиент запрашивает, сервер выполняет
session.StartSession();  // -> StartSessionServerRpc
session.EndSession(...); // -> EndSessionServerRpc
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

public class GameSessionSystem : ...
{
    protected override void InitEvents()
    {
        AddEvent(EventBus.Player.Died, OnPlayerDied);
    }
    
    private void OnPlayerDied(object payload)
    {
        // Решение принимается здесь
        EndSession(SessionEndReason.PlayerDeath, false);
    }
}
```

## Debug меню

Контекстное меню на компоненте:
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

## Миграция

### Минимальная интеграция
1. Добавить GameSessionSystem в менеджер
2. Заменить прямые `EventBus.Publish(GameOver)` на `session.EndSession()`

### Полная интеграция
1. Реализовать IResettable во всех системах с состоянием
2. Использовать паттерн "Факты vs Решения"
3. Использовать RestartSession() вместо перезагрузки сцены
