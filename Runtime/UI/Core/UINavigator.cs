// Packages/com.protosystem.core/Runtime/UI/Core/UINavigator.cs
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Управляет навигацией между UI окнами.
    /// Поддерживает стек окон, модальные окна и Back-навигацию.
    /// </summary>
    public class UINavigator
    {
        private readonly UIWindowGraph _graph;
        private readonly UIWindowFactory _factory;
        
        // Стек обычных окон
        private readonly Stack<UIWindowBase> _windowStack = new();
        
        // Стек модальных окон
        private readonly Stack<UIWindowBase> _modalStack = new();
        
        // Активные overlay окна (не в стеке)
        private readonly Dictionary<string, UIWindowBase> _overlays = new();
        
        // Текущее активное окно (верхнее в стеке)
        public UIWindowBase CurrentWindow { get; private set; }
        
        // Текущее модальное окно (если есть)
        public UIWindowBase CurrentModal => _modalStack.Count > 0 ? _modalStack.Peek() : null;
        
        // Есть ли модальные окна
        public bool HasModal => _modalStack.Count > 0;
        
        // Можно ли выполнить Back
        public bool CanGoBack => CurrentModal != null || _windowStack.Count > 1;

        // Событие навигации
        public event Action<NavigationEventData> OnNavigated;

        public UINavigator(UIWindowGraph graph, UIWindowFactory factory)
        {
            _graph = graph;
            _factory = factory;
        }

        #region Navigation

        /// <summary>
        /// Навигация по триггеру (использует граф переходов)
        /// </summary>
        public NavigationResult Navigate(string trigger)
        {
            if (string.IsNullOrEmpty(trigger))
                return Fail(NavigationResult.TriggerNotFound, null, null, trigger);

            // Если есть модальное окно — сначала проверяем переходы модального.
            // Если в модальном перехода нет, обычно ожидаем, что триггер относится
            // к "подложке" (CurrentWindow), например ConfirmDialog поверх PauseMenu.
            string modalFromId = CurrentModal?.WindowId;
            string windowFromId = CurrentWindow?.WindowId;

            TransitionDefinition transition = null;
            string fromId = "";

            if (!string.IsNullOrEmpty(modalFromId))
            {
                transition = _graph?.FindTransition(modalFromId, trigger);
                fromId = modalFromId;
            }

            if (transition == null && !string.IsNullOrEmpty(windowFromId))
            {
                transition = _graph?.FindTransition(windowFromId, trigger);
                fromId = windowFromId;
            }

            Debug.Log($"[UINavigator] Navigate('{trigger}') from '{fromId}' -> {(transition != null ? transition.toWindowId : "NOT FOUND")}");

            if (transition == null)
                return Fail(NavigationResult.TriggerNotFound, fromId, null, trigger);

            return OpenWindow(transition.toWindowId, transition.animation, fromId, trigger);
        }

        /// <summary>
        /// Открыть окно напрямую по ID
        /// </summary>
        public NavigationResult Open(string windowId, TransitionAnimation animation = TransitionAnimation.Fade)
        {
            string fromId = CurrentModal?.WindowId ?? CurrentWindow?.WindowId ?? "";
            return OpenWindow(windowId, animation, fromId, null);
        }

        /// <summary>
        /// Открыть окно без проверок (алиас для Open, для совместимости)
        /// </summary>
        public NavigationResult OpenDirect(string windowId, TransitionAnimation animation = TransitionAnimation.Fade)
        {
            return Open(windowId, animation);
        }

        /// <summary>
        /// Вернуться назад
        /// </summary>
        public NavigationResult Back()
        {
            Debug.Log($"[UINavigator] Back() called. WindowStack={_windowStack.Count}, ModalStack={_modalStack.Count}");

            // Сначала закрываем модальные
            if (_modalStack.Count > 0)
            {
                return CloseTopModal();
            }

            // Затем обычные окна
            if (_windowStack.Count <= 1)
            {
                Debug.Log("[UINavigator] Back() failed - stack has only 1 window");
                return Fail(NavigationResult.StackEmpty, CurrentWindow?.WindowId, null, "Back");
            }

            var closing = _windowStack.Pop();
            var opening = _windowStack.Peek();

            Debug.Log($"[UINavigator] Back: closing '{closing.WindowId}', showing '{opening.WindowId}'");

            // Получаем определения окон
            var closingDef = _graph?.GetWindow(closing.WindowId);
            var openingDef = _graph?.GetWindow(opening.WindowId);

            // Восстанавливаем состояния при закрытии
            if (closingDef != null)
            {
                if (closingDef.pauseGame)
                {
                    UITimeManager.Instance.ReleasePause(closing.WindowId);
                }

                if (closingDef.cursorMode != WindowCursorMode.Inherit)
                {
                    Cursor.CursorManagerSystem.RestoreWindowCursorMode(closingDef.cursorMode, closing.WindowId);
                }
            }

            // Принудительно применяем режим курсора активного окна
            if (openingDef != null && openingDef.cursorMode != WindowCursorMode.Inherit)
            {
                Cursor.CursorManagerSystem.ForceApplyCursorMode(openingDef.cursorMode);
            }

            // Закрываем текущее
            closing.Hide(() =>
            {
                _factory.Release(closing);
            });

            // Показываем предыдущее (оно могло быть скрыто)
            CurrentWindow = opening;
            opening.Show(); // Show вместо Focus - окно могло быть скрыто

            PublishEvent(new NavigationEventData
            {
                FromWindowId = closing.WindowId,
                ToWindowId = opening.WindowId,
                Trigger = "Back",
                Result = NavigationResult.Success
            });

            return NavigationResult.Success;
        }

        /// <summary>
        /// Закрыть все окна и вернуться к стартовому
        /// </summary>
        public void Reset()
        {
            // Закрываем все модальные
            while (_modalStack.Count > 0)
            {
                var modal = _modalStack.Pop();
                modal.Hide(() => _factory.Release(modal));
            }

            // Закрываем все кроме первого
            while (_windowStack.Count > 1)
            {
                var window = _windowStack.Pop();
                window.Hide(() => _factory.Release(window));
            }

            // Активируем первое
            if (_windowStack.Count > 0)
            {
                CurrentWindow = _windowStack.Peek();
                CurrentWindow.Focus();
            }
        }

        /// <summary>
        /// Закрыть конкретное окно
        /// </summary>
        public void Close(UIWindowBase window)
        {
            if (window == null) return;

            // Получаем определение для восстановления состояний
            var definition = _graph?.GetWindow(window.WindowId);

            // Проверяем в стеке модальных
            if (_modalStack.Contains(window))
            {
                // Создаём новый стек без этого окна
                var temp = new Stack<UIWindowBase>();
                while (_modalStack.Count > 0)
                {
                    var w = _modalStack.Pop();
                    if (w != window)
                        temp.Push(w);
                }
                while (temp.Count > 0)
                    _modalStack.Push(temp.Pop());
            }

            // Восстанавливаем состояния
            if (definition != null)
            {
                // Снимаем запрос паузы
                if (definition.pauseGame)
                {
                    UITimeManager.Instance.ReleasePause(window.WindowId);
                }

                // Восстанавливаем курсор
                if (definition.cursorMode != WindowCursorMode.Inherit)
                {
                    Cursor.CursorManagerSystem.RestoreWindowCursorMode(definition.cursorMode, window.WindowId);
                }
            }

            // Определяем следующее активное окно
            UIWindowBase nextWindow = _modalStack.Count > 0 ? _modalStack.Peek() : CurrentWindow;
            var nextDef = nextWindow != null ? _graph?.GetWindow(nextWindow.WindowId) : null;

            // Принудительно применяем режим курсора следующего окна
            if (nextDef != null && nextDef.cursorMode != WindowCursorMode.Inherit)
            {
                Cursor.CursorManagerSystem.ForceApplyCursorMode(nextDef.cursorMode);
            }

            window.Hide(() => _factory.Release(window));
        }

        #endregion

        #region Internal

        private NavigationResult OpenWindow(string windowId, TransitionAnimation animation, string fromId, string trigger)
        {
            // Получаем определение окна
            var definition = _graph?.GetWindow(windowId);
            if (definition == null && TryResolveWindowId(windowId, out var resolvedId))
            {
                Debug.Log($"[UINavigator] Resolved window id '{windowId}' -> '{resolvedId}'");
                windowId = resolvedId;
                definition = _graph?.GetWindow(windowId);
            }

            if (definition == null)
            {
                // Диагностика: какие окна есть в графе?
                Debug.LogError($"[UINavigator] Window '{windowId}' not found in graph. Available windows: {string.Join(", ", _graph?.GetAllWindows().Select(w => w.id) ?? System.Array.Empty<string>())}. " +
                               "Hint: Open expects UIWindowAttribute.WindowId (e.g. 'MainMenu'), not the class name (e.g. 'MainMenuWindow').");
                return Fail(NavigationResult.WindowNotFound, fromId, windowId, trigger);
            }

            // Диагностика prefab
            if (definition.prefab == null)
            {
                Debug.LogError($"[UINavigator] Window '{windowId}' found but prefab is NULL! Check UISystemConfig.");
            }

            // Проверяем что не открываем то же самое окно
            if (CurrentWindow?.WindowId == windowId && definition.type != WindowType.Modal)
                return Fail(NavigationResult.AlreadyOnWindow, fromId, windowId, trigger);

            // Создаём экземпляр окна
            var window = _factory.Create(definition);
            if (window == null)
                return Fail(NavigationResult.WindowNotFound, fromId, windowId, trigger);

            // Обрабатываем в зависимости от типа
            switch (definition.type)
            {
                case WindowType.Modal:
                    OpenModal(window, definition);
                    break;

                case WindowType.Overlay:
                    OpenOverlay(window, definition);
                    break;

                default:
                    OpenNormal(window, definition);
                    break;
            }

            // Пауза через UITimeManager
            if (definition.pauseGame)
            {
                UITimeManager.Instance.RequestPause(windowId);
            }

            // Курсор через CursorManagerSystem
            if (definition.cursorMode != WindowCursorMode.Inherit)
            {
                Cursor.CursorManagerSystem.ApplyWindowCursorMode(definition.cursorMode, windowId);
            }

            PublishEvent(new NavigationEventData
            {
                FromWindowId = fromId,
                ToWindowId = windowId,
                Trigger = trigger,
                Result = NavigationResult.Success
            });

            return NavigationResult.Success;
        }

        private bool TryResolveWindowId(string requestedId, out string resolvedId)
        {
            resolvedId = null;
            if (_graph == null || string.IsNullOrWhiteSpace(requestedId))
                return false;

            // 1) Common mismatch: callers pass class name like 'MainMenuWindow', while graph uses WindowId like 'MainMenu'
            var withoutSuffix = StripWindowSuffix(requestedId);
            if (!string.Equals(withoutSuffix, requestedId, StringComparison.Ordinal))
            {
                if (_graph.HasWindow(withoutSuffix))
                {
                    resolvedId = withoutSuffix;
                    return true;
                }
            }

            // 2) Some projects may do the opposite (rare): call 'MainMenu' while graph uses 'MainMenuWindow'
            var withSuffix = EnsureWindowSuffix(requestedId);
            if (!string.Equals(withSuffix, requestedId, StringComparison.Ordinal))
            {
                if (_graph.HasWindow(withSuffix))
                {
                    resolvedId = withSuffix;
                    return true;
                }
            }

            // 3) Match by window type name (simple or full) stored in graph
            //    Allows calls like Open(nameof(MainMenuWindow)) to work.
            foreach (var window in _graph.GetAllWindows() ?? Array.Empty<WindowDefinition>())
            {
                if (window == null) continue;
                if (string.IsNullOrEmpty(window.typeName) || string.IsNullOrEmpty(window.id)) continue;

                if (string.Equals(window.typeName, requestedId, StringComparison.Ordinal) ||
                    string.Equals(GetSimpleTypeName(window.typeName), requestedId, StringComparison.Ordinal) ||
                    string.Equals(GetSimpleTypeName(window.typeName), EnsureWindowSuffix(requestedId), StringComparison.Ordinal))
                {
                    resolvedId = window.id;
                    return true;
                }
            }

            return false;
        }

        private static string StripWindowSuffix(string id)
        {
            const string suffix = "Window";
            if (string.IsNullOrEmpty(id)) return id;
            return id.EndsWith(suffix, StringComparison.Ordinal) ? id.Substring(0, id.Length - suffix.Length) : id;
        }

        private static string EnsureWindowSuffix(string id)
        {
            const string suffix = "Window";
            if (string.IsNullOrEmpty(id)) return id;
            return id.EndsWith(suffix, StringComparison.Ordinal) ? id : id + suffix;
        }

        private static string GetSimpleTypeName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return fullTypeName;
            int lastDot = fullTypeName.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < fullTypeName.Length ? fullTypeName.Substring(lastDot + 1) : fullTypeName;
        }

        private void OpenNormal(UIWindowBase window, WindowDefinition definition)
        {
            Debug.Log($"[UINavigator] OpenNormal '{definition.id}' level={definition.level}");

            // Закрываем все Normal окна с уровнем >= открываемого
            // Это гарантирует что Level 0 окна взаимоисключающие,
            // а Level 1+ окна закрывают все окна того же или выше уровня
            CloseWindowsAtOrAboveLevel(definition.level);

            // Level 1+ окна должны быть непрозрачными (чтобы не накладывались на другие)
            if (definition.level > 0)
            {
                ApplyOpaqueBackground(window);
            }

            // Добавляем в стек
            _windowStack.Push(window);
            CurrentWindow = window;

            // Показываем
            window.Show();
        }

        /// <summary>
        /// Делает фон окна непрозрачным (для окон level 1+)
        /// </summary>
        private void ApplyOpaqueBackground(UIWindowBase window)
        {
            // Проверяем UITwoColorImage (приоритет)
            var twoColor = window.GetComponent<UITwoColorImage>();
            if (twoColor != null)
            {
                var fill = twoColor.FillColor;
                fill.a = 1f;
                twoColor.FillColor = fill;
                Debug.Log($"[UINavigator] Made '{window.WindowId}' opaque via UITwoColorImage");
                return;
            }

            // Fallback на обычный Image
            var image = window.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                var color = image.color;
                color.a = 1f;
                image.color = color;
                Debug.Log($"[UINavigator] Made '{window.WindowId}' opaque via Image.color");
            }
        }

        /// <summary>
        /// Закрывает все Normal окна с уровнем >= указанного.
        /// При открытии окна уровня X все Normal окна уровня X и выше закрываются.
        /// Это гарантирует что Level 0 окна взаимоисключающие,
        /// а Level 1+ окна также закрывают все окна того же или выше уровня.
        /// </summary>
        /// <param name="targetLevel">Минимальный уровень окон для закрытия</param>
        private void CloseWindowsAtOrAboveLevel(int targetLevel)
        {
            if (_windowStack.Count == 0) return;

            var windowsToClose = new List<(UIWindowBase window, WindowDefinition def)>();
            var windowsToKeep = new Stack<UIWindowBase>();

            // Разбираем стек
            while (_windowStack.Count > 0)
            {
                var w = _windowStack.Pop();
                var def = _graph?.GetWindow(w.WindowId);
                int windowLevel = def?.level ?? 0;

                // Закрываем Normal окна с level >= targetLevel
                if (def != null && def.type == WindowType.Normal && windowLevel >= targetLevel)
                {
                    windowsToClose.Add((w, def));
                    Debug.Log($"[UINavigator] Closing level {windowLevel} window '{w.WindowId}' (targetLevel={targetLevel})");
                }
                else
                {
                    // Overlay/Modal или level ниже целевого - сохраняем
                    windowsToKeep.Push(w);
                }
            }

            // Восстанавливаем стек
            while (windowsToKeep.Count > 0)
            {
                _windowStack.Push(windowsToKeep.Pop());
            }

            // Закрываем окна и восстанавливаем состояния
            foreach (var (w, def) in windowsToClose)
            {
                // Восстанавливаем состояния
                if (def != null)
                {
                    if (def.pauseGame)
                    {
                        UITimeManager.Instance.ReleasePause(w.WindowId);
                    }

                    if (def.cursorMode != WindowCursorMode.Inherit)
                    {
                        Cursor.CursorManagerSystem.RestoreWindowCursorMode(def.cursorMode, w.WindowId);
                    }
                }

                w.Hide(() => _factory.Release(w));
            }

            // Обновляем CurrentWindow
            CurrentWindow = _windowStack.Count > 0 ? _windowStack.Peek() : null;
        }


        private void OpenModal(UIWindowBase window, WindowDefinition definition)
        {
            // Блюрим нижнее окно
            if (_modalStack.Count > 0)
            {
                _modalStack.Peek().Blur();
            }
            else if (CurrentWindow != null)
            {
                CurrentWindow.Blur();
            }

            // Добавляем в стек модальных
            _modalStack.Push(window);
            
            // Показываем
            window.Show();
        }

        private void OpenOverlay(UIWindowBase window, WindowDefinition definition)
        {
            // Overlay не блокирует и не добавляется в стек навигации
            _overlays[window.WindowId] = window;
            window.Show();
        }

        #endregion

        #region Get/Close by ID

        /// <summary>
        /// Получить открытое окно по ID
        /// </summary>
        public UIWindowBase GetWindow(string windowId)
        {
            if (string.IsNullOrEmpty(windowId)) return null;

            // Проверяем overlay
            if (_overlays.TryGetValue(windowId, out var overlay))
                return overlay;

            // Проверяем текущее окно
            if (CurrentWindow?.WindowId == windowId)
                return CurrentWindow;

            // Проверяем стек окон
            foreach (var window in _windowStack)
            {
                if (window.WindowId == windowId)
                    return window;
            }

            // Проверяем модальные
            foreach (var modal in _modalStack)
            {
                if (modal.WindowId == windowId)
                    return modal;
            }

            return null;
        }

        /// <summary>
        /// Получить открытое окно по ID с кастом к типу
        /// </summary>
        public T GetWindow<T>(string windowId) where T : UIWindowBase
        {
            return GetWindow(windowId) as T;
        }

        /// <summary>
        /// Закрыть окно по ID
        /// </summary>
        public NavigationResult Close(string windowId)
        {
            if (string.IsNullOrEmpty(windowId))
                return NavigationResult.WindowNotFound;

            // Проверяем overlay
            if (_overlays.TryGetValue(windowId, out var overlay))
            {
                _overlays.Remove(windowId);
                overlay.Hide(() => _factory.Release(overlay));
                return NavigationResult.Success;
            }

            // Проверяем модальные - закрываем по порядку если это верхнее
            if (_modalStack.Count > 0 && _modalStack.Peek().WindowId == windowId)
            {
                return CloseTopModal();
            }

            // Для обычных окон - только если это текущее
            if (CurrentWindow?.WindowId == windowId && _windowStack.Count > 1)
            {
                return Back();
            }

            return NavigationResult.WindowNotFound;
        }

        private NavigationResult CloseTopModal()
        {
            if (_modalStack.Count == 0)
                return NavigationResult.StackEmpty;

            var closing = _modalStack.Pop();
            var closingDef = _graph?.GetWindow(closing.WindowId);

            // Восстанавливаем состояния
            if (closingDef != null)
            {
                if (closingDef.pauseGame)
                {
                    UITimeManager.Instance.ReleasePause(closing.WindowId);
                }

                if (closingDef.cursorMode != WindowCursorMode.Inherit)
                {
                    Cursor.CursorManagerSystem.RestoreWindowCursorMode(closingDef.cursorMode, closing.WindowId);
                }
            }

            // Определяем следующее активное окно и его режим курсора
            UIWindowBase nextWindow = _modalStack.Count > 0 ? _modalStack.Peek() : CurrentWindow;
            var nextDef = nextWindow != null ? _graph?.GetWindow(nextWindow.WindowId) : null;

            // Принудительно применяем режим курсора следующего окна
            if (nextDef != null && nextDef.cursorMode != WindowCursorMode.Inherit)
            {
                Cursor.CursorManagerSystem.ForceApplyCursorMode(nextDef.cursorMode);
            }

            closing.Hide(() =>
            {
                _factory.Release(closing);
            });

            // Фокусируем следующее модальное или основное окно
            if (_modalStack.Count > 0)
            {
                _modalStack.Peek().Focus();
            }
            else if (CurrentWindow != null)
            {
                CurrentWindow.Focus();
            }

            PublishEvent(new NavigationEventData
            {
                FromWindowId = closing.WindowId,
                ToWindowId = nextWindow?.WindowId,
                Trigger = "Back",
                Result = NavigationResult.Success
            });

            return NavigationResult.Success;
        }

        private NavigationResult Fail(NavigationResult result, string from, string to, string trigger)
        {
            var data = new NavigationEventData
            {
                FromWindowId = from,
                ToWindowId = to,
                Trigger = trigger,
                Result = result
            };

            Debug.LogWarning($"[UINavigator] Navigation failed: {result}. From: {from}, To: {to}, Trigger: {trigger}");
            
            EventBus.Publish(EventBus.UI.NavigationFailed, data);
            return result;
        }

        private void PublishEvent(NavigationEventData data)
        {
            OnNavigated?.Invoke(data);
            EventBus.Publish(EventBus.UI.NavigationCompleted, data);

            if (!string.IsNullOrEmpty(data.ToWindowId))
            {
                EventBus.Publish(EventBus.UI.WindowOpened, new WindowEventData
                {
                    WindowId = data.ToWindowId
                });
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Открыть стартовое окно
        /// </summary>
        public void OpenStartWindow()
        {
            if (_graph == null || string.IsNullOrEmpty(_graph.startWindowId))
            {
                Debug.LogWarning("[UINavigator] No start window defined in graph");
                return;
            }

            Open(_graph.startWindowId);
        }

        #endregion
    }
}
