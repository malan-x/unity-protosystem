# ProtoSystem v1.6.1 - Исправления GUID конфликтов

## Дата: 2026-01-09

## Исправленные проблемы

### 1. Конфликт GUID: EffectsManager.cs.meta vs EffectsManagerSystem.cs.meta

**Проблема:**
```
GUID [e81748ec822b2844697a7459ac9aa87b] for asset 'EffectsManagerSystem.cs.meta' 
conflicts with: 'EffectsManager.cs.meta'
```

**Причина:**
- Файл был переименован с `EffectsManager.cs` на `EffectsManagerSystem.cs`
- В некоторых проектах остался старый .meta файл с тем же GUID

**Решение:**
- Регенерирован новый GUID для `EffectsManagerSystem.cs.meta`
- Старый GUID: `e81748ec822b2844697a7459ac9aa87b`
- Новый GUID: `a3f9b2c8d5e6f7a8b9c0d1e2f3a4b5c6`

### 2. Невалидный .meta файл: Tests/Editor.meta

**Проблема:**
```
The .meta file Tests/Editor.meta does not have a valid GUID
```

**Причина:**
- Неполный формат .meta файла (отсутствовали обязательные поля)

**Решение:**
- Добавлен полный формат с правильной структурой
- Новый GUID: `e5f67890a1b2c3d4e5f678901234abcd`

## Новые возможности в v1.6.1

### ProjectSetupWizard

Добавлен визард для первичной настройки проекта:
- Автоматический запуск при первом старте
- Выбор типа проекта (Single/Multiplayer)
- Генерация структуры папок
- Создание Assembly Definition
- Генерация EventCategories
- Создание UI спрайтов и префабов
- Настройка Bootstrap сцены

**Использование:**
- Автоматически: появится диалог при первом запуске
- Вручную: `Tools → ProtoSystem → Project Setup Wizard`

**Компоненты:**
- `ProjectSetupWizard.cs` - главное окно
- `ProjectConfig.cs` - ScriptableObject для namespace
- `ProjectSetupDetector.cs` - автозапуск

## Инструкции по обновлению

### Для проектов с конфликтом GUID:

1. **Обновите пакет до v1.6.1**
2. **Если проблема осталась:**
   - Закройте Unity
   - Удалите `Library/` папку
   - Откройте проект заново (Unity пересоздаст кэш)

### Для проектов без проблем:

Просто обновите пакет через Package Manager или Git.

## Файлы с изменениями

- `package.json` - версия 1.6.0 → 1.6.1
- `Runtime/Effects/EffectsManagerSystem.cs.meta` - новый GUID
- `Tests/Editor.meta` - исправлен формат
- `Editor/Initialization/ProjectSetupWizard.cs` - новый файл
- `Editor/Initialization/ProjectSetupDetector.cs` - новый файл
- `Runtime/Initialization/ProjectConfig.cs` - новый файл
- `Editor/Initialization/PROJECT_SETUP_WIZARD.md` - документация

## Совместимость

- Unity 2021.3+
- Netcode for GameObjects 2.4.4+
- Обратная совместимость с v1.6.0 сохранена

## Благодарности

Спасибо за репорт проблемы с GUID конфликтом в других проектах!
