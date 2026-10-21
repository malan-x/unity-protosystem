using System;
using System.Collections.Generic;

namespace ProtoSystem.ModelGen
{
    /// <summary>
    /// Контракт ИИ 3D-студии: игровой SO сам объявляет, какие 3D-модели ему нужны.
    /// Зеркало IArtContentProvider/IAudioContentProvider. Модели — промежуточное сырьё
    /// для 2D-пайплайна (рендер в спрайты), поэтому публикация — путь к ассету модели.
    /// </summary>
    public interface IModelContentProvider
    {
        List<ModelContentSet> GetModelSets();
    }

    /// <summary>Набор моделей («Мобы», «Состав», «Объекты карты»).</summary>
    public class ModelContentSet
    {
        public string Id;
        public string Title;
        /// <summary>Описание набора на понятном языке: где эти модели используются.</summary>
        public string UsageNote;
        public List<ModelContentItem> Items = new();
    }

    /// <summary>Сущность-модель + колбэки чтения/записи в игровые данные.</summary>
    public class ModelContentItem
    {
        /// <summary>Стабильный ключ (история вариантов привязана к нему).</summary>
        public string EntityId;
        public string DisplayName;
        /// <summary>Когда/где используется — подсказка в студии.</summary>
        public string UsageNote;

        /// <summary>Текстовое описание модели (промпт text-to-3D и подпись).</summary>
        public Func<string> GetPrompt;
        public Action<string> SetPrompt;

        /// <summary>Assets-путь референс-картинки для image-to-3D (может быть null).</summary>
        public Func<string> GetReferenceImagePath;
        public Action<string> SetReferenceImagePath;

        /// <summary>Папка публикации (Assets/...), куда кладётся итоговая модель.</summary>
        public Func<string> GetPublishFolder;

        /// <summary>Текущий опубликованный ассет модели (Assets-путь) или null.</summary>
        public Func<string> GetPublishedModelPath;
        public Action<string> SetPublishedModelPath;

        /// <summary>
        /// Потолок треугольников для генератора (Hunyuan reduce_faces). 0 — из настроек
        /// окна. Для меток карты нужен настоящий low-poly (1500–3000): дефолтные 30k
        /// топят силуэт в деталях.
        /// </summary>
        public Func<int> GetMaxFaces;
        public Action<int> SetMaxFaces;

        /// <summary>
        /// Контракт «во что превратить»: ассет-приёмник и имя его поля, куда студия
        /// положит созданный из модели префаб (например GlobalMapConfig.baseMarkerModel).
        /// Пусто — модель только публикуется, вставлять некуда.
        /// </summary>
        public Func<UnityEngine.Object> GetTargetAsset;
        public Func<string> GetTargetField;

        /// <summary>Куда складывать созданные префабы (Assets/...).</summary>
        public Func<string> GetPrefabFolder;

        /// <summary>Объект для Undo/SetDirty при записи колбэками.</summary>
        public UnityEngine.Object UndoTarget;
    }
}
