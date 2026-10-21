using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.IconGen
{
    /// <summary>
    /// Хранилище истории вариантов всех сущностей в одном ассете. Доступно и в редакторе
    /// (генерация копит варианты), и в плей-моде (dev-оверлей переключает иконки/паки).
    ///
    /// Один ассет вместо SO-на-сущность: проще грузить и не засоряет AssetDatabase.
    /// Студия находит его по типу (первый в проекте), а если нет — создаёт в Assets/AIStudio/Icons.
    /// </summary>
    [CreateAssetMenu(fileName = "IconVariantLibrary", menuName = "ProtoSystem/Icon Studio/Variant Library")]
    public class IconVariantLibrary : ScriptableObject
    {
        public List<EntityIconHistory> entities = new();

        // Папки проекта. Пусто — подпапка рядом с ассетом библиотеки.
        [Tooltip("Куда сохранять паки (IconPack). Пусто — папка IconPacks рядом с библиотекой.")]
        public string packsFolder = "";

        [Tooltip("Куда класть референсы стиля, вставленные из буфера. Пусто — StyleRefs рядом с библиотекой.")]
        public string styleRefsFolder = "";

        public EntityIconHistory Find(string entityId)
        {
            foreach (var e in entities)
                if (e != null && e.entityId == entityId) return e;
            return null;
        }

        public EntityIconHistory GetOrCreate(string entityId)
        {
            var existing = Find(entityId);
            if (existing != null) return existing;
            var created = new EntityIconHistory { entityId = entityId };
            entities.Add(created);
            return created;
        }

        /// <summary>Активная (выбранная) иконка сущности — то, что должно показываться в игре.</summary>
        public Sprite ActiveSprite(string entityId) => Find(entityId)?.Active?.sprite;
    }
}
