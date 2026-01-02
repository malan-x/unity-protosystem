# ProtoSystem — Quick Start Guide

Быстрая интеграция ProtoSystem в Unity проект.

## 1. Установка

Добавить пакет через Package Manager → Add package from git URL:
```
https://github.com/your-repo/ProtoSystem.git
```

Или скопировать папку `com.protosystem.core` в `Packages/`.

## 2. Базовая настройка

### 2.1 Создать EventIds

```csharp
// Assets/YourProject/Scripts/Events/EventIds.cs
namespace YourProject
{
    public static class Evt
    {
        public enum EventType
        {
            GameStarted,
            PlayerSpawned,
            EnemyKilled,
        }

        public static class Game
        {
            public const int Started = (int)EventType.GameStarted;
        }
        
        public static class Player
        {
            public const int Spawned = (int)EventType.PlayerSpawned;
        }
    }
}
```

### 2.2 Добавить SystemInitializationManager

1. Создать пустой GameObject "Systems"
2. Add Component → ProtoSystem → System Initialization Manager
3. В инспекторе нажать "Добавить ProtoSystem компоненты"

### 2.3 Настроить UISystem

1. **Create → ProtoSystem → UI System Config**
2. Сгенерировать базовые префабы: **ProtoSystem → UI → Generate All Base Windows**
3. В UISystemConfig нажать **Scan & Add Prefabs**
4. Добавить UISystem на сцену (Add Component → UISystem)
5. Назначить UISystemConfig

## 3. Создание системы

```csharp
using ProtoSystem;
using YourProject;

public class GameSystem : InitializableSystemBase
{
    public override string SystemId => "game";
    public override string DisplayName => "Game System";
    
    protected override void InitEvents()
    {
        AddEvent(Evt.Player.Spawned, OnPlayerSpawned);
    }
    
    public override async Task<bool> InitializeAsync()
    {
        ReportProgress(0.5f);
        // Инициализация
        ReportProgress(1.0f);
        return true;
    }
    
    private void OnPlayerSpawned(object payload)
    {
        Debug.Log("Player spawned!");
    }
    
    public void StartGame()
    {
        EventBus.Publish(Evt.Game.Started, null);
    }
}
```

## 4. Создание UI окна

```csharp
using ProtoSystem.UI;

[UIWindow("my_dialog", WindowType.Modal, WindowLayer.Modals,
    Level = 2, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
public class MyDialog : UIWindowBase
{
    [SerializeField] private Button closeButton;
    
    protected override void OnOpened(object context)
    {
        closeButton.onClick.AddListener(() => UISystem.Back());
    }
    
    protected override void OnClosed()
    {
        closeButton.onClick.RemoveAllListeners();
    }
}
```

## 5. Навигация UI

```csharp
// Открыть окно
UISystem.Open("settings");

// С контекстом
UISystem.Open("dialog", new { title = "Hello" });

// Назад
UISystem.Back();

// Закрыть конкретное
UISystem.Close("my_dialog");
```

## 6. Настройка переходов сцены

```csharp
public class GameplayInitializer : MonoBehaviour, IUISceneInitializer
{
    public string[] GetStartupWindows() => new[] { "game_hud" };
    
    public UITransition[] GetAdditionalTransitions() => new[]
    {
        new UITransition("game_hud", "pause_menu"),
        new UITransition("pause_menu", "settings"),
    };
}
```

Назначить в UISystem.sceneInitializerComponent.

## 7. Чеклист

- [ ] Создан EventIds.cs с событиями проекта
- [ ] SystemInitializationManager на сцене
- [ ] UISystemConfig создан и настроен
- [ ] UI префабы сгенерированы и добавлены
- [ ] IUISceneInitializer настроен для каждой сцены
- [ ] Системы добавлены и зарегистрированы

## Полезные ссылки

- [README.md](README.md) — Обзор пакета
- [AI_INSTRUCTIONS.md](Documentation~/AI_INSTRUCTIONS.md) — Инструкции для ИИ
- [UISystem.md](Documentation~/UISystem.md) — Документация UI
- [CHANGELOG.md](CHANGELOG.md) — История изменений
