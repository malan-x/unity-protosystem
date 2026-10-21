using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Внутренняя модель набора для студии: разрешённый снимок IconCollectionAsset
    /// (сущности от провайдера + настройки из ассета). Строится студией из ассетов
    /// конфига (IconStudioConfig) при пересборке вкладок; сам по себе не сериализуется.
    /// </summary>
    public class IconCollection
    {
        /// <summary>Стабильный id набора (setId провайдера) — ключ настроек.</summary>
        public string Id;

        /// <summary>Название для вкладки студии.</summary>
        public string Title;

        /// <summary>В каком размере хранится результат в проекте (256×256, 256×160…).</summary>
        public Vector2Int StorageSize = new(256, 256);

        /// <summary>В каком размере рендерит генератор (1024×1024, 1024×640…).</summary>
        public Vector2Int RenderSize = new(1024, 1024);

        /// <summary>Корневая папка PNG вариантов этого набора.</summary>
        public string PngFolder;

        /// <summary>Куда идёт арт набора (для шапки секции). Пусто — не задано.</summary>
        public string UsageNote;

        /// <summary>Не удалять фон после генерации (референсы image-to-3D)</summary>
        public bool KeepBackground;

        // Рендишены — размеры общие для набора (null — набор без рендишенов)
        public Func<System.Collections.Generic.IReadOnlyList<int>> GetRenditionSizes;
        public Action<int> AddRenditionSize;
        public Action<int> RemoveRenditionSize;

        /// <summary>Элементы набора (живой список — вызывается при каждой пересборке грида).</summary>
        public Func<List<IconItem>> GetItems;

        /// <summary>Ассет-источник: профиль генерации, настройки виджета, сериализация.</summary>
        public IconCollectionAsset SourceAsset;

        public static IconCollection FromAsset(IconCollectionAsset asset)
        {
            if (asset == null) return null;
            return new IconCollection
            {
                Id = asset.setId,
                Title = asset.ResolveTitle(),
                StorageSize = asset.ResolveStorageSize(),   // контракт: ассет либо провайдер
                RenderSize = asset.renderSize,
                PngFolder = asset.pngFolder,
                KeepBackground = asset.keepBackground,
                UsageNote = asset.ResolveSet()?.UsageNote ?? "",
                GetRenditionSizes = asset.ResolveSet()?.GetRenditionSizes,
                AddRenditionSize = asset.ResolveSet()?.AddRenditionSize,
                RemoveRenditionSize = asset.ResolveSet()?.RemoveRenditionSize,
                GetItems = asset.ResolveItems,
                SourceAsset = asset,
            };
        }
    }

    /// <summary>Один элемент набора: сущность + доступ к её спрайту и промпту через колбэки.</summary>
    public class IconItem
    {
        /// <summary>Ключ истории вариантов ("garage", "preview_garage") — уникален глобально.</summary>
        public string EntityId;

        public string DisplayName;
        public Color Accent = new(0.45f, 0.45f, 0.45f);

        /// <summary>Максимальный размер показа в UI (контракт провайдера). Zero — не задан.</summary>
        public Vector2Int DisplaySize;

        /// <summary>Где используется сущность (может отличаться от сетового). Пусто — не задано.</summary>
        public string UsageNote;

        public Func<Sprite> GetSprite;
        public Action<Sprite> SetSprite;
        public Func<string> GetPrompt;
        public Action<string> SetPrompt;

        // Рендишены: размеры общие для набора, здесь — спрайт сущности под размер
        // (null-колбэк — сущность без рендишенов, UI не показывается)
        public Func<int, Sprite> GetRenditionSprite;
        public Action<int, Sprite> SetRendition;

        // UI-элемент (подложка/рамка): не резать фон, 9-slice рамка, публикация по пути USS.
        // Дефолт — обычная иконка.
        public bool IsUiElement;
        public Vector4 SliceBorder;
        public Action<Vector4> SetSliceBorder;
        public string ApplyToPath;
        public string UssFile;
        public string UssClass;
        public string[] UssClasses;
        /// <summary>Другие окна на том же спрайте: свой USS-файл и свои классы.</summary>
        public UssTarget[] ExtraUssTargets;
        public bool TintEnabled;
        public Color TintColor;
        public Action<bool, Color> SetTint;

        /// <summary>Объект для Undo/SetDirty при назначении спрайта/промпта.</summary>
        public UnityEngine.Object UndoTarget;

        /// <summary>Все USS-цели: основная пара + дополнительные окна.</summary>
        public IEnumerable<UssTarget> AllUssTargets()
        {
            if (!string.IsNullOrEmpty(UssFile) && UssClasses != null)
                yield return new UssTarget { File = UssFile, Classes = UssClasses };

            if (ExtraUssTargets == null) yield break;
            foreach (var t in ExtraUssTargets)
                if (!string.IsNullOrEmpty(t.File) && t.Classes != null)
                    yield return t;
        }
    }
}
