# ProtoLocalization — Система локализации ProtoSystem

## Обзор

ProtoLocalization — тонкий wrapper поверх Unity Localization Package, предоставляющий:
- Простой API в стиле ProtoSystem
- AI-оптимизированный экспорт/импорт переводов
- Интеграцию с EventBus, UISystem, CreditsData, SoundSystem
- Автоматическую локализацию UI без кода

---

## Архитектура

```
ProtoLocalization (wrapper)
├── LocalizationSystem : InitializableSystemBase     ← Точка входа, API
├── Loc (static helper)                              ← Быстрый доступ: Loc.Get("key")
├── LocalizationConfig : ScriptableObject            ← Настройки: языки, fallback, таблицы
├── AI Export/Import                                 ← JSON экспорт для AI-перевода
├── Components                                       ← UI компоненты для автолокализации
│   ├── LocalizeTMP                                  ← Локализация TMP_Text
│   ├── LocalizeImage                                ← Локализация Image (спрайты по языку)
│   └── LocalizeSound                                ← Локализация звуков (озвучка по языку)
└── Editor Tools                                     ← Утилиты редактора
    ├── AI Translation Window                        ← Окно экспорта/импорта для AI
    ├── String Scanner                               ← Поиск нелокализованных строк
    └── Localization Setup Wizard                    ← Первоначальная настройка

Зависимости:
├── com.unity.localization (Unity Localization Package)
├── com.unity.addressables (транзитивная зависимость)
└── com.protosystem.core (EventBus, InitializableSystem, UISystem)
```

---

## Зависимость: Unity Localization

ProtoLocalization **не заменяет** Unity Localization, а оборачивает его:

| Слой | Что делает |
|------|-----------|
| **Unity Localization** | Хранение таблиц, Smart Strings, Locale management, Addressables |
| **ProtoLocalization** | Простой API, EventBus интеграция, AI export/import, UI компоненты |

Данные хранятся в стандартных String Tables / Asset Tables Unity Localization.
ProtoLocalization только упрощает доступ и добавляет инструментарий.

---

## API

### Loc — статический helper

```csharp
// Простой ключ (таблица по умолчанию)
string text = Loc.Get("menu.play");  // → "ИГРАТЬ"

// Явная таблица
string text = Loc.Get("Achievements", "first_blood");  // → "Первая кровь"

// С переменными (Smart String)
string text = Loc.Get("kill.message", 
    ("enemy", "Мутант"), 
    ("count", 5));
// "Вы убили Мутант x5" / "You killed Мутант x5"

// С вложенной локализованной переменной
string text = Loc.Get("achievement.unlocked", 
    ("item", Loc.Ref("Achievements", dynamicKey)));
// dynamicKey = "speed_demon" → "Получено: Демон скорости"

// Проверка наличия ключа
bool exists = Loc.Has("menu.play");

// Текущий язык
string lang = Loc.CurrentLanguage;  // "ru", "en"

// Смена языка
Loc.SetLanguage("en");

// Список доступных языков
IReadOnlyList<string> langs = Loc.AvailableLanguages;
```

### Loc.Ref — ссылка на локализованный ключ

```csharp
// Возвращает LocalizedString, который сам переведётся при смене языка
LocalizedString reference = Loc.Ref("Items", "sword_of_fire");

// Использование в Smart Strings
string msg = Loc.Get("found.item", ("item", Loc.Ref("Items", itemId)));
// RU: "Найден предмет: Меч огня"
// EN: "Item found: Sword of Fire"
```

### LocalizationSystem — InitializableSystemBase

```csharp
public class LocalizationSystem : InitializableSystemBase
{
    public override string SystemId => "localization";
    public override string DisplayName => "Localization System";
    
    // API дублирует Loc.* для доступа через DI
    public string Get(string key);
    public string Get(string table, string key);
    public string Get(string key, params (string name, object value)[] args);
    public void SetLanguage(string languageCode);
    public string CurrentLanguage { get; }
    public IReadOnlyList<string> AvailableLanguages { get; }
}
```

### Инициализация

