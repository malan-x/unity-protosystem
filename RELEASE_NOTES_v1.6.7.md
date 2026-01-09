# ProtoSystem v1.6.7 - Input System & UIWindowGraph

## 📅 Дата: 2026-01-09

## 🔧 Критические исправления

### 1. EventSystem с неправильным Input Module

**Проблема:**
```
InvalidOperationException: You are trying to read Input using the UnityEngine.Input class, 
but you have switched active Input handling to Input System package in Player Settings.
```

**Причина:**
- Визард создавал EventSystem с `StandaloneInputModule`
- StandaloneInputModule использует старый Input Manager (Input.mousePosition)
- В проектах с новым Input System это вызывало ошибку

**Решение в v1.6.7:**
- Автоопределение наличия Input System пакета
- Использование `InputSystemUIInputModule` если пакет установлен
- Fallback на `StandaloneInputModule` для старых проектов

**Код:**
```csharp
// Проверка наличия Input System
bool hasInputSystem = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem") != null;

if (hasInputSystem)
{
    // Используем InputSystemUIInputModule
    var inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
    eventSystem.AddComponent(inputSystemModule);
}
else
{
    // Fallback на старый модуль
    eventSystem.AddComponent<StandaloneInputModule>();
}
```

### 2. UIWindowGraph не создавался

**Проблема:**
- UISystem требует UIWindowGraph ScriptableObject
- Визард не создавал этот asset
- В Inspector UISystem показывал ошибку "Graph Override (optional): None"
- Отсутствовала кнопка создания графа

**Причина:**
- Не было задачи создания UIWindowGraph в визарде

**Решение в v1.6.7:**
- Добавлена задача "Create UIWindowGraph"
- Создаётся asset в стандартном месте: `Assets/Resources/ProtoSystem/UIWindowGraph.asset`
- UISystem автоматически находит граф через Resources.Load

**Создаваемая структура:**
```
Assets/
└── Resources/
    └── ProtoSystem/
        └── UIWindowGraph.asset  ← новый файл
```

## 📋 Что изменилось

### Новая задача в визарде:

**7. Create UIWindowGraph**
- Описание: "Create UIWindowGraph ScriptableObject asset"
- Создаёт граф в `Assets/Resources/ProtoSystem/`
- Проверяет существование перед созданием
- Пустой граф, готовый к наполнению окнами

### Обновлённая задача:

**8. Create Bootstrap Scene**
- Теперь создаёт EventSystem с правильным Input Module
- Автоопределение Input System пакета
- Совместимость со старыми и новыми проектами

## ✅ Результат

**До исправления:**
```
❌ InvalidOperationException при запуске сцены
❌ EventSystem с StandaloneInputModule
❌ UIWindowGraph не создан
❌ UISystem показывает ошибку
```

**После исправления:**
```
✅ EventSystem работает с Input System
✅ Нет ошибок при запуске
✅ UIWindowGraph создан автоматически
✅ UISystem готов к использованию
```

## 🎯 Для пользователей

### Если создали проект на v1.6.6 или ранее:

**Проблема 1 - InvalidOperationException:**

**Ручное исправление:**
1. Найти GameObject "EventSystem" в Bootstrap сцене
2. Удалить компонент `Standalone Input Module`
3. Добавить компонент `Input System UI Input Module`
4. Сохранить сцену

**Через визард:**
1. `Tools → ProtoSystem → Project Setup Wizard`
2. Uncheck задачу "Create Bootstrap Scene"
3. Delete файл `Assets/{YourProject}/Scenes/Bootstrap.unity`
4. Reset Progress
5. Execute All Pending

**Проблема 2 - UIWindowGraph отсутствует:**

**Ручное создание:**
1. Create → ProtoSystem → UI Window Graph
2. Сохранить в `Assets/Resources/ProtoSystem/UIWindowGraph.asset`

**Через визард:**
1. `Tools → ProtoSystem → Project Setup Wizard`
2. Execute → "Create UIWindowGraph"

### Для новых проектов:

✅ Просто используйте визард - всё будет правильно! 

## 🔍 Технические детали

### InputSystemUIInputModule vs StandaloneInputModule:

| Модуль | Input System | Совместимость |
|--------|--------------|---------------|
| StandaloneInputModule | ❌ Старый (Input Manager) | Unity 2019-2023 |
| InputSystemUIInputModule | ✅ Новый (Input System) | Unity 2019+ (с пакетом) |

### UIWindowGraph создание:

**Путь:** `Assets/Resources/ProtoSystem/UIWindowGraph.asset`

**Почему Resources?**
- UISystem загружает граф через `Resources.Load<UIWindowGraph>("ProtoSystem/UIWindowGraph")`
- Это стандартный путь ProtoSystem
- Граф доступен во всех сценах без ссылок

**Структура графа:**
```csharp
UIWindowGraph
├── startWindowId: ""  // пусто по умолчанию
├── windows: []        // пустой список
└── transitions: []    // пустой список
```

## 📦 Обновлённые файлы

**Код:**
- `ProjectSetupWizard.cs` - CreateBootstrapScene() с Input System detection
- `ProjectSetupWizard.cs` - новый метод CreateUIWindowGraph()
- `TaskType` enum - добавлен CreateUIWindowGraph

**Документация:**
- `RELEASE_NOTES_v1.6.7.md` - этот файл
- `PROJECT_SETUP_WIZARD.md` - обновлена информация о задачах

## 🔄 История версий

**v1.6.7** - Input System support, UIWindowGraph creation ✅
**v1.6.6** - EventBus integration with built-in system
**v1.6.5** - (bugfix)
**v1.6.4** - EventBus generation attempt
**v1.6.3** - GUID-based assembly references
**v1.6.1** - ProjectSetupWizard initial release

## 💡 Дополнительная информация

### Как использовать UIWindowGraph:

**1. Создать префабы окон:**
```
Assets/{YourProject}/Prefabs/UI/Windows/
├── MainMenuWindow.prefab
├── SettingsWindow.prefab
└── GameHUDWindow.prefab
```

**2. Добавить в граф:**
- Выбрать UIWindowGraph asset
- Нажать "Scan & Add Prefabs"
- Настроить transitions между окнами
- Нажать "Rebuild Graph"

**3. Использовать в коде:**
```csharp
UISystem.Instance.OpenWindow("MainMenuWindow");
UISystem.Instance.Navigate("settings"); // через trigger
```

### Проверка Input System:

```csharp
// В коде можно проверить активный Input Handling:
#if ENABLE_INPUT_SYSTEM
    Debug.Log("Using new Input System");
#else
    Debug.Log("Using old Input Manager");
#endif
```

---

**Обновитесь до v1.6.7 для полной совместимости!** 🚀
