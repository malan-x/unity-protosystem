using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Банк звуков для ленивой загрузки/выгрузки
    /// </summary>
    [CreateAssetMenu(fileName = "SoundBank", menuName = "ProtoSystem/Sound/Sound Bank")]
    public class SoundBank : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Уникальный ID банка")]
        public string bankId;
        
        [Tooltip("Описание банка")]
        [TextArea(2, 3)]
        public string description;
        
        [Header("Sounds")]
        [Tooltip("Звуки в этом банке")]
        public List<SoundEntry> entries = new();
        
        [Header("Auto-loading")]
        [Tooltip("Загружать при старте игры")]
        public bool loadOnStartup = false;
        
        [Tooltip("Загружать вместе с этими сценами")]
        public List<string> loadWithScenes = new();
        
        [Header("FMOD (опционально)")]
        [Tooltip("Путь к FMOD банку")]
        public string fmodBankPath;
        
        /// <summary>
        /// Проверить нужно ли загружать банк для сцены
        /// </summary>
        public bool ShouldLoadForScene(string sceneName)
        {
            return loadWithScenes.Contains(sceneName);
        }
        
        /// <summary>
        /// Получить все ID звуков в банке
        /// </summary>
        public IEnumerable<string> GetSoundIds()
        {
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.id))
                    yield return entry.id;
            }
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Автоматически заполнить bankId из имени ассета
            if (string.IsNullOrEmpty(bankId))
            {
                bankId = name;
            }
            
            // Проверить дубликаты ID
            var ids = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.id)) continue;
                
                if (!ids.Add(entry.id))
                {
                    ProtoLogger.Log("sound_manager", LogCategory.Runtime, LogLevel.Warnings, $"Duplicate sound id: {entry.id} in bank {bankId}", this);
                }
                
                // Установить bankId в entry
                entry.bankId = bankId;
            }
        }
#endif
    }
}
