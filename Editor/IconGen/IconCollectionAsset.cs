using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProtoSystem.IconGen;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Набор студии иконок как АССЕТ: все настройки — данные (размеры, папка, профиль
    /// генерации, виджет «как в игре»), а сущности приходят от провайдера — ассета игры,
    /// реализующего IArtContentProvider (напр. CampaignConfig), по стабильному Id сета.
    ///
    /// Так наборы описываются в инспекторе без кода: «иконки зданий», «превью зданий»,
    /// потом «иконки ресурсов» — новый ассет + (при новом типе сущностей) реализация
    /// провайдера на их хранилище.
    /// </summary>
    [CreateAssetMenu(fileName = "IconCollection", menuName = "ProtoSystem/Icon Studio/Collection")]
    public class IconCollectionAsset : ScriptableObject
    {
        [Header("Источник сущностей")]
        [Tooltip("Ассет-провайдер (реализует IArtContentProvider): конфиг кампании, база ресурсов…")]
        public UnityEngine.Object provider;

        [Tooltip("Id сета у провайдера (building_icons, building_previews…). " +
                 "Список доступных — в подсказке ниже.")]
        public string setId;

        [Header("Отображение")]
        [Tooltip("Название вкладки. Пусто — берётся из сета провайдера.")]
        public string title;

        [Header("Генерация")]
        [Tooltip("Профиль генерации этого набора (стиль + модель + LoRA).")]
        public IconGenProfile profile;

        [Tooltip("В каком размере результат хранится в проекте. Zero — взять из контракта " +
                 "провайдера (PreferredStorageSize сета); значение тут — осознанный override.")]
        public Vector2Int storageSize = Vector2Int.zero;

        [Tooltip("В каком размере рендерит генератор (SDXL любит 1024 по большей стороне).")]
        public Vector2Int renderSize = new(1024, 1024);

        [Tooltip("Корневая папка PNG вариантов этого набора.")]
        public string pngFolder = "Assets/Art/Variants";

        [Header("Виджет «как в игре» (примерка в пикере)")]
        [Tooltip("РЕАЛЬНЫЙ StyleSheet игры (напр. BaseWindow.uss) — мок не разъезжается с игрой.")]
        public StyleSheet widgetUss;

        [Tooltip("USS-класс контейнера виджета (напр. building-card).")]
        public string widgetCardClass;

        [Tooltip("USS-класс элемента с картинкой (напр. card-icon / card-preview).")]
        public string widgetImageClass;

        [Tooltip("USS-класс подписи (напр. card-title). Пусто — без подписи.")]
        public string widgetTitleClass;

        [Tooltip("Тонировать рамку картинки цветом категории сущности (как делает код игры).")]
        public bool tintFrameByAccent;

        [Tooltip("НЕ удалять фон rembg'ом после генерации. Для референсов image-to-3D " +
                 "фон нужен: модель собирается по цельной картинке, а вырезанный " +
                 "силуэт теряет объём и освещение")]
        public bool keepBackground;

        /// <summary>Провайдер, если назначен корректно.</summary>
        public IArtContentProvider Provider => provider as IArtContentProvider;

        /// <summary>Сет провайдера по setId (null — не найден/не назначен).</summary>
        public ArtContentSet ResolveSet()
        {
            var p = Provider;
            if (p == null || string.IsNullOrEmpty(setId)) return null;
            foreach (var s in p.GetArtSets())
                if (s != null && s.Id == setId) return s;
            return null;
        }

        public string ResolveTitle()
        {
            if (!string.IsNullOrEmpty(title)) return title;
            var set = ResolveSet();
            return set != null && !string.IsNullOrEmpty(set.Title) ? set.Title : name;
        }

        /// <summary>
        /// Размер хранения по контракту: явный storageSize ассета, иначе рекомендация
        /// провайдера (PreferredStorageSize сета), иначе 256×256.
        /// </summary>
        public Vector2Int ResolveStorageSize()
        {
            if (storageSize.x > 0 && storageSize.y > 0) return storageSize;
            var set = ResolveSet();
            if (set != null && set.PreferredStorageSize.x > 0 && set.PreferredStorageSize.y > 0)
                return set.PreferredStorageSize;
            return new Vector2Int(256, 256);
        }

        /// <summary>Элементы набора в internal-модели студии.</summary>
        public List<IconItem> ResolveItems()
        {
            var result = new List<IconItem>();
            var set = ResolveSet();
            if (set == null) return result;

            foreach (var a in set.Items)
            {
                if (a == null) continue;
                result.Add(new IconItem
                {
                    EntityId = a.EntityId,
                    DisplayName = a.DisplayName,
                    Accent = a.Accent,
                    DisplaySize = a.DisplaySize,
                    // Пусто у сущности — наследуется назначение сета
                    UsageNote = string.IsNullOrEmpty(a.UsageNote) ? set.UsageNote : a.UsageNote,
                    GetSprite = a.GetSprite,
                    SetSprite = a.SetSprite,
                    GetPrompt = a.GetPrompt,
                    SetPrompt = a.SetPrompt,
                    GetRenditionSprite = a.GetRenditionSprite,
                    SetRendition = a.SetRendition,
                    IsUiElement = a.IsUiElement,
                    SliceBorder = a.SliceBorder,
                    SetSliceBorder = a.SetSliceBorder,
                    ApplyToPath = a.ApplyToPath,
                    UssFile = a.UssFile,
                    UssClass = a.UssClass,
                    UssClasses = a.UssClasses,
                    ExtraUssTargets = a.ExtraUssTargets,
                    TintEnabled = a.TintEnabled,
                    TintColor = a.TintColor,
                    SetTint = a.SetTint,
                    UndoTarget = a.UndoTarget,
                });
            }
            return result;
        }

        /// <summary>Мок виджета из настроек ассета (реальный USS + классы). null — не настроен.</summary>
        public VisualElement BuildWidgetPreview(Sprite sprite, IconItem item)
            => WidgetPreviewBuilder.Build(this, sprite, item);
    }
}
