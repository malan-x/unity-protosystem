# ProtoSystem Core — Быстрый старт

## Интеграция AI-инструкций

ProtoSystem включает инструкции для AI-ассистентов (GitHub Copilot, Claude и др.) в папке `Documentation~/`.

### Автоматическая интеграция с GitHub Copilot

Для автоматической загрузки инструкций при работе с проектом, **скопируйте файл инструкций в корень проекта**:

```bash
# Из корня Unity-проекта
cp Packages/com.protosystem.core/Documentation~/copilot-instructions.md .github/copilot-instructions.md
```

Или создайте `.github/copilot-instructions.md` и добавьте ссылку:

```markdown
# Project AI Instructions

This project uses ProtoSystem framework.
See: Packages/com.protosystem.core/Documentation~/copilot-instructions.md

<!-- Include the content from the package documentation -->
```

### Структура документации

```
Documentation~/
├── ProtoSystem-Guide.md      — Полное руководство разработчика
└── copilot-instructions.md   — Инструкции для AI-ассистентов
```

### Для VS Code / Cursor

Добавьте в настройки проекта (`.vscode/settings.json`):

```json
{
    "github.copilot.chat.codeGeneration.instructions": [
        {
            "file": "Packages/com.protosystem.core/Documentation~/copilot-instructions.md"
        }
    ]
}
```

### Для других AI-инструментов

При начале работы с проектом попросите AI прочитать:
- `Packages/com.protosystem.core/Documentation~/copilot-instructions.md`
- `Packages/com.protosystem.core/Documentation~/ProtoSystem-Guide.md`

## Быстрый старт

### 1. Создайте файл событий проекта

```csharp
// Assets/YourProject/Scripts/Events/EventIds.YourProject.cs
namespace YourProject
{
    public static class Evt
    {
        public static class Gameplay
        {
            public const int PlayerSpawned = 1001;
            public const int EnemyKilled = 1002;
        }
    }
}
```

### 2. Создайте систему

```csharp
using ProtoSystem;
using YourProject;

public class GameplaySystem : InitializableSystemBase
{
    public override string SystemId => "gameplay_system";
    
    protected override void InitEvents()
    {
        AddEvent(Evt.Gameplay.PlayerSpawned, OnPlayerSpawned);
    }
    
    public override async Task<bool> InitializeAsync()
    {
        ReportProgress(1.0f);
        return true;
    }
    
    private void OnPlayerSpawned(object payload) { }
}
```

### 3. Настройте сцену

1. Создайте GameObject с `SystemInitializationManager`
2. Добавьте системы в список
3. Нажмите "Анализировать зависимости"

## Полезные ссылки

- [Руководство разработчика](Documentation~/ProtoSystem-Guide.md)
- [AI инструкции](Documentation~/copilot-instructions.md)
- [Changelog](CHANGELOG.md)
