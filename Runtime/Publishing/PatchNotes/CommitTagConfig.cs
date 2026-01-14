// Packages/com.protosystem.core/Runtime/Publishing/PatchNotes/CommitTagConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Publishing
{
    /// <summary>
    /// Конфигурация тега коммита для парсинга changelog
    /// </summary>
    [Serializable]
    public class CommitTag
    {
        [Tooltip("Тег в квадратных скобках, например: FIX, ADD, UPD")]
        public string tag = "FIX";
        
        [Tooltip("Отображаемое название в патчноутах")]
        public string displayName = "Bug Fixes";
        
        [Tooltip("Emoji или символ для отображения")]
        public string emoji = "🐛";
        
        [Tooltip("Приоритет сортировки (меньше = выше)")]
        public int sortOrder = 0;
        
        [Tooltip("Включать в публичные патчноуты")]
        public bool includeInPublic = true;
        
        [Tooltip("Цвет в редакторе")]
        public Color editorColor = Color.white;
    }

    /// <summary>
    /// Конфигурация тегов коммитов
    /// </summary>
    [CreateAssetMenu(fileName = "CommitTagConfig", menuName = "ProtoSystem/Publishing/Commit Tag Config")]
    public class CommitTagConfig : ScriptableObject
    {
        [Header("Теги коммитов")]
        [Tooltip("Список распознаваемых тегов. Формат в коммите: [TAG] описание")]
        public List<CommitTag> tags = new List<CommitTag>();

        [Header("Настройки парсинга")]
        [Tooltip("Регистронезависимый поиск тегов")]
        public bool caseInsensitive = true;
        
        [Tooltip("Разделитель между тегом и описанием")]
        public string separator = " ";

        /// <summary>
        /// Создать конфиг с тегами по умолчанию
        /// </summary>
        public static CommitTagConfig CreateDefault()
        {
            var config = CreateInstance<CommitTagConfig>();
            config.tags = GetDefaultTags();
            return config;
        }

        /// <summary>
        /// Теги по умолчанию
        /// </summary>
        public static List<CommitTag> GetDefaultTags()
        {
            return new List<CommitTag>
            {
                new CommitTag 
                { 
                    tag = "ADD", 
                    displayName = "New Features", 
                    emoji = "✨", 
                    sortOrder = 0,
                    includeInPublic = true,
                    editorColor = new Color(0.4f, 0.8f, 0.4f)
                },
                new CommitTag 
                { 
                    tag = "UPD", 
                    displayName = "Improvements", 
                    emoji = "💫", 
                    sortOrder = 1,
                    includeInPublic = true,
                    editorColor = new Color(0.4f, 0.6f, 1f)
                },
                new CommitTag 
                { 
                    tag = "FIX", 
                    displayName = "Bug Fixes", 
                    emoji = "🐛", 
                    sortOrder = 2,
                    includeInPublic = true,
                    editorColor = new Color(1f, 0.6f, 0.4f)
                },
                new CommitTag 
                { 
                    tag = "DEV", 
                    displayName = "Development", 
                    emoji = "🔧", 
                    sortOrder = 10,
                    includeInPublic = false,
                    editorColor = new Color(0.6f, 0.6f, 0.6f)
                },
                new CommitTag 
                { 
                    tag = "DOC", 
                    displayName = "Documentation", 
                    emoji = "📝", 
                    sortOrder = 5,
                    includeInPublic = false,
                    editorColor = new Color(0.8f, 0.8f, 0.4f)
                },
                new CommitTag 
                { 
                    tag = "BAL", 
                    displayName = "Balance Changes", 
                    emoji = "⚖️", 
                    sortOrder = 3,
                    includeInPublic = true,
                    editorColor = new Color(0.8f, 0.4f, 0.8f)
                }
            };
        }

        /// <summary>
        /// Найти тег по имени
        /// </summary>
        public CommitTag FindTag(string tagName)
        {
            if (string.IsNullOrEmpty(tagName)) return null;
            
            var comparison = caseInsensitive 
                ? StringComparison.OrdinalIgnoreCase 
                : StringComparison.Ordinal;
                
            return tags.Find(t => string.Equals(t.tag, tagName, comparison));
        }

        /// <summary>
        /// Получить все публичные теги отсортированные
        /// </summary>
        public List<CommitTag> GetPublicTagsSorted()
        {
            var result = tags.FindAll(t => t.includeInPublic);
            result.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
            return result;
        }
    }
}
