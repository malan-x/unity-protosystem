using System;
using System.Threading.Tasks;
using UnityEngine;
using ProtoSystem;
using ProtoSystem.UI;

namespace ProtoSystem.Examples
{
    /// <summary>
    /// Пример локальной системы, использующей новую архитектуру через интерфейс
    /// Наследуется от MonoEventBus и реализует IInitializableSystem
    /// </summary>
    public class ExampleLocalSystem : MonoEventBus, IInitializableSystem
    {
        /// <summary>
        /// Примерные константы событий для демонстрации
        /// </summary>
        private static class ExampleLocalEvents
        {
            public const int SystemInitialized = 20001;
            public const int StaminaStateChanged = 20002;
        }

        [Header("Система")]
        [SerializeField] private bool verboseLogging = false;

        /// <summary>
        /// Вспомогательный класс для инициализации
        /// </summary>
        private InitializationHelper initHelper;

        /// <summary>
        /// Пример зависимости от другой системы
        /// </summary>
        [Dependency(required: true, description: "Нужна для получения текущего времени")]
        // private GameTimeManager timeManager; // Раскомментируйте для реальной зависимости

        /// <summary>
        /// Пример пост-зависимости (опциональная)
        /// </summary>
        [PostDependency(required: false, description: "Для отображения UI")]
        private MonoBehaviour uiSystem;

        #region IInitializableSystem Implementation

        public string SystemId => "example_local_system";
        public string DisplayName => "Пример локальной системы";

        public bool IsInitializedDependencies => initHelper?.IsInitializedDependencies ?? false;
        public bool IsInitializedPostDependencies => initHelper?.IsInitializedPostDependencies ?? false;

        public InitializationStatus Status => initHelper?.Status ?? InitializationStatus.NotStarted;

        public event Action<string, float> OnProgressChanged;
        public event Action<string, InitializationStatus> OnStatusChanged;

        protected override void Awake()
        {
            base.Awake();

            // Инициализируем помощника
            initHelper = new InitializationHelper(this, this, verboseLogging);
            initHelper.OnProgressChanged += (id, progress) => OnProgressChanged?.Invoke(id, progress);
            initHelper.OnStatusChanged += (id, status) => OnStatusChanged?.Invoke(id, status);
        }

        public void InitializeDependencies(SystemProvider provider)
        {
            initHelper.InitializeDependencies(provider);
        }

        public void InitializePostDependencies(SystemProvider provider)
        {
            initHelper.InitializePostDependencies(provider);
        }

        public async Task<bool> InitializeAsync()
        {
            LogMessage("Начало инициализации примера локальной системы");
            initHelper.ReportProgress(0.1f);

            // Симуляция асинхронной инициализации
            await Task.Delay(100);

            // Проверяем что критические зависимости доступны
            // if (timeManager != null)
            // {
            //     LogMessage($"TimeManager доступен. Текущий день: {timeManager.CurrentDay}");
            // }

            initHelper.ReportProgress(0.5f);

            // Инициализация компонентов системы
            InitializeComponents();

            initHelper.ReportProgress(1.0f);
            LogMessage("Инициализация завершена успешно");

            return true;
        }

        public async Task<bool> FullInitializeAsync(SystemProvider provider)
        {
            return await initHelper.FullInitializeAsync(provider);
        }

        public bool InitializePostDependenciesSync(SystemProvider provider)
        {
            return initHelper.InitializePostDependenciesSync(provider);
        }

        public DependencyInfo[] GetDependencies()
        {
            return initHelper.GetDependencies();
        }

        #endregion

        #region EventBus Implementation

        /// <summary>
        /// Инициализация событий (от MonoEventBus)
        /// </summary>
        protected override void InitEvents()
        {
            // Подписываемся на события
            AddEvent(ExampleLocalEvents.SystemInitialized, OnSystemInitialized);
            AddEvent(ExampleLocalEvents.StaminaStateChanged, OnStaminaStateChanged);

            // UI Dialog events (works even if dialog was opened directly)
            AddEvent(EventBus.UI.DialogConfirmed, OnDialogConfirmed);
            AddEvent(EventBus.UI.DialogCancelled, OnDialogCancelled);
        }

        private void OnSystemInitialized(object data)
        {
            LogMessage($"Получено событие инициализации системы: {data}");
        }

        private void OnStaminaStateChanged(object data)
        {
            LogMessage($"Получено событие изменения выносливости: {data}");
        }

        private void OnDialogConfirmed(object data)
        {
            if (data is DialogEventData dialog)
            {
                LogMessage($"DialogConfirmed: {dialog.DialogId}, confirmed={dialog.Confirmed}, input='{dialog.InputValue}', index={dialog.SelectedIndex}");
            }
        }

        private void OnDialogCancelled(object data)
        {
            if (data is DialogEventData dialog)
            {
                LogMessage($"DialogCancelled: {dialog.DialogId}, confirmed={dialog.Confirmed}");
            }
        }

        #endregion

        #region Private Methods

        private void InitializeComponents()
        {
            // Инициализация внутренних компонентов
            LogMessage("Инициализация внутренних компонентов...");
        }

        private void LogMessage(string message)
        {
            if (verboseLogging)
                Debug.Log($"[{SystemId}] {message}");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Пример публичного метода системы
        /// </summary>
        public void DoSomething()
        {
            LogMessage("Выполнение действия в системе");

            // Отправляем событие
            EventBus.Publish(ExampleLocalEvents.SystemInitialized, SystemId);
        }

        [ContextMenu("ProtoSystem/Examples/Show Confirm Dialog")]
        public void ShowConfirmDialogExample()
        {
            var ui = UISystem.Instance;
            if (ui == null || ui.Dialog == null)
            {
                Debug.LogWarning("[ExampleLocalSystem] UISystem or DialogBuilder not ready. Ensure UISystem is present and initialized.");
                return;
            }

            ui.Dialog.Confirm(
                message: "Подтвердить тестовое действие?",
                onYes: () =>
                {
                    LogMessage("YES: подтверждено (пример действия)");
                    EventBus.Publish(ExampleLocalEvents.StaminaStateChanged, "YES clicked");
                },
                onNo: () =>
                {
                    LogMessage("NO: отменено (пример действия)");
                    EventBus.Publish(ExampleLocalEvents.StaminaStateChanged, "NO clicked");
                },
                title: "ConfirmDialog Example",
                yesText: "Да",
                noText: "Нет"
            );
        }

        #endregion
    }
}
