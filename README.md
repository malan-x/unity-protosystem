# ProtoSystem Core

Universal Unity framework for system initialization and event-driven architecture.

## Features

- **EventBus**: Global event dispatcher with grouped event IDs
- **System Initialization**: Attribute-driven dependency injection and initialization ordering
- **Network Support**: Built-in support for Netcode for GameObjects
- **AI-Ready**: Includes instructions for GitHub Copilot and other AI assistants

## Quick Start

See [QUICKSTART.md](QUICKSTART.md) for fast integration guide.

## Documentation

- [Developer Guide](Documentation~/ProtoSystem-Guide.md) — Full documentation
- [AI Instructions](Documentation~/copilot-instructions.md) — For AI coding assistants
- [Changelog](CHANGELOG.md) — Version history

### AI Assistant Integration

ProtoSystem includes instructions for AI assistants. To enable automatic loading:

```bash
# Copy to project root for GitHub Copilot
cp Packages/com.protosystem.core/Documentation~/copilot-instructions.md .github/copilot-instructions.md
```

Or add to `.vscode/settings.json`:
```json
{
    "github.copilot.chat.codeGeneration.instructions": [
        { "file": "Packages/com.protosystem.core/Documentation~/copilot-instructions.md" }
    ]
}
```

## Installation

Add this package to your Unity project via Package Manager:

```
https://github.com/wildforest/ProtoSystem.git
```

## Usage

### EventBus

```csharp
using ProtoSystem;

// Publish an event
EventBus.Publish(EventBus.Group.EventId, payload);

// Subscribe in a MonoBehaviour
public class MyComponent : MonoEventBus
{
    internal override void InitEvents()
    {
        AddEvent(EventBus.Group.EventId, OnEvent);
    }

    private void OnEvent(object payload)
    {
        // Handle event
    }
}
```

### System Initialization

```csharp
using ProtoSystem;

public class MySystem : InitializableSystemBase
{
    [Dependency(required: true, description: "Required system")]
    private OtherSystem otherSystem;

    public override string SystemId => "MySystem";
    public override string DisplayName => "My System";

    public override async Task<bool> InitializeAsync()
    {
        // Initialize system
        return true;
    }

    internal override void InitEvents()
    {
        // Subscribe to events
    }
}
```

## Extending EventBus

Create a separate EventIds class in your project for event constants:

```csharp
// Assets/YourProject/Scripts/Events/EventIds.YourProject.cs
namespace YourProject
{
    public static class Evt
    {
        public static class MyGroup
        {
            public const int MyEvent = 1000;
        }
    }
}
```

Then use with `using YourProject;`:
```csharp
EventBus.Publish(Evt.MyGroup.MyEvent, payload);
```

## Dependencies

- Unity 2021.3+
- Netcode for GameObjects 2.4.4

## License

See LICENSE file for details.