# ИИ Аудио студия — Документация

Единое окно генерации игрового звука на локальном ComfyUI: UI-звуки, боевые SFX,
эмбиенты (Stable Audio Open) и музыка/стингеры (ACE-Step). История вариантов с
рецептами, публикация активного варианта прямо в `SoundLibrary` игры.

Меню: **ProtoSystem → ИИ студии → ИИ Аудио студия** (класс `AudioStudioWindow`,
сборка ProtoSystem.AudioGen.Editor). Кнопка «🎵 Звук» в главном тулбаре (появляется
после перезапуска редактора). Подключение в новом проекте — `Documentation~/AIStudios.md`.

## Архитектура (зеркало ИИ Арт-студии)

```
Runtime-контракт  Packages/com.protosystem.core/Runtime/AudioGen/
└── IAudioContentProvider — игровой SO сам объявляет, какой звук ему нужен
    └── AudioContentSet   — набор («UI: звуки», «Эмбиенты»)
        └── AudioContentItem — сущность + колбэки (клип, промпт, луп, рандом-пул)

Editor-ядро       Packages/com.protosystem.core/Editor/AudioGen/
├── ComfyAudioClient    — транспорт: графы Stable Audio / ACE-Step, polling /history
├── ComfyAudioServerControl — статус/запуск сервера (общие настройки с арт-студией)
├── AiAudioGenerator    — очередь (Progress API), группировка по чекпоинту,
│                          SaveResult: FLAC → ffmpeg → WAV → импорт AudioClip
├── AudioConvert        — ffmpeg: FLAC→WAV 44.1к/16бит, срез хвостовой тишины one-shot'ов
├── AudioStylePreset / AudioGenProfile — стиль (движок+шаблон+негатив) и профиль
├── AudioCollectionAsset / AudioCollectionGroup / AudioStudioConfig — наборы и вкладки
├── AudioVariantLibrary — данные истории вариантов (рецепты)
├── AudioPreviewUtil    — проигрывание в редакторе (AudioUtil через рефлексию), волноформа
├── AudioStudioWindow   — окно (вкладки, витрина, пикер вариантов, левая панель)
├── AudioVariantStore   — история вариантов, паки, публикация, чистка; SoundLibrary проекта
├── AudioStudioToolbar  — кнопка «🎵 Звук» в главном тулбаре
├── AudioStudioShortcuts / SoundSoloMode / SoundUsageScanner — ПКМ по SoundLibrary,
│                          режим «Соло», поиск использований id
└── AudioStudioExtensions — точки расширения для проекта

LastConvoy        Assets/LastConvoy/Scripts/Editor/
├── AIStudioHooks       — игровые лупы (LoopPlayer), гейт соло (SoundSoloGate),
│                          исключения скана (VoiceLineLibrary, регистраторы голосов)
└── Audio/EtherVoiceRegistrar, IntroVoiceRegistrar — разовая озвучка эфира и интро

Провайдер         Assets/LastConvoy/Scripts/Systems/Meta/Configs/
└── LastConvoyAudioContent(.AudioContent) — сущности поверх SoundLibrary
```

**Принцип:** ядро не знает про игру. Провайдер объявляет сеты по префиксам id
(`ui_` / `car_` / `enemy_`+`boss_` / `amb_` / `music_`+`stinger_` / остальное — миссия),
публикация пишет `SoundEntry.clip` (или создаёт запись). Все игровые системы ссылаются
на звуки по строковым id — после публикации ничего дополнительно подключать не надо.

## Модели и сервер

Тот же ComfyUI, что у арт-студии (`E:\AI\ComfyUI`, адрес/запуск — общие настройки
`ProtoIcon.ComfyServer/ComfyLaunch`). Аудио-модели в `models/`:

| Файл | Модель | Назначение | Лицензия |
|---|---|---|---|
| `checkpoints/stable-audio-open-1.0.safetensors` | Stable Audio Open 1.0 (репак Comfy-Org) | SFX, UI, эмбиенты до ~47 с | Stability Community (коммерч. до $1M) |
| `text_encoders/t5_base.safetensors` | T5-base | энкодер Stable Audio (в чекпоинте его НЕТ) | Apache 2.0 |
| `checkpoints/ace_step_v1_3.5b.safetensors` | ACE-Step 3.5B | музыка, стингеры | Apache 2.0 |

Настройки: **Project Settings ▸ AI Audio Tools** (чекпоинты, T5, путь к ffmpeg,
API-ключ ElevenLabs, дефолтный стиль).

## Третий движок: ElevenLabs (облако)

`ElevenLabsClient` → `POST /v1/sound-generation`, заголовок `xi-api-key`. Качество SFX
выше локального Stable Audio; нативные `duration_seconds` (0.5–30 с, трюк «генерим 5 с
и режем» не нужен) и `loop: true` — честные бесшовные лупы для эмбиентов. Отдаёт MP3 →
общий конвейер ffmpeg → WAV. Цена ~40 кредитов/сек (тариф Starter $5 = 30 000 кредитов).

