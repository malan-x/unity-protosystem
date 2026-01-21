using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Настройки приоритетов звуков
    /// </summary>
    [Serializable]
    public class PrioritySettings
    {
        [Tooltip("Включить систему приоритетов")]
        public bool enabled = true;
        
        [Tooltip("Приоритет по умолчанию для новых звуков")]
        public SoundPriority defaultPriority = SoundPriority.Normal;
        
        [Tooltip("Порядок важности категорий (первая = самая важная)")]
        public List<SoundCategory> categoryPriorityOrder = new()
        {
            SoundCategory.Voice,
            SoundCategory.UI,
            SoundCategory.SFX,
            SoundCategory.Ambient,
            SoundCategory.Music
        };
        
        /// <summary>
        /// Получить приоритет категории (меньше = важнее)
        /// </summary>
        public int GetCategoryPriority(SoundCategory category)
        {
            int index = categoryPriorityOrder.IndexOf(category);
            return index >= 0 ? index : categoryPriorityOrder.Count;
        }
    }
}
