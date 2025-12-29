// Packages/com.protosystem.core/Runtime/UI/Core/UIWindowFactory.cs
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Фабрика для создания и пулинга UI окон
    /// </summary>
    public class UIWindowFactory
    {
        private readonly Transform _layerRoot;
        private readonly Dictionary<WindowLayer, Transform> _layers = new();
        private readonly Dictionary<string, Queue<UIWindowBase>> _pool = new();
        private readonly Dictionary<string, WindowDefinition> _definitions = new();

        public UIWindowFactory(Transform root)
        {
            _layerRoot = root;
            CreateLayers();
        }

        #region Layers

        private void CreateLayers()
        {
            // Создаём контейнеры для каждого слоя
            foreach (WindowLayer layer in System.Enum.GetValues(typeof(WindowLayer)))
            {
                var layerObj = new GameObject($"Layer_{layer}");
                layerObj.transform.SetParent(_layerRoot, false);
                
                var rect = layerObj.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                // Canvas для управления порядком
                var canvas = layerObj.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = (int)layer;

                layerObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                _layers[layer] = layerObj.transform;
            }
        }

        public Transform GetLayer(WindowLayer layer)
        {
            return _layers.TryGetValue(layer, out var transform) ? transform : _layerRoot;
        }

        #endregion

        #region Create/Release

        /// <summary>
        /// Создать экземпляр окна
        /// </summary>
        public UIWindowBase Create(WindowDefinition definition)
        {
            if (definition == null || definition.prefab == null)
            {
                Debug.LogError($"[UIWindowFactory] Cannot create window: definition or prefab is null");
                return null;
            }

            // Сохраняем определение
            _definitions[definition.id] = definition;

            UIWindowBase window = null;

            // Пробуем взять из пула
            if (_pool.TryGetValue(definition.id, out var pool) && pool.Count > 0)
            {
                window = pool.Dequeue();
                window.gameObject.SetActive(true);
            }
            else
            {
                // Создаём новый
                var layer = GetLayer(definition.layer);
                var instance = Object.Instantiate(definition.prefab, layer);
                
                window = instance.GetComponent<UIWindowBase>();
                if (window == null)
                {
                    Debug.LogError($"[UIWindowFactory] Prefab for '{definition.id}' doesn't have UIWindowBase component");
                    Object.Destroy(instance);
                    return null;
                }
            }

            // Настраиваем окно
            window.WindowId = definition.id;
            window.WindowType = definition.type;
            window.Layer = definition.layer;
            window.AllowBack = definition.allowBack;

            // Помещаем в правильный слой
            window.transform.SetParent(GetLayer(definition.layer), false);
            
            // Сброс RectTransform
            var rect = window.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            return window;
        }

        /// <summary>
        /// Вернуть окно в пул
        /// </summary>
        public void Release(UIWindowBase window)
        {
            if (window == null) return;

            string windowId = window.WindowId;
            
            // Деактивируем
            window.gameObject.SetActive(false);

            // Добавляем в пул
            if (!_pool.TryGetValue(windowId, out var pool))
            {
                pool = new Queue<UIWindowBase>();
                _pool[windowId] = pool;
            }

            // Ограничиваем размер пула
            const int maxPoolSize = 3;
            if (pool.Count < maxPoolSize)
            {
                pool.Enqueue(window);
            }
            else
            {
                // Уничтожаем лишние
                Object.Destroy(window.gameObject);
            }
        }

        /// <summary>
        /// Очистить весь пул
        /// </summary>
        public void ClearPool()
        {
            foreach (var pool in _pool.Values)
            {
                while (pool.Count > 0)
                {
                    var window = pool.Dequeue();
                    if (window != null)
                        Object.Destroy(window.gameObject);
                }
            }
            _pool.Clear();
        }

        #endregion
    }
}
