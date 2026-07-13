using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProtoSystem.Effects;
using ProtoSystem.Compat;

namespace ProtoSystem.Effects.Editor
{
    /// <summary>
    /// Утилитный класс для работы с событиями в редакторе.
    /// Автоматически находит классы событий: Evt, _Events, Events, EventIds, GameEvents
    /// Предоставляет кеширование структуры событий и построение многоуровневого меню.
    /// </summary>
    public static class EventPathDrawer
    {
        // Кеш структуры событий: Category -> List<(EventName, EventPath, EventId)>
        private static Dictionary<string, List<(string Name, string Path, int Id)>> _eventCache;
        private static string[] _categoryNames;
        private static bool _cacheInitialized = false;
        private static string _foundEventClassName = null;

        /// <summary>
        /// Возвращает имя найденного класса событий (для отладки)
        /// </summary>
        public static string FoundEventClassName => _foundEventClassName;

        /// <summary>
        /// Сбрасывает кеш событий для повторной инициализации
        /// </summary>
        public static void ResetCache()
        {
            _cacheInitialized = false;
            _eventCache = null;
            _categoryNames = null;
            _foundEventClassName = null;
        }

        /// <summary>
        /// Инициализирует кеш событий из класса событий через рефлексию
        /// </summary>
        public static void InitializeCache()
        {
            if (_cacheInitialized) return;

            _eventCache = new Dictionary<string, List<(string, string, int)>>();

            // Ищем класс событий в пользовательских сборках
            var evtType = FindEventIdsType();
            if (evtType == null)
            {
                Debug.LogWarning("[EventPathDrawer] Класс событий не найден. Поддерживаемые имена: Evt, _Events, Events, EventIds, GameEvents. " +
                    "Создайте класс событий или используйте EventPathResolver.SetEventIdsType<T>()");
                _categoryNames = new string[0];
                _cacheInitialized = true;
                return;
            }

            _foundEventClassName = evtType.FullName ?? evtType.Name;
            Debug.Log($"[EventPathDrawer] Используется класс событий: {_foundEventClassName}");

            var nestedTypes = evtType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);

            foreach (var nestedType in nestedTypes)
            {
                // Пропускаем enum EventType
                if (nestedType.IsEnum) continue;

                ProcessNestedType(nestedType, nestedType.Name);
            }

            _categoryNames = _eventCache.Keys.OrderBy(k => k).ToArray();
            _cacheInitialized = true;
        }

        // Стандартные имена классов событий для автопоиска (fallback)
        private static readonly string[] StandardEventClassNames = new[]
        {
            "_Events",       // KM проект (приоритет)
            "Evt",           // ProtoSystem стандарт
            "Events",        // Общее
            "EventIds",      // Альтернатива
            "GameEvents"     // Ещё вариант
        };

