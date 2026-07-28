using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Набор аудио-студии как ассет: провайдер + Id сета + профиль генерации + папка WAV.
    /// Зеркало IconCollectionAsset.
    /// </summary>
    [CreateAssetMenu(menuName = "ProtoSystem/Audio Studio/Collection", fileName = "AudioSet_")]
    public class AudioCollectionAsset : ScriptableObject
    {
        [Tooltip("Ассет-провайдер (реализует IAudioContentProvider).")]
        public Object provider;

        [Tooltip("Id сета у провайдера (\"ui_sounds\").")]
        public string setId;

        [Tooltip("Название вкладки. Пусто — Title сета.")]
        public string title;

        [Tooltip("Профиль генерации набора (стиль + чекпоинт).")]
        public AudioGenProfile profile;

        [Tooltip("Длительность генерации, сек. 0 — контракт сета/стиля.")]
        public float seconds = 0f;

        [Tooltip("Куда складывать WAV-варианты.")]
        public string wavFolder = "Assets/Audio/Variants";

        public IAudioContentProvider Provider => provider as IAudioContentProvider;

        public AudioContentSet ResolveSet()
        {
            var p = Provider;
            if (p == null) return null;
            foreach (var set in p.GetAudioSets())
                if (set != null && set.Id == setId)
                    return set;
            return null;
        }

        public string ResolveTitle()
        {
            if (!string.IsNullOrEmpty(title)) return title;
            var set = ResolveSet();
            return set != null && !string.IsNullOrEmpty(set.Title) ? set.Title : name;
        }

        /// <summary>Длительность: ассет → контракт сета → 0 (решит стиль).</summary>
        public float ResolveSeconds()
        {
            if (seconds > 0f) return seconds;
            var set = ResolveSet();
            return set?.DefaultSeconds ?? 0f;
        }

        public List<AudioItem> ResolveItems()
        {
            var result = new List<AudioItem>();
            var set = ResolveSet();
            if (set == null) return result;

            foreach (var it in set.Items)
            {
                if (it == null || string.IsNullOrEmpty(it.EntityId)) continue;
                result.Add(new AudioItem
                {
                    EntityId = it.EntityId,
                    DisplayName = string.IsNullOrEmpty(it.DisplayName) ? it.EntityId : it.DisplayName,
                    Accent = it.Accent,
                    UsageNote = string.IsNullOrEmpty(it.UsageNote) ? set.UsageNote : it.UsageNote,
                    SetUsageNote = it.SetUsageNote,
                    Seconds = it.Seconds,
                    Loop = it.Loop,
                    GetClip = it.GetClip,
                    SetClip = it.SetClip,
                    GetVolume = it.GetVolume,
                    SetVolume = it.SetVolume,
                    GetPrompt = it.GetPrompt,
                    SetPrompt = it.SetPrompt,
                    GetClipVariants = it.GetClipVariants,
                    SetClipVariants = it.SetClipVariants,
                    UndoTarget = it.UndoTarget != null ? it.UndoTarget : provider,
                });
            }
            return result;
        }
    }
}
