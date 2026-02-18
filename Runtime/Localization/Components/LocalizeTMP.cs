// Packages/com.protosystem.core/Runtime/Localization/Components/LocalizeTMP.cs
using UnityEngine;
using TMPro;

namespace ProtoSystem
{
    /// <summary>
    /// Автоматическая локализация TMP_Text компонента.
    /// Подставляет перевод по ключу при старте и при смене языка.
    /// 
    /// Добавляется на GameObject с TMP_Text.
    /// В инспекторе указывается таблица и ключ.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("ProtoSystem/Localization/Localize TMP")]
    public class LocalizeTMP : MonoBehaviour
    {
        [Header("Localization")]
        [Tooltip("Таблица строк (по умолчанию: UI)")]
        [SerializeField] private string table = "UI";
        
        [Tooltip("Ключ строки в таблице")]
        [SerializeField] private string key;
        
        [Tooltip("Fallback текст если перевод не найден")]
        [SerializeField] private string fallback;
        
        [Header("Formatting")]
        [Tooltip("Привести к верхнему регистру")]
        [SerializeField] private bool toUpperCase;
        
        private TMP_Text _text;
        
        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }
        
        private void OnEnable()
        {
            EventBus.Subscribe(EventBus.Localization.LanguageChanged, OnLanguageChanged);
            EventBus.Subscribe(EventBus.Localization.Ready, OnReady);
            UpdateText();
        }
        
        private void OnDisable()
        {
            EventBus.Unsubscribe(EventBus.Localization.LanguageChanged, OnLanguageChanged);
            EventBus.Unsubscribe(EventBus.Localization.Ready, OnReady);
        }
        
        private void OnLanguageChanged(object payload) => UpdateText();
        private void OnReady(object payload) => UpdateText();
        
        /// <summary>
        /// Обновить текст из текущей локализации.
        /// </summary>
        public void UpdateText()
        {
            if (_text == null || string.IsNullOrEmpty(key)) return;
            
            string resolved;
            if (!string.IsNullOrEmpty(table) && table != "UI")
                resolved = !string.IsNullOrEmpty(fallback) 
                    ? Loc.From(table, key, fallback) 
                    : Loc.From(table, key);
            else
                resolved = !string.IsNullOrEmpty(fallback) 
                    ? Loc.Get(key, fallback) 
                    : Loc.Get(key);
            
            if (toUpperCase)
                resolved = resolved.ToUpperInvariant();
            
            _text.text = resolved;
        }
        
        /// <summary>
        /// Сменить ключ в рантайме и обновить текст.
        /// </summary>
        public void SetKey(string newKey, string newFallback = null)
        {
            key = newKey;
            if (newFallback != null) fallback = newFallback;
            UpdateText();
        }
        
        /// <summary>
        /// Сменить таблицу и ключ в рантайме.
        /// </summary>
        public void SetKey(string newTable, string newKey, string newFallback = null)
        {
            table = newTable;
            key = newKey;
            if (newFallback != null) fallback = newFallback;
            UpdateText();
        }
    }
}
