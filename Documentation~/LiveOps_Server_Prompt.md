# LiveOps Server — задание для Claude Code

## Контекст

Ты реализуешь серверную часть LiveOps-системы для Unity-игр на базе **PocketBase**.

Unity-клиент уже написан и ожидает **точный API-контракт**, описанный ниже.
Отклонение от контракта сломает клиент. Клиент исправить нельзя.

**Стек:** PocketBase (Go-бинарник, SQLite) + статичный HTML-дашборд + Bash-скрипты деплоя.

---

## Архитектура

```
VDS
├── /opt/liveops/
│   ├── pocketbase          # бинарник
│   ├── pb_data/            # БД и файлы PocketBase
│   └── dashboard/          # статичный HTML-дашборд (index.html + assets)
├── /etc/systemd/system/
│   └── liveops.service     # автозапуск
└── /etc/nginx/sites-available/
    └── liveops.conf        # reverse proxy + SSL
```

**Один инстанс PocketBase обслуживает несколько проектов через `project_id`.**
Например: `last-convoy`, `last-convoy-demo`, `last-convoy-playtest` — три проекта в одной БД.

---

## Структура файлов проекта

```
liveops-server/
├── scripts/
│   ├── install.sh              # установка PocketBase + nginx + systemd на VDS
│   ├── create_collections.sh   # создание всех коллекций через PocketBase API
│   └── backup.sh               # ежедневный бэкап pb_data/
├── dashboard/
│   ├── index.html              # дашборд (всё в одном файле)
│   └── (никаких внешних зависимостей кроме CDN)
├── nginx/
│   └── liveops.conf
├── systemd/
│   └── liveops.service
└── README.md
```

---

## Коллекции PocketBase

Все коллекции содержат поле `project_id` (string, required, indexed).
Все read-запросы клиента фильтруются по `project_id`.
Поле передаётся клиентом в заголовке `X-Project-ID`.

### 1. `panel_config`
Настройки виджетов Community Panel. Одна запись на `project_id`.

| Поле | Тип PocketBase | Описание |
|------|----------------|----------|
| `project_id` | text, required | |
| `show_cards` | bool, default: true | |
| `show_messages` | bool, default: true | |
| `show_goal` | bool, default: false | |
| `show_rating` | bool, default: false | |
| `show_after` | json | Условия показа виджетов (см. ниже) |
| `wishlist_data` | json | `LiveOpsMilestoneData` (current, goal, unit, description) |
| `rating_meta` | json | `{ "version": "0.4.1" }` |

Доступ: **read — public, write — admin only.**

`show_after` JSON-схема (все поля опциональны):
```json
{
  "operator": "AND",
  "launches": 3,
  "playtime_minutes": 30,
  "player_prefs": [
    { "key": "tutorial_complete", "value": "1" }
  ]
}
```

---

### 2. `announcements`
Новости и объявления. Карточки типа `"announcement"` в панели.

| Поле | Тип PocketBase | Описание |
|------|----------------|----------|
| `project_id` | text, required | |
| `title` | json, required | `{"ru": "...", "en": "..."}` |
| `body` | json, required | `{"ru": "...", "en": "..."}` |
| `url` | url | ссылка на полный пост (Steam / сайт), nullable |
| `published_at` | date, required | UTC |
| `is_active` | bool, default: true | фильтр активных |

Доступ: **read — public** (только `is_active = true`), **write — admin only.**

---

### 3. `polls`
Опросы. Карточки типа `"poll"` в панели.

| Поле | Тип PocketBase | Описание |
|------|----------------|----------|
| `project_id` | text, required | |
| `question` | json, required | `{"ru": "...", "en": "..."}` |
| `poll_type` | text, default: "single" | `"single"` / `"multi"` |
| `options` | json, required | массив `[{"id": "opt1", "label": {"ru":"...", "en":"..."}}]` |
| `expires_at` | date | UTC, nullable |
| `is_active` | bool, default: true | |

Доступ: **read — public** (только `is_active = true`), **write — admin only.**

---

### 4. `poll_votes`
Голоса игроков.

