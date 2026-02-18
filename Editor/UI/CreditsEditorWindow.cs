// Packages/com.protosystem.core/Editor/UI/CreditsEditorWindow.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Окно редактора для управления данными Credits.
    /// Справочники (данные) + Layout (секции) + Шрифты + Предпросмотр.
    /// </summary>
    public class CreditsEditorWindow : EditorWindow
    {
        private CreditsData creditsData;
        private SerializedObject serializedObject;
        
        // Lists
        private ReorderableList rolesList;
        private ReorderableList authorsList;
        private ReorderableList thanksList;
        private ReorderableList quotesList;
        private ReorderableList sectionsList;
        
        private Vector2 scrollPosition;
        private int selectedTab;
        private readonly string[] tabNames = { "Layout", "Роли & Авторы", "Контент", "Шрифты", "Предпросмотр" };

        // Section visuals
        private static GUIStyle _disabledLabelStyle;

        private static readonly Dictionary<CreditsSectionType, Color> SectionColors = new()
        {
            { CreditsSectionType.Header,       new Color(0.9f, 0.7f, 0.3f, 0.15f) },
            { CreditsSectionType.RoleGroup,    new Color(0.3f, 0.7f, 0.9f, 0.15f) },
            { CreditsSectionType.Thanks,       new Color(0.7f, 0.5f, 0.9f, 0.15f) },
            { CreditsSectionType.Technology,   new Color(0.5f, 0.9f, 0.5f, 0.15f) },
            { CreditsSectionType.Inspirations, new Color(0.5f, 0.8f, 0.7f, 0.15f) },
            { CreditsSectionType.Quote,        new Color(0.9f, 0.5f, 0.5f, 0.15f) },
            { CreditsSectionType.Logo,         new Color(0.9f, 0.8f, 0.3f, 0.15f) },
            { CreditsSectionType.CustomText,   new Color(0.6f, 0.6f, 0.6f, 0.15f) },
        };

        private static readonly string[] SectionTypeLabels =
        {
            "🎮 Header", "👥 RoleGroup", "🙏 Thanks", "⚙ Technology",
            "💡 Inspirations", "💬 Quote", "🏷 Logo", "📝 Custom"
        };

        [MenuItem("ProtoSystem/UI/Tools/Credits Editor", priority = 210)]
        public static void ShowWindow()
        {
            var window = GetWindow<CreditsEditorWindow>("Credits Editor");
            window.minSize = new Vector2(550, 600);
            window.Show();
        }

        private void OnEnable()
        {
            var guids = AssetDatabase.FindAssets("t:CreditsData");
            if (guids.Length > 0)
            {
                creditsData = AssetDatabase.LoadAssetAtPath<CreditsData>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (creditsData != null)
                SetupAll();
        }

        private void SetupAll()
        {
            if (creditsData == null) return;
            serializedObject = new SerializedObject(creditsData);
            SetupSectionsList();
            SetupRolesList();
            SetupAuthorsList();
            SetupThanksList();
            SetupQuotesList();
        }

        // ═══════════════════════════════════════════════════════════════════
        // MAIN GUI
        // ═══════════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            // Header + asset picker
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Credits Data", EditorStyles.boldLabel);
            var newData = (CreditsData)EditorGUILayout.ObjectField(creditsData, typeof(CreditsData), false);
            if (newData != creditsData)
            {
                creditsData = newData;
                if (creditsData != null) SetupAll();
                else serializedObject = null;
            }
            EditorGUILayout.EndHorizontal();

            if (creditsData == null)
            {
                DrawCreateButton();
                return;
            }

            if (serializedObject == null) SetupAll();
            if (serializedObject == null)
            {
                EditorGUILayout.HelpBox("Ошибка SerializedObject", MessageType.Error);
                return;
            }

            // Mode
            var mode = creditsData.HasSections ? "Sections layout" : "Simple (roles → authors)";
            EditorGUILayout.LabelField($"Режим: {mode}", EditorStyles.miniLabel);

            // Tabs
            EditorGUILayout.Space(5);
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
            EditorGUILayout.Space(10);

            serializedObject.Update();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (selectedTab)
            {
                case 0: DrawLayoutTab();       break;
                case 1: DrawPeopleTab();       break;
                case 2: DrawContentTab();      break;
                case 3: DrawFontsTab();        break;
                case 4: DrawPreviewTab();      break;
            }

            EditorGUILayout.EndScrollView();
            serializedObject.ApplyModifiedProperties();

            // Save
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Сохранить", GUILayout.Width(100), GUILayout.Height(25)))
            {
                EditorUtility.SetDirty(creditsData);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        // ═══════════════════════════════════════════════════════════════════
        // TAB 0: LAYOUT
        // ═══════════════════════════════════════════════════════════════════

        private void DrawLayoutTab()
        {
            EditorGUILayout.LabelField("Layout секций", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Секции определяют порядок отображения и ссылаются на справочники данных.\n" +
                "Снимите ✓ чтобы временно скрыть секцию. Перетаскивайте для сортировки.\n" +
                "Если layout пуст — используется простой вывод roles → authors + thanks.",
                MessageType.Info);
            EditorGUILayout.Space(5);

            sectionsList?.DoLayoutList();

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Шаблон Last Convoy"))
                FillLastConvoyTemplate();
            if (GUILayout.Button("Очистить layout"))
            {
                if (EditorUtility.DisplayDialog("Очистить?", "Удалить все секции layout?", "Да", "Отмена"))
                {
                    Undo.RecordObject(creditsData, "Clear Sections");
                    creditsData.sections.Clear();
                    EditorUtility.SetDirty(creditsData);
                    SetupAll();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════════════════════════
        // TAB 1: ROLES & AUTHORS
        // ═══════════════════════════════════════════════════════════════════

        private void DrawPeopleTab()
        {
            EditorGUILayout.LabelField("Роли", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Роли — справочник. Секция RoleGroup ссылается на roleId.", MessageType.Info);
            rolesList?.DoLayoutList();

            EditorGUILayout.Space(5);
            if (GUILayout.Button("+ Стандартные роли", GUILayout.Width(180)))
                AddDefaultRoles();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Авторы", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Один автор может иметь несколько ролей.", MessageType.Info);

            if (creditsData.roles.Count == 0)
            {
                EditorGUILayout.HelpBox("Сначала добавьте роли выше.", MessageType.Warning);
                return;
            }
            authorsList?.DoLayoutList();
        }

        // ═══════════════════════════════════════════════════════════════════
        // TAB 2: CONTENT
        // ═══════════════════════════════════════════════════════════════════

        private void DrawContentTab()
        {
            // Thanks
            EditorGUILayout.LabelField("Благодарности", EditorStyles.boldLabel);
            thanksList?.DoLayoutList();

            EditorGUILayout.Space(15);

            // Technologies
            EditorGUILayout.LabelField("Технологии", EditorStyles.boldLabel);
            var techProp = serializedObject.FindProperty("technologies");
            EditorGUILayout.PropertyField(techProp, new GUIContent("Список"), true);

            EditorGUILayout.Space(15);

            // Inspirations
            EditorGUILayout.LabelField("Вдохновение", EditorStyles.boldLabel);
            var inspProp = serializedObject.FindProperty("inspirations");
            EditorGUILayout.PropertyField(inspProp, new GUIContent("Список"), true);

            EditorGUILayout.Space(15);

            // Quotes
            EditorGUILayout.LabelField("Цитаты", EditorStyles.boldLabel);
            quotesList?.DoLayoutList();
        }

        // ═══════════════════════════════════════════════════════════════════
        // TAB 3: FONTS
        // ═══════════════════════════════════════════════════════════════════

        private void DrawFontsTab()
        {
            EditorGUILayout.LabelField("Шрифты по умолчанию", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Шрифты и размеры по умолчанию для всех секций.\n" +
                "Каждая секция может переопределить их через override-поля.",
                MessageType.Info);
            EditorGUILayout.Space(5);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultTitleFont"), new GUIContent("Шрифт заголовков"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultBodyFont"), new GUIContent("Шрифт текста"));
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultTitleSize"), new GUIContent("Размер заголовков"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultBodySize"), new GUIContent("Размер текста"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultCaptionSize"), new GUIContent("Размер подписей"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // TAB 4: PREVIEW
        // ═══════════════════════════════════════════════════════════════════

        private void DrawPreviewTab()
        {
            EditorGUILayout.LabelField("Предпросмотр", EditorStyles.boldLabel);

            var mode = creditsData.HasSections ? "Layout (sections)" : "Simple (roles → authors)";
            EditorGUILayout.LabelField($"Источник: {mode}", EditorStyles.miniLabel);

            if (creditsData.HasSections)
            {
                var en = creditsData.GetEnabledSections().Count;
                EditorGUILayout.LabelField($"Секций: {en}/{creditsData.sections.Count} включено", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(10);
            var text = creditsData.GenerateCreditsText();
            var style = new GUIStyle(EditorStyles.textArea) { richText = true, wordWrap = true };
            EditorGUILayout.TextArea(text, style, GUILayout.ExpandHeight(true));

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Скопировать в буфер"))
                GUIUtility.systemCopyBuffer = text;
        }

        // ═══════════════════════════════════════════════════════════════════
        // SECTIONS REORDERABLE LIST
        // ═══════════════════════════════════════════════════════════════════

        private void SetupSectionsList()
        {
            sectionsList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("sections"), true, true, true, true);

            sectionsList.drawHeaderCallback = r =>
                EditorGUI.LabelField(r, $"Секции layout ({creditsData.sections.Count})");

            sectionsList.drawElementCallback = DrawSectionElement;
            sectionsList.elementHeightCallback = GetSectionHeight;

            sectionsList.onAddDropdownCallback = (rect, list) =>
            {
                var menu = new GenericMenu();
                var names = System.Enum.GetNames(typeof(CreditsSectionType));
                for (int i = 0; i < names.Length; i++)
                {
                    var type = (CreditsSectionType)i;
                    var label = i < SectionTypeLabels.Length ? SectionTypeLabels[i] : names[i];
                    menu.AddItem(new GUIContent(label), false, () => AddNewSection(type));
                }
                menu.DropDown(rect);
            };
        }

        private void AddNewSection(CreditsSectionType type)
        {
            Undo.RecordObject(creditsData, "Add Section");
            var s = new CreditsSection
            {
                enabled = true,
                type = type,
                showDividerAfter = type != CreditsSectionType.Logo,
            };

            switch (type)
            {
                case CreditsSectionType.Header:
                    s.headerTitle = "GAME TITLE";
                    s.headerSubtitle = "Subtitle";
                    break;
                case CreditsSectionType.RoleGroup:
                    s.roleId = creditsData.roles.Count > 0 ? creditsData.roles[0].id : "";
                    break;
                case CreditsSectionType.Thanks:
                    s.title = "БЛАГОДАРНОСТИ";
                    break;
                case CreditsSectionType.Technology:
                    s.title = "ТЕХНОЛОГИИ";
                    break;
                case CreditsSectionType.Inspirations:
                    s.title = "ВДОХНОВЕНИЕ";
                    break;
                case CreditsSectionType.Quote:
                    s.quoteIndex = 0;
                    break;
                case CreditsSectionType.Logo:
                    s.logoText = "LAST";
                    s.logoAccent = "CONVOY";
                    s.logoYear = "2026";
                    s.showDividerAfter = false;
                    break;
                case CreditsSectionType.CustomText:
                    s.customRichText = "<b>Custom text</b>";
                    break;
            }

            creditsData.sections.Add(s);
            EditorUtility.SetDirty(creditsData);
            SetupAll();
        }

        private void DrawSectionElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index >= creditsData.sections.Count) return;
            var section = creditsData.sections[index];
            var prop = sectionsList.serializedProperty.GetArrayElementAtIndex(index);

            float y = rect.y + 2;
            float lineH = EditorGUIUtility.singleLineHeight + 2;
            float indent = 20;

            // Background
            if (SectionColors.TryGetValue(section.type, out var bg))
            {
                var bgR = new Rect(rect.x - 4, rect.y, rect.width + 8, GetSectionHeight(index));
                if (!section.enabled) bg.a *= 0.3f;
                EditorGUI.DrawRect(bgR, bg);
            }

            // Row 1: ✓ + type + summary
            section.enabled = EditorGUI.Toggle(new Rect(rect.x, y, 16, EditorGUIUtility.singleLineHeight), section.enabled);
            var typeIdx = (int)section.type;
            var label = typeIdx < SectionTypeLabels.Length ? SectionTypeLabels[typeIdx] : section.type.ToString();
            var summary = GetSectionSummary(section);
            var style = section.enabled ? EditorStyles.boldLabel : DisabledStyle();
            EditorGUI.LabelField(new Rect(rect.x + 20, y, rect.width - 20, EditorGUIUtility.singleLineHeight),
                $"{label}  {summary}", style);

            if (!section.enabled) return;
            y += lineH + 2;

            // Type-specific fields
            float w = rect.width - indent;
            switch (section.type)
            {
                case CreditsSectionType.Header:
                    DrawField(rect.x + indent, ref y, w, lineH, "Название", ref section.headerTitle);
                    DrawField(rect.x + indent, ref y, w, lineH, "Подзаголовок", ref section.headerSubtitle);
                    break;

                case CreditsSectionType.RoleGroup:
                    DrawRoleDropdown(rect.x + indent, ref y, w, lineH, section);
                    DrawField(rect.x + indent, ref y, w, lineH, "Заголовок (override)", ref section.title);
                    break;

                case CreditsSectionType.Thanks:
                    DrawField(rect.x + indent, ref y, w, lineH, "Заголовок", ref section.title);
                    DrawField(rect.x + indent, ref y, w, lineH, "Фильтр категории", ref section.thanksCategory);
                    break;

                case CreditsSectionType.Technology:
                    DrawField(rect.x + indent, ref y, w, lineH, "Заголовок", ref section.title);
                    break;

                case CreditsSectionType.Inspirations:
                    DrawField(rect.x + indent, ref y, w, lineH, "Заголовок", ref section.title);
                    break;

                case CreditsSectionType.Quote:
                    DrawQuoteDropdown(rect.x + indent, ref y, w, lineH, section);
                    break;

                case CreditsSectionType.Logo:
                    DrawField(rect.x + indent, ref y, w, lineH, "Текст", ref section.logoText);
                    DrawField(rect.x + indent, ref y, w, lineH, "Акцент", ref section.logoAccent);
                    DrawField(rect.x + indent, ref y, w, lineH, "Год", ref section.logoYear);
                    break;

                case CreditsSectionType.CustomText:
                    var taRect = new Rect(rect.x + indent, y, w, EditorGUIUtility.singleLineHeight * 3);
                    section.customRichText = EditorGUI.TextArea(taRect, section.customRichText ?? "");
                    y += EditorGUIUtility.singleLineHeight * 3 + 4;
                    break;
            }

            // Font overrides (compact)
            DrawFontOverrides(rect.x + indent, ref y, w, lineH, prop);

            // Divider toggle
            section.showDividerAfter = EditorGUI.Toggle(
                new Rect(rect.x + indent, y, w, EditorGUIUtility.singleLineHeight),
                "Разделитель", section.showDividerAfter);
        }

        private void DrawField(float x, ref float y, float w, float lineH, string label, ref string value)
        {
            value = EditorGUI.TextField(new Rect(x, y, w, EditorGUIUtility.singleLineHeight), label, value ?? "");
            y += lineH;
        }

        private void DrawRoleDropdown(float x, ref float y, float w, float lineH, CreditsSection section)
        {
            if (creditsData.roles.Count == 0)
            {
                EditorGUI.LabelField(new Rect(x, y, w, EditorGUIUtility.singleLineHeight), "Нет ролей в справочнике!");
                y += lineH;
                return;
            }

            var ids = creditsData.roles.ConvertAll(r => r.id);
            var names = creditsData.roles.ConvertAll(r => $"{r.displayName} ({r.id})");
            int current = Mathf.Max(0, ids.IndexOf(section.roleId));
            int selected = EditorGUI.Popup(
                new Rect(x, y, w, EditorGUIUtility.singleLineHeight), "Роль", current, names.ToArray());
            section.roleId = ids[selected];
            y += lineH;

            // Preview: count authors
            var count = creditsData.GetAuthorsByRole(section.roleId).Count;
            EditorGUI.LabelField(
                new Rect(x, y, w, EditorGUIUtility.singleLineHeight),
                $"  → {count} авторов", EditorStyles.miniLabel);
            y += lineH;
        }

        private void DrawQuoteDropdown(float x, ref float y, float w, float lineH, CreditsSection section)
        {
            if (creditsData.quotes == null || creditsData.quotes.Count == 0)
            {
                EditorGUI.LabelField(new Rect(x, y, w, EditorGUIUtility.singleLineHeight), "Нет цитат в справочнике!");
                y += lineH;
                return;
            }

            var names = new string[creditsData.quotes.Count];
            for (int i = 0; i < names.Length; i++)
            {
                var q = creditsData.quotes[i];
                var preview = q.text?.Length > 40 ? q.text.Substring(0, 40) + "…" : q.text;
                names[i] = $"[{i}] {preview}";
            }

            section.quoteIndex = EditorGUI.Popup(
                new Rect(x, y, w, EditorGUIUtility.singleLineHeight),
                "Цитата", Mathf.Clamp(section.quoteIndex, 0, names.Length - 1), names);
            y += lineH;
        }

        private void DrawFontOverrides(float x, ref float y, float w, float lineH, SerializedProperty sectionProp)
        {
            float halfW = (w - 10) * 0.5f;

            // Title font + size on one line
            var tfProp = sectionProp.FindPropertyRelative("overrideTitleFont");
            var tsProp = sectionProp.FindPropertyRelative("overrideTitleSize");
            EditorGUI.LabelField(new Rect(x, y, 80, EditorGUIUtility.singleLineHeight), "Title font:", EditorStyles.miniLabel);
            EditorGUI.PropertyField(new Rect(x + 80, y, halfW - 80, EditorGUIUtility.singleLineHeight), tfProp, GUIContent.none);
            EditorGUI.PropertyField(new Rect(x + halfW + 5, y, halfW, EditorGUIUtility.singleLineHeight), tsProp, new GUIContent("size (0=def)"));
            y += lineH;

            // Body font + size
            var bfProp = sectionProp.FindPropertyRelative("overrideBodyFont");
            var bsProp = sectionProp.FindPropertyRelative("overrideBodySize");
            EditorGUI.LabelField(new Rect(x, y, 80, EditorGUIUtility.singleLineHeight), "Body font:", EditorStyles.miniLabel);
            EditorGUI.PropertyField(new Rect(x + 80, y, halfW - 80, EditorGUIUtility.singleLineHeight), bfProp, GUIContent.none);
            EditorGUI.PropertyField(new Rect(x + halfW + 5, y, halfW, EditorGUIUtility.singleLineHeight), bsProp, new GUIContent("size (0=def)"));
            y += lineH;
        }

        private float GetSectionHeight(int index)
        {
            if (index >= creditsData.sections.Count) return EditorGUIUtility.singleLineHeight;
            var s = creditsData.sections[index];
            float lineH = EditorGUIUtility.singleLineHeight + 2;
            float h = lineH + 6; // header row + padding

            if (!s.enabled) return h;

            switch (s.type)
            {
                case CreditsSectionType.Header:     h += lineH * 2; break;
                case CreditsSectionType.RoleGroup:  h += lineH * 3; break; // dropdown + preview + title
                case CreditsSectionType.Thanks:     h += lineH * 2; break;
                case CreditsSectionType.Technology:  h += lineH; break;
                case CreditsSectionType.Inspirations: h += lineH; break;
                case CreditsSectionType.Quote:      h += lineH; break;
                case CreditsSectionType.Logo:       h += lineH * 3; break;
                case CreditsSectionType.CustomText: h += EditorGUIUtility.singleLineHeight * 3 + 4; break;
            }

            h += lineH * 2; // font overrides (2 lines)
            h += lineH;     // divider toggle
            h += 4;         // padding
            return h;
        }

        private string GetSectionSummary(CreditsSection section)
        {
            switch (section.type)
            {
                case CreditsSectionType.Header:
                    return section.headerTitle ?? "";
                case CreditsSectionType.RoleGroup:
                    var role = creditsData.GetRole(section.roleId);
                    return role != null ? $"→ {role.displayName}" : $"→ {section.roleId}";
                case CreditsSectionType.Thanks:
                    return string.IsNullOrEmpty(section.thanksCategory)
                        ? "(все)" : $"→ {section.thanksCategory}";
                case CreditsSectionType.Technology:
                    return $"({creditsData.technologies?.Count ?? 0} шт.)";
                case CreditsSectionType.Inspirations:
                    return $"({creditsData.inspirations?.Count ?? 0} шт.)";
                case CreditsSectionType.Quote:
                    if (creditsData.quotes != null && section.quoteIndex >= 0 && section.quoteIndex < creditsData.quotes.Count)
                    {
                        var t = creditsData.quotes[section.quoteIndex].text;
                        return t?.Length > 30 ? $"«{t.Substring(0, 30)}…»" : $"«{t}»";
                    }
                    return $"[{section.quoteIndex}]";
                case CreditsSectionType.Logo:
                    return $"{section.logoText} {section.logoAccent}";
                case CreditsSectionType.CustomText:
                    var ct = section.customRichText;
                    return ct?.Length > 30 ? ct.Substring(0, 30) + "…" : ct ?? "";
                default:
                    return "";
            }
        }

        private static GUIStyle DisabledStyle()
        {
            if (_disabledLabelStyle == null)
            {
                _disabledLabelStyle = new GUIStyle(EditorStyles.boldLabel);
                _disabledLabelStyle.normal.textColor = Color.gray;
            }
            return _disabledLabelStyle;
        }

        // ═══════════════════════════════════════════════════════════════════
        // REORDERABLE LISTS: ROLES, AUTHORS, THANKS, QUOTES
        // ═══════════════════════════════════════════════════════════════════

        private void SetupRolesList()
        {
            rolesList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("roles"), true, true, true, true);
            rolesList.drawHeaderCallback = r => EditorGUI.LabelField(r, "Роли");
            rolesList.drawElementCallback = (rect, idx, _, _) =>
            {
                var el = rolesList.serializedProperty.GetArrayElementAtIndex(idx);
                rect.y += 2;
                float lh = EditorGUIUtility.singleLineHeight + 2;
                float w3 = (rect.width - 10) / 3;

                EditorGUI.PropertyField(new Rect(rect.x, rect.y, w3, EditorGUIUtility.singleLineHeight),
                    el.FindPropertyRelative("id"), GUIContent.none);
                EditorGUI.PropertyField(new Rect(rect.x + w3 + 5, rect.y, w3, EditorGUIUtility.singleLineHeight),
                    el.FindPropertyRelative("displayName"), GUIContent.none);
                EditorGUI.PropertyField(new Rect(rect.x + w3 * 2 + 10, rect.y, w3 - 5, EditorGUIUtility.singleLineHeight),
                    el.FindPropertyRelative("order"), GUIContent.none);
            };
            rolesList.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 6;
            rolesList.onAddCallback = list =>
            {
                var i = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                var el = list.serializedProperty.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("id").stringValue = $"role_{i}";
                el.FindPropertyRelative("displayName").stringValue = "Новая роль";
                el.FindPropertyRelative("order").intValue = i;
            };
        }

        private void SetupAuthorsList()
        {
            authorsList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("authors"), true, true, true, true);
            authorsList.drawHeaderCallback = r => EditorGUI.LabelField(r, "Авторы");
            authorsList.drawElementCallback = DrawAuthorElement;
            authorsList.elementHeightCallback = _ => GetAuthorHeight();
            authorsList.onAddCallback = list =>
            {
                var i = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                var el = list.serializedProperty.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("name").stringValue = "Новый автор";
                el.FindPropertyRelative("roleIds").ClearArray();
            };
        }

        private void DrawAuthorElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var el = authorsList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;
            float lh = EditorGUIUtility.singleLineHeight + 2;

            // Name + URL
            float half = (rect.width - 5) * 0.6f;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, half, EditorGUIUtility.singleLineHeight),
                el.FindPropertyRelative("name"), GUIContent.none);
            EditorGUI.PropertyField(new Rect(rect.x + half + 5, rect.y, rect.width - half - 5, EditorGUIUtility.singleLineHeight),
                el.FindPropertyRelative("url"), GUIContent.none);

            // Role checkboxes
            rect.y += lh;
            var roleIdsProp = el.FindPropertyRelative("roleIds");
            var roleIds = GetStringList(roleIdsProp);
            float xOff = 0;

            foreach (var role in creditsData.roles)
            {
                bool has = roleIds.Contains(role.id);
                bool val = EditorGUI.ToggleLeft(
                    new Rect(rect.x + xOff, rect.y, 100, EditorGUIUtility.singleLineHeight),
                    role.displayName, has);

                if (val != has)
                {
                    if (val)
                    {
                        roleIdsProp.arraySize++;
                        roleIdsProp.GetArrayElementAtIndex(roleIdsProp.arraySize - 1).stringValue = role.id;
                    }
                    else
                    {
                        for (int i = 0; i < roleIdsProp.arraySize; i++)
                            if (roleIdsProp.GetArrayElementAtIndex(i).stringValue == role.id)
                            { roleIdsProp.DeleteArrayElementAtIndex(i); break; }
                    }
                }

                xOff += 100;
                if (xOff > rect.width - 100) { xOff = 0; rect.y += lh; }
            }
        }

        private float GetAuthorHeight()
        {
            int perRow = Mathf.Max(1, (int)((position.width - 60) / 100));
            int rows = creditsData != null ? Mathf.CeilToInt((float)creditsData.roles.Count / perRow) : 1;
            return EditorGUIUtility.singleLineHeight * (1 + rows) + 10;
        }

        private void SetupThanksList()
        {
            thanksList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("specialThanks"), true, true, true, true);
            thanksList.drawHeaderCallback = r => EditorGUI.LabelField(r, "Благодарности");
            thanksList.drawElementCallback = (rect, idx, _, _) =>
            {
                var el = thanksList.serializedProperty.GetArrayElementAtIndex(idx);
                rect.y += 2;
                float w3 = rect.width * 0.3f;
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, w3 - 5, EditorGUIUtility.singleLineHeight),
                    el.FindPropertyRelative("category"), GUIContent.none);
                EditorGUI.PropertyField(new Rect(rect.x + w3, rect.y, rect.width - w3, EditorGUIUtility.singleLineHeight),
                    el.FindPropertyRelative("text"), GUIContent.none);
            };
            thanksList.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 6;
            thanksList.onAddCallback = list =>
            {
                var i = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                var el = list.serializedProperty.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("category").stringValue = "";
                el.FindPropertyRelative("text").stringValue = "Текст";
            };
        }

        private void SetupQuotesList()
        {
            quotesList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("quotes"), true, true, true, true);
            quotesList.drawHeaderCallback = r => EditorGUI.LabelField(r, "Цитаты");
            quotesList.drawElementCallback = (rect, idx, _, _) =>
            {
                var el = quotesList.serializedProperty.GetArrayElementAtIndex(idx);
                rect.y += 2;
                float lh = EditorGUIUtility.singleLineHeight + 2;

                // Text (2 lines)
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight * 2),
                    el.FindPropertyRelative("text"), GUIContent.none);
                rect.y += EditorGUIUtility.singleLineHeight * 2 + 4;

                // Attribution + style
                float half = (rect.width - 10) * 0.65f;
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, half, EditorGUIUtility.singleLineHeight),
                    el.FindPropertyRelative("attribution"), GUIContent.none);
                EditorGUI.PropertyField(new Rect(rect.x + half + 5, rect.y, rect.width - half - 5, EditorGUIUtility.singleLineHeight),
                    el.FindPropertyRelative("style"), GUIContent.none);
            };
            quotesList.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight * 3 + 12;
            quotesList.onAddCallback = list =>
            {
                var i = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                var el = list.serializedProperty.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("text").stringValue = "Цитата";
                el.FindPropertyRelative("attribution").stringValue = "Автор";
                el.FindPropertyRelative("style").enumValueIndex = (int)QuoteStyle.Italic;
            };
        }

        private List<string> GetStringList(SerializedProperty arrayProp)
        {
            var list = new List<string>();
            for (int i = 0; i < arrayProp.arraySize; i++)
                list.Add(arrayProp.GetArrayElementAtIndex(i).stringValue);
            return list;
        }

        // ═══════════════════════════════════════════════════════════════════
        // TEMPLATE
        // ═══════════════════════════════════════════════════════════════════

        private void FillLastConvoyTemplate()
        {
            if (creditsData.sections.Count > 0 &&
                !EditorUtility.DisplayDialog("Шаблон", "Заменить layout и заполнить справочники?", "Да", "Отмена"))
                return;

            Undo.RecordObject(creditsData, "Last Convoy Template");

            // === Data sources ===

            creditsData.roles.Clear();
            creditsData.roles.Add(new RoleDefinition { id = "dev", displayName = "Разработка", order = 0 });
            creditsData.roles.Add(new RoleDefinition { id = "ai", displayName = "AI-ассистент", order = 1 });
            creditsData.roles.Add(new RoleDefinition { id = "art", displayName = "Визуал", order = 2 });

            creditsData.authors.Clear();
            creditsData.authors.Add(new AuthorEntry
            {
                name = "Anatoly",
                roleIds = new List<string> { "dev" },
            });
            creditsData.authors.Add(new AuthorEntry
            {
                name = "Claude (Anthropic)",
                roleIds = new List<string> { "ai" },
            });
            creditsData.authors.Add(new AuthorEntry
            {
                name = "Midjourney",
                roleIds = new List<string> { "art" },
            });

            creditsData.technologies = new List<string>
            {
                "Unity", "C#", "ProtoSystem", "URP", "Burst", "GPU Instancing"
            };

            creditsData.inspirations = new List<string>
            {
                "Deep Rock Galactic: Survivor",
                "Enter the Gungeon",
                "Hotline Miami",
                "RimWorld"
            };

            creditsData.quotes.Clear();
            creditsData.quotes.Add(new QuoteEntry
            {
                text = "Последний конвой — не просто поезд.\nЭто всё, что осталось от цивилизации.",
                attribution = "Бортовой журнал, запись #001",
                style = QuoteStyle.Italic,
            });

            creditsData.specialThanks.Clear();
            creditsData.specialThanks.Add(new ThanksEntry { text = "Плейтестерам раннего прототипа" });
            creditsData.specialThanks.Add(new ThanksEntry { text = "Сообществу инди-разработчиков" });
            creditsData.specialThanks.Add(new ThanksEntry { text = "Всем, кто дочитал до конца" });

            // === Layout ===

            creditsData.sections.Clear();

            creditsData.sections.Add(new CreditsSection
            {
                enabled = true, type = CreditsSectionType.Header,
                headerTitle = "LAST CONVOY", headerSubtitle = "Armored Survivors",
            });
            creditsData.sections.Add(new CreditsSection
            {
                enabled = true, type = CreditsSectionType.RoleGroup, roleId = "dev",
            });
            creditsData.sections.Add(new CreditsSection
            {
                enabled = true, type = CreditsSectionType.Technology, title = "ТЕХНОЛОГИИ",
            });
            creditsData.sections.Add(new CreditsSection
            {
                enabled = true, type = CreditsSectionType.RoleGroup, roleId = "ai",
            });
            creditsData.sections.Add(new CreditsSection
            {
                enabled = true, type = CreditsSectionType.RoleGroup, roleId = "art",
            });
            creditsData.sections.Add(new CreditsSection
            {
                enabled = true, type = CreditsSectionType.Inspirations, title = "ВДОХНОВЕНИЕ",
            });
            creditsData.sections.Add(new CreditsSection
            {
                enabled = true, type = CreditsSectionType.Quote, quoteIndex = 0,
            });
            creditsData.sections.Add(new CreditsSection
            {
                enabled = true, type = CreditsSectionType.Thanks, title = "ОТДЕЛЬНАЯ БЛАГОДАРНОСТЬ",
            });
            creditsData.sections.Add(new CreditsSection
            {
                enabled = true, type = CreditsSectionType.Logo,
                logoText = "LAST", logoAccent = "CONVOY", logoYear = "2026",
                showDividerAfter = false,
            });

            EditorUtility.SetDirty(creditsData);
            SetupAll();
        }

        // ═══════════════════════════════════════════════════════════════════
        // CREATE / DEFAULTS
        // ═══════════════════════════════════════════════════════════════════

        private void DrawCreateButton()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox("Выберите CreditsData или создайте новый", MessageType.Info);
            var path = GetProjectCreditsPath();
            EditorGUILayout.LabelField("Путь:", path, EditorStyles.miniLabel);
            EditorGUILayout.Space(10);
            if (GUILayout.Button("Создать CreditsData", GUILayout.Height(30)))
            {
                var dir = Path.GetDirectoryName(path);
                if (!AssetDatabase.IsValidFolder(dir)) CreateFolderRecursive(dir);
                creditsData = CreateInstance<CreditsData>();
                AddDefaultRolesToData(creditsData);
                AssetDatabase.CreateAsset(creditsData, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                SetupAll();
            }
        }

        private void AddDefaultRoles()
        {
            Undo.RecordObject(creditsData, "Add Default Roles");
            AddDefaultRolesToData(creditsData);
            EditorUtility.SetDirty(creditsData);
        }

        private void AddDefaultRolesToData(CreditsData data)
        {
            var defaults = new[]
            {
                ("dev", "Разработка", 0), ("design", "Дизайн", 1), ("art", "Арт", 2),
                ("music", "Музыка", 3), ("sound", "Звук", 4), ("writing", "Сценарий", 5),
                ("qa", "Тестирование", 6), ("management", "Менеджмент", 7)
            };
            foreach (var (id, name, order) in defaults)
                if (!data.roles.Exists(r => r.id == id))
                    data.roles.Add(new RoleDefinition { id = id, displayName = name, order = order });
        }

        private string GetProjectCreditsPath()
        {
            var asmGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { "Assets" });
            string ns = null;
            foreach (var g in asmGuids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (p.Contains("Editor")) continue;
                var parts = p.Split('/');
                if (parts.Length >= 2 && parts[0] == "Assets") { ns = parts[1]; break; }
            }
            if (string.IsNullOrEmpty(ns))
            {
                foreach (var f in AssetDatabase.GetSubFolders("Assets"))
                {
                    var n = Path.GetFileName(f);
                    if (n is "Plugins" or "Editor" or "Resources" or "StreamingAssets" or "Gizmos" ||
                        n.StartsWith(".")) continue;
                    ns = n; break;
                }
            }
            return $"Assets/{ns ?? "Game"}/Resources/Data/Credits/CreditsData.asset";
        }

        private void CreateFolderRecursive(string path)
        {
            path = path.Replace("\\", "/");
            var parts = path.Split('/');
            if (parts[0] != "Assets") return;
            var cur = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
            AssetDatabase.Refresh();
        }
    }
}
