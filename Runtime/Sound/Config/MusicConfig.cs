using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Настройки музыкальной системы (кроссфейд, слои)
    /// </summary>
    [CreateAssetMenu(fileName = "MusicConfig", menuName = "ProtoSystem/Sound/Music Config")]
    public class MusicConfig : ScriptableObject
    {
        [Header("Crossfade")]
        [Tooltip("Время кроссфейда по умолчанию")]
        [Range(0f, 5f)]
        public float defaultCrossfadeTime = 2f;
        
        [Tooltip("Кривая кроссфейда")]
        public AnimationCurve crossfadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Vertical Layering")]
        [Tooltip("Музыкальные слои для динамического микширования")]
        public List<MusicLayerDefinition> layers = new();
        
        [Header("Parameters")]
        [Tooltip("Параметры для управления музыкой")]
        public List<MusicParameter> parameters = new()
        {
            new MusicParameter { name = "intensity", defaultValue = 0f },
            new MusicParameter { name = "danger", defaultValue = 0f }
        };
        
        /// <summary>
        /// Получить определение параметра
        /// </summary>
        public MusicParameter GetParameter(string name)
        {
            return parameters.Find(p => p.name == name);
        }
    }
    
    /// <summary>
    /// Определение музыкального слоя
    /// </summary>
    [Serializable]
    public class MusicLayerDefinition
    {
        [Tooltip("ID звука слоя")]
        public string soundId;
        
        [Tooltip("Параметр, управляющий слоем")]
        public string parameterName = "intensity";
        
        [Tooltip("Слой появляется при значении параметра выше этого")]
        [Range(0f, 1f)]
        public float minValue = 0f;
        
        [Tooltip("Слой на максимуме при значении параметра выше этого")]
        [Range(0f, 1f)]
        public float maxValue = 1f;
        
        [Tooltip("Кривая громкости слоя")]
        public AnimationCurve volumeCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        /// <summary>
        /// Рассчитать громкость слоя для значения параметра
        /// </summary>
        public float GetVolumeForParameter(float parameterValue)
        {
            if (parameterValue < minValue) return 0f;
            if (parameterValue >= maxValue) return 1f;
            
            float t = (parameterValue - minValue) / (maxValue - minValue);
            return volumeCurve.Evaluate(t);
        }
    }
    
    /// <summary>
    /// Параметр для управления музыкой
    /// </summary>
    [Serializable]
    public class MusicParameter
    {
        [Tooltip("Имя параметра")]
        public string name;
        
        [Tooltip("Значение по умолчанию")]
        [Range(0f, 1f)]
        public float defaultValue;
        
        [Tooltip("Плавный переход значения")]
        public bool smoothTransition = true;
        
        [Tooltip("Скорость перехода")]
        [Range(0.1f, 10f)]
        public float transitionSpeed = 2f;
    }
}
