// Packages/com.protosystem.core/Runtime/Cursor/CursorManager.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ProtoSystem.Cursor
{
    /// <summary>
    /// Система управления курсором.
    /// Поддерживает режимы Lock/Confine/Free и стек состояний.
    /// </summary>
    [ProtoSystemComponent("Cursor Manager", "Управление курсором (Lock/Confine/Free)", "UI", "🖱️", 25)]
    public class CursorManager : InitializableSystemBase
    {
        public override string SystemId => "CursorManager";
        public override string DisplayName => "Cursor Manager";

        [Header("Configuration")]
        [SerializeField] private CursorConfig config;

        [Header("Default State")]
        [SerializeField] private CursorMode defaultMode = CursorMode.Free;
        [SerializeField] private bool defaultVisible = true;

        // Стек состояний курсора
        private readonly Stack<CursorState> _stateStack = new();
        
        // Текущее состояние
        private CursorState _currentState;

        // Кастомные курсоры
        private Dictionary<string, CursorData> _customCursors = new();

        #region Singleton

        private static CursorManager _instance;
        public static CursorManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<CursorManager>();
                return _instance;
            }
        }

        #endregion

        #region Properties

        /// <summary>Текущий режим курсора</summary>
        public CursorMode CurrentMode => _currentState.Mode;

        /// <summary>Виден ли курсор</summary>
        public bool IsVisible => _currentState.Visible;

        /// <summary>Заблокирован ли курсор</summary>
        public bool IsLocked => _currentState.Mode == CursorMode.Locked;

        #endregion

        #region Static API

        /// <summary>Установить режим курсора</summary>
        public static void SetMode(CursorMode mode)
            => Instance?.SetCursorMode(mode);

        /// <summary>Показать курсор</summary>
        public static void Show()
            => Instance?.SetVisible(true);

        /// <summary>Скрыть курсор</summary>
        public static void Hide()
            => Instance?.SetVisible(false);

        /// <summary>Заблокировать курсор (для FPS)</summary>
        public static void Lock()
            => Instance?.SetCursorMode(CursorMode.Locked);

        /// <summary>Освободить курсор</summary>
        public static void Free()
            => Instance?.SetCursorMode(CursorMode.Free);

        /// <summary>Ограничить курсор окном</summary>
        public static void Confine()
            => Instance?.SetCursorMode(CursorMode.Confined);

        /// <summary>Push состояния (для временного изменения)</summary>
        public static void PushState(CursorMode mode, bool visible)
            => Instance?.PushCursorState(mode, visible);

        /// <summary>Pop состояния (вернуть предыдущее)</summary>
        public static void PopState()
            => Instance?.PopCursorState();

        /// <summary>Установить кастомный курсор</summary>
        public static void SetCursor(string cursorId)
            => Instance?.SetCustomCursor(cursorId);

        /// <summary>Сбросить на стандартный курсор</summary>
        public static void ResetCursor()
            => Instance?.SetDefaultCursor();

        #endregion

        #region Initialization

        protected override void Awake()
        {
            base.Awake();
            _instance = this;
        }

        protected override void InitEvents()
        {
            // Подписка на UI события для автоматического управления курсором
            AddEvent(EventBus.UI.WindowOpened, OnWindowOpened);
            AddEvent(EventBus.UI.WindowClosed, OnWindowClosed);
        }

        public override Task<bool> InitializeAsync()
        {
            LogMessage("Initializing Cursor Manager...");

            // Загружаем конфиг
            if (config == null)
            {
                config = CursorConfig.CreateDefault();
            }

            // Регистрируем кастомные курсоры
            if (config.customCursors != null)
            {
                foreach (var cursor in config.customCursors)
                {
                    _customCursors[cursor.id] = cursor;
                }
            }

            // Устанавливаем начальное состояние
            _currentState = new CursorState
            {
                Mode = defaultMode,
                Visible = defaultVisible,
                CursorId = "default"
            };

            ApplyState(_currentState);

            LogMessage("Cursor Manager initialized");
            return Task.FromResult(true);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Установить режим курсора
        /// </summary>
        public void SetCursorMode(CursorMode mode)
        {
            _currentState.Mode = mode;
            ApplyState(_currentState);

            EventBus.Publish(EventBus.Cursor.ModeChanged, new CursorEventData
            {
                Mode = mode,
                Visible = _currentState.Visible
            });
        }

        /// <summary>
        /// Установить видимость курсора
        /// </summary>
        public void SetVisible(bool visible)
        {
            _currentState.Visible = visible;
            ApplyState(_currentState);

            EventBus.Publish(EventBus.Cursor.VisibilityChanged, new CursorEventData
            {
                Mode = _currentState.Mode,
                Visible = visible
            });
        }

        /// <summary>
        /// Push состояния курсора в стек
        /// </summary>
        public void PushCursorState(CursorMode mode, bool visible)
        {
            // Сохраняем текущее состояние
            _stateStack.Push(_currentState);

            // Применяем новое
            _currentState = new CursorState
            {
                Mode = mode,
                Visible = visible,
                CursorId = _currentState.CursorId
            };

            ApplyState(_currentState);
            
            LogMessage($"Cursor state pushed. Stack depth: {_stateStack.Count}");
        }

        /// <summary>
        /// Pop состояния курсора из стека
        /// </summary>
        public void PopCursorState()
        {
            if (_stateStack.Count == 0)
            {
                LogWarning("Cursor state stack is empty");
                return;
            }

            _currentState = _stateStack.Pop();
            ApplyState(_currentState);
            
            LogMessage($"Cursor state popped. Stack depth: {_stateStack.Count}");
        }

        /// <summary>
        /// Очистить стек состояний
        /// </summary>
        public void ClearStateStack()
        {
            _stateStack.Clear();
            
            _currentState = new CursorState
            {
                Mode = defaultMode,
                Visible = defaultVisible,
                CursorId = "default"
            };
            
            ApplyState(_currentState);
        }

        /// <summary>
        /// Установить кастомный курсор
        /// </summary>
        public void SetCustomCursor(string cursorId)
        {
            if (!_customCursors.TryGetValue(cursorId, out var cursorData))
            {
                LogWarning($"Custom cursor '{cursorId}' not found");
                return;
            }

            _currentState.CursorId = cursorId;
            UnityEngine.Cursor.SetCursor(cursorData.texture, cursorData.hotspot, UnityEngine.CursorMode.Auto);

            EventBus.Publish(EventBus.Cursor.CursorChanged, new CursorEventData
            {
                CursorId = cursorId
            });
        }

        /// <summary>
        /// Сбросить на стандартный курсор
        /// </summary>
        public void SetDefaultCursor()
        {
            _currentState.CursorId = "default";
            UnityEngine.Cursor.SetCursor(null, Vector2.zero, UnityEngine.CursorMode.Auto);

            EventBus.Publish(EventBus.Cursor.CursorChanged, new CursorEventData
            {
                CursorId = "default"
            });
        }

        /// <summary>
        /// Зарегистрировать кастомный курсор в runtime
        /// </summary>
        public void RegisterCursor(string id, Texture2D texture, Vector2 hotspot)
        {
            _customCursors[id] = new CursorData
            {
                id = id,
                texture = texture,
                hotspot = hotspot
            };
        }

        #endregion

        #region Event Handlers

        private void OnWindowOpened(object data)
        {
            if (!config.autoManageForUI) return;

            var windowData = (UI.WindowEventData)data;
            
            // При открытии UI окна — показываем и освобождаем курсор
            if (windowData.Type == UI.WindowType.Modal || windowData.Layer >= UI.WindowLayer.Windows)
            {
                PushCursorState(CursorMode.Free, true);
            }
        }

        private void OnWindowClosed(object data)
        {
            if (!config.autoManageForUI) return;

            var windowData = (UI.WindowEventData)data;
            
            // При закрытии — возвращаем предыдущее состояние
            if (windowData.Type == UI.WindowType.Modal || windowData.Layer >= UI.WindowLayer.Windows)
            {
                PopCursorState();
            }
        }

        #endregion

        #region Private Methods

        private void ApplyState(CursorState state)
        {
            // Видимость
            UnityEngine.Cursor.visible = state.Visible;

            // Режим
            UnityEngine.Cursor.lockState = state.Mode switch
            {
                CursorMode.Locked => CursorLockMode.Locked,
                CursorMode.Confined => CursorLockMode.Confined,
                _ => CursorLockMode.None
            };
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Восстанавливаем состояние при возврате фокуса
            if (hasFocus)
            {
                ApplyState(_currentState);
            }
        }

        #endregion

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }

    /// <summary>
    /// Режим курсора
    /// </summary>
    public enum CursorMode
    {
        /// <summary>Свободный курсор</summary>
        Free,
        /// <summary>Заблокирован в центре (для FPS)</summary>
        Locked,
        /// <summary>Ограничен окном игры</summary>
        Confined
    }

    /// <summary>
    /// Состояние курсора
    /// </summary>
    public struct CursorState
    {
        public CursorMode Mode;
        public bool Visible;
        public string CursorId;
    }

    /// <summary>
    /// Данные события курсора
    /// </summary>
    public struct CursorEventData
    {
        public CursorMode Mode;
        public bool Visible;
        public string CursorId;
    }
}

namespace ProtoSystem
{
    public static partial class EventBus
    {
        public static partial class Cursor
        {
            /// <summary>Режим курсора изменён</summary>
            public const int ModeChanged = 10400;
            /// <summary>Видимость курсора изменена</summary>
            public const int VisibilityChanged = 10401;
            /// <summary>Курсор изменён</summary>
            public const int CursorChanged = 10402;
        }
    }
}
