using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Базовый класс для сетевых инициализируемых систем
    /// Аналог InitializableSystemBase, но для NetworkBehaviour
    /// </summary>
    public abstract class NetworkInitializableSystem : NetworkEventBus, IInitializableSystem
    {
        [Header("Система")]
        [SerializeField] protected bool verboseLogging = false;

        /// <summary>
        /// Вспомогательный класс для инициализации
        /// </summary>
        private InitializationHelper initHelper;

        // NEW: флаги защиты от дублей
        private bool initDeferred;    // отложить до спавна
        private bool initRunning;     // идёт инициализация
        private bool initCompleted;   // уже выполнена

        // NEW: кэш NetworkObject и авто-добавление при необходимости
        private NetworkObject cachedNO;
        private NetworkObject EnsureNetworkObject()
        {
            if (cachedNO != null) return cachedNO;
            cachedNO = GetComponent<NetworkObject>();
            if (cachedNO == null)
            {
                cachedNO = gameObject.AddComponent<NetworkObject>();
                LogMessage("NetworkObject добавлен автоматически для сетевой системы.");
            }
            return cachedNO;
        }

        /// <summary>
        /// Уникальный идентификатор системы
        /// </summary>
        public abstract string SystemId { get; }

        /// <summary>
        /// Название системы для отображения
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// Флаги инициализации
        /// </summary>
        public bool IsInitializedDependencies => initHelper?.IsInitializedDependencies ?? false;
        public bool IsInitializedPostDependencies => initHelper?.IsInitializedPostDependencies ?? false;
        public bool IsInitialized => IsInitializedDependencies;

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
        /// Инициализация помощника при спавне
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Инициализируем помощника только на сервере или хосте
            if (IsServer || IsHost)
            {
                InitializeHelper();
            }

            // ИСПРАВЛЕНО: запускаем отложенную инициализацию только если действительно откладывали
            if (initDeferred && !initCompleted && !initRunning)
            {
                LogMessage("Запуск отложенной инициализации после спавна");
                _ = TryInitializeAfterSpawn();
            }
        }

        /// <summary>
        /// Попытка инициализации после спавна в сети
        /// </summary>
        private async Task TryInitializeAfterSpawn()
        {
            if (initCompleted || initRunning) return;

            try
            {
                initRunning = true;

                // Получаем SystemProvider если он доступен (опционально для совместимости)
                SystemProvider provider = null;
                try
                {
                    // Пытаемся получить SystemInitializationManager через reflection для обратной совместимости
                    var systemManagerType = System.Type.GetType("KM.SystemInitializationManager, Assembly-CSharp");
                    if (systemManagerType != null)
                    {
                        var instanceProperty = systemManagerType.GetProperty("Instance");
                        var systemProviderProperty = systemManagerType.GetProperty("SystemProvider");
                        if (instanceProperty != null && systemProviderProperty != null)
                        {
                            var instance = instanceProperty.GetValue(null);
                            if (instance != null)
                            {
                                provider = systemProviderProperty.GetValue(instance) as SystemProvider;
                            }
                        }
                    }
                }
                catch
                {
                    // Игнорируем ошибки - SystemInitializationManager может быть недоступен
                }

                if (provider != null)
                {
                    bool result = await initHelper.FullInitializeAsync(provider);
                    initCompleted = result;
                    LogMessage(result ? "Система успешно инициализирована (отложенно)" : "Инициализация завершилась с ошибкой (отложенно)");
                }
                else
                {
                    LogMessage("SystemProvider недоступен, выполняем базовую инициализацию (отложенно)");
                    var emptyProvider = new SystemProvider();
                    bool result = await initHelper.FullInitializeAsync(emptyProvider);
                    initCompleted = result;
                }
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при отложенной инициализации: {ex.Message}");
            }
            finally
            {
                initRunning = false;
                initDeferred = false;
            }
        }

        /// <summary>
        /// Инициализация помощника
        /// </summary>
        private void InitializeHelper()
        {
            if (initHelper == null)
            {
                initHelper = new InitializationHelper(this, this, verboseLogging);
                initHelper.OnProgressChanged += (id, progress) => OnProgressChanged?.Invoke(id, progress);
                initHelper.OnStatusChanged += (id, status) => OnStatusChanged?.Invoke(id, status);
            }
        }

        /// <summary>
        /// Инициализация критических зависимостей
        /// </summary>
        public virtual void InitializeDependencies(SystemProvider provider)
        {
            // Убеждаемся что помощник инициализирован
            InitializeHelper();
            initHelper.InitializeDependencies(provider);
        }

        /// <summary>
        /// Инициализация пост-зависимостей
        /// </summary>
        public virtual void InitializePostDependencies(SystemProvider provider)
        {
            InitializeHelper();
            initHelper.InitializePostDependencies(provider);
        }

        /// <summary>
        /// Асинхронная инициализация системы
        /// Переопределите этот метод в наследниках
        /// </summary>
        public abstract Task<bool> InitializeAsync();

        /// <summary>
        /// Полная инициализация системы
        /// </summary>
        public async Task<bool> FullInitializeAsync(SystemProvider provider)
        {
            InitializeHelper();
            enabled = true;

            // Попытка авто-спавна, если сеть уже запущена
            var nm = NetworkManager.Singleton;
            var no = EnsureNetworkObject();
            if (!IsSpawned && nm != null && nm.IsListening && (nm.IsServer || nm.IsHost))
            {
                try
                {
                    if (no != null && !no.IsSpawned)
                    {
                        no.Spawn(true);
                        LogMessage("Авто-спавн сетевой системы на сервере выполнен.");
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"Авто-спавн не удался: {ex.Message}");
                }
            }

            // Если всё ещё не заспавнились — короткое ожидание
            if (!IsSpawned)
            {
                LogMessage("Ожидание спавна объекта в сети...");
                float timeoutSeconds = 3f;
                float elapsedTime = 0f;
                while (!IsSpawned && elapsedTime < timeoutSeconds)
                {
                    await Task.Delay(100);
                    elapsedTime += 0.1f;
                }

                if (!IsSpawned)
                {
                    // ИСПРАВЛЕНО: не запускаем инициализацию здесь повторно, а помечаем как отложенную
                    LogWarning($"Таймаут ожидания спавна ({timeoutSeconds}s). Переносим инициализацию до OnNetworkSpawn.");
                    initDeferred = true;
                    return true; // не проваливаем всю цепочку менеджера
                }
            }

            if (initCompleted) return true; // уже инициализировано

            // Основная инициализация (единожды)
            try
            {
                initRunning = true;
                bool ok = await initHelper.FullInitializeAsync(provider);
                initCompleted = ok;
                return ok;
            }
            finally
            {
                initRunning = false;
            }
        }

        /// <summary>
        /// Синхронная инициализация пост-зависимостей
        /// </summary>
        public bool InitializePostDependenciesSync(SystemProvider provider)
        {
            InitializeHelper();
            return initHelper.InitializePostDependenciesSync(provider);
        }

        /// <summary>
        /// Получить информацию о зависимостях
        /// </summary>
        public DependencyInfo[] GetDependencies()
        {
            InitializeHelper();
            return initHelper.GetDependencies();
        }

        #region Сетевые вспомогательные методы

        /// <summary>
        /// Инициализация только на сервере
        /// </summary>
        protected async Task<bool> InitializeServerOnlyAsync(Func<Task<bool>> initFunc)
        {
            if (!IsServer)
            {
                LogMessage("Пропуск инициализации - не сервер");
                return true;
            }

            return await initFunc();
        }

        /// <summary>
        /// Инициализация только на клиенте
        /// </summary>
        protected async Task<bool> InitializeClientOnlyAsync(Func<Task<bool>> initFunc)
        {
            if (!IsClient || IsHost)
            {
                LogMessage("Пропуск инициализации - не клиент");
                return true;
            }

            return await initFunc();
        }

        /// <summary>
        /// Инициализация только для владельца объекта
        /// </summary>
        protected async Task<bool> InitializeOwnerOnlyAsync(Func<Task<bool>> initFunc)
        {
            if (!IsOwner)
            {
                LogMessage("Пропуск инициализации - не владелец");
                return true;
            }

            return await initFunc();
        }

        #endregion

        #region Вспомогательные методы логирования

        protected void ReportProgress(float progress)
        {
            initHelper?.ReportProgress(progress);
        }

        protected void LogMessage(string message)
        {
            if (verboseLogging)
            {
                string networkInfo = $"[{(IsServer ? "S" : "")}{(IsClient ? "C" : "")}{(IsOwner ? "O" : "")}]";
                Debug.Log($"[{SystemId}]{networkInfo} {message}");
            }
        }

        protected void LogError(string message)
        {
            string networkInfo = $"[{(IsServer ? "S" : "")}{(IsClient ? "C" : "")}{(IsOwner ? "O" : "")}]";
            Debug.LogError($"[{SystemId}]{networkInfo} {message}");
        }

        protected void LogWarning(string message)
        {
            string networkInfo = $"[{(IsServer ? "S" : "")}{(IsClient ? "C" : "")}{(IsOwner ? "O" : "")}]";
            Debug.LogWarning($"[{SystemId}]{networkInfo} {message}");
        }

        protected void LogMessageInitSystemStart(string message)
        {
            LogMessage($"Начало инициализации системы {message}");
        }

        protected void LogMessageInitSystemEnd(string message)
        {
            LogMessage($"Система {message} завершена успешно");
        }

        #endregion
    }
}
