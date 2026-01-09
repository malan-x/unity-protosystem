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
    /// Окно редактора для управления данными Credits
    /// </summary>
    public class CreditsEditorWindow : EditorWindow
    {
        private CreditsData creditsData;
        private SerializedObject serializedObject;
        
        private ReorderableList rolesList;
        private ReorderableList authorsList;
        private ReorderableList thanksList;
        
        private Vector2 scrollPosition;
        private int selectedTab = 0;
        private string[] tabNames = { "Роли", "Авторы", "Благодарности", "Предпросмотр" };

        [MenuItem("ProtoSystem/UI/Credits Editor", priority = 130)]
        public static void ShowWindow()
        {
            var window = GetWindow<CreditsEditorWindow>("Credits Editor");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private void OnEnable()
        {
            // Пытаемся найти существующий CreditsData
            FindOrCreateCreditsData();
        }

        private void FindOrCreateCreditsData()
        {
            // Ищем в проекте
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

        /// <summary>
        /// Определяет путь для сохранения данных на основе namespace проекта
        /// Ищет asmdef файлы в Assets/ для определения namespace
        /// </summary>
        private string GetProjectCreditsPath()
        {
            // Ищем asmdef файлы в Assets (не в Packages)
            var asmdefGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { "Assets" });

            string projectNamespace = null;

            foreach (var guid in asmdefGuids)
            {
                var asmdefPath = AssetDatabase.GUIDToAssetPath(guid);

                // Пропускаем Editor сборки
                if (asmdefPath.Contains("Editor")) continue;

                // Извлекаем namespace из пути (например Assets/KM/Scripts/KM.asmdef -> KM)
                var parts = asmdefPath.Split('/');
                if (parts.Length >= 2 && parts[0] == "Assets")
                {
                    projectNamespace = parts[1];
                    break;
                }
            }

            // Fallback: ищем первую папку в Assets которая похожа на namespace проекта
            if (string.IsNullOrEmpty(projectNamespace))
            {
                var assetsPath = "Assets";
                var subfolders = AssetDatabase.GetSubFolders(assetsPath);

                foreach (var folder in subfolders)
                {
                    var folderName = Path.GetFileName(folder);
                    // Пропускаем стандартные папки Unity
                    if (folderName == "Plugins" || folderName == "Editor" || 
                        folderName == "Resources" || folderName == "StreamingAssets" ||
                        folderName == "Gizmos" || folderName == "Editor Default Resources" ||
                        folderName.StartsWith("."))
                        continue;

                    projectNamespace = folderName;
                    break;
                }
            }

            // Если всё ещё не нашли - используем дефолт
            if (string.IsNullOrEmpty(projectNamespace))
            {
                projectNamespace = "Game";
            }

            // Путь в Resources для загрузки через Resources.Load()
            return $"Assets/{projectNamespace}/Resources/Data/Credits/CreditsData.asset";
        }

        private void SetupSerializedObject()
        {
            if (creditsData == null) return;

            serializedObject = new SerializedObject(creditsData);
            
            // Roles list
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

            // Authors list
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

            // Thanks list
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

            // ID
            var idRect = new Rect(rect.x, rect.y, rect.width * 0.3f - 5, EditorGUIUtility.singleLineHeight);
            var idLabelRect = new Rect(rect.x, rect.y, 25, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(idLabelRect, "ID");
            idRect.x += 25;
            idRect.width -= 25;
            EditorGUI.PropertyField(idRect, element.FindPropertyRelative("id"), GUIContent.none);

            // Display Name
            var nameRect = new Rect(rect.x + rect.width * 0.3f, rect.y, rect.width * 0.7f, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(nameRect, element.FindPropertyRelative("displayName"), new GUIContent("Название"));

            // Order
            rect.y += lineHeight;
            var orderRect = new Rect(rect.x, rect.y, rect.width * 0.3f, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(orderRect, element.FindPropertyRelative("order"), new GUIContent("Порядок"));
        }

        private void DrawAuthorElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = authorsList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;
            float lineHeight = EditorGUIUtility.singleLineHeight + 2;

            // Name
            var nameRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(nameRect, element.FindPropertyRelative("name"), new GUIContent("Имя"));

            // Roles (checkboxes)
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

            // URL (optional)
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

            // Category
            var catRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(catRect, element.FindPropertyRelative("category"), new GUIContent("Категория"));

            // Text
            rect.y += lineHeight;
            var textRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(textRect, element.FindPropertyRelative("text"), new GUIContent("Текст"));
        }

        private List<string> GetStringList(SerializedProperty arrayProp)
        {
            var list = new List<string>();
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                list.Add(arrayProp.GetArrayElementAtIndex(i).stringValue);
            }
            return list;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Credits Data", EditorStyles.boldLabel);
            
            // Asset selector
            var newData = (CreditsData)EditorGUILayout.ObjectField(creditsData, typeof(CreditsData), false);
            if (newData != creditsData)
            {
                creditsData = newData;
                if (creditsData != null)
                {
                    SetupSerializedObject();
                }
                else
                {
                    serializedObject = null;
                }
            }
            EditorGUILayout.EndHorizontal();

            // Create button if no data
            if (creditsData == null)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox("Выберите существующий CreditsData или создайте новый", MessageType.Info);
                
                // Показываем путь где будет создан файл
                var expectedPath = GetProjectCreditsPath();
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Путь создания:", expectedPath, EditorStyles.miniLabel);
                
                EditorGUILayout.Space(10);
                if (GUILayout.Button("Создать CreditsData", GUILayout.Height(30)))
                {
                    CreateNewCreditsData();
                }
                return;
            }

            // Null-check для serializedObject
            if (serializedObject == null)
            {
                SetupSerializedObject();
                if (serializedObject == null)
                {
                    EditorGUILayout.HelpBox("Ошибка инициализации SerializedObject", MessageType.Error);
                    return;
                }
            }

            // Tabs
            EditorGUILayout.Space(10);
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
            
            EditorGUILayout.Space(10);
            
            serializedObject.Update();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (selectedTab)
            {
                case 0: // Roles
                    DrawRolesTab();
                    break;
                case 1: // Authors
                    DrawAuthorsTab();
                    break;
                case 2: // Thanks
                    DrawThanksTab();
                    break;
                case 3: // Preview
                    DrawPreviewTab();
                    break;
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

        private void DrawRolesTab()
        {
            EditorGUILayout.LabelField("Управление ролями", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Роли отображаются в порядке их расположения в списке. Перетаскивайте для изменения порядка.", MessageType.Info);
            EditorGUILayout.Space(5);
            
            rolesList?.DoLayoutList();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Добавить стандартные роли"))
            {
                AddDefaultRoles();
            }
        }

        private void DrawAuthorsTab()
        {
            EditorGUILayout.LabelField("Управление авторами", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Укажите роли для каждого автора. Один автор может иметь несколько ролей.", MessageType.Info);
            EditorGUILayout.Space(5);

            if (creditsData.roles.Count == 0)
            {
                EditorGUILayout.HelpBox("Сначала добавьте роли на вкладке 'Роли'", MessageType.Warning);
                return;
            }

            authorsList?.DoLayoutList();
        }

        private void DrawThanksTab()
        {
            EditorGUILayout.LabelField("Специальные благодарности", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Категория опциональна. Благодарности отображаются в конце Credits.", MessageType.Info);
            EditorGUILayout.Space(5);
            
            thanksList?.DoLayoutList();
        }

        private void DrawPreviewTab()
        {
            EditorGUILayout.LabelField("Предпросмотр текста Credits", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            var previewText = creditsData.GenerateCreditsText();
            
            var style = new GUIStyle(EditorStyles.textArea)
            {
                richText = true,
                wordWrap = true
            };
            
            EditorGUILayout.TextArea(previewText, style, GUILayout.ExpandHeight(true));

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Скопировать в буфер обмена"))
            {
                GUIUtility.systemCopyBuffer = previewText;
                Debug.Log("[CreditsEditor] Текст скопирован в буфер обмена");
            }
        }

        private void CreateNewCreditsData()
        {
            var assetPath = GetProjectCreditsPath();
            var directory = Path.GetDirectoryName(assetPath);
            
            // Создаём папки рекурсивно
            if (!AssetDatabase.IsValidFolder(directory))
            {
                CreateFolderRecursive(directory);
            }

            // Создаём asset
            creditsData = CreateInstance<CreditsData>();
            
            // Добавляем стандартные роли
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

        /// <summary>
        /// Создаёт папки рекурсивно, корректно работая с AssetDatabase
        /// </summary>
        private void CreateFolderRecursive(string path)
        {
            // Нормализуем путь
            path = path.Replace("\\", "/");
            
            var parts = path.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                Debug.LogError($"[CreditsEditor] Invalid path: {path}. Must start with 'Assets'");
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
                        Debug.LogError($"[CreditsEditor] Failed to create folder: {next}");
                        return;
                    }
                    Debug.Log($"[CreditsEditor] Created folder: {next}");
                }
                
                current = next;
            }
            
            // Обновляем AssetDatabase после создания папок
            AssetDatabase.Refresh();
        }
    }
}
