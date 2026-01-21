using System;
using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Настройки громкости по категориям
    /// </summary>
    [Serializable]
    public class VolumeSettings
    {
        [Range(0, 1), Tooltip("Общая громкость")]
        public float master = 1f;
        
        [Range(0, 1), Tooltip("Громкость музыки")]
        public float music = 0.8f;
        
        [Range(0, 1), Tooltip("Громкость звуковых эффектов")]
        public float sfx = 1f;
        
        [Range(0, 1), Tooltip("Громкость голоса/диалогов")]
        public float voice = 1f;
        
        [Range(0, 1), Tooltip("Громкость фоновых звуков")]
        public float ambient = 0.9f;
        
        [Range(0, 1), Tooltip("Громкость звуков интерфейса")]
        public float ui = 1f;
        
        /// <summary>
        /// Получить громкость по категории
        /// </summary>
        public float GetVolume(SoundCategory category)
        {
            return category switch
            {
                SoundCategory.Master => master,
                SoundCategory.Music => music,
                SoundCategory.SFX => sfx,
                SoundCategory.Voice => voice,
                SoundCategory.Ambient => ambient,
                SoundCategory.UI => ui,
                _ => 1f
            };
        }
        
        /// <summary>
        /// Установить громкость по категории
        /// </summary>
        public void SetVolume(SoundCategory category, float value)
        {
            value = Mathf.Clamp01(value);
            
            switch (category)
            {
                case SoundCategory.Master: master = value; break;
                case SoundCategory.Music: music = value; break;
                case SoundCategory.SFX: sfx = value; break;
                case SoundCategory.Voice: voice = value; break;
                case SoundCategory.Ambient: ambient = value; break;
                case SoundCategory.UI: ui = value; break;
            }
        }
        
        /// <summary>
        /// Создать копию настроек
        /// </summary>
        public VolumeSettings Clone()
        {
            return new VolumeSettings
            {
                master = master,
                music = music,
                sfx = sfx,
                voice = voice,
                ambient = ambient,
                ui = ui
            };
        }
    }
}
