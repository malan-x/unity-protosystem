using System.Collections.Generic;
using UnityEditor;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Реестр стиль-пресетов: находит все IconStylePreset в проекте и хранит выбор дефолтного
    /// (GUID в AiToolsSettings). Редакторы берут стиль отсюда или задают явно в запросе.
    /// </summary>
    public static class IconStyleRegistry
    {
        // Кэш: FindAssets на каждый кадр OnGUI — дорого. Сброс — IconGenAssetWatcher.
        private static List<IconStylePreset> _cache;

        public static void InvalidateCache() => _cache = null;

        /// <summary>Все пресеты проекта (кэшируется до изменения ассетов).</summary>
        public static IReadOnlyList<IconStylePreset> All()
        {
            if (_cache != null)
            {
                _cache.RemoveAll(p => p == null);
                return _cache;
            }

            _cache = new List<IconStylePreset>();
            foreach (var guid in AssetDatabase.FindAssets("t:IconStylePreset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<IconStylePreset>(path);
                if (preset != null) _cache.Add(preset);
            }
            return _cache;
        }

        private static IconStylePreset _defaultCache;
        private static string _defaultCacheGuid;

        /// <summary>
        /// Дефолтный пресет: по GUID из настроек, иначе первый найденный. null — если пресетов нет
        /// (тогда вызывающий покажет подсказку «создайте IconStylePreset»). Кэшируется по GUID.
        /// </summary>
        public static IconStylePreset Default
        {
            get
            {
                string guid = AiToolsSettings.DefaultStyleGuid;
                if (_defaultCache != null && _defaultCacheGuid == guid) return _defaultCache;

                _defaultCacheGuid = guid;
                _defaultCache = null;
                if (!string.IsNullOrEmpty(guid))
                    _defaultCache = AssetDatabase.LoadAssetAtPath<IconStylePreset>(
                        AssetDatabase.GUIDToAssetPath(guid));

                if (_defaultCache == null)
                {
                    var all = All();
                    _defaultCache = all.Count > 0 ? (IconStylePreset)all[0] : null;
                }
                return _defaultCache;
            }
            set
            {
                AiToolsSettings.DefaultStyleGuid =
                    value == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
                _defaultCache = value;
                _defaultCacheGuid = AiToolsSettings.DefaultStyleGuid;
            }
        }

        public static IconStylePreset ByName(string name)
        {
            foreach (var preset in All())
                if (preset.name == name) return preset;
            return null;
        }
    }
}
