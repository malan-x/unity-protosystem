// Packages/com.protosystem.core/Runtime/UI/Core/UIWindowGraph.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Автогенерируемый граф UI окон.
    /// Создаётся и обновляется автоматически при компиляции.
    /// Используется для навигации и визуализации связей.
    /// </summary>
    public class UIWindowGraph : ScriptableObject
    {
        public const string RESOURCE_PATH = "ProtoSystem/UIWindowGraph";
        public const string ASSET_PATH = "Assets/Resources/ProtoSystem/UIWindowGraph.asset";

        [Header("Auto-generated Data")]
        public List<WindowDefinition> windows = new();
        public List<TransitionDefinition> transitions = new();
        public List<TransitionDefinition> globalTransitions = new();
        
        [Header("Settings")]
        [Tooltip("ID стартового окна")]
        public string startWindowId = "";
        
        [Header("Build Info")]
                [System.NonSerialized] public string lastBuildTime;
        public int windowCount;
        public int transitionCount;

        // Runtime кэш
        private Dictionary<string, WindowDefinition> _windowCache;
        private Dictionary<string, List<TransitionDefinition>> _transitionCache;
        private bool _cacheValid;

        #region Static Access

        private static UIWindowGraph _instance;

        /// <summary>
        /// Получить граф (загружает из Resources если нужно)
        /// </summary>
        public static UIWindowGraph Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<UIWindowGraph>(RESOURCE_PATH);
                    
                    #if UNITY_EDITOR
                    // В Editor создаём если не существует
                    if (_instance == null)
                    {
                        _instance = CreateInstance<UIWindowGraph>();
                        Debug.Log("[UIWindowGraph] Created new instance (will be saved on compilation)");
                    }
                    #endif
                }
                return _instance;
            }
        }

        /// <summary>
        /// Принудительно перезагрузить граф
        /// </summary>
        public static void Reload()
        {
            _instance = null;
            var _ = Instance;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Получить определение окна по ID
        /// </summary>
        public WindowDefinition GetWindow(string windowId)
        {
            EnsureCache();
            return _windowCache.TryGetValue(windowId, out var window) ? window : null;
        }

        /// <summary>
        /// Получить все окна
        /// </summary>
        public IReadOnlyList<WindowDefinition> GetAllWindows()
        {
            return windows;
        }

        /// <summary>
        /// Получить все переходы из окна
        /// </summary>
        public IEnumerable<TransitionDefinition> GetTransitionsFrom(string windowId)
        {
            EnsureCache();
            
            var result = new List<TransitionDefinition>();
            
            if (_transitionCache.TryGetValue(windowId, out var local))
                result.AddRange(local);
            
            result.AddRange(globalTransitions);
            
            return result;
        }

        /// <summary>
        /// Найти переход по триггеру
        /// </summary>
        public TransitionDefinition FindTransition(string fromWindowId, string trigger)
        {
            EnsureCache();

            // Сначала локальные
            if (_transitionCache.TryGetValue(fromWindowId, out var local))
            {
                var found = local.FirstOrDefault(t => t.trigger == trigger);
                if (found != null) return found;
            }

            // Затем глобальные
            return globalTransitions.FirstOrDefault(t => t.trigger == trigger);
        }

        /// <summary>
        /// Проверить существование окна
        /// </summary>
        public bool HasWindow(string windowId) => GetWindow(windowId) != null;

        #endregion

        #region Cache

        private void EnsureCache()
        {
            if (_cacheValid) return;

            _windowCache = new Dictionary<string, WindowDefinition>();
            _transitionCache = new Dictionary<string, List<TransitionDefinition>>();

            foreach (var window in windows)
            {
                if (!string.IsNullOrEmpty(window.id))
                    _windowCache[window.id] = window;
            }

            foreach (var transition in transitions)
            {
                if (string.IsNullOrEmpty(transition.fromWindowId)) continue;
                
                if (!_transitionCache.TryGetValue(transition.fromWindowId, out var list))
                {
                    list = new List<TransitionDefinition>();
                    _transitionCache[transition.fromWindowId] = list;
                }
                list.Add(transition);
            }

            _cacheValid = true;
        }

        public void InvalidateCache()
        {
            _cacheValid = false;
        }

        private void OnEnable()
        {
            InvalidateCache();
        }

        #endregion

        #region Builder Methods

        /// <summary>
        /// Очистить данные перед пересборкой
        /// </summary>
        public void ClearForRebuild()
        {
            windows.Clear();
            transitions.Clear();
            globalTransitions.Clear();
            InvalidateCache();
        }

        /// <summary>
        /// Добавить определение окна
        /// </summary>
        public void AddWindow(WindowDefinition window)
        {
            windows.RemoveAll(w => w.id == window.id);
            windows.Add(window);
        }

        /// <summary>
        /// Добавить переход
        /// </summary>
        public void AddTransition(TransitionDefinition transition, bool allowOverride = false)
        {
            string context = Application.isPlaying ? "Runtime" : "Editor";

            if (string.IsNullOrEmpty(transition.fromWindowId))
            {
                // Глобальный переход
                if (allowOverride)
                {
                    int removed = globalTransitions.RemoveAll(t => t.trigger == transition.trigger);
                    if (removed > 0)
                        Debug.Log($"[UIWindowGraph:{context}] Override: removed {removed} global transitions with trigger '{transition.trigger}'");
                }
                globalTransitions.Add(transition);
                Debug.Log($"[UIWindowGraph:{context}] Added global transition: * --({transition.trigger})--> {transition.toWindowId}");
            }
            else
            {
                // Локальный переход
                if (allowOverride)
                {
                    int removed = transitions.RemoveAll(t => 
                        t.fromWindowId == transition.fromWindowId && 
                        t.trigger == transition.trigger);
                    if (removed > 0)
                        Debug.Log($"[UIWindowGraph:{context}] Override: removed {removed} transitions from '{transition.fromWindowId}' with trigger '{transition.trigger}'");
                }
                transitions.Add(transition);
                Debug.Log($"[UIWindowGraph:{context}] Added transition: {transition.fromWindowId} --({transition.trigger})--> {transition.toWindowId}");
            }
        }

        /// <summary>
        /// Завершить сборку графа
        /// </summary>
        public void FinalizeBuild()
        {
            lastBuildTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            windowCount = windows.Count;
            transitionCount = transitions.Count + globalTransitions.Count;
            
            // Автоопределение startWindowId если не задан
            if (string.IsNullOrEmpty(startWindowId) && windows.Count > 0)
            {
                var mainMenu = windows.FirstOrDefault(w => 
                    w.id.Contains("MainMenu") || w.id.Contains("Main"));
                    
                if (mainMenu != null)
                    startWindowId = mainMenu.id;
                else
                {
                    var firstNormal = windows.FirstOrDefault(w => w.type == WindowType.Normal);
                    if (firstNormal != null)
                        startWindowId = firstNormal.id;
                }
            }
            
            InvalidateCache();
        }

        #endregion
    }

    /// <summary>
    /// Определение окна
    /// </summary>
    [Serializable]
    public class WindowDefinition
    {
        [Tooltip("Уникальный ID окна")]
        public string id;
        
        [Tooltip("Префаб окна")]
        public GameObject prefab;
        
        [Tooltip("Тип окна")]
        public WindowType type = WindowType.Normal;
        
        [Tooltip("Слой отображения")]
        public WindowLayer layer = WindowLayer.Windows;
        
        [Tooltip("Уровень иерархии. При открытии окна уровня N все Normal окна с level <= N закрываются.")]
        public int level = 0;
        
        [Tooltip("Ставить игру на паузу")]
        public bool pauseGame;
        
        [Tooltip("Режим курсора при открытии окна")]
        public WindowCursorMode cursorMode = WindowCursorMode.Visible;
        
        [Tooltip("Скрывать окна ниже (deprecated)")]
        public bool hideBelow = true;
        
        [Tooltip("Разрешить закрытие через Back")]
        public bool allowBack = true;

        [Tooltip("Имя типа класса окна")]
        public string typeName;

        // Для визуального редактора
        [HideInInspector] public Vector2 editorPosition;
    }

    /// <summary>
    /// Определение перехода
    /// </summary>
    [Serializable]
    public class TransitionDefinition
    {
        [Tooltip("ID исходного окна (пусто = глобальный)")]
        public string fromWindowId;
        
        [Tooltip("ID целевого окна")]
        public string toWindowId;
        
        [Tooltip("Триггер для Navigate()")]
        public string trigger;
        
        [Tooltip("Анимация перехода")]
        public TransitionAnimation animation = TransitionAnimation.Fade;
    }
}
