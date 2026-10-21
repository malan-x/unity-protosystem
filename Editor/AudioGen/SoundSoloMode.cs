using System.Collections.Generic;
using ProtoSystem.Sound;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Режим «Соло» (как в DAW): слышны только отмеченные звуки, остальные глушатся —
    /// вживую, в Play Mode.
    ///
    /// Реализация — ТОЛЬКО mute источников по клипу, каждый кадр. Громкости записей
    /// SoundLibrary не трогаем: LoopPlayer'ы захватывают громкость один раз на старте,
    /// и луп, запущенный при занулённой записи, оставался немым до перезапуска боя
    /// (баг car_wheel_rumble). Провайдер звука ставит source.clip даже для one-shot'ов,
    /// так что идентификация покрывает всё, что идёт через SoundManager.
    ///
    /// Известная утечка: прямые PlayOneShot без клипа на источнике (удары врагов,
    /// пара стингеров миссии) в соло-режиме НЕ глушатся — это осознанная плата за то,
    /// что ничего не ломается. Проект может закрыть её сам: фильтр соло приходит в
    /// AudioStudioExtensions.SoloFilterChanged — его спрашивают перед PlayOneShot.
    /// </summary>
    [InitializeOnLoad]
    public static class SoundSoloMode
    {
        public static bool Active { get; private set; }

        private static readonly HashSet<string> _soloIds = new();
        private static readonly List<AudioSource> _mutedSources = new();
        private static Dictionary<AudioClip, string> _clipToId;

        public static event System.Action Changed;

        static SoundSoloMode()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) Disable();
            };

            // Публикация нового варианта прямо в Play Mode меняет клип записи — карту
            // «клип → id» надо пересобрать, иначе новый клип не узнаётся, а его источник
            // (переиспользованный из пула, возможно заглушенный раньше) остаётся немым
            AudioVariantStore.Changed += InvalidateMap;
            ProtoSystem.AudioGen.Editor.AudioGenAssetWatcher.AssetsChanged += InvalidateMap;
        }

        private static void InvalidateMap() => _clipToId = null;

        public static bool IsSolo(string id) => _soloIds.Contains(id);
        public static int SoloCount => _soloIds.Count;

        public static void Toggle(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!_soloIds.Remove(id)) _soloIds.Add(id);

            if (_soloIds.Count == 0)
            {
                Disable();
                return;
            }

            if (!Active)
            {
                Active = true;
                _clipToId = null;
                EditorApplication.update += Tick;
                // Гейт one-shot'ов проекта: прямые проигрыватели спрашивают фильтр перед
                // PlayOneShot — чужие выстрелы в соло просто не запускаются
                AudioStudioExtensions.RaiseSoloFilterChanged(id => _soloIds.Contains(id));
            }
            Changed?.Invoke();
        }

        public static void Disable()
        {
            if (!Active && _soloIds.Count == 0) return;

            _soloIds.Clear();
            foreach (var src in _mutedSources)
                if (src != null) src.mute = false;
            _mutedSources.Clear();

            _clipToId = null;
            AudioStudioExtensions.RaiseSoloFilterChanged(null);
            if (Active)
            {
                Active = false;
                EditorApplication.update -= Tick;
            }
            Changed?.Invoke();
        }

        /// <summary>Каждый кадр в Play Mode: глушим чужие источники, возвращаем соло-звуки.</summary>
        private static void Tick()
        {
            if (!Active || !EditorApplication.isPlaying) return;

            _clipToId ??= BuildClipMap();

            var sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var src in sources)
            {
                if (src == null || src.clip == null) continue;
                if (!_clipToId.TryGetValue(src.clip, out var id)) continue;

                bool shouldMute = !_soloIds.Contains(id);
                if (src.mute == shouldMute) continue;

                src.mute = shouldMute;
                if (shouldMute) _mutedSources.Add(src);
                else _mutedSources.Remove(src);
            }

            // Пуловые источники переиспользуются: если заглушенный источник теперь играет
            // клип не из библиотеки (или пуст) — вернуть ему звук, он больше «не наш»
            for (int i = _mutedSources.Count - 1; i >= 0; i--)
            {
                var src = _mutedSources[i];
                if (src == null) { _mutedSources.RemoveAt(i); continue; }
                if (src.clip == null || !_clipToId.ContainsKey(src.clip))
                {
                    src.mute = false;
                    _mutedSources.RemoveAt(i);
                }
            }
        }

        private static Dictionary<AudioClip, string> BuildClipMap()
        {
            var map = new Dictionary<AudioClip, string>();
            var library = AudioVariantStore.SoundLibrary;
            if (library == null) return map;

            foreach (var e in library.coreEntries)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if (e.clip != null) map.TryAdd(e.clip, e.id);
                if (e.clipVariants == null) continue;
                foreach (var v in e.clipVariants)
                    if (v != null) map.TryAdd(v, e.id);
            }
            return map;
        }
    }
}
