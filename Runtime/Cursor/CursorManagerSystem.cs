// Packages/com.protosystem.core/Runtime/Cursor/CursorManagerSystem.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ProtoSystem.Cursor
{
    /// <summary>
    /// Система управления курсором.
    /// Поддерживает режимы Lock/Confine/Free и стек состояний.
    /// Интегрируется с UI системой для автоматического управления.
    /// </summary>
    [ProtoSystemComponent("Cursor Manager", "Управление курсором (Lock/Confine/Free)", "UI", "🖱️", 25)]
    public class CursorManagerSystem : InitializableSystemBase
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

        // Fallback стек для случая когда Instance == null
        private static readonly Stack<(CursorLockMode lockState, bool visible)> _fallbackStack = new();

        #region Singleton

        private static CursorManagerSystem _instance;
        public static CursorManagerSystem Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<CursorManagerSystem>();
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

        /// <summary>Глубина стека состояний</summary>
        public int StateStackDepth => _stateStack.Count;

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

        /// <summary>Применить режим из WindowCursorMode (всегда использует статический стек)</summary>
        public static void ApplyWindowCursorMode(UI.WindowCursorMode mode, string windowId)
        {
            if (mode == UI.WindowCursorMode.Inherit) return;

            // Всегда используем статический стек для UI окон (чтобы работало до инициализации Instance)
            _fallbackStack.Push((UnityEngine.Cursor.lockState, UnityEngine.Cursor.visible));

            switch (mode)
            {
                case UI.WindowCursorMode.Visible:
                    UnityEngine.Cursor.visible = true;
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    break;
                case UI.WindowCursorMode.Locked:
                    UnityEngine.Cursor.visible = false;
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    break;
                case UI.WindowCursorMode.Confined:
                    UnityEngine.Cursor.visible = true;
                    UnityEngine.Cursor.lockState = CursorLockMode.Confined;
                    break;
            }
            
            Debug.Log($"[CursorManager] Window '{windowId}' applied {mode}. Stack depth: {_fallbackStack.Count}");
        }

        /// <summary>Восстановить режим курсора (всегда использует статический стек)</summary>
        public static void RestoreWindowCursorMode(UI.WindowCursorMode mode, string windowId)
        {
            if (mode == UI.WindowCursorMode.Inherit) return;

            if (_fallbackStack.Count > 0)
            {
                _fallbackStack.Pop(); // Убираем из стека, но не применяем - применит ForceApply
                Debug.Log($"[CursorManager] Window '{windowId}' popped from stack. Stack depth: {_fallbackStack.Count}");
            }
            else
            {
                Debug.LogWarning($"[CursorManager] Window '{windowId}' tried to restore but stack is empty!");
            }
        }

        /// <summary>Принудительно применить режим курсора (без push в стек)</summary>
        public static void ForceApplyCursorMode(UI.WindowCursorMode mode)
        {
            if (mode == UI.WindowCursorMode.Inherit) return;

            switch (mode)
            {
                case UI.WindowCursorMode.Visible:
                    UnityEngine.Cursor.visible = true;
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    break;
                case UI.WindowCursorMode.Locked:
                    UnityEngine.Cursor.visible = false;
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    break;
                case UI.WindowCursorMode.Confined:
                    UnityEngine.Cursor.visible = true;
                    UnityEngine.Cursor.lockState = CursorLockMode.Confined;
                    break;
            }
            
            Debug.Log($"[CursorManager] Force applied {mode}: lockState={UnityEngine.Cursor.lockState}, visible={UnityEngine.Cursor.visible}");
        }

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
            // Убрана автоматика - теперь управляется через UINavigator
        }

        public override Task<bool> InitializeAsync()
        {
            LogMessage("Initializing Cursor Manager System...");

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

            LogMessage("Cursor Manager System initialized");
            return Task.FromResult(true);
        }

        #endregion

        #region Window Cursor Mode Integration

        /// <summary>
        /// Применить режим курсора из UI окна
        /// </summary>
        public void ApplyWindowCursor(UI.WindowCursorMode windowMode, string windowId)
        {
            if (windowMode == UI.WindowCursorMode.Inherit)
            {
                // Не меняем состояние
                LogMessage($"Window '{windowId}' uses Inherit cursor mode - no change");
                return;
            }

            // Конвертируем WindowCursorMode в CursorMode
            var (mode, visible) = ConvertWindowCursorMode(windowMode);
            
            // Push в стек с новым режимом
            PushCursorState(mode, visible);
            LogMessage($"Window '{windowId}' applied cursor mode: {windowMode} -> {mode}, visible={visible}");
        }

        /// <summary>
        /// Восстановить режим курсора при закрытии окна
        /// </summary>
        public void RestoreWindowCursor(UI.WindowCursorMode windowMode, string windowId)
        {
            if (windowMode == UI.WindowCursorMode.Inherit)
            {
                // Ничего не делаем - состояние не менялось
                return;
            }

            PopCursorState();
            LogMessage($"Window '{windowId}' restored cursor state");
        }

        /// <summary>
        /// Конвертировать WindowCursorMode в (CursorMode, visible)
        /// </summary>
        private (CursorMode mode, bool visible) ConvertWindowCursorMode(UI.WindowCursorMode windowMode)
        {
            return windowMode switch
            {
                UI.WindowCursorMode.Visible => (CursorMode.Free, true),
                UI.WindowCursorMode.Locked => (CursorMode.Locked, false),
                UI.WindowCursorMode.Confined => (CursorMode.Confined, true),
                _ => (CursorMode.Free, true)
            };
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

            EventBus.Publish(EventBus.CursorEvents.ModeChanged, new CursorEventData
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

            EventBus.Publish(EventBus.CursorEvents.VisibilityChanged, new CursorEventData
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

            EventBus.Publish(EventBus.CursorEvents.CursorChanged, new CursorEventData
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

            EventBus.Publish(EventBus.CursorEvents.CursorChanged, new CursorEventData
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
        /// <summary>События курсора (переименовано из Cursor во избежание конфликта с UnityEngine.Cursor)</summary>
        public static partial class CursorEvents
        {
            /// <summary>Режим курсора изменён</summary>
            public const int ModeChanged = 10400;
            /// <summary>Видимость курсора изменена</summary>
            public const int VisibilityChanged = 10401;
            /// <summary>Курсор изменён</summary>
            public const int CursorChanged = 10402;
        }
        
        /// <summary>Алиас для обратной совместимости (deprecated, используйте CursorEvents)</summary>
        [System.Obsolete("Use EventBus.CursorEvents instead to avoid conflict with UnityEngine.Cursor")]
        public static class Курсор
        {
            public const int Режим_изменён = CursorEvents.ModeChanged;
            public const int Видимость_изменена = CursorEvents.VisibilityChanged;
            public const int Курсор_изменён = CursorEvents.CursorChanged;
        }
    }
}
