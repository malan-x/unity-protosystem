# ProtoSystem — AI Agent Instructions

> This file contains instructions for AI coding assistants (GitHub Copilot, Claude, etc.) working with ProtoSystem framework.

## Quick Reference

ProtoSystem is a Unity framework with:
- **EventBus**: Global event dispatcher (`EventBus.Publish/Subscribe`)
- **System Initialization**: Attribute-driven DI with `[Dependency]` and `[PostDependency]`
- **Base Classes**: `InitializableSystemBase`, `NetworkInitializableSystem`, `MonoEventBus`

## Architecture Rules

### 1. Creating New Systems

Always inherit from the appropriate base class:

```csharp
// Local system
public class MySystem : InitializableSystemBase
{
    public override string SystemId => "my_system";
    public override string DisplayName => "My System";
    
    [Dependency] private OtherSystem dependency;
    
    protected override void InitEvents()
    {
        AddEvent(Evt.Category.Event, OnEventHandler);
    }
    
    public override async Task<bool> InitializeAsync()
    {
        ReportProgress(0.5f);
        // Init logic
        ReportProgress(1.0f);
        return true;
    }
}

// Network system (for Netcode for GameObjects)
public class MyNetworkSystem : NetworkInitializableSystem
{
    // Same structure, but with network helpers
}
```

### 2. Event Handling

**DO:**
```csharp
// Use project-specific Evt class for event IDs
EventBus.Publish(Evt.Category.EventName, payload);
AddEvent(Evt.Category.EventName, handler);
```

**DON'T:**
```csharp
// Don't use magic numbers
EventBus.Publish(1001, payload);  // BAD
```

### 3. Dependency Injection

**DO:**
```csharp
[Dependency] private RequiredSystem required;      // Must exist
[PostDependency] private OptionalSystem optional;  // Resolved after main init
```

**DON'T:**
```csharp
// Never use FindObjectOfType in ProtoSystem projects
var system = FindObjectOfType<MySystem>();  // BAD
// Use SystemProvider instead:
var system = SystemInitializationManager.Instance.SystemProvider.GetSystem<MySystem>();
```

### 4. Project Event IDs

Events are defined in a separate file in the project (NOT in the package):

```csharp
// File: Assets/ProjectName/Scripts/Events/EventIds.ProjectName.cs
namespace ProjectName
{
    public static class Evt
    {
        public static class CategoryName
        {
            public const int EventName = 1001;
        }
    }
}
```

Usage requires `using ProjectName;` in files that use events.

### 5. Network Systems

For networked components, use helper methods:
- `PublishEventServerOnly(eventId, payload)` — Server-side only
- `PublishEventClientOnly(eventId, payload)` — Client-side only
- `PublishEventIfLocalPlayer(eventId, payload)` — Local player only

Always check authority:
```csharp
if (!IsSpawned) return;
if (IsServer) { /* server logic */ }
if (IsOwner) { /* owner logic */ }
```

### 6. Command Pattern

For complex operations (especially networked), use commands:

```csharp
public class MyCommand : ICommand
{
    public void Execute() { /* do work */ }
    public void Undo() { /* rollback */ }
}

// Execute via executor for proper network sync
commandExecutor.Execute(new MyCommand(params));
```

### 7. Facade Pattern

Hide complexity behind facades:

```csharp
public class SystemFacade : InitializableSystemBase
{
    [Dependency] private StateManager state;
    [Dependency] private CommandExecutor executor;
    
    // Clean public API
    public void DoAction(params) => executor.Execute(new ActionCommand(params));
}
```

## File Organization

```
Assets/ProjectName/Scripts/
├── Events/
│   └── EventIds.ProjectName.cs    # Event ID constants
├── Systems/
│   └── MySystem/
│       ├── MySystem.cs            # Main system
│       ├── Commands/              # System commands
│       ├── Visual/                # Visual components
│       └── UI/                    # UI integration
```

## Common Patterns

### Initialization Flow
1. `SystemInitializationManager` collects all systems
2. Analyzes dependency graph (topological sort)
3. Calls `InitializeAsync()` in order
4. Resolves `[PostDependency]` after main init
5. Systems are ready

### Event Flow
1. System A publishes: `EventBus.Publish(Evt.X.Y, data)`
2. EventBus dispatches to all subscribers
3. System B receives in handler registered via `AddEvent()`

### Auto-subscription Lifecycle
- `MonoEventBus`: OnEnable/OnDisable
- `NetworkInitializableSystem`: OnNetworkSpawn/OnNetworkDespawn

## Debugging Tips

1. Enable `verboseLogging` on systems for detailed logs
2. Use `EventBus.GetEventPath(id)` to debug event routing
3. Check dependency graph in SystemInitializationManager Inspector
4. Use "Анализировать зависимости" button to rebuild graph

## Avoid These Anti-patterns

1. **Circular dependencies**: Use events or `[PostDependency]` to break cycles
2. **Direct system references**: Use `[Dependency]` attribute instead
3. **Magic event IDs**: Always use `Evt.Category.Name` constants
4. **Synchronous heavy init**: Use `async/await` with `ReportProgress()`
5. **FindObjectOfType**: Use `SystemProvider.GetSystem<T>()`

## Integration Checklist

When adding ProtoSystem to a new project:

1. [ ] Create `Assets/ProjectName/Scripts/Events/EventIds.ProjectName.cs`
2. [ ] Add `SystemInitializationManager` to main scene
3. [ ] Create systems inheriting from base classes
4. [ ] Register systems in SystemInitializationManager
5. [ ] Run "Анализировать зависимости" to verify
6. [ ] Add `using ProjectName;` where events are used
