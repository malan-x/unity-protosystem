using System.Collections.Generic;
using UnityEditor;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Реестр стилей звука в проекте. Кэшируется (FindAssets на каждый вызов — дорого),
    /// сброс — AudioGenAssetWatcher при изменении ассетов.
    /// </summary>
    public static class AudioStyleRegistry
    {
        private static List<AudioStylePreset> _cache;

        public static void InvalidateCache()
        {
            _cache = null;
            _defaultCached = null;
        }

        public static IReadOnlyList<AudioStylePreset> All()
        {
            if (_cache != null)
            {
                _cache.RemoveAll(p => p == null);
                return _cache;
            }

            _cache = new List<AudioStylePreset>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioStylePreset"))
            {
                var p = AssetDatabase.LoadAssetAtPath<AudioStylePreset>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null) _cache.Add(p);
            }
            return _cache;
        }

        public static AudioStylePreset ByName(string name)
        {
            foreach (var s in All())
                if (s != null && s.name == name) return s;
            return null;
        }

        // ── Дефолтный стиль: GUID в настройках, фолбэк — первый в проекте ──

        private static AudioStylePreset _defaultCached;
        private static string _defaultCachedGuid;

        public static AudioStylePreset Default
        {
            get
            {
                string guid = AudioAiSettings.DefaultStyleGuid;
                if (_defaultCached != null && _defaultCachedGuid == guid) return _defaultCached;

                _defaultCachedGuid = guid;
                _defaultCached = null;
                if (!string.IsNullOrEmpty(guid))
                    _defaultCached = AssetDatabase.LoadAssetAtPath<AudioStylePreset>(
                        AssetDatabase.GUIDToAssetPath(guid));

                if (_defaultCached == null)
                {
                    var all = All();
                    if (all.Count > 0) _defaultCached = all[0];
                }
                return _defaultCached;
            }
            set
            {
                _defaultCached = value;
                _defaultCachedGuid = value == null
                    ? ""
                    : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
                AudioAiSettings.DefaultStyleGuid = _defaultCachedGuid;
            }
        }
    }
}
