# ⚡ Quick Fix - Input System Error & Missing UIWindowGraph

## Проблема 1: InvalidOperationException

```
InvalidOperationException: You are trying to read Input using the UnityEngine.Input class,
but you have switched active Input handling to Input System package in Player Settings.
```

### Быстрое исправление (1 минута):

**Bootstrap сцена → EventSystem → Inspector:**

1. Удалить компонент `Standalone Input Module`
2. Add Component → `Input System UI Input Module`
3. Save Scene

### Через визард (2 минуты):

1. Delete файл `Assets/{YourProject}/Scenes/Bootstrap.unity`
2. `Tools → ProtoSystem → Project Setup Wizard`
3. Uncheck "Create Bootstrap Scene"
4. Reset Progress
5. Execute All Pending

---

## Проблема 2: UIWindowGraph не найден

**UISystem Inspector показывает:**
```
Graph Override (optional): None
```

### Быстрое исправление:

**Вариант 1 - Через Asset Menu:**
```
Assets → Create → ProtoSystem → UI Window Graph
Сохранить в: Assets/Resources/ProtoSystem/UIWindowGraph.asset
```

**Вариант 2 - Через визард:**
```
Tools → ProtoSystem → Project Setup Wizard
Execute → "Create UIWindowGraph"
```

### Проверка:

После исправления в UISystem должно быть:
```
Configuration
└── Config: UISystemConfig (UI System Config)

Graph Override (optional)
└── None (UI Window Graph)  ← это нормально, граф загружается из Resources
```

---

## Почему это произошло?

**Input System Error:**
- Визард v1.6.6 и ранее создавал `StandaloneInputModule`
- В проектах с Input System это вызывало ошибку
- v1.6.7 автоматически использует `InputSystemUIInputModule`

**UIWindowGraph Missing:**
- Визард не создавал UIWindowGraph ScriptableObject
- UISystem требует этот asset для работы
- v1.6.7 автоматически создаёт граф

---

## Проверка Input System

**Узнать какой Input используется:**

1. Edit → Project Settings → Player
2. Active Input Handling:
   - `Input Manager (Old)` → StandaloneInputModule ✅
   - `Input System Package (New)` → InputSystemUIInputModule ✅
   - `Both` → InputSystemUIInputModule ✅

**Если используете Input System:**
- EventSystem должен иметь `Input System UI Input Module`
- Не должно быть `Standalone Input Module`

---

## Для новых проектов

✅ **Обновитесь до v1.6.7** - всё будет работать автоматически!

```
Package Manager → ProtoSystem Core → Update to 1.6.7+
```

---

**v1.6.7+** - Input System support & UIWindowGraph creation included! 🚀
