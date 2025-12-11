using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace ProtoSystem
{
    /// <summary>
    /// Провайдер систем - содержит все инициализированные системы
    /// Теперь работает с интерфейсом IInitializableSystem для поддержки разных базовых классов
    /// </summary>
    public class SystemProvider
    {
        private Dictionary<Type, object> systems = new Dictionary<Type, object>();
        private bool isDebug = false;

        /// <summary>
        /// Регистрация системы через интерфейс
        /// </summary>
        public void RegisterSystem(IInitializableSystem system)
        {
            if (system == null)
            {
                Debug.LogError("Attempted to register null system!");
                return;
            }

            Type actualType = system.GetType();
            systems[actualType] = system;

            if (isDebug) Debug.Log($"Registering system: {actualType.Name} (ID: {system.SystemId})");

            // Регистрируем по всем интерфейсам для удобства поиска
            foreach (var interfaceType in actualType.GetInterfaces())
            {
                if (!systems.ContainsKey(interfaceType))
                {
                    systems[interfaceType] = system;
                    if (isDebug) Debug.Log($"Also registering under interface: {interfaceType.Name}");
                }
            }

            // Также регистрируем по базовым типам для совместимости
            Type baseType = actualType.BaseType;
            while (baseType != null && baseType != typeof(MonoBehaviour) && baseType != typeof(object))
            {
                if (!systems.ContainsKey(baseType))
                {
                    systems[baseType] = system;
                    if (isDebug) Debug.Log($"Also registering under base type: {baseType.Name}");
                }
                baseType = baseType.BaseType;
            }

            // Выводим информацию о зарегистрированных типах для отладки
            if (isDebug)
            {
                var typesForThisSystem = systems.Keys.Where(k => systems[k] == system).ToList();
                Debug.Log($"System {system.SystemId} registered under types: {string.Join(", ", typesForThisSystem.Select(t => t.Name))}");
            }
        }

        /// <summary>
        /// Перегрузка для регистрации системы без generic параметра (обратная совместимость)
        /// </summary>
        public void RegisterSystem(object system)
        {
            if (system == null)
            {
                Debug.LogError("Attempted to register null system!");
                return;
            }

            // Если это IInitializableSystem, используем типизированный метод
            if (system is IInitializableSystem initSystem)
            {
                RegisterSystem(initSystem);
                return;
            }

            // Для обратной совместимости - регистрируем как обычный объект
            Type actualType = system.GetType();
            systems[actualType] = system;

            if (isDebug) Debug.Log($"Registering legacy system: {actualType.Name}");

            // Регистрируем по базовым типам
            Type baseType = actualType.BaseType;
            while (baseType != null && baseType != typeof(MonoBehaviour) && baseType != typeof(object))
            {
                if (!systems.ContainsKey(baseType))
                {
                    systems[baseType] = system;
                }
                baseType = baseType.BaseType;
            }
        }

        /// <summary>
        /// Получить систему по типу
        /// </summary>
        public T GetSystem<T>() where T : class
        {
            Type requestedType = typeof(T);

            if (systems.TryGetValue(requestedType, out var system))
            {
                if (isDebug) Debug.Log($"Found system {requestedType.Name}: {(system as IInitializableSystem)?.SystemId ?? system.GetType().Name}");
                return system as T;
            }

            // Если не найдено по точному типу, ищем среди зарегистрированных систем
            var foundSystem = systems.Values.FirstOrDefault(s => s is T);
            if (foundSystem != null)
            {
                if (isDebug) Debug.Log($"Found system {requestedType.Name} by instance check");
                return foundSystem as T;
            }

            if (isDebug) Debug.LogError($"System of type {requestedType.Name} not found! Available systems: {string.Join(", ", systems.Keys.Select(k => k.Name))}");
            return null;
        }

        /// <summary>
        /// Проверить, есть ли система определенного типа
        /// </summary>
        public bool HasSystem<T>() where T : class
        {
            Type requestedType = typeof(T);

            if (systems.ContainsKey(requestedType))
            {
                return true;
            }

            // Также проверяем среди зарегистрированных систем
            return systems.Values.Any(s => s is T);
        }

        /// <summary>
        /// Получить все зарегистрированные системы
        /// </summary>
        public IEnumerable<IInitializableSystem> GetAllSystems()
        {
            return systems.Values
                .Where(s => s is IInitializableSystem)
                .Cast<IInitializableSystem>()
                .Distinct();
        }

        /// <summary>
        /// Получить все объекты (включая не-системы, для обратной совместимости)
        /// </summary>
        public IEnumerable<object> GetAllObjects()
        {
            return systems.Values.Distinct();
        }

        /// <summary>
        /// Очистить все системы
        /// </summary>
        public void Clear()
        {
            systems.Clear();
            if (isDebug) Debug.Log("SystemProvider cleared");
        }

        /// <summary>
        /// Установить режим отладки
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            isDebug = enabled;
        }
    }
}