```csharp
public override async Task<bool> InitializeAsync()
{
    // 1. Инициализация Unity Localization
    // 2. Загрузка LocalizationConfig
    // 3. Определение языка: сохранённый → системный → fallback
    // 4. Загрузка таблиц для текущего языка
    // 5. Публикация Evt.Localization.Ready
    return true;
}
```

---

## События (EventBus)

```csharp
public static class Evt
{
    public static class Localization
    {
        // Язык изменён. Payload: LocaleChangedData { previousLang, newLang }
        public const int LanguageChanged = (int)EventType.LocalizationLanguageChanged;
        
        // Система готова. Payload: null
        public const int Ready = (int)EventType.LocalizationReady;
        
        // Таблица загружена. Payload: string tableName
        public const int TableLoaded = (int)EventType.LocalizationTableLoaded;
    }
}
```

При `LanguageChanged` все UI компоненты автоматически обновляются.

---

## UI Компоненты

### LocalizeTMP

Автоматическая локализация TMP_Text без кода:

```csharp
[RequireComponent(typeof(TMP_Text))]
public class LocalizeTMP : MonoBehaviour, IEventBus
{
    [SerializeField] private string table = "UI";
    [SerializeField] private string key;
    
    // При смене языка — автообновление
    // При старте — автозаполнение из таблицы
}
```

**Инспектор:**
```
┌─ LocalizeTMP ─────────────────┐
│ Table: [UI          ▼]        │
│ Key:   [menu.play    ]        │
│ Preview: "ИГРАТЬ"             │
│ [▶ Все языки]                 │
│   en: "PLAY"                  │
│   ru: "ИГРАТЬ"                │
│   de: "SPIELEN"               │
└───────────────────────────────┘
```

### LocalizeImage

```csharp
[RequireComponent(typeof(Image))]
public class LocalizeImage : MonoBehaviour, IEventBus
{
    [SerializeField] private string table = "Assets";
    [SerializeField] private string key;
    // Загружает спрайт из Asset Table по текущему языку
}
```

### LocalizeSound

```csharp
public class LocalizeSound : MonoBehaviour, IEventBus
{
    [SerializeField] private string table = "Audio";
    [SerializeField] private string key;
    // Загружает AudioClip из Asset Table по языку
}
```

---

## Интеграция с ProtoSystem

### UISystem — автолокализация окон

При `LanguageChanged` — все видимые окна получают событие и обновляют текст.
Окна с `LocalizeTMP` компонентами обновляются автоматически.

Для динамического текста в окнах:

```csharp
[UIWindow("game_over", WindowType.Modal, WindowLayer.Modals)]
public class GameOverWindow : UIWindowBase, IEventBus
{
    [SerializeField] private TMP_Text messageText;
    
    public void InitEvents()
    {
        AddEvent(Evt.Localization.LanguageChanged, _ => UpdateTexts());
    }
    
    private void UpdateTexts()
    {
        messageText.text = Loc.Get("game_over.message", ("score", currentScore));
    }
}
```

### CreditsData

CreditsData получает поддержку локализации через ключи:

```csharp
[System.Serializable]
public class RoleEntry
{
    public string id;           // "dev"
    public string titleKey;     // "credits.role.dev" — ключ локализации
    public string titleFallback; // "Разработка" — если локализация не найдена
}

[System.Serializable]
public class CreditsSection
{
    public string titleKey;      // "credits.section.development"
    public string titleFallback; // "РАЗРАБОТКА"
}
```

`GenerateCreditsText()` пытается `Loc.Get(titleKey)`, при неудаче — `titleFallback`.

### SettingsSystem

Язык сохраняется через SettingsSystem:

```ini
[Localization]
language=ru
```

При изменении языка в настройках — автосохранение.

### SoundSystem

Для озвученных реплик — Asset Table с AudioClip по языкам.
`SoundManagerSystem.Play()` автоматически выбирает клип текущего языка, если запись помечена как локализуемая.

---

## Организация таблиц

### Рекомендуемая структура

