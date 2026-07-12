# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.19.0] - 2026-07-12

### Added
- **Community Panel на UI Toolkit** — `CommunityPanelToolkit`: контроллер над
  VisualElement-деревом (не MonoBehaviour), порт CommunityPanelWindow (uGUI).
  Окно-хозяин инстанциирует шаблон `Runtime/UI/Toolkit/CommunityPanel.uxml`
  в свой контейнер и создаёт контроллер в OnBuildUI; Dispose() при пересоздании дерева.
  Стили — `CommunityPanel.uss` через переменные `--cp-*`: проект переопределяет
  их (или селекторы `.cp-*`) в USS окна-хозяина. Рейтинг — один фокусируемый
  контрол: геймпад/клавиатура выбирают 1–10 стрелками влево/вправо и ставят
  оценку Submit'ом (вверх/вниз — уход с контрола), мышь — ховер-превью + клик
- `ILiveOpsPanel` — интерфейс панели для LiveOpsSystem; `RegisterPanel/UnregisterPanel`
  теперь принимают интерфейс (uGUI CommunityPanelWindow реализует его — код проектов
  не меняется), видимость через `SetPanelVisible` вместо прямого SetActive

## [1.18.0] - 2026-07-11

### Added
- **UISystemConfig.preferredBackend (uGUI / UIToolkit)** — в конфиге могут быть
  зарегистрированы ОБА префаба одного окна; какой использовать, выбирает селектор.
  Переключение бэкенда одним полем, инспектор показывает окна с двумя вариантами
- Генератор toolkit-окон ставит префабам метку из UISystemConfig.windowPrefabLabels
  (раньше — хардкод "UIWindow")
## [1.17.3] - 2026-07-11

### Added
- CaptureSystem: имя активного окна UISystem в имени файла скриншота
  (`screenshot_<WindowId>_<timestamp>.png`) — поиск скриншота окна по маске
## [1.17.2] - 2026-07-11

### Fixed
- **UI Toolkit: геймпад не управлял окнами** — EventSystem раздаёт Move/Submit/Cancel
  только выбранному объекту; база теперь сама выбирает PanelEventHandler своей панели
  при показе/фокусе окна (работает с Rewired/Standalone/InputSystem модулями)
- **Кнопки не реагировали на геймпадный Submit** — шаблоны генератора и рекомендация
  переведены с RegisterCallback<ClickEvent> (только указатель) на Button.clicked;
  добавлен хелпер `UIToolkitWindowBase.OnClick(root, name, action)`
## [1.17.0] - 2026-07-11

### Added
- **UI Toolkit как второй бэкенд окон UISystem** — окна на UXML/USS полноправно живут
  в общем графе/стеке с uGUI-окнами (модалки, Level, пауза, курсор, события)
  - `UIToolkitWindowBase` — база: Show/Hide через display+opacity, Blur/Focus через
    pickingMode + USS-класс `window-blurred`, `OnBuildUI(root)` (контракт пересоздания
    дерева после пула), `ApplyOpaqueBackground` через `window-opaque`
  - **Локализация**: конвенция `#лок.ключ` в текстах/tooltip UXML (`ToolkitLocalization`),
    перелокализация по LanguageChanged, USS-класс `lang-{code}` на корне для шрифтов,
    `Localization.SetKey()` для динамики
  - **Геймпад/Steam Deck**: `defaultFocusName` (стартовый фокус), память фокуса при
    Blur/Focus, Cancel (Esc/B) → `OnBackPressed()`, видимый `:focus` в генерируемом USS
  - **Единая сортировка**: фабрика назначает UIDocument'ам PanelSettings по WindowLayer
    (клоны шаблона `UISystemConfig.panelSettings`, sortingOrder = (int)layer — одна
    шкала с Canvas-слоями), внутри слоя — порядок по sortingOrder документа
  - **Wizard + генератор**: ProtoSystem → UI → UI Toolkit Setup & Generator — шаг 1
    устанавливает PanelSettings, шаг 2 генерирует базовые окна (MainMenu, PauseMenu,
    Settings, Loading, GameOver, Credits, GameHUD) в проект: C# (те же WindowId и
    переходы, что у uGUI), UXML с лок-ключами, общий ProtoWindow.uss; существующие
    файлы не перезаписываются
