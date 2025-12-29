# SettingsSystem — Сценарии тестирования

## Предварительные условия

1. На сцене есть GameObject с `SettingsSystem`
2. Назначен `SettingsConfig` (или используются дефолты)
3. SystemInitializationManager инициализирует систему

---

## 1. Базовые операции

### 1.1 Загрузка настроек при старте

**Шаги:**
1. Запустить игру
2. Проверить консоль

**Ожидаемый результат:**
```
[SettingsSystem] Initializing Settings System...
[IniPersistence] Settings file not found: .../settings.ini (первый запуск)
[SettingsSystem] Settings System initialized successfully
```

### 1.2 Изменение и сохранение

**Шаги:**
1. Изменить настройку: `SettingsSystem.Instance.Audio.MasterVolume.Value = 0.5f;`
2. Проверить `HasUnsavedChanges()` → `true`
3. Вызвать `Save()`
4. Проверить файл `settings.ini`

**Ожидаемый результат:**
- Файл создан в `Application.persistentDataPath`
- Содержит `MasterVolume=0.5`

### 1.3 Откат изменений

**Шаги:**
1. Изменить: `Audio.MasterVolume.Value = 0.3f`
2. НЕ сохранять
3. Вызвать `RevertAll()`

**Ожидаемый результат:**
- `Audio.MasterVolume.Value` вернулся к предыдущему сохранённому значению
- `HasUnsavedChanges()` → `false`

### 1.4 Сброс к дефолтам

**Шаги:**
1. Изменить несколько настроек
2. Сохранить
3. Вызвать `ResetAllToDefaults()`

**Ожидаемый результат:**
- Все значения = дефолтам из `SettingsConfig`
- `HasUnsavedChanges()` → `true` (не сохранено)

---

## 2. Видео настройки

### 2.1 Изменение разрешения

**Шаги:**
1. Получить список: `VideoSettings.GetAvailableResolutions()`
2. Установить: `Video.Resolution.Value = "1280x720"`
3. Вызвать `Video.Apply()` или `ApplyAll()`

**Ожидаемый результат:**
- `Screen.currentResolution` изменилось на 1280x720
- Лог: `[VideoSettings] Resolution set to 1280x720 ...`

### 2.2 Переключение VSync

**Шаги:**
1. `Video.VSync.Value = false`
2. `ApplyAll()`

**Ожидаемый результат:**
- `QualitySettings.vSyncCount` = 0
- При `true` → = 1

### 2.3 Изменение качества

**Шаги:**
1. `Video.Quality.Value = 0` (минимальное)
2. `ApplyAll()`

**Ожидаемый результат:**
- `QualitySettings.GetQualityLevel()` = 0
- Визуально качество изменилось

### 2.4 Ограничение FPS

**Шаги:**
1. `Video.TargetFrameRate.Value = 30`
2. `ApplyAll()`

**Ожидаемый результат:**
- `Application.targetFrameRate` = 30
- FPS не превышает ~30

---

## 3. Аудио настройки

### 3.1 Изменение громкости

**Шаги:**
1. `Audio.MasterVolume.Value = 0.25f`
2. `ApplyAll()`

**Ожидаемый результат:**
- `AudioListener.volume` = 0.25

### 3.2 Mute

**Шаги:**
1. `Audio.Mute.Value = true`
2. `ApplyAll()`

**Ожидаемый результат:**
- `AudioListener.volume` = 0 (звук выключен)

---

## 4. События EventBus

### 4.1 Подписка на изменение

**Шаги:**
1. Подписаться на `EventBus.Settings.Audio.MasterChanged`
2. Изменить `Audio.MasterVolume.Value`

**Ожидаемый результат:**
- Callback вызван
- `SettingChangedData<float>.Value` = новое значение
- `SettingChangedData<float>.PreviousValue` = старое

### 4.2 Событие Modified

**Шаги:**
1. Подписаться на `EventBus.Settings.Modified`
2. Изменить любую настройку

**Ожидаемый результат:**
- Callback вызван для каждого изменения

### 4.3 События Save/Load

**Шаги:**
1. Подписаться на `EventBus.Settings.Saved`
2. Вызвать `Save()`

**Ожидаемый результат:**
- Callback вызван после успешного сохранения

---

## 5. Persistence

### 5.1 INI файл (Desktop)

**Шаги:**
1. Изменить настройки
2. Сохранить
3. Открыть `settings.ini` в текстовом редакторе
4. Вручную изменить значение
5. Перезапустить игру