| Поле | Тип PocketBase | Описание |
|------|----------------|----------|
| `poll_id` | text, required | ссылка на polls.id |
| `player_id` | text, required | Steam-ник или GUID |
| `option_ids` | json, required | `["opt1"]` или `["opt1", "opt2"]` |
| `project_id` | text, required | |

Доступ: **read — public, write — public.**
Уникальность: один `player_id` на один `poll_id` (проверять при POST /polls/{id}/vote).

---

### 5. `devlog`
Dev Log с чеклистом задач. Одна актуальная запись на `project_id`.

| Поле | Тип PocketBase | Описание |
|------|----------------|----------|
| `project_id` | text, required | |
| `focus` | json | `{"ru": "...", "en": "..."}` короткий фокус |
| `title` | json | |
| `description` | json | |
| `items` | json | `[{"label": {"ru":"...", "en":"..."}, "done": false}]` |
| `updated_at` | date | UTC |
| `is_active` | bool, default: true | |

Доступ: **read — public** (только `is_active = true`), **write — admin only.**

---

### 6. `ratings`
Оценки игроков (1–10) по версии билда.

| Поле | Тип PocketBase | Описание |
|------|----------------|----------|
| `project_id` | text, required | |
| `version` | text, required | версия билда, например `"0.4.1"` |
| `score` | number, required | 1–10 |
| `player_id` | text, required | |

Доступ: **read — public, write — public.**
Уникальность: один `player_id` на `project_id + version`. Повторный POST обновляет оценку (upsert).

---

### 7. `messages`
Фидбек от игроков.

| Поле | Тип PocketBase | Описание |
|------|----------------|----------|
| `project_id` | text, required | |
| `player_id` | text, required | |
| `game_version` | text | |
| `message` | text, required | |
| `category` | text | `"bug"` / `"suggestion"` / `"other"` |
| `tag` | text | `"poll:{poll_id}"` / `"general"` / `"bug"` / `"suggestion"` |
| `timestamp` | text | ISO 8601 UTC от клиента |

Доступ: **read — admin only, write — public.**

---

### 8. `events`
Аналитические события.

| Поле | Тип PocketBase | Описание |
|------|----------------|----------|
| `project_id` | text, required | |
| `player_id` | text, required | |
| `name` | text, required | `"session_start"`, `"level_complete"` и т.д. |
| `game_version` | text | |
| `timestamp` | text | ISO 8601 UTC от клиента |
| `data` | json | произвольные параметры `{"key": "value"}` |

Доступ: **read — admin only, write — public.**

---

## API-контракт

Клиент обращается **напрямую к PocketBase REST API** (`/api/collections/.../records`).
Провайдер `DefaultHttpLiveOpsProvider` в Unity транслирует наши методы в PocketBase-запросы.

**Заголовки во всех запросах от клиента:**
```
X-Project-ID: last-convoy
X-Steam-ID: SteamNickname  (если задан playerId)
```

Все GET-запросы фильтруют по `project_id` через PocketBase filter:
```
GET /api/collections/announcements/records?filter=(project_id='last-convoy'%26%26is_active=true)&sort=-published_at
```

---

### GET /api/collections/panel_config/records
Возвращает конфигурацию виджетов.

Ответ (PocketBase список, берём `items[0]`):
```json
{
  "items": [{
    "project_id": "last-convoy",
    "show_cards": true,
    "show_messages": true,
    "show_goal": true,
    "show_rating": false,
    "show_after": {
      "operator": "AND",
      "launches": 3,
      "playtime_minutes": 30,
      "player_prefs": []
    },
    "wishlist_data": {
      "description": {"ru": "Вишлист в Steam", "en": "Steam Wishlists"},
      "current": 1240,
      "goal": 5000,
      "unit": {"ru": "вишлистов", "en": "wishlists"},
      "updatedAt": "2025-03-01T12:00:00Z"
    },
    "rating_meta": { "version": "0.4.1" }
  }]
}
```

**Маппинг в C#:** `LiveOpsPanelConfig`

---

### GET /api/collections/announcements/records
Фильтр: `is_active=true`, сортировка: `-published_at`.

