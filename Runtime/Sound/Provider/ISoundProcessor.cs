using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Интерфейс для кастомной обработки активных звуков
    /// (occlusion, doppler, и т.д.)
    /// Реализуется в конкретном проекте при необходимости
    /// </summary>
    public interface ISoundProcessor
    {
        /// <summary>
        /// Обработать активный звук
        /// </summary>
        /// <param name="sound">Информация о звуке (read-only + модификаторы)</param>
        /// <param name="listenerPosition">Позиция слушателя</param>
        void ProcessActiveSound(ref ActiveSoundInfo sound, Vector3 listenerPosition);
    }
    
    /// <summary>
    /// Информация об активном звуке для обработки
    /// </summary>
    public struct ActiveSoundInfo
    {
        /// <summary>ID звука</summary>
        public readonly string Id;
        
        /// <summary>Позиция в мире</summary>
        public readonly Vector3 Position;
        
        /// <summary>Категория звука</summary>
        public readonly SoundCategory Category;
        
        /// <summary>Базовая громкость (из SoundEntry)</summary>
        public readonly float BaseVolume;
        
        /// <summary>3D звук?</summary>
        public readonly bool IsSpatial;
        
        /// <summary>Расстояние до слушателя</summary>
        public readonly float DistanceToListener;
        
        // === Модификаторы (устанавливаются процессором) ===
        
        /// <summary>Множитель громкости от процессора</summary>
        public float VolumeMultiplier;
        
        /// <summary>Множитель высоты от процессора</summary>
        public float PitchMultiplier;
        
        /// <summary>Cutoff для low-pass фильтра (0 = не применять)</summary>
        public float LowPassCutoff;
        
        internal ActiveSoundInfo(
            string id,
            Vector3 position,
            SoundCategory category,
            float baseVolume,
            bool isSpatial,
            float distanceToListener)
        {
            Id = id;
            Position = position;
            Category = category;
            BaseVolume = baseVolume;
            IsSpatial = isSpatial;
            DistanceToListener = distanceToListener;
            
            VolumeMultiplier = 1f;
            PitchMultiplier = 1f;
            LowPassCutoff = 0f;
        }
    }
}
