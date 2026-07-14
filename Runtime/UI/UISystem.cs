// Packages/com.protosystem.core/Runtime/UI/UISystem.cs
using System;
using System.Linq;
using System.Collections.Generic;
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
        public override string Description => "Управляет UI окнами, навигацией между ними, диалогами, тостами и тултипами.";

        [Header("Configuration")]
        [SerializeField, InlineConfig] private UISystemConfig config;
        
        [Header("Scene Initializer (optional)")]
        [Tooltip("Скрипт инициализации UI для этой сцены. Определяет какие окна открывать и как.")]
        [SerializeField] private MonoBehaviour sceneInitializerComponent;
        
        [Header("Graph Override (optional)")]
        [Tooltip("Оставьте пустым для автогенерации из Config.windowPrefabs")]
        [SerializeField] private UIWindowGraph windowGraphOverride;

        [Header("Startup (if no Initializer)")]
        [Tooltip("Автоматически открыть стартовое окно при инициализации")]
        [SerializeField] private bool autoOpenStartWindow = true;
        
        [Tooltip("ID окна для открытия (если пусто — используется startWindowId из графа)")]
        [SerializeField] private string overrideStartWindowId;

        [Header("Canvas Settings")]
        [SerializeField] private bool createCanvas = true;
        [SerializeField] private int canvasSortOrder = 100;

        // Компоненты
        private UIWindowGraph _windowGraph;
        private Canvas _canvas;
        private CanvasScaler _canvasScaler;
        private UINavigator _navigator;
        private UIWindowFactory _factory;
        private IUISceneInitializer _sceneInitializer;

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
                    _instance = FindFirstObjectByType<UISystem>();
                return _instance;
            }
        }

        #endregion

        #region Public Properties

        /// <summary>Граф окон</summary>
        public UIWindowGraph Graph => _windowGraph;
        
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

        /// <summary>Конфигурация системы</summary>
        internal UISystemConfig Config => config;
        
        /// <summary>Canvas системы</summary>
        public Canvas Canvas => _canvas;

        /// <summary>Scene Initializer</summary>
        public IUISceneInitializer SceneInitializer => _sceneInitializer;

        #endregion

        #region Static API

        /// <summary>Навигация по триггеру</summary>
        public static NavigationResult Navigate(string trigger)
            => Instance?._navigator?.Navigate(trigger) ?? NavigationResult.WindowNotFound;

        /// <summary>Навигация по триггеру с payload — окно получит его в OnPayload() до Show</summary>
        public static NavigationResult Navigate(string trigger, object payload)
            => Instance?._navigator?.Navigate(trigger, payload) ?? NavigationResult.WindowNotFound;

        /// <summary>Открыть окно напрямую по ID</summary>
        public static NavigationResult Open(string windowId, TransitionAnimation animation = TransitionAnimation.Fade)
            => Instance?._navigator?.Open(windowId, animation) ?? NavigationResult.WindowNotFound;

        /// <summary>Открыть окно по ID с payload — окно получит его в OnPayload() до Show</summary>
        public static NavigationResult Open(string windowId, object payload, TransitionAnimation animation = TransitionAnimation.Fade)
            => Instance?._navigator?.Open(windowId, animation, payload) ?? NavigationResult.WindowNotFound;

        /// <summary>Вернуться назад</summary>
        public static NavigationResult Back()
            => Instance?._navigator?.Back() ?? NavigationResult.StackEmpty;

        /// <summary>Закрыть модалки и окна сверху стека, пока верхним не станет windowId</summary>
        public static NavigationResult BackTo(string windowId)
            => Instance?._navigator?.BackTo(windowId) ?? NavigationResult.WindowNotFound;

        /// <summary>Сбросить навигацию к начальному окну</summary>
        public static void Reset()
            => Instance?._navigator?.Reset();

        /// <summary>Закрыть все окна</summary>
        public static void CloseAll()
        {
            Instance?._navigator?.Reset();
        }

        #endregion

        #region Initialization

        protected override void Awake()
        {
            base.Awake();
            _instance = this;
            
            // Получаем инициализатор если назначен
            if (sceneInitializerComponent != null)
            {
                _sceneInitializer = sceneInitializerComponent as IUISceneInitializer;
                if (_sceneInitializer == null)
                {
                    LogWarning($"{sceneInitializerComponent.name} does not implement IUISceneInitializer");
                }
                else
                {
                    LogInit($"SceneInitializer assigned: {sceneInitializerComponent.GetType().Name}");
                }
            }
            else
            {
                LogWarning("No sceneInitializerComponent assigned in Inspector!");
            }
        }

        protected override void InitEvents()
        {
            AddEvent(EventBus.UI.BackPressed, OnBackPressed);
        }

        public override async Task<bool> InitializeAsync()
        {
            try
            {
                LogMessage("Initializing UI System...");

                // Сбрасываем UITimeManager при старте
                UITimeManager.Instance.Reset();
                LogMessage("UITimeManager reset");

                // Создаём Canvas
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

                // Загружаем или строим граф
                _windowGraph = BuildOrLoadGraph();

                if (_windowGraph == null)
                {
                    LogWarning("UIWindowGraph not available, creating empty");
                    _windowGraph = ScriptableObject.CreateInstance<UIWindowGraph>();
                }
                else
                {
                    LogMessage($"Graph ready: {_windowGraph.windowCount} windows, {_windowGraph.transitionCount} transitions");
                }

                // Создаём фабрику и навигатор
                _factory = new UIWindowFactory(_canvas.transform, config);
                _navigator = new UINavigator(_windowGraph, _factory);

                // Создаём builders
                _dialogBuilder = new DialogBuilder(this);
                _toastBuilder = new ToastBuilder(this);
                _tooltipBuilder = new TooltipBuilder(this);

                // Инициализация через SceneInitializer или стандартный flow
                if (_sceneInitializer != null)
                {
                    LogMessage($"Using SceneInitializer: {sceneInitializerComponent.GetType().Name}");
                    _sceneInitializer.Initialize(this);
                }
                else if (autoOpenStartWindow)
                {
                    OpenStartWindow();
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

        /// <summary>
        /// Построить граф из Config.windowPrefabs или загрузить override/Resources
        /// </summary>
        private UIWindowGraph BuildOrLoadGraph()
        {
            LogInit($"BuildOrLoadGraph: config={config != null}, prefabs={config?.windowPrefabs?.Count ?? 0}, override={windowGraphOverride != null}");

            // 1. Приоритет: строим из prefab'ов в Config (всегда актуальные ссылки)
            if (config != null && config.windowPrefabs != null && config.windowPrefabs.Count > 0)
            {
                LogMessage($"Building graph from {config.windowPrefabs.Count} prefabs in Config");
                return BuildGraphFromPrefabs();
            }

            // 2. Fallback: явно указанный override (может содержать устаревшие ссылки!)
            if (windowGraphOverride != null)
            {
                LogMessage("Using windowGraphOverride (WARNING: may have stale prefab references)");
                return windowGraphOverride;
            }

            // 3. Fallback: загружаем из Resources
            var resourceGraph = UIWindowGraph.Instance;
            if (resourceGraph != null)
            {
                LogMessage("Loaded graph from Resources (no SceneInitializer transitions!)");
                return resourceGraph;
            }

            return null;
        }

        /// <summary>
        /// Построить граф на основе prefab'ов из Config + дополнительных переходов из Initializer
        /// </summary>
        private UIWindowGraph BuildGraphFromPrefabs()
        {
            LogInit($"BuildGraphFromPrefabs: _sceneInitializer = {(_sceneInitializer != null ? _sceneInitializer.GetType().Name : "NULL")}");

            var graph = ScriptableObject.CreateInstance<UIWindowGraph>();
            graph.ClearForRebuild();

            // Диагностика prefabs
            int totalPrefabs = config.windowPrefabs?.Count ?? 0;
            int nullPrefabs = config.windowPrefabs?.Count(p => p == null) ?? 0;
            LogInit($"Config has {totalPrefabs} prefabs, {nullPrefabs} are NULL");

            if (nullPrefabs > 0)
            {
                LogWarning($"{nullPrefabs} prefabs are NULL! Open UISystemConfig and click 'Scan & Add Prefabs'");
            }

            // Собираем кандидатов: один WindowId может иметь ДВА префаба (uGUI и UI Toolkit),
            // выбор — по config.preferredBackend
            var candidateOrder = new List<string>();
            var candidates = new Dictionary<string, List<(GameObject prefab, UIWindowBase component)>>();

            foreach (var prefab in config.windowPrefabs)
            {
                if (prefab == null) continue;

                var component = prefab.GetComponent<UIWindowBase>();
                if (component == null)
                {
                    LogWarning($"Prefab '{prefab.name}' has no UIWindowBase component");
                    continue;
                }

                var attr = (UIWindowAttribute)Attribute.GetCustomAttribute(component.GetType(), typeof(UIWindowAttribute));
                if (attr == null)
                {
                    LogWarning($"{component.GetType().Name} has no [UIWindow] attribute");
                    continue;
                }

                if (!candidates.TryGetValue(attr.WindowId, out var list))
                {
                    list = new List<(GameObject, UIWindowBase)>();
                    candidates[attr.WindowId] = list;
                    candidateOrder.Add(attr.WindowId);
                }
                list.Add((prefab, component));
            }

            foreach (var windowId in candidateOrder)
            {
                var list = candidates[windowId];
                var (prefab, windowComponent) = list[0];

                if (list.Count > 1)
                {
                    bool preferToolkit = config.preferredBackend == UIBackendPreference.UIToolkit;
                    foreach (var candidate in list)
                    {
                        bool isToolkit = candidate.component is UIToolkitWindowBase;
                        if (isToolkit == preferToolkit)
                        {
                            (prefab, windowComponent) = candidate;
                            break;
                        }
                    }
                    LogInit($"Window '{windowId}': {list.Count} prefabs, chose '{prefab.name}' (preferredBackend={config.preferredBackend})");
                }

                // Получаем атрибуты выбранного класса
                var type = windowComponent.GetType();
                var windowAttr = (UIWindowAttribute)Attribute.GetCustomAttribute(type, typeof(UIWindowAttribute));

                LogInit($"Adding window '{windowAttr.WindowId}' from prefab '{prefab.name}'");

                // Добавляем окно
                graph.AddWindow(new WindowDefinition
                {
                    id = windowAttr.WindowId,
                    prefab = prefab,
                    type = windowAttr.Type,
                    layer = windowAttr.Layer,
                    level = windowAttr.Level,
                    pauseGame = windowAttr.PauseGame,
                    cursorMode = windowAttr.CursorMode,
                    hideBelow = windowAttr.HideBelow,
                    allowBack = windowAttr.AllowBack,
                    typeName = type.FullName
                });

                // Добавляем переходы из атрибутов
                var transitionAttrs = (UITransitionAttribute[])Attribute.GetCustomAttributes(type, typeof(UITransitionAttribute));
                foreach (var trans in transitionAttrs)
                {
                    graph.AddTransition(new TransitionDefinition
                    {
                        fromWindowId = windowAttr.WindowId,
                        toWindowId = trans.ToWindowId,
                        trigger = trans.Trigger,
                        animation = trans.Animation
                    });
                }

                // Глобальные переходы
                var globalAttrs = (UIGlobalTransitionAttribute[])Attribute.GetCustomAttributes(type, typeof(UIGlobalTransitionAttribute));
                foreach (var global in globalAttrs)
                {
                    graph.AddTransition(new TransitionDefinition
                    {
                        fromWindowId = "", // глобальный (пустой = глобальный в AddTransition)
                        toWindowId = global.ToWindowId,
                        trigger = global.Trigger,
                        animation = global.Animation
                    });
                }
            }

            // Добавляем переходы из SceneInitializer (с приоритетом над атрибутами)
            if (_sceneInitializer != null)
            {
                LogInit($"Adding transitions from SceneInitializer: {sceneInitializerComponent?.GetType().Name}");

                var additionalTransitions = _sceneInitializer.GetAdditionalTransitions();
                int count = 0;
                foreach (var trans in additionalTransitions)
                {
                    LogInit($"SceneInitializer transition: {trans.fromWindowId} --({trans.trigger})--> {trans.toWindowId}");
                    graph.AddTransition(new TransitionDefinition
                    {
                        fromWindowId = trans.fromWindowId == "*" ? "" : trans.fromWindowId,
                        toWindowId = trans.toWindowId,
                        trigger = trans.trigger,
                        animation = trans.animation
                    }, allowOverride: true); // Переопределяем существующие переходы
                    count++;
                }
                LogInit($"Added {count} transitions from SceneInitializer");

                // Устанавливаем стартовое окно из инициализатора
                if (!string.IsNullOrEmpty(_sceneInitializer.StartWindowId))
                {
                    graph.startWindowId = _sceneInitializer.StartWindowId;
                    LogInit($"Start window from initializer: {graph.startWindowId}");
                }
            }
            else
            {
                LogWarning("No SceneInitializer! Transitions from attributes only.");
            }

            graph.FinalizeBuild();
            return graph;
        }

        /// <summary>
        /// Открыть стартовое окно (можно вызвать вручную если autoOpenStartWindow = false)
        /// </summary>
        public void OpenStartWindow()
        {
            // Приоритет: overrideStartWindowId → initializer → graph.startWindowId
            string windowId = overrideStartWindowId;
            
            if (string.IsNullOrEmpty(windowId) && _sceneInitializer != null)
                windowId = _sceneInitializer.StartWindowId;
            
            if (string.IsNullOrEmpty(windowId))
                windowId = _windowGraph?.startWindowId;

            if (string.IsNullOrEmpty(windowId))
            {
                LogWarning("No start window configured");
                return;
            }

            var window = _windowGraph?.GetWindow(windowId);
            if (window == null || window.prefab == null)
            {
                LogWarning($"Start window '{windowId}' not found or has no prefab");
                return;
            }

            _navigator.Open(windowId);
            LogMessage($"Opened start window: {windowId}");
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

        #endregion

        #region Event Handlers

        private int _lastBackFrame = -1;

        private void OnBackPressed(object data)
        {
            // Back обрабатываем не чаще раза за кадр: один Escape приходит из двух источников —
            // Update() ниже (клавиша) и NavigationCancelEvent панели UI Toolkit (Esc/геймпад B).
            // Без этого первый вызов закрывал окно, а второй в том же кадре доставался окну
            // под ним (Esc в базе закрывал базу и тут же открывал паузу на глобальной карте).
            if (_lastBackFrame == Time.frameCount) return;
            _lastBackFrame = Time.frameCount;

            // Give the active window a chance to handle Back/Escape.
            // This enables window-specific behavior (e.g. GameHUD -> open PauseMenu) and prevents
            // generic stack back from overriding custom flows.
            var modal = Navigator?.CurrentModal;
            if (modal != null)
            {
                if (modal.AllowBack)
                    modal.OnBackPressed();
                return;
            }

            var current = Navigator?.CurrentWindow;
            if (current != null)
            {
                if (current.AllowBack)
                    current.OnBackPressed();
                return;
            }

            if (CanGoBack)
                Back();
        }

                                        private void Update()
                                        {
                                            // Проверка Escape для навигации назад
                                            bool escapePressed = false;

                                #if PROTO_HAS_INPUT_SYSTEM
                                            // Новый Input System (только если пакет установлен)
                                            if (UnityEngine.InputSystem.Keyboard.current != null)
                                            {
                                                escapePressed = UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
                                            }
                                #else
                                            // Legacy Input Manager (fallback)
                                            escapePressed = Input.GetKeyDown(KeyCode.Escape);
                                #endif

                                            if (escapePressed)
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
