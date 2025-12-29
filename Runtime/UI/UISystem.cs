// Packages/com.protosystem.core/Runtime/UI/UISystem.cs
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Главная система управления UI.
    /// Обеспечивает навигацию между окнами, диалоги, тосты и другие UI элементы.
    /// </summary>
    [ProtoSystemComponent("UI System", "Управление окнами, диалогами, тостами и тултипами", "UI", "🖼️", 10)]
    public class UISystem : InitializableSystemBase
    {
        public override string SystemId => "UISystem";
        public override string DisplayName => "UI System";

        [Header("Configuration")]
        [SerializeField] private UIWindowGraph windowGraph;
        [SerializeField] private UISystemConfig config;

        [Header("Canvas Settings")]
        [SerializeField] private bool createCanvas = true;
        [SerializeField] private int canvasSortOrder = 100;

        // Компоненты
        private Canvas _canvas;
        private CanvasScaler _canvasScaler;
        private UINavigator _navigator;
        private UIWindowFactory _factory;

        // Builders
        private DialogBuilder _dialogBuilder;
        private ToastBuilder _toastBuilder;
        private TooltipBuilder _tooltipBuilder;

        #region Singleton

        private static UISystem _instance;

        public static UISystem Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<UISystem>();
                return _instance;
            }
        }

        #endregion

        #region Public Properties

        /// <summary>Граф окон</summary>
        public UIWindowGraph Graph => windowGraph;
        
        /// <summary>Навигатор</summary>
        public UINavigator Navigator => _navigator;
        
        /// <summary>Текущее активное окно</summary>
        public UIWindowBase CurrentWindow => _navigator?.CurrentWindow;
        
        /// <summary>Есть ли модальное окно</summary>
        public bool HasModal => _navigator?.HasModal ?? false;
        
        /// <summary>Можно ли вернуться назад</summary>
        public bool CanGoBack => _navigator?.CanGoBack ?? false;

        /// <summary>Builder для диалогов</summary>
        public DialogBuilder Dialog => _dialogBuilder;
        
        /// <summary>Builder для тостов</summary>
        public ToastBuilder Toast => _toastBuilder;
        
        /// <summary>Builder для тултипов</summary>
        public TooltipBuilder Tooltip => _tooltipBuilder;

        /// <summary>Конфигурация системы (для внутреннего использования)</summary>
        internal UISystemConfig Config => config;

        #endregion

        #region Static API (shortcuts)

        /// <summary>Навигация по триггеру</summary>
        public static NavigationResult Navigate(string trigger)
            => Instance?._navigator?.Navigate(trigger) ?? NavigationResult.WindowNotFound;

        /// <summary>Открыть окно напрямую</summary>
        public static NavigationResult Open(string windowId, TransitionAnimation animation = TransitionAnimation.Fade)
            => Instance?._navigator?.Open(windowId, animation) ?? NavigationResult.WindowNotFound;

        /// <summary>Вернуться назад</summary>
        public static NavigationResult Back()
            => Instance?._navigator?.Back() ?? NavigationResult.StackEmpty;

        /// <summary>Сбросить навигацию к начальному окну</summary>
        public static void Reset()
            => Instance?._navigator?.Reset();

        #endregion

        #region Initialization

        protected override void Awake()
        {
            base.Awake();
            _instance = this;
        }

        protected override void InitEvents()
        {
            // Подписка на системные события
            AddEvent(EventBus.UI.BackPressed, OnBackPressed);
        }

        public override async Task<bool> InitializeAsync()
        {
            try
            {
                LogMessage("Initializing UI System...");

                // Создаём или находим Canvas
                if (!SetupCanvas())
                {
                    LogError("Failed to setup Canvas");
                    return false;
                }

                // Загружаем конфиг
                if (config == null)
                {
                    config = UISystemConfig.CreateDefault();
                    LogWarning("UISystemConfig not assigned, using defaults");
                }

                // Загружаем или создаём граф
                if (windowGraph == null)
                {
                    windowGraph = ScriptableObject.CreateInstance<UIWindowGraph>();
                    LogWarning("UIWindowGraph not assigned, using empty graph");
                }

                // Собираем атрибуты из кода
                CollectWindowAttributes();

                // Валидируем граф
                var validation = windowGraph.Validate();
                if (!validation.isValid)
                {
                    LogError($"Window graph validation failed:\n{validation}");
                    // Продолжаем работу, но с предупреждениями
                }
                else if (validation.warnings.Count > 0)
                {
                    LogWarning($"Window graph warnings:\n{validation}");
                }

                // Создаём фабрику и навигатор
                _factory = new UIWindowFactory(_canvas.transform);
                _navigator = new UINavigator(windowGraph, _factory);

                // Создаём builders
                _dialogBuilder = new DialogBuilder(this);
                _toastBuilder = new ToastBuilder(this);
                _tooltipBuilder = new TooltipBuilder(this);

                // Открываем стартовое окно
                if (!string.IsNullOrEmpty(windowGraph.startWindowId))
                {
                    _navigator.OpenStartWindow();
                }

                LogMessage("UI System initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize UI System: {ex.Message}");
                return false;
            }
        }

        private bool SetupCanvas()
        {
            if (!createCanvas)
            {
                _canvas = GetComponentInChildren<Canvas>();
                if (_canvas == null)
                {
                    LogError("Canvas not found and createCanvas is false");
                    return false;
                }
                return true;
            }

            // Создаём Canvas
            var canvasObj = new GameObject("UISystem_Canvas");
            canvasObj.transform.SetParent(transform, false);

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = canvasSortOrder;

            _canvasScaler = canvasObj.AddComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(1920, 1080);
            _canvasScaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            return true;
        }

        private void CollectWindowAttributes()
        {
            LogMessage("Collecting window attributes from code...");

            // Находим все типы с атрибутом UIWindow
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            int windowsFound = 0;
            int transitionsFound = 0;

            foreach (var assembly in assemblies)
            {
                // Пропускаем системные сборки
                if (assembly.FullName.StartsWith("System") || 
                    assembly.FullName.StartsWith("Unity") ||
                    assembly.FullName.StartsWith("mscorlib"))
                    continue;

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!typeof(UIWindowBase).IsAssignableFrom(type) || type.IsAbstract)
                            continue;

                        // Получаем атрибут окна
                        var windowAttr = (UIWindowAttribute)Attribute.GetCustomAttribute(type, typeof(UIWindowAttribute));
                        if (windowAttr == null)
                            continue;

                        // Проверяем, есть ли уже такое окно в графе
                        var existing = windowGraph.GetWindow(windowAttr.WindowId);
                        if (existing == null)
                        {
                            // Добавляем определение окна (без prefab - он должен быть в Inspector)
                            windowGraph.RegisterWindow(new WindowDefinition
                            {
                                id = windowAttr.WindowId,
                                type = windowAttr.Type,
                                layer = windowAttr.Layer,
                                pauseGame = windowAttr.PauseGame,
                                hideBelow = windowAttr.HideBelow,
                                allowBack = windowAttr.AllowBack,
                                fromCode = true
                            });
                            windowsFound++;
                        }

                        // Получаем атрибуты переходов
                        var transitionAttrs = (UITransitionAttribute[])Attribute.GetCustomAttributes(type, typeof(UITransitionAttribute));
                        foreach (var transAttr in transitionAttrs)
                        {
                            windowGraph.RegisterTransition(new TransitionDefinition
                            {
                                fromWindowId = windowAttr.WindowId,
                                toWindowId = transAttr.ToWindowId,
                                trigger = transAttr.Trigger,
                                animation = transAttr.Animation,
                                fromCode = true
                            });
                            transitionsFound++;
                        }

                        // Глобальные переходы
                        var globalAttrs = (UIGlobalTransitionAttribute[])Attribute.GetCustomAttributes(type, typeof(UIGlobalTransitionAttribute));
                        foreach (var globalAttr in globalAttrs)
                        {
                            windowGraph.RegisterTransition(new TransitionDefinition
                            {
                                fromWindowId = "", // Глобальный
                                toWindowId = globalAttr.ToWindowId,
                                trigger = globalAttr.Trigger,
                                animation = globalAttr.Animation,
                                fromCode = true
                            });
                            transitionsFound++;
                        }
                    }
                }
                catch (Exception)
                {
                    // Игнорируем ошибки рефлексии
                }
            }

            LogMessage($"Found {windowsFound} windows and {transitionsFound} transitions from attributes");
        }

        #endregion

        #region Event Handlers

        private void OnBackPressed(object data)
        {
            if (CanGoBack)
                Back();
        }

        private void Update()
        {
            // Обработка Escape для Back
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EventBus.Publish(EventBus.UI.BackPressed, null);
            }
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            _factory?.ClearPool();
            
            if (_instance == this)
                _instance = null;
        }

        #endregion
    }
}
