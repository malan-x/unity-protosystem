using UnityEngine;
using UnityEngine.Audio;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Главный конфиг SoundManagerSystem
    /// </summary>
    [CreateAssetMenu(fileName = "SoundManagerConfig", menuName = "ProtoSystem/Sound/Sound Manager Config")]
    public class SoundManagerConfig : ScriptableObject
    {
        [Header("Provider")]
        [Tooltip("Тип аудио-провайдера")]
        public SoundProviderType providerType = SoundProviderType.Unity;
        
        [Header("Library")]
        [Tooltip("Библиотека звуков")]
        public SoundLibrary soundLibrary;
        
        [Header("Sound Schemes")]
        [Tooltip("Схема звуков для UI")]
        public UISoundScheme uiScheme;
        
        [Tooltip("Схема звуков для GameSession")]
        public GameSessionSoundScheme sessionScheme;
        
        [Tooltip("Настройки музыки")]
        public MusicConfig musicConfig;
        
        [Header("Audio Mixer")]
        [Tooltip("Мастер-миксер (опционально)")]
        public AudioMixer masterMixer;
        
        [Tooltip("Имена групп миксера по категориям")]
        public MixerGroupNames mixerGroupNames = new();
        
        [Header("Volumes")]
        [Tooltip("Громкость по умолчанию")]
        public VolumeSettings defaultVolumes = new();
        
        [Header("Unity Provider Settings")]
        [Tooltip("Размер пула AudioSource")]
        [Range(8, 64)]
        public int audioSourcePoolSize = 24;
        
        [Tooltip("Максимум одновременных звуков")]
        [Range(16, 128)]
        public int maxSimultaneousSounds = 32;
        
        [Header("Playback Control")]
        [Tooltip("Настройки приоритетов")]
        public PrioritySettings priority = new();
        
        [Tooltip("Настройки cooldown")]
        public CooldownSettings cooldown = new();
        
        [Header("3D Sound Defaults")]
        [Tooltip("Минимальное расстояние 3D звука")]
        [Range(0.1f, 10f)]
        public float default3DMinDistance = 1f;
        
        [Tooltip("Максимальное расстояние 3D звука")]
        [Range(10f, 500f)]
        public float default3DMaxDistance = 50f;
        
        [Tooltip("Кривая затухания 3D звука")]
        public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    }
    
    /// <summary>
    /// Имена групп AudioMixer по категориям
    /// </summary>
    [System.Serializable]
    public class MixerGroupNames
    {
        [Tooltip("Exposed parameter для Master volume")]
        public string masterVolume = "MasterVolume";
        
        [Tooltip("Exposed parameter для Music volume")]
        public string musicVolume = "MusicVolume";
        
        [Tooltip("Exposed parameter для SFX volume")]
        public string sfxVolume = "SFXVolume";
        
        [Tooltip("Exposed parameter для Voice volume")]
        public string voiceVolume = "VoiceVolume";
        
        [Tooltip("Exposed parameter для Ambient volume")]
        public string ambientVolume = "AmbientVolume";
        
        [Tooltip("Exposed parameter для UI volume")]
        public string uiVolume = "UIVolume";
        
        /// <summary>
        /// Получить имя параметра по категории
        /// </summary>
        public string GetParameterName(SoundCategory category)
        {
            return category switch
            {
                SoundCategory.Master => masterVolume,
                SoundCategory.Music => musicVolume,
                SoundCategory.SFX => sfxVolume,
                SoundCategory.Voice => voiceVolume,
                SoundCategory.Ambient => ambientVolume,
                SoundCategory.UI => uiVolume,
                _ => masterVolume
            };
        }
    }
}
