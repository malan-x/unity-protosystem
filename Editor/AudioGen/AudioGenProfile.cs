using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Профиль генерации: проверенная связка «стиль + чекпоинт» с описанием и журналом
    /// неудач. Набор ссылается на профиль — так у «UI кликов» и «эмбиентов» разные
    /// модели и промпты без ручного переключения глобальных настроек.
    /// </summary>
    [CreateAssetMenu(menuName = "ProtoSystem/Audio Gen Profile", fileName = "AudioProfile_")]
    public class AudioGenProfile : ScriptableObject
    {
        [TextArea(1, 3)]
        public string description;

        public AudioStylePreset style;

        [Tooltip("Файл чекпоинта в models/checkpoints. Пусто — глобальная настройка движка стиля.")]
        public string checkpoint = "";

        [Tooltip("Заметки о плохих результатах — что этой связкой генерить не стоит.")]
        public List<string> badNotes = new();

        public void AddBadNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note)) return;
            badNotes.Add(note.Trim());
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // ── Кэш всех профилей проекта (сброс — AudioGenAssetWatcher) ──

        private static List<AudioGenProfile> _cache;

        public static void InvalidateCache() => _cache = null;

        public static IReadOnlyList<AudioGenProfile> All()
        {
            if (_cache != null)
            {
                _cache.RemoveAll(p => p == null);
                return _cache;
            }

            _cache = new List<AudioGenProfile>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AudioGenProfile"))
            {
                var p = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioGenProfile>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (p != null) _cache.Add(p);
            }
            return _cache;
        }
    }
}
