# LiveOps System

Система связи с игроками без обновления билда.

---

## Обзор

**Пакет** (`com.protosystem.core`) — данные, логика, EventBus, HTTP:
- `LiveOpsSystem` — главная система: health check, FetchAsync, регистрация панели, EventBus.Publish
- `DefaultHttpLiveOpsProvider` — встроенный HTTP-провайдер (подставляется автоматически)
- `ILiveOpsProvider` — интерфейс для кастомного бэкенда
- `LiveOpsConfig` — ScriptableObject с настройками сервера, feature flags, триггерами обновления
- `CommunityPanelWindow` — UI-компонент панели (MonoEventBus, авто-регистрация)
- `LiveOpsStubConfig` — ScriptableObject с mock-данными для тестирования без сервера

**Проект** — только конфиг:
- `LiveOpsConfig.asset` — заполнить `serverUrl`, `projectId`, `defaultLanguage`
- Опционально: `config.SetProvider(new MyProvider(...))` для кастомного бэкенда

---

## Минимальная интеграция

```csharp
// Только заполнить LiveOpsConfig.asset в Inspector:
//   serverUrl  = "https://api.mygame.com/v1"
//   projectId  = "my-game"
//   defaultLanguage = "ru"

// До InitializeAsync() — опционально:
liveOps.SetPlayerId(SteamFriends.GetPersonaName());
liveOps.SetPlayerContext(new LiveOpsPlayerContext(
    PlayerPrefs.GetInt("LaunchCount"),
    Mathf.FloorToInt(totalPlaytimeSec / 60f)
));
// DefaultHttpLiveOpsProvider создаётся автоматически если serverUrl задан
```

---

## Поток запуска

```
InitializeAsync()
  ├── Если _provider == null && serverUrl != "" → создать DefaultHttpLiveOpsProvider
  ├── Health check (HEAD /config, таймаут = healthCheckTimeoutSeconds)
  │     ├── OK  → FetchAsync() → EventBus.Publish(DataUpdated) × каждый тип
  │     └── Fail → _serverAvailable = false
  │
CommunityPanelWindow.Start()
  ├── stubConfig задан → ApplyStubConfig(), выйти
  └── GetSystem<LiveOpsSystem>().RegisterPanel(this)
        ├── !serverAvailable → gameObject.SetActive(false)
        ├── hasData          → PushAllDataToEventBus()
        └── fetchOnPanelOpen → FetchAsync()
```

---

## Community Panel

Виджет главного меню. Структура:

```
MainMenuWindow
  └── CommunityPanelWindow (MonoEventBus)
        ├── CardsRoot   — карусель (опросы / новости / devlog)
        ├── MessageRoot — поле сообщения игрока
        ├── WishlistRoot — прогресс-бар вишлиста
        └── RatingRoot  — 10 звёзд + средняя оценка
```

**Генерация:** ProtoSystem → UI → Tools → UI Generator → **Community Panel**

`LiveOpsSystem` берётся автоматически через `SystemInitializationManager.Instance?.GetSystem<LiveOpsSystem>()`. Поле в Inspector не нужно.

---

## Тестирование без сервера (Stub)

```
ProtoSystem → UI → Prefabs → LiveOps → Generate Stub Configs
```

Создаёт 4 ассета в `<OutputPath>/LiveOpsStubs/`:

| Ассет | Содержимое |
|-------|-----------|
| `LiveOpsStub_Poll` | Опрос + поле сообщения |
| `LiveOpsStub_Announcement` | Новость + вишлист + рейтинг |
| `LiveOpsStub_DevLog` | Dev Log + вишлист + рейтинг |
| `LiveOpsStub_AllWidgets` | Все 3 карточки + все виджеты |

Назначить `.asset` в поле **Stub Config** компонента `CommunityPanelWindow`.  
В PlayMode можно менять ассет на лету — панель обновится на следующем кадре.  
Из кода: `panel.ApplyStubConfig(stub)`.

---

## Триггеры обновления данных

Настраиваются в `LiveOpsConfig`:

| Поле | По умолчанию | Описание |
|------|-------------|----------|
| `fetchIntervalSeconds` | 300 | Периодический опрос. 0 — отключить |
| `fetchOnPanelOpen` | true | При каждом открытии панели |
| `fetchOnMainMenuOpen` | true | При открытии окна `mainMenuWindowName` |
| `mainMenuWindowName` | "MainMenuWindow" | Имя окна для триггера |
| `healthCheckTimeoutSeconds` | 5 | Таймаут ping при старте |
| `requestTimeoutSeconds` | 10 | Таймаут всех остальных запросов |

