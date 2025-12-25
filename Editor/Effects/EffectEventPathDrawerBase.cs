using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.Effects.Editor
{
    /// <summary>
    /// Базовый класс для drawer'а событий.
    /// Содержит логику отрисовки UI для выбора событий.
    /// 
    /// Сгенерированный EffectEventPathDrawer.Generated.cs предоставляет данные,
    /// а этот класс предоставляет методы отрисовки.
    /// 
    /// Паттерн: partial class
    /// - EffectEventPathDrawerBase.cs (этот файл) — методы отрисовки
    /// - EffectEventPathDrawer.Generated.cs — данные событий
    /// </summary>
    public static partial class EffectEventPathDrawer
    {
        // Категории событий, которые подходят для Spatial эффектов (требуют позицию)
        private static readonly HashSet<string> SpatialEventCategories = new()
        {
            "Стадо", "Flocking",
            "Поведение", "Behavior",
            "Отдельные", "Individual"
        };

        // Категории событий, которые подходят для Screen эффектов (не требуют позицию)
        private static readonly HashSet<string> GlobalEventCategories = new()
        {
            "Интерфейс", "UI",
            "Игра", "Game",
            "Сеть", "Network",
            "Эффекты", "Effects"
        };

        /// <summary>
        /// Рисует поле выбора события с многоуровневым dropdown меню
        /// </summary>
        public static string DrawEventPathField(Rect position, GUIContent label, string currentPath)
        {
            var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            var fieldRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth - 25, position.height);
            var buttonRect = new Rect(position.x + position.width - 22, position.y, 22, position.height);

            EditorGUI.LabelField(labelRect, label);

            // Отображаем текущее значение или placeholder
            var displayText = string.IsNullOrEmpty(currentPath) ? "(Не выбрано)" : $"Evt.{currentPath}";
            
            // Цвет в зависимости от валидности
            var oldColor = GUI.color;
            if (!string.IsNullOrEmpty(currentPath) && !EventPathDrawer.EventPathExists(currentPath))
            {
                GUI.color = new Color(1f, 0.7f, 0.7f); // Красноватый для невалидного
            }
            else if (string.IsNullOrEmpty(currentPath))
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f); // Серый для пустого
            }

            EditorGUI.TextField(fieldRect, displayText);
            GUI.color = oldColor;

            // Кнопка выбора события
            if (GUI.Button(buttonRect, "▼"))
            {
                ShowEventSelectionMenu(fieldRect, currentPath, (selectedPath) =>
                {
                    GUI.changed = true;
                    return selectedPath;
                });
            }

            return currentPath;
        }

        /// <summary>
        /// Рисует поле выбора события в EditorGUILayout стиле
        /// </summary>
        public static string DrawEventPathFieldLayout(GUIContent label, string currentPath, Action<string> onChanged = null)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PrefixLabel(label);

            // Отображаем текущее значение
            var displayText = string.IsNullOrEmpty(currentPath) ? "(Не выбрано)" : $"Evt.{currentPath}";
            
            var oldColor = GUI.color;
            if (!string.IsNullOrEmpty(currentPath) && !EventPathDrawer.EventPathExists(currentPath))
            {
                GUI.color = new Color(1f, 0.7f, 0.7f);
            }
            else if (string.IsNullOrEmpty(currentPath))
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
            }

            var fieldRect = EditorGUILayout.GetControlRect();
            EditorGUI.TextField(fieldRect, displayText);
            GUI.color = oldColor;

            // Кнопка выбора
            if (GUILayout.Button("▼", GUILayout.Width(22)))
            {
                ShowEventSelectionMenu(fieldRect, currentPath, (selectedPath) =>
                {
                    onChanged?.Invoke(selectedPath);
                    return selectedPath;
                });
            }

            EditorGUILayout.EndHorizontal();

            return currentPath;
        }

        /// <summary>
        /// Рисует поле выбора события с фильтрацией по категории эффекта
        /// </summary>
        public static string DrawEventPathFieldLayout(GUIContent label, string currentPath, EffectCategory effectCategory, Action<string> onChanged = null)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PrefixLabel(label);

            var displayText = string.IsNullOrEmpty(currentPath) ? "(Не выбрано)" : $"Evt.{currentPath}";
            
            var oldColor = GUI.color;
            if (!string.IsNullOrEmpty(currentPath) && !EventPathDrawer.EventPathExists(currentPath))
            {
                GUI.color = new Color(1f, 0.7f, 0.7f);
            }
            else if (string.IsNullOrEmpty(currentPath))
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
            }

            var fieldRect = EditorGUILayout.GetControlRect();
            EditorGUI.TextField(fieldRect, displayText);
            GUI.color = oldColor;

            if (GUILayout.Button("▼", GUILayout.Width(22)))
            {
                ShowEventSelectionMenuFiltered(fieldRect, currentPath, effectCategory, (selectedPath) =>
                {
                    onChanged?.Invoke(selectedPath);
                    return selectedPath;
                });
            }

            EditorGUILayout.EndHorizontal();

            return currentPath;
        }

        /// <summary>
        /// Показывает dropdown меню для выбора события
        /// </summary>
        public static void ShowEventSelectionMenu(Rect buttonRect, string currentPath, Func<string, string> onSelected)
        {
            var menu = new GenericMenu();

            // Пустое значение
            menu.AddItem(new GUIContent("(Очистить)"), string.IsNullOrEmpty(currentPath), () =>
            {
                onSelected?.Invoke("");
            });

            menu.AddSeparator("");

            // Категории и события
            var categories = EventPathDrawer.GetCategories();
            foreach (var category in categories)
            {
                var events = EventPathDrawer.GetEventsInCategory(category);
                foreach (var evt in events)
                {
                    var isSelected = evt.Path == currentPath;
                    var menuPath = $"{category}/{evt.Name}";

                    menu.AddItem(new GUIContent(menuPath), isSelected, () =>
                    {
                        onSelected?.Invoke(evt.Path);
                    });
                }
            }

            menu.DropDown(buttonRect);
        }

        /// <summary>
        /// Показывает dropdown меню с фильтрацией по категории эффекта
        /// </summary>
        public static void ShowEventSelectionMenuFiltered(Rect buttonRect, string currentPath, EffectCategory effectCategory, Func<string, string> onSelected)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("(Очистить)"), string.IsNullOrEmpty(currentPath), () =>
            {
                onSelected?.Invoke("");
            });

            menu.AddSeparator("");

            // Определяем какие категории показывать
            var relevantCategories = effectCategory switch
            {
                EffectCategory.Spatial => SpatialEventCategories,
                EffectCategory.Screen => GlobalEventCategories,
                _ => null // Audio - показываем все
            };

            // Сначала релевантные категории
            if (relevantCategories != null)
            {
                menu.AddDisabledItem(new GUIContent("— Рекомендуемые —"));

                var categories = EventPathDrawer.GetCategories();
                foreach (var category in categories)
                {
                    // Проверяем первую часть пути (категорию верхнего уровня)
                    var topCategory = category.Split('.')[0];
                    if (!relevantCategories.Contains(topCategory)) continue;

                    var events = EventPathDrawer.GetEventsInCategory(category);
                    foreach (var evt in events)
                    {
                        var isSelected = evt.Path == currentPath;
                        var menuPath = $"{category}/{evt.Name}";

                        menu.AddItem(new GUIContent(menuPath), isSelected, () =>
                        {
                            onSelected?.Invoke(evt.Path);
                        });
                    }
                }

                menu.AddSeparator("");
                menu.AddDisabledItem(new GUIContent("— Все события —"));
            }

            // Все события
            var allCategories = EventPathDrawer.GetCategories();
            foreach (var category in allCategories)
            {
                var events = EventPathDrawer.GetEventsInCategory(category);
                foreach (var evt in events)
                {
                    var isSelected = evt.Path == currentPath;
                    var menuPath = $"Все/{category}/{evt.Name}";

                    menu.AddItem(new GUIContent(menuPath), isSelected, () =>
                    {
                        onSelected?.Invoke(evt.Path);
                    });
                }
            }

            menu.DropDown(buttonRect);
        }

        /// <summary>
        /// Возвращает emoji для категории события
        /// </summary>
        public static string GetCategoryEmoji(string category)
        {
            var topCategory = category.Split('.')[0];
            return topCategory switch
            {
                "Стадо" or "Flocking" => "🐑",
                "Поведение" or "Behavior" => "🧠",
                "Отдельные" or "Individual" => "🎯",
                "Интерфейс" or "UI" => "🖥️",
                "Игра" or "Game" => "🎮",
                "Сеть" or "Network" => "🌐",
                "Эффекты" or "Effects" => "✨",
                _ => "📋"
            };
        }

        /// <summary>
        /// Проверяет, подходит ли событие для указанной категории эффекта
        /// </summary>
        public static bool IsEventSuitableForCategory(string eventPath, EffectCategory category)
        {
            if (string.IsNullOrEmpty(eventPath)) return true;

            var parts = eventPath.Split('.');
            if (parts.Length < 1) return true;

            var eventCategory = parts[0];

            switch (category)
            {
                case EffectCategory.Spatial:
                    // Spatial эффекты — лучше использовать события с позицией
                    return SpatialEventCategories.Contains(eventCategory);

                case EffectCategory.Audio:
                    // Audio — может быть и пространственным и глобальным
                    return true;

                case EffectCategory.Screen:
                    // Screen — подходят все события
                    return true;

                default:
                    return true;
            }
        }

        /// <summary>
        /// Создает GenericMenu с подсветкой событий по категории эффекта
        /// </summary>
        public static GenericMenu CreateEventSelectionMenuWithHighlight(
            string currentPath, 
            EffectCategory effectCategory, 
            Action<string> onChanged)
        {
            var menu = new GenericMenu();

            // Опция "Нет события"
            menu.AddItem(new GUIContent("(Нет события)"), string.IsNullOrEmpty(currentPath), () =>
            {
                onChanged?.Invoke("");
            });

            menu.AddSeparator("");

            var categories = EventPathDrawer.GetCategories();

            // Сначала добавляем рекомендуемые события
            if (effectCategory == EffectCategory.Spatial)
            {
                menu.AddDisabledItem(new GUIContent("━━━ ✓ Рекомендуемые (с позицией) ━━━"));

                foreach (var category in categories.Where(c => SpatialEventCategories.Contains(c.Split('.')[0])))
                {
                    AddCategoryEventsToMenu(menu, category, currentPath, onChanged, "✓ ");
                }

                menu.AddSeparator("");
                menu.AddDisabledItem(new GUIContent("━━━ Другие события ━━━"));

                foreach (var category in categories.Where(c => !SpatialEventCategories.Contains(c.Split('.')[0])))
                {
                    AddCategoryEventsToMenu(menu, category, currentPath, onChanged, "");
                }
            }
            else if (effectCategory == EffectCategory.Audio)
            {
                // Для Audio показываем все, но выделяем пространственные
                menu.AddDisabledItem(new GUIContent("━━━ 🔊 Пространственные ━━━"));

                foreach (var category in categories.Where(c => SpatialEventCategories.Contains(c.Split('.')[0])))
                {
                    AddCategoryEventsToMenu(menu, category, currentPath, onChanged, "🎯 ");
                }

                menu.AddSeparator("");
                menu.AddDisabledItem(new GUIContent("━━━ 🌐 Глобальные ━━━"));

                foreach (var category in categories.Where(c => !SpatialEventCategories.Contains(c.Split('.')[0])))
                {
                    AddCategoryEventsToMenu(menu, category, currentPath, onChanged, "");
                }
            }
            else
            {
                // Screen — все события равнозначны
                foreach (var category in categories)
                {
                    AddCategoryEventsToMenu(menu, category, currentPath, onChanged, "");
                }
            }

            return menu;
        }

        private static void AddCategoryEventsToMenu(
            GenericMenu menu, 
            string category, 
            string currentPath, 
            Action<string> onChanged,
            string prefix)
        {
            var events = EventPathDrawer.GetEventsInCategory(category);
            foreach (var evt in events)
            {
                var isSelected = evt.Path == currentPath;
                var menuPath = $"{prefix}{category}/{evt.Name}";

                menu.AddItem(new GUIContent(menuPath), isSelected, () =>
                {
                    onChanged?.Invoke(evt.Path);
                });
            }
        }
    }
}
