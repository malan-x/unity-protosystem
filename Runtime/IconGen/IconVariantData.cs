using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.IconGen
{
    /// <summary>
    /// Рецепт генерации — всё, чтобы воспроизвести иконку точь-в-точь: промпт, seed, параметры,
    /// модель, стиль. Весит байты и переживает удаление PNG: удалив картинки ради места, набор
    /// иконок всё равно можно перегенерировать из рецептов.
    /// Рантайм-зеркало ProtoSystem.IconGen.Editor.IconGenResult (без editor-зависимостей).
    /// </summary>
    [Serializable]
    public class IconRecipe
    {
        public string subject;         // сюжет-промпт (для правки)
        public string positive;        // финальный позитив (что реально ушло)
        public string negative;
        public int seed;
        public int size;
        public int height;             // высота рендера; 0 — квадрат (size)
        public int steps;
        public float cfg;
        public string sampler;
        public string scheduler;
        public string checkpoint;      // модель, которой сгенерировано
        public string lora;
        public float loraStrength;
        public string styleName;
        public int pixelGrid;          // пиксельная сетка квантования (0 — не применялась)
        public bool pixelTrim;         // обрезка полей перед квантованием
        public int pixelColors;        // ограничение палитры (0 — без)
        public string runId;           // общий id пачки
        public string generatedAtUtc;  // ISO 8601
        public List<string> styleRefPaths = new();  // asset-пути референсов стиля (IPAdapter); пусто — без них
        public float styleRefWeight;
        public string styleRefMode;                 // режим IPAdapter ("style transfer" / "standard")
        public string initImagePath;                // asset-путь исходника img2img-стилизации; пусто — с нуля
        public float denoise;                       // сила перерисовки стилизации
    }

    /// <summary>Один сгенерированный вариант иконки: рецепт + ссылка на PNG (может быть удалён).</summary>
    [Serializable]
    public class IconVariant
    {
        public string id;              // стабильный guid — по нему ссылаются паки
        public IconRecipe recipe = new();
        public Sprite sprite;          // null, если картинку удалили (imageDeleted)
        public bool imageDeleted;      // PNG удалён ради места, рецепт жив
        public string pngAssetPath;    // куда сохранён/сохранится PNG (для перегенерации в тот же путь)
        public bool bad;               // помечен как неудачный («!» в витрине пака)

        // Постобработка UI-элемента — СВОЯ у каждого варианта (обрезка/зеркало под конкретную
        // картинку): 0=Оригинал,1=Обрезать,2=Зеркалить,3=Заливка области; ось 0=обе,1=гориз,2=верт;
        // область top-down; цвет заливки для режима 3.
        public int postMode;
        public int postAxis;
        public RectInt postRect;
        public Color postFillColor = new(0.05f, 0.05f, 0.06f, 1f);
        public int postFeather = 6;        // размытие края заливки, px (0 — резкий край)
        public int postCornerRadius = 8;   // скругление углов заливки, px (0 — прямые углы)
    }

    /// <summary>История вариантов одной сущности (одной иконки). Ничего не перезаписывается.</summary>
    [Serializable]
    public class EntityIconHistory
    {
        public string entityId;
        public List<IconVariant> variants = new();
        public string activeVariantId;

        public IconVariant Active
        {
            get
            {
                foreach (var v in variants)
                    if (v != null && v.id == activeVariantId) return v;
                return variants.Count > 0 ? variants[variants.Count - 1] : null;
            }
        }

        public IconVariant Find(string variantId)
        {
            foreach (var v in variants)
                if (v != null && v.id == variantId) return v;
            return null;
        }
    }
}
