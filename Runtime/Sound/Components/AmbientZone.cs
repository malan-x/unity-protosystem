using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Зона ambient звука с 3D позиционированием и fade
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AmbientZone : MonoBehaviour
    {
        [Header("Sound")]
        [SoundId(SoundCategory.Ambient)]
        [Tooltip("ID ambient звука")]
        public string soundId;
        
        [Range(0f, 1f)]
        [Tooltip("Громкость внутри зоны")]
        public float volume = 1f;
        
        [Header("Fade")]
        [Tooltip("Время появления при входе")]
        public float fadeInTime = 1f;
        
        [Tooltip("Время затухания при выходе")]
        public float fadeOutTime = 1f;
        
        [Header("Trigger")]
        [Tooltip("Тег объекта для активации (пусто = любой)")]
        public string playerTag = "Player";
        
        [Tooltip("Воспроизводить в центре зоны (иначе следует за игроком)")]
        public bool playAtCenter = false;
        
        [Header("State")]
        [Tooltip("Активна ли зона по умолчанию")]
        public bool startActive = false;
        
        // Runtime
        private SoundHandle _handle;
        private bool _isInside;
        private float _currentVolume;
        private float _targetVolume;
        private Transform _listener;
        
        private void Start()
        {
            // Убедиться что коллайдер — триггер
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            
            if (startActive)
            {
                StartAmbient();
            }
        }
        
        private void Update()
        {
            // Плавное изменение громкости
            if (!Mathf.Approximately(_currentVolume, _targetVolume))
            {
                float speed = _targetVolume > _currentVolume 
                    ? 1f / Mathf.Max(0.01f, fadeInTime) 
                    : 1f / Mathf.Max(0.01f, fadeOutTime);
                
                _currentVolume = Mathf.MoveTowards(_currentVolume, _targetVolume, speed * Time.deltaTime);
                
                // TODO: Применить громкость к handle
                // В текущей реализации SoundHandle не поддерживает SetVolume
                // Нужно расширить ISoundProvider
            }
            
            // Следовать за слушателем если нужно
            if (_handle.IsValid && !playAtCenter && _listener != null)
            {
                // TODO: Обновить позицию звука
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!CheckTag(other.gameObject)) return;
            
            _listener = other.transform;
            _isInside = true;
            StartAmbient();
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (!CheckTag(other.gameObject)) return;
            
            _isInside = false;
            StopAmbient();
        }
        
        private void OnDisable()
        {
            if (_handle.IsValid)
            {
                SoundManagerSystem.Stop(_handle);
                _handle = SoundHandle.Invalid;
            }
        }
        
        /// <summary>
        /// Запустить ambient звук
        /// </summary>
        public void StartAmbient()
        {
            if (_handle.IsValid) return;
            if (string.IsNullOrEmpty(soundId)) return;
            
            Vector3? pos = playAtCenter ? transform.position : _listener?.position;
            _handle = SoundManagerSystem.Play(soundId, pos, volume);
            _targetVolume = volume;
        }
        
        /// <summary>
        /// Остановить ambient звук
        /// </summary>
        public void StopAmbient()
        {
            _targetVolume = 0f;
            
            // Если fade = 0, сразу остановить
            if (fadeOutTime <= 0 && _handle.IsValid)
            {
                SoundManagerSystem.Stop(_handle);
                _handle = SoundHandle.Invalid;
            }
        }
        
        private bool CheckTag(GameObject obj)
        {
            if (string.IsNullOrEmpty(playerTag)) return true;
            return obj.CompareTag(playerTag);
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 0.3f, 0.3f);
            
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }
}
