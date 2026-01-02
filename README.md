# ProtoSystem Core

Модульный Unity фреймворк для быстрого прототипирования с системой инициализации, событийной архитектурой и UI.

## Возможности

### Ядро
- **EventBus** — Глобальная система событий с группировкой и автоподпиской
- **System Initialization** — Атрибутивное внедрение зависимостей и управление порядком инициализации
- **Network Support** — Встроенная поддержка Netcode for GameObjects

### UI Система
- **UISystem** — Граф-ориентированная навигация между окнами
- **UINavigator** — Стековая навигация с историей (Back, CloseTopModal)
- **Window Prefab Generator** — Автогенерация UI префабов из редактора
- **UITimeManager** — Управление паузой игры для UI
- **CursorManagerSystem** — Стек состояний курсора

### Дополнительные системы
- **SettingsSystem** — Управление настройками (INI формат)
- **EffectsManager** — Система визуальных эффектов
- **SceneFlowSystem** — Управление переходами между сценами

## Быстрый старт

См. [QUICKSTART.md](QUICKSTART.md) для быстрой интеграции.

## Документация

- [ProtoSystem Guide](Documentation~/ProtoSystem-Guide.md) — Основная документация
- [UISystem Guide](Documentation~/UISystem.md) — Документация UI системы
- [SettingsSystem Guide](Documentation~/SettingsSystem.md) — Система настроек
- [AI Instructions](Documentation~/AI_INSTRUCTIONS.md) — Инструкции для ИИ-ассистентов
- [Changelog](CHANGELOG.md) — История изменений

## Установка

### Package Manager (Git URL)
```
https://github.com/your-repo/ProtoSystem.git
```

### Локально (Packages/)
Скопировать папку `com.protosystem.core` в `Packages/` проекта.

## Структура пакета

```
com.protosystem.core/
├── Runtime/
│   ├── EventBus/          # Система событий
│   ├── Initialization/    # Инициализация и DI
│   ├── UI/                # UI система
│   │   ├── Core/          # UISystem, UINavigator, Config
│   │   ├── Windows/       # Базовые классы окон
│   │   └── Attributes/    # UIWindowAttribute
│   ├── Settings/          # Система настроек
│   ├── Effects/           # Эффекты
│   ├── Cursor/            # Управление курсором
│   └── SceneFlow/         # Управление сценами
├── Editor/
│   ├── UI/                # UIWindowPrefabGenerator, редакторы
│   └── Initialization/    # Инспекторы систем
└── Documentation~/        # Документация
```

## Основные компоненты

### EventBus

```csharp
// Публикация события
EventBus.Publish(Evt.Combat.AttackPerformed, damage);

// Подписка в MonoEventBus
public class MyComponent : MonoEventBus
{
    protected override void InitEvents()
    {
        AddEvent(Evt.Combat.AttackPerformed, OnAttack);
    }
}
```

### System Initialization

```csharp
public class MySystem : InitializableSystemBase
{
    [Dependency] private OtherSystem dependency;
    
    public override string SystemId => "my_system";
    public override string DisplayName => "My System";
    
    public override async Task<bool> InitializeAsync()
    {
        ReportProgress(0.5f);
        // Логика инициализации
        return true;
    }
}
```

### UISystem

```csharp
// Открытие окна
UISystem.Open("pause_menu");

// Навигация назад
UISystem.Back();

// Кастомное окно
[UIWindow("my_window", WindowType.Normal, WindowLayer.Windows, 
    Level = 1, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
public class MyWindow : UIWindowBase
{
    protected override void OnOpened(object context) { }
    protected override void OnClosed() { }
}
```

### Генерация UI префабов

В Unity Editor: **ProtoSystem → UI → Generate Window → [тип окна]**

Доступные генераторы:
- MainMenu, PauseMenu, Settings
- GameHUD, GameOver, Statistics
- Credits, Loading

## Зависимости

- Unity 2021.3+
- Netcode for GameObjects 2.4.4
- TextMeshPro

## Интеграция с ИИ

Пакет включает инструкции для ИИ-ассистентов. Для автозагрузки в GitHub Copilot:

```bash
cp Packages/com.protosystem.core/Documentation~/AI_INSTRUCTIONS.md .github/copilot-instructions.md
```

## Лицензия

См. файл LICENSE.
