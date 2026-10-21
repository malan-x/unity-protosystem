using System;
using UnityEditor;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Сброс кэшей реестров при изменении ассетов. Реестры (профили, стили, паки) кэшируют
    /// результаты FindAssets — иначе каждый кадр OnGUI сканирует базу ассетов, и редакторы
    /// тормозят. Постпроцессор — единственный надёжный сигнал «ассеты изменились».
    /// </summary>
    public class IconGenAssetWatcher : AssetPostprocessor
    {
        /// <summary>Дёргается при любом импорте/удалении/перемещении ассетов.</summary>
        public static event Action AssetsChanged;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                                   string[] moved, string[] movedFrom)
        {
            // Не фильтруем по типу: событие дешёвое, а кэши пересоберутся лениво
            // при следующем обращении. Ложные сбросы безвредны.
            IconGenProfile.InvalidateCache();
            IconStyleRegistry.InvalidateCache();
            AssetsChanged?.Invoke();
        }
    }
}
