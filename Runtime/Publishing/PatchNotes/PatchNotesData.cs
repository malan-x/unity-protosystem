// Packages/com.protosystem.core/Runtime/Publishing/PatchNotes/PatchNotesData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Publishing
{
    /// <summary>
    /// Шаблон патчноутов
    /// </summary>
    [Serializable]
    public class PatchNotesTemplate
    {
        public string name = "Default";
        
        [TextArea(10, 20)]
        public string template = @"## Version {version}
*Released: {date}*

{content}

---
Thank you for playing!";
    }

    /// <summary>
    /// Хранилище истории патчноутов
    /// </summary>
    [CreateAssetMenu(fileName = "PatchNotesData", menuName = "ProtoSystem/Publishing/Patch Notes Data")]
    public class PatchNotesData : ScriptableObject
    {
        [Header("Версионирование")]
        [Tooltip("Текущая версия проекта")]
        public string currentVersion = "1.0.0";
        
        [Tooltip("Автоматически синхронизировать с PlayerSettings")]
        public bool syncWithPlayerSettings = true;

        [Header("Конфигурация тегов")]
        [Tooltip("Конфигурация тегов коммитов")]
        public CommitTagConfig tagConfig;

        [Header("Шаблоны")]
        [Tooltip("Доступные шаблоны патчноутов")]
        public List<PatchNotesTemplate> templates = new List<PatchNotesTemplate>();
        
        [Tooltip("Индекс шаблона по умолчанию")]
        public int defaultTemplateIndex = 0;

        [Header("История")]
        [Tooltip("Все версии патчноутов")]
        public List<PatchNotesEntry> entries = new List<PatchNotesEntry>();

        /// <summary>
        /// Создать с настройками по умолчанию
        /// </summary>
        public static PatchNotesData CreateDefault()
        {
            var data = CreateInstance<PatchNotesData>();
            data.templates = GetDefaultTemplates();
            return data;
        }

        /// <summary>
        /// Шаблоны по умолчанию
        /// </summary>
        public static List<PatchNotesTemplate> GetDefaultTemplates()
        {
            return new List<PatchNotesTemplate>
            {
                new PatchNotesTemplate
                {
                    name = "Minimal",
                    template = @"# {version}

{content}"
                },
                new PatchNotesTemplate
                {
                    name = "Standard",
                    template = @"## Version {version}
*Released: {date}*

{content}

---
Thank you for playing!"
                },
                new PatchNotesTemplate
                {
                    name = "Detailed",
                    template = @"# 🎮 {title}

**Version:** {version}
**Release Date:** {date}
**Build:** {buildId}

---

{content}

---

## 📋 Full Changelog
{gitCommits}

---
*Thank you for your support!*"
                },
                new PatchNotesTemplate
                {
                    name = "Steam BBCode",
                    template = @"[h1]{title}[/h1]

[b]Version:[/b] {version}
[b]Date:[/b] {date}

{content:bbcode}

[hr][/hr]
Thank you for playing!"
                }
            };
        }

        /// <summary>
        /// Получить текущую запись или создать новую
        /// </summary>
        public PatchNotesEntry GetOrCreateCurrent()
        {
            var entry = entries.Find(e => e.version == currentVersion);
            if (entry == null)
            {
                entry = PatchNotesEntry.Create(currentVersion);
                entries.Insert(0, entry);
            }
            return entry;
        }

        /// <summary>
        /// Получить запись по версии
        /// </summary>
        public PatchNotesEntry GetEntry(string version)
        {
            return entries.Find(e => e.version == version);
        }

        /// <summary>
        /// Инкрементировать версию
        /// </summary>
        public string IncrementVersion(VersionIncrement increment)
        {
            var parts = currentVersion.Split('.');
            if (parts.Length != 3)
            {
                currentVersion = "1.0.0";
                return currentVersion;
            }

            int.TryParse(parts[0], out int major);
            int.TryParse(parts[1], out int minor);
            int.TryParse(parts[2], out int patch);

            switch (increment)
            {
                case VersionIncrement.Major:
                    major++;
                    minor = 0;
                    patch = 0;
                    break;
                case VersionIncrement.Minor:
                    minor++;
                    patch = 0;
                    break;
                case VersionIncrement.Patch:
                    patch++;
                    break;
            }

            currentVersion = $"{major}.{minor}.{patch}";
            return currentVersion;
        }

        /// <summary>
        /// Применить шаблон к записи
        /// </summary>
        public string ApplyTemplate(PatchNotesEntry entry, int templateIndex = -1)
        {
            if (templateIndex < 0) templateIndex = defaultTemplateIndex;
            if (templateIndex >= templates.Count) return entry.content;

            var template = templates[templateIndex].template;
            
            return template
                .Replace("{version}", entry.version)
                .Replace("{date}", entry.date)
                .Replace("{title}", entry.title ?? $"Version {entry.version}")
                .Replace("{content}", entry.content)
                .Replace("{gitTag}", entry.gitTag ?? "")
                .Replace("{buildId}", ""); // Заполняется при публикации
        }

        /// <summary>
        /// Получить последнюю опубликованную версию для площадки
        /// </summary>
        public PatchNotesEntry GetLastPublished(string platformId)
        {
            return entries.Find(e => e.IsPublishedOn(platformId));
        }

        /// <summary>
        /// Получить все неопубликованные версии для площадки
        /// </summary>
        public List<PatchNotesEntry> GetUnpublished(string platformId)
        {
            return entries.FindAll(e => !e.IsPublishedOn(platformId));
        }
    }

    /// <summary>
    /// Тип инкремента версии
    /// </summary>
    public enum VersionIncrement
    {
        Patch,  // 1.0.X
        Minor,  // 1.X.0
        Major   // X.0.0
    }
}
