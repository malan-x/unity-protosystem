using System;
using System.Threading.Tasks;
using UnityEngine;
using System.Reflection;
using System.Linq;
using ProtoSystem;

namespace ProtoSystem
{
    /// <summary>
    /// Базовый класс для всех инициализируемых систем (обратная совместимость)
    /// Теперь реализует интерфейс IInitializableSystem для унификации с сетевыми системами
    /// Наследуется от MonoEventBus для локальных систем
    /// </summary>
    public abstract class InitializableSystemBase : MonoEventBus, IInitializableSystem
    {
        [Header("Система")]
        [SerializeField] protected bool verboseLogging = false;

        /// <summary>
        /// Вспомогательный класс для инициализации
        /// </summary>
        private InitializationHelper initHelper;

        /// <summary>
        /// Уникальный идентификатор системы
        /// </summary>
        public abstract string SystemId { get; }

        /// <summary>
        /// Название системы для отображения
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// Краткое описание назначения системы (отображается в инспекторе)
        /// </summary>
        public virtual string Description => null;

        /// <summary>
        /// Флаги инициализации (для обратной совместимости)
        /// </summary>
        public bool isInitializedDependencies => initHelper?.IsInitializedDependencies ?? false;
        public bool isInitializedPostDependencies => initHelper?.IsInitializedPostDependencies ?? false;

        /// <summary>
        /// Интерфейсные свойства
        /// </summary>
        public bool IsInitializedDependencies => isInitializedDependencies;
        public bool IsInitializedPostDependencies => isInitializedPostDependencies;

        /// <summary>
        /// Статус инициализации
        /// </summary>
        public InitializationStatus Status => initHelper?.Status ?? InitializationStatus.NotStarted;

        /// <summary>
        /// События изменения прогресса и статуса
        /// </summary>
        public event Action<string, float> OnProgressChanged;
        public event Action<string, InitializationStatus> OnStatusChanged;

        /// <summary>
        /// Инициализация помощника в Awake
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            initHelper = new InitializationHelper(this, this, verboseLogging);
            initHelper.OnProgressChanged += (id, progress) => OnProgressChanged?.Invoke(id, progress);
            initHelper.OnStatusChanged += (id, status) => OnStatusChanged?.Invoke(id, status);
        }

        /// <summary>
        /// Инициализация критических зависимостей - выполняется ДО инициализации системы
        /// Теперь виртуальный метод с автоматической инициализацией через аттрибуты
        /// Переопределите для кастомной логики, но не забудьте вызвать base.InitializeDependencies(provider)
        /// </summary>
        /// <param name="provider">Провайдер для получения других систем</param>
        public virtual void InitializeDependencies(SystemProvider provider)
        {
            initHelper.InitializeDependencies(provider);
        }

        /// <summary>
        /// Инициализация дополнительных зависимостей - выполняется ПОСЛЕ инициализации всех систем
        /// Теперь виртуальный метод с автоматической инициализацией через аттрибуты
        /// Переопределите для кастомной логики, но не забудьте вызвать base.InitializePostDependencies(provider)
        /// </summary>
        /// <param name="provider">Провайдер для получения других систем</param>
        public virtual void InitializePostDependencies(SystemProvider provider)
        {
            initHelper.InitializePostDependencies(provider);
        }

        /// <summary>
        /// Асинхронная инициализация системы
        /// </summary>
        public abstract Task<bool> InitializeAsync();

        /// <summary>
        /// Публичный метод для запуска полной инициализации (без post-зависимостей)
        /// </summary>
        public async Task<bool> FullInitializeAsync(SystemProvider provider)
        {
            return await initHelper.FullInitializeAsync(provider);
        }

        /// <summary>
        /// Публичный метод для инициализации post-зависимостей
        /// Вызывается после того, как все системы прошли FullInitializeAsync
        /// </summary>
        public bool InitializePostDependenciesSync(SystemProvider provider)
        {
            return initHelper.InitializePostDependenciesSync(provider);
        }

        /// <summary>
        /// Получает список всех зависимостей системы для анализа
        /// </summary>
        public DependencyInfo[] GetDependencies()
        {
            return initHelper.GetDependencies();
        }

        /// <summary>
        /// Вспомогательные методы для наследников (обратная совместимость)
        /// </summary>
        protected void ReportProgress(float progress)
        {
            initHelper?.ReportProgress(progress);
        }

        protected bool SetSystem(SystemProvider provider, IInitializableSystem system)
        {
            if (system == null)
            {
                LogError($"Не удалось получить {system?.name}:{system?.GetType()?.Name} из провайдера!");
                return false;
            }
            else
            {
                LogMessage($"Зависимость от {system.name}:{system.GetType().Name} успешно установлена");
                return true;
            }
        }

        protected void LogMessage(string message)
        {
            if (verboseLogging)
                Debug.Log($"[{SystemId}] {message}");
        }

        protected void LogError(string message)
        {
            Debug.LogError($"[{SystemId}] {message}");
        }

        protected void LogWarning(string message)
        {
            Debug.LogWarning($"[{SystemId}] {message}");
        }

        protected void LogMessageInitSystemStart(string message)
        {
            if (verboseLogging)
                Debug.Log($"[{SystemId}] Начало инициализации системы {message}");
        }

        protected void LogMessageInitSystemEnd(string message)
        {
            if (verboseLogging)
                Debug.Log($"[{SystemId}] Система {message} завершена успешно");
        }

        protected void LogMessageInitInterfaceStart(string message)
        {
            if (verboseLogging)
                Debug.Log($"[{SystemId}] Начало инициализации интерфейса {message}");
        }

        protected void LogMessageInitInterfaceEnd(string message)
        {
            if (verboseLogging)
                Debug.Log($"[{SystemId}] Интерфейс {message} завершён успешно");
        }
    }

    /// <summary>
    /// Информация о зависимости для анализа
    /// </summary>
    [System.Serializable]
    public struct DependencyInfo
    {
        public string FieldName;
        public Type SystemType;
        public bool IsRequired;
        public string Description;
        public DependencyType DependencyType;
    }

    /// <summary>
    /// Тип зависимости
    /// </summary>
    public enum DependencyType
    {
        Critical,  // Критическая зависимость (инициализируется до InitializeAsync)
        Post       // Пост-зависимость (инициализируется после InitializeAsync всех систем)
    }
}
