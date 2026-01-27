using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;
using System.Reflection;
using System.IO;

namespace ProtoSystem
{
    /// <summary>
    /// Главный менеджер инициализации систем
    /// </summary>
    [AddComponentMenu("ProtoSystem/System Initialization Manager")]
    [DefaultExecutionOrder(-1000)]
    public class SystemInitializationManager : MonoBehaviour
    {
        [Header("Настройки инициализации")]
        [SerializeField] private bool autoStartInitialization = true;
        [SerializeField] private float maxInitializationTimeoutSeconds = 30f;

        [Header("Логирование")]
        [SerializeField] private LogSettings logSettings = new LogSettings();
        
        [Header("Внутренние компоненты")]
        [Tooltip("Логирование EventPathResolver (резолвер событий)")]
        [SerializeField] private bool logEventPathResolver = true;
        [SerializeField] private LogLevel eventPathResolverLogLevel = LogLevel.Errors | LogLevel.Warnings | LogLevel.Info;
        [SerializeField] private LogCategory eventPathResolverLogCategories = LogCategory.All;
        
        [Tooltip("Логирование SystemInit (менеджер инициализации)")]
        [SerializeField] private bool logSystemInit = true;
        [SerializeField] private LogLevel systemInitLogLevel = LogLevel.Errors | LogLevel.Warnings | LogLevel.Info;
        [SerializeField] private LogCategory systemInitLogCategories = LogCategory.All;

        [Header("Системы")]
        [SerializeField] private List<SystemEntry> systems = new List<SystemEntry>();

        [Header("Граф зависимостей (только для чтения)")]
        [TextArea(5, 10)]
        [SerializeField] private string dependencyGraph = "";

        [Header("Состояние инициализации")]
        [SerializeField] private float overallProgress = 0f;
        [SerializeField] private string currentSystemName = "";
        [SerializeField] private bool isInitialized = false;
        [SerializeField] private bool isPostDependenciesInitialized = false;

        // События
        public event Action<float> OnInitializationProgress;
        public event Action<string> OnSystemStarted;
        public event Action<string, bool> OnSystemCompleted;
        public event Action<bool> OnInitializationComplete;
        public event Action<bool> OnPostDependenciesComplete;

        // Данные
        private SystemProvider systemProvider;
        private Dictionary<string, IInitializableSystem> systemInstances;
        private Dictionary<string, float> systemProgress;

        // Singleton
        public static SystemInitializationManager Instance { get; private set; }
        public bool IsInitialized => isInitialized;
        public bool IsPostDependenciesInitialized => isPostDependenciesInitialized;
        public SystemProvider SystemProvider => systemProvider;

        // Публичные свойства для доступа к настройкам
        public bool AutoStartInitialization => autoStartInitialization;
        public float MaxInitializationTimeoutSeconds => maxInitializationTimeoutSeconds;
        public LogSettings LogSettings => logSettings;
        public List<SystemEntry> Systems => systems;
        public string DependencyGraph => dependencyGraph;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // Инициализируем логгер
                ProtoLogger.Settings = logSettings;
                
                // Регистрируем per-system настройки логирования СРАЗУ
                // (до того как системы начнут логировать в своих Awake)
                RegisterAllSystemLogSettings();

                systemProvider = new SystemProvider();
                systemInstances = new Dictionary<string, IInitializableSystem>();
                systemProgress = new Dictionary<string, float>();
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Валидируем настройки
            if (!Validate(out List<string> errors))
            {
                ProtoLogger.LogError("SystemInitializationManager", "Ошибки в настройках инициализации:");
                foreach (var error in errors)
                {
                    ProtoLogger.LogError("SystemInitializationManager", $"  - {error}");
                }
                return;
            }
        }

        private void Start()
        {
            if (autoStartInitialization)
            {
                _ = InitializeAllSystemsAsync();
            }
        }

        #region Анализ зависимостей

        /// <summary>
        /// Анализирует зависимости всех систем
        /// </summary>
        public void AnalyzeDependencies()
        {
            foreach (var entry in systems)
            {
                AnalyzeSystemDependencies(entry);
            }

            DetectCyclicDependencies();
            BuildDependencyGraph();
        }