Использование: профиль `AudioProfile_SFX_Cloud` (стиль `AudioStyle_SFX_Cloud`,
`engine = ElevenLabs`) — выбрать в дропдауне «Профиль» у нужной вкладки. Строгость
следования промпту — `promptInfluence` в стиле (0.4). У облака нет чекпоинтов и
сида — «перегенерация по рецепту» даёт похожий, но не идентичный результат.

**API-ключ хранится ТОЛЬКО в EditorPrefs** (`ProtoAudio.ElevenLabsKey`, реестр
Windows) — в файлы проекта и git не попадает: ElevenLabs автоматически отключает
ключи, засветившиеся в публичных репозиториях. Поле ввода — в Project Settings.
Лимит трат задаётся на самом ключе в дашборде ElevenLabs (Usage Limits).

## Четвёртый движок: ElevenLabs TTS — «Голос» (озвучка поверх локализации)

Вкладка «Голос» (3 секции: миссия/загрузка/итоги) — реплики диспетчера по рации.
Ключевая идея: **тексты реплик = ключи локализации** (`lc.voice.*` в `LCKeys.Voice`,
таблица UI). Студия читает/пишет String Table нужной локали напрямую: правка
«Промпта» в пикере = правка перевода. Новые языки переводятся обычным `/localize`.

- `VoiceLineLibrary.asset` (Settings/Sound/) — СПИСОК того, что озвучиваем: реплики
  {id, locKey, категория} и **языки озвучки** (`languages`, старт en+ru). Добавить
  язык озвучки = перевести ключи + дописать код локали → в студии появляются
  сущности `voice_{id}_{lang}` → «Пустые ×N».
- Публикация — в SoundLibrary под `voice_{id}_{lang}` с категорией Voice.
- Стиль `AudioStyle_VoiceRadio`: движок ElevenLabsTts, голос из дропдауна
  (`GET /v1/voices`), стабильность/похожесть, модель `eleven_multilingual_v2`,
  радио-фильтр ffmpeg (`postFilterChain`: bandpass 250-3400 + crush + компрессор).
- Рецепт хранит voiceId/модель/подачу/фильтр + seed (best-effort детерминизм TTS).
- Runtime: `VoiceLines.Play("swarm")` — язык по `Loc.CurrentLanguage` (fallback en),
  анти-спам (занятость + кулдаун 25 с + пауза эфира 2 с), `Priority.High` прерывает,
  ducking-снапшот `VoiceDucking` глушит SFX/музыку на время реплики (уровни снапшота
  настраиваются руками в окне Audio Mixer — создан копией Default).
- Контракт длины: 8-14 слов ≈ 2-4 с (длительность TTS управляется только текстом).
- Вызовы: MissionSoundHandler (11 событий, delay 0.6 с после сирены), GameOver-окно
  (по причине исхода), LoadingScreen (случайная болтовня), WorldGen (лор-цитата).

## Зависимость: ffmpeg

ComfyUI сохраняет аудио только FLAC/MP3/Opus, Unity импортирует WAV/MP3/OGG.
Цепочка lossless: FLAC → ffmpeg → WAV (44.1 кГц, 16 бит). ffmpeg ищется в PATH
(«ffmpeg»), путь можно указать в настройках. Для one-shot'ов (не-лупов) при
конвертации срезается хвостовая тишина (порог −55 дБ, анти-щелчковый фейд 15 мс) —
модель заполняет запрошенную длительность, а хвост тишины даёт «лаг» при частом
проигрывании.

## Быстрый старт

1. Поднять ComfyUI (индикатор в левой панели, кнопка «Запустить»).
2. Выбрать вкладку (UI / Состав / Мобы / Миссия / Эмбиенты / Музыка) и звук в списке.
3. Проверить промпт (заготовки на ~75 звуков уже засеяны) — поле «Промпт» в пикере,
   это только `{subject}`; шаблон стиля и негатив — в левой панели.
4. «+N ещё» — пачка в очередь. Каждая генерация = новый вариант.
5. ▶ — прослушать, **двойной клик** — сделать активным (публикуется в SoundLibrary).
6. Ctrl+клик несколько вариантов → «Выбранные → рандом-пул» — заполняет
   `SoundEntry.clipVariants` (движок умеет играть случайный клип из пула).

## Стили и профили

- **AudioStyle_UI** — Stable Audio, 2 с, негатив против музыки/эмбиента, трим хвоста.
- **AudioStyle_SFX** — Stable Audio, 4 с, дизель-панк SFX.
- **AudioStyle_Ambient** — Stable Audio, 45 с, «seamless loop, field recording»,
  негатив против музыки/ритма, без трима.
- **AudioStyle_Music** — ACE-Step, 95 с, теги в промпте, лирика `[inst]` (инструментал).

Профиль (стиль + чекпоинт) назначается на набор в ассете набора. Полные песни с
вокалом сознательно вне студии — их проще делать в Suno и класть руками.

## Что уже подключено к игре

