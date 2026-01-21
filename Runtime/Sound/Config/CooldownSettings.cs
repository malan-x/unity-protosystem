using System;
using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Настройки защиты от спама звуков
    /// </summary>
    [Serializable]
    public class CooldownSettings
    {
        [Tooltip("Включить систему cooldown")]
        public bool enabled = true;
        
        [Tooltip("Минимальный интервал между одинаковыми звуками (сек)")]
        [Range(0f, 1f)]
        public float defaultCooldown = 0.05f;
        
        [Tooltip("Максимум одинаковых звуков одновременно")]
        [Range(1, 10)]
        public int maxSameSoundSimultaneous = 3;
    }
}
