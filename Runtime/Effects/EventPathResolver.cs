using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ProtoSystem.Effects
{
    /// <summary>
    /// Runtime утилита для конвертации текстовых путей событий в числовые ID.
    /// Используется при инициализации эффектов для преобразования строк вида "Боевка.Урон_нанесен" в ID.
    /// 
    /// Поддерживает конфигурируемый тип событий:
    /// - По умолчанию ищет класс Evt в пользовательских сборках
    /// - Можно явно указать тип через SetEventIdsType<T>()
    /// </summary>
    public static class EventPathResolver
    {
        // Кеш для быстрого доступа: eventPath -> eventId
        private static Dictionary<string, int> _pathToIdCache;
        private static Dictionary<int, string> _idToPathCache;
        private static bool _initialized = false;

        // Конфигурируемый тип событий (по умолчанию null = автопоиск)
        private static Type _eventIdsType;

        /// <summary>
        /// Устанавливает тип класса событий явно.
        /// Используйте если класс событий называется не Evt или находится в нестандартной сборке.
        /// </summary>
        public static void SetEventIdsType(Type type)
        {
            _eventIdsType = type;
            _initialized = false; // Сброс для переинициализации
        }

        /// <summary>
        /// Устанавливает тип класса событий явно (generic версия)
        /// </summary>
        public static void SetEventIdsType<T>()
        {
            _eventIdsType = typeof(T);
            _initialized = false;
        }

        /// <summary>
        /// Инициализирует кеш путей событий через рефлексию класса Evt
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            _pathToIdCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _idToPathCache = new Dictionary<int, string>();

            var evtType = _eventIdsType ?? FindEventIdsType();
            if (evtType == null)
            {
                Debug.LogWarning("[EventPathResolver] Класс событий (Evt) не найден. Сгенерируйте EventIds через ProtoSystem.");
                _initialized = true;
                return;
            }

            var nestedTypes = evtType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);

            foreach (var nestedType in nestedTypes)
            {
                // Пропускаем enum EventType
                if (nestedType.IsEnum) continue;

                ProcessNestedType(nestedType, nestedType.Name);
            }

            _initialized = true;
            Debug.Log($"[EventPathResolver] Инициализировано {_pathToIdCache.Count} путей событий");
        }

        /// <summary>
        /// Рекурсивно обрабатывает вложенные типы для поддержки многоуровневой вложенности
        /// </summary>
        private static void ProcessNestedType(Type type, string pathPrefix)
        {
            // Получаем все public const int поля
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(int));

            foreach (var field in fields)
            {
                var eventName = field.Name;
                var eventPath = $"{pathPrefix}.{eventName}";
                var eventId = (int)field.GetRawConstantValue();

                _pathToIdCache[eventPath] = eventId;
                _idToPathCache[eventId] = eventPath;
            }

            // Рекурсивно обрабатываем вложенные классы (например Интерфейс.HUD)
            var nestedTypes = type.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);
            foreach (var nestedType in nestedTypes)
            {
                if (nestedType.IsEnum) continue;
                ProcessNestedType(nestedType, $"{pathPrefix}.{nestedType.Name}");
            }
        }

        // Стандартные имена классов событий для автопоиска
        private static readonly string[] StandardEventClassNames = new[]
        {
            "_Events",       // KM проект (приоритет)
            "Evt",           // ProtoSystem стандарт
            "Events",        // Общее
            "EventIds",      // Альтернатива
            "GameEvents"     // Ещё вариант
        };

        // Стандартные namespace для поиска (в порядке приоритета)
        private static readonly string[] StandardNamespaces = new[]
        {
            "KM",            // KM проект (приоритет)
            "",              // Глобальный namespace
            "Game",          // Общее
            "Events"         // Namespace Events
        };

        /// <summary>
        /// Ищет класс событий в пользовательских сборках.
        /// Приоритет: KM._Events, затем глобальные Evt/_Events, затем поиск по всем типам
        /// </summary>
        private static Type FindEventIdsType()
        {
            // Порядок поиска:
            // 1. По известным namespace + именам классов
            // 2. Fallback: поиск среди всех типов сборки по имени класса

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;

                // Пропускаем системные и Unity сборки для ускорения
                if (name.StartsWith("Unity.") ||
                    name.StartsWith("UnityEngine") ||
                    name.StartsWith("UnityEditor") ||
                    name.StartsWith("System") ||
                    name.StartsWith("mscorlib") ||
                    name.StartsWith("netstandard") ||
                    name.StartsWith("Mono.") ||
                    name.StartsWith("ProtoSystem")) // Пропускаем сам пакет
                    continue;

                // Ищем по всем стандартным именам и namespace
                foreach (var ns in StandardNamespaces)
                {
                    foreach (var className in StandardEventClassNames)
                    {
                        var fullName = string.IsNullOrEmpty(ns) ? className : $"{ns}.{className}";
                        var evtType = assembly.GetType(fullName);
                        if (evtType != null)
                        {
                            Debug.Log($"[EventPathResolver] Найден класс событий '{fullName}' в сборке {name}");
                            return evtType;
                        }
                    }
                }
            }

            // Fallback: ищем класс с именем Evt/_Events среди ВСЕХ типов (для любого namespace)
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
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
                                    Debug.Log($"[EventPathResolver] Найден класс событий '{type.FullName}' в сборке {name} (fallback)");
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
        /// Получает ID события по текстовому пути.
        /// Автоматически инициализирует кеш при первом вызове.
        /// </summary>
        /// <param name="eventPath">Путь вида "Category.EventName" (например, "Боевка.Урон_нанесен")</param>
        /// <returns>ID события или 0 если не найдено</returns>
        public static int Resolve(string eventPath)
        {
            if (string.IsNullOrEmpty(eventPath)) return 0;

            if (!_initialized) Initialize();

            return _pathToIdCache.TryGetValue(eventPath, out var id) ? id : 0;
        }

        /// <summary>
        /// Получает текстовый путь по ID события
        /// </summary>
        public static string GetPath(int eventId)
        {
            if (eventId <= 0) return "";

            if (!_initialized) Initialize();

            return _idToPathCache.TryGetValue(eventId, out var path) ? path : "";
        }

        /// <summary>
        /// Проверяет существование пути события
        /// </summary>
        public static bool Exists(string eventPath)
        {
            if (string.IsNullOrEmpty(eventPath)) return false;

            if (!_initialized) Initialize();

            return _pathToIdCache.ContainsKey(eventPath);
        }

        /// <summary>
        /// Получает все зарегистрированные пути событий
        /// </summary>
        public static IEnumerable<string> GetAllPaths()
        {
            if (!_initialized) Initialize();

            return _pathToIdCache.Keys;
        }

        /// <summary>
        /// Получает все пути событий в указанной категории
        /// </summary>
        public static IEnumerable<string> GetPathsInCategory(string category)
        {
            if (!_initialized) Initialize();

            return _pathToIdCache.Keys.Where(p => p.StartsWith(category + ".", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Принудительно переинициализирует кеш
        /// </summary>
        public static void Reinitialize()
        {
            _initialized = false;
            Initialize();
        }

        /// <summary>
        /// Возвращает текущий тип класса событий
        /// </summary>
        public static Type GetCurrentEventIdsType()
        {
            return _eventIdsType ?? FindEventIdsType();
        }
    }
}