- **Payload навигации**: `UISystem.Open(id, payload)` / `Navigate(trigger, payload)` →
  `OnPayload(object)` в окне до Show (замена статических _pending-сеттеров)
- **`UISystem.BackTo(windowId)`** — закрыть модалки и окна до указанного
- Навигатор публикует `WindowClosed` / `WindowFocused` / `WindowBlurred` (раньше были
  объявлены, но не публиковались)

### Fixed
- **`Evt.UI.WindowOpened` (10001) никогда не публиковался** — навигатор шлёт
  `EventBus.UI.WindowOpened` (10200); подписчики Evt.UI (в т.ч. LiveOps
  fetchOnMainMenuOpen) молча не получали событий. Теперь Evt.UI.WindowOpened/Closed —
  алиасы 10200/10201, обработчик LiveOps понимает WindowEventData
- `UIWindowBase` больше не требует CanvasGroup жёстко ([RequireComponent] снят,
  обращения null-tolerant) — для uGUI-окон поведение не меняется
## [1.16.3] - 2026-07-10

### Fixed
- **CommunityPanel: описание DevLog-карточки не отображалось** — у окна не было поля
  `devLogDescriptionText`, сервер присылал description, но в префабе навсегда оставалась
  заглушка генератора («Описание обновления...»). Добавлено поле + вывод с локализацией;
  при пустом описании текст скрывается. Требуется перегенерация панели или ручная
  привязка Description TMP в префабе.
## [1.16.0] - 2026-07-10

### Breaking behaviour (инициализация систем)
- **Fail-fast по required-зависимостям**: если обязательная `[Dependency]` не найдена,
  `InitializeAsync()` системы не вызывается — система помечается Failed с внятной ошибкой
  (раньше выполнение продолжалось и падало NRE внутри системы)
- **Регистрация в SystemProvider — только после успешной инициализации**: `GetSystem<T>()`
  и инъекции больше не выдают сырые или упавшие системы (раньше регистрация шла до init)
- **Анализ зависимостей по совместимости типов** (`IsAssignableFrom` вместо точного равенства):
  поля-интерфейсы и базовые классы теперь попадают в граф и топологическую сортировку —
  порядок инициализации может измениться; неоднозначные случаи логируются
- Выключенная галочкой система: не инициализируется, недоступна через GetSystem/инъекции
  (поведение прежнее, теперь зафиксировано явно + лог при пропуске)

### Fixed
- `[Dependency]`-поля, объявленные приватными в базовых классах, не инжектились
  (GetFields не возвращает приватные поля предков) — обход иерархии + кеш рефлексии
- Пер-системный прогресс терялся из-за несовпадения ключей (systemName vs SystemId) —
  общий прогресс-бар теперь плавный
- Повторный прогон post-зависимостей идемпотентен (guard по IsInitializedPostDependencies)
- Таймаут инициализации: отписка от событий «зомби»-задачи, поздний результат игнорируется
  с warning в лог

### Added
- `Validate()`: проверка дублей SystemId и зависимостей на выключенные системы
- Инспектор SIM: розовая подсветка систем, зависящих от выключенных (⛔ у имени зависимости,
  счётчик в блоке «Проблемы»)
- `SystemProvider.GetSystem<T>()`: warning при нескольких кандидатах на один тип
- `InitializableSystemBase.SetSystem()` помечен [Obsolete]
## [1.15.0] - 2026-07-10

### Added
- **Claude Translation Runner** — полный цикл AI-перевода одной кнопкой (вкладка "Claude" в AI Translation Window)
  - Export выбранных таблиц/языков → headless-запуск Claude Code CLI → Import + валидация
  - Промпт передаётся через stdin (`claude -p --permission-mode acceptEdits`), без ручного экранирования
  - Если в проекте есть скилл `.claude/skills/localize/SKILL.md` — используется `/localize` (проектный контракт перевода)
  - Кнопка **"Создать скилл на основе проекта"** — headless-Claude изучает доки и строки таблиц, генерирует SKILL.md с тоном игры и глоссарием
  - Живой лог процесса, Cancel, итоговые бейджи (imported/skipped/errors)
