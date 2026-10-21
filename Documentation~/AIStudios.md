# ИИ-студии ProtoSystem

Четыре редакторных инструмента для производства игрового контента генеративными моделями.
Все живут в пакете и не знают типов игры: игра сама объявляет, какой контент ей нужен
(провайдеры), а студия генерирует варианты, хранит историю с рецептами и публикует
выбранный вариант обратно в игровые данные.

| Студия | Меню | Тулбар | Модули |
|---|---|---|---|
| ИИ Арт студия — иконки, превью, UI-элементы 9-slice | ProtoSystem → ИИ студии → ИИ Арт студия | 🎨 Арт | `Runtime/IconGen`, `Editor/IconGen` |
| ИИ Аудио студия — SFX, эмбиенты, музыка, озвучка | ProtoSystem → ИИ студии → ИИ Аудио студия | 🎵 Звук | `Runtime/AudioGen`, `Editor/AudioGen` |
| ИИ 3D студия — image→3D, риггинг и анимации, спрайт-листы | ProtoSystem → ИИ студии → ИИ 3D студия | 🧊 3D | `Runtime/ModelGen`, `Editor/ModelGen` |
| Генератор спрайтов — рендер 3D-моделей в листы по ракурсам | ProtoSystem → ИИ студии → Генератор спрайтов | 🎞 Спрайты | `Runtime/SpriteGen`, `Editor/SpriteGen` |

Кнопки главного тулбара (Unity 6000.7+) появляются **после перезапуска редактора**: набор
элементов тулбара Unity собирает при старте. Пакет включает их один раз (`ToolbarVisibility`),
дальше видимостью управляет пользователь.

Подробно по студиям: `AIArtStudio.md`, `AIAudioStudio.md`.

## Что нужно снаружи

- **ComfyUI** (локально) — генерация арта, звука (Stable Audio Open, ACE-Step) и 3D (Hunyuan3D).
  Адрес сервера и .bat запуска — Project Settings ▸ AI Icon Tools / AI Audio Tools / AI Model Tools.
- **ffmpeg** в PATH — для звука (ComfyUI отдаёт FLAC, Unity его не читает).
- **Ключи облачных движков** — только в EditorPrefs, не в репозитории: ElevenLabs
  (`ProtoAudio.ElevenLabsKey`), Meshy (`ProtoModel.MeshyApiKey`), PixelLab.
