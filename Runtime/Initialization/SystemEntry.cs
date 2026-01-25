using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Запись о системе в менеджере инициализации
    /// Теперь поддерживает IInitializableSystem для работы с разными базовыми классами
    /// </summary>
    [System.Serializable]
    public class SystemEntry
    {
        [Header("Основные настройки")]
        public string systemName;
        public bool enabled = true;

        [Header("Источник системы")]
        public bool useExistingObject = false;

        [SerializeField] private MonoBehaviour existingSystemObject;
        [SerializeField] private int selectedComponentIndex = 0;
        public string systemTypeName;

        [Header("Отладка")]
        public bool verboseLogging = true;
        
        [Header("Логирование")]
        [Tooltip("Включить логирование для этой системы")]
        public bool logEnabled = true;
        
        [Tooltip("Уровни логов (флаги)")]
        public LogLevel logLevel = LogLevel.Errors | LogLevel.Warnings | LogLevel.Info;
        
        [Tooltip("Категории логов для этой системы")]
        public LogCategory logCategories = LogCategory.All;
        
        [Tooltip("Цвет логов этой системы в консоли")]
        public Color logColor = Color.white;

        // Скрытые поля для редактора
        [HideInInspector] public List<string> detectedDependencies = new List<string>();
        [HideInInspector] public bool hasCyclicDependency = false;
        [HideInInspector] public string cyclicDependencyInfo = "";

        /// <summary>
        /// Существующий объект системы (может быть из сцены)
        /// Теперь работает с MonoBehaviour для универсальности
        /// </summary>
        public MonoBehaviour ExistingSystemObject
        {
            get => existingSystemObject;
            set => existingSystemObject = value;
        }

        /// <summary>
        /// Тип системы
        /// </summary>
        public Type SystemType
        {
            get
            {
                if (useExistingObject && existingSystemObject != null)
                {
                    return existingSystemObject.GetType();
                }

                if (!string.IsNullOrEmpty(systemTypeName))
                {
                    return Type.GetType(systemTypeName);
                }

                return null;
            }
            set
            {
                if (value != null)
                {
                    systemTypeName = value.AssemblyQualifiedName;
                    if (string.IsNullOrEmpty(systemName))
                    {
                        systemName = value.Name;
                    }
                }
            }
        }

        /// <summary>
        /// Получить или создать экземпляр системы
        /// Теперь возвращает IInitializableSystem для поддержки разных базовых классов
        /// </summary>
        public IInitializableSystem GetOrCreateSystemInstance(GameObject container)
        {
            if (useExistingObject && existingSystemObject != null)
            {
                GameObject sourceObject = existingSystemObject.gameObject;

                // Ищем компоненты, реализующие IInitializableSystem
                var components = sourceObject.GetComponents<MonoBehaviour>()
                    .Where(c => c is IInitializableSystem)
                    .Cast<IInitializableSystem>()
                    .ToArray();

                if (components.Length > 0)
                {
                    int index = Mathf.Clamp(selectedComponentIndex, 0, components.Length - 1);
                    return components[index];
                }

                // Для обратной совместимости - проверяем InitializableSystemBase
                var legacyComponents = sourceObject.GetComponents<InitializableSystemBase>();
                if (legacyComponents.Length > 0)
                {
                    int index = Mathf.Clamp(selectedComponentIndex, 0, legacyComponents.Length - 1);
                    return legacyComponents[index];
                }

                return existingSystemObject as IInitializableSystem;
            }

            Type type = SystemType;
            if (type != null)
            {
                // Проверяем что тип реализует IInitializableSystem
                if (typeof(IInitializableSystem).IsAssignableFrom(type))
                {
                    var component = container.AddComponent(type) as IInitializableSystem;
                    return component;
                }
                // Для обратной совместимости - проверяем InitializableSystemBase
                else if (typeof(InitializableSystemBase).IsAssignableFrom(type))
                {
                    var component = container.AddComponent(type) as IInitializableSystem;
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Проверяет, валидна ли настройка системы
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            errorMessage = "";

            // Проверяем название
            if (string.IsNullOrEmpty(systemName))
            {
                errorMessage = "Название системы не может быть пустым";
                return false;
            }

            // Если используем существующий объект
            if (useExistingObject)
            {
                if (existingSystemObject == null)
                {
                    errorMessage = "Не указан существующий объект системы";
                    return false;
                }

                // Проверяем что объект реализует IInitializableSystem
                if (!(existingSystemObject is IInitializableSystem))
                {
                    // Для обратной совместимости проверяем InitializableSystemBase
                    if (!(existingSystemObject is InitializableSystemBase))
                    {
                        errorMessage = $"Объект {existingSystemObject.name} не реализует IInitializableSystem";
                        return false;
                    }
                }
            }
            else
            {
                // Если создаём новую систему
                if (string.IsNullOrEmpty(systemTypeName))
                {
                    errorMessage = "Не указан тип системы";
                    return false;
                }

                Type type = Type.GetType(systemTypeName);
                if (type == null)
                {
                    errorMessage = $"Не удается найти тип: {systemTypeName}";
                    return false;
                }

                // Проверяем что тип реализует IInitializableSystem
                if (!typeof(IInitializableSystem).IsAssignableFrom(type))
                {
                    // Для обратной совместимости проверяем InitializableSystemBase
                    if (!typeof(InitializableSystemBase).IsAssignableFrom(type))
                    {
                        errorMessage = $"Тип {type.Name} не реализует IInitializableSystem";
                        return false;
                    }
                }

                if (type.IsAbstract)
                {
                    errorMessage = $"Тип {type.Name} является абстрактным";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Получает краткое описание системы для отображения в редакторе
        /// </summary>
        public string GetDisplayInfo()
        {
            if (useExistingObject && existingSystemObject != null)
            {
                return $"📦 {existingSystemObject.name} ({existingSystemObject.GetType().Name})";
            }
            else if (!string.IsNullOrEmpty(systemTypeName))
            {
                Type type = Type.GetType(systemTypeName);
                if (type != null)
                {
                    return $"🔨 Новый {type.Name}";
                }
                else
                {
                    return $"❌ Тип не найден: {systemTypeName}";
                }
            }

            return "❓ Не настроено";
        }

        /// <summary>
        /// Клонирует запись системы
        /// </summary>
        public SystemEntry Clone()
        {
            var clone = new SystemEntry
            {
                systemName = systemName + " (Copy)",
                enabled = enabled,
                useExistingObject = useExistingObject,
                existingSystemObject = existingSystemObject,
                systemTypeName = systemTypeName,
                verboseLogging = verboseLogging,
                logEnabled = logEnabled,
                logLevel = logLevel,
                logCategories = logCategories,
                logColor = logColor
            };

            // Копируем зависимости
            clone.detectedDependencies = new List<string>(detectedDependencies);
            clone.hasCyclicDependency = hasCyclicDependency;
            clone.cyclicDependencyInfo = cyclicDependencyInfo;

            return clone;
        }

        /// <summary>
        /// Сбрасывает данные анализа зависимостей
        /// </summary>
        public void ResetDependencyAnalysis()
        {
            detectedDependencies.Clear();
            hasCyclicDependency = false;
            cyclicDependencyInfo = "";
        }

        /// <summary>
        /// Проверяет, ссылается ли эта система на указанную зависимость
        /// </summary>
        public bool DependsOn(string systemName)
        {
            return detectedDependencies.Contains(systemName);
        }

        /// <summary>
        /// Добавляет зависимость
        /// </summary>
        public void AddDependency(string systemName)
        {
            if (!detectedDependencies.Contains(systemName))
            {
                detectedDependencies.Add(systemName);
            }
        }

        /// <summary>
        /// Удаляет зависимость
        /// </summary>
        public void RemoveDependency(string systemName)
        {
            detectedDependencies.Remove(systemName);
        }

        /// <summary>
        /// Возвращает строковое представление для отладки
        /// </summary>
        public override string ToString()
        {
            string status = enabled ? "✅" : "⭕";
            if (hasCyclicDependency) status = "❌";

            return $"{status} {systemName} - {GetDisplayInfo()}";
        }
    }

    /// <summary>
    /// Аттрибут для автоматической инициализации критических зависимостей
    /// Поля с этим аттрибутом инициализируются ДО выполнения InitializeAsync()
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class DependencyAttribute : Attribute
    {
        /// <summary>
        /// Обязательна ли зависимость (по умолчанию true)
        /// </summary>
        public bool Required { get; set; } = true;

        /// <summary>
        /// Описание зависимости для отладки
        /// </summary>
        public string Description { get; set; } = "";

        public DependencyAttribute(bool required = true, string description = "")
        {
            Required = required;
            Description = description;
        }
    }

    /// <summary>
    /// Аттрибут для автоматической инициализации пост-зависимостей
    /// Поля с этим аттрибутом инициализируются ПОСЛЕ завершения InitializeAsync() всех систем
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class PostDependencyAttribute : Attribute
    {
        /// <summary>
        /// Обязательна ли зависимость (по умолчанию false для пост-зависимостей)
        /// </summary>
        public bool Required { get; set; } = false;

        /// <summary>
        /// Описание зависимости для отладки
        /// </summary>
        public string Description { get; set; } = "";

        public PostDependencyAttribute(bool required = false, string description = "")
        {
            Required = required;
            Description = description;
        }
    }



    /// <summary>
    /// Статусы инициализации
    /// </summary>
    public enum InitializationStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Failed
    }
}
