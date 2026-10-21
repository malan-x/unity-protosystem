using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Конфигурация студии: упорядоченный список вкладок (наборы и группы). Центральный
    /// ассет нужен, чтобы работать не со всеми ассетами проекта, а с конкретным сетапом:
    /// два конфига кампании с разными наборами зданий → два конфига студии, каждый со
    /// своими стилями, генерятся раздельно.
    /// </summary>
    [CreateAssetMenu(fileName = "IconStudioConfig", menuName = "ProtoSystem/Icon Studio/Studio Config")]
    public class IconStudioConfig : ScriptableObject
    {
        [Tooltip("Вкладки по порядку: IconCollectionAsset или IconCollectionGroup.")]
        public List<UnityEngine.Object> tabs = new();

        /// <summary>Наборы вкладки: набор → он сам; группа → её наборы. Мусор отфильтрован.</summary>
        public List<IconCollectionAsset> CollectionsOfTab(UnityEngine.Object tab)
        {
            var result = new List<IconCollectionAsset>();
            if (tab is IconCollectionAsset col && col != null)
                result.Add(col);
            else if (tab is IconCollectionGroup group && group != null)
                foreach (var c in group.collections)
                    if (c != null) result.Add(c);
            return result;
        }

        public string TabTitle(UnityEngine.Object tab)
        {
            if (tab is IconCollectionAsset col) return col.ResolveTitle();
            if (tab is IconCollectionGroup group) return group.ResolveTitle();
            return "(?)";
        }
    }
}