        /// <summary>
        /// Ищет класс событий в пользовательских сборках.
        /// Сначала пытается использовать namespace из настроек ProtoSystem (EventBusEditorUtils),
        /// затем fallback на стандартные имена.
        /// </summary>
        private static Type FindEventIdsType()
        {
            // 1. Пробуем получить namespace из настроек проекта ProtoSystem
            var projectInfo = EventBusEditorUtils.GetProjectEventBusInfo();
            if (projectInfo.Exists && !string.IsNullOrEmpty(projectInfo.Namespace))
            {
                var projectNamespace = projectInfo.Namespace;
                
                foreach (var assembly in UnityVersionCompat.GetLoadedAssemblies())
                {
                    var name = assembly.GetName().Name;
                    if (name.StartsWith("Unity.") || name.StartsWith("UnityEngine") || 
                        name.StartsWith("UnityEditor") || name.StartsWith("System") ||
                        name.StartsWith("mscorlib") || name.StartsWith("ProtoSystem"))
                        continue;

                    // Ищем с namespace проекта
                    foreach (var className in StandardEventClassNames)
                    {
                        var fullName = $"{projectNamespace}.{className}";
                        var evtType = assembly.GetType(fullName);
                        if (evtType != null)
                        {
                            return evtType;
                        }
                    }
                }
            }

            // 2. Fallback: ищем в глобальном namespace и стандартных местах
            foreach (var assembly in UnityVersionCompat.GetLoadedAssemblies())
            {
                var name = assembly.GetName().Name;
                
                // Пропускаем системные и Unity сборки
                if (name.StartsWith("Unity.") || 
                    name.StartsWith("UnityEngine") ||
                    name.StartsWith("UnityEditor") ||
                    name.StartsWith("System") ||
                    name.StartsWith("mscorlib") ||
                    name.StartsWith("netstandard") ||
                    name.StartsWith("Mono.") ||
                    name.StartsWith("ProtoSystem"))
                    continue;

                // Ищем в глобальном namespace
                foreach (var className in StandardEventClassNames)
                {
                    var evtType = assembly.GetType(className);
                    if (evtType != null)
                    {
                        return evtType;
                    }
                }
            }

            // 3. Fallback: ищем класс с именем Evt/_Events среди ВСЕХ типов (для любого namespace, например Sheeps.Evt)
            foreach (var assembly in UnityVersionCompat.GetLoadedAssemblies())
            {
                var name = assembly.GetName().Name;

                if (name.StartsWith("Unity.") ||
                    name.StartsWith("UnityEngine") ||
                    name.StartsWith("UnityEditor") ||
                    name.StartsWith("System") ||
                    name.StartsWith("mscorlib") ||
                    name.StartsWith("netstandard") ||
                    name.StartsWith("Mono.") ||
                    name.StartsWith("ProtoSystem"))
                    continue;

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        // Ищем static class с нужным именем
                        if (type.IsClass && type.IsAbstract && type.IsSealed) // static class
                        {
                            foreach (var className in StandardEventClassNames)
                            {
                                if (type.Name == className)
                                {
                                    return type;
                                }
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Игнорируем ошибки загрузки типов
                }
            }

            return null;
        }

        /// <summary>
        /// Рекурсивно обрабатывает вложенные типы для поддержки многоуровневой вложенности
        /// </summary>
        private static void ProcessNestedType(Type type, string pathPrefix)
        {
            var events = new List<(string Name, string Path, int Id)>();

            // Получаем все public const int поля
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(int));

            foreach (var field in fields)
            {
                var eventName = field.Name;
                var eventPath = $"{pathPrefix}.{eventName}";
                var eventId = (int)field.GetRawConstantValue();
                events.Add((eventName, eventPath, eventId));
            }

            if (events.Count > 0)
            {
                _eventCache[pathPrefix] = events;
            }

            // Рекурсивно обрабатываем вложенные классы (например Интерфейс.HUD)
            var nestedTypes = type.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);
            foreach (var nestedType in nestedTypes)
            {
                if (nestedType.IsEnum) continue;
                ProcessNestedType(nestedType, $"{pathPrefix}.{nestedType.Name}");
            }
        }

        /// <summary>
        /// Получает ID события по его текстовому пути (например, "Боевка.Урон_нанесен")
        /// </summary>
        public static int GetEventIdByPath(string eventPath)
        {
            InitializeCache();

            if (string.IsNullOrEmpty(eventPath)) return 0;

            // Ищем по полному пути в кеше
            foreach (var kvp in _eventCache)
            {
                var evt = kvp.Value.FirstOrDefault(e => e.Path == eventPath);
                if (evt.Id != 0) return evt.Id;
            }

            return 0;
        }

        /// <summary>
        /// Получает текстовый путь события по его ID
        /// </summary>
        public static string GetEventPathById(int eventId)
        {
            InitializeCache();

            if (eventId <= 0) return "";

            foreach (var kvp in _eventCache)
            {
                var evt = kvp.Value.FirstOrDefault(e => e.Id == eventId);
                if (evt.Id == eventId && !string.IsNullOrEmpty(evt.Path))
                {
                    return evt.Path;
                }
            }

            return "";
        }

        /// <summary>
        /// Проверяет существование события по пути
        /// </summary>
        public static bool EventPathExists(string eventPath)
        {
            return GetEventIdByPath(eventPath) > 0;
        }

        /// <summary>
        /// Получает все категории событий
        /// </summary>
        public static string[] GetCategories()
        {
            InitializeCache();
            return _categoryNames ?? new string[0];
        }

        /// <summary>
        /// Получает все события в категории
        /// </summary>
        public static List<(string Name, string Path, int Id)> GetEventsInCategory(string category)
        {
            InitializeCache();
            return _eventCache != null && _eventCache.TryGetValue(category, out var events) 
                ? events 
                : new List<(string, string, int)>();
        }

        /// <summary>
        /// Рисует поле выбора события с многоуровневым dropdown меню
        /// </summary>
        public static string DrawEventPathField(Rect position, GUIContent label, string currentPath)
        {
            InitializeCache();

            var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            var fieldRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth - 25, position.height);
            var buttonRect = new Rect(position.x + position.width - 22, position.y, 22, position.height);

            EditorGUI.LabelField(labelRect, label);

            // Отображаем текущее значение или placeholder
            var displayText = string.IsNullOrEmpty(currentPath) ? "(Не выбрано)" : $"Evt.{currentPath}";
            
            // Цвет в зависимости от валидности
            var oldColor = GUI.color;
            if (!string.IsNullOrEmpty(currentPath) && !EventPathExists(currentPath))
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
                    // Callback вызывается при выборе
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
            InitializeCache();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PrefixLabel(label);

            // Отображаем текущее значение
            var displayText = string.IsNullOrEmpty(currentPath) ? "(Не выбрано)" : $"Evt.{currentPath}";

            // Цвет в зависимости от валидности
            var oldColor = GUI.backgroundColor;
            if (!string.IsNullOrEmpty(currentPath) && !EventPathExists(currentPath))
            {
                GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            }

            EditorGUILayout.TextField(displayText);
            GUI.backgroundColor = oldColor;

            // Кнопка выбора
            if (GUILayout.Button("▼", GUILayout.Width(22)))
            {
                var menu = CreateEventSelectionMenu(currentPath, onChanged);
                menu.ShowAsContext();
            }

            // Кнопка очистки
            if (!string.IsNullOrEmpty(currentPath))
            {
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    currentPath = "";
                    onChanged?.Invoke(currentPath);
                    GUI.changed = true;
                }
            }

            EditorGUILayout.EndHorizontal();

            // Показываем ID события для справки
            if (!string.IsNullOrEmpty(currentPath))
            {
                var eventId = GetEventIdByPath(currentPath);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Event ID: {eventId}", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            return currentPath;
        }

        /// <summary>
        /// Показывает многоуровневое меню выбора события
        /// </summary>
        private static void ShowEventSelectionMenu(Rect position, string currentPath, Func<string, string> onSelected)
        {
            var menu = new GenericMenu();

            // Опция "Нет события"
            menu.AddItem(new GUIContent("(Нет события)"), string.IsNullOrEmpty(currentPath), () =>
            {
                onSelected?.Invoke("");
            });

            menu.AddSeparator("");

            // Добавляем категории и события
            if (_categoryNames != null)
            {
                foreach (var category in _categoryNames)
                {
                    var events = _eventCache[category];
                    foreach (var evt in events)
                    {
                        var isSelected = evt.Path == currentPath;
                        // Заменяем точки на / для создания подменю в GenericMenu
                        var menuCategory = category.Replace('.', '/');
                        var menuPath = $"{menuCategory}/{evt.Name}  (ID: {evt.Id})";

                        menu.AddItem(new GUIContent(menuPath), isSelected, () =>
                        {
                            onSelected?.Invoke(evt.Path);
                        });
                    }
                }
            }

            menu.DropDown(position);
        }

        /// <summary>
        /// Создает GenericMenu для выбора события
        /// </summary>
        private static GenericMenu CreateEventSelectionMenu(string currentPath, Action<string> onChanged)
        {
            var menu = new GenericMenu();

            // Опция "Нет события"
            menu.AddItem(new GUIContent("(Нет события)"), string.IsNullOrEmpty(currentPath), () =>
            {
                onChanged?.Invoke("");
            });

            menu.AddSeparator("");

            // Добавляем категории и события
            if (_categoryNames != null)
            {
                foreach (var category in _categoryNames)
                {
                    var events = _eventCache[category];
                    foreach (var evt in events)
                    {
                        var isSelected = evt.Path == currentPath;
                        // Заменяем точки на / для создания подменю в GenericMenu
                        var menuCategory = category.Replace('.', '/');
                        var menuPath = $"{menuCategory}/{evt.Name}";

                        menu.AddItem(new GUIContent(menuPath), isSelected, () =>
                        {
                            onChanged?.Invoke(evt.Path);
                        });
                    }
                }
            }

            return menu;
        }

        /// <summary>
        /// Очищает кеш (вызывать при изменении EventIds)
        /// </summary>
        public static void ClearCache()
        {
            _eventCache = null;
            _categoryNames = null;
            _cacheInitialized = false;
        }

        // === Подсветка событий по категории эффекта ===

        // Категории событий, которые обычно публикуются с IEffectTarget (Spatial эффекты)
        private static readonly HashSet<string> SpatialEventCategories = new HashSet<string>
        {
            "Поведение",     // Овца_разбегается, Овца_следует и т.д.
            "Отдельные",     // Овца_схвачена, Овца_брошена, Овца_упала
            "Стадо",         // Стадо_сформировано, Стадо_распалось
            "Команды"        // Следуй, Разбегайся, Собирайся, Стой, Толкай
        };

        // Категории событий, подходящие для всех (Screen эффекты)
        private static readonly HashSet<string> GlobalEventCategories = new HashSet<string>
        {
            "Игра",          // Раунд_начался, Победа, Поражение
            "Сеть",          // Игрок_подключился, Игрок_отключился
            "Интерфейс",     // Экран_изменился, Кнопка_нажата
            "Голос"          // Включён, Выключён
        };

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
            InitializeCache();
            var menu = new GenericMenu();

            // Опция "Нет события"
            menu.AddItem(new GUIContent("(Нет события)"), string.IsNullOrEmpty(currentPath), () =>
            {
                onChanged?.Invoke("");
            });

            menu.AddSeparator("");

            if (_categoryNames == null || _categoryNames.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("Нет доступных событий"));
                return menu;
            }

            // Сначала добавляем рекомендуемые события
            if (effectCategory == EffectCategory.Spatial)
            {
                menu.AddDisabledItem(new GUIContent("━━━ ✓ Рекомендуемые (с позицией) ━━━"));

                foreach (var category in _categoryNames.Where(c => SpatialEventCategories.Contains(c)))
                {
                    AddCategoryEventsToMenu(menu, category, currentPath, onChanged, "✓ ");
                }

                menu.AddSeparator("");
                menu.AddDisabledItem(new GUIContent("━━━ Другие события ━━━"));

                foreach (var category in _categoryNames.Where(c => !SpatialEventCategories.Contains(c)))
                {
                    AddCategoryEventsToMenu(menu, category, currentPath, onChanged, "");
                }
            }
            else if (effectCategory == EffectCategory.Audio)
            {
                // Для Audio показываем все, но выделяем пространственные
                menu.AddDisabledItem(new GUIContent("━━━ 🔊 Пространственные ━━━"));

                foreach (var category in _categoryNames.Where(c => SpatialEventCategories.Contains(c)))
                {
                    AddCategoryEventsToMenu(menu, category, currentPath, onChanged, "🎯 ");
                }

                menu.AddSeparator("");
                menu.AddDisabledItem(new GUIContent("━━━ 🌐 Глобальные ━━━"));

                foreach (var category in _categoryNames.Where(c => !SpatialEventCategories.Contains(c)))
                {
                    AddCategoryEventsToMenu(menu, category, currentPath, onChanged, "");
                }
            }
            else
            {
                // Screen — все события равнозначны
                foreach (var category in _categoryNames)
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
            if (_eventCache == null || !_eventCache.TryGetValue(category, out var events))
                return;

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

        /// <summary>
        /// Рисует поле выбора события с подсветкой по категории эффекта
        /// </summary>
        public static string DrawEventPathFieldWithCategoryHighlight(
            GUIContent label, 
            string currentPath, 
            EffectCategory effectCategory,
            Action<string> onChanged = null)
        {
            InitializeCache();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PrefixLabel(label);

            // Отображаем текущее значение
            var displayText = string.IsNullOrEmpty(currentPath) ? "(Не выбрано)" : $"Evt.{currentPath}";

            // Цвет в зависимости от валидности и соответствия категории
            var oldColor = GUI.backgroundColor;
            if (!string.IsNullOrEmpty(currentPath))
            {
                if (!EventPathExists(currentPath))
                {
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f); // Красный — не существует
                }
                else if (!IsEventSuitableForCategory(currentPath, effectCategory))
                {
                    GUI.backgroundColor = new Color(1f, 0.9f, 0.5f); // Жёлтый — не рекомендуется
                }
                else
                {
                    GUI.backgroundColor = new Color(0.6f, 1f, 0.6f); // Зелёный — отлично
                }
            }

            EditorGUILayout.TextField(displayText);
            GUI.backgroundColor = oldColor;

            // Кнопка выбора с подсветкой
            if (GUILayout.Button("▼", GUILayout.Width(22)))
            {
                var menu = CreateEventSelectionMenuWithHighlight(currentPath, effectCategory, onChanged);
                menu.ShowAsContext();
            }

            // Кнопка очистки
            if (!string.IsNullOrEmpty(currentPath))
            {
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    currentPath = "";
                    onChanged?.Invoke(currentPath);
                    GUI.changed = true;
                }
            }

            EditorGUILayout.EndHorizontal();

            // Показываем ID события и предупреждение
            if (!string.IsNullOrEmpty(currentPath))
            {
                var eventId = GetEventIdByPath(currentPath);
                EditorGUI.indentLevel++;
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Event ID: {eventId}", EditorStyles.miniLabel, GUILayout.Width(100));
                
                if (!IsEventSuitableForCategory(currentPath, effectCategory) && effectCategory == EffectCategory.Spatial)
                {
                    var oldLabelColor = GUI.contentColor;
                    GUI.contentColor = new Color(1f, 0.6f, 0f);
                    EditorGUILayout.LabelField("⚠ Событие может не содержать данных позиции", EditorStyles.miniLabel);
                    GUI.contentColor = oldLabelColor;
                }
                
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }

            return currentPath;
        }
    }
}
