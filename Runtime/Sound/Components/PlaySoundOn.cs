using UnityEngine;
using UnityEngine.EventSystems;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Триггер воспроизведения звука
    /// </summary>
    public enum SoundTrigger
    {
        // Lifecycle
        Enable,
        Disable,
        Start,
        Destroy,
        
        // Physics
        CollisionEnter,
        CollisionExit,
        TriggerEnter,
        TriggerExit,
        
        // UI
        PointerEnter,
        PointerExit,
        PointerClick,
        PointerDown,
        PointerUp,
        
        // Input
        KeyDown,
        KeyUp,
        
        // Custom
        EventBus,
        Manual
    }
    
    /// <summary>
    /// Универсальный компонент воспроизведения звука по событию
    /// </summary>
    public class PlaySoundOn : MonoBehaviour, 
        IPointerEnterHandler, IPointerExitHandler, 
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Sound")]
        [SoundId]
        [Tooltip("ID звука из SoundLibrary")]
        public string soundId;
        
        [Range(0f, 1f)]
        [Tooltip("Множитель громкости")]
        public float volume = 1f;
        
        [Range(0f, 0.5f)]
        [Tooltip("Случайное отклонение высоты (±)")]
        public float pitchVariation = 0f;
        
        [Header("Trigger")]
        [Tooltip("Когда воспроизводить звук")]
        public SoundTrigger trigger = SoundTrigger.Enable;
        
        [Header("Trigger Settings")]
        [Tooltip("Тег объекта для Collision/Trigger (пусто = любой)")]
        public string targetTag = "";
        
        [Tooltip("Минимальная сила столкновения")]
        public float minCollisionForce = 0f;
        
        [Tooltip("ID события EventBus")]
        public int eventBusId;
        
        [Tooltip("Клавиша для Input триггера")]
        public KeyCode inputKey = KeyCode.None;
        
        [Header("Conditions")]
        [Tooltip("Минимальный интервал между воспроизведениями")]
        public float cooldown = 0f;
        
        [Tooltip("Воспроизвести только один раз")]
        public bool playOnce = false;
        
        [Tooltip("Использовать позицию объекта (3D звук)")]
        public bool useObjectPosition = false;
        
        [Header("Advanced")]
        [Tooltip("Останавливать предыдущий звук при новом воспроизведении")]
        public bool stopPrevious = false;
        
        // Runtime
        private float _lastPlayTime = -999f;
        private bool _hasPlayed = false;
        private SoundHandle _lastHandle;
        
        // === Lifecycle ===
        
        private void OnEnable()
        {
            if (trigger == SoundTrigger.Enable)
                TryPlay();
            
            if (trigger == SoundTrigger.EventBus && eventBusId != 0)
                EventBus.Subscribe(eventBusId, OnEventBusTriggered);
        }
        
        private void OnDisable()
        {
            if (trigger == SoundTrigger.Disable)
                TryPlay();
            
            if (trigger == SoundTrigger.EventBus && eventBusId != 0)
                EventBus.Unsubscribe(eventBusId, OnEventBusTriggered);
        }
        
        private void Start()
        {
            if (trigger == SoundTrigger.Start)
                TryPlay();
        }
        
        private void OnDestroy()
        {
            if (trigger == SoundTrigger.Destroy)
                TryPlay();
        }
        
        private void Update()
        {
            if (trigger == SoundTrigger.KeyDown && inputKey != KeyCode.None)
            {
                if (Input.GetKeyDown(inputKey))
                    TryPlay();
            }
            else if (trigger == SoundTrigger.KeyUp && inputKey != KeyCode.None)
            {
                if (Input.GetKeyUp(inputKey))
                    TryPlay();
            }
        }
        
        // === Physics ===
        
        private void OnCollisionEnter(Collision collision)
        {
            if (trigger != SoundTrigger.CollisionEnter) return;
            if (!CheckTag(collision.gameObject)) return;
            if (minCollisionForce > 0 && collision.relativeVelocity.magnitude < minCollisionForce) return;
            
            TryPlay();
        }
        
        private void OnCollisionExit(Collision collision)
        {
            if (trigger != SoundTrigger.CollisionExit) return;
            if (!CheckTag(collision.gameObject)) return;
            
            TryPlay();
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (trigger != SoundTrigger.TriggerEnter) return;
            if (!CheckTag(other.gameObject)) return;
            
            TryPlay();
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (trigger != SoundTrigger.TriggerExit) return;
            if (!CheckTag(other.gameObject)) return;
            
            TryPlay();
        }
        
        // 2D Physics
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (trigger != SoundTrigger.CollisionEnter) return;
            if (!CheckTag(collision.gameObject)) return;
            if (minCollisionForce > 0 && collision.relativeVelocity.magnitude < minCollisionForce) return;
            
            TryPlay();
        }
        
        private void OnCollisionExit2D(Collision2D collision)
        {
            if (trigger != SoundTrigger.CollisionExit) return;
            if (!CheckTag(collision.gameObject)) return;
            
            TryPlay();
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (trigger != SoundTrigger.TriggerEnter) return;
            if (!CheckTag(other.gameObject)) return;
            
            TryPlay();
        }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            if (trigger != SoundTrigger.TriggerExit) return;
            if (!CheckTag(other.gameObject)) return;
            
            TryPlay();
        }
        
        // === UI Events ===
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (trigger == SoundTrigger.PointerEnter)
                TryPlay();
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            if (trigger == SoundTrigger.PointerExit)
                TryPlay();
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (trigger == SoundTrigger.PointerClick)
                TryPlay();
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            if (trigger == SoundTrigger.PointerDown)
                TryPlay();
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            if (trigger == SoundTrigger.PointerUp)
                TryPlay();
        }
        
        // === EventBus ===
        
        private void OnEventBusTriggered(object payload)
        {
            TryPlay();
        }
        
        // === Public API ===
        
        /// <summary>
        /// Воспроизвести звук вручную (для Manual триггера или скриптов)
        /// </summary>
        public void Play()
        {
            TryPlay();
        }
        
        /// <summary>
        /// Остановить текущий звук
        /// </summary>
        public void Stop()
        {
            if (_lastHandle.IsValid)
            {
                SoundManagerSystem.Stop(_lastHandle);
                _lastHandle = SoundHandle.Invalid;
            }
        }
        
        // === Internal ===
        
        private void TryPlay()
        {
            // PlayOnce check
            if (playOnce && _hasPlayed) return;
            
            // Cooldown check
            if (cooldown > 0 && Time.unscaledTime - _lastPlayTime < cooldown) return;
            
            // Stop previous
            if (stopPrevious && _lastHandle.IsValid)
            {
                SoundManagerSystem.Stop(_lastHandle);
            }
            
            // Play
            Vector3? position = useObjectPosition ? transform.position : null;
            _lastHandle = SoundManagerSystem.Play(soundId, position, volume);
            
            _lastPlayTime = Time.unscaledTime;
            _hasPlayed = true;
        }
        
        private bool CheckTag(GameObject obj)
        {
            if (string.IsNullOrEmpty(targetTag)) return true;
            return obj.CompareTag(targetTag);
        }
        
        // === Reset ===
        
        /// <summary>
        /// Сбросить состояние (для повторного использования)
        /// </summary>
        public void ResetState()
        {
            _hasPlayed = false;
            _lastPlayTime = -999f;
        }
    }
}
