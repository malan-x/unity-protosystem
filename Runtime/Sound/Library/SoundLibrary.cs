using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Главная библиотека звуков
    /// </summary>
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "ProtoSystem/Sound/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        [Header("Inline Sounds")]
        [Tooltip("Звуки, загруженные всегда (Core)")]
        public List<SoundEntry> coreEntries = new();
        
        [Header("Sound Banks")]
        [Tooltip("Банки звуков для ленивой загрузки")]
        public List<SoundBank> banks = new();
        
        // === Runtime Cache ===
        private Dictionary<string, SoundEntry> _cache;
        private HashSet<string> _loadedBankIds = new();
        private bool _isInitialized;
        
        /// <summary>
        /// Инициализировать библиотеку и построить кэш
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            
            _cache = new Dictionary<string, SoundEntry>();
            _loadedBankIds = new HashSet<string>();
            
            // Добавить core звуки
            foreach (var entry in coreEntries)
            {
                if (entry.IsValid() && !_cache.ContainsKey(entry.id))
                {
                    _cache[entry.id] = entry;
                }
            }
            
            // Загрузить банки, помеченные loadOnStartup
            foreach (var bank in banks)
            {
                if (bank != null && bank.loadOnStartup)
                {
                    LoadBankInternal(bank);
                }
            }
            
            _isInitialized = true;
        }
        
        /// <summary>
        /// Получить звук по ID
        /// </summary>
        public SoundEntry Get(string id)
        {
            if (!_isInitialized) Initialize();
            
            if (string.IsNullOrEmpty(id)) return null;
            
            return _cache.TryGetValue(id, out var entry) ? entry : null;
        }
        
        /// <summary>
        /// Проверить существование звука
        /// </summary>
        public bool Has(string id)
        {
            if (!_isInitialized) Initialize();
            return !string.IsNullOrEmpty(id) && _cache.ContainsKey(id);
        }
        
        /// <summary>
        /// Получить все звуки категории
        /// </summary>
        public IEnumerable<SoundEntry> GetByCategory(SoundCategory category)
        {
            if (!_isInitialized) Initialize();
            return _cache.Values.Where(e => e.category == category);
        }
        
        /// <summary>
        /// Получить все ID звуков
        /// </summary>
        public IEnumerable<string> GetAllIds()
        {
            if (!_isInitialized) Initialize();
            return _cache.Keys;
        }
        
        /// <summary>
        /// Получить все ID звуков категории
        /// </summary>
        public IEnumerable<string> GetIdsByCategory(SoundCategory category)
        {
            return GetByCategory(category).Select(e => e.id);
        }
        
        // === Bank Management ===
        
        /// <summary>
        /// Загрузить банк по ID
        /// </summary>
        public bool LoadBank(string bankId)
        {
            if (!_isInitialized) Initialize();
            
            if (_loadedBankIds.Contains(bankId)) return true;
            
            var bank = banks.Find(b => b != null && b.bankId == bankId);
            if (bank == null)
            {
                ProtoLogger.Log("sound_manager", LogCategory.Runtime, LogLevel.Warnings, $"Bank not found: {bankId}");
                return false;
            }
            
            LoadBankInternal(bank);
            return true;
        }
        
        /// <summary>
        /// Выгрузить банк по ID
        /// </summary>
        public void UnloadBank(string bankId)
        {
            if (!_isInitialized) return;
            if (!_loadedBankIds.Contains(bankId)) return;
            
            var bank = banks.Find(b => b != null && b.bankId == bankId);
            if (bank == null) return;
            
            // Удалить звуки банка из кэша
            foreach (var entry in bank.entries)
            {
                if (!string.IsNullOrEmpty(entry.id))
                {
                    _cache.Remove(entry.id);
                }
            }
            
            _loadedBankIds.Remove(bankId);
            ProtoLogger.Log("sound_manager", LogCategory.Runtime, LogLevel.Info, $"Unloaded bank: {bankId}");
        }
        
        /// <summary>
        /// Проверить загружен ли банк
        /// </summary>
        public bool IsBankLoaded(string bankId)
        {
            return _loadedBankIds.Contains(bankId);
        }
        
        /// <summary>
        /// Получить банки для сцены
        /// </summary>
        public IEnumerable<SoundBank> GetBanksForScene(string sceneName)
        {
            return banks.Where(b => b != null && b.ShouldLoadForScene(sceneName));
        }
        
        /// <summary>
        /// Получить все загруженные банки
        /// </summary>
        public IEnumerable<string> GetLoadedBankIds()
        {
            return _loadedBankIds;
        }
        
        private void LoadBankInternal(SoundBank bank)
        {
            if (bank == null) return;
            
            foreach (var entry in bank.entries)
            {
                if (entry.IsValid() && !_cache.ContainsKey(entry.id))
                {
                    _cache[entry.id] = entry;
                }
            }
            
            _loadedBankIds.Add(bank.bankId);
            ProtoLogger.Log("sound_manager", LogCategory.Runtime, LogLevel.Info, $"Loaded bank: {bank.bankId} ({bank.entries.Count} sounds)");
        }
        
        /// <summary>
        /// Сбросить кэш (для горячей перезагрузки в Editor)
        /// </summary>
        public void Reset()
        {
            _cache?.Clear();
            _loadedBankIds?.Clear();
            _isInitialized = false;
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Проверить дубликаты в core
            var ids = new HashSet<string>();
            
            foreach (var entry in coreEntries)
            {
                if (string.IsNullOrEmpty(entry.id)) continue;
                
                if (!ids.Add(entry.id))
                {
                    ProtoLogger.Log("sound_manager", LogCategory.Runtime, LogLevel.Warnings, $"Duplicate id in core: {entry.id}", this);
                }
            }
            
            // Проверить дубликаты в банках
            foreach (var bank in banks)
            {
                if (bank == null) continue;
                
                foreach (var entry in bank.entries)
                {
                    if (string.IsNullOrEmpty(entry.id)) continue;
                    
                    if (!ids.Add(entry.id))
                    {
                        ProtoLogger.Log("sound_manager", LogCategory.Runtime, LogLevel.Warnings, $"Duplicate id '{entry.id}' in bank: {bank.bankId}", this);
                    }
                }
            }
            
            // Сбросить кэш при изменениях в Editor
            Reset();
        }
#endif
    }
}