Принудительный запрос из кода: `liveOpsSystem.TriggerFetch()`

---

## EventBus

Все данные через `Evt.LiveOps.DataUpdated`:

```csharp
AddEvent(Evt.LiveOps.DataUpdated, OnLiveOpsDataUpdated);

private void OnLiveOpsDataUpdated(object payload)
{
    if (payload is not LiveOpsDataPayload data) return;
    switch (data.Type)
    {
        case LiveOpsDataType.Polls        when data.Data is List<LiveOpsPoll> polls:   ...
        case LiveOpsDataType.Announcements when data.Data is List<LiveOpsAnnouncement>: ...
        case LiveOpsDataType.DevLog       when data.Data is LiveOpsDevLog devLog:      ...
        case LiveOpsDataType.Rating       when data.Data is LiveOpsRatingData rating:  ...
        case LiveOpsDataType.PanelConfig  when data.Data is LiveOpsPanelConfig cfg:    ...
    }
}
```

**Типы:** `panel_config` · `polls` · `announcements` · `devlog` · `rating` · `messages`

---

## API сервера

Все запросы с заголовками `X-Project-ID` и `X-Steam-ID` (если задан playerId).

| Метод | Путь | Описание |
|-------|------|----------|
| HEAD | `/config` | Health check |
| GET  | `/config` | `LiveOpsPanelConfig` |
| GET  | `/polls` | `LiveOpsPoll[]` |
| POST | `/polls/{id}/vote` | `{ optionIds, playerId }` |
| GET  | `/announcements` | `LiveOpsAnnouncement[]` |
| GET  | `/devlog` | `LiveOpsDevLog` |
| GET  | `/ratings?version=v` | `LiveOpsRatingData` |
| POST | `/ratings` | `{ version, score, playerId }` |
| POST | `/messages` | `{ playerId, message, category, tag }` |
| POST | `/events` | `LiveOpsEvent` |

Ответы-массивы ожидаются как JSON-массив `[...]`.

---

## Локализация

```csharp
string lang = liveOpsSystem.Language; // из LiveOpsConfig.defaultLanguage
string text = message.title.Get(lang); // fallback → "en" → первый доступный
```

JSON с сервера:
```json
"title": { "ru": "Заголовок", "en": "Title", "de": "Titel" }
```

---

## show_after (условия показа виджетов)

```json
"show_after": {
  "operator": "AND",
  "launches": 3,
  "playtime_minutes": 30,
  "player_prefs": [{ "key": "tutorial_complete", "value": "1" }]
}
```

Проверяется на клиенте: `liveOpsSystem.IsWidgetVisible("rating")`.  
Ключи: `"cards"` · `"messages"` · `"wishlist"` · `"rating"`.

---

## Кастомный провайдер

Если нужен нестандартный бэкенд — реализовать `ILiveOpsProvider` и установить до `InitializeAsync()`:

```csharp
liveOpsConfig.SetProvider(new MyGameProvider(serverUrl, projectId));
```

Новые методы имеют default-реализацию (возвращают null) — существующие провайдеры не ломаются.

---

## Файлы пакета

| Файл | Назначение |
|------|-----------|
| `LiveOpsSystem.cs` | Главная система |
| `LiveOpsConfig.cs` | ScriptableObject конфига |
| `DefaultHttpLiveOpsProvider.cs` | Встроенный HTTP-провайдер |
| `ILiveOpsProvider.cs` | Интерфейс провайдера |
| `Data/LocalizedString.cs` | Локализованная строка |
| `Data/LiveOpsDataPayload.cs` | Payload для EventBus |
| `Data/LiveOpsWidgetConfig.cs` | PanelConfig, WidgetDef, ShowAfter, PlayerContext |
| `Data/LiveOpsAnnouncement.cs` | Новость/объявление |
| `Data/LiveOpsDevLog.cs` | Dev Log с чеклистом |
| `Data/LiveOpsMilestone.cs` | Прогресс-бар (вишлист) |
| `Data/LiveOpsRating.cs` | Рейтинг билда |
| `Data/LiveOpsStubConfig.cs` | Mock-данные для тестирования |
| `UI/Windows/LiveOps/CommunityPanelWindow.cs` | UI-компонент панели |