        /// <summary>
        /// Анализирует зависимости конкретной системы через рефлексию
        /// </summary>
        private void AnalyzeSystemDependencies(SystemEntry entry)
        {
            entry.detectedDependencies.Clear();

            Type systemType = entry.SystemType;
            if (systemType == null) return;

            // Анализируем только критические зависимости (без Post-зависимостей)
            AnalyzeFieldDependencies(systemType, entry, "InitializeDependencies", false);
        }

        /// <summary>
        /// Анализирует поля класса для определения зависимостей
        /// </summary>
        private void AnalyzeFieldDependencies(Type systemType, SystemEntry entry, string methodContext, bool includePostDependencies = true)
        {
            var fields = systemType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                // Ищем поля типа InitializableSystemBase или его наследников
                if (typeof(IInitializableSystem).IsAssignableFrom(field.FieldType))
                {
                    // Проверяем атрибуты поля
                    var dependencyAttr = field.GetCustomAttribute<DependencyAttribute>();
                    var postDependencyAttr = field.GetCustomAttribute<PostDependencyAttribute>();

                    bool shouldInclude = false;

                    if (dependencyAttr != null)
                    {
                        // Критическая зависимость - всегда включаем
                        shouldInclude = true;
                    }
                    else if (postDependencyAttr != null && includePostDependencies)
                    {
                        // Post-зависимость - включаем только если разрешено
                        shouldInclude = true;
                    }

                    if (shouldInclude)
                    {
                        // Находим соответствующую запись в системах
                        var dependentSystem = systems.FirstOrDefault(s => s.SystemType == field.FieldType);
                        if (dependentSystem != null && !entry.detectedDependencies.Contains(dependentSystem.systemName))
                        {
                            entry.detectedDependencies.Add(dependentSystem.systemName);

                            string depType = dependencyAttr != null ? "Critical" : "Post";
                            LogMessage($"Обнаружена {depType} зависимость {entry.systemName} -> {dependentSystem.systemName} ({methodContext})", LogCategory.Dependencies);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Обнаруживает циклические зависимости
        /// </summary>
        private void DetectCyclicDependencies()
        {
            var systemDict = systems.ToDictionary(s => s.systemName, s => s);

            foreach (var entry in systems)
            {
                entry.hasCyclicDependency = false;
                entry.cyclicDependencyInfo = "";

                var visited = new HashSet<string>();
                var path = new List<string>();

                if (HasCyclicDependency(entry.systemName, systemDict, visited, path))
                {
                    entry.hasCyclicDependency = true;
                    entry.cyclicDependencyInfo = string.Join(" -> ", path);
                }
            }
        }

        /// <summary>
        /// Рекурсивная проверка циклических зависимостей
        /// </summary>
        private bool HasCyclicDependency(string systemName, Dictionary<string, SystemEntry> systemDict,
            HashSet<string> visited, List<string> path)
        {
            if (path.Contains(systemName))
            {
                path.Add(systemName);
                return true;
            }

            if (visited.Contains(systemName))
                return false;

            visited.Add(systemName);
            path.Add(systemName);

            if (systemDict.TryGetValue(systemName, out var entry))
            {
                foreach (var dependency in entry.detectedDependencies)
                {
                    if (HasCyclicDependency(dependency, systemDict, visited, path))
                    {
                        return true;
                    }
                }
            }

            path.Remove(systemName);
            return false;
        }

        /// <summary>
        /// Строит текстовое представление графа зависимостей
        /// </summary>
        private void BuildDependencyGraph()
        {
            var graph = new System.Text.StringBuilder();
            graph.AppendLine("Граф зависимостей систем:");
            graph.AppendLine();

            int index = 1;
            foreach (var entry in systems)
            {
                graph.AppendLine($"[{index}] {entry.systemName}");

                if (entry.detectedDependencies.Count > 0)
                {
                    graph.AppendLine($"  Зависимости: {string.Join(", ", entry.detectedDependencies)}");
                }
                else
                {
                    graph.AppendLine("  Зависимости: нет");
                }

                if (entry.hasCyclicDependency)
                {
                    graph.AppendLine($"  ЦИКЛИЧЕСКАЯ ЗАВИСИМОСТЬ: {entry.cyclicDependencyInfo}");
                }

                graph.AppendLine();
                index++;
            }

            dependencyGraph = graph.ToString();
        }

        /// <summary>
        /// Получает системы в правильном порядке инициализации
        /// </summary>
        public List<SystemEntry> GetSystemsInInitializationOrder()
        {
            var result = new List<SystemEntry>();
            var systemDict = systems.Where(s => s.enabled).ToDictionary(s => s.systemName, s => s);
            var visited = new HashSet<string>();

            // Топологическая сортировка с сохранением порядка из списка
            foreach (var entry in systems.Where(s => s.enabled))
            {
                if (!visited.Contains(entry.systemName))
                {
                    TopologicalSort(entry.systemName, systemDict, visited, result);
                }
            }

            return result;
        }

        /// <summary>
        /// Топологическая сортировка для определения порядка инициализации
        /// </summary>
        private void TopologicalSort(string systemName, Dictionary<string, SystemEntry> systemDict,
            HashSet<string> visited, List<SystemEntry> result)
        {
            if (visited.Contains(systemName))
                return;

            visited.Add(systemName);

            if (systemDict.TryGetValue(systemName, out var entry))
            {
                // Сначала обрабатываем зависимости
                foreach (var dependency in entry.detectedDependencies)
                {
                    TopologicalSort(dependency, systemDict, visited, result);
                }

                // Затем добавляем саму систему
                result.Add(entry);
            }
        }

        /// <summary>
        /// Валидация настроек
        /// </summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            // Проверяем уникальность имен
            var nameGroups = systems.GroupBy(s => s.systemName).Where(g => g.Count() > 1);
            foreach (var group in nameGroups)
            {
                errors.Add($"Дублированное имя системы: {group.Key}");
            }

            // Проверяем типы систем
            foreach (var entry in systems)
            {
                if (entry.SystemType == null)
                {
                    errors.Add($"Не удается найти тип для системы: {entry.systemName}");
                }
                else if (!typeof(IInitializableSystem).IsAssignableFrom(entry.SystemType))
                {
                    errors.Add($"Тип {entry.SystemType.Name} не реализует IInitializableSystem");
                }
            }

            // Проверяем циклические зависимости
            foreach (var entry in systems.Where(s => s.hasCyclicDependency))
            {
                errors.Add($"Циклическая зависимость в системе {entry.systemName}: {entry.cyclicDependencyInfo}");
            }

            return errors.Count == 0;
        }

        #endregion

        #region Инициализация

        /// <summary>
        /// Главный метод инициализации всех систем
        /// </summary>
        public async Task<bool> InitializeAllSystemsAsync()
        {
            if (isInitialized)
            {
                LogMessage("Системы уже инициализированы");

                if (!isPostDependenciesInitialized)
                {
                    LogMessage("Инициализируем post-зависимости...");
                    bool postResult = InitializePostDependencies();
                    OnPostDependenciesComplete?.Invoke(postResult);
                    return postResult;
                }

                return true;
            }

            LogMessage("Начало инициализации систем");

            try
            {
                // Получаем системы в правильном порядке
                var orderedSystems = GetSystemsInInitializationOrder();

                LogMessage($"Найдено {orderedSystems.Count} систем для инициализации");

                // Создаем экземпляры систем
                if (!CreateSystemInstances(orderedSystems))
                {
                    return false;
                }

                // Инициализируем системы по порядку
                bool allSucceeded = await InitializeSystemsInOrder(orderedSystems);

                isInitialized = allSucceeded;
                OnInitializationComplete?.Invoke(allSucceeded);

                if (allSucceeded)
                {
                    LogMessage("Все системы инициализированы успешно!");

                    // После успешной инициализации всех систем инициализируем post-зависимости
                    LogMessage("Начинаем инициализацию post-зависимостей...");
                    bool postResult = InitializePostDependencies();
                    OnPostDependenciesComplete?.Invoke(postResult);

                    if (postResult)
                    {
                        LogMessage("Post-зависимости инициализированы успешно!");
                    }
                    else
                    {
                        LogError("Инициализация post-зависимостей завершена с ошибками");
                    }

                    return postResult;
                }
                else
                {
                    LogError("Инициализация завершена с ошибками");
                }

                return allSucceeded;
            }
            catch (Exception ex)
            {
                LogError($"Критическая ошибка инициализации: {ex.Message}");
                OnInitializationComplete?.Invoke(false);
                return false;
            }
        }

        /// <summary>
        /// Инициализация post-зависимостей для всех систем
        /// </summary>
        private bool InitializePostDependencies()
        {
            if (isPostDependenciesInitialized)
            {
                LogMessage("Post-зависимости уже инициализированы");
                return true;
            }

            bool allSucceeded = true;

            foreach (var kvp in systemInstances)
            {
                currentSystemName = kvp.Key;
                var systemInstance = kvp.Value;

                LogMessage($"Инициализация post-зависимостей для системы: {kvp.Key}");

                try
                {
                    bool success = systemInstance.InitializePostDependenciesSync(systemProvider);
                    if (!success)
                    {
                        allSucceeded = false;
                        LogError($"Ошибка инициализации post-зависимостей для системы: {kvp.Key}");
                    }
                }
                catch (Exception ex)
                {
                    allSucceeded = false;
                    LogError($"Исключение при инициализации post-зависимостей {kvp.Key}: {ex.Message}");
                }
            }

            currentSystemName = "";
            isPostDependenciesInitialized = allSucceeded;

            return allSucceeded;
        }

        /// <summary>
        /// Создает экземпляры всех систем
        /// </summary>
        private bool CreateSystemInstances(List<SystemEntry> orderedSystems)
        {
            LogMessage("Создание экземпляров систем...");

            foreach (var entry in orderedSystems)
            {
                try
                {
                    var systemInstance = entry.GetOrCreateSystemInstance(gameObject);
                    if (systemInstance == null)
                    {
                        LogError($"Не удалось создать экземпляр системы: {entry.systemName}");
                        return false;
                    }

                    systemInstances[entry.systemName] = systemInstance;
                    systemProgress[entry.systemName] = 0f;

                    // Регистрируем per-system настройки логирования
                    RegisterSystemLogSettings(entry, systemInstance);

                    // Подписываемся на события
                    systemInstance.OnProgressChanged += OnSystemProgressChanged;
                    systemInstance.OnStatusChanged += OnSystemStatusChanged;

                    LogMessage($"Создан экземпляр системы: {entry.systemName} ({systemInstance.GetType().Name})");
                }
                catch (Exception ex)
                {
                    LogError($"Ошибка создания системы {entry.systemName}: {ex.Message}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Регистрирует настройки логирования для системы из SystemEntry
        /// </summary>
        /// <summary>
        /// Регистрирует настройки логирования для ВСЕХ систем из списка (вызывается в Awake)
        /// Определяет SystemId через рефлексию ДО создания экземпляров
        /// </summary>
        private void RegisterAllSystemLogSettings()
        {
            if (ProtoLogger.Settings == null) return;

            // Регистрируем псевдосистемы (внутренние компоненты ProtoSystem)
            RegisterPseudoSystemLogSettings();

            foreach (var entry in systems)
            {
                if (!entry.enabled) continue;

                // Определяем systemId
                string systemId = GetSystemIdForEntry(entry);
                
                if (string.IsNullOrEmpty(systemId))
                {
                    systemId = entry.systemName; // Fallback
                }

                // Регистрируем override
                if (!entry.logEnabled)
                {
                    ProtoLogger.Settings.SetOverride(systemId, LogLevel.None, LogCategory.None, false);
                }
                else
                {
                    ProtoLogger.Settings.SetOverride(systemId, entry.logLevel, entry.logCategories, false);
                }
            }
        }
        
        /// <summary>
        /// Регистрирует настройки логирования для внутренних компонентов ProtoSystem
        /// </summary>
        private void RegisterPseudoSystemLogSettings()
        {
            // EventPathResolver
            if (!logEventPathResolver)
            {
                ProtoLogger.Settings.SetOverride("EventPathResolver", LogLevel.None, LogCategory.None, false);
            }
            else
            {
                ProtoLogger.Settings.SetOverride("EventPathResolver", eventPathResolverLogLevel, eventPathResolverLogCategories, false);
            }
            
            // SystemInit (LOG_ID этого менеджера)
            if (!logSystemInit)
            {
                ProtoLogger.Settings.SetOverride(LOG_ID, LogLevel.None, LogCategory.None, false);
            }
            else
            {
                ProtoLogger.Settings.SetOverride(LOG_ID, systemInitLogLevel, systemInitLogCategories, false);
            }
        }
        
        /// <summary>
        /// Определяет SystemId для SystemEntry через рефлексию или существующий объект
        /// </summary>
        private string GetSystemIdForEntry(SystemEntry entry)
        {
            // Если есть существующий объект — берём его SystemId
            if (entry.useExistingObject && entry.ExistingSystemObject is IInitializableSystem existingSystem)
            {
                return existingSystem.SystemId;
            }
            
            // Пробуем получить SystemId через временный объект
            Type systemType = entry.SystemType;
            if (systemType == null || !typeof(MonoBehaviour).IsAssignableFrom(systemType)) 
                return null;
            
            try
            {
                // Создаём временный disabled GameObject (Awake не вызовется)
                var tempGO = new GameObject("__TempSystemIdResolver__");
                tempGO.SetActive(false);
                
                var tempComponent = tempGO.AddComponent(systemType) as IInitializableSystem;
                string systemId = tempComponent?.SystemId;
                
                // Уничтожаем временный объект
                DestroyImmediate(tempGO);
                
                return systemId;
            }
            catch
            {
                // Рефлексия не сработала — используем fallback
            }
            
            return null;
        }

        private void RegisterSystemLogSettings(SystemEntry entry, IInitializableSystem systemInstance)
        {
            if (ProtoLogger.Settings == null) return;

            string systemId = systemInstance.SystemId;

            // Если логирование выключено для этой системы — ставим None
            if (!entry.logEnabled)
            {
                ProtoLogger.Settings.SetOverride(systemId, LogLevel.None, LogCategory.None, false);
                return;
            }

            // Регистрируем override с настройками из SystemEntry
            var existingOverride = ProtoLogger.Settings.GetOverride(systemId);
            if (existingOverride != null)
            {
                existingOverride.logLevel = entry.logLevel;
                existingOverride.logCategories = entry.logCategories;
                existingOverride.useGlobal = false;
            }
            else
            {
                ProtoLogger.Settings.systemOverrides.Add(new SystemLogOverride
                {
                    systemId = systemId,
                    logLevel = entry.logLevel,
                    logCategories = entry.logCategories,
                    useGlobal = false
                });
            }
        }

        /// <summary>
        /// Инициализирует системы в правильном порядке
        /// </summary>
        private async Task<bool> InitializeSystemsInOrder(List<SystemEntry> orderedSystems)
        {
            bool allSucceeded = true;
            int completedCount = 0;

            foreach (var entry in orderedSystems)
            {
                if (!systemInstances.TryGetValue(entry.systemName, out var systemInstance))
                {
                    LogError($"Экземпляр системы {entry.systemName} не найден!");
                    allSucceeded = false;
                    continue;
                }

                currentSystemName = entry.systemName;
                OnSystemStarted?.Invoke(entry.systemName);

                LogMessage($"Инициализация системы: {entry.systemName}");

                // Регистрируем систему в провайдере перед инициализацией
                systemProvider.RegisterSystem(systemInstance);

                // Используем timeout для предотвращения зависания
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(maxInitializationTimeoutSeconds));
                var initTask = systemInstance.FullInitializeAsync(systemProvider);

                var completedTask = await Task.WhenAny(initTask, timeoutTask);

                bool systemSuccess = false;
                if (completedTask == timeoutTask)
                {
                    LogError($"Timeout инициализации системы {entry.systemName}");
                    allSucceeded = false;
                }
                else
                {
                    systemSuccess = await initTask;
                    if (!systemSuccess)
                    {
                        allSucceeded = false;
                    }
                }

                // Включаем систему после успешной инициализации
                if (systemSuccess)
                {
                    systemInstance.enabled = true;
                }

                OnSystemCompleted?.Invoke(entry.systemName, systemSuccess);
                completedCount++;

                // Обновляем общий прогресс
                overallProgress = (float)completedCount / orderedSystems.Count;
                OnInitializationProgress?.Invoke(overallProgress);

                LogMessage($"Система {entry.systemName} - {(systemSuccess ? "УСПЕХ" : "НЕУДАЧА")}");
            }

            currentSystemName = "";
            return allSucceeded;
        }

        #endregion

        #region Обработчики событий

        /// <summary>
        /// Обработчик изменения прогресса системы
        /// </summary>
        private void OnSystemProgressChanged(string systemId, float progress)
        {
            if (systemProgress.ContainsKey(systemId))
            {
                systemProgress[systemId] = progress;
                RecalculateOverallProgress();
            }
        }

        /// <summary>
        /// Обработчик изменения статуса системы
        /// </summary>
        private void OnSystemStatusChanged(string systemId, InitializationStatus status)
        {
            LogMessage($"Система {systemId} изменила статус на: {status}");
        }

        /// <summary>
        /// Пересчитывает общий прогресс на основе прогресса отдельных систем
        /// </summary>
        private void RecalculateOverallProgress()
        {
            if (systemProgress.Count == 0)
                return;

            float totalProgress = systemProgress.Values.Sum();
            overallProgress = totalProgress / systemProgress.Count;
            OnInitializationProgress?.Invoke(overallProgress);
        }

        #endregion

        #region Публичные методы

        /// <summary>
        /// Ручной запуск инициализации
        /// </summary>
        public void StartManualInitialization()
        {
            if (!isInitialized)
            {
                _ = InitializeAllSystemsAsync();
            }
            else if (!isPostDependenciesInitialized)
            {
                bool postResult = InitializePostDependencies();
                OnPostDependenciesComplete?.Invoke(postResult);
            }
        }

        /// <summary>
        /// Ручной запуск инициализации только post-зависимостей
        /// </summary>
        public void StartPostDependenciesInitialization()
        {
            if (isInitialized && !isPostDependenciesInitialized)
            {
                bool postResult = InitializePostDependencies();
                OnPostDependenciesComplete?.Invoke(postResult);
            }
        }

        /// <summary>
        /// Получить систему по типу
        /// </summary>
        public T GetSystem<T>() where T : class
        {
            return systemProvider.GetSystem<T>();
        }

        /// <summary>
        /// Проверить наличие системы
        /// </summary>
        public bool HasSystem<T>() where T : class
        {
            return systemProvider.HasSystem<T>();
        }

        /// <summary>
        /// Добавить новую систему в рантайме
        /// </summary>
        public void AddSystem(SystemEntry systemEntry)
        {
            if (!systems.Any(s => s.systemName == systemEntry.systemName))
            {
                systems.Add(systemEntry);
                AnalyzeDependencies();
            }
        }

        /// <summary>
        /// Удалить систему
        /// </summary>
        public void RemoveSystem(string systemName)
        {
            var systemToRemove = systems.FirstOrDefault(s => s.systemName == systemName);
            if (systemToRemove != null)
            {
                systems.Remove(systemToRemove);
                AnalyzeDependencies();
            }
        }

        /// <summary>
        /// Сбросить состояние всех систем, реализующих IResettable.
        /// Вызывается автоматически при событии Session.Reset.
        /// </summary>
        public void ResetAllResettableSystems()
        {
            if (systemProvider == null) return;

            int resetCount = 0;

            foreach (var system in systemProvider.GetAllSystems())
            {
                if (system is IResettable resettable)
                {
                    try
                    {
                        resettable.ResetState();
                        resetCount++;
                        // Логируем от имени конкретной системы
                        ProtoLogger.Log(system.SystemId, LogCategory.Runtime, LogLevel.Info, "Reset");
                    }
                    catch (System.Exception ex)
                    {
                        ProtoLogger.LogError(system.SystemId, $"Error resetting: {ex.Message}");
                    }
                }
            }

            // Итоговый лог от имени GameSessionSystem (т.к. он вызывает reset)
            ProtoLogger.Log("game_session", LogCategory.Runtime, LogLevel.Info, $"Reset {resetCount} resettable systems");
        }

        /// <summary>
        /// Получить все системы, реализующие IResettable
        /// </summary>
        public IEnumerable<IResettable> GetResettableSystems()
        {
            if (systemProvider == null) yield break;

            foreach (var system in systemProvider.GetAllSystems())
            {
                if (system is IResettable resettable)
                {
                    yield return resettable;
                }
            }
        }

        #endregion

        #region Утилиты

        private const string LOG_ID = "SystemInit";

        private void LogMessage(string message, LogCategory category = LogCategory.Initialization)
        {
            ProtoLogger.Log(LOG_ID, category, LogLevel.Info, message);
        }

        private void LogError(string message)
        {
            ProtoLogger.LogError(LOG_ID, message);
        }

        private void OnDestroy()
        {
            // Отписываемся от событий
            if (systemInstances != null)
            {
                foreach (var system in systemInstances.Values)
                {
                    if (system != null)
                    {
                        system.OnProgressChanged -= OnSystemProgressChanged;
                        system.OnStatusChanged -= OnSystemStatusChanged;
                    }
                }
            }
        }

        /// <summary>
        /// Обновляет настройки логирования в рантайме (вызывается из Editor)
        /// </summary>
        public void RefreshLogSettings()
        {
            if (!Application.isPlaying) return;
            
            ProtoLogger.Settings = logSettings;
            RegisterAllSystemLogSettings();
        }

        /// <summary>
        /// Синхронизирует настройки логирования при изменении в инспекторе
        /// </summary>
        private void OnValidate()
        {
            // Обновляем настройки ProtoLogger при изменении в инспекторе
            if (Instance == this && Application.isPlaying)
            {
                ProtoLogger.Settings = logSettings;
                RegisterAllSystemLogSettings();
            }
        }

        #endregion

        #region MCP интеграция

        /// <summary>
        /// Экспортирует порядок инициализации в JSON
        /// </summary>
        public string ExportInitializationOrderToJSON()
        {
            var orderedSystems = GetSystemsInInitializationOrder();

            var exportData = new InitializationExportData
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                totalSystems = orderedSystems.Count,
                systems = new List<SystemExportInfo>()
            };

            int order = 1;
            foreach (var system in orderedSystems)
            {
                exportData.systems.Add(new SystemExportInfo
                {
                    order = order++,
                    systemName = system.systemName,
                    typeName = system.SystemType?.FullName ?? "Unknown",
                    enabled = system.enabled,
                    dependencies = new List<string>(system.detectedDependencies),
                    hasCyclicDependency = system.hasCyclicDependency
                });
            }

            string json = JsonUtility.ToJson(exportData, true);

            // Сохраняем в файл
            string mcpDir = Path.Combine(Application.dataPath, "MCP");
            if (!Directory.Exists(mcpDir))
            {
                Directory.CreateDirectory(mcpDir);
            }

            string filePath = Path.Combine(mcpDir, "initialization_order.json");
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);

            LogMessage($"Экспорт для MCP: {filePath}");
            return filePath;
        }

        /// <summary>
        /// Импортирует порядок инициализации из JSON
        /// </summary>
        public bool ImportInitializationOrderFromJSON(string jsonContent)
        {
            try
            {
                var importData = JsonUtility.FromJson<InitializationExportData>(jsonContent);

                if (importData == null || importData.systems == null)
                {
                    LogError("Неверный формат JSON");
                    return false;
                }

                LogMessage($"Импортировано {importData.systems.Count} систем из JSON");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Ошибка импорта JSON: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Импортирует данные от MCP сервера
        /// </summary>
        public bool ImportFromMCPServer()
        {
            string mcpDir = Path.Combine(Application.dataPath, "MCP");
            string filePath = Path.Combine(mcpDir, "initialization_order.json");

            if (!File.Exists(filePath))
            {
                LogError($"MCP файл не найден: {filePath}");
                return false;
            }

            try
            {
                string jsonContent = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                bool success = ImportInitializationOrderFromJSON(jsonContent);

                if (success)
                {
                    LogMessage($"Импорт от MCP успешен: {filePath}");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogError($"Ошибка импорта от MCP: {ex.Message}");
                return false;
            }
        }

        #endregion
    }

    #region MCP Data Classes

    [Serializable]
    public class InitializationExportData
    {
        public string timestamp;
        public int totalSystems;
        public List<SystemExportInfo> systems;
    }

    [Serializable]
    public class SystemExportInfo
    {
        public int order;
        public string systemName;
        public string typeName;
        public bool enabled;
        public List<string> dependencies;
        public bool hasCyclicDependency;
    }

    #endregion
}
