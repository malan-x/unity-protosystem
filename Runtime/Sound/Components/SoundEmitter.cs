using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Точка воспроизведения 3D звука (для использования с Animator или UnityEvents)
    /// </summary>
    public class SoundEmitter : MonoBehaviour
    {
        [Header("Default Sound")]
        [SoundId]
        [Tooltip("ID звука по умолчанию")]
        public string defaultSoundId;
        
        [Range(0f, 1f)]
        [Tooltip("Громкость по умолчанию")]
        public float defaultVolume = 1f;
        
        [Header("Options")]
        [Tooltip("Использовать позицию этого объекта")]
        public bool use3DPosition = true;
        
        [Tooltip("Останавливать предыдущий звук")]
        public bool stopPreviousOnPlay = false;
        
        // Runtime
        private SoundHandle _lastHandle;
        
        /// <summary>
        /// Воспроизвести звук по умолчанию (для UnityEvents/Animator)
        /// </summary>
        public void Play()
        {
            PlaySound(defaultSoundId, defaultVolume);
        }
        
        /// <summary>
        /// Воспроизвести звук по ID
        /// </summary>
        public void PlaySound(string soundId)
        {
            PlaySound(soundId, defaultVolume);
        }
        
        /// <summary>
        /// Воспроизвести звук по ID с громкостью
        /// </summary>
        public void PlaySound(string soundId, float volume)
        {
            if (string.IsNullOrEmpty(soundId)) return;
            
            if (stopPreviousOnPlay && _lastHandle.IsValid)
            {
                SoundManagerSystem.Stop(_lastHandle);
            }
            
            Vector3? position = use3DPosition ? transform.position : null;
            _lastHandle = SoundManagerSystem.Play(soundId, position, volume);
        }
        
        /// <summary>
        /// Остановить последний воспроизведённый звук
        /// </summary>
        public void Stop()
        {
            if (_lastHandle.IsValid)
            {
                SoundManagerSystem.Stop(_lastHandle);
                _lastHandle = SoundHandle.Invalid;
            }
        }
        
        /// <summary>
        /// Воспроизвести звук (для Animation Events)
        /// Формат параметра: "sound_id" или "sound_id:0.8" (с громкостью)
        /// </summary>
        public void PlayAnimationSound(string param)
        {
            if (string.IsNullOrEmpty(param)) return;
            
            string soundId = param;
            float volume = defaultVolume;
            
            // Парсинг формата "id:volume"
            int colonIndex = param.IndexOf(':');
            if (colonIndex > 0)
            {
                soundId = param.Substring(0, colonIndex);
                if (float.TryParse(param.Substring(colonIndex + 1), out float parsedVolume))
                {
                    volume = parsedVolume;
                }
            }
            
            PlaySound(soundId, volume);
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.DrawIcon(transform.position, "d_AudioSource Icon", true);
        }
    }
}