- **Coverage-панель** в AI Translation Window — прогресс перевода по каждой таблице и языку
- Сводка по файлам на вкладке Import — "N/M переведено" для каждого JSON
- **LiveOps `verboseLogging`** — флаг в LiveOpsConfig: подробные логи (HTTP, парсинг, unread) только при включённом флаге, через `LiveOpsLog.Info`; ошибки/предупреждения — всегда
- **Documentation~/LiveOps_ServerContract.md** — контракт клиент↔сервер: хуки PocketBase, endpoints, коллекции, правила изменения

### Fixed
- **SystemInitializationManager: инспектор ломался при дубле системы** — `ToDictionary(systemName)` кидал ArgumentException в `GetSystemsInInitializationOrder`/`DetectCyclicDependencies`; заменено на дубле-терпимый словарь (первая запись выигрывает)
- Инспектор SIM: предупреждение о дублях имён с кнопкой «Удалить дубли», подсветка дублированных записей, прогресс-бар инициализации, шапка в общем стиле, строка фильтра систем по имени/типу, устранены дубль заголовка настроек и обрезающиеся подписи

### Changed
- **Редизайн окон локализации** (AI Translation Window, Setup Wizard) — общий стиль `ProtoEditorStyles` (Editor/Common):
  шапка с акцентной полосой, карточки-секции, цветные бейджи статусов, акцентные кнопки,
  компактная сетка языков в две колонки, лог-консоль

## [1.14.0] - 2026-03-06

### Added
- **Steam Build Targets** — поддержка Playtest и Demo в Build Publisher
  - `SteamAppTarget` — каждый target имеет свой App ID, депоты и ветки
  - `SteamBuildTargetType` enum: Main, Playtest, Demo
  - Toolbar переключения между targets в Quick Settings
  - Setup Panel: добавление/удаление targets, настройка App ID для каждого
  - Автоматическая миграция старых конфигов (appId/depotConfig/branches → Main target)
  - VDF генерация использует App ID активного target
  - Общие настройки (username, SteamCMD, options) — одни на все targets
  - Описания и подсказки во всех секциях Setup Panel
- **Auto Version в Build Description** — toggle автоматически подставляет текущую версию в описание билда

## [1.13.3] - 2026-03-06

### Added
- **Cheat Code System** — система чит-кодов через Build Publisher + SettingsSystem
  - Пароль задаётся в Build Publisher (секция "Cheat Codes")
  - SHA256 хэш вшивается в скомпилированный скрипт через `.asmref`
  - При загрузке SettingsSystem автоматически проверяет секцию `[Cheats]` в `settings.ini`
  - `SettingsSystem.IsCheatsUnlocked` — публичное свойство для проверки
  - В Editor-режиме читы всегда разрешены
  - Кнопка "Open settings.ini" в Build Publisher
  - Генерируемые файлы: `Assets/{namespace}/Cheats/CheatCodeHash.g.cs` + `.asmref`

## [1.10.1] - 2025-01-26

### Changed
- **ProtoLogger Migration** — полная миграция Debug.Log → ProtoLogger в пакете
  - EventBus: 28 вызовов
  - GameSession: 3 вызова
  - Initialization: 14 вызовов
  - UI: 14 вызовов
  - Examples: 4 вызова
  - **Итого: 63 вызова мигрированы**

### Fixed
- **ProtoLogger фильтрация не работала** — исправлена логика `ShouldLog()`:
  - Ошибки и предупреждения теперь обходят фильтры категорий и систем
  - Дефолтные категории изменены на `All` (ранее были только Init + Dep)
  - Добавлен `OnValidate` для синхронизации настроек при изменении в инспекторе
- Документация ProtoLogger — исправлены сигнатуры методов в AI_INSTRUCTIONS.md
- ProtoLogger_Migration.md — полностью переписан с актуальным API
- Удалены дубликаты документации SettingsSystem

### Technical Details
- Shortcut методы: `LogInit`, `LogDep`, `LogEvent`, `LogRuntime`
- `LogError` и `LogWarning` принимают только (systemId, message)
- Основной метод: `Log(systemId, category, level, message)`

## [1.8.1] - 2025-01-22

