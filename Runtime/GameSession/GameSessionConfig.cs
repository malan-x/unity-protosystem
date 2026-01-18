// Packages/com.protosystem.core/Runtime/GameSession/GameSessionConfig.cs
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Конфигурация системы игровых сессий
    /// </summary>
    [CreateAssetMenu(fileName = "GameSessionConfig", menuName = "ProtoSystem/Game Session Config")]
    public class GameSessionConfig : ScriptableObject
    {
        [Header("Startup")]
        [Tooltip("Автоматически запускать сессию после инициализации")]
        public bool autoStartSession = false;
        
        [Tooltip("Начальное состояние системы")]
        public SessionState initialState = SessionState.Ready;
        
        [Header("Restart")]
        [Tooltip("Задержка между сбросом и стартом (секунды)")]
        [Range(0f, 2f)]
        public float restartDelay = 0.1f;
        
        [Tooltip("Увеличивать счётчик рестартов при каждом RestartSession")]
        public bool trackRestarts = true;
        
        [Header("Debug")]
        [Tooltip("Логировать события сессии")]
        public bool logEvents = true;
        
        [Tooltip("Подробное логирование (все переходы состояний)")]
        public bool verboseLogging = false;
        
        [Header("Network")]
        [Tooltip("Синхронизировать состояние сессии по сети")]
        public bool syncOverNetwork = true;
        
        [Tooltip("Только хост может управлять сессией")]
        public bool hostAuthoritative = true;
    }
}