Ответ:
```json
{
  "items": [{
    "id": "abc123",
    "title": {"ru": "Обновление 0.4", "en": "Update 0.4"},
    "body": {"ru": "Текст...", "en": "Text..."},
    "url": "https://store.steampowered.com/news/...",
    "published_at": "2025-03-01T10:00:00Z"
  }]
}
```

**Маппинг в C#:** `List<LiveOpsAnnouncement>`

---

### GET /api/collections/polls/records
Фильтр: `is_active=true`.

Ответ:
```json
{
  "items": [{
    "id": "poll1",
    "question": {"ru": "Какой режим важнее?", "en": "Which mode matters more?"},
    "poll_type": "single",
    "options": [
      {"id": "opt1", "label": {"ru": "Кампания", "en": "Campaign"}},
      {"id": "opt2", "label": {"ru": "Выживание", "en": "Survival"}}
    ],
    "expires_at": null
  }]
}
```

Для каждого опроса клиент дополнительно запрашивает агрегат голосов (см. ниже).

**Маппинг в C#:** `List<LiveOpsPoll>`

**Агрегат голосов** (запрашивается сразу после получения опросов):
```
GET /api/collections/poll_votes/records?filter=(poll_id='poll1')
```
Клиент сам считает `votes` на каждый `option_id` из полученных записей.

**Голос текущего игрока:**
```
GET /api/collections/poll_votes/records?filter=(poll_id='poll1'%26%26player_id='SteamNick')
```
Если `items` не пуст — `userVote = items[0].option_ids`.

---

### POST /api/collections/poll_votes/records
Отправка голоса.

Тело запроса:
```json
{
  "poll_id": "poll1",
  "player_id": "SteamNick",
  "option_ids": ["opt1"],
  "project_id": "last-convoy"
}
```

Перед записью проверить: если запись с `poll_id + player_id` уже есть — обновить (PATCH), иначе создать (POST).

Ответ: `200 OK` / `400 Bad Request`.

---

### GET /api/collections/devlog/records
Фильтр: `is_active=true`, взять первую запись.

Ответ:
```json
{
  "items": [{
    "id": "dev1",
    "focus": {"ru": "Боевая система", "en": "Combat System"},
    "title": {"ru": "Что в работе", "en": "In Progress"},
    "description": {"ru": "Описание...", "en": "Description..."},
    "items": [
      {"label": {"ru": "Ближний бой", "en": "Melee"}, "done": true},
      {"label": {"ru": "Дальний бой", "en": "Ranged"}, "done": false}
    ],
    "updated_at": "2025-03-05T09:00:00Z"
  }]
}
```

**Маппинг в C#:** `LiveOpsDevLog`

---

### GET /api/collections/ratings/records?filter=(project_id='...'%26%26version='0.4.1')
Получить все оценки по версии, вернуть агрегат.

Клиент вычисляет `avg` и `count` из массива `items`.
Для `userVote` — отдельный запрос:
```
GET /api/collections/ratings/records?filter=(project_id='...'%26%26version='0.4.1'%26%26player_id='SteamNick')
```

**Маппинг в C#:** `LiveOpsRatingData { version, avg, count, userVote }`

---

### POST /api/collections/ratings/records
Отправить оценку.

Тело:
```json
{
  "project_id": "last-convoy",
  "version": "0.4.1",
  "score": 8,
  "player_id": "SteamNick"
}
```

Upsert: если запись с `project_id + version + player_id` существует — PATCH, иначе POST.

Ответ: вернуть `{ "ok": true, "avg": 7.4, "count": 42 }` — подсчитать из БД после записи.

**Маппинг в C#:** `LiveOpsRatingResult { ok, avg, count }`

---

### POST /api/collections/messages/records
Тело:
```json
{
  "project_id": "last-convoy",
  "player_id": "SteamNick",
  "game_version": "0.4.1",
  "message": "Не работает кнопка...",
  "category": "bug",
  "tag": "general",
  "timestamp": "2025-03-09T10:00:00Z"
}
```

Ответ: `200 OK`.

---

