using System;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Предустановленные звуковые состояния окружения
    /// </summary>
    public enum SoundSnapshotPreset
    {
        None = 0,
        
        // Окружение
        Underwater = 1,
        Cave = 2,
        Indoor = 3,
        Outdoor = 4,
        
        // Геймплей
        Combat = 10,
        Stealth = 11,
        SlowMotion = 12,
        
        // UI/Система
        Paused = 20,
        MenuFocus = 21,
        Cinematic = 22
    }
    
    /// <summary>
    /// Идентификатор snapshot — preset или кастомная строка
    /// </summary>
    [Serializable]
    public struct SoundSnapshotId
    {
        public SoundSnapshotPreset preset;
        public string custom;
        
        /// <summary>
        /// Имя snapshot для AudioMixer/FMOD
        /// </summary>
        public string Name => preset != SoundSnapshotPreset.None 
            ? preset.ToString() 
            : custom;
        
        /// <summary>
        /// Проверка на пустой snapshot
        /// </summary>
        public bool IsEmpty => preset == SoundSnapshotPreset.None && string.IsNullOrEmpty(custom);
        
        public static implicit operator SoundSnapshotId(SoundSnapshotPreset preset) 
            => new() { preset = preset };
        
        public static implicit operator SoundSnapshotId(string custom) 
            => new() { custom = custom };
        
        public override string ToString() => Name ?? "None";
    }
}
