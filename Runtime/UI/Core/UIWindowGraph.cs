// Packages/com.protosystem.core/Runtime/UI/Core/UIWindowGraph.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Граф UI окон и переходов между ними.
    /// Комбинирует данные из ScriptableObject и атрибутов в коде.
    /// </summary>
    [CreateAssetMenu(fileName = "UIWindowGraph", menuName = "ProtoSystem/UI/Window Graph")]
    public class UIWindowGraph : ScriptableObject
    {
        [Header("Entry Point")]
        [Tooltip("ID стартового окна")]
        public string startWindowId = "MainMenu";

        [Header("Windows (from Inspector)")]
        [Tooltip("Окна, определённые в Inspector")]
        public List<WindowDefinition> windows = new();

        [Header("Transitions (from Inspector)")]
        [Tooltip("Переходы, определённые в Inspector")]
        public List<TransitionDefinition> transitions = new();

        [Header("Global Transitions")]
        [Tooltip("Переходы доступные из любого окна")]
        public List<TransitionDefinition> globalTransitions = new();

        // Кэш для быстрого доступа
        private Dictionary<string, WindowDefinition> _windowCache;
        private Dictionary<string, List<TransitionDefinition>> _transitionCache;
        private List<TransitionDefinition> _globalTransitionCache;
        private bool _cacheValid;

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
        public IEnumerable<WindowDefinition> GetAllWindows()
        {
            EnsureCache();
            return _windowCache.Values;
        }

        /// <summary>
        /// Получить все переходы из окна
        /// </summary>
        public IEnumerable<TransitionDefinition> GetTransitionsFrom(string windowId)
        {
            EnsureCache();
            
            var result = new List<TransitionDefinition>();
            
            // Локальные переходы
            if (_transitionCache.TryGetValue(windowId, out var local))
                result.AddRange(local);
            
            // Глобальные переходы
            result.AddRange(_globalTransitionCache);
            
            return result;
        }

        /// <summary>
        /// Найти переход по триггеру из текущего окна
        /// </summary>
        public TransitionDefinition FindTransition(string fromWindowId, string trigger)
        {
            EnsureCache();

            // Сначала ищем в локальных
            if (_transitionCache.TryGetValue(fromWindowId, out var local))
            {
                var found = local.FirstOrDefault(t => t.trigger == trigger);
                if (found != null) return found;
            }

            // Затем в глобальных
            return _globalTransitionCache.FirstOrDefault(t => t.trigger == trigger);
        }

        /// <summary>
        /// Проверить, разрешён ли переход
        /// </summary>
        public bool IsTransitionAllowed(string fromWindowId, string toWindowId)
        {
            var transitions = GetTransitionsFrom(fromWindowId);
            return transitions.Any(t => t.toWindowId == toWindowId);
        }

        /// <summary>
        /// Зарегистрировать окно в runtime
        /// </summary>
        public void RegisterWindow(WindowDefinition window)
        {
            if (window == null || string.IsNullOrEmpty(window.id)) return;
            
            // Удаляем существующее с таким же ID
            windows.RemoveAll(w => w.id == window.id);
            windows.Add(window);
            
            InvalidateCache();
        }

        /// <summary>
        /// Зарегистрировать переход в runtime
        /// </summary>
        public void RegisterTransition(TransitionDefinition transition)
        {
            if (transition == null) return;
            
            if (string.IsNullOrEmpty(transition.fromWindowId))
            {
                globalTransitions.Add(transition);
            }
            else
            {
                transitions.Add(transition);
            }
            
            InvalidateCache();
        }

        /// <summary>
        /// Валидация графа
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult { isValid = true };

            // Проверка стартового окна
            if (string.IsNullOrEmpty(startWindowId))
            {
                result.errors.Add("Start window ID is empty");
                result.isValid = false;
            }
            else if (GetWindow(startWindowId) == null)
            {
                result.errors.Add($"Start window '{startWindowId}' not found");
                result.isValid = false;
            }

            // Проверка окон
            var windowIds = new HashSet<string>();
            foreach (var window in windows)
            {
                if (string.IsNullOrEmpty(window.id))
                {
                    result.errors.Add("Window with empty ID found");
                    result.isValid = false;
                    continue;
                }

                if (!windowIds.Add(window.id))
                {
                    result.errors.Add($"Duplicate window ID: {window.id}");
                    result.isValid = false;
                }

                if (window.prefab == null)
                {
                    result.warnings.Add($"Window '{window.id}' has no prefab assigned");
                }
            }

            // Проверка переходов
            var triggerSet = new HashSet<string>();
            foreach (var transition in transitions.Concat(globalTransitions))
            {
                if (string.IsNullOrEmpty(transition.toWindowId))
                {
                    result.errors.Add($"Transition with trigger '{transition.trigger}' has no target window");
                    result.isValid = false;
                    continue;
                }

                if (GetWindow(transition.toWindowId) == null)
                {
                    result.errors.Add($"Transition target '{transition.toWindowId}' not found");
                    result.isValid = false;
                }

                // Проверка уникальности триггера в контексте окна
                string key = $"{transition.fromWindowId ?? "global"}:{transition.trigger}";
                if (!triggerSet.Add(key))
                {
                    result.warnings.Add($"Duplicate trigger '{transition.trigger}' from '{transition.fromWindowId ?? "global"}'");
                }
            }

            // Проверка достижимости (все окна должны быть достижимы из startWindow)
            var reachable = new HashSet<string>();
            CollectReachable(startWindowId, reachable);
            
            foreach (var window in windows)
            {
                if (!reachable.Contains(window.id) && window.id != startWindowId)
                {
                    result.warnings.Add($"Window '{window.id}' is not reachable from start");
                }
            }

            return result;
        }

        #endregion

        #region Private Methods

        private void EnsureCache()
        {
            if (_cacheValid) return;

            _windowCache = new Dictionary<string, WindowDefinition>();
            _transitionCache = new Dictionary<string, List<TransitionDefinition>>();
            _globalTransitionCache = new List<TransitionDefinition>();

            // Кэшируем окна
            foreach (var window in windows)
            {
                if (!string.IsNullOrEmpty(window.id))
                    _windowCache[window.id] = window;
            }

            // Кэшируем переходы
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

            // Глобальные переходы
            _globalTransitionCache.AddRange(globalTransitions);

            _cacheValid = true;
        }

        private void InvalidateCache()
        {
            _cacheValid = false;
        }

        private void CollectReachable(string windowId, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(windowId) || visited.Contains(windowId)) return;
            
            visited.Add(windowId);

            foreach (var transition in GetTransitionsFrom(windowId))
            {
                CollectReachable(transition.toWindowId, visited);
            }
        }

        private void OnValidate()
        {
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
        
        [Tooltip("Ставить игру на паузу")]
        public bool pauseGame;
        
        [Tooltip("Скрывать окна ниже")]
        public bool hideBelow = true;
        
        [Tooltip("Разрешить закрытие через Back")]
        public bool allowBack = true;

        [HideInInspector]
        public Vector2 editorPosition; // Для визуального редактора
        
        [HideInInspector]
        public bool fromCode; // Помечает что окно из атрибутов
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

        [HideInInspector]
        public bool fromCode; // Помечает что переход из атрибутов
    }

    /// <summary>
    /// Результат валидации графа
    /// </summary>
    public class ValidationResult
    {
        public bool isValid = true;
        public List<string> errors = new();
        public List<string> warnings = new();

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(isValid ? "✓ Graph is valid" : "✗ Graph has errors");
            
            foreach (var error in errors)
                sb.AppendLine($"  ERROR: {error}");
            foreach (var warning in warnings)
                sb.AppendLine($"  WARNING: {warning}");
            
            return sb.ToString();
        }
    }
}
