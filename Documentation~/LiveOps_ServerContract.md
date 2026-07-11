# LiveOps — контракт клиент ↔ сервер

Что должен предоставлять сервер (PocketBase + JS-хуки), чтобы клиентская часть
пакета (`Runtime/LiveOps`, провайдер `PocketBaseHttpLiveOpsProvider`) работала.

**Источник правды по файлам сервера** — репозиторий `liveops-dashboard`
(`D:\_UNITY\liveops-dashboard`): папка `server/` (pb_hooks) и `deploy.ps1`/`deploy.sh`.
**Источник правды по схеме коллекций** — миграции PocketBase на сервере
(`/opt/liveops/pb_migrations/`).

## Раскладка на сервере

```
/opt/liveops/
├── pocketbase            # бинарник PB (systemd-сервис "liveops", порт 8090)
├── pb_data/              # база (data.db) — БЭКАПИТЬ, в репозитории её нет!
├── pb_migrations/        # миграции схемы
├── pb_hooks/             # JS-хуки (заливаются деплой-скриптом из server/)
├── dashboard/index.html  # веб-дашборд (собирается build.py из src/)
└── localization/{project_id}/  # экспорты локализации из дашборда
```

Деплой: `deploy.ps1` (Windows) / `deploy.sh` — scp файлов + `systemctl restart liveops`.
Reverse proxy — nginx (порты 80/443), домен `api.twohuyakproduction.com`.

⚠ **Права:** сервис работает под `www-data`, файлы заливаются под `deploy`.
Каталог `/opt/liveops/localization/` должен быть доступен `www-data` на запись
(hook создаёт в нём подпапки проектов), иначе `/api/localization/upload` отвечает 500.
Правильное состояние: `chown -R www-data:www-data /opt/liveops/localization`
(сейчас временно решено через `chmod o+w` — у deploy нет root-sudo).
У `deploy` sudo ограничен `systemctl restart liveops`.

## JS-хуки (pb_hooks) и их зоны ответственности

| Файл | Что делает |
|------|-----------|
| `hooks.pb.js` | `GET /api/system-stats` (RAM/диск, только superuser — для дашборда); upsert `poll_votes` по (poll_id, player_id) через `onRecordCreateRequest` |
| `ratings.pb.js` | **Перехватывает роут** `POST /api/collections/ratings/records`: upsert по (project_id, player_id, version), поддержка `player_name`, ответ `{ok, avg, count}` (avg — по последней оценке каждого игрока) |
| `poll_results.pb.js` | `GET /api/polls/results?poll_id=&project_id=` — агрегация голосов |
| `messages_my.pb.js` | `GET /api/messages/my?player_id=&project_id=` — переписка игрока; `POST /api/messages/confirm` — отметка доставки ответов (`reply_status: sent → delivered`) |
| `localization.pb.js` | `POST /api/localization/upload` — сохранение экспорта локализации на диск |
| `translate.pb.js` | `GET /api/translate?text=&target=&source=` — прокси к Google Translate (без ключа) |

⚠ **Не добавлять** `onRecordCreateRequest("ratings")` в другие хуки: роут оценок
перехвачен `ratings.pb.js` целиком, такой хук будет мёртвым кодом — но молча
включится с другой логикой, если перехват убрать.

## Что вызывает клиент (PocketBaseHttpLiveOpsProvider)

- `GET /api/collections/{collection}/records` — коллекции: `messages`, `polls`,
  `announcements`, `devlog`, `goals`, `ratings`, `content_order`, panel config.
- `POST /api/collections/messages/records` — фидбек/сообщение игрока
  (player_id, project_id, message, category).
- `POST /api/collections/ratings/records` — оценка (обрабатывает `ratings.pb.js`).
- `POST /api/collections/poll_votes/records` — голос (upsert в `hooks.pb.js`).
- `GET /api/messages/my`, `POST /api/messages/confirm` — переписка.
- `GET /api/polls/results` — результаты опросов.

Локализуемые поля коллекций хранятся как JSON `{lang: text}` → `LocalizedString`
на клиенте. Ответы разработчика на сообщения — `reply` + `reply_localized`.

## Идентификация

- `player_id` — GUID из PlayerPrefs либо задаётся `LiveOpsSystem.SetPlayerId()`.
- `project_id` — из `LiveOpsConfig.projectId` (например `last-convoy`);
  один сервер обслуживает несколько проектов.
- Авторизации на игровых endpoints нет — сервер доверяет `player_id`
  (риск принят для плейтестов; при выходе в прод добавить подпись запросов).

## Правила изменения контракта

1. Меняешь хук/endpoint — сначала в `liveops-dashboard/server/`, деплой скриптом.
   Ничего не редактировать на сервере напрямую (файлы перезатрёт следующий деплой).
2. Меняешь схему коллекции — через PB-дашборд/миграцию; миграции остаются на
   сервере, бэкапь `pb_data` перед серьёзными изменениями.
3. Новый метод клиента (`ILiveOpsProvider`) + новый endpoint — добавить строку
   в таблицу выше и в деплой-скрипты, если появился новый файл хука.
