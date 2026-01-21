using System;
using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Запись в библиотеке звуков
    /// </summary>
    [Serializable]
    public class SoundEntry
    {
        [Header("Identification")]
        [Tooltip("Уникальный ID звука")]
        public string id;
        
        [Tooltip("Категория звука")]
        public SoundCategory category = SoundCategory.SFX;
        
        [Header("Unity Audio")]
        [Tooltip("Основной аудиоклип")]
        public AudioClip clip;
        
        [Tooltip("Варианты для рандома (опционально)")]
        public AudioClip[] clipVariants;
        
        [Header("FMOD (опционально)")]
        [Tooltip("Путь к FMOD событию (event:/Category/Sound)")]
        public string fmodEvent;
        
        [Header("Playback Settings")]
        [Tooltip("Громкость звука")]
        [Range(0f, 1f)]
        public float volume = 1f;
        
        [Tooltip("Базовая высота звука")]
        [Range(0.1f, 3f)]
        public float pitch = 1f;
        
        [Tooltip("Случайное отклонение высоты (±)")]
        [Range(0f, 0.5f)]
        public float pitchVariation = 0f;
        
        [Tooltip("Зацикливать звук")]
        public bool loop = false;
        
        [Header("3D Settings")]
        [Tooltip("3D позиционирование")]
        public bool spatial = false;
        
        [Tooltip("Минимальное расстояние (использовать дефолт из конфига если 0)")]
        [Range(0f, 50f)]
        public float minDistance = 0f;
        
        [Tooltip("Максимальное расстояние (использовать дефолт из конфига если 0)")]
        [Range(0f, 500f)]
        public float maxDistance = 0f;
        
        [Header("Priority & Cooldown")]
        [Tooltip("Приоритет звука")]
        public SoundPriority priority = SoundPriority.Normal;
        
        [Tooltip("Минимальный интервал между воспроизведениями (0 = использовать дефолт)")]
        [Range(0f, 2f)]
        public float cooldown = 0f;
        
        [Header("Bank")]
        [Tooltip("ID банка (пусто = загружен всегда)")]
        public string bankId;
        
        /// <summary>
        /// Получить случайный клип (основной или из вариантов)
        /// </summary>
        public AudioClip GetRandomClip()
        {
            if (clipVariants != null && clipVariants.Length > 0)
            {
                int index = UnityEngine.Random.Range(0, clipVariants.Length);
                return clipVariants[index] != null ? clipVariants[index] : clip;
            }
            return clip;
        }
        
        /// <summary>
        /// Получить случайную высоту с учётом вариации
        /// </summary>
        public float GetRandomPitch()
        {
            if (pitchVariation <= 0) return pitch;
            return pitch + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
        }
        
        /// <summary>
        /// Проверка валидности записи
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(id)) return false;
            
            // Должен быть либо Unity клип, либо FMOD событие
            bool hasUnityClip = clip != null || (clipVariants != null && clipVariants.Length > 0);
            bool hasFmodEvent = !string.IsNullOrEmpty(fmodEvent);
            
            return hasUnityClip || hasFmodEvent;
        }
    }
}
