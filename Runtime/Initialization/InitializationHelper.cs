using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Вспомогательный класс для инициализации систем
    /// Содержит общую логику работы с зависимостями и инициализацией
    /// </summary>
    public class InitializationHelper
    {
        private readonly IInitializableSystem system;
        private readonly MonoBehaviour component;
        private bool verboseLogging;

        /// <summary>
        /// Статус инициализации
        /// </summary>
        public InitializationStatus Status { get; private set; } = InitializationStatus.NotStarted;

        /// <summary>
        /// Флаги инициализации
        /// </summary>
        public bool IsInitializedDependencies { get; private set; } = false;
        public bool IsInitializedPostDependencies { get; private set; } = false;

        /// <summary>
        /// События
        /// </summary>
        public event Action<string, float> OnProgressChanged;
        public event Action<string, InitializationStatus> OnStatusChanged;

        /// <summary>
        /// Конструктор помощника
        /// </summary>
        /// <param name="system">Система для инициализации</param>
        /// <param name="component">Unity компонент системы</param>
        /// <param name="verboseLogging">Включить подробное логирование</param>
        public InitializationHelper(IInitializableSystem system, MonoBehaviour component, bool verboseLogging = false)
        {
            this.system = system;
            this.component = component;
            this.verboseLogging = verboseLogging;
        }

        /// <summary>
        /// Автоматическая инициализация зависимостей по атрибутам
        /// </summary>
        /// <param name="provider">Провайдер систем</param>
        /// <param name="attributeType">Тип атрибута (Dependency или PostDependency)</param>
        /// <returns>True если все обязательные зависимости инициализированы</returns>
        public bool AutoInitializeDependencies(SystemProvider provider, Type attributeType)
        {
            bool allSucceeded = true;
            var fields = component.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                // Проверяем, что поле наследуется от IInitializableSystem
                if (!typeof(IInitializableSystem).IsAssignableFrom(field.FieldType))
                {
                    // Также проверяем MonoBehaviour для обратной совместимости
                    if (!typeof(MonoBehaviour).IsAssignableFrom(field.FieldType))
                        continue;
                }

                // Проверяем наличие нужного атрибута
                var attribute = field.GetCustomAttribute(attributeType);
                if (attribute == null)
                    continue;

                // Получаем параметры атрибута
                bool isRequired = true;
                string description = "";

                if (attribute is DependencyAttribute dep)
                {
                    isRequired = dep.Required;
                    description = dep.Description;
                }
                else if (attribute is PostDependencyAttribute postDep)
                {
                    isRequired = postDep.Required;
                    description = postDep.Description;
                }

                // Пытаемся получить систему
                var dependencySystem = GetSystemByType(provider, field.FieldType);

                if (dependencySystem != null)
                {
                    field.SetValue(component, dependencySystem);
                    string msg = $"Зависимость {field.Name} ({field.FieldType.Name}) успешно установлена";
                    if (!string.IsNullOrEmpty(description))
                        msg += $" - {description}";
                    LogMessage(msg);
                }
                else
                {
                    string errorMsg = $"Не удалось получить зависимость {field.Name} ({field.FieldType.Name})";
                    if (!string.IsNullOrEmpty(description))
                        errorMsg += $" - {description}";

                    if (isRequired)
                    {
                        LogError(errorMsg + " (обязательная зависимость)");
                        allSucceeded = false;
                    }
                    else
                    {
                        LogMessage(errorMsg + " (необязательная зависимость)");
                    }
                }
            }

            return allSucceeded;
        }

        /// <summary>
        /// Инициализация критических зависимостей
        /// </summary>
        public void InitializeDependencies(SystemProvider provider)
        {
            LogMessage("Автоматическая инициализация критических зависимостей...");
            IsInitializedDependencies = AutoInitializeDependencies(provider, typeof(DependencyAttribute));
        }

        /// <summary>
        /// Инициализация пост-зависимостей
        /// </summary>
        public void InitializePostDependencies(SystemProvider provider)
        {
            LogMessage("Автоматическая инициализация пост-зависимостей...");
            IsInitializedPostDependencies = AutoInitializeDependencies(provider, typeof(PostDependencyAttribute));
        }

        /// <summary>
        /// Полная асинхронная инициализация
        /// </summary>
        public async Task<bool> FullInitializeAsync(SystemProvider provider)
        {
            try
            {
                ChangeStatus(InitializationStatus.InProgress);
                ReportProgress(0f);

                LogMessage($"Начало инициализации {system.DisplayName}");

                // Сначала инициализируем критические зависимости
                InitializeDependencies(provider);
                ReportProgress(0.2f);

                // Затем саму систему
                bool success = await system.InitializeAsync();

                if (success)
                {
                    ChangeStatus(InitializationStatus.Completed);
                    ReportProgress(1f);
                    LogMessage($"Инициализация {system.DisplayName} завершена успешно");
                }
                else
                {
                    ChangeStatus(InitializationStatus.Failed);
                    LogError($"Инициализация {system.DisplayName} завершилась неудачей");
                }

                return success;
            }
            catch (Exception ex)
            {
                ChangeStatus(InitializationStatus.Failed);
                LogError($"Ошибка инициализации {system.DisplayName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Синхронная инициализация пост-зависимостей
        /// </summary>
        public bool InitializePostDependenciesSync(SystemProvider provider)
        {
            try
            {
                LogMessage($"Начало инициализации post-зависимостей {system.DisplayName}");

                InitializePostDependencies(provider);

                LogMessage($"Post-зависимости {system.DisplayName} инициализированы успешно");
                return IsInitializedPostDependencies;
            }
            catch (Exception ex)
            {
                LogError($"Ошибка инициализации post-зависимостей {system.DisplayName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получить систему по типу через рефлексию
        /// </summary>
        private object GetSystemByType(SystemProvider provider, Type systemType)
        {
            // Сначала пробуем через интерфейс
            if (typeof(IInitializableSystem).IsAssignableFrom(systemType))
            {
                var method = provider.GetType().GetMethod("GetSystem").MakeGenericMethod(systemType);
                return method.Invoke(provider, null);
            }

            // Для обратной совместимости - если это просто MonoBehaviour
            if (typeof(MonoBehaviour).IsAssignableFrom(systemType))
            {
                var method = provider.GetType().GetMethod("GetSystem").MakeGenericMethod(systemType);
                return method.Invoke(provider, null);
            }

            return null;
        }

        /// <summary>
        /// Получить информацию о зависимостях
        /// </summary>
        public DependencyInfo[] GetDependencies()
        {
            var dependencies = new System.Collections.Generic.List<DependencyInfo>();
            var fields = component.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (!typeof(IInitializableSystem).IsAssignableFrom(field.FieldType) &&
                    !typeof(MonoBehaviour).IsAssignableFrom(field.FieldType))
                    continue;

                var depAttr = field.GetCustomAttribute<DependencyAttribute>();
                var postDepAttr = field.GetCustomAttribute<PostDependencyAttribute>();

                if (depAttr != null)
                {
                    dependencies.Add(new DependencyInfo
                    {
                        FieldName = field.Name,
                        SystemType = field.FieldType,
                        IsRequired = depAttr.Required,
                        Description = depAttr.Description,
                        DependencyType = DependencyType.Critical
                    });
                }
                else if (postDepAttr != null)
                {
                    dependencies.Add(new DependencyInfo
                    {
                        FieldName = field.Name,
                        SystemType = field.FieldType,
                        IsRequired = postDepAttr.Required,
                        Description = postDepAttr.Description,
                        DependencyType = DependencyType.Post
                    });
                }
            }

            return dependencies.ToArray();
        }

        /// <summary>
        /// Изменить статус инициализации
        /// </summary>
        private void ChangeStatus(InitializationStatus newStatus)
        {
            Status = newStatus;
            OnStatusChanged?.Invoke(system.SystemId, newStatus);
        }

        /// <summary>
        /// Сообщить о прогрессе
        /// </summary>
        public void ReportProgress(float progress)
        {
            OnProgressChanged?.Invoke(system.SystemId, Mathf.Clamp01(progress));
        }

        /// <summary>
        /// Логирование
        /// </summary>
        protected void LogMessage(string message)
        {
            if (verboseLogging)
                Debug.Log($"[{system.SystemId}] {message}");
        }

        protected void LogError(string message)
        {
            Debug.LogError($"[{system.SystemId}] {message}");
        }

        protected void LogWarning(string message)
        {
            Debug.LogWarning($"[{system.SystemId}] {message}");
        }
    }
}
