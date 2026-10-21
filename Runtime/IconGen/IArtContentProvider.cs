using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.IconGen
{
    /// <summary>
    /// Провайдер арт-контента: ассет, хранящий сущности (конфиг кампании, база ресурсов,
    /// база вагонов…), сам объявляет, каким его сущностям какой арт нужен. Что-то требует
    /// только иконку, что-то — иконку и превью: каждый вид арта — отдельный сет.
    ///
    /// Runtime-интерфейс без editor-зависимостей: реализуется игровыми SO. Студия иконок
    /// (editor) подключает провайдера через ассет набора (IconCollectionAsset) по Id сета.
    /// </summary>
    public interface IArtContentProvider
    {
        IEnumerable<ArtContentSet> GetArtSets();
    }

    /// <summary>USS-цель публикации: файл окна и его классы, которые оформляет этот элемент.</summary>
    [Serializable]
    public struct UssTarget
    {
        public string File;
        public string[] Classes;
    }

    /// <summary>Один вид арта у провайдера: «иконки зданий», «превью зданий»…</summary>
    public class ArtContentSet
    {
        /// <summary>Стабильный ключ сета ("building_icons") — по нему ссылается ассет набора.</summary>
        public string Id;

        /// <summary>Имя по умолчанию (ассет набора может переопределить).</summary>
        public string Title;

        /// <summary>
        /// Контракт размера: в каком разрешении хранить готовый арт в проекте.
        /// Провайдер знает, где картинки показываются, и декларирует потребность
        /// (обычно 2×–4× от максимального размера показа — под UI-скейл и ретину).
        /// Zero — не задано, ассет набора решает сам.
        /// </summary>
        public Vector2Int PreferredStorageSize;

        /// <summary>
        /// Куда идёт арт сета («окно Game Over», «окно базы: панель ресурсов») —
        /// студия показывает это в шапке секции, чтобы было видно назначение.
        /// </summary>
        public string UsageNote;

        // ── Размеры рендишенов — ОБЩИЕ для набора ──
        // Все сущности набора показываются в одних местах, значит и требуемые размеры
        // одинаковы: список размеров — свойство набора, а не каждой иконки. Спрайт под
        // конкретный размер живёт у сущности (ArtContentItem.GetRenditionSprite).
        // null — набор без рендишенов, студия их UI не показывает.

        /// <summary>Размеры рендишенов набора, px (по возрастанию). null — не поддерживается.</summary>
        public Func<IReadOnlyList<int>> GetRenditionSizes;

        /// <summary>Добавить размер во ВЕСЬ набор (у всех сущностей появляется слот).</summary>
        public Action<int> AddRenditionSize;

        /// <summary>Убрать размер из набора (у всех сущностей).</summary>
        public Action<int> RemoveRenditionSize;

        public List<ArtContentItem> Items = new();
    }

    /// <summary>
    /// Элемент сета: сущность + доступ к её спрайту/промпту. Делегаты создаются при каждом
    /// вызове GetArtSets() и не сериализуются.
    /// </summary>
    public class ArtContentItem
    {
        /// <summary>Ключ истории вариантов ("garage", "preview_garage") — глобально уникален и стабилен.</summary>
        public string EntityId;

        public string DisplayName;
        public Color Accent = new(0.45f, 0.45f, 0.45f);

        /// <summary>
        /// Максимальный размер показа в UI (px): специалист 16×16 в Game Over, ресурс
        /// 26×26 в базе, превью 200×140 в деталях… Информация контракта: студия
        /// показывает её и предупреждает, если хранение меньше показа. Zero — не задано.
        /// </summary>
        public Vector2Int DisplaySize;

        /// <summary>
        /// Где используется ИМЕННО эта сущность — уточнение к UsageNote сета; одна
        /// иконка может жить в нескольких окнах («Game Over + окно базы: строка
        /// специалиста»). Пусто — студия покажет сетовое значение.
        /// </summary>
        public string UsageNote;

        public Func<Sprite> GetSprite;
        public Action<Sprite> SetSprite;
        public Func<string> GetPrompt;
        public Action<string> SetPrompt;

        // ── Размерные варианты (рендишены) — опционально ──
        // Размеры общие для набора (ArtContentSet.GetRenditionSizes); здесь — спрайт
        // ЭТОЙ сущности под конкретный размер. null-колбэки — сущность без рендишенов.

        /// <summary>Спрайт сущности под размер size (null — слот пуст). null-колбэк — не поддерживается.</summary>
        public Func<int, Sprite> GetRenditionSprite;

        /// <summary>Назначить спрайт этой сущности в размер size.</summary>
        public Action<int, Sprite> SetRendition;

        // ── UI-элемент (подложка/рамка/панель) — опционально ──
        // Отличается от иконки тем, что тянется под разный размер через 9-slice, а значит
        // его НЕЛЬЗЯ обрезать до контента (rembg-вырез + trim сдвинут границы рамки).
        // Дефолт (false/Zero/пусто) — обычная иконка, старые сеты не затрагиваются.

        /// <summary>true — это элемент интерфейса: не вырезать фон, не обрезать до контента.</summary>
        public bool IsUiElement;

        /// <summary>
        /// 9-slice рамка в px хранения (L,B,R,T — порядок Unity spriteBorder). Zero — без
        /// 9-slice. Применяется при импорте спрайта (importer.spriteBorder). Точное значение
        /// подбирается вручную под конкретную картинку (студия) — авто-подбор ненадёжен.
        /// </summary>
        public Vector4 SliceBorder;

        /// <summary>Задать 9-slice рамку (ручная настройка в студии). null-колбэк — рамка не редактируется.</summary>
        public Action<Vector4> SetSliceBorder;

        /// <summary>
        /// Целевой PNG для «применить в игру»: активный вариант копируется сюда и на этот
        /// путь уже ссылается USS (background-image + -unity-slice). Пусто — элемент только
        /// копится вариантами, в игру не публикуется.
        /// </summary>
        public string ApplyToPath;

        /// <summary>
        /// USS-файл и классы, чей -unity-slice/цвет синхронизировать при публикации (UI Toolkit
        /// берёт рамку из USS, а НЕ из spriteBorder ассета). Одна панель может оформлять несколько
        /// элементов разного размера. UssClass — основной (для tint/фон/превью), UssClasses — все.
        /// </summary>
        public string UssFile;
        public string UssClass;
        public string[] UssClasses;

        /// <summary>
        /// Дополнительные окна, оформленные тем же элементом: у каждого свой USS-файл и свой
        /// набор классов. Панель базы и панель глобальной карты — один спрайт, но разные файлы,
        /// поэтому одной пары UssFile/UssClasses не хватает.
        /// </summary>
        public UssTarget[] ExtraUssTargets;

        /// <summary>Все USS-цели: основная пара + дополнительные. Публикация идёт по ним.</summary>
        public IEnumerable<UssTarget> AllUssTargets()
        {
            if (!string.IsNullOrEmpty(UssFile) && UssClasses != null)
                yield return new UssTarget { File = UssFile, Classes = UssClasses };

            if (ExtraUssTargets == null) yield break;
            foreach (var t in ExtraUssTargets)
                if (!string.IsNullOrEmpty(t.File) && t.Classes != null)
                    yield return t;
        }

        /// <summary>
        /// Наложение цвета (tint) в игре — хранится у сущности (персист между переключениями),
        /// применяется в USS при публикации. Белый = без изменений (умножение на 1).
        /// </summary>
        public bool TintEnabled;
        public Color TintColor;

        /// <summary>Задать наложение цвета (write-through в провайдера). null-колбэк — не редактируется.</summary>
        public Action<bool, Color> SetTint;

        // Постобработка (обрезка/зеркало/область) хранится ПОФАЙЛОВО у каждого варианта
        // (IconVariant), а не у сущности — это editor-концепция истории вариантов, в контракт
        // провайдера не входит. См. IconVariantStore.SetActiveVariantPost.

        /// <summary>Объект для Undo/SetDirty при назначении спрайта/промпта (обычно сам конфиг).</summary>
        public UnityEngine.Object UndoTarget;
    }
}
