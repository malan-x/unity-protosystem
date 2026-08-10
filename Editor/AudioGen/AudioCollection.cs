using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Рабочая модель набора для окна студии (не сериализуется) — снимок ассета набора.
    /// Зеркало IconCollection.
    /// </summary>
    public class AudioCollection
    {
        public string Id;
        public string Title;
        public string UsageNote;
        public float Seconds;
        public string WavFolder;

        /// <summary>Шина сета по умолчанию — записи с другой шиной студия красит красным.</summary>
        public ProtoSystem.Sound.SoundCategory? DefaultBus;

        public Func<List<AudioItem>> GetItems;
        public AudioCollectionAsset SourceAsset;

        public static AudioCollection FromAsset(AudioCollectionAsset asset)
        {
            if (asset == null || asset.Provider == null || string.IsNullOrEmpty(asset.setId)) return null;
            return new AudioCollection
            {
                Id = asset.setId,
                Title = asset.ResolveTitle(),
                UsageNote = asset.ResolveSet()?.UsageNote,
                DefaultBus = asset.ResolveSet()?.DefaultBus,
                Seconds = asset.ResolveSeconds(),
                WavFolder = string.IsNullOrEmpty(asset.wavFolder) ? "Assets/Audio/Variants" : asset.wavFolder,
                GetItems = asset.ResolveItems,
                SourceAsset = asset,
            };
        }
    }

    /// <summary>Зеркало AudioContentItem для окна студии.</summary>
    public class AudioItem
    {
        public string EntityId;
        public string DisplayName;
        public Color Accent;
        public string UsageNote;
        public Action<string> SetUsageNote;
        public float Seconds;
        public bool Loop;
        public Func<AudioClip> GetClip;
        public Action<AudioClip> SetClip;
        public Func<float> GetVolume;
        public Action<float> SetVolume;
        public Func<ProtoSystem.Sound.SoundCategory> GetBus;
        public Action<ProtoSystem.Sound.SoundCategory> SetBus;
        public Func<bool> GetMuted;
        public Action<bool> SetMuted;
        public Func<string> GetPrompt;
        public Action<string> SetPrompt;
        public Func<AudioClip[]> GetClipVariants;
        public Action<AudioClip[]> SetClipVariants;
        public UnityEngine.Object UndoTarget;
    }
}
