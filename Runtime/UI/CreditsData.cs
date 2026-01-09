// Packages/com.protosystem.core/Runtime/UI/CreditsData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Данные для окна Credits - авторы, роли, благодарности
    /// </summary>
    [CreateAssetMenu(fileName = "CreditsData", menuName = "ProtoSystem/UI/Credits Data", order = 100)]
    public class CreditsData : ScriptableObject
    {
        [Header("Роли (порядок отображения)")]
        [Tooltip("Список ролей в порядке отображения")]
        public List<RoleDefinition> roles = new();

        [Header("Авторы")]
        [Tooltip("Список авторов с привязкой к ролям")]
        public List<AuthorEntry> authors = new();

        [Header("Благодарности")]
        [Tooltip("Список специальных благодарностей")]
        public List<ThanksEntry> specialThanks = new();

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
        /// Генерирует форматированный текст для CreditsWindow
        /// </summary>
        public string GenerateCreditsText()
        {
            var sb = new System.Text.StringBuilder();

            // Группируем авторов по ролям
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

            // Благодарности
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

    /// <summary>
    /// Определение роли
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
    /// Запись об авторе
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
    /// Запись благодарности
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