- Все 83 записи SoundLibrary видны в студии; публикация — прямо в запись.
- «Дыры» объявлены в `AudioContent.asset` (extraSounds) и появятся в библиотеке при
  первой публикации: `music_gameplay/victory/defeat`, `stinger_*` (4 шт.),
  `amb_swamp/dead_forest/ice_world/scorched`, `car_cannon_charge`, `car_mines_arm`,
  `car_surface_ice/organic`.
- Починены битые id: `BiomeProfile_Swamp.ambientSoundId` (`amb_ambient_sound` →
  `amb_swamp`), пустые эмбиенты DeadForest/IceWorld/Scorched, и
  `GameSessionSoundScheme.menuMusic` (`music_menu` → существующий `music_menu_music`).

## Инструменты связки с игрой

- **Монитор «Сейчас звучит»** (левая панель, Play Mode): все играющие источники,
  ■ — остановить, клик — переход к сущности в студии, Ctrl+клик — пинг источника.
  Строки списка подсвечиваются зелёным, пока их звук реально звучит.
- **Соло (S на строке, можно несколько)**: в игре слышны только отмеченные — mute
  источников по клипу + гейт one-shot'ов (`SoundSoloGate.Allow` в 8 точках вызова:
  оружие, снаряды, враги, миссия, поезд, вагоны). Индикатор «СОЛО: N ✕» в тулбаре.
- **Громкость записи**: слайдер в % + числовое поле в пикере, пишет SoundEntry.volume
  и применяется к играющим источникам сразу (лупам — через LoopPlayer.EditorApplyBaseVolume,
  игровые системы перезаписывают громкость каждый кадр).
- **«Исп.?»**: скан использований id по ассетам/коду; ⚠ — звук нигде не триггерится.
  Текстовый — мёртвые сериализованные поля и «спящие» триггеры (car_drift без
  Drift-физики) считает использованием.
- **Паки («Паки ▾»)**: снапшот активных вариантов всех сущностей, применение одним
  кликом (работает и в Play Mode), ✓ на совпадающем. Ассеты в AudioStudio/Packs.
- **Поиск** в тулбаре (id/имя/описание); **описание «где звучит»** — редактируемое
  поле в пикере (контракт SetUsageNote), засеяно из sound_effects_list.md.
- **Переходы**: ПКМ по полям записи в инспекторе SoundLibrary → «Открыть в студии»;
  ПКМ по AudioClip в Project → «Найти звук в ИИ Аудио студии».
- **Кредиты ElevenLabs** в левой панели (нужно право User Read у ключа).

## Грабли (выстрадано)

- **Репак чекпоинта Stable Audio НЕ содержит T5** — «checkpoint does not contain a
  valid clip». Поэтому в графе отдельная нода `CLIPLoader` с `t5_base.safetensors`
  (`type: stable_audio`).
- **Трим хвоста — только через двойной `areverse`**: `silenceremove` режет лишь начало.
  Фейд внутри цепочки обязан быть `afade=t=in` (на развёрнутом звуке!) — `t=out`
  глушит весь клип после первых миллисекунд.
- **Своп моделей дорог**: Stable Audio ↔ ACE-Step в VRAM — десятки секунд. Очередь
  группирует запросы по чекпоинту (как арт-студия по Flux/SDXL).
- Превью-волноформа (`AssetPreview.GetAssetPreview`) грузится асинхронно — ячейка
  варианта добирает её повторными попытками по таймеру.
- Проигрывание в редакторе — только внутренний `UnityEditor.AudioUtil` через
  рефлексию (`PlayPreviewClip`), публичного API нет до сих пор.
- `AudioImporter.preloadAudioData` устарел — ставится через
  `defaultSampleSettings.preloadAudioData`.

## Грабли, добавленные жизнью

- **SO-класс — только в одноимённом файле**: AudioCollectionGroup вторым классом в
  AudioStudioConfig.cs терял m_Script у ассетов после перезапуска редактора.
- **Очередь генерации** статическая: domain reload её убивает → на время очереди
  LockReloadAssemblies (компиляция ждёт конца пачки).
- **SoundLibrary**: 39 записей были spatial с нулевыми радиусами → неслышимые 3D
  (лечится spatial=false для звуков, играющихся без координат); флаги loop — из дока.
- **Qwen отдаёт 24 кГц**: фильтры вроде asetrate=44100*x требуют ресемпла до 44.1к
  ДО цепочки (иначе ускорение ×1.7).
- **EditorPrefs нельзя читать из Task.Run** (main thread only) — значения снимать
  до запуска фона. **Никогда не ждать async блокирующе на главном потоке** — дедлок.

## Не сделано (кандидаты)

- UI Toolkit-окна НЕ играют клики/ховеры (клипы есть, кода нет) — дыра игры, не студии.
- Бесшовность эмбиент-лупов: модель «seamless loop» понимает средне; при
  необходимости — кроссфейд-запекание в AudioConvert (стык концов через afade+amix).
- Постобработка вариантов (нормализация громкости, питч) — по образцу постобработки
  арт-студии (неразрушающе, у варианта).
