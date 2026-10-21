using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Точки расширения ИИ Аудио студии для проекта. Студия живёт в пакете и не знает
    /// игровых проигрывателей — проект подписывается из [InitializeOnLoad]-класса:
    ///
    ///   AudioStudioExtensions.PlayModeVolumeChanged += (clip, v) => MyLoopPlayer.SetBase(clip, v);
    ///   AudioStudioExtensions.SoloFilterChanged += filter => MySoloGate.Filter = filter;
    ///   AudioStudioExtensions.UsageScanExcludes.Add("/Settings/Sound/VoiceLines.asset");
    /// </summary>
    public static class AudioStudioExtensions
    {
        /// <summary>
        /// Громкость записи поменяли в студии во время Play Mode. Источники SoundManager студия
        /// правит сама; это событие — для игровых лупов, которые держат свою базовую громкость
        /// и перезаписывают её каждый кадр.
        /// </summary>
        public static event Action<AudioClip, float> PlayModeVolumeChanged;

        /// <summary>
        /// Режим «Соло» включился/поменялся (фильтр пропускает только отмеченные sound id) или
        /// выключился (null). Студия глушит источники по клипу; прямые PlayOneShot проекта
        /// закрываются этим фильтром.
        /// </summary>
        public static event Action<Func<string, bool>> SoloFilterChanged;

        /// <summary>
        /// Префиксы sound id, которые играются динамически (по константам, а не литералом) —
        /// скан использований их не проверяет.
        /// </summary>
        public static readonly List<string> DynamicIdPrefixes = new() { "voice_" };

        /// <summary>Доп. фрагменты путей, которые скан использований не читает (хранилища звуков проекта).</summary>
        public static readonly List<string> UsageScanExcludes = new();

        internal static void RaisePlayModeVolumeChanged(AudioClip clip, float volume)
            => PlayModeVolumeChanged?.Invoke(clip, volume);

        internal static void RaiseSoloFilterChanged(Func<string, bool> filter)
            => SoloFilterChanged?.Invoke(filter);
    }
}