### Added
- **IEventBus** — интерфейс для классов, которые не могут наследоваться от MonoEventBus
  - Позволяет использовать автоматическую подписку/отписку на события
  - Default implementation для SubscribeEvents(), UnsubscribeEvents(), AddEvent()
  - Пример использования: UIWindowBase, который уже наследуется от MonoBehaviour

## [1.8.0] - 2025-01-21

### Added
- **Sound System** — полноценная система управления звуком
  - **SoundManagerSystem** — центральный фасад для воспроизведения звуков и музыки
  - **Provider Pattern** — абстракция бэкенда (Unity/FMOD/Wwise)
  - **Sound Library** — централизованное хранилище звуков с Dictionary-кэшем
  - **Sound Banks** — ленивая загрузка/выгрузка групп звуков для оптимизации памяти
  - **UISoundScheme** — автоматические звуки для UI событий
  - **GameSessionSoundScheme** — автоматическая музыка и звуки для игровых состояний
  - **MusicConfig** — кроссфейд, вертикальное микширование, параметры
  - **Priority System** — отсечение низкоприоритетных звуков при превышении лимита
  - **Cooldown Protection** — защита от спама одинаковых звуков
  
- **Sound Setup Wizard** — полная настройка за один клик
  - Создаёт SoundManagerConfig, SoundLibrary, AudioMixer
  - Генерирует 19 готовых UI звуков (процедурная генерация)
  - Настраивает UISoundScheme с правильными ID
  - ProtoSystem → Sound → Sound Setup Wizard

- **Процедурный генератор звуков** (ProceduralSoundGenerator)
  - 19 UI звуков: window, button, navigation, feedback, controls
  - Генерация в .wav формате без внешних зависимостей
  - Звуки готовы к использованию, можно заменить на свои

- **Компоненты звука**
  - **PlaySoundOn** — универсальный триггер без кода
  - **MusicZone** — зона автоматической смены музыки
  - **AmbientZone** — 3D ambient с fade in/out
  - **SoundEmitter** — для Animator/UnityEvents

- **Editor Tools**
  - **SoundManagerConfigEditor** — информативный редактор с валидацией и статусом
  - **SoundLibraryEditor** — поиск, фильтрация, статистика
  - **UISoundSchemeEditor** — валидация ID, кнопка "Create Missing"
  - **GameSessionSoundSchemeEditor** — аналогичная валидация
  - **SoundBankEditor** — описание и инструкции по использованию
  - **MusicConfigEditor** — настройки адаптивной музыки
  - **SoundMixerGenerator** — генерация AudioMixer из шаблона

- **AudioMixer Template**
  - Шаблон с группами: Master → Music, SFX, Voice, Ambient, UI
  - Exposed параметры для программного управления громкостью
  - Копируется при создании через Wizard

### Technical Details
- SoundManagerSystem наследуется от InitializableSystemBase
- Приоритет инициализации: 12
- Категория в меню компонентов: Core
- Поддержка EventBus: Evt.Sound.Play, PlayMusic, SetVolume и др.

## [1.7.0] - 2025-01-18

### Added
- **GameSessionSystem** — центральная система управления жизненным циклом игровой сессии
  - Состояния: None, Ready, Starting, Playing, Paused, GameOver, Victory
  - Soft reset без перезагрузки сцены через события
  - Гибкая статистика SessionStats с Dictionary для произвольных данных
  - Двуязычные события (EventBus.Session.* / EventBus.Сессия.*)
  - Debug меню через контекстное меню компонента

- **GameSessionNetworkSync** — опциональная сетевая синхронизация (отдельный компонент)
  - Разделение логики и сети: GameSessionSystem работает без Netcode
  - Для мультиплеера: добавить GameSessionNetworkSync на тот же GameObject
  - NetworkVariable синхронизация состояния, причины завершения, флага победы
  - ServerRpc/ClientRpc для команд
  
- **IGameSessionNetworkSync** — интерфейс для сетевой синхронизации (позволяет заменить Netcode на другое решение)

- **IResettable** — интерфейс для систем с поддержкой мягкого сброса
  - Автоматический вызов ResetState() через SystemInitializationManager
  - Событие Session.Reset для ручной подписки
  
- **SystemInitializationManager.ResetAllResettableSystems()** — метод для сброса всех IResettable систем

