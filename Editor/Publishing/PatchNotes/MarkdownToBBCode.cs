// Packages/com.protosystem.core/Editor/Publishing/PatchNotes/MarkdownToBBCode.cs
using System.Text.RegularExpressions;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Конвертер Markdown в BBCode (для Steam)
    /// </summary>
    public static class MarkdownToBBCode
    {
        /// <summary>
        /// Конвертировать Markdown в BBCode
        /// </summary>
        public static string Convert(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return "";
            
            var result = markdown;
            
            // Headers
            result = Regex.Replace(result, @"^### (.+)$", "[h3]$1[/h3]", RegexOptions.Multiline);
            result = Regex.Replace(result, @"^## (.+)$", "[h2]$1[/h2]", RegexOptions.Multiline);
            result = Regex.Replace(result, @"^# (.+)$", "[h1]$1[/h1]", RegexOptions.Multiline);
            
            // Bold and Italic
            result = Regex.Replace(result, @"\*\*\*(.+?)\*\*\*", "[b][i]$1[/i][/b]");
            result = Regex.Replace(result, @"\*\*(.+?)\*\*", "[b]$1[/b]");
            result = Regex.Replace(result, @"\*(.+?)\*", "[i]$1[/i]");
            result = Regex.Replace(result, @"__(.+?)__", "[b]$1[/b]");
            result = Regex.Replace(result, @"_(.+?)_", "[i]$1[/i]");
            
            // Strikethrough
            result = Regex.Replace(result, @"~~(.+?)~~", "[strike]$1[/strike]");
            
            // Links
            result = Regex.Replace(result, @"\[([^\]]+)\]\(([^\)]+)\)", "[url=$2]$1[/url]");
            
            // Images (Steam doesn't really support inline images, convert to link)
            result = Regex.Replace(result, @"!\[([^\]]*)\]\(([^\)]+)\)", "[url=$2]$1[/url]");
            
            // Code
            result = Regex.Replace(result, @"```[\s\S]*?```", m => 
                "[code]" + m.Value.Trim('`').Trim() + "[/code]");
            result = Regex.Replace(result, @"`([^`]+)`", "[code]$1[/code]");
            
            // Lists
            result = Regex.Replace(result, @"^[\*\-] (.+)$", "[*] $1", RegexOptions.Multiline);
            // Wrap consecutive list items in [list] tags
            result = Regex.Replace(result, @"((?:^\[\*\] .+\n?)+)", "[list]\n$1[/list]\n", RegexOptions.Multiline);
            
            // Numbered lists
            result = Regex.Replace(result, @"^\d+\. (.+)$", "[*] $1", RegexOptions.Multiline);
            
            // Horizontal rule
            result = Regex.Replace(result, @"^[\-\*_]{3,}$", "[hr][/hr]", RegexOptions.Multiline);
            
            // Blockquote
            result = Regex.Replace(result, @"^> (.+)$", "[quote]$1[/quote]", RegexOptions.Multiline);
            
            // Cleanup extra newlines
            result = Regex.Replace(result, @"\n{3,}", "\n\n");
            
            return result.Trim();
        }

        /// <summary>
        /// Конвертировать BBCode в Markdown
        /// </summary>
        public static string ToMarkdown(string bbcode)
        {
            if (string.IsNullOrEmpty(bbcode)) return "";
            
            var result = bbcode;
            
            // Headers
            result = Regex.Replace(result, @"\[h1\](.+?)\[/h1\]", "# $1");
            result = Regex.Replace(result, @"\[h2\](.+?)\[/h2\]", "## $1");
            result = Regex.Replace(result, @"\[h3\](.+?)\[/h3\]", "### $1");
            
            // Bold and Italic
            result = Regex.Replace(result, @"\[b\]\[i\](.+?)\[/i\]\[/b\]", "***$1***");
            result = Regex.Replace(result, @"\[b\](.+?)\[/b\]", "**$1**");
            result = Regex.Replace(result, @"\[i\](.+?)\[/i\]", "*$1*");
            
            // Strikethrough
            result = Regex.Replace(result, @"\[strike\](.+?)\[/strike\]", "~~$1~~");
            
            // Links
            result = Regex.Replace(result, @"\[url=([^\]]+)\](.+?)\[/url\]", "[$2]($1)");
            
            // Code
            result = Regex.Replace(result, @"\[code\](.+?)\[/code\]", "`$1`", RegexOptions.Singleline);
            
            // Lists
            result = Regex.Replace(result, @"\[list\]\s*", "");
            result = Regex.Replace(result, @"\s*\[/list\]", "");
            result = Regex.Replace(result, @"\[\*\]\s*", "- ");
            
            // Horizontal rule
            result = Regex.Replace(result, @"\[hr\]\[/hr\]", "---");
            
            // Quote
            result = Regex.Replace(result, @"\[quote\](.+?)\[/quote\]", "> $1");
            
            return result.Trim();
        }

        /// <summary>
        /// Превью BBCode (для отображения в редакторе)
        /// </summary>
        public static string PreviewBBCode(string bbcode)
        {
            if (string.IsNullOrEmpty(bbcode)) return "";
            
            var result = bbcode;
            
            // Удаляем теги, оставляя содержимое
            result = Regex.Replace(result, @"\[h[123]\](.+?)\[/h[123]\]", "[ $1 ]");
            result = Regex.Replace(result, @"\[b\](.+?)\[/b\]", "$1");
            result = Regex.Replace(result, @"\[i\](.+?)\[/i\]", "$1");
            result = Regex.Replace(result, @"\[u\](.+?)\[/u\]", "$1");
            result = Regex.Replace(result, @"\[strike\](.+?)\[/strike\]", "$1");
            result = Regex.Replace(result, @"\[url=[^\]]+\](.+?)\[/url\]", "$1");
            result = Regex.Replace(result, @"\[code\](.+?)\[/code\]", "$1");
            result = Regex.Replace(result, @"\[list\]|\[/list\]", "");
            result = Regex.Replace(result, @"\[\*\]", "• ");
            result = Regex.Replace(result, @"\[hr\]\[/hr\]", "────────");
            result = Regex.Replace(result, @"\[quote\](.+?)\[/quote\]", "│ $1");
            
            return result.Trim();
        }
    }
}
