// Packages/com.protosystem.core/Runtime/UI/Core/UIWindowFactory.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Фабрика для создания и пулинга UI окон (uGUI и UI Toolkit)
    /// </summary>
    public class UIWindowFactory
    {
        private readonly Transform _layerRoot;
        private readonly UISystemConfig _config;
        private readonly Dictionary<WindowLayer, Transform> _layers = new();
        private readonly Dictionary<string, Queue<UIWindowBase>> _pool = new();
        private readonly Dictionary<string, WindowDefinition> _definitions = new();

        // UI Toolkit: PanelSettings на слой (клоны шаблона из конфига) + счётчик порядка в слое
        private readonly Dictionary<WindowLayer, PanelSettings> _panelSettingsByLayer = new();
        private readonly Dictionary<WindowLayer, int> _panelTopOrder = new();

        public UIWindowFactory(Transform root, UISystemConfig config = null)
        {
            _layerRoot = root;
            _config = config;
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
        /// Принять окно, ЗАПЕЧЁННОЕ в сцену: его экземпляр уже лежит в сцене активным и потому
        /// рисуется с первого кадра — задолго до того, как поднимется UISystem. Это убирает
        /// «дыру» в начале запуска, когда стартовое окно ещё не открыто и виден голый 3D-мир.
        ///
        /// Экземпляр кладётся в пул, поэтому обычный Create() возьмёт именно его и не станет
        /// инстанцировать префаб заново.
        ///
        /// isStartWindow — окно останется видимым и покажется без fade-in (оно уже на экране,
        /// анимация «из прозрачности» дала бы моргание). Остальные запечённые окна гасим:
        /// в сцене они лежат активными, иначе так и висели бы поверх.
        /// </summary>
        public void RegisterBaked(WindowDefinition definition, UIWindowBase window, bool isStartWindow)
        {
            if (definition == null || window == null) return;

            _definitions[definition.id] = definition;

            // Панель, назначенная запечённому окну в сцене, становится панелью всего слоя:
            // иначе рантайм создал бы для слоя свою копию PanelSettings, окно переехало бы
            // в другую панель (свой FocusController, свой порядок) и моргнуло при переключении.
            var doc = window.GetComponent<UIDocument>();
            if (doc != null && doc.panelSettings != null &&
                !_panelSettingsByLayer.ContainsKey(definition.layer))
            {
                _panelSettingsByLayer[definition.layer] = doc.panelSettings;
            }

            if (isStartWindow)
            {
                window.SkipShowAnimation = true;
            }
            else
            {
                window.gameObject.SetActive(false);
            }

            if (!_pool.TryGetValue(definition.id, out var pool))
            {
                pool = new Queue<UIWindowBase>();
                _pool[definition.id] = pool;
            }
            pool.Enqueue(window);
        }

        /// <summary>
        /// Создать экземпляр окна
        /// </summary>
        public UIWindowBase Create(WindowDefinition definition)
        {
            if (definition == null || definition.prefab == null)
            {
                ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Errors, "Cannot create window: definition or prefab is null");
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
                    ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Errors, $"Prefab for '{definition.id}' doesn't have UIWindowBase component");
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

            // Новое окно всегда сверху в своём слое
            window.transform.SetAsLastSibling();

            // Только сбрасываем scale, НЕ трогаем anchors/offsets из prefab'а
            var rect = window.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = Vector3.one;
            }

            // UI Toolkit: настраиваем панель окна (сортировка в единой шкале с Canvas-слоями)
            ConfigureToolkitDocument(window, definition);

            return window;
        }

        /// <summary>
        /// Для окон с UIDocument: назначает PanelSettings слоя (если у префаба нет своих)
        /// и поднимает документ наверх внутри слоя. PanelSettings.sortingOrder = (int)WindowLayer —
        /// та же шкала, что у Canvas-слоёв, поэтому uGUI- и toolkit-окна сортируются согласованно.
        /// </summary>
        /// <summary>
        /// Настройка toolkit-окна: PanelSettings по слою + порядок внутри слоя.
        /// Бэкендов два: UIDocument (по умолчанию, есть везде) и PanelRenderer (Unity 6000.5+).
        /// Оба несут panelSettings/visualTreeAsset/sortingOrder — различается только тип компонента.
        /// </summary>
        private void ConfigureToolkitDocument(UIWindowBase window, WindowDefinition definition)
        {
            // «SetAsLastSibling» для toolkit: внутри панели окна сортируются по sortingOrder
            _panelTopOrder.TryGetValue(definition.layer, out int top);

            var doc = window.GetComponent<UIDocument>();

#if UNITY_6000_5_OR_NEWER
            var renderer = window.GetComponent<PanelRenderer>();
            if (renderer != null)
            {
                if (renderer.panelSettings == null)
                {
                    var rendererPanel = GetOrCreatePanelSettings(definition.layer);
                    if (rendererPanel != null)
                        renderer.panelSettings = rendererPanel;
                    else
                        LogMissingPanelSettings(definition.id, "PanelRenderer");
                }

                _panelTopOrder[definition.layer] = ++top;
                renderer.sortingOrder = top;   // у PanelRenderer это int (унаследован от Renderer)
                return;
            }
#endif

            if (doc == null) return;

            if (doc.panelSettings == null)
            {
                var panel = GetOrCreatePanelSettings(definition.layer);
                if (panel != null)
                    doc.panelSettings = panel;
                else
                    LogMissingPanelSettings(definition.id, "UIDocument");
            }

            _panelTopOrder[definition.layer] = ++top;
            doc.sortingOrder = top;
        }

        private static void LogMissingPanelSettings(string windowId, string component)
        {
            ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Errors,
                $"Окно '{windowId}' (UI Toolkit) без PanelSettings: назначьте PanelSettings " +
                $"в UISystemConfig.panelSettings (шаблон) или на {component} префаба.");
        }

        private PanelSettings GetOrCreatePanelSettings(WindowLayer layer)
        {
            if (_panelSettingsByLayer.TryGetValue(layer, out var cached) && cached != null)
                return cached;

            var template = _config != null ? _config.panelSettings : null;
            if (template == null) return null;

            var instance = Object.Instantiate(template);
            instance.name = $"PanelSettings_{layer}";
            instance.sortingOrder = (int)layer;
            _panelSettingsByLayer[layer] = instance;
            return instance;
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
