using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.IconGen
{
    /// <summary>
    /// Пак-снапшот: именованный выбор «для каждой сущности — конкретный вариант». Уровень «микс
    /// лучшего»: можно собрать идеальный набор из иконок разных прогонов. Применение пака
    /// проставляет активные варианты в IconVariantLibrary.
    ///
    /// Второй уровень (прогоны — авто-варианты одним рецептом на все сущности) не отдельный ассет,
    /// а группировка вариантов по recipe.runId внутри истории.
    /// </summary>
    [CreateAssetMenu(fileName = "IconPack", menuName = "ProtoSystem/Icon Studio/Icon Pack")]
    public class IconPack : ScriptableObject
    {
        [Serializable]
        public class Selection
        {
            public string entityId;
            public string variantId;
        }

        public string packName;
        public List<Selection> selections = new();

        public string VariantFor(string entityId)
        {
            foreach (var s in selections)
                if (s != null && s.entityId == entityId) return s.variantId;
            return null;
        }

        public void Set(string entityId, string variantId)
        {
            foreach (var s in selections)
                if (s != null && s.entityId == entityId) { s.variantId = variantId; return; }
            selections.Add(new Selection { entityId = entityId, variantId = variantId });
        }
    }
}
