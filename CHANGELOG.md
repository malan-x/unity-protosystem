# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
  - **Window Levels** — Level 0 окна взаимоисключающие (MainMenu, GameHUD), Level 1+ стековые
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