- **glTFast** (`com.unity.cloud.gltfast`) — импорт GLB моделей 3D-студии.
- **URP** — желателен для генератора спрайтов: его шейдеры написаны под URP. Без URP модуль
  компилируется (компоненты URP и SubShader'ы пропускаются), но стили рендера не работают.
- **Input System** не обязателен: превью-сцена генератора спрайтов читает старый Input Manager,
  если пакета нет.

## Подключение в новом проекте

### Арт

1. Реализовать `ProtoSystem.IconGen.IArtContentProvider` на ScriptableObject игры (обычно
   partial-файл `*.ArtContent.cs` под `#if UNITY_EDITOR`): `GetArtSets()` отдаёт наборы
   `ArtContentSet` с сущностями `ArtContentItem` — id, размеры показа/хранения, колбэки
   `GetSprite/SetSprite`, `GetPrompt/SetPrompt`. `EntityId` — ключ истории вариантов, менять нельзя.
2. Создать ассет набора: Create → ProtoSystem → Icon Studio → Collection (провайдер + setId +
   профиль генерации + папка PNG). Без ассета набор в студии **не виден**, даже если провайдер
   его отдаёт.
3. Создать Studio Config (Create → ProtoSystem → Icon Studio → Studio Config) и разложить
   наборы и группы по вкладкам.
4. Стили и профили: Create → ProtoSystem → Icon Style Preset / Icon Gen Profile.

Библиотека вариантов (`IconVariantLibrary`) ищется по типу; если её нет — создаётся
в `Assets/AIStudio/Icons/`. Папки паков и референсов стиля — поля библиотеки.

### Звук

1. Реализовать `ProtoSystem.AudioGen.IAudioContentProvider` (обычно поверх `SoundLibrary`:
   сеты по префиксам id, публикация пишет `SoundEntry.clip`).
2. Create → ProtoSystem → Audio Studio → Collection / Group, плюс `AudioStudioConfig`.
3. Профили: Create → ProtoSystem → Audio Gen Profile.

`SoundLibrary` и `AudioVariantLibrary` ищутся по типу (первые по пути). Библиотека вариантов
без ассета создаётся в `Assets/AIStudio/Audio/`, паки — в папке `Packs` рядом с ней.

### 3D

1. ScriptableObject игры реализует `ProtoSystem.ModelGen.IModelContentProvider`
   (`GetModelSets()` → сущности с промптом, референсом, папками публикации и слотом
   `targetAsset.targetField` для готового префаба). Студия находит такие SO по всему `Assets/`.
2. Референсы удобно брать из Арт студии: тот же SO может реализовать и `IArtContentProvider`.

`ModelVariantLibrary` ищется по типу, без ассета создаётся в `Assets/AIStudio/Models/`.
Варианты лежат в `Variants` рядом с ней; папки листов, референсов, префабов и публикации
по умолчанию — поля библиотеки (`sheetsFolder`, `refsFolder`, `prefabsFolder`, `publishFolder`).

### Генератор спрайтов

`SpriteGenConfig` (Create → ProtoSystem → SpriteGen → Config) на модель; конфиги, вывод и
превью-сцена — в `Assets/SpriteGen/`. Из 3D-студии открывается кнопкой «Спрайты…» у
опубликованной модели. Рантайм-сборка модуля существует только в редакторе
(`defineConstraints: UNITY_EDITOR`) и в билд игры не попадает.

## Точки расширения

Проект регистрирует свои надстройки из одного `[InitializeOnLoad]`-класса
(пример — `AIStudioHooks` в Last Convoy):

```csharp
[InitializeOnLoad]
static class MyStudioHooks
{
    static MyStudioHooks()
    {
        // Арт: в Play Mode перерисовать игровой UI после смены варианта/пака
        IconStudioExtensions.PlayModeArtChanged += () => EventBus.Publish(MyEvents.IconsChanged, null);
        // Арт: свои пункты меню «⋯» и блоки в панели рендишенов набора
        IconStudioExtensions.MoreMenuItems.Add(("Переключатель паков (Play Mode)", MySwitcher.Show));
        IconStudioExtensions.RenditionPanelExtras.Add(col => col.SourceAsset?.provider is MyLib lib ? BuildRow(lib) : null);

        // Звук: игровые лупы, которые держат свою громкость; фильтр «Соло» для прямых PlayOneShot
        AudioStudioExtensions.PlayModeVolumeChanged += MyLoopPlayer.ApplyBaseVolume;
        AudioStudioExtensions.SoloFilterChanged += filter => MySoloGate.Filter = filter;
        AudioStudioExtensions.UsageScanExcludes.Add("/Settings/Sound/VoiceLines.asset");
        AudioStudioExtensions.DynamicIdPrefixes.Add("bark_");   // id, собираемые в коде динамически

        // 3D: IMGUI-блок под промптом сущности; куда отдать анимированный спрайт-лист
        ModelStudioExtensions.EntityDetailsGUI.Add(item => { /* кнопки проекта */ });
        ModelStudioExtensions.AnimatedSheetRendered.Add((item, sheet, clips) => "Атлас назначен");
    }
}
```

Списки и события статические и обнуляются при domain reload — повторной регистрации не будет.

## Грабли

- **SO-класс — только в одноимённом файле.** Второй класс в чужом файле после перезапуска
  редактора теряет `m_Script`, ассеты грузятся пустыми.
- **Ассеты наборов создавать с уникальным GUID** (Create-меню или Instantiate + CreateAsset),
  не копией .asset вместе с .meta.
- **Поиск использований звука** читает файлы в фоне — никогда не ждать его блокирующе на
  главном потоке (дедлок редактора).
- **EditorPrefs нельзя читать из Task.Run** — значения снимать на главном потоке до запуска фона.
