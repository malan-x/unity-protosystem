using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.ModelGen.Editor
{
    /// <summary>Один сгенерированный вариант модели с воспроизводимым рецептом.</summary>
    [Serializable]
    public class ModelVariant
    {
        public string id;             // "original" — базовый (существовавший до студии)
        public string assetPath;      // Assets-путь GLB
        public ModelEngine engine;
        public string prompt;
        public string refImagePath;   // Assets-путь референса на момент генерации
        public int seed;
        public int steps;
        public float guidance;
        public int octreeRes;
        public int maxFaces;
        public string createdAt;

        // Риггинг/анимации Meshy
        public string meshyRigTaskId;   // на базовом варианте после риггинга
        public string animLabel;        // "walk", "action_12"… — вариант-анимация
        public string parentVariantId;  // из какого варианта сделан риг/анимация

        public bool IsAnimation => !string.IsNullOrEmpty(animLabel);
    }

    [Serializable]
    public class ModelEntityHistory
    {
        public string entityId;
        public string activeId;
        public List<ModelVariant> variants = new();

        public ModelVariant Find(string id) => variants.Find(v => v.id == id);
        public ModelVariant Active => Find(activeId);
    }

    /// <summary>История вариантов всех сущностей 3D-студии (один SO-ассет на проект).</summary>
    public class ModelVariantLibrary : ScriptableObject
    {
        public List<ModelEntityHistory> entries = new();

        // Папки проекта. Пусто — подпапка рядом с ассетом библиотеки (варианты всегда в Variants рядом).
        [Tooltip("Спрайт-листы турнтейбла и анимаций. Пусто — Sheets рядом с библиотекой.")]
        public string sheetsFolder = "";

        [Tooltip("Референсы, вставленные из буфера. Пусто — Refs рядом с библиотекой.")]
        public string refsFolder = "";

        [Tooltip("Префабы моделей, если сущность не задала свою папку. Пусто — Prefabs рядом с библиотекой.")]
        public string prefabsFolder = "";

        [Tooltip("Опубликованные модели, если сущность не задала свою папку. Пусто — Published рядом с библиотекой.")]
        public string publishFolder = "";

        public ModelEntityHistory Get(string entityId) => entries.Find(e => e.entityId == entityId);

        public ModelEntityHistory Ensure(string entityId)
        {
            var e = Get(entityId);
            if (e == null) { e = new ModelEntityHistory { entityId = entityId }; entries.Add(e); }
            return e;
        }
    }
}
