// Packages/com.protosystem.core/Runtime/UI/Core/ToolkitLocalization.cs
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Локализация дерева UI Toolkit по конвенции «#ключ».
    ///
    /// В UXML (или при создании элементов кодом) текст пишется как ключ с префиксом '#':
    /// <code>
    ///   &lt;ui:Label text="#menu.title" /&gt;
    ///   &lt;ui:Button text="#menu.play" /&gt;
    /// </code>
    /// Localize() заменяет такие тексты на Loc.Get(key), запоминает пары элемент↔ключ
    /// и при повторном вызове (смена языка) перелокализует уже заменённые тексты.
    /// Поддерживаются также tooltip'ы с тем же префиксом.
    ///
    /// Дополнительно вешает на корень USS-класс "lang-{code}" (и снимает предыдущий) —
    /// для переключения шрифтов/стилей по языку в USS.
    /// </summary>
    public class ToolkitLocalization
    {
        public const char KeyPrefix = '#';

        private readonly Dictionary<TextElement, string> _textKeys = new();
        private readonly Dictionary<VisualElement, string> _tooltipKeys = new();
        private string _appliedLangClass;

        /// <summary>
        /// Локализовать дерево. Безопасно вызывать повторно (смена языка, пересборка дерева).
        /// </summary>
        public void Localize(VisualElement root)
        {
            if (root == null) return;

            ApplyLanguageClass(root);
            PruneDead();

            root.Query<VisualElement>().ForEach(element =>
            {
                // Тексты
                if (element is TextElement textElement)
                {
                    if (!_textKeys.TryGetValue(textElement, out var key))
                    {
                        key = ExtractKey(textElement.text);
                        if (key != null) _textKeys[textElement] = key;
                    }
                    if (key != null)
                        textElement.text = Loc.Get(key, key);
                }

                // Tooltip'ы
                if (!_tooltipKeys.TryGetValue(element, out var tooltipKey))
                {
                    tooltipKey = ExtractKey(element.tooltip);
                    if (tooltipKey != null) _tooltipKeys[element] = tooltipKey;
                }
                if (tooltipKey != null)
                    element.tooltip = Loc.Get(tooltipKey, tooltipKey);
            });
        }

        /// <summary>
        /// Локализовать один элемент по явному ключу (для динамического контента).
        /// </summary>
        public void SetKey(TextElement element, string key)
        {
            if (element == null || string.IsNullOrEmpty(key)) return;
            _textKeys[element] = key;
            element.text = Loc.Get(key, key);
        }

        private static string ExtractKey(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 2 || text[0] != KeyPrefix)
                return null;
            return text.Substring(1);
        }

        private void ApplyLanguageClass(VisualElement root)
        {
            string langClass = $"lang-{Loc.CurrentLanguage}";
            if (_appliedLangClass == langClass && root.ClassListContains(langClass)) return;

            if (!string.IsNullOrEmpty(_appliedLangClass))
                root.RemoveFromClassList(_appliedLangClass);
            root.AddToClassList(langClass);
            _appliedLangClass = langClass;
        }

        /// <summary>Убирает элементы, чьи панели уже уничтожены (пул пересоздал дерево).</summary>
        private void PruneDead()
        {
            PruneDict(_textKeys);
            PruneDict(_tooltipKeys);
        }

        private static void PruneDict<T>(Dictionary<T, string> dict) where T : VisualElement
        {
            List<T> dead = null;
            foreach (var kvp in dict)
            {
                if (kvp.Key.panel == null)
                    (dead ??= new List<T>()).Add(kvp.Key);
            }
            if (dead != null)
                foreach (var el in dead) dict.Remove(el);
        }
    }
}
