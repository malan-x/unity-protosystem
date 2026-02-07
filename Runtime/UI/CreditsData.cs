// Packages/com.protosystem.core/Runtime/UI/CreditsData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Данные для окна Credits - авторы, роли, благодарности.
    /// Поддерживает два режима:
    /// 1) Legacy: roles + authors + specialThanks (обратная совместимость)
    /// 2) Sections: типизированные секции с enabled-флагом
    /// Если sections непустой — используется он. Иначе — legacy.
    /// </summary>
    [CreateAssetMenu(fileName = "CreditsData", menuName = "ProtoSystem/UI/Credits Data", order = 100)]
    public class CreditsData : ScriptableObject
    {
        // ═══════════════════════════════════════════════════════════════════
        // SECTIONS MODE (новый)
        // ═══════════════════════════════════════════════════════════════════

        [Header("Секции титров")]
        [Tooltip("Типизированные секции. Если список непуст — используется вместо legacy-полей.")]
        public List<CreditsSection> sections = new();

        // ═══════════════════════════════════════════════════════════════════
        // LEGACY MODE (обратная совместимость)
        // ═══════════════════════════════════════════════════════════════════

        [Header("Legacy: Роли (порядок отображения)")]
        [Tooltip("Список ролей в порядке отображения")]
        public List<RoleDefinition> roles = new();

        [Header("Legacy: Авторы")]
        [Tooltip("Список авторов с привязкой к ролям")]
        public List<AuthorEntry> authors = new();

        [Header("Legacy: Благодарности")]
        [Tooltip("Список специальных благодарностей")]
        public List<ThanksEntry> specialThanks = new();

        // ═══════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Используется ли новый sections-режим
        /// </summary>
        public bool UseSections => sections != null && sections.Count > 0;

        /// <summary>
        /// Возвращает только включённые секции
        /// </summary>
        public List<CreditsSection> GetEnabledSections()
        {
            if (sections == null) return new List<CreditsSection>();
            return sections.FindAll(s => s.enabled);
        }

        /// <summary>
        /// Получить авторов по роли (legacy)
        /// </summary>
        public List<AuthorEntry> GetAuthorsByRole(string roleId)
        {
            return authors.FindAll(a => a.roleIds != null && a.roleIds.Contains(roleId));
        }

        /// <summary>
        /// Получить все роли автора (legacy)
        /// </summary>
        public List<RoleDefinition> GetRolesForAuthor(AuthorEntry author)
        {
            if (author.roleIds == null) return new List<RoleDefinition>();
            return roles.FindAll(r => author.roleIds.Contains(r.id));
        }

        /// <summary>
        /// Генерирует форматированный текст для CreditsWindow.
        /// Sections mode → секции, Legacy mode → роли/авторы.
        /// </summary>
        public string GenerateCreditsText()
        {
            if (UseSections)
                return GenerateFromSections();
            return GenerateFromLegacy();
        }

        // ═══════════════════════════════════════════════════════════════════
        // GENERATION
        // ═══════════════════════════════════════════════════════════════════

        private string GenerateFromSections()
        {
            var sb = new System.Text.StringBuilder();

            foreach (var section in sections)
            {
                if (!section.enabled) continue;

                switch (section.type)
                {
                    case CreditsSectionType.Header:
                        GenerateHeader(sb, section);
                        break;
                    case CreditsSectionType.Team:
                        GenerateTeam(sb, section);
                        break;
                    case CreditsSectionType.Technology:
                        GenerateTechnology(sb, section);
                        break;
                    case CreditsSectionType.SimpleList:
                        GenerateSimpleList(sb, section);
                        break;
                    case CreditsSectionType.Quote:
                        GenerateQuote(sb, section);
                        break;
                    case CreditsSectionType.Logo:
                        GenerateLogo(sb, section);
                        break;
                }

                if (section.showDividerAfter)
                    sb.AppendLine("\n<color=#3a3d42>───────</color>\n");
            }

            return sb.ToString();
        }

        private void GenerateHeader(System.Text.StringBuilder sb, CreditsSection section)
        {
            if (section.persons != null && section.persons.Count > 0)
            {
                var p = section.persons[0];
                sb.AppendLine($"<size=32><b>{p.name}</b></size>");
                if (!string.IsNullOrEmpty(p.role))
                    sb.AppendLine($"<size=14>{p.role}</size>");
            }
            else if (!string.IsNullOrEmpty(section.title))
            {
                sb.AppendLine($"<size=32><b>{section.title}</b></size>");
            }
            sb.AppendLine();
        }

        private void GenerateTeam(System.Text.StringBuilder sb, CreditsSection section)
        {
            if (!string.IsNullOrEmpty(section.title))
                sb.AppendLine($"<size=16><color=#c9a96e>{section.title}</color></size>");

            if (section.persons != null)
            {
                foreach (var p in section.persons)
                {
                    sb.AppendLine($"<size=22><b>{p.name}</b></size>");
                    if (!string.IsNullOrEmpty(p.role))
                        sb.AppendLine($"<size=12>{p.role}</size>");
                    sb.AppendLine();
                }
            }
        }

        private void GenerateTechnology(System.Text.StringBuilder sb, CreditsSection section)
        {
            if (!string.IsNullOrEmpty(section.title))
                sb.AppendLine($"<size=16><color=#c9a96e>{section.title}</color></size>");

            if (section.tags != null && section.tags.Count > 0)
                sb.AppendLine(string.Join("  ·  ", section.tags));
            sb.AppendLine();
        }

        private void GenerateSimpleList(System.Text.StringBuilder sb, CreditsSection section)
        {
            if (!string.IsNullOrEmpty(section.title))
                sb.AppendLine($"<size=16><color=#c9a96e>{section.title}</color></size>");

            if (section.items != null)
            {
                foreach (var item in section.items)
                    sb.AppendLine(item);
            }
            sb.AppendLine();
        }

        private void GenerateQuote(System.Text.StringBuilder sb, CreditsSection section)
        {
            if (!string.IsNullOrEmpty(section.quoteText))
            {
                sb.AppendLine($"\n<i>{section.quoteText}</i>");
                if (!string.IsNullOrEmpty(section.quoteAttribution))
                    sb.AppendLine($"<size=12>— {section.quoteAttribution}</size>");
            }
            sb.AppendLine();
        }

        private void GenerateLogo(System.Text.StringBuilder sb, CreditsSection section)
        {
            if (section.persons != null && section.persons.Count > 0)
            {
                var p = section.persons[0];
                sb.AppendLine($"\n<size=28><b>{p.name}</b></size>");
                if (!string.IsNullOrEmpty(p.role))
                    sb.AppendLine($"<size=28><color=#e8a033>{p.role}</color></size>");
            }
            if (!string.IsNullOrEmpty(section.logoYear))
                sb.AppendLine($"<size=12>© {section.logoYear}</size>");
            sb.AppendLine();
        }

        private string GenerateFromLegacy()
        {
            var sb = new System.Text.StringBuilder();

            foreach (var role in roles)
            {
                var roleAuthors = GetAuthorsByRole(role.id);
                if (roleAuthors.Count == 0) continue;

                sb.AppendLine($"<size=24><b>{role.displayName}</b></size>");
                
                foreach (var author in roleAuthors)
                {
                    sb.AppendLine(author.name);
                }
                
                sb.AppendLine();
            }

            if (specialThanks.Count > 0)
            {
                sb.AppendLine("<size=24><b>Благодарности</b></size>");
                
                foreach (var thanks in specialThanks)
                {
                    if (!string.IsNullOrEmpty(thanks.category))
                    {
                        sb.AppendLine($"<i>{thanks.category}</i>");
                    }
                    sb.AppendLine(thanks.text);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // SECTIONS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Тип секции титров
    /// </summary>
    public enum CreditsSectionType
    {
        Header,      // Название игры + подзаголовок
        Team,        // Люди с именами и ролями
        Technology,  // Сетка тегов
        SimpleList,  // Список строк
        Quote,       // Цитата с атрибуцией
        Logo,        // Финальный логотип + год
    }

    /// <summary>
    /// Секция титров с enabled-флагом
    /// </summary>
    [Serializable]
    public class CreditsSection
    {
        [Tooltip("Включена ли секция")]
        public bool enabled = true;

        [Tooltip("Тип секции определяет визуальное отображение")]
        public CreditsSectionType type = CreditsSectionType.Team;

        [Tooltip("Заголовок секции")]
        public string title;

        [Tooltip("Показывать разделитель после секции")]
        public bool showDividerAfter = true;

        // ── Team / Header / Logo ──
        [Tooltip("Люди (для Team, Header, Logo)")]
        public List<CreditsPerson> persons = new();

        // ── Technology ──
        [Tooltip("Теги (для Technology)")]
        public List<string> tags = new();

        // ── SimpleList ──
        [Tooltip("Элементы списка (для SimpleList)")]
        public List<string> items = new();

        // ── Quote ──
        [TextArea(2, 4)]
        [Tooltip("Текст цитаты")]
        public string quoteText;

        [Tooltip("Автор цитаты")]
        public string quoteAttribution;

        // ── Logo ──
        [Tooltip("Год для логотипа")]
        public string logoYear = "2026";
    }

    /// <summary>
    /// Персона в титрах (для секций)
    /// </summary>
    [Serializable]
    public class CreditsPerson
    {
        [Tooltip("Имя")]
        public string name;

        [Tooltip("Роль/описание (под именем)")]
        public string role;

        [Tooltip("URL (опционально)")]
        public string url;
    }

    // ═══════════════════════════════════════════════════════════════════
    // LEGACY TYPES (обратная совместимость)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Определение роли (legacy)
    /// </summary>
    [Serializable]
    public class RoleDefinition
    {
        [Tooltip("Уникальный идентификатор роли (для привязки авторов)")]
        public string id;

        [Tooltip("Отображаемое название роли")]
        public string displayName;

        [Tooltip("Порядок отображения (меньше = выше)")]
        public int order;
    }

    /// <summary>
    /// Запись об авторе (legacy)
    /// </summary>
    [Serializable]
    public class AuthorEntry
    {
        [Tooltip("Имя автора")]
        public string name;

        [Tooltip("Идентификаторы ролей автора")]
        public List<string> roleIds = new();

        [Tooltip("Ссылка (опционально)")]
        public string url;
    }

    /// <summary>
    /// Запись благодарности (legacy)
    /// </summary>
    [Serializable]
    public class ThanksEntry
    {
        [Tooltip("Категория благодарности (опционально)")]
        public string category;

        [Tooltip("Текст благодарности")]
        public string text;
    }
}
