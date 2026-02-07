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
    /// Поддерживает как legacy-режим (роли/авторы), так и sections-режим.
    /// </summary>
    public class CreditsEditorWindow : EditorWindow
    {
        private CreditsData creditsData;
        private SerializedObject serializedObject;
        
        private ReorderableList rolesList;
        private ReorderableList authorsList;
        private ReorderableList thanksList;
        private ReorderableList sectionsList;
        
        private Vector2 scrollPosition;
        private int selectedTab = 0;
        private string[] tabNames = { "Секции", "Legacy: Роли", "Legacy: Авторы", "Legacy: Благодарности", "Предпросмотр" };

        // Стили
        private static GUIStyle _sectionHeaderStyle;
        private static GUIStyle _sectionBoxStyle;
        private static GUIStyle _disabledLabelStyle;

        // Цвета секций по типу
        private static readonly Dictionary<CreditsSectionType, Color> SectionColors = new()
        {
            { CreditsSectionType.Header,     new Color(0.9f, 0.7f, 0.3f, 0.15f) },
            { CreditsSectionType.Team,       new Color(0.3f, 0.7f, 0.9f, 0.15f) },
            { CreditsSectionType.Technology, new Color(0.5f, 0.9f, 0.5f, 0.15f) },
            { CreditsSectionType.SimpleList, new Color(0.7f, 0.5f, 0.9f, 0.15f) },
            { CreditsSectionType.Quote,      new Color(0.9f, 0.5f, 0.5f, 0.15f) },
            { CreditsSectionType.Logo,       new Color(0.9f, 0.8f, 0.3f, 0.15f) },
        };

        private static readonly string[] SectionTypeLabels = 
        {
            "🎮 Header", "👥 Team", "⚙ Technology", "📋 List", "💬 Quote", "🏷 Logo"
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
            FindOrCreateCreditsData();
        }

        private void FindOrCreateCreditsData()
        {
            var guids = AssetDatabase.FindAssets("t:CreditsData");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                creditsData = AssetDatabase.LoadAssetAtPath<CreditsData>(path);
            }

            if (creditsData != null)
            {
                SetupSerializedObject();
            }
        }

        private string GetProjectCreditsPath()
        {
            var asmdefGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { "Assets" });
            string projectNamespace = null;

            foreach (var guid in asmdefGuids)
            {
                var asmdefPath = AssetDatabase.GUIDToAssetPath(guid);
                if (asmdefPath.Contains("Editor")) continue;
                var parts = asmdefPath.Split('/');
                if (parts.Length >= 2 && parts[0] == "Assets")
                {
                    projectNamespace = parts[1];
                    break;
                }
            }

            if (string.IsNullOrEmpty(projectNamespace))
            {
                var subfolders = AssetDatabase.GetSubFolders("Assets");
                foreach (var folder in subfolders)
                {
                    var folderName = Path.GetFileName(folder);
                    if (folderName is "Plugins" or "Editor" or "Resources" or "StreamingAssets"
                        or "Gizmos" or "Editor Default Resources" || folderName.StartsWith("."))
                        continue;
                    projectNamespace = folderName;
                    break;
                }
            }

            if (string.IsNullOrEmpty(projectNamespace))
                projectNamespace = "Game";

            return $"Assets/{projectNamespace}/Resources/Data/Credits/CreditsData.asset";
        }

        private void SetupSerializedObject()
        {
            if (creditsData == null) return;

            serializedObject = new SerializedObject(creditsData);
            
            SetupSectionsList();
            SetupRolesList();
            SetupAuthorsList();
            SetupThanksList();
        }

        // ═══════════════════════════════════════════════════════════════════
        // SECTIONS LIST
        // ═══════════════════════════════════════════════════════════════════

        private void SetupSectionsList()
        {
            sectionsList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("sections"),
                true, true, true, true);

            sectionsList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, $"Секции ({creditsData.sections.Count})");
            };

            sectionsList.drawElementCallback = DrawSectionElement;
            sectionsList.elementHeightCallback = GetSectionElementHeight;

            sectionsList.onAddDropdownCallback = (rect, list) =>
            {
                var menu = new GenericMenu();
                for (int i = 0; i < SectionTypeLabels.Length; i++)
                {
                    var type = (CreditsSectionType)i;
                    menu.AddItem(new GUIContent(SectionTypeLabels[i]), false, () => AddSection(type));
                }
                menu.DropDown(rect);
            };
        }

        private void AddSection(CreditsSectionType type)
        {
            Undo.RecordObject(creditsData, "Add Credits Section");

            var section = new CreditsSection
            {
                enabled = true,
                type = type,
                showDividerAfter = type != CreditsSectionType.Logo,
            };

            switch (type)
            {
                case CreditsSectionType.Header:
                    section.persons = new List<CreditsPerson>
                    {
                        new() { name = "GAME TITLE", role = "Subtitle" }
                    };
                    break;
                case CreditsSectionType.Team:
                    section.title = "SECTION TITLE";
                    section.persons = new List<CreditsPerson>
                    {
                        new() { name = "Name", role = "Role" }
                    };
                    break;
                case CreditsSectionType.Technology:
                    section.title = "TECHNOLOGY";
                    section.tags = new List<string> { "Unity", "C#" };
                    break;
                case CreditsSectionType.SimpleList:
                    section.title = "THANKS";
                    section.items = new List<string> { "Item 1" };
                    break;
                case CreditsSectionType.Quote:
                    section.quoteText = "Quote text here";
                    section.quoteAttribution = "Author";
                    break;
                case CreditsSectionType.Logo:
                    section.persons = new List<CreditsPerson>
                    {
                        new() { name = "LAST", role = "CONVOY" }
                    };
                    section.logoYear = "2026";
                    section.showDividerAfter = false;
                    break;
            }

            creditsData.sections.Add(section);
            EditorUtility.SetDirty(creditsData);
            SetupSerializedObject();
        }

        private void DrawSectionElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index >= creditsData.sections.Count) return;

            var section = creditsData.sections[index];
            var prop = sectionsList.serializedProperty.GetArrayElementAtIndex(index);
            float y = rect.y + 2;
            float lineH = EditorGUIUtility.singleLineHeight + 2;
            float indent = 16;

            // Фоновый цвет по типу
            if (SectionColors.TryGetValue(section.type, out var bgColor))
            {
                var bgRect = new Rect(rect.x - 4, rect.y, rect.width + 8, GetSectionElementHeight(index));
                if (!section.enabled) bgColor.a *= 0.3f;
                EditorGUI.DrawRect(bgRect, bgColor);
            }

            // ── Row 1: enabled + type + title summary ──
            var enabledRect = new Rect(rect.x, y, 16, EditorGUIUtility.singleLineHeight);
            section.enabled = EditorGUI.Toggle(enabledRect, section.enabled);

            var typeLabel = SectionTypeLabels[(int)section.type];
            var summaryText = GetSectionSummary(section);
            
            var labelStyle = section.enabled ? EditorStyles.boldLabel : GetDisabledLabelStyle();
            var labelRect = new Rect(rect.x + 20, y, rect.width - 20, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, $"{typeLabel}  {summaryText}", labelStyle);

            if (!section.enabled) return; // Скрываем поля отключённых секций

            y += lineH + 2;

            // ── Fields по типу ──
            switch (section.type)
            {
                case CreditsSectionType.Header:
                    DrawPersonsCompact(rect, ref y, prop, lineH, indent);
                    break;

                case CreditsSectionType.Team:
                    DrawTitleField(rect, ref y, prop, lineH, indent);
                    DrawPersonsCompact(rect, ref y, prop, lineH, indent);
                    // + button
                    var addBtnRect = new Rect(rect.x + indent, y, 120, EditorGUIUtility.singleLineHeight);
                    if (GUI.Button(addBtnRect, "+ Добавить"))
                    {
                        Undo.RecordObject(creditsData, "Add Person");
                        section.persons.Add(new CreditsPerson { name = "Name", role = "Role" });
                        EditorUtility.SetDirty(creditsData);
                    }
                    y += lineH;
                    break;

                case CreditsSectionType.Technology:
                    DrawTitleField(rect, ref y, prop, lineH, indent);
                    DrawTagsField(rect, ref y, section, lineH, indent);
                    break;

                case CreditsSectionType.SimpleList:
                    DrawTitleField(rect, ref y, prop, lineH, indent);
                    DrawItemsField(rect, ref y, section, lineH, indent);
                    break;

                case CreditsSectionType.Quote:
                    var qtRect = new Rect(rect.x + indent, y, rect.width - indent, EditorGUIUtility.singleLineHeight * 2);
                    section.quoteText = EditorGUI.TextArea(qtRect, section.quoteText ?? "");
                    y += EditorGUIUtility.singleLineHeight * 2 + 4;

                    var attrRect = new Rect(rect.x + indent, y, rect.width - indent, EditorGUIUtility.singleLineHeight);
                    section.quoteAttribution = EditorGUI.TextField(attrRect, "Автор", section.quoteAttribution ?? "");
                    y += lineH;
                    break;

                case CreditsSectionType.Logo:
                    DrawPersonsCompact(rect, ref y, prop, lineH, indent);
                    var yearRect = new Rect(rect.x + indent, y, rect.width - indent, EditorGUIUtility.singleLineHeight);
                    section.logoYear = EditorGUI.TextField(yearRect, "Год", section.logoYear ?? "");
                    y += lineH;
                    break;
            }

            // Divider toggle
            var divRect = new Rect(rect.x + indent, y, rect.width - indent, EditorGUIUtility.singleLineHeight);
            section.showDividerAfter = EditorGUI.Toggle(divRect, "Разделитель после", section.showDividerAfter);
        }

        private void DrawTitleField(Rect rect, ref float y, SerializedProperty prop, float lineH, float indent)
        {
            var titleProp = prop.FindPropertyRelative("title");
            var titleRect = new Rect(rect.x + indent, y, rect.width - indent, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(titleRect, titleProp, new GUIContent("Заголовок"));
            y += lineH;
        }

        private void DrawPersonsCompact(Rect rect, ref float y, SerializedProperty prop, float lineH, float indent)
        {
            var personsProp = prop.FindPropertyRelative("persons");
            for (int i = 0; i < personsProp.arraySize; i++)
            {
                var person = personsProp.GetArrayElementAtIndex(i);
                float halfW = (rect.width - indent - 24) * 0.5f;

                var nameRect = new Rect(rect.x + indent, y, halfW, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(nameRect, person.FindPropertyRelative("name"), GUIContent.none);

                var roleRect = new Rect(rect.x + indent + halfW + 4, y, halfW, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(roleRect, person.FindPropertyRelative("role"), GUIContent.none);

                // Кнопка удаления (кроме первого в Header/Logo)
                var delRect = new Rect(rect.x + rect.width - 18, y, 18, EditorGUIUtility.singleLineHeight);
                if (personsProp.arraySize > 1 || 
                    (CreditsSectionType)prop.FindPropertyRelative("type").enumValueIndex == CreditsSectionType.Team)
                {
                    if (GUI.Button(delRect, "×"))
                    {
                        personsProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                y += lineH;
            }
        }

        private void DrawTagsField(Rect rect, ref float y, CreditsSection section, float lineH, float indent)
        {
            if (section.tags == null) section.tags = new List<string>();

            for (int i = 0; i < section.tags.Count; i++)
            {
                var tagRect = new Rect(rect.x + indent, y, rect.width - indent - 24, EditorGUIUtility.singleLineHeight);
                section.tags[i] = EditorGUI.TextField(tagRect, section.tags[i] ?? "");
                
                var delRect = new Rect(rect.x + rect.width - 18, y, 18, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(delRect, "×"))
                {
                    section.tags.RemoveAt(i);
                    EditorUtility.SetDirty(creditsData);
                    break;
                }
                y += lineH;
            }

            var addRect = new Rect(rect.x + indent, y, 80, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(addRect, "+ Тег"))
            {
                section.tags.Add("New");
                EditorUtility.SetDirty(creditsData);
            }
            y += lineH;
        }

        private void DrawItemsField(Rect rect, ref float y, CreditsSection section, float lineH, float indent)
        {
            if (section.items == null) section.items = new List<string>();

            for (int i = 0; i < section.items.Count; i++)
            {
                var itemRect = new Rect(rect.x + indent, y, rect.width - indent - 24, EditorGUIUtility.singleLineHeight);
                section.items[i] = EditorGUI.TextField(itemRect, section.items[i] ?? "");
                
                var delRect = new Rect(rect.x + rect.width - 18, y, 18, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(delRect, "×"))
                {
                    section.items.RemoveAt(i);
                    EditorUtility.SetDirty(creditsData);
                    break;
                }
                y += lineH;
            }

            var addRect = new Rect(rect.x + indent, y, 80, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(addRect, "+ Пункт"))
            {
                section.items.Add("New item");
                EditorUtility.SetDirty(creditsData);
            }
            y += lineH;
        }

        private float GetSectionElementHeight(int index)
        {
            if (index >= creditsData.sections.Count) return EditorGUIUtility.singleLineHeight;

            var section = creditsData.sections[index];
            float lineH = EditorGUIUtility.singleLineHeight + 2;
            float h = lineH + 4; // header row

            if (!section.enabled) return h;

            switch (section.type)
            {
                case CreditsSectionType.Header:
                    h += lineH * Mathf.Max(1, section.persons?.Count ?? 0);
                    break;
                case CreditsSectionType.Team:
                    h += lineH; // title
                    h += lineH * Mathf.Max(1, section.persons?.Count ?? 0);
                    h += lineH; // add button
                    break;
                case CreditsSectionType.Technology:
                    h += lineH; // title
                    h += lineH * Mathf.Max(1, section.tags?.Count ?? 0);
                    h += lineH; // add button
                    break;
                case CreditsSectionType.SimpleList:
                    h += lineH; // title
                    h += lineH * Mathf.Max(1, section.items?.Count ?? 0);
                    h += lineH; // add button
                    break;
                case CreditsSectionType.Quote:
                    h += EditorGUIUtility.singleLineHeight * 2 + 4; // textarea
                    h += lineH; // attribution
                    break;
                case CreditsSectionType.Logo:
                    h += lineH * Mathf.Max(1, section.persons?.Count ?? 0);
                    h += lineH; // year
                    break;
            }

            h += lineH; // divider toggle
            h += 4; // padding
            return h;
        }

        private string GetSectionSummary(CreditsSection section)
        {
            switch (section.type)
            {
                case CreditsSectionType.Header:
                    return section.persons?.Count > 0 ? section.persons[0].name : "";
                case CreditsSectionType.Team:
                    var count = section.persons?.Count ?? 0;
                    return $"\"{section.title}\" ({count} чел.)";
                case CreditsSectionType.Technology:
                    return $"\"{section.title}\" ({section.tags?.Count ?? 0} тегов)";
                case CreditsSectionType.SimpleList:
                    return $"\"{section.title}\" ({section.items?.Count ?? 0} шт.)";
                case CreditsSectionType.Quote:
                    var preview = section.quoteText?.Length > 30 
                        ? section.quoteText.Substring(0, 30) + "…" 
                        : section.quoteText;
                    return $"«{preview}»";
                case CreditsSectionType.Logo:
                    return section.logoYear ?? "";
                default:
                    return "";
            }
        }

        private static GUIStyle GetDisabledLabelStyle()
        {
            if (_disabledLabelStyle == null)
            {
                _disabledLabelStyle = new GUIStyle(EditorStyles.boldLabel);
                _disabledLabelStyle.normal.textColor = Color.gray;
            }
            return _disabledLabelStyle;
        }

        // ═══════════════════════════════════════════════════════════════════
        // LEGACY LISTS
        // ═══════════════════════════════════════════════════════════════════

        private void SetupRolesList()
        {
            rolesList = new ReorderableList(serializedObject, 
                serializedObject.FindProperty("roles"), 
                true, true, true, true);
            
            rolesList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Роли (порядок = порядок отображения)");
            rolesList.drawElementCallback = DrawRoleElement;
            rolesList.elementHeightCallback = index => EditorGUIUtility.singleLineHeight * 3 + 10;
            rolesList.onAddCallback = list =>
            {
                var index = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                var element = list.serializedProperty.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("id").stringValue = $"role_{index}";
                element.FindPropertyRelative("displayName").stringValue = "Новая роль";
                element.FindPropertyRelative("order").intValue = index;
            };
        }

        private void SetupAuthorsList()
        {
            authorsList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("authors"),
                true, true, true, true);
            
            authorsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Авторы");
            authorsList.drawElementCallback = DrawAuthorElement;
            authorsList.elementHeightCallback = index => GetAuthorElementHeight(index);
            authorsList.onAddCallback = list =>
            {
                var index = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                var element = list.serializedProperty.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("name").stringValue = "Новый автор";
                element.FindPropertyRelative("roleIds").ClearArray();
            };
        }

        private void SetupThanksList()
        {
            thanksList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("specialThanks"),
                true, true, true, true);
            
            thanksList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Благодарности");
            thanksList.drawElementCallback = DrawThanksElement;
            thanksList.elementHeightCallback = index => EditorGUIUtility.singleLineHeight * 3 + 10;
            thanksList.onAddCallback = list =>
            {
                var index = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                var element = list.serializedProperty.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("category").stringValue = "";
                element.FindPropertyRelative("text").stringValue = "Текст благодарности";
            };
        }

        private void DrawRoleElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = rolesList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;
            float lineHeight = EditorGUIUtility.singleLineHeight + 2;

            var idRect = new Rect(rect.x, rect.y, rect.width * 0.3f - 5, EditorGUIUtility.singleLineHeight);
            var idLabelRect = new Rect(rect.x, rect.y, 25, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(idLabelRect, "ID");
            idRect.x += 25;
            idRect.width -= 25;
            EditorGUI.PropertyField(idRect, element.FindPropertyRelative("id"), GUIContent.none);

            var nameRect = new Rect(rect.x + rect.width * 0.3f, rect.y, rect.width * 0.7f, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(nameRect, element.FindPropertyRelative("displayName"), new GUIContent("Название"));

            rect.y += lineHeight;
            var orderRect = new Rect(rect.x, rect.y, rect.width * 0.3f, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(orderRect, element.FindPropertyRelative("order"), new GUIContent("Порядок"));
        }

        private void DrawAuthorElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = authorsList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;
            float lineHeight = EditorGUIUtility.singleLineHeight + 2;

            var nameRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(nameRect, element.FindPropertyRelative("name"), new GUIContent("Имя"));

            rect.y += lineHeight;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, 50, EditorGUIUtility.singleLineHeight), "Роли:");

            var roleIdsProp = element.FindPropertyRelative("roleIds");
            var roleIds = GetStringList(roleIdsProp);

            float xOffset = 55;
            foreach (var role in creditsData.roles)
            {
                bool hasRole = roleIds.Contains(role.id);
                var toggleRect = new Rect(rect.x + xOffset, rect.y, 20, EditorGUIUtility.singleLineHeight);
                var labelRect = new Rect(rect.x + xOffset + 18, rect.y, 80, EditorGUIUtility.singleLineHeight);
                
                bool newValue = EditorGUI.Toggle(toggleRect, hasRole);
                EditorGUI.LabelField(labelRect, role.displayName);
                
                if (newValue != hasRole)
                {
                    if (newValue)
                    {
                        roleIdsProp.arraySize++;
                        roleIdsProp.GetArrayElementAtIndex(roleIdsProp.arraySize - 1).stringValue = role.id;
                    }
                    else
                    {
                        for (int i = 0; i < roleIdsProp.arraySize; i++)
                        {
                            if (roleIdsProp.GetArrayElementAtIndex(i).stringValue == role.id)
                            {
                                roleIdsProp.DeleteArrayElementAtIndex(i);
                                break;
                            }
                        }
                    }
                }
                
                xOffset += 100;
                if (xOffset > rect.width - 100)
                {
                    xOffset = 55;
                    rect.y += lineHeight;
                }
            }

            rect.y += lineHeight;
            var urlRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(urlRect, element.FindPropertyRelative("url"), new GUIContent("URL (опц.)"));
        }

        private float GetAuthorElementHeight(int index)
        {
            int rolesPerRow = Mathf.Max(1, (int)((position.width - 100) / 100));
            int roleRows = creditsData != null ? Mathf.CeilToInt((float)creditsData.roles.Count / rolesPerRow) : 1;
            return EditorGUIUtility.singleLineHeight * (3 + roleRows) + 15;
        }

        private void DrawThanksElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = thanksList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;
            float lineHeight = EditorGUIUtility.singleLineHeight + 2;

            var catRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(catRect, element.FindPropertyRelative("category"), new GUIContent("Категория"));

            rect.y += lineHeight;
            var textRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(textRect, element.FindPropertyRelative("text"), new GUIContent("Текст"));
        }

        private List<string> GetStringList(SerializedProperty arrayProp)
        {
            var list = new List<string>();
            for (int i = 0; i < arrayProp.arraySize; i++)
                list.Add(arrayProp.GetArrayElementAtIndex(i).stringValue);
            return list;
        }

        // ═══════════════════════════════════════════════════════════════════
        // MAIN GUI
        // ═══════════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Credits Data", EditorStyles.boldLabel);
            
            var newData = (CreditsData)EditorGUILayout.ObjectField(creditsData, typeof(CreditsData), false);
            if (newData != creditsData)
            {
                creditsData = newData;
                if (creditsData != null) SetupSerializedObject();
                else serializedObject = null;
            }
            EditorGUILayout.EndHorizontal();

            // Create button if no data
            if (creditsData == null)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox("Выберите существующий CreditsData или создайте новый", MessageType.Info);
                
                var expectedPath = GetProjectCreditsPath();
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Путь создания:", expectedPath, EditorStyles.miniLabel);
                
                EditorGUILayout.Space(10);
                if (GUILayout.Button("Создать CreditsData", GUILayout.Height(30)))
                    CreateNewCreditsData();
                return;
            }

            if (serializedObject == null)
            {
                SetupSerializedObject();
                if (serializedObject == null)
                {
                    EditorGUILayout.HelpBox("Ошибка инициализации SerializedObject", MessageType.Error);
                    return;
                }
            }

            // Mode indicator
            EditorGUILayout.Space(5);
            var mode = creditsData.UseSections ? "Sections" : "Legacy";
            var modeColor = creditsData.UseSections ? Color.green : Color.yellow;
            var prevColor = GUI.contentColor;
            GUI.contentColor = modeColor;
            EditorGUILayout.LabelField($"Режим: {mode}", EditorStyles.miniLabel);
            GUI.contentColor = prevColor;

            // Tabs
            EditorGUILayout.Space(5);
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
            EditorGUILayout.Space(10);
            
            serializedObject.Update();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (selectedTab)
            {
                case 0: DrawSectionsTab(); break;
                case 1: DrawRolesTab();    break;
                case 2: DrawAuthorsTab();  break;
                case 3: DrawThanksTab();   break;
                case 4: DrawPreviewTab();  break;
            }

            EditorGUILayout.EndScrollView();
            serializedObject.ApplyModifiedProperties();

            // Save button
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Сохранить", GUILayout.Width(100), GUILayout.Height(25)))
            {
                EditorUtility.SetDirty(creditsData);
                AssetDatabase.SaveAssets();
                Debug.Log("[CreditsEditor] Данные сохранены");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        // ═══════════════════════════════════════════════════════════════════
        // TABS
        // ═══════════════════════════════════════════════════════════════════

        private void DrawSectionsTab()
        {
            EditorGUILayout.LabelField("Секции титров", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Секции отображаются сверху вниз. Перетаскивайте для изменения порядка.\n" +
                "Снимите галочку чтобы временно скрыть секцию.\n" +
                "Если список секций непуст — legacy-поля игнорируются.",
                MessageType.Info);
            EditorGUILayout.Space(5);

            sectionsList?.DoLayoutList();

            EditorGUILayout.Space(10);

            // Quick-fill presets
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Заполнить шаблоном Last Convoy"))
                FillLastConvoyTemplate();
            if (GUILayout.Button("Очистить все секции"))
            {
                if (EditorUtility.DisplayDialog("Подтверждение", "Удалить все секции?", "Да", "Отмена"))
                {
                    Undo.RecordObject(creditsData, "Clear Sections");
                    creditsData.sections.Clear();
                    EditorUtility.SetDirty(creditsData);
                    SetupSerializedObject();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRolesTab()
        {
            if (creditsData.UseSections)
            {
                EditorGUILayout.HelpBox("Активен режим Sections. Legacy-поля не используются в GenerateCreditsText().", MessageType.Warning);
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.LabelField("Legacy: Роли", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            rolesList?.DoLayoutList();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Добавить стандартные роли"))
                AddDefaultRoles();
        }

        private void DrawAuthorsTab()
        {
            if (creditsData.UseSections)
            {
                EditorGUILayout.HelpBox("Активен режим Sections. Legacy-поля не используются в GenerateCreditsText().", MessageType.Warning);
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.LabelField("Legacy: Авторы", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (creditsData.roles.Count == 0)
            {
                EditorGUILayout.HelpBox("Сначала добавьте роли на вкладке 'Legacy: Роли'", MessageType.Warning);
                return;
            }
            authorsList?.DoLayoutList();
        }

        private void DrawThanksTab()
        {
            if (creditsData.UseSections)
            {
                EditorGUILayout.HelpBox("Активен режим Sections. Legacy-поля не используются в GenerateCreditsText().", MessageType.Warning);
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.LabelField("Legacy: Благодарности", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            thanksList?.DoLayoutList();
        }

        private void DrawPreviewTab()
        {
            EditorGUILayout.LabelField("Предпросмотр текста Credits", EditorStyles.boldLabel);

            var mode = creditsData.UseSections ? "Sections" : "Legacy";
            EditorGUILayout.LabelField($"Источник: {mode}", EditorStyles.miniLabel);
            
            if (creditsData.UseSections)
            {
                var enabledCount = creditsData.GetEnabledSections().Count;
                var totalCount = creditsData.sections.Count;
                EditorGUILayout.LabelField($"Секций: {enabledCount}/{totalCount} включено", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(10);

            var previewText = creditsData.GenerateCreditsText();
            var style = new GUIStyle(EditorStyles.textArea) { richText = true, wordWrap = true };
            EditorGUILayout.TextArea(previewText, style, GUILayout.ExpandHeight(true));

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Скопировать в буфер обмена"))
            {
                GUIUtility.systemCopyBuffer = previewText;
                Debug.Log("[CreditsEditor] Текст скопирован");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // TEMPLATES
        // ═══════════════════════════════════════════════════════════════════

        private void FillLastConvoyTemplate()
        {
            if (creditsData.sections.Count > 0)
            {
                if (!EditorUtility.DisplayDialog("Подтверждение", 
                    "Заменить текущие секции шаблоном Last Convoy?", "Да", "Отмена"))
                    return;
            }

            Undo.RecordObject(creditsData, "Fill Last Convoy Template");
            creditsData.sections.Clear();

            creditsData.sections.Add(new CreditsSection
            {
                enabled = true,
                type = CreditsSectionType.Header,
                persons = new List<CreditsPerson> { new() { name = "LAST CONVOY", role = "Armored Survivors" } },
            });

            creditsData.sections.Add(new CreditsSection
            {
                enabled = true,
                type = CreditsSectionType.Team,
                title = "РАЗРАБОТКА",
                persons = new List<CreditsPerson>
                {
                    new() { name = "ANATOLY", role = "Game Design · Programming · Art Direction" }
                },
            });

            creditsData.sections.Add(new CreditsSection
            {
                enabled = true,
                type = CreditsSectionType.Technology,
                title = "ТЕХНОЛОГИИ",
                tags = new List<string> { "Unity", "C#", "ProtoSystem", "URP", "Burst", "GPU Instancing" },
            });

            creditsData.sections.Add(new CreditsSection
            {
                enabled = true,
                type = CreditsSectionType.Team,
                title = "AI-АССИСТЕНТ",
                persons = new List<CreditsPerson>
                {
                    new() { name = "CLAUDE", role = "Anthropic · Code Generation · Design Consultation" }
                },
            });

            creditsData.sections.Add(new CreditsSection
            {
                enabled = true,
                type = CreditsSectionType.Team,
                title = "ВИЗУАЛ",
                persons = new List<CreditsPerson>
                {
                    new() { name = "MIDJOURNEY", role = "Concept Art · Asset Generation" }
                },
                tags = new List<string> { "Russo One — заголовки", "Noto Sans — основной текст" },
            });

            creditsData.sections.Add(new CreditsSection
            {
                enabled = true,
                type = CreditsSectionType.SimpleList,
                title = "ВДОХНОВЕНИЕ",
                items = new List<string>
                {
                    "Deep Rock Galactic: Survivor",
                    "Enter the Gungeon",
                    "Hotline Miami",
                    "RimWorld"
                },
            });

            creditsData.sections.Add(new CreditsSection
            {
                enabled = true,
                type = CreditsSectionType.Quote,
                quoteText = "Последний конвой — не просто поезд.\nЭто всё, что осталось от цивилизации.",
                quoteAttribution = "Бортовой журнал, запись #001",
            });

            creditsData.sections.Add(new CreditsSection
            {
                enabled = true,
                type = CreditsSectionType.SimpleList,
                title = "ОТДЕЛЬНАЯ БЛАГОДАРНОСТЬ",
                items = new List<string>
                {
                    "Плейтестерам раннего прототипа",
                    "Сообществу инди-разработчиков",
                    "Всем, кто дочитал до конца"
                },
            });

            creditsData.sections.Add(new CreditsSection
            {
                enabled = true,
                type = CreditsSectionType.Logo,
                persons = new List<CreditsPerson> { new() { name = "LAST", role = "CONVOY" } },
                logoYear = "2026",
                showDividerAfter = false,
            });

            EditorUtility.SetDirty(creditsData);
            SetupSerializedObject();
            Debug.Log("[CreditsEditor] Шаблон Last Convoy заполнен");
        }

        // ═══════════════════════════════════════════════════════════════════
        // CREATE / DEFAULTS
        // ═══════════════════════════════════════════════════════════════════

        private void CreateNewCreditsData()
        {
            var assetPath = GetProjectCreditsPath();
            var directory = Path.GetDirectoryName(assetPath);
            
            if (!AssetDatabase.IsValidFolder(directory))
                CreateFolderRecursive(directory);

            creditsData = CreateInstance<CreditsData>();
            AddDefaultRolesToData(creditsData);

            AssetDatabase.CreateAsset(creditsData, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            SetupSerializedObject();
            Debug.Log($"[CreditsEditor] Создан CreditsData: {assetPath}");
        }

        private void AddDefaultRoles()
        {
            if (creditsData == null) return;
            Undo.RecordObject(creditsData, "Add Default Roles");
            AddDefaultRolesToData(creditsData);
            EditorUtility.SetDirty(creditsData);
        }

        private void AddDefaultRolesToData(CreditsData data)
        {
            var defaultRoles = new[]
            {
                ("dev", "Разработка", 0),
                ("design", "Дизайн", 1),
                ("art", "Арт", 2),
                ("music", "Музыка", 3),
                ("sound", "Звук", 4),
                ("writing", "Сценарий", 5),
                ("qa", "Тестирование", 6),
                ("management", "Менеджмент", 7)
            };

            foreach (var (id, name, order) in defaultRoles)
            {
                if (!data.roles.Exists(r => r.id == id))
                {
                    data.roles.Add(new RoleDefinition
                    {
                        id = id,
                        displayName = name,
                        order = order
                    });
                }
            }
        }

        private void CreateFolderRecursive(string path)
        {
            path = path.Replace("\\", "/");
            var parts = path.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                Debug.LogError($"[CreditsEditor] Invalid path: {path}");
                return;
            }

            var current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                var folderName = parts[i];
                if (string.IsNullOrEmpty(folderName)) continue;
                
                var next = current + "/" + folderName;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    var guid = AssetDatabase.CreateFolder(current, folderName);
                    if (string.IsNullOrEmpty(guid))
                    {
                        Debug.LogError($"[CreditsEditor] Failed to create: {next}");
                        return;
                    }
                }
                current = next;
            }
            AssetDatabase.Refresh();
        }
    }
}