- **События сессии** (номера 10200-10299):
  - Session.Started / Сессия.Началась
  - Session.Ended / Сессия.Завершена  
  - Session.Reset / Сессия.Сброс
  - Session.Paused / Сессия.Пауза
  - Session.Resumed / Сессия.Продолжена
  - Session.StateChanged / Сессия.Состояние_изменено
  - Session.ReturnedToMenu / Сессия.Возврат_в_меню
  - Session.RestartRequested / Сессия.Запрос_рестарта

- **SessionEndReason enum** — причины завершения сессии (PlayerDeath, MissionComplete, etc.)

- **Editor утилиты**:
  - ProtoSystem → Game Session → Create Config
  - ProtoSystem → Game Session → Select Config

- **ProjectSetupWizard** — кнопка принудительного перезапуска (↻) для выполненных задач

- **Документация**: Documentation~/GameSession.md

### Technical Details
- GameSessionSystem наследуется от InitializableSystemBase (не NetworkBehaviour)
- GameSessionNetworkSync — отдельный NetworkBehaviour компонент
- Паттерн "Факты vs Решения" для координации систем
- Приоритет инициализации: 100

## [1.5.0] - 2025-01-03

### Added
- **UISystem enhancements**
  - **Window Levels** — при открытии окна уровня X закрываются все Normal окна уровня X и выше (Level 0 взаимоисключающие, Level 1+ стековые)
  - **IUISceneInitializer** — интерфейс для добавления переходов и стартовых окон при загрузке сцены
  - **UITimeManager** — счётчик-based управление паузой для нескольких окон одновременно
  - **CursorManagerSystem** — стековое управление состоянием курсора
  - **WindowCursorMode** — атрибут для автоматического управления курсором при открытии окна

- **UIWindowPrefabGenerator improvements**
  - `CreateSettingsSlider()` — слайдер с текстовым полем значения
  - `CreateDropdown()` — полностью корректная структура TMP_Dropdown с Toggle в Item
  - `CreateWindowBase()` overload с кастомной альфой фона
  - Генераторы: GameOver, Statistics
  - Автоматическое присвоение метки "UIWindow" для автосканирования

- **New window classes**
  - `GameOverWindow` — окно победы/поражения с ShowVictory()/ShowDefeat()
  - `StatisticsWindow` — окно статистики с AddStat()/ClearStats()

- **Documentation**
  - `AI_INSTRUCTIONS.md` — комплексные инструкции для ИИ-ассистентов
  - Обновлённый README.md с описанием всех систем

### Changed
- **UINavigator** — интеграция с UITimeManager и CursorManagerSystem
- **SettingsWindow generator** — увеличенные размеры (580×700), непрозрачный фон, улучшенная читаемость
- **CreateDropdown** — исправлена структура Template для совместимости с TMP_Dropdown

### Fixed
- Ошибка "dropdown template is not valid" — добавлен Toggle компонент в Item
- Курсор остаётся видимым после закрытия окна — исправлено через ForceApplyCursorMode
- Переходы не работают после загрузки сцены — добавлена проверка sceneInitializer

## [1.4.0] - 2024-12-29

### Added
- **Автоматическое создание конфигов в инспекторе**
  - Кнопка "🔨 Создать *Config" для пустых полей конфигурации
  - Кнопка "📦 Создать *Container" для пустых полей контейнеров
  - Путь создания: `Assets/<ProjectNamespace>/Settings/<SystemFolder>/`
  - Namespace проекта берётся из файла EventIds
- **InitializableSystemEditor** — кастомный редактор для всех локальных систем
- **NetworkInitializableSystemEditor** — кастомный редактор для сетевых систем
- **ConfigCreationUtility** — утилита для создания ScriptableObject ассетов
- Компоненты ProtoSystem теперь создаются как дочерние объекты SystemInitializationManager

### Changed
- **EffectsManager** переименован в **EffectsManagerSystem** для консистентности именования

### Fixed
- Исправлена ошибка доступа к приватному полю `existingSystemObject` в ProtoSystemComponentsUtility

## [1.3.0] - 2024-12-26

### Added
- **SettingsSystem** — система управления настройками игры
  - Секции настроек: Audio, Video, Controls, Gameplay
  - Автоматическое применение видео настроек (Resolution, Quality, VSync)
  - Поддержка кастомных секций настроек
  - Хранение в INI файле (Desktop) или PlayerPrefs (WebGL/Mobile)
  - Миграция между версиями схемы настроек
  - Интеграция с EventBus для реактивного обновления UI
