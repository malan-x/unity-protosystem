// Packages/com.protosystem.core/Runtime/UI/Core/UINavigator.cs
using System;
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
        /// Навигация по триггеру
        /// </summary>
        public NavigationResult Navigate(string trigger)
        {
            if (string.IsNullOrEmpty(trigger))
                return Fail(NavigationResult.TriggerNotFound, null, null, trigger);

            // Если есть модальное окно — проверяем его переходы
            string fromId = CurrentModal?.WindowId ?? CurrentWindow?.WindowId ?? "";
            
            // Ищем переход в графе
            var transition = _graph.FindTransition(fromId, trigger);
            if (transition == null)
                return Fail(NavigationResult.TriggerNotFound, fromId, null, trigger);

            return OpenWindow(transition.toWindowId, transition.animation, fromId, trigger);
        }

        /// <summary>
        /// Открыть окно напрямую (проверяет разрешённость в графе)
        /// </summary>
        public NavigationResult Open(string windowId, TransitionAnimation animation = TransitionAnimation.Fade)
        {
            string fromId = CurrentModal?.WindowId ?? CurrentWindow?.WindowId ?? "";
            
            // Проверяем разрешённость
            if (!string.IsNullOrEmpty(fromId) && !_graph.IsTransitionAllowed(fromId, windowId))
                return Fail(NavigationResult.TransitionNotAllowed, fromId, windowId, null);

            return OpenWindow(windowId, animation, fromId, null);
        }

        /// <summary>
        /// Открыть окно без проверки графа (для системных окон)
        /// </summary>
        public NavigationResult OpenDirect(string windowId, TransitionAnimation animation = TransitionAnimation.Fade)
        {
            string fromId = CurrentModal?.WindowId ?? CurrentWindow?.WindowId ?? "";
            return OpenWindow(windowId, animation, fromId, null);
        }

        /// <summary>
        /// Вернуться назад
        /// </summary>
        public NavigationResult Back()
        {
            // Сначала закрываем модальные
            if (_modalStack.Count > 0)
            {
                return CloseTopModal();
            }

            // Затем обычные окна
            if (_windowStack.Count <= 1)
                return Fail(NavigationResult.StackEmpty, CurrentWindow?.WindowId, null, "Back");

            var closing = _windowStack.Pop();
            var opening = _windowStack.Peek();

            // Закрываем текущее
            closing.Hide(() =>
            {
                _factory.Release(closing);
            });

            // Показываем предыдущее
            CurrentWindow = opening;
            opening.Focus();

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

        #endregion

        #region Internal

        private NavigationResult OpenWindow(string windowId, TransitionAnimation animation, string fromId, string trigger)
        {
            // Получаем определение окна
            var definition = _graph.GetWindow(windowId);
            if (definition == null)
                return Fail(NavigationResult.WindowNotFound, fromId, windowId, trigger);

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

            // Пауза
            if (definition.pauseGame)
                Time.timeScale = 0f;

            PublishEvent(new NavigationEventData
            {
                FromWindowId = fromId,
                ToWindowId = windowId,
                Trigger = trigger,
                Result = NavigationResult.Success
            });

            return NavigationResult.Success;
        }

        private void OpenNormal(UIWindowBase window, WindowDefinition definition)
        {
            // Скрываем/блюрим текущее
            if (CurrentWindow != null)
            {
                if (definition.hideBelow)
                {
                    var current = CurrentWindow;
                    current.Hide(() => { }); // Не освобождаем, остаётся в стеке
                }
                else
                {
                    CurrentWindow.Blur();
                }
            }

            // Добавляем в стек
            _windowStack.Push(window);
            CurrentWindow = window;
            
            // Показываем
            window.Show();
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
            // Overlay не блокирует и не добавляется в стек
            window.Show();
        }

        private NavigationResult CloseTopModal()
        {
            if (_modalStack.Count == 0)
                return NavigationResult.StackEmpty;

            var closing = _modalStack.Pop();
            
            // Возвращаем паузу если это было последнее модальное с паузой
            // (упрощённо — снимаем паузу при закрытии любого модального)
            if (_modalStack.Count == 0)
                Time.timeScale = 1f;

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
                ToWindowId = _modalStack.Count > 0 ? _modalStack.Peek().WindowId : CurrentWindow?.WindowId,
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
            if (string.IsNullOrEmpty(_graph.startWindowId))
            {
                Debug.LogError("[UINavigator] No start window defined in graph");
                return;
            }

            OpenDirect(_graph.startWindowId);
        }

        #endregion
    }
}
