// Packages/com.protosystem.core/Runtime/UI/Builders/TooltipBuilder.cs
using System;
using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Builder для создания тултипов
    /// </summary>
    public class TooltipBuilder
    {
        private readonly UISystem _system;
        private GameObject _currentTooltip;
        private ITooltip _tooltipComponent;
        private Coroutine _delayCoroutine;
        private string _currentText;

        public TooltipBuilder(UISystem system)
        {
            _system = system;
        }

        #region Show/Hide

        /// <summary>
        /// Показать тултип с текстом
        /// </summary>
        public void Show(string text, Vector2? position = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                Hide();
                return;
            }

            _currentText = text;

            // Отменяем предыдущий delay
            if (_delayCoroutine != null)
            {
                _system.StopCoroutine(_delayCoroutine);
                _delayCoroutine = null;
            }

            float delay = _system.Config?.tooltipDelay ?? 0.5f;
            
            if (delay > 0 && _currentTooltip == null)
            {
                _delayCoroutine = _system.StartCoroutine(ShowDelayed(text, position, delay));
            }
            else
            {
                ShowImmediate(text, position);
            }
        }

        /// <summary>
        /// Показать тултип с конфигом
        /// </summary>
        public void Show(TooltipConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.Text))
            {
                Hide();
                return;
            }

            _currentText = config.Text;

            if (_delayCoroutine != null)
            {
                _system.StopCoroutine(_delayCoroutine);
                _delayCoroutine = null;
            }

            float delay = config.Delay >= 0 ? config.Delay : (_system.Config?.tooltipDelay ?? 0.5f);
            
            if (delay > 0 && _currentTooltip == null)
            {
                _delayCoroutine = _system.StartCoroutine(ShowDelayedConfig(config, delay));
            }
            else
            {
                ShowImmediateConfig(config);
            }
        }

        /// <summary>
        /// Скрыть текущий тултип
        /// </summary>
        public void Hide()
        {
            if (_delayCoroutine != null)
            {
                _system.StopCoroutine(_delayCoroutine);
                _delayCoroutine = null;
            }

            if (_currentTooltip != null)
            {
                _tooltipComponent?.Hide(() =>
                {
                    if (_currentTooltip != null)
                        Object.Destroy(_currentTooltip);
                    _currentTooltip = null;
                    _tooltipComponent = null;
                });

                EventBus.Publish(EventBus.UI.TooltipHidden, new TooltipEventData
                {
                    Text = _currentText
                });
            }

            _currentText = null;
        }

        /// <summary>
        /// Обновить позицию тултипа (вызывать в Update при следовании за курсором)
        /// </summary>
        public void UpdatePosition(Vector2 position)
        {
            if (_currentTooltip == null) return;

            var rect = _currentTooltip.GetComponent<RectTransform>();
            if (rect != null)
            {
                var offset = _system.Config?.tooltipOffset ?? new Vector2(10, -10);
                rect.position = position + offset;
                
                // Не даём выйти за пределы экрана
                ClampToScreen(rect);
            }
        }

        #endregion

        #region Private

        private IEnumerator ShowDelayed(string text, Vector2? position, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            ShowImmediate(text, position);
            _delayCoroutine = null;
        }

        private IEnumerator ShowDelayedConfig(TooltipConfig config, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            ShowImmediateConfig(config);
            _delayCoroutine = null;
        }

        private void ShowImmediate(string text, Vector2? position)
        {
            ShowImmediateConfig(new TooltipConfig
            {
                Text = text,
                Position = position
            });
        }

        private void ShowImmediateConfig(TooltipConfig config)
        {
            // Если уже показан - обновляем
            if (_currentTooltip != null)
            {
                _tooltipComponent?.Setup(config);
                if (config.Position.HasValue)
                    UpdatePosition(config.Position.Value);
                return;
            }

            // Создаём новый
            var prefab = _system.Config?.tooltipPrefab;
            if (prefab == null)
            {
                Debug.LogWarning("[TooltipBuilder] Tooltip prefab not assigned in UISystemConfig");
                return;
            }

            _currentTooltip = Object.Instantiate(prefab, _system.transform);
            _tooltipComponent = _currentTooltip.GetComponent<ITooltip>();

            if (_tooltipComponent == null)
            {
                Debug.LogWarning("[TooltipBuilder] Tooltip prefab doesn't implement ITooltip interface");
                Object.Destroy(_currentTooltip);
                _currentTooltip = null;
                return;
            }

            _tooltipComponent.Setup(config);
            _tooltipComponent.Show();

            // Позиционируем
            if (config.Position.HasValue)
            {
                UpdatePosition(config.Position.Value);
            }
            else
            {
                // По умолчанию рядом с курсором
                UpdatePosition(Input.mousePosition);
            }

            EventBus.Publish(EventBus.UI.TooltipShown, new TooltipEventData
            {
                Text = config.Text
            });
        }

        private void ClampToScreen(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            float minX = corners[0].x;
            float maxX = corners[2].x;
            float minY = corners[0].y;
            float maxY = corners[2].y;

            Vector2 offset = Vector2.zero;

            if (minX < 0) offset.x = -minX;
            else if (maxX > Screen.width) offset.x = Screen.width - maxX;

            if (minY < 0) offset.y = -minY;
            else if (maxY > Screen.height) offset.y = Screen.height - maxY;

            if (offset != Vector2.zero)
            {
                rect.position += (Vector3)offset;
            }
        }

        #endregion
    }

    #region Tooltip Config & Interface

    public class TooltipConfig
    {
        public string Text { get; set; }
        public string Title { get; set; }
        public Sprite Icon { get; set; }
        public Vector2? Position { get; set; }
        public float Delay { get; set; } = -1f; // -1 = use default from config
        public TooltipStyle Style { get; set; } = TooltipStyle.Default;
    }

    public enum TooltipStyle
    {
        Default,
        Info,
        Warning,
        Error,
        Custom
    }

    /// <summary>
    /// Интерфейс для компонента Tooltip
    /// </summary>
    public interface ITooltip
    {
        void Setup(TooltipConfig config);
        void Show();
        void Hide(Action onComplete = null);
    }

    #endregion
}
