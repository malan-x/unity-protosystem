using UnityEditor;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Проигрывание AudioClip в редакторе без Play Mode. Через скрытый AudioSource
    /// (HideAndDontSave), а НЕ внутренний AudioUtil: тому нельзя задать громкость,
    /// а слушать пачки вариантов на максимуме больно.
    /// </summary>
    public static class AudioPreviewUtil
    {
        private static AudioSource _source;
        private static float? _volume;

        /// <summary>Громкость прослушивания (персистится, дефолт 0.5).</summary>
        public static float Volume
        {
            get => _volume ??= EditorPrefs.GetFloat("ProtoAudio.PreviewVolume", 0.5f);
            set
            {
                _volume = Mathf.Clamp01(value);
                EditorPrefs.SetFloat("ProtoAudio.PreviewVolume", _volume.Value);
                if (_source != null) _source.volume = _volume.Value;
            }
        }

        private static AudioSource Source
        {
            get
            {
                if (_source != null) return _source;
                var go = new GameObject("~AudioStudioPreview")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
                return _source;
            }
        }

        /// <summary>Текущий превью-клип (для отображения ▶/■ на нужной ячейке).</summary>
        public static AudioClip Playing { get; private set; }

        public static bool IsPlaying(AudioClip clip)
            => clip != null && Playing == clip && _source != null && _source.isPlaying;

        public static void Play(AudioClip clip, bool loop = false)
        {
            if (clip == null) return;
            var src = Source;
            src.Stop();
            src.clip = clip;
            src.loop = loop;
            src.volume = Volume;
            src.Play();
            Playing = clip;
        }

        public static void StopAll()
        {
            Playing = null;
            if (_source != null) _source.Stop();
        }

        /// <summary>Волновая форма клипа (встроенное превью ассета). Может вернуть null, пока не прогрузилось.</summary>
        public static Texture2D Waveform(AudioClip clip)
            => clip == null ? null : AssetPreview.GetAssetPreview(clip);
    }
}
