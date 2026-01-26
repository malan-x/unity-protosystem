using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using ProtoSystem;

namespace ProtoSystem.Examples
{
    /// <summary>
    /// Пример сетевой системы, использующей новую архитектуру
    /// Наследуется от NetworkEventBus (который наследуется от NetworkBehaviour)
    /// </summary>
    public class ExampleNetworkSystem : NetworkEventBus, IInitializableSystem
    {
        /// <summary>
        /// Примерные константы событий для демонстрации
        /// </summary>
        private static class ExampleNetworkEvents
        {
            public const int PlayerConnected = 10001;
            public const int PlayerDisconnected = 10002;
            public const int DataSynchronized = 10003;
        }

        [Header("Система")]
        [SerializeField] private bool verboseLogging = false;

        /// <summary>
        /// Вспомогательный класс для инициализации
        /// </summary>
        private InitializationHelper initHelper;

        /// <summary>
        /// Пример зависимости от локальной системы
        /// </summary>
        [Dependency(required: true, description: "Нужна для управления временем")]
        // private GameTimeManager timeManager; // Раскомментируйте для реальной зависимости

        /// <summary>
        /// Пример зависимости от другой сетевой системы
        /// </summary>
        [PostDependency(required: false, description: "Для синхронизации с другими игроками")]
        private NetworkManager networkManager;

        #region IInitializableSystem Implementation

        public string SystemId => "example_network_system";
        public string DisplayName => "Пример сетевой системы";
        public string Description => "Демонстрирует создание сетевой системы через NetworkEventBus + IInitializableSystem.";

        public bool IsInitializedDependencies => initHelper?.IsInitializedDependencies ?? false;
        public bool IsInitializedPostDependencies => initHelper?.IsInitializedPostDependencies ?? false;

        public InitializationStatus Status => initHelper?.Status ?? InitializationStatus.NotStarted;

        public event Action<string, float> OnProgressChanged;
        public event Action<string, InitializationStatus> OnStatusChanged;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Инициализируем помощника при спавне в сети
            InitializeHelper();
        }

        private void InitializeHelper()
        {
            if (initHelper == null)
            {
                initHelper = new InitializationHelper(this, this, verboseLogging);
                initHelper.OnProgressChanged += (id, progress) => OnProgressChanged?.Invoke(id, progress);
                initHelper.OnStatusChanged += (id, status) => OnStatusChanged?.Invoke(id, status);
            }
        }

        public void InitializeDependencies(SystemProvider provider)
        {
            InitializeHelper();
            initHelper.InitializeDependencies(provider);
        }

        public void InitializePostDependencies(SystemProvider provider)
        {
            InitializeHelper();
            initHelper.InitializePostDependencies(provider);
        }

        public async Task<bool> InitializeAsync()
        {
            LogMessage("Начало инициализации сетевой системы");
            initHelper.ReportProgress(0.1f);

            // Проверяем сетевое состояние
            if (!IsSpawned)
            {
                LogWarning("Система еще не заспавнена в сети!");
                return false;
            }

            // Разная инициализация для сервера и клиента
            if (IsServer)
            {
                await InitializeServerSide();
            }
            else if (IsClient)
            {
                await InitializeClientSide();
            }

            initHelper.ReportProgress(0.5f);

            // Инициализация общих компонентов
            InitializeCommonComponents();

            initHelper.ReportProgress(1.0f);
            LogMessage("Инициализация сетевой системы завершена");

            return true;
        }

        public async Task<bool> FullInitializeAsync(SystemProvider provider)
        {
            InitializeHelper();
            return await initHelper.FullInitializeAsync(provider);
        }

        public bool InitializePostDependenciesSync(SystemProvider provider)
        {
            InitializeHelper();
            return initHelper.InitializePostDependenciesSync(provider);
        }

        public DependencyInfo[] GetDependencies()
        {
            InitializeHelper();
            return initHelper.GetDependencies();
        }

        #endregion

        #region EventBus Implementation

        /// <summary>
        /// Инициализация событий (от NetworkEventBus)
        /// </summary>
        protected override void InitEvents()
        {
            // Подписываемся на сетевые события (используем простые константы для примера)
            AddEvent(ExampleNetworkEvents.PlayerConnected, OnPlayerConnected);
            AddEvent(ExampleNetworkEvents.PlayerDisconnected, OnPlayerDisconnected);
            AddEvent(ExampleNetworkEvents.DataSynchronized, OnDataSynchronized);
        }

        private void OnPlayerConnected(object data)
        {
            if (IsServer)
            {
                LogMessage($"Игрок подключился: {data}");
                // Синхронизируем состояние для нового игрока
                SyncStateForNewPlayer();
            }
        }

        private void OnPlayerDisconnected(object data)
        {
            if (IsServer)
            {
                LogMessage($"Игрок отключился: {data}");
            }
        }

        private void OnDataSynchronized(object data)
        {
            LogMessage($"Данные синхронизированы: {data}");
        }

        #endregion

        #region Network Methods

        private async Task InitializeServerSide()
        {
            LogMessage("Инициализация серверной части");
            await Task.Delay(100);

            // Серверная логика
            // if (timeManager != null)
            // {
            //     LogMessage($"Сервер: текущий игровой день {timeManager.CurrentDay}");
            // }
        }

        private async Task InitializeClientSide()
        {
            LogMessage("Инициализация клиентской части");
            await Task.Delay(100);

            // Клиентская логика
            RequestServerState();
        }

        private void InitializeCommonComponents()
        {
            LogMessage("Инициализация общих компонентов");
        }

        private void SyncStateForNewPlayer()
        {
            // Отправляем состояние новому игроку
            PublishEventServerOnly(ExampleNetworkEvents.DataSynchronized, "InitialState");
        }

        private void RequestServerState()
        {
            // Запрашиваем состояние у сервера
            PublishEventClientOnly(ExampleNetworkEvents.DataSynchronized, "RequestState");
        }

        #endregion

        #region RPC Methods

        [ServerRpc]
        private void RequestStateServerRpc()
        {
            LogMessage("Получен запрос состояния от клиента");
            SendStateClientRpc();
        }

        [ClientRpc]
        private void SendStateClientRpc()
        {
            LogMessage("Получено состояние от сервера");
        }

        #endregion

        #region Utility Methods

        private void LogMessage(string message)
        {
            if (verboseLogging)
            {
                string networkInfo = $"[{(IsServer ? "S" : "")}{(IsClient ? "C" : "")}{(IsOwner ? "O" : "")}]";
                ProtoLogger.Log(SystemId, LogCategory.Runtime, LogLevel.Info, $"{networkInfo} {message}");
            }
        }

        private void LogWarning(string message)
        {
            string networkInfo = $"[{(IsServer ? "S" : "")}{(IsClient ? "C" : "")}{(IsOwner ? "O" : "")}]";
            ProtoLogger.LogWarning(SystemId, $"{networkInfo} {message}");
        }

        #endregion
    }
}