| Таблица | Содержимое | Тип |
|---------|-----------|-----|
| `UI` | Кнопки, заголовки, подсказки | String Table |
| `Game` | Игровые строки (урон, события) | String Table |
| `Items` | Названия предметов, описания | String Table |
| `Enemies` | Названия врагов | String Table |
| `Achievements` | Достижения | String Table |
| `Credits` | Титры | String Table |
| `Assets` | Локализованные спрайты | Asset Table |
| `Audio` | Локализованная озвучка | Asset Table |

### Именование ключей

```
# Формат: section.element[.modifier]

# UI
menu.play
menu.settings
menu.quit
settings.volume.master
settings.volume.sfx

# Game
game.stage.1
game.boss.defeated
game.evacuation.timer

# Items
item.weapon.railgun.name
item.weapon.railgun.description

# Credits
credits.role.dev
credits.section.development
credits.section.thanks
```

---

## AI Translation — Экспорт/Импорт

### Формат экспорта

По файлу на пару таблица×язык: `<Table>_<src>_to_<tgt>.json` (пишет `LocalizationExporter`):

```json
{
  "sourceLanguage": "ru",
  "targetLanguage": "de",
  "table": "UI",
  "exported": "2026-03-17T14:00:57Z",
  "projectName": "Last Convoy",
  "instructions": "Translate from ru to de. Preserve {{variables}} in curly braces exactly as-is. Respect maxLength if > 0. Fill 'translation' field for each entry. Keep the same key.",
  "entries": [
    {
      "key": "lc.community.cat_announcement",
      "source": "новость",
      "context": "",
      "maxLength": 0,
      "tags": [],
      "pluralForm": "",
      "translation": ""
    }
  ]
}
```

`context`, `maxLength`, `tags`, `pluralForm` берутся из `StringMetadataDatabase` (если назначена).

### Формат импорта

Тот же файл с заполненными полями `translation` — кладётся в папку `Import/`.
`LocalizationImporter` записывает значения в StringTable целевого языка:
пустые `translation` пропускаются, существующие переводы не перезаписываются
без флага **Overwrite Existing**.
### AI Translation Window (Editor)

Окно `ProtoSystem → Localization → AI Translation`. Четыре вкладки (на скетче открыта Claude):

```
┌─ 🌐 AI Translation ────────────────────────────────────────┐
│ Экспорт строк, импорт результата и полный цикл через Claude│
│ [📤 Export] [📥 Import] [✅ Validate] [🤖 Claude]          │
│ Config: [LocalizationConfig] [StringMetadataDatabase]      │
│                                                            │
│ ┌ Объём перевода ─────────────────────────────────────┐   │
│ │ Tables:  ☑ UI   ☑ Game                              │   │
│ │ Source Language: [Русский (ru) ▼]      [Все][Ничего]│   │
│ │ ☑ English (en)        ☑ Français (fr)               │   │
│ │ ☑ Deutsch (de)        ☑ Español (es)                │   │
│ └─────────────────────────────────────────────────────┘   │
│ ┌ Параметры ──────────────────────────────────────────┐   │
│ │ Only Missing ☑   Overwrite Existing ☐               │   │
│ │ Export Folder: Assets/.../Localization/Export  [...]│   │
│ └─────────────────────────────────────────────────────┘   │
│ ┌ Скилл проекта ──────────────────────────────────────┐   │
│ │ [✓] .claude/skills/localize/SKILL.md                │   │
│ │ [📝 Перегенерировать (Claude)]  [Открыть]           │   │
│ └─────────────────────────────────────────────────────┘   │
│ [        🤖 Translate via Claude        ]                  │
│ [+120] [skip 4]  Готово: файлов 8, импортировано 120...   │
│ Log: UI_ru_to_de.json — 30 translated ...                 │
│                                                            │
│ ▼ Покрытие переводов                              [↻]     │
│   UI                                                       │
│   en  ████████████████████░░  38/42                        │
│   de  ██████████░░░░░░░░░░░░  20/42                        │
│   Game                                                     │
│   en  ████░░░░░░░░░░░░░░░░░░   4/18                        │
└────────────────────────────────────────────────────────────┘
```

