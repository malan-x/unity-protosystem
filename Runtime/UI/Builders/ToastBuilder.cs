// Packages/com.protosystem.core/Runtime/UI/Builders/ToastBuilder.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Builder для создания уведомлений (Toast)
    /// </summary>
    public class ToastBuilder
    {
        private readonly UISystem _system;
        private readonly Queue<ToastInstance> _activeToasts = new();
        private Transform _container;
        private int _toastIdCounter;

        public ToastBuilder(UISystem system)
        {
            _system = system;
        }

        #region Show Methods

        /// <summary>
        /// Показать простое уведомление
        /// </summary>
        public string Show(string message, float duration = 0f)
        {
            return Show(new ToastConfig
            {
                Message = message,
                Duration = duration > 0 ? duration : GetDefaultDuration()
            });
        }

        /// <summary>
        /// Показать уведомление с иконкой
        /// </summary>
        public string Show(string message, Sprite icon, float duration = 0f)
        {
            return Show(new ToastConfig
            {
                Message = message,
                Icon = icon,
                Duration = duration > 0 ? duration : GetDefaultDuration()
            });
        }

        /// <summary>
        /// Показать уведомление с типом
        /// </summary>
        public string ShowInfo(string message, float duration = 0f)
            => Show(new ToastConfig { Message = message, Type = ToastType.Info, Duration = duration > 0 ? duration : GetDefaultDuration() });

        public string ShowSuccess(string message, float duration = 0f)
            => Show(new ToastConfig { Message = message, Type = ToastType.Success, Duration = duration > 0 ? duration : GetDefaultDuration() });

        public string ShowWarning(string message, float duration = 0f)
            => Show(new ToastConfig { Message = message, Type = ToastType.Warning, Duration = duration > 0 ? duration : GetDefaultDuration() });

        public string ShowError(string message, float duration = 0f)
            => Show(new ToastConfig { Message = message, Type = ToastType.Error, Duration = duration > 0 ? duration : GetDefaultDuration() });

        /// <summary>
        /// Показать уведомление с полными настройками
        /// </summary>
        public string Show(ToastConfig config)
        {
            EnsureContainer();

            string toastId = $"toast_{++_toastIdCounter}";

            // Проверяем лимит
            var maxToasts = _system.Config?.maxToasts ?? 3;
            while (_activeToasts.Count >= maxToasts)
            {
                var oldest = _activeToasts.Dequeue();
                HideToast(oldest);
            }

            // Создаём toast
            var toastPrefab = _system.Config?.toastPrefab;
            if (toastPrefab == null)
            {
                Debug.LogWarning("[ToastBuilder] Toast prefab not assigned in UISystemConfig");
                return null;
            }

            var instance = Object.Instantiate(toastPrefab, _container);
            var toast = instance.GetComponent<IToast>();
            
            if (toast == null)
            {
                Debug.LogWarning("[ToastBuilder] Toast prefab doesn't implement IToast interface");
                Object.Destroy(instance);
                return null;
            }

            var toastInstance = new ToastInstance
            {
                Id = toastId,
                GameObject = instance,
                Toast = toast,
                Config = config
            };

            toast.Setup(config);
            toast.Show();
            _activeToasts.Enqueue(toastInstance);

            // Авто-скрытие
            if (config.Duration > 0)
            {
                _system.StartCoroutine(AutoHideCoroutine(toastInstance, config.Duration));
            }

            EventBus.Publish(EventBus.UI.ToastShown, new ToastEventData
            {
                ToastId = toastId,
                Message = config.Message,
                Duration = config.Duration
            });

            return toastId;
        }

        #endregion

        #region Hide Methods

        /// <summary>
        /// Скрыть конкретный toast
        /// </summary>
        public void Hide(string toastId)
        {
            var toasts = new List<ToastInstance>(_activeToasts);
            foreach (var toast in toasts)
            {
                if (toast.Id == toastId)
                {
                    HideToast(toast);
                    break;
                }
            }
        }

        /// <summary>
        /// Скрыть все toasts
        /// </summary>
        public void HideAll()
        {
            while (_activeToasts.Count > 0)
            {
                var toast = _activeToasts.Dequeue();
                HideToast(toast);
            }
        }

        private void HideToast(ToastInstance instance)
        {
            if (instance.GameObject == null) return;

            instance.Toast.Hide(() =>
            {
                if (instance.GameObject != null)
                    Object.Destroy(instance.GameObject);
            });

            EventBus.Publish(EventBus.UI.ToastHidden, new ToastEventData
            {
                ToastId = instance.Id,
                Message = instance.Config.Message
            });
        }

        #endregion

        #region Private

        private void EnsureContainer()
        {
            if (_container != null) return;

            // Создаём контейнер для тостов
            var containerObj = new GameObject("ToastContainer");
            containerObj.transform.SetParent(_system.transform, false);

            var rect = containerObj.AddComponent<RectTransform>();
            
            // Позиционируем в зависимости от настроек
            var position = _system.Config?.toastPosition ?? ToastPosition.TopCenter;
            SetContainerPosition(rect, position);

            // Vertical Layout для стека тостов
            var layout = containerObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            containerObj.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = 
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            _container = containerObj.transform;
        }

        private void SetContainerPosition(RectTransform rect, ToastPosition position)
        {
            switch (position)
            {
                case ToastPosition.TopLeft:
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 1);
                    rect.anchoredPosition = new Vector2(20, -20);
                    break;
                case ToastPosition.TopCenter:
                    rect.anchorMin = new Vector2(0.5f, 1);
                    rect.anchorMax = new Vector2(0.5f, 1);
                    rect.pivot = new Vector2(0.5f, 1);
                    rect.anchoredPosition = new Vector2(0, -20);
                    break;
                case ToastPosition.TopRight:
                    rect.anchorMin = new Vector2(1, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(1, 1);
                    rect.anchoredPosition = new Vector2(-20, -20);
                    break;
                case ToastPosition.BottomLeft:
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(0, 0);
                    rect.pivot = new Vector2(0, 0);
                    rect.anchoredPosition = new Vector2(20, 20);
                    break;
                case ToastPosition.BottomCenter:
                    rect.anchorMin = new Vector2(0.5f, 0);
                    rect.anchorMax = new Vector2(0.5f, 0);
                    rect.pivot = new Vector2(0.5f, 0);
                    rect.anchoredPosition = new Vector2(0, 20);
                    break;
                case ToastPosition.BottomRight:
                    rect.anchorMin = new Vector2(1, 0);
                    rect.anchorMax = new Vector2(1, 0);
                    rect.pivot = new Vector2(1, 0);
                    rect.anchoredPosition = new Vector2(-20, 20);
                    break;
            }
        }

        private float GetDefaultDuration() => _system.Config?.defaultToastDuration ?? 3f;

        private IEnumerator AutoHideCoroutine(ToastInstance instance, float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            
            if (instance.GameObject != null)
                HideToast(instance);
        }

        #endregion

        #region Types

        private class ToastInstance
        {
            public string Id;
            public GameObject GameObject;
            public IToast Toast;
            public ToastConfig Config;
        }

        #endregion
    }

    #region Toast Config & Interface

    public class ToastConfig
    {
        public string Message { get; set; }
        public ToastType Type { get; set; } = ToastType.Info;
        public Sprite Icon { get; set; }
        public float Duration { get; set; } = 3f;
        public Action OnClick { get; set; }
    }

    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Интерфейс для компонента Toast
    /// </summary>
    public interface IToast
    {
        void Setup(ToastConfig config);
        void Show();
        void Hide(Action onComplete = null);
    }

    #endregion
}