- События настроек в EventBus (Settings.Audio.*, Settings.Video.*, etc.)
- SettingsConfig ScriptableObject для настройки дефолтов
- Юнит-тесты для системы настроек
- Документация по SettingsSystem

### Technical Details
- События настроек используют номера 10100-10199
- События UI зарезервированы: 10500-10599

## [1.2.0] - 2024-12-XX

### Added
- **EffectsManagerSystem** — система управления VFX/Audio/UI эффектами
  - Пул эффектов с автоматическим переиспользованием
  - Поддержка VFX, Audio, UI и комбинированных эффектов
  - Автоматические триггеры через EventBus
  - UI анимации (Scale, Fade, Slide, Bounce, Rotate)
  - Интеграция с MoreMountains.Tools (опционально)

## [1.0.0] - 2024-01-XX

### Added
- Initial release of ProtoSystem Core package
- EventBus with partial class extension support
- System initialization framework with dependency injection
- Network support for Netcode for GameObjects
- Example systems and usage documentation

### Changed
- Extracted from KM project as reusable UPM package

### Technical Details
- Unity 2021.3+ compatibility
- Netcode for GameObjects 2.4.4 dependency

---

## Системы из коробки

ProtoSystem включает следующие готовые системы (по приоритету инициализации):

| Приоритет | Система | Категория | Описание |
|-----------|---------|-----------|----------|
| 5 | ⚙️ **SettingsSystem** | Core | Настройки игры с персистентностью (INI/PlayerPrefs) |
| 10 | 🖼️ **UISystem** | UI | Управление окнами, навигация, время, курсор |
| 12 | 🔊 **SoundManagerSystem** | Core | Централизованное управление звуком и музыкой |
| 15 | 🎬 **SceneFlowSystem** | Core | Загрузка сцен с переходами и loading screen |
| 20 | ✨ **EffectsManagerSystem** | Effects | Управление визуальными и звуковыми эффектами |
| 25 | 🖱️ **CursorManagerSystem** | UI | Стековое управление курсором |
| 30 | 🌐 **NetworkLobbySystem** | Network | Сетевое лобби для мультиплеера (Netcode) |
| 100 | 🎮 **GameSessionSystem** | Core | Управление жизненным циклом игровой сессии |

### Конфигурация систем

Каждая система использует ScriptableObject для конфигурации:

| Система | Конфиг | Путь по умолчанию |
|---------|--------|-------------------|
| SettingsSystem | `SettingsConfig` | `Assets/<NS>/Settings/Settings/` |
| UISystem | `UISystemConfig` | `Assets/<NS>/Settings/UI/` |
| SoundManagerSystem | `SoundManagerConfig` | `Assets/<NS>/Settings/Sound/` |
| SceneFlowSystem | `SceneFlowConfig` | `Assets/<NS>/Settings/SceneFlow/` |
| EffectsManagerSystem | `EffectContainer` | `Assets/<NS>/Settings/Containers/` |
| CursorManagerSystem | `CursorConfig` | `Assets/<NS>/Settings/Cursor/` |
| NetworkLobbySystem | `NetworkLobbyConfig` | `Assets/<NS>/Settings/NetworkLobby/` |
| GameSessionSystem | `GameSessionConfig` | `Assets/Resources/` |

> `<NS>` — namespace проекта из файла EventIds (например, `KM`)

### UI Окна из коробки

| Окно | Класс | Описание |
|------|-------|----------|
| MainMenu | `MainMenuWindow` | Главное меню (Level 0) |
| GameHUD | `GameHUDWindow` | Игровой HUD (Level 0) |
| PauseMenu | `PauseMenuWindow` | Меню паузы (Level 1) |
| Settings | `SettingsWindow` | Настройки (Level 1) |
| GameOver | `GameOverWindow` | Победа/Поражение (Level 1) |
| Statistics | `StatisticsWindow` | Статистика (Level 1) |
| Credits | `CreditsWindow` | Титры (Level 1) |
| Loading | `LoadingWindow` | Экран загрузки (Overlay) |
