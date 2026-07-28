using UnityEditor;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Сброс кэшей реестров при любом изменении ассетов + общее событие для окон студии.
    /// Зеркало IconGenAssetWatcher.
    /// </summary>
    public class AudioGenAssetWatcher : AssetPostprocessor
    {
        /// <summary>Ассеты изменились (импорт/удаление/перемещение) — окнам пора обновиться.</summary>
        public static event System.Action AssetsChanged;

        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            AudioGenProfile.InvalidateCache();
            AudioStyleRegistry.InvalidateCache();
            AssetsChanged?.Invoke();
        }
    }
}
