using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Полный слепок того, что реально ушло в генерацию: промпт, seed, параметры, модель, стиль.
    /// Отдаётся вызывающему в OnDone — чтобы сохранить рецепт варианта и позже воспроизвести
    /// точь-в-точь, даже если дефолтная модель/стиль сменились.
    /// </summary>
    public struct IconGenResult
    {
        public string Subject;         // исходный сюжет-промпт (для правки)
        public string Positive;        // финальный позитив (что реально ушло)
        public string Negative;
        public int Seed;
        public int Size;
        public int Height;             // высота рендера; 0 — квадрат
        public int Steps;
        public float Cfg;
        public string Sampler;
        public string Scheduler;
        public string Checkpoint;      // модель, которой сгенерировано
        public string Lora;
        public float LoraStrength;
        public string StyleName;       // имя стиль-пресета
        public int PixelGrid;          // пиксельная сетка (0 — не квантовалось)
        public bool PixelTrim;         // обрезка полей перед квантованием
        public int PixelColors;        // ограничение палитры (0 — без)
        public string RunId;           // общий id пачки (одна кнопка «сгенерить все» → один RunId)
        public string GeneratedAtUtc;  // ISO 8601
        public string PngAssetPath;    // куда сохранён PNG
        public string[] StyleRefPaths; // asset-пути референсов стиля (IPAdapter); пусто — не использовались
        public float StyleRefWeight;
        public string StyleRefMode;    // режим IPAdapter ("style transfer" / "standard")
        public string InitImagePath;   // asset-путь исходника img2img-стилизации; пусто — с нуля
        public float Denoise;
    }

    /// <summary>
    /// Запрос на генерацию одной иконки — вся привязка к сущности живёт здесь, в полях-данных
    /// и колбэках. Ядро (AiIconGenerator) не знает, что за объект получит иконку: назначение идёт
    /// через OnDone, а Undo — через UndoTarget. Это то, что делает генератор переиспользуемым.
    /// </summary>
    public struct IconGenRequest
    {
        /// <summary>Сюжет («что нарисовано»). Стиль дописывает пресет.</summary>
        public string Prompt;

        /// <summary>Куда сохранить итог, "Assets/...". Вызывающий владеет именованием.</summary>
        public string AssetPath;

        /// <summary>0 — вывести стабильный seed из AssetPath (тот же ассет → тот же кадр).</summary>
        public int Seed;

        /// <summary>Метка для прогресс-бара.</summary>
        public string DisplayName;

        /// <summary>Размер иконки в проекте (текстура ужимается до этого).</summary>
        public Vector2Int StorageSize;

        /// <summary>Размер РЕНДЕРА (0 — квадрат из стиль-пресета). Для превью — 1024×640.</summary>
        public Vector2Int RenderSize;

        /// <summary>Стиль. null — дефолт из IconStyleRegistry.</summary>
        public IconStylePreset Style;

        /// <summary>Вырезать фон через rembg (обычно да; false — если фон уже прозрачный/не нужен).</summary>
        public bool CutBackground;

        /// <summary>
        /// UI-элемент (подложка/рамка): не обрезать до контента (ни rembg, ни PixelSnap-trim) —
        /// иначе сдвинутся границы 9-slice. Сохраняется как есть, кадр целиком.
        /// </summary>
        public bool NoCrop;

        /// <summary>9-slice рамка (L,B,R,T) для импорта UI-элемента. Zero — обычный спрайт.</summary>
        public Vector4 SpriteBorder;

        /// <summary>Объект для Undo.RecordObject (здание / база / дефиниция).</summary>
        public Object UndoTarget;

        /// <summary>Метка операции Undo.</summary>
        public string UndoLabel;

        /// <summary>
        /// Готовый рецепт вместо сюжет+стиль. Если задан — генератор гонит его напрямую
        /// (для перегенерации старого варианта ТОЙ ЖЕ моделью/промптом/сидом). null — обычный путь.
        /// </summary>
        public ComfyUIClient.IconRequest? DirectRequest;

        /// <summary>Общий id пачки (проставляется в «сгенерить все»); попадёт в рецепт варианта.</summary>
        public string RunId;

        /// <summary>
        /// img2img-стилизация: asset-путь исходной картинки. Композиция сохраняется, стиль
        /// перерисовывается текущим промптом/стилем. Пусто — обычная генерация с нуля.
        /// </summary>
        public string InitImageAssetPath;

        /// <summary>Сила перерисовки стилизации: 0.4 — доводка, 0.6 — смена стиля, 0.8 — почти с нуля.</summary>
        public float Denoise;

        /// <summary>
        /// Оверрайд модели для ЭТОГО запроса (напр. стилизация другим профилем без смены глобальных
        /// настроек). Пусто — глобальный чекпоинт/LoRA.
        /// </summary>
        public string CheckpointOverride;
        public string LoraOverride;
        public float LoraStrengthOverride;

        /// <summary>Куда присвоить готовый спрайт + слепок рецепта (для истории вариантов).</summary>
        public Action<Sprite, IconGenResult> OnDone;

        /// <summary>Стабильный seed по пути ассета, если явный не задан.</summary>
        public int ResolveSeed()
            => Seed != 0 ? Seed : Math.Abs((AssetPath ?? "").GetHashCode()) % 1_000_000;

        public IconStylePreset ResolveStyle()
            => Style != null ? Style : IconStyleRegistry.Default;

        public Vector2Int ResolveSize()
            => StorageSize.x > 0 && StorageSize.y > 0 ? StorageSize : new Vector2Int(256, 256);
    }
}