### POST /api/collections/events/records
Тело:
```json
{
  "project_id": "last-convoy",
  "player_id": "SteamNick",
  "name": "session_start",
  "game_version": "0.4.1",
  "timestamp": "2025-03-09T10:00:00Z",
  "data": {"level": "1", "difficulty": "normal"}
}
```

Ответ: `200 OK`.

---

## Скрипты

### install.sh
Должен:
1. Скачать последний PocketBase бинарник с GitHub releases (`pocketbase/pocketbase`)
2. Создать директорию `/opt/liveops/`
3. Скопировать бинарник, dashboard/, systemd unit, nginx конфиг
4. Включить и запустить systemd сервис
5. Перезагрузить nginx

Параметры через переменные окружения или аргументы:
```bash
DOMAIN=liveops.example.com ./install.sh
```

---

### create_collections.sh
Создать все коллекции через PocketBase API (`/api/collections`).
Использует Admin API key (переменная `PB_ADMIN_TOKEN`).

Порядок создания важен (poll_votes ссылается на polls).

---

### backup.sh
Копировать `/opt/liveops/pb_data/` в `/opt/backups/liveops/YYYY-MM-DD/`.
Хранить последние 7 дней. Добавить в crontab.

---

## Дашборд (`dashboard/index.html`)

Статичный HTML-файл. Всё в одном файле (CSS и JS inline или из CDN).
Деплоится в `/opt/liveops/dashboard/` и раздаётся через nginx как статика.

**Авторизация:** при открытии запрашивать PocketBase Admin token (хранить в `sessionStorage`).

**Интерфейс — три вкладки:**

### Вкладка 1: Опросы
- Выбор `project_id` (select сверху, общий для всех вкладок)
- Список активных опросов
- Для каждого опроса:
  - Вопрос + тип (single/multi)
  - Bar chart результатов: название варианта | полоса | процент | кол-во голосов
  - Итого: N голосов
- Кнопка "Закрыть опрос" (устанавливает `is_active = false`)
- Список закрытых опросов (свёрнутый, раскрывается по клику)

### Вкладка 2: Аналитика
- Выбор `project_id` + диапазон дат
- Таблица событий: Event Name | Count | Уникальных игроков | Последнее
- График событий по дням (Chart.js line chart, топ-5 событий)
- Таблица версий: Version | Sessions | Уникальных игроков

### Вкладка 3: Фидбек
- Таблица сообщений: дата | версия | категория | тег | игрок | сообщение
- Фильтры: по категории, по тегу, по версии
- Пагинация (50 записей на страницу)

**CDN разрешён:** Chart.js для графиков, больше ничего тяжёлого.
**Стиль:** тёмная тема, минимализм. Никаких UI-фреймворков.

---

## nginx конфиг

```nginx
server {
    listen 443 ssl;
    server_name liveops.example.com;

    # SSL (certbot добавит сам)

    # PocketBase API и Admin UI
    location / {
        proxy_pass http://127.0.0.1:8090;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

    # Дашборд
    location /dashboard/ {
        alias /opt/liveops/dashboard/;
        index index.html;
        auth_basic off;
    }
}

server {
    listen 80;
    server_name liveops.example.com;
    return 301 https://$host$request_uri;
}
```

---

## systemd unit

```ini
[Unit]
Description=LiveOps PocketBase
After=network.target

[Service]
Type=simple
User=www-data
WorkingDirectory=/opt/liveops
ExecStart=/opt/liveops/pocketbase serve --http="127.0.0.1:8090"
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

---

## README.md

Включить:
1. Требования (Ubuntu 22.04+, nginx, certbot)
2. Быстрый старт (5 команд от свежего VDS до работающего сервера)
3. Добавление нового проекта (создать запись в `panel_config` с новым `project_id`)
4. Добавление нового инстанса (запустить второй PocketBase на другом порту)
5. Настройка Unity-клиента (`LiveOpsConfig.asset` — заполнить `serverUrl`, `projectId`)
6. Доступ к дашборду

---

## Приоритет реализации

1. `create_collections.sh` — основа всего
2. `install.sh`
3. `dashboard/index.html`
4. nginx конфиг + systemd unit
5. `backup.sh`
6. `README.md`
