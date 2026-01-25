using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Custom PropertyDrawer для LogSettings
    /// Упрощённый UI с двумя рядами toggle-кнопок
    /// </summary>
    [CustomPropertyDrawer(typeof(LogSettings))]
    public class LogSettingsDrawer : PropertyDrawer
    {
        private bool showSystemsList = false;
        private Vector2 systemsScrollPos;
        
        // Кэш систем из менеджера
        private static List<string> cachedSystemIds = new List<string>();
        private static double lastCacheTime = 0;
        private const double CACHE_LIFETIME = 2.0; // секунды

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight; // Header

            if (property.isExpanded)
            {
                height += EditorGUIUtility.singleLineHeight * 3; // Уровни, Категории, Colors
                height += EditorGUIUtility.standardVerticalSpacing * 3;
                
                // Системы
                height += EditorGUIUtility.singleLineHeight; // Foldout
                if (showSystemsList)
                {
                    var systems = GetAvailableSystems(property);
                    int visibleCount = Mathf.Min(systems.Count, 8); // Макс 8 видимых
                    height += visibleCount * (EditorGUIUtility.singleLineHeight + 2);
                    height += EditorGUIUtility.standardVerticalSpacing;
                    
                    if (systems.Count > 8)
                        height += EditorGUIUtility.singleLineHeight; // Scroll hint
                }
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // Header foldout
            property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);
            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float indentOffset = EditorGUI.indentLevel * 15f;

                // Ряд 1: Уровни логирования
                var globalLogLevel = property.FindPropertyRelative("globalLogLevel");
                DrawLogLevelButtons(rect, globalLogLevel, indentOffset);
                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // Ряд 2: Категории
                var categories = property.FindPropertyRelative("enabledCategories");
                DrawCategoryButtons(rect, categories, indentOffset);
                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // Colors toggle
                var useColors = property.FindPropertyRelative("useColors");
                EditorGUI.PropertyField(rect, useColors, new GUIContent("🎨 Цвета в консоли"));
                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // Список систем
                rect = DrawSystemsFilter(rect, property, indentOffset);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Рисует ряд кнопок уровней логирования (флаговый multi-select)
        /// </summary>
        private void DrawLogLevelButtons(Rect rect, SerializedProperty prop, float indentOffset)
        {
            var labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth - indentOffset, rect.height);
            var fieldRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y, rect.width - EditorGUIUtility.labelWidth, rect.height);

            EditorGUI.LabelField(labelRect, new GUIContent("Уровень", "Типы сообщений для вывода (флаги)"));

            var levels = new (LogLevel level, string label, Color color)[]
            {
                (LogLevel.Errors, "Errors", new Color(0.96f, 0.26f, 0.21f)),
                (LogLevel.Warnings, "Warn", new Color(1f, 0.76f, 0.03f)),
                (LogLevel.Info, "Info", new Color(0.6f, 0.8f, 1f)),
            };

            float buttonWidth = (fieldRect.width - (levels.Length - 1) * 2) / levels.Length;
            var currentValue = (LogLevel)prop.intValue;

            for (int i = 0; i < levels.Length; i++)
            {
                var buttonRect = new Rect(fieldRect.x + i * (buttonWidth + 2), fieldRect.y, buttonWidth, fieldRect.height);
                bool isEnabled = (currentValue & levels[i].level) != 0;

                // Стиль кнопки
                var style = new GUIStyle(EditorStyles.miniButton);
                
                if (isEnabled)
                {
                    // Активная кнопка — яркий фон
                    var bgTex = MakeColorTexture(levels[i].color * 0.7f);
                    style.normal.background = bgTex;
                    style.normal.textColor = Color.white;
                    style.fontStyle = FontStyle.Bold;
                }
                else
                {
                    style.normal.textColor = levels[i].color * 0.8f;
                }

                string buttonLabel = isEnabled ? $"✓ {levels[i].label}" : levels[i].label;
                
                if (GUI.Button(buttonRect, buttonLabel, style))
                {
                    // Переключаем флаг
                    if (isEnabled)
                        prop.intValue = (int)(currentValue & ~levels[i].level);
                    else
                        prop.intValue = (int)(currentValue | levels[i].level);
                }
            }
        }

        /// <summary>
        /// Рисует ряд кнопок категорий (multi-select)
        /// </summary>
        private void DrawCategoryButtons(Rect rect, SerializedProperty prop, float indentOffset)
        {
            var labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth - indentOffset, rect.height);
            var fieldRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y, rect.width - EditorGUIUtility.labelWidth, rect.height);

            EditorGUI.LabelField(labelRect, new GUIContent("Категории", "Типы сообщений для вывода"));

            var categories = new (LogCategory cat, string label, Color color)[]
            {
                (LogCategory.Initialization, "Init", new Color(0.30f, 0.69f, 0.31f)),  // Зелёный
                (LogCategory.Dependencies, "Dep", new Color(1f, 0.60f, 0f)),           // Оранжевый
                (LogCategory.Events, "Event", new Color(0.13f, 0.59f, 0.95f)),         // Синий
                (LogCategory.Runtime, "Run", new Color(0.61f, 0.15f, 0.69f))           // Фиолетовый
            };

            float buttonWidth = (fieldRect.width - (categories.Length - 1) * 2) / categories.Length;
            var currentValue = (LogCategory)prop.intValue;

            for (int i = 0; i < categories.Length; i++)
            {
                var buttonRect = new Rect(fieldRect.x + i * (buttonWidth + 2), fieldRect.y, buttonWidth, fieldRect.height);
                bool isEnabled = (currentValue & categories[i].cat) != 0;

                var style = new GUIStyle(EditorStyles.miniButton);
                
                if (isEnabled)
                {
                    var bgTex = MakeColorTexture(categories[i].color * 0.6f);
                    style.normal.background = bgTex;
                    style.normal.textColor = Color.white;
                    style.fontStyle = FontStyle.Bold;
                }
                else
                {
                    style.normal.textColor = Color.gray;
                }

                string buttonLabel = isEnabled ? $"✓ {categories[i].label}" : categories[i].label;

                if (GUI.Button(buttonRect, buttonLabel, style))
                {
                    if (isEnabled)
                        prop.intValue = (int)(currentValue & ~categories[i].cat);
                    else
                        prop.intValue = (int)(currentValue | categories[i].cat);
                }
            }
        }

        /// <summary>
        /// Рисует список систем с чекбоксами
        /// </summary>
        private Rect DrawSystemsFilter(Rect rect, SerializedProperty property, float indentOffset)
        {
            var filterMode = property.FindPropertyRelative("filterMode");
            var filteredSystems = property.FindPropertyRelative("filteredSystems");
            
            // Foldout с режимом фильтра
            var foldoutRect = new Rect(rect.x, rect.y, rect.width - 100, EditorGUIUtility.singleLineHeight);
            var modeRect = new Rect(rect.x + rect.width - 95, rect.y, 95, EditorGUIUtility.singleLineHeight);
            
            int enabledCount = filteredSystems.arraySize;
            string foldoutLabel = $"Системы ({enabledCount} выбрано)";
            
            showSystemsList = EditorGUI.Foldout(foldoutRect, showSystemsList, foldoutLabel, true);
            
            // Dropdown режима
            var modes = new string[] { "Все", "Только ✓", "Кроме ✓" };
            int currentMode = filterMode.enumValueIndex;
            int newMode = EditorGUI.Popup(modeRect, currentMode, modes);
            if (newMode != currentMode)
            {
                filterMode.enumValueIndex = newMode;
            }
            
            rect.y += EditorGUIUtility.singleLineHeight;

            if (showSystemsList)
            {
                var systems = GetAvailableSystems(property);
                var selectedSystems = GetSelectedSystemsList(filteredSystems);
                
                // Область списка
                int visibleCount = Mathf.Min(systems.Count, 8);
                float listHeight = visibleCount * (EditorGUIUtility.singleLineHeight + 2);
                
                var listRect = new Rect(rect.x + indentOffset, rect.y, rect.width - indentOffset, listHeight);
                
                // Рисуем системы
                float itemY = listRect.y;
                int drawn = 0;
                
                foreach (var systemId in systems)
                {
                    if (drawn >= 8) break;
                    
                    var itemRect = new Rect(listRect.x, itemY, listRect.width, EditorGUIUtility.singleLineHeight);
                    bool isSelected = selectedSystems.Contains(systemId);
                    
                    // Чекбокс + имя системы
                    bool newSelected = EditorGUI.ToggleLeft(itemRect, systemId, isSelected);
                    
                    if (newSelected != isSelected)
                    {
                        if (newSelected)
                        {
                            // Добавляем в список
                            filteredSystems.InsertArrayElementAtIndex(filteredSystems.arraySize);
                            filteredSystems.GetArrayElementAtIndex(filteredSystems.arraySize - 1).stringValue = systemId;
                        }
                        else
                        {
                            // Удаляем из списка
                            for (int i = 0; i < filteredSystems.arraySize; i++)
                            {
                                if (filteredSystems.GetArrayElementAtIndex(i).stringValue == systemId)
                                {
                                    filteredSystems.DeleteArrayElementAtIndex(i);
                                    break;
                                }
                            }
                        }
                    }
                    
                    itemY += EditorGUIUtility.singleLineHeight + 2;
                    drawn++;
                }
                
                rect.y += listHeight;
                
                // Подсказка если систем больше 8
                if (systems.Count > 8)
                {
                    var hintRect = new Rect(rect.x + indentOffset, rect.y, rect.width - indentOffset, EditorGUIUtility.singleLineHeight);
                    EditorGUI.LabelField(hintRect, $"... и ещё {systems.Count - 8} систем", EditorStyles.centeredGreyMiniLabel);
                    rect.y += EditorGUIUtility.singleLineHeight;
                }
                
                rect.y += EditorGUIUtility.standardVerticalSpacing;
            }

            return rect;
        }

        /// <summary>
        /// Получает список доступных систем из менеджера
        /// </summary>
        private List<string> GetAvailableSystems(SerializedProperty property)
        {
            // Проверяем кэш
            if (EditorApplication.timeSinceStartup - lastCacheTime < CACHE_LIFETIME && cachedSystemIds.Count > 0)
            {
                return cachedSystemIds;
            }
            
            cachedSystemIds.Clear();
            
            // Ищем SystemInitializationManager
            var manager = Object.FindFirstObjectByType<SystemInitializationManager>();
            if (manager != null)
            {
                foreach (var system in manager.Systems)
                {
                    if (!string.IsNullOrEmpty(system.systemName))
                    {
                        // Пытаемся получить SystemId из объекта
                        string systemId = system.systemName;
                        
                        if (system.ExistingSystemObject is IInitializableSystem initSystem)
                        {
                            systemId = initSystem.SystemId;
                        }
                        
                        if (!cachedSystemIds.Contains(systemId))
                        {
                            cachedSystemIds.Add(systemId);
                        }
                    }
                }
            }
            
            // Добавляем стандартные системы ProtoSystem если не найдены
            if (cachedSystemIds.Count == 0)
            {
                cachedSystemIds.AddRange(new[]
                {
                    "ui_system", "settings_system", "game_session", 
                    "cursor_manager", "sound_manager", "scene_flow",
                    "effects_manager", "network_lobby"
                });
            }
            
            lastCacheTime = EditorApplication.timeSinceStartup;
            return cachedSystemIds;
        }

        /// <summary>
        /// Получает HashSet выбранных систем
        /// </summary>
        private HashSet<string> GetSelectedSystemsList(SerializedProperty filteredSystems)
        {
            var result = new HashSet<string>();
            for (int i = 0; i < filteredSystems.arraySize; i++)
            {
                result.Add(filteredSystems.GetArrayElementAtIndex(i).stringValue);
            }
            return result;
        }

        /// <summary>
        /// Создаёт текстуру заданного цвета для фона кнопки
        /// </summary>
        private Texture2D MakeColorTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
        
        /// <summary>
        /// Инвалидирует кэш систем
        /// </summary>
        public static void InvalidateCache()
        {
            lastCacheTime = 0;
            cachedSystemIds.Clear();
        }
    }
}
