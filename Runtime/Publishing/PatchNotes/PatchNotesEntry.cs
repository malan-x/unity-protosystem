// Packages/com.protosystem.core/Runtime/Publishing/PatchNotes/PatchNotesEntry.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProtoSystem.Publishing
{
    /// <summary>
    /// Статус публикации на площадке
    /// </summary>
    [Serializable]
    public class PlatformPublishStatus
    {
        public string platformId;
        public bool isPublished;
        public string publishedBranch;
        public string publishDateStr; // Сериализуемая дата
        public string buildId;
        
        public DateTime PublishDate
        {
            get => DateTime.TryParse(publishDateStr, out var dt) ? dt : DateTime.MinValue;
            set => publishDateStr = value.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    /// <summary>
    /// Запись из коммита
    /// </summary>
    [Serializable]
    public class CommitEntry
    {
        [Tooltip("Теги записи (может быть несколько)")]
        public List<string> tags = new List<string>();
        
        [Tooltip("Текст записи")]
        public string message;
        
        [Tooltip("Хеш коммита")]
        public string commitHash;
        
        [Tooltip("Короткий хеш коммита")]
        public string shortHash;
        
        [Tooltip("Автор коммита")]
        public string author;
        
        [Tooltip("Дата коммита")]
        public string date;
        
        [Tooltip("Включить в публичные патчноуты")]
        public bool includeInPublic = true;
    }

    /// <summary>
    /// Одна версия в истории патчноутов
    /// </summary>
    [Serializable]
    public class PatchNotesEntry
    {
        [Header("Версия")]
        [Tooltip("Версия в формате SemVer (1.0.0)")]
        public string version = "1.0.0";
        
        [Tooltip("Дата создания")]
        public string date;
        
        [Tooltip("Заголовок релиза")]
        public string title;

        [Header("Содержимое")]
        [TextArea(5, 20)]
        [Tooltip("Содержимое патчноутов (Markdown)")]
        public string content;
        
        [Tooltip("Записи из коммитов")]
        public List<CommitEntry> commitEntries = new List<CommitEntry>();

        [Header("Git")]
        [Tooltip("Git тег")]
        public string gitTag;
        
        [Tooltip("Список хешей связанных коммитов")]
        public List<string> commitHashes = new List<string>();
        
        [Tooltip("Список авторов")]
        public List<string> authors = new List<string>();

        [Header("Публикация")]
        [Tooltip("Статусы публикации по площадкам")]
        public List<PlatformPublishStatus> publishStatuses = new List<PlatformPublishStatus>();

        /// <summary>
        /// Создать новую запись
        /// </summary>
        public static PatchNotesEntry Create(string version)
        {
            return new PatchNotesEntry
            {
                version = version,
                date = DateTime.Now.ToString("yyyy-MM-dd"),
                title = $"Version {version}",
                content = ""
            };
        }

        /// <summary>
        /// Проверить, опубликовано ли на площадке
        /// </summary>
        public bool IsPublishedOn(string platformId)
        {
            var status = publishStatuses.Find(s => s.platformId == platformId);
            return status?.isPublished ?? false;
        }

        /// <summary>
        /// Отметить как опубликованное
        /// </summary>
        public void MarkPublished(string platformId, string branch, string buildId = null)
        {
            var status = publishStatuses.Find(s => s.platformId == platformId);
            if (status == null)
            {
                status = new PlatformPublishStatus { platformId = platformId };
                publishStatuses.Add(status);
            }
            
            status.isPublished = true;
            status.publishedBranch = branch;
            status.PublishDate = DateTime.Now;
            status.buildId = buildId;
        }

        /// <summary>
        /// Собрать уникальных авторов из записей коммитов
        /// </summary>
        public void CollectAuthors()
        {
            authors = commitEntries
                .Where(e => !string.IsNullOrEmpty(e.author))
                .Select(e => e.author)
                .Distinct()
                .OrderBy(a => a)
                .ToList();
        }

        /// <summary>
        /// Собрать хеши коммитов
        /// </summary>
        public void CollectCommitHashes()
        {
            commitHashes = commitEntries
                .Where(e => !string.IsNullOrEmpty(e.commitHash))
                .Select(e => e.commitHash)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Сгенерировать содержимое из записей коммитов
        /// </summary>
        public void GenerateContentFromCommits(CommitTagConfig tagConfig, bool publicOnly = true)
        {
            if (commitEntries == null || commitEntries.Count == 0)
            {
                content = "";
                return;
            }

            // Собираем авторов и хеши
            CollectAuthors();
            CollectCommitHashes();

            var groupedByTag = new Dictionary<string, List<CommitEntry>>();
            
            foreach (var entry in commitEntries)
            {
                if (publicOnly && !entry.includeInPublic) continue;
                
                // Если нет тегов, пропускаем
                if (entry.tags == null || entry.tags.Count == 0) continue;
                
                foreach (var tagName in entry.tags)
                {
                    var tag = tagConfig?.FindTag(tagName);
                    if (publicOnly && tag != null && !tag.includeInPublic) continue;
                    
                    var key = tag?.displayName ?? tagName;
                    if (!groupedByTag.ContainsKey(key))
                        groupedByTag[key] = new List<CommitEntry>();
                    
                    // Избегаем дубликатов сообщений в одной группе
                    if (!groupedByTag[key].Any(e => e.message == entry.message))
                    {
                        groupedByTag[key].Add(entry);
                    }
                }
            }

            var sb = new System.Text.StringBuilder();
            
            // Сортируем группы по приоритету тегов
            var sortedGroups = new List<(string name, List<CommitEntry> entries, int order)>();
            foreach (var kvp in groupedByTag)
            {
                var tag = tagConfig?.tags.Find(t => t.displayName == kvp.Key);
                var order = tag?.sortOrder ?? 100;
                sortedGroups.Add((kvp.Key, kvp.Value, order));
            }
            sortedGroups.Sort((a, b) => a.order.CompareTo(b.order));

            foreach (var group in sortedGroups)
            {
                var tag = tagConfig?.tags.Find(t => t.displayName == group.name);
                var emoji = tag?.emoji ?? "•";
                
                sb.AppendLine($"### {emoji} {group.name}");
                sb.AppendLine();
                
                foreach (var entry in group.entries)
                {
                    sb.AppendLine($"- {entry.message}");
                }
                sb.AppendLine();
            }

            content = sb.ToString().TrimEnd();
        }
    }
}