- **Export** — таблица + целевые языки, Only Missing, экспорт в JSON по файлу на язык.
- **Import** — список файлов со сводкой «N/M переведено», Validate First, Overwrite Existing.
- **Validate** — проверка файла: цветные бейджи err/warn по каждой строке.
- **Claude** — полный цикл (см. ниже) + генерация проектного скилла.
- **Покрытие переводов** — прогресс по каждой таблице×языку, пересчёт по кнопке ↻
  и автоматически после импорта.

### Процесс AI-перевода

```
1. Editor: Экспорт JSON (фильтр по таблицам и статусу)
      ↓
2. Отдать JSON в Claude / GPT / DeepL API
      ↓
3. Получить JSON с заполненными translated
      ↓
4. Editor: Импорт JSON → валидация (max_length, переменные, теги)
      ↓
5. Автоматическое обновление String Tables
      ↓
6. Preview в редакторе (переключить язык → проверить)
```

### Полный цикл одной кнопкой — вкладка Claude

Вкладка **🤖 Claude** в AI Translation Window (`ClaudeTranslationRunner`) выполняет весь
процесс автоматически: Export выбранных таблиц/языков → headless-запуск Claude Code
(`claude -p --permission-mode acceptEdits`, промпт через stdin) → Import + валидация.

Требования:
- Установленный [Claude Code CLI](https://claude.com/claude-code) (команда `claude` в PATH),
  залогиненный в аккаунт.

Кастомизация под проект (опционально):
- Если в проекте есть скилл `.claude/skills/localize/SKILL.md` — раннер вызывает `/localize`
  с путями Export/Import, и контракт перевода (тон, глоссарий, правила) берётся из скилла.
- Без скилла используется встроенный проектно-независимый промпт (сохранение `{переменных}`,
  TMP-тегов, maxLength, контекста).
- Кнопка **«Создать скилл на основе проекта»** генерирует SKILL.md автоматически:
  headless-Claude читает доки проекта и исходные строки таблиц, определяет тон игры
  и собирает глоссарий повторяющихся терминов. Скилл можно перегенерировать или
  править вручную — кнопка «Открыть».

### Валидация при импорте

| Проверка | Описание |
|----------|----------|
| max_length | Перевод не длиннее указанного |
| variables | Все `{переменные}` из source присутствуют в translated |
| rich_text | TMP теги (`<color>`, `<b>`) сохранены или корректно заменены |
| empty | Нет пустых переводов |
| newlines | `\n` сохранены где нужно |
| duplicates | Нет дублированных ключей |

---

## Метаданные строк

Unity Localization поддерживает метаданные на записях таблиц. ProtoLocalization добавляет кастомные:

```csharp
[System.Serializable]
public class ProtoStringMetadata : SharedTableEntryMetadata
{
    public string context;       // Описание для переводчика
    public int maxLength;        // Максимальная длина перевода
    public string[] tags;        // Теги для фильтрации
    public string[] variables;   // Список переменных в Smart String
    public string screenshot;    // Путь к скриншоту (опционально)
}
```

---

## Конфигурация

### LocalizationConfig (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "ProtoSystem/Localization Config")]
public class LocalizationConfig : ScriptableObject
{
    [Header("Languages")]
    public string defaultLanguage = "ru";
    public string fallbackLanguage = "en";
    public List<LanguageEntry> supportedLanguages = new()
    {
        new() { code = "ru", displayName = "Русский", isSource = true },
        new() { code = "en", displayName = "English" },
    };
    
    [Header("Tables")]
    public string defaultStringTable = "UI";
    public List<string> preloadTables = new() { "UI", "Game" };
    
    [Header("Behavior")]
    public bool autoDetectSystemLanguage = true;
    public bool logMissingKeys = true;
    public string missingKeyFormat = "[{key}]"; // Что показывать для отсутствующих ключей
    
    [Header("AI Export")]
    public string exportPath = "Localization/Export";
    public bool includeContext = true;
    public bool includeMaxLength = true;
    public bool includeTags = true;
}

[System.Serializable]
public class LanguageEntry
{
    public string code;          // "ru", "en", "de"
    public string displayName;   // "Русский", "English"
    public bool isSource;        // Исходный язык (для экспорта)
    public TMP_FontAsset font;   // Шрифт для языка (опционально, для CJK)
}
```

---

## Поддержка шрифтов

Для CJK (китайский, японский, корейский) и других языков с нестандартными символами:

```csharp
// При смене языка LocalizationSystem проверяет LanguageEntry.font
// Если задан — обновляет fallback font в TMP Settings
// или публикует событие для UI компонентов
```

---

## IResettable

LocalizationSystem реализует IResettable:

```csharp
public void ResetState()
{
    // Сброс языка к значению из настроек/конфига
    // Перезагрузка таблиц не нужна — они кешированы Unity Localization
}
```

---

## Файловая структура

### В пакете ProtoSystem

```
com.protosystem.core/
├── Runtime/
│   └── Localization/
│       ├── LocalizationSystem.cs        # Система
│       ├── Loc.cs                       # Статический helper
│       ├── LocalizationConfig.cs        # Конфигурация
│       ├── LocalizationEvents.cs        # События EventBus
│       ├── StringMetadata.cs            # Метаданные для AI (context/maxLength/tags)
│       ├── PluralRules.cs               # Плюральные формы (CLDR)
│       ├── UILocalizationKeys.cs        # Ключи встроенного UI
│       ├── LocalizeTMP.cs               # Компонент для TMP_Text
│       └── LocalizeTMPSwitch.cs         # Переключатель вариантов TMP
├── Editor/
│   ├── Common/
│   │   └── ProtoEditorStyles.cs         # Общие стили editor-окон ProtoSystem
│   └── Localization/
│       ├── AITranslationWindow.cs       # Окно AI Translation (+вкладка Claude)
│       ├── LocalizationExporter.cs      # Exporter + Importer + Validator
│       ├── LocalizationExportData.cs    # JSON-модели экспорта/импорта
│       ├── ClaudeTranslationRunner.cs   # Полный цикл через Claude Code CLI
│       ├── LocalizationSetupWizard.cs   # Wizard первоначальной настройки
│       ├── LocalizationConfigEditor.cs  # Инспектор конфига
│       └── LocalizeTMPEditor.cs         # Кастомный инспектор LocalizeTMP
└── Documentation~/
    ├── Localization.md                  # Этот документ
    └── LiveOps_ServerContract.md        # Контракт LiveOps клиент↔сервер
```

### В проекте Last Convoy

```
Assets/LastConvoy/
├── Localization/
│   ├── Tables/
│   │   ├── UI/                          # String Table Collection "UI"
│   │   │   ├── UI.asset
│   │   │   ├── UI_ru.asset
│   │   │   └── UI_en.asset
│   │   ├── Game/
│   │   ├── Credits/
│   │   └── Items/
│   ├── Locales/
│   │   ├── ru.asset                     # Locale
│   │   └── en.asset
│   ├── Export/                          # JSON файлы для AI-перевода
│   │   ├── UI_ru_to_en.json
│   │   └── Credits_ru_to_en.json
│   └── Config/
│       └── LocalizationConfig.asset
```

---

## Статус реализации

Ядро (LocalizationSystem, Loc, LocalizeTMP, Setup Wizard), AI Translation
(экспорт/импорт/валидация, метаданные) и интеграции (Settings, шрифты) — реализованы.

Инструменты:
- [x] `LocalizeTMPEditor` — кастомный инспектор компонента
- [x] Статистика покрытия переводов — панель Coverage в AI Translation Window
- [x] Полный AI-цикл одной кнопкой — `ClaudeTranslationRunner` (вкладка Claude)
- [ ] `StringScanner` — поиск нелокализованных строк (не реализован)
- [ ] Batch-операции «ключ во все таблицы сразу» (не реализованы)

---

## Пример полного workflow

### 1. Настройка (один раз)

```
ProtoSystem → Localization → Setup Wizard
  → Создаёт LocalizationConfig
  → Создаёт Locale (ru, en)
  → Создаёт String Table Collection "UI"
  → Добавляет LocalizationSystem на сцену
```

### 2. Добавление строк

В Unity Localization Table Window:
```
UI Table:
  menu.play        | ru: "ИГРАТЬ"        | en: ""
  menu.settings    | ru: "НАСТРОЙКИ"     | en: ""
  menu.quit        | ru: "ВЫХОД"         | en: ""
```

Или через код:
```csharp
// Editor-only утилита
ProtoLocalizationEditor.AddEntry("UI", "menu.play", "ИГРАТЬ", 
    context: "Кнопка главного меню", maxLength: 15);
```

### 3. Использование в UI

На TMP_Text компоненте добавить `LocalizeTMP`:
```
Table: UI
Key: menu.play
```

Или в коде:
```csharp
titleText.text = Loc.Get("menu.play");
```

### 4. AI-перевод

Одной кнопкой (нужен Claude Code CLI):
```
ProtoSystem → Localization → AI Translation → вкладка Claude
  → Выбрать таблицы и языки → 🤖 Translate via Claude
  → Export → перевод → Import + валидация — автоматически
```

Или вручную:
```
Вкладка Export → JSON → отдать AI → заполненный JSON в Import/
  → вкладка Import → валидация ✓
```

### 5. Проверка

В настройках игры → сменить язык → все тексты обновились автоматически.

---

## Принятые решения

### Plural forms → Simplified (отдельные ключи)

Вместо ICU Smart Strings используем отдельные ключи на каждую форму:

```
"enemies.killed.one"   = "убит {count} враг"
"enemies.killed.few"   = "убито {count} врага"
"enemies.killed.other" = "убито {count} врагов"
```

Helper в Loc:
```csharp
// Автоматически выбирает форму по count
string text = Loc.GetPlural("enemies.killed", count);

// Внутри: определяет суффикс (.one, .few, .other) по правилам языка
// Используется System.Globalization для plural rules
```

**Причина:** AI-перевод. Claude надёжно переводит простые строки, но регулярно ломает вложенный ICU синтаксис `{plural:one{...}few{...}}`. Три простых ключа — три корректных перевода.

### Редактор таблиц → Стандартный Unity + утилиты

Используем стандартный Unity Localization Table Window для просмотра/редактирования.

Дополнительно в ProtoSystem:
- AI Translation Window — экспорт/импорт (уже в плане)
- String Scanner — поиск нелокализованных строк
- Batch Add Keys — меню для массового добавления ключей

**Причина:** свой полноценный редактор таблиц — scope creep. Стандартное окно Unity покрывает 90% задач.

### Локализация ScriptableObject → Ключи в данных (Вариант B + C)

**Вариант B** — для уникальных SO (CreditsData, конфиги):
```csharp
public class CreditsSection
{
    public string titleKey;      // "credits.section.development"
    public string titleFallback; // "РАЗРАБОТКА"
}
```

**Вариант C** — для массовых однотипных SO (оружие, враги, предметы):
```csharp
public class WeaponData : ScriptableObject
{
    public string id = "railgun";  // Ключ = "weapon.{id}.name"
    public string nameFallback = "Рельсотрон";
    
    public string GetName() => Loc.Get($"weapon.{id}.name", nameFallback);
    public string GetDesc() => Loc.Get($"weapon.{id}.desc");
}
```

**Причина:** Вариант B — явный, совместим с текущей архитектурой. Вариант C — экономит поля для 20+ однотипных предметов. Asset Tables (`LocalizedString`) избыточны — раздувают инспектор и усложняют AI-экспорт.

### Hot reload → Да

Строки обновляются в редакторе без перезапуска Play Mode. При изменении таблицы в Unity Localization Table Window → `LocalizationSystem` ловит событие → публикует `Evt.Localization.LanguageChanged` → все `LocalizeTMP` компоненты обновляются.

---

## Открытые вопросы

*Все ключевые решения приняты. Мелкие детали уточняются по ходу реализации.*
