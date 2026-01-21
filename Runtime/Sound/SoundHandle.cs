using System;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Хэндл играющего звука для внешнего управления
    /// </summary>
    public readonly struct SoundHandle : IEquatable<SoundHandle>
    {
        /// <summary>Пустой/невалидный хэндл</summary>
        public static readonly SoundHandle Invalid = new(0, 0);
        
        /// <summary>Уникальный ID инстанса</summary>
        public readonly int Id;
        
        /// <summary>Поколение (для проверки актуальности)</summary>
        public readonly int Generation;
        
        internal SoundHandle(int id, int generation)
        {
            Id = id;
            Generation = generation;
        }
        
        /// <summary>Проверка валидности хэндла</summary>
        public bool IsValid => Id != 0;
        
        public bool Equals(SoundHandle other) => Id == other.Id && Generation == other.Generation;
        public override bool Equals(object obj) => obj is SoundHandle other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Id, Generation);
        
        public static bool operator ==(SoundHandle left, SoundHandle right) => left.Equals(right);
        public static bool operator !=(SoundHandle left, SoundHandle right) => !left.Equals(right);
        
        public override string ToString() => IsValid ? $"Sound({Id}:{Generation})" : "Sound(Invalid)";
    }
}
