// Packages/com.protosystem.core/Runtime/UI/CreditsData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Данные для окна Credits.
    /// 
    /// Справочники (источники данных):
    ///   roles, authors, specialThanks, technologies, quotes, inspirations
    /// 
    /// Layout (порядок отображения):
    ///   sections — типизированные секции со ссылками на справочники.
    ///   Если sections пуст — fallback на простой вывод roles/authors + thanks.
    /// </summary>
    [CreateAssetMenu(fileName = "CreditsData", menuName = "ProtoSystem/UI/Credits Data", order = 100)]
    public class CreditsData : ScriptableObject
    {
        // ═══════════════════════════════════════════════════════════════════
        // ИСТОЧНИКИ ДАННЫХ
        // ═══════════════════════════════════════════════════════════════════

        [Header("Роли")]
        [Tooltip("Определения ролей (id → displayName)")]
        public List<RoleDefinition> roles = new();

        [Header("Авторы")]
        [Tooltip("Авторы с привязкой к ролям")]
        public List<AuthorEntry> authors = new();

        [Header("Благодарности")]
        [Tooltip("Специальные благодарности по категориям")]
        public List<ThanksEntry> specialThanks = new();

        [Header("Технологии")]
        [Tooltip("Список технологий / инструментов")]
        public List<string> technologies = new();

        [Header("Цитаты")]
        [Tooltip("Цитаты для использования в секциях")]
        public List<QuoteEntry> quotes = new();

        [Header("Вдохновение")]
        [Tooltip("Игры, фильмы и другие источники вдохновения")]
        public List<string> inspirations = new();

        // ═══════════════════════════════════════════════════════════════════
        // LAYOUT
        // ═══════════════════════════════════════════════════════════════════

        [Header("Layout секций")]
        [Tooltip("Порядок и состав отображения. Секции ссылаются на источники данных выше.")]
        public List<CreditsSection> sections = new();

        // ═══════════════════════════════════════════════════════════════════
        // ШРИФТЫ ПО УМОЛЧАНИЮ
        // ═══════════════════════════════════════════════════════════════════

        [Header("Шрифты по умолчанию")]
        [Tooltip("TMP Font Asset для заголовков секций (null = системный)")]
        public TMPro.TMP_FontAsset defaultTitleFont;

        [Tooltip("TMP Font Asset для основного текста (null = системный)")]
        public TMPro.TMP_FontAsset defaultBodyFont;

        [Tooltip("Размер заголовков по умолчанию")]
        public int defaultTitleSize = 16;

        [Tooltip("Размер основного текста по умолчанию")]
        public int defaultBodySize = 20;

        [Tooltip("Размер подписей (роль, атрибуция и т.п.)")]
        public int defaultCaptionSize = 12;

        // ═══════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Есть ли секции layout
        /// </summary>
        public bool HasSections => sections != null && sections.Count > 0;

        /// <summary>
        /// Возвращает только включённые секции
        /// </summary>
        public List<CreditsSection> GetEnabledSections()
        {
            if (sections == null) return new List<CreditsSection>();
            return sections.FindAll(s => s.enabled);
        }

        /// <summary>
        /// Получить авторов по роли
        /// </summary>
        public List<AuthorEntry> GetAuthorsByRole(string roleId)
        {
            return authors.FindAll(a => a.roleIds != null && a.roleIds.Contains(roleId));
        }

        /// <summary>
        /// Получить все роли автора
        /// </summary>
        public List<RoleDefinition> GetRolesForAuthor(AuthorEntry author)
        {
            if (author.roleIds == null) return new List<RoleDefinition>();
            return roles.FindAll(r => author.roleIds.Contains(r.id));
        }

        /// <summary>
        /// Найти роль по id
        /// </summary>
        public RoleDefinition GetRole(string roleId)
        {
            return roles.Find(r => r.id == roleId);
        }

        /// <summary>
        /// Получить благодарности по категории (пустая строка = все)
        /// </summary>
        public List<ThanksEntry> GetThanksByCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
                return new List<ThanksEntry>(specialThanks);
            return specialThanks.FindAll(t => t.category == category);
        }

        /// <summary>
        /// Генерирует форматированный текст для CreditsWindow.
        /// Если есть sections — использует layout. Иначе — простой вывод.
        /// </summary>
        public string GenerateCreditsText()
        {
            if (HasSections)
                return GenerateFromSections();
            return GenerateSimple();
        }

        // ═══════════════════════════════════════════════════════════════════
        // GENERATION — SECTIONS LAYOUT
        // ═══════════════════════════════════════════════════════════════════

        private string GenerateFromSections()
        {
            var sb = new System.Text.StringBuilder();

            foreach (var section in sections)
            {
                if (!section.enabled) continue;

                int titleSize = section.overrideTitleSize > 0 ? section.overrideTitleSize : defaultTitleSize;
                int bodySize = section.overrideBodySize > 0 ? section.overrideBodySize : defaultBodySize;
                int captionSize = defaultCaptionSize;

                switch (section.type)
                {
                    case CreditsSectionType.Header:
                        BuildHeader(sb, section, bodySize);
                        break;
                    case CreditsSectionType.RoleGroup:
                        BuildRoleGroup(sb, section, titleSize, bodySize, captionSize);
                        break;
                    case CreditsSectionType.Thanks:
                        BuildThanks(sb, section, titleSize, bodySize);
                        break;
                    case CreditsSectionType.Technology:
                        BuildTechnology(sb, section, titleSize, bodySize);
                        break;
                    case CreditsSectionType.Inspirations:
                        BuildInspirations(sb, section, titleSize, bodySize);
                        break;
                    case CreditsSectionType.Quote:
                        BuildQuote(sb, section, bodySize, captionSize);
                        break;
                    case CreditsSectionType.Logo:
                        BuildLogo(sb, section, bodySize, captionSize);
                        break;
                    case CreditsSectionType.CustomText:
                        BuildCustomText(sb, section);
                        break;
                }

                if (section.showDividerAfter)
                    sb.AppendLine("\n<color=#3a3d42>───────</color>\n");
            }

            return sb.ToString();
        }

        private void BuildHeader(System.Text.StringBuilder sb, CreditsSection section, int bodySize)
        {
            int size = bodySize + 12;
            sb.AppendLine($"<size={size}><b>{section.headerTitle}</b></size>");
            var subtitle = section.GetLocalizedSubtitle();
            if (!string.IsNullOrEmpty(subtitle))
                sb.AppendLine($"<size={bodySize - 6}>{subtitle}</size>");
            sb.AppendLine();
        }

        private void BuildRoleGroup(System.Text.StringBuilder sb, CreditsSection section,
            int titleSize, int bodySize, int captionSize)
        {
            var role = GetRole(section.roleId);
            string displayTitle = section.GetLocalizedTitle(role?.GetLocalizedName() ?? section.roleId);

            sb.AppendLine($"<size={titleSize}><color=#c9a96e>{displayTitle}</color></size>");

            var roleAuthors = GetAuthorsByRole(section.roleId);
            foreach (var author in roleAuthors)
            {
                sb.AppendLine($"<size={bodySize}><b>{author.name}</b></size>");
                
                // Показываем все роли автора кроме текущей
                var otherRoles = GetRolesForAuthor(author);
                otherRoles.RemoveAll(r => r.id == section.roleId);
                if (otherRoles.Count > 0)
                {
                    var roleNames = otherRoles.ConvertAll(r => r.GetLocalizedName());
                    sb.AppendLine($"<size={captionSize}>{string.Join(" · ", roleNames)}</size>");
                }
                sb.AppendLine();
            }
        }

        private void BuildThanks(System.Text.StringBuilder sb, CreditsSection section,
            int titleSize, int bodySize)
        {
            string displayTitle = section.GetLocalizedTitle("Благодарности");
            sb.AppendLine($"<size={titleSize}><color=#c9a96e>{displayTitle}</color></size>");

            var entries = GetThanksByCategory(section.thanksCategory);
            foreach (var thanks in entries)
            {
                if (!string.IsNullOrEmpty(thanks.category) && string.IsNullOrEmpty(section.thanksCategory))
                    sb.AppendLine($"<i>{thanks.category}</i>");
                sb.AppendLine(thanks.GetLocalizedText());
            }
            sb.AppendLine();
        }

        private void BuildTechnology(System.Text.StringBuilder sb, CreditsSection section,
            int titleSize, int bodySize)
        {
            string displayTitle = section.GetLocalizedTitle("Технологии");
            sb.AppendLine($"<size={titleSize}><color=#c9a96e>{displayTitle}</color></size>");

            if (technologies != null && technologies.Count > 0)
                sb.AppendLine(string.Join("  ·  ", technologies));
            sb.AppendLine();
        }

        private void BuildInspirations(System.Text.StringBuilder sb, CreditsSection section,
            int titleSize, int bodySize)
        {
            string displayTitle = section.GetLocalizedTitle("Вдохновение");
            sb.AppendLine($"<size={titleSize}><color=#c9a96e>{displayTitle}</color></size>");

            if (inspirations != null)
            {
                foreach (var item in inspirations)
                    sb.AppendLine(item);
            }
            sb.AppendLine();
        }

        private void BuildQuote(System.Text.StringBuilder sb, CreditsSection section,
            int bodySize, int captionSize)
        {
            if (section.quoteIndex < 0 || quotes == null || section.quoteIndex >= quotes.Count)
                return;

            var quote = quotes[section.quoteIndex];
            string openTag = "", closeTag = "";

            switch (quote.style)
            {
                case QuoteStyle.Italic:
                    openTag = "<i>"; closeTag = "</i>";
                    break;
                case QuoteStyle.Bold:
                    openTag = "<b>"; closeTag = "</b>";
                    break;
                case QuoteStyle.BoldItalic:
                    openTag = "<b><i>"; closeTag = "</i></b>";
                    break;
            }

            sb.AppendLine($"\n{openTag}{quote.GetLocalizedText()}{closeTag}");
            var attr = quote.GetLocalizedAttribution();
            if (!string.IsNullOrEmpty(attr))
                sb.AppendLine($"<size={captionSize}>— {attr}</size>");
            sb.AppendLine();
        }

        private void BuildLogo(System.Text.StringBuilder sb, CreditsSection section,
            int bodySize, int captionSize)
        {
            int size = bodySize + 8;
            sb.AppendLine($"\n<size={size}><b>{section.logoText}</b></size>");
            if (!string.IsNullOrEmpty(section.logoAccent))
                sb.AppendLine($"<size={size}><color=#e8a033>{section.logoAccent}</color></size>");
            if (!string.IsNullOrEmpty(section.logoYear))
                sb.AppendLine($"<size={captionSize}>© {section.logoYear}</size>");
            sb.AppendLine();
        }

        private void BuildCustomText(System.Text.StringBuilder sb, CreditsSection section)
        {
            if (!string.IsNullOrEmpty(section.customRichText))
                sb.AppendLine(section.customRichText);
            sb.AppendLine();
        }

        // ═══════════════════════════════════════════════════════════════════
        // GENERATION — SIMPLE (no sections)
        // ═══════════════════════════════════════════════════════════════════

        private string GenerateSimple()
        {
            var sb = new System.Text.StringBuilder();

            foreach (var role in roles)
            {
                var roleAuthors = GetAuthorsByRole(role.id);
                if (roleAuthors.Count == 0) continue;

                sb.AppendLine($"<size=24><b>{role.GetLocalizedName()}</b></size>");
                foreach (var author in roleAuthors)
                    sb.AppendLine(author.name);
                sb.AppendLine();
            }

            if (specialThanks.Count > 0)
            {
                sb.AppendLine("<size=24><b>Благодарности</b></size>");
                foreach (var thanks in specialThanks)
                {
                    if (!string.IsNullOrEmpty(thanks.category))
                        sb.AppendLine($"<i>{thanks.category}</i>");
                    sb.AppendLine(thanks.GetLocalizedText());
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // SECTION TYPES
    // ═══════════════════════════════════════════════════════════════════

    public enum CreditsSectionType
    {
        Header,       // Название игры + подзаголовок (inline)
        RoleGroup,    // Ссылка на roleId → авторы из справочника
        Thanks,       // Ссылка на specialThanks (опц. фильтр по категории)
        Technology,   // Ссылка на technologies
        Inspirations, // Ссылка на inspirations
        Quote,        // Ссылка на quotes по индексу
        Logo,         // Финальный логотип (inline)
        CustomText,   // Произвольный Rich Text (inline)
    }

    // ═══════════════════════════════════════════════════════════════════
    // SECTION
    // ═══════════════════════════════════════════════════════════════════

    [Serializable]
    public class CreditsSection
    {
        [Tooltip("Включить/выключить секцию")]
        public bool enabled = true;

        [Tooltip("Тип секции")]
        public CreditsSectionType type = CreditsSectionType.RoleGroup;

        [Tooltip("Заголовок секции (переопределяет автоматический)")]
        public string title;

        [Tooltip("Ключ локализации заголовка (если пусто — используется title как fallback)")]
        public string titleKey;

        /// <summary>Получить локализованный заголовок секции</summary>
        public string GetLocalizedTitle(string fallbackTitle = null)
        {
            string fb = !string.IsNullOrEmpty(title) ? title : fallbackTitle ?? "";
            if (!string.IsNullOrEmpty(titleKey))
                return UIKeys.L(titleKey, fb);
            return fb;
        }

        [Tooltip("Разделитель после секции")]
        public bool showDividerAfter = true;

        // ── Шрифты (override) ──

        [Tooltip("Переопределить шрифт заголовка (null = default)")]
        public TMPro.TMP_FontAsset overrideTitleFont;

        [Tooltip("Переопределить шрифт тела (null = default)")]
        public TMPro.TMP_FontAsset overrideBodyFont;

        [Tooltip("Переопределить размер заголовка (0 = default)")]
        public int overrideTitleSize;

        [Tooltip("Переопределить размер тела (0 = default)")]
        public int overrideBodySize;

        // ── Header ──

        [Tooltip("Название игры (Header)")]
        public string headerTitle;

        [Tooltip("Подзаголовок (Header)")]
        public string headerSubtitle;

        [Tooltip("Ключ локализации подзаголовка (Header)")]
        public string headerSubtitleKey;

        public string GetLocalizedSubtitle()
        {
            if (!string.IsNullOrEmpty(headerSubtitleKey))
                return UIKeys.L(headerSubtitleKey, headerSubtitle);
            return headerSubtitle ?? "";
        }

        // ── RoleGroup ──

        [Tooltip("ID роли из справочника roles (RoleGroup)")]
        public string roleId;

        // ── Thanks ──

        [Tooltip("Фильтр по категории (пусто = все) (Thanks)")]
        public string thanksCategory;

        // ── Quote ──

        [Tooltip("Индекс цитаты из справочника quotes (Quote)")]
        public int quoteIndex;

        // ── Logo ──

        [Tooltip("Основной текст логотипа (Logo)")]
        public string logoText;

        [Tooltip("Акцентный текст логотипа (Logo)")]
        public string logoAccent;

        [Tooltip("Год (Logo)")]
        public string logoYear = "2026";

        // ── CustomText ──

        [TextArea(3, 8)]
        [Tooltip("Произвольный Rich Text (CustomText)")]
        public string customRichText;
    }

    // ═══════════════════════════════════════════════════════════════════
    // DATA ENTRIES
    // ═══════════════════════════════════════════════════════════════════

    public enum QuoteStyle
    {
        Normal,
        Italic,
        Bold,
        BoldItalic,
    }

    [Serializable]
    public class QuoteEntry
    {
        [TextArea(2, 4)]
        [Tooltip("Текст цитаты")]
        public string text;

        [Tooltip("Ключ локализации текста цитаты (если пусто — используется text)")]
        public string textKey;

        [Tooltip("Автор / источник")]
        public string attribution;

        [Tooltip("Ключ локализации атрибуции (если пусто — используется attribution)")]
        public string attributionKey;

        [Tooltip("Стиль отображения")]
        public QuoteStyle style = QuoteStyle.Italic;

        public string GetLocalizedText()
        {
            if (!string.IsNullOrEmpty(textKey))
                return UIKeys.L(textKey, text);
            return text;
        }

        public string GetLocalizedAttribution()
        {
            if (!string.IsNullOrEmpty(attributionKey))
                return UIKeys.L(attributionKey, attribution);
            return attribution;
        }
    }

    [Serializable]
    public class RoleDefinition
    {
        [Tooltip("Уникальный идентификатор роли")]
        public string id;

        [Tooltip("Отображаемое название роли")]
        public string displayName;

        [Tooltip("Ключ локализации названия роли (если пусто — используется displayName)")]
        public string displayNameKey;

        [Tooltip("Порядок отображения (меньше = выше)")]
        public int order;

        /// <summary>Получить локализованное название роли</summary>
        public string GetLocalizedName()
        {
            if (!string.IsNullOrEmpty(displayNameKey))
                return UIKeys.L(displayNameKey, displayName);
            return displayName;
        }
    }

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

    [Serializable]
    public class ThanksEntry
    {
        [Tooltip("Категория благодарности (для фильтрации в секциях)")]
        public string category;

        [Tooltip("Текст благодарности")]
        public string text;

        [Tooltip("Ключ локализации текста (если пусто — используется text)")]
        public string textKey;

        public string GetLocalizedText()
        {
            if (!string.IsNullOrEmpty(textKey))
                return UIKeys.L(textKey, text);
            return text;
        }
    }
}