**Ожидаемый результат:**
- Изменённое значение загрузилось

### 5.2 PlayerPrefs (WebGL)

**Шаги:**
1. В `SettingsConfig` установить `persistenceMode = PlayerPrefs`
2. Изменить и сохранить настройки
3. Проверить `PlayerPrefs.GetString("ProtoSettings_Audio_MasterVolume")`

**Ожидаемый результат:**
- Значение сохранено в PlayerPrefs

### 5.3 Авто-выбор платформы

**Шаги:**
1. `persistenceMode = Auto`
2. Запустить на Desktop → проверить создание INI
3. Запустить на WebGL → проверить PlayerPrefs

**Ожидаемый результат:**
- Desktop: INI файл
- WebGL: PlayerPrefs

---

## 6. Кастомные секции

### 6.1 Через SettingsConfig

**Шаги:**
1. В Inspector добавить Custom Section:
   - `sectionName` = "MyGame"
   - `comment` = "Custom settings"
   - Добавить: `key="Difficulty"`, `type=Int`, `default="1"`
2. Запустить игру
3. Получить: `GetSection("MyGame").GetSetting("Difficulty")`

**Ожидаемый результат:**
- Секция существует
- Значение = 1

### 6.2 Программная регистрация

**Шаги:**
1. Создать класс наследник `CustomSettingsSection`
2. Вызвать `RegisterSection(new MySection())`
3. Изменить и сохранить

**Ожидаемый результат:**
- В INI появилась секция `[MySection]`
- Значения сохраняются/загружаются

---

## 7. Миграция

### 7.1 Миграция старой версии

**Подготовка:**
1. Создать `settings.ini` с `; Version: 0`
2. Зарегистрировать миграцию для версии 1

**Шаги:**
1. Запустить игру

**Ожидаемый результат:**
- Лог: `[SettingsMigrator] Migrating settings from v0 to v1`
- Миграция выполнена

---

## 8. Edge Cases

### 8.1 Отсутствующий файл

**Шаги:**
1. Удалить `settings.ini`
2. Запустить игру

**Ожидаемый результат:**
- Используются дефолты
- Файл создаётся при первом Save()

### 8.2 Повреждённый файл

**Шаги:**
1. Записать мусор в `settings.ini`
2. Запустить игру

**Ожидаемый результат:**
- Warning в консоли
- Используются дефолты для нераспознанных значений

### 8.3 Неизвестные ключи в файле

**Шаги:**
1. Добавить в INI: `[Audio] UnknownKey=123`
2. Запустить игру

**Ожидаемый результат:**
- Игра запускается без ошибок
- Неизвестный ключ игнорируется

### 8.4 Float precision

**Шаги:**
1. `Audio.MasterVolume.Value = 0.50001f`

**Ожидаемый результат:**
- `IsModified` = false (в пределах погрешности)

---

## 9. Производительность

### 9.1 Частое изменение

**Шаги:**
1. В Update(): `Audio.MasterVolume.Value = Random.value`
2. Проверить профайлер

**Ожидаемый результат:**
- Нет утечек памяти
- События публикуются без задержек

### 9.2 Большой файл настроек

**Шаги:**
1. Добавить 100+ кастомных настроек
2. Измерить время Load()/Save()

**Ожидаемый результат:**
- Load < 50ms
- Save < 100ms

---

## Чек-лист ручного тестирования

- [ ] Первый запуск — дефолты загружены
- [ ] Изменение настройки — событие опубликовано
- [ ] Save() — файл создан/обновлён
- [ ] Load() — значения восстановлены
- [ ] Revert() — откат работает
- [ ] ResetToDefaults() — сброс работает
- [ ] Video.Apply() — разрешение/качество меняется
- [ ] Audio.Apply() — громкость меняется
- [ ] Кастомная секция — сохраняется/загружается
- [ ] INI комментарии — генерируются корректно
- [ ] События EventBus — все срабатывают
- [ ] PlayerPrefs fallback — работает на WebGL

---

## Автоматические тесты

Расположение: `Packages/com.protosystem.core/Tests/Editor/SettingsSystemTests.cs`

Запуск: Window → General → Test Runner → Edit Mode → Run All

### Покрытие

| Категория | Тестов |
|-----------|--------|
| SettingValue | 12 |
| AudioSettings | 5 |
| VideoSettings | 3 |
| IniPersistence | 6 |
| PlayerPrefsPersistence | 1 |
| SettingsMigrator | 4 |
| DynamicSettingsSection | 2 |
| Integration | 1 |
| **Всего** | **34** |
