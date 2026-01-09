# Краткая справка по ProjectSetupWizard v1.6.1

## 🎯 Новые возможности

### Автоматическая настройка камеры и освещения

Визард теперь создаёт Bootstrap сцену с правильными настройками в зависимости от типа проекта.

## 🎮 Настройки в визарде

### Project Type
- **Single** - одиночная игра
- **Multiplayer** - мультиплеер + Netcode

### Camera Type  
- **3D** - перспективная камера (FOV 60°, позиция 0,1,-10)
- **2D** - ортографическая камера (Size 5, позиция 0,0,-10)

### Render Pipeline
- **Standard** - встроенный рендер
  - Камера: Skybox clear, background (0.49, 0.67, 0.85)
  - Свет: Intensity 1.0
- **URP** - Universal Pipeline
  - Камера: SolidColor clear, background (0.02, 0.02, 0.02)
  - Свет: Intensity 1.0
- **HDRP** - High Definition
  - Камера: SolidColor clear, background (0, 0, 0)
  - Свет: Intensity 130000 (физические единицы)
- **Auto** ✅ - автоопределение

## 📋 Что создаётся в Bootstrap сцене

### Для всех проектов:
- **Main Camera** с правильными настройками
- **SystemInitializationManager**
- **EventSystem** для UI

### Для 3D проектов:
- **Directional Light** с правильной intensity
- Поворот света: (50°, -30°, 0°)

### Для 2D проектов:
- Камера без света (для 2D Lights)

## ⚙️ Автоопределение Render Pipeline

```csharp
GraphicsSettings.currentRenderPipeline:
- null → Standard
- "Universal..." → URP  
- "HDRenderPipeline..." → HDRP
```

Включается чекбоксом **Auto** в настройках.

## 🔧 Примеры использования

### Новый 2D проект с URP:
1. Project Type: **Single**
2. Camera Type: **2D**
3. Render Pipeline: **URP** (Auto)
4. Execute → Bootstrap сцена с ортографической камерой

### 3D мультиплеер со Standard:
1. Project Type: **Multiplayer**
2. Camera Type: **3D**
3. Render Pipeline: **Standard** (Auto)
4. Execute → Bootstrap + камера + свет + NetworkManager

### HDRP проект:
1. Project Type: **Single**
2. Camera Type: **3D**
3. Render Pipeline: **HDRP** (Auto)
4. Execute → Bootstrap с физическим освещением (130k intensity)

## 🐛 Исправленные ошибки

### Assembly Definition
**Было:**
```
Required property 'name' not set (*.asmdef)
```

**Исправлено:**
- Ручная генерация JSON
- Гарантировано поле "name"

### Пример сгенерированного .asmdef:
```json
{
    "name": "MyGame",
    "rootNamespace": "MyGame",
    "references": ["ProtoSystem", "Unity.TextMeshPro"],
    ...
}
```

## 📝 Сохранение настроек

Все настройки сохраняются в EditorPrefs:
- ProjectName
- Namespace  
- RootFolder
- ProjectType
- **CameraType** (новое)
- **AutoDetectPipeline** (новое)

## ⚠️ Важно

### HDRP требует:
- Volume Profile для глобальных настроек
- Physical Light Units (intensity × 1000)
- Правильные Material шейдеры

### URP для 2D:
- Может требовать 2D Light System
- Настройка через URP Asset

### Рекомендации:
1. Используйте **Auto** для Render Pipeline
2. Выбирайте Camera Type соответственно проекту
3. Bootstrap сцена - стартовая точка, можно изменять

## 🔗 Связанные файлы

- `ProjectSetupWizard.cs` - главный код
- `PROJECT_SETUP_WIZARD.md` - полная документация
- `RELEASE_NOTES_v1.6.1.md` - changelog
