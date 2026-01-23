using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Интерфейс для всех инициализируемых систем
    /// Позволяет использовать как MonoEventBus, так и NetworkEventBus в качестве базовых классов
    /// </summary>
    public interface IInitializableSystem
    {
        /// <summary>
        /// Уникальный идентификатор системы
        /// </summary>
        string SystemId { get; }

        /// <summary>
        /// Отображаемое название системы
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Краткое описание назначения системы
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Текущий статус инициализации
        /// </summary>
        InitializationStatus Status { get; }

        /// <summary>
        /// Флаг готовности критических зависимостей
        /// </summary>
        bool IsInitializedDependencies { get; }

        /// <summary>
        /// Флаг готовности пост-зависимостей
        /// </summary>
        bool IsInitializedPostDependencies { get; }

        /// <summary>
        /// Основная асинхронная инициализация системы
        /// </summary>
        /// <returns>True если инициализация прошла успешно</returns>
        Task<bool> InitializeAsync();

        /// <summary>
        /// Инициализация критических зависимостей (выполняется ДО основной инициализации)
        /// </summary>
        /// <param name="provider">Провайдер систем</param>
        void InitializeDependencies(SystemProvider provider);

        /// <summary>
        /// Инициализация пост-зависимостей (выполняется ПОСЛЕ инициализации всех систем)
        /// </summary>
        /// <param name="provider">Провайдер систем</param>
        void InitializePostDependencies(SystemProvider provider);

        /// <summary>
        /// Полная инициализация системы (зависимости + основная логика)
        /// </summary>
        /// <param name="provider">Провайдер систем</param>
        /// <returns>True если инициализация прошла успешно</returns>
        Task<bool> FullInitializeAsync(SystemProvider provider);

        /// <summary>
        /// Синхронная инициализация пост-зависимостей
        /// </summary>
        /// <param name="provider">Провайдер систем</param>
        /// <returns>True если инициализация прошла успешно</returns>
        bool InitializePostDependenciesSync(SystemProvider provider);

        /// <summary>
        /// Получить информацию о зависимостях системы
        /// </summary>
        /// <returns>Массив зависимостей</returns>
        DependencyInfo[] GetDependencies();

        /// <summary>
        /// Событие изменения прогресса инициализации
        /// </summary>
        event Action<string, float> OnProgressChanged;

        /// <summary>
        /// Событие изменения статуса инициализации
        /// </summary>
        event Action<string, InitializationStatus> OnStatusChanged;

        /// <summary>
        /// Включена ли система (для Unity компонентов)
        /// </summary>
        bool enabled { get; set; }

        /// <summary>
        /// GameObject системы (для Unity компонентов)
        /// </summary>
        GameObject gameObject { get; }

        /// <summary>
        /// Имя системы (для Unity компонентов)
        /// </summary>
        string name { get; }
    }
}
