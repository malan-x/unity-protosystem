using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Схема звуков для GameSession системы
    /// </summary>
    [CreateAssetMenu(fileName = "GameSessionSoundScheme", menuName = "ProtoSystem/Sound/Game Session Sound Scheme")]
    public class GameSessionSoundScheme : ScriptableObject
    {
        [Header("Music")]
        [Tooltip("Музыка главного меню")]
        public string menuMusic = "music_menu";
        
        [Tooltip("Музыка геймплея")]
        public string gameplayMusic = "music_gameplay";
        
        [Tooltip("Музыка паузы (пусто = продолжать gameplay)")]
        public string pauseMusic;
        
        [Tooltip("Музыка победы")]
        public string victoryMusic = "music_victory";
        
        [Tooltip("Музыка поражения")]
        public string defeatMusic = "music_defeat";
        
        [Header("Stingers (короткие акценты)")]
        [Tooltip("Акцент при старте сессии")]
        public string sessionStartStinger = "stinger_start";
        
        [Tooltip("Акцент при победе")]
        public string victoryStinger = "stinger_victory";
        
        [Tooltip("Акцент при поражении")]
        public string defeatStinger = "stinger_defeat";
        
        [Tooltip("Акцент при чекпоинте")]
        public string checkpointStinger = "stinger_checkpoint";
        
        [Header("Transitions")]
        [Tooltip("Время кроссфейда между треками")]
        [Range(0f, 5f)]
        public float musicFadeTime = 2f;
        
        [Tooltip("Приглушение музыки во время stinger (0 = без приглушения)")]
        [Range(0f, 1f)]
        public float stingerDuckAmount = 0.5f;
        
        [Tooltip("Длительность приглушения")]
        [Range(0f, 3f)]
        public float stingerDuckDuration = 1f;
        
        [Header("Snapshots")]
        [Tooltip("Snapshot для паузы")]
        public SoundSnapshotId pauseSnapshot = SoundSnapshotPreset.Paused;
        
        [Tooltip("Snapshot для game over")]
        public SoundSnapshotId gameOverSnapshot;
        
        [Header("Per-State Overrides")]
        [Tooltip("Переопределения для конкретных состояний сессии")]
        public List<SessionStateSoundOverride> stateOverrides = new();
        
        /// <summary>
        /// Получить музыку для состояния
        /// </summary>
        public string GetMusicForState(SessionState state)
        {
            var over = stateOverrides.Find(o => o.state == state);
            if (over != null && !string.IsNullOrEmpty(over.music))
                return over.music;
            
            return state switch
            {
                SessionState.None => menuMusic,
                SessionState.Ready => menuMusic,
                SessionState.Starting => gameplayMusic,
                SessionState.Playing => gameplayMusic,
                SessionState.Paused => string.IsNullOrEmpty(pauseMusic) ? null : pauseMusic,
                SessionState.GameOver => defeatMusic,
                SessionState.Victory => victoryMusic,
                _ => null
            };
        }
        
        /// <summary>
        /// Получить snapshot для состояния
        /// </summary>
        public SoundSnapshotId GetSnapshotForState(SessionState state)
        {
            var over = stateOverrides.Find(o => o.state == state);
            if (over != null && !over.snapshot.IsEmpty)
                return over.snapshot;
            
            return state switch
            {
                SessionState.Paused => pauseSnapshot,
                SessionState.GameOver => gameOverSnapshot,
                _ => default
            };
        }
    }
    
    /// <summary>
    /// Переопределение звуков для состояния сессии
    /// </summary>
    [Serializable]
    public class SessionStateSoundOverride
    {
        public SessionState state;
        
        [Tooltip("Музыка для состояния")]
        public string music;
        
        [Tooltip("Snapshot для состояния")]
        public SoundSnapshotId snapshot;
    }
}
