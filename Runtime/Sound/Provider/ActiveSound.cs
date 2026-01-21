using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Внутреннее представление активного звука
    /// </summary>
    internal class ActiveSound
    {
        public SoundHandle Handle;
        public SoundEntry Entry;
        public AudioSource Source;
        public Vector3 Position;
        public float StartTime;
        public float BaseVolume;
        public float BasePitch;
        public bool IsPaused;
        
        // Модификаторы от ISoundProcessor
        public float ProcessorVolumeMultiplier = 1f;
        public float ProcessorPitchMultiplier = 1f;
        public float ProcessorLowPassCutoff = 0f;
        
        // Для fade
        public float FadeTargetVolume = 1f;
        public float FadeSpeed = 0f;
        public bool FadeComplete = true;
        
        public bool IsPlaying => Source != null && Source.isPlaying;
        public bool IsLooping => Entry != null && Entry.loop;
        
        public void UpdateVolume(float categoryVolume, float masterVolume)
        {
            if (Source == null) return;
            
            float targetVolume = BaseVolume * categoryVolume * masterVolume * ProcessorVolumeMultiplier;
            
            if (!FadeComplete && FadeSpeed > 0)
            {
                float current = Source.volume;
                float newVolume = Mathf.MoveTowards(current, FadeTargetVolume * targetVolume, FadeSpeed * Time.unscaledDeltaTime);
                Source.volume = newVolume;
                
                if (Mathf.Approximately(newVolume, FadeTargetVolume * targetVolume))
                {
                    FadeComplete = true;
                }
            }
            else
            {
                Source.volume = targetVolume * FadeTargetVolume;
            }
        }
        
        public void UpdatePitch()
        {
            if (Source == null) return;
            Source.pitch = BasePitch * ProcessorPitchMultiplier;
        }
        
        public void Reset()
        {
            Handle = SoundHandle.Invalid;
            Entry = null;
            Source = null;
            Position = Vector3.zero;
            StartTime = 0f;
            BaseVolume = 1f;
            BasePitch = 1f;
            IsPaused = false;
            ProcessorVolumeMultiplier = 1f;
            ProcessorPitchMultiplier = 1f;
            ProcessorLowPassCutoff = 0f;
            FadeTargetVolume = 1f;
            FadeSpeed = 0f;
            FadeComplete = true;
        }
    }
}
