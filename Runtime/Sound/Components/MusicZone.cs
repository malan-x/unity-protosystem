using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Тип действия зоны
    /// </summary>
    public enum MusicZoneType
    {
        /// <summary>Сменить музыку</summary>
        Music,
        
        /// <summary>Активировать snapshot</summary>
        Snapshot,
        
        /// <summary>И музыку, и snapshot</summary>
        Both
    }
    
    /// <summary>
    /// Зона смены музыки и/или snapshot при входе игрока
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MusicZone : MonoBehaviour
    {
        [Header("Zone Type")]
        [Tooltip("Тип действия зоны")]
        public MusicZoneType type = MusicZoneType.Music;
        
        [Header("Music")]
        [SoundId(SoundCategory.Music)]
        [Tooltip("ID музыкального трека")]
        public string musicId;
        
        [Tooltip("Время кроссфейда")]
        public float fadeTime = 2f;
        
        [Header("Snapshot")]
        [Tooltip("Snapshot для активации")]
        public SoundSnapshotId snapshot;
        
        [Tooltip("Время перехода snapshot")]
        public float snapshotTransitionTime = 0.5f;
        
        [Header("Trigger")]
        [Tooltip("Тег объекта для активации")]
        public string playerTag = "Player";
        
        [Header("Behavior")]
        [Tooltip("Восстановить предыдущую музыку при выходе")]
        public bool restoreOnExit = false;
        
        [Tooltip("Деактивировать snapshot при выходе")]
        public bool clearSnapshotOnExit = true;
        
        // Runtime
        private string _previousMusicId;
        private bool _isInside;
        
        private void Start()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!CheckTag(other.gameObject)) return;
            if (_isInside) return;
            
            _isInside = true;
            
            // Запомнить текущую музыку для восстановления
            // TODO: Получить текущий music id из SoundManagerSystem
            // _previousMusicId = SoundManagerSystem.CurrentMusicId;
            
            // Музыка
            if ((type == MusicZoneType.Music || type == MusicZoneType.Both) && !string.IsNullOrEmpty(musicId))
            {
                SoundManagerSystem.CrossfadeMusic(musicId, fadeTime);
            }
            
            // Snapshot
            if ((type == MusicZoneType.Snapshot || type == MusicZoneType.Both) && !snapshot.IsEmpty)
            {
                SoundManagerSystem.SetSnapshot(snapshot, snapshotTransitionTime);
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (!CheckTag(other.gameObject)) return;
            if (!_isInside) return;
            
            _isInside = false;
            
            // Восстановить музыку
            if (restoreOnExit && !string.IsNullOrEmpty(_previousMusicId))
            {
                SoundManagerSystem.CrossfadeMusic(_previousMusicId, fadeTime);
            }
            
            // Деактивировать snapshot
            if (clearSnapshotOnExit && !snapshot.IsEmpty)
            {
                SoundManagerSystem.ClearSnapshot(snapshot, snapshotTransitionTime);
            }
        }
        
        private bool CheckTag(GameObject obj)
        {
            if (string.IsNullOrEmpty(playerTag)) return true;
            return obj.CompareTag(playerTag);
        }
        
        // 2D support
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!CheckTag(other.gameObject)) return;
            if (_isInside) return;
            
            _isInside = true;
            
            if ((type == MusicZoneType.Music || type == MusicZoneType.Both) && !string.IsNullOrEmpty(musicId))
            {
                SoundManagerSystem.CrossfadeMusic(musicId, fadeTime);
            }
            
            if ((type == MusicZoneType.Snapshot || type == MusicZoneType.Both) && !snapshot.IsEmpty)
            {
                SoundManagerSystem.SetSnapshot(snapshot, snapshotTransitionTime);
            }
        }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            if (!CheckTag(other.gameObject)) return;
            if (!_isInside) return;
            
            _isInside = false;
            
            if (restoreOnExit && !string.IsNullOrEmpty(_previousMusicId))
            {
                SoundManagerSystem.CrossfadeMusic(_previousMusicId, fadeTime);
            }
            
            if (clearSnapshotOnExit && !snapshot.IsEmpty)
            {
                SoundManagerSystem.ClearSnapshot(snapshot, snapshotTransitionTime);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            Color color = type switch
            {
                MusicZoneType.Music => new Color(0.8f, 0.3f, 0.8f, 0.3f),
                MusicZoneType.Snapshot => new Color(0.3f, 0.3f, 0.8f, 0.3f),
                MusicZoneType.Both => new Color(0.8f, 0.3f, 0.3f, 0.3f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.3f)
            };
            
            Gizmos.color = color;
            
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
