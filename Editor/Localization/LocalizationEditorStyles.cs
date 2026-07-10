// Packages/com.protosystem.core/Editor/Localization/LocalizationEditorStyles.cs
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Общие стили для окон локализации (Setup Wizard, AI Translation).
    /// Учитывают Pro/Light скин редактора.
    /// </summary>
    public static class LocalizationEditorStyles
    {
        // ── Палитра ──
        public static readonly Color Accent    = new(0.26f, 0.54f, 0.96f);
        public static readonly Color AccentDim = new(0.26f, 0.54f, 0.96f, 0.35f);
        public static readonly Color Ok        = new(0.35f, 0.75f, 0.40f);
        public static readonly Color Warn      = new(0.95f, 0.70f, 0.25f);
        public static readonly Color Error     = new(0.90f, 0.35f, 0.30f);
        public static readonly Color Claude    = new(0.85f, 0.47f, 0.25f); // фирменный оранжевый

        private static Color HeaderBg => EditorGUIUtility.isProSkin
            ? new Color(0.16f, 0.16f, 0.18f) : new Color(0.80f, 0.82f, 0.86f);
        private static Color CardBg => EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.03f) : new Color(0f, 0f, 0f, 0.03f);

        private static GUIStyle _headerTitle, _headerSub, _cardTitle, _bigButton,
            _claudeButton, _logBox, _badge, _tabButton;

        public static GUIStyle HeaderTitle => _headerTitle ??= new GUIStyle(EditorStyles.boldLabel)
        { fontSize = 15, alignment = TextAnchor.MiddleLeft };

        public static GUIStyle HeaderSub => _headerSub ??= new GUIStyle(EditorStyles.miniLabel)
        { alignment = TextAnchor.MiddleLeft, wordWrap = true };

        public static GUIStyle CardTitle => _cardTitle ??= new GUIStyle(EditorStyles.boldLabel)
        { fontSize = 12 };

        public static GUIStyle BigButton => _bigButton ??= new GUIStyle(GUI.skin.button)
        { fontSize = 12, fontStyle = FontStyle.Bold, fixedHeight = 34 };

        public static GUIStyle ClaudeButton => _claudeButton ??= new GUIStyle(BigButton) { };

        public static GUIStyle LogBox => _logBox ??= new GUIStyle(EditorStyles.helpBox)
        {
            font = EditorStyles.miniLabel.font,
            fontSize = 10,
            wordWrap = false,
            padding = new RectOffset(8, 8, 6, 6),
        };

        public static GUIStyle Badge => _badge ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };

        public static GUIStyle TabButton => _tabButton ??= new GUIStyle(EditorStyles.toolbarButton)
        { fontSize = 11, fixedHeight = 24 };

        // ── Компоненты ──

        /// <summary>Шапка окна: заголовок + подзаголовок на тёмной плашке.</summary>
        public static void Header(string title, string subtitle = null)
        {
            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(rect, HeaderBg);
            // Акцентная полоса слева
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), Accent);

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(title, HeaderTitle);
            if (!string.IsNullOrEmpty(subtitle))
                EditorGUILayout.LabelField(subtitle, HeaderSub);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.EndVertical();
            GUILayout.Space(4);
        }

        /// <summary>Карточка-секция с необязательным заголовком.</summary>
        public static void BeginCard(string title = null)
        {
            var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.DrawRect(rect, CardBg);
            GUILayout.Space(4);
            if (!string.IsNullOrEmpty(title))
            {
                EditorGUILayout.LabelField(title, CardTitle);
                GUILayout.Space(2);
            }
        }

        public static void EndCard()
        {
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
            GUILayout.Space(4);
        }

        /// <summary>Большая акцентная кнопка. Возвращает true при клике.</summary>
        public static bool AccentButton(string text, Color color, bool enabled = true)
        {
            var oldBg = GUI.backgroundColor;
            var oldEnabled = GUI.enabled;
            GUI.backgroundColor = color;
            GUI.enabled = enabled;
            bool clicked = GUILayout.Button(text, BigButton);
            GUI.backgroundColor = oldBg;
            GUI.enabled = oldEnabled;
            return clicked;
        }

        /// <summary>Плоская вкладка тулбара; возвращает true при клике.</summary>
        public static bool Tab(string label, bool active)
        {
            var oldBg = GUI.backgroundColor;
            if (active) GUI.backgroundColor = Accent;
            bool clicked = GUILayout.Button(label, TabButton);
            GUI.backgroundColor = oldBg;
            return clicked;
        }

        /// <summary>Цветной бейдж-статус (например "3 err").</summary>
        public static void DrawBadge(string text, Color color, float width = 0)
        {
            var content = new GUIContent(text);
            float w = width > 0 ? width : Badge.CalcSize(content).x + 12;
            var rect = GUILayoutUtility.GetRect(w, 16, GUILayout.Width(w));
            var rounded = new Rect(rect.x, rect.y + 1, rect.width, rect.height - 2);
            EditorGUI.DrawRect(rounded, color);
            GUI.Label(rect, content, Badge);
        }

        /// <summary>Компактный прогресс-бар покрытия: label слева, done/total справа.</summary>
        public static void CoverageBar(string label, int done, int total)
        {
            float frac = total > 0 ? (float)done / total : 0f;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(90));

            var rect = GUILayoutUtility.GetRect(50, 12, GUILayout.ExpandWidth(true));
            rect.y += 3;
            rect.height = 8;
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.10f));

            var fill = new Rect(rect.x, rect.y, rect.width * frac, rect.height);
            EditorGUI.DrawRect(fill, frac >= 0.999f ? Ok : frac > 0.5f ? Accent : Warn);

            EditorGUILayout.LabelField($"{done}/{total}", EditorStyles.miniLabel, GUILayout.Width(64));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Строка-чекбокс с моноширинным выравниванием для списков языков/таблиц.</summary>
        public static bool ToggleRow(string label, bool value)
        {
            return EditorGUILayout.ToggleLeft(label, value);
        }
    }
}
