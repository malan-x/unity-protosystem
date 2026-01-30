using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Effects
{
    /// <summary>
    /// Компонент для объектов, которые могут быть целью эффектов.
    /// Позволяет настраивать точки привязки эффектов в инспекторе.
    /// 
    /// Поддерживает переадресацию событий:
    /// - reactToEvents: события, на которые компонент реагирует
    /// - forwardEvents: события, которые компонент публикует с данными о себе
    /// 
    /// Это позволяет добавлять эффекты на любой объект без правки кода.
    /// </summary>
    public class EffectTargetComponent : MonoEventBus, IEffectTargetMultiPoint
    {
        [Header("Точки привязки эффектов")]
        [Tooltip("Основная точка привязки (Transform объекта по умолчанию)")]
        [SerializeField] private Transform defaultAttachPoint;

        [Tooltip("Дополнительные точки привязки (кости, сокеты)")]
        [SerializeField] private EffectAttachPoint[] attachPoints;

        [Header("Настройки по умолчанию")]
        [Tooltip("Смещение по умолчанию для эффектов")]
        [SerializeField] private Vector3 defaultOffset = Vector3.zero;

        [Tooltip("Масштаб эффектов по умолчанию")]
        [SerializeField] private float defaultScale = 1f;

        [Header("Переадресация событий")]
        [Tooltip("События, на которые компонент реагирует")]
        [SerializeField] private string[] reactToEvents;

        [Tooltip("События, которые компонент публикует при срабатывании")]
        [SerializeField] private string[] forwardEvents;

        [Tooltip("Точка привязки для переадресуемых событий")]
        [SerializeField] private string forwardAttachPoint = "default";

        /// <summary>
        /// Структура для точки привязки эффекта
        /// </summary>
        [System.Serializable]
        public struct EffectAttachPoint
        {
            [Tooltip("Имя точки (для поиска по имени)")]
            public string name;
            
            [Tooltip("Transform точки привязки")]
            public Transform transform;
            
            [Tooltip("Локальное смещение")]
            public Vector3 localOffset;
        }

        // Кеш для быстрого поиска
        private Dictionary<string, EffectAttachPoint> _attachPointCache;
        
        // Кеш ID событий
        private HashSet<int> _reactEventIds;
        private List<int> _forwardEventIds;
        
        // Флаг инициализации
        private bool _isInitialized;

        protected override void InitEvents()
        {
            BuildCache();
            ValidateAndInitializeEvents();
        }

        private void BuildCache()
        {
            _attachPointCache = new Dictionary<string, EffectAttachPoint>();
            if (attachPoints != null)
            {
                foreach (var point in attachPoints)
                {
                    if (!string.IsNullOrEmpty(point.name) && point.transform != null)
                    {
                        _attachPointCache[point.name] = point;
                    }
                }
            }
        }

        /// <summary>
        /// Валидирует события и удаляет зацикленные
        /// </summary>
        private void ValidateAndInitializeEvents()
        {
            _reactEventIds = new HashSet<int>();
            _forwardEventIds = new List<int>();

            // Собираем ID событий для реакции
            if (reactToEvents != null)
            {
                foreach (var eventPath in reactToEvents)
                {
                    if (string.IsNullOrEmpty(eventPath)) continue;
                    
                    var eventId = EventPathResolver.Resolve(eventPath);
                    if (eventId > 0)
                    {
                        _reactEventIds.Add(eventId);
                    }
                    else
                    {
                        ProtoLogger.Log("effects_manager", LogCategory.Runtime, LogLevel.Warnings, $"Событие '{eventPath}' не найдено на {name}");
                    }
                }
            }

            // Собираем ID событий для переадресации, исключая зацикленные
            if (forwardEvents != null)
            {
                foreach (var eventPath in forwardEvents)
                {
                    if (string.IsNullOrEmpty(eventPath)) continue;
                    
                    var eventId = EventPathResolver.Resolve(eventPath);
                    if (eventId <= 0)
                    {
                        ProtoLogger.Log("effects_manager", LogCategory.Runtime, LogLevel.Warnings, $"Событие переадресации '{eventPath}' не найдено на {name}");
                        continue;
                    }

                    // Проверка на зацикливание
                    if (_reactEventIds.Contains(eventId))
                    {
                        ProtoLogger.Log("effects_manager", LogCategory.Runtime, LogLevel.Errors, $"ЗАЦИКЛИВАНИЕ! Событие '{eventPath}' есть и во входных, и в выходных событиях на {name}. Событие пропущено.");
                        continue;
                    }

                    _forwardEventIds.Add(eventId);
                }
            }

            // Регистрируем события для автоматической подписки через MonoEventBus
            foreach (var eventId in _reactEventIds)
            {
                AddEvent(eventId, OnReactEvent);
            }
            _isInitialized = true;
        }

        /// <summary>
        /// Обработчик входящих событий
        /// </summary>
        private void OnReactEvent(object data)
        {
            // Проверяем, относится ли событие к нам
            // Если данные содержат ссылку на объект - проверяем что это мы
            if (data != null)
            {
                // Проверяем разные форматы данных
                Transform dataTransform = null;
                
                if (data is Component comp)
                    dataTransform = comp.transform;
                else if (data is GameObject go)
                    dataTransform = go.transform;
                else if (data is EffectEventData effectData && effectData.SpawnData.AttachTarget != null)
                    dataTransform = effectData.SpawnData.AttachTarget;
                else if (data is EffectSpawnData spawnData && spawnData.AttachTarget != null)
                    dataTransform = spawnData.AttachTarget;

                // Если событие содержит ссылку на объект, проверяем что это наш объект
                if (dataTransform != null && dataTransform != transform && !dataTransform.IsChildOf(transform))
                {
                    return; // Событие не для нас
                }
            }

            // Переадресуем события
            ForwardEvents();
        }

        /// <summary>
        /// Публикует все события переадресации с данными о себе
        /// </summary>
        private void ForwardEvents()
        {
            if (_forwardEventIds == null || _forwardEventIds.Count == 0) return;

            // Получаем данные для спавна
            var spawnData = GetEffectSpawnData(forwardAttachPoint);
            var eventData = new EffectEventData(spawnData);

            // Публикуем все события переадресации
            foreach (var eventId in _forwardEventIds)
            {
                EventBus.Publish(eventId, eventData);
            }
        }

        /// <summary>
        /// Вручную вызвать переадресацию событий (для использования из кода/анимаций)
        /// </summary>
        public void TriggerForwardEvents()
        {
            ForwardEvents();
        }

        /// <summary>
        /// Вручную вызвать переадресацию с указанной точкой привязки
        /// </summary>
        public void TriggerForwardEvents(string attachPoint)
        {
            if (_forwardEventIds == null || _forwardEventIds.Count == 0) return;

            var spawnData = GetEffectSpawnData(attachPoint);
            var eventData = new EffectEventData(spawnData);

            foreach (var eventId in _forwardEventIds)
            {
                EventBus.Publish(eventId, eventData);
            }
        }

        /// <summary>
        /// Точка привязки по умолчанию
        /// </summary>
        public string DefaultAttachPoint => "default";

        /// <summary>
        /// Получает данные для спавна эффекта (точка по умолчанию)
        /// </summary>
        public EffectSpawnData GetEffectSpawnData()
        {
            var attachTarget = defaultAttachPoint != null ? defaultAttachPoint : transform;
            
            return new EffectSpawnData
            {
                WorldPosition = attachTarget.position + attachTarget.TransformDirection(defaultOffset),
                WorldRotation = attachTarget.rotation,
                Scale = Vector3.one * defaultScale,
                AttachTarget = attachTarget,
                LocalOffset = defaultOffset
            };
        }

        /// <summary>
        /// Получает данные для спавна эффекта на указанной точке
        /// </summary>
        public EffectSpawnData GetEffectSpawnData(string attachPointName)
        {
            if (string.IsNullOrEmpty(attachPointName) || attachPointName == "default")
            {
                return GetEffectSpawnData();
            }

            // Проверяем кеш
            if (_attachPointCache == null) BuildCache();

            if (_attachPointCache.TryGetValue(attachPointName, out var point))
            {
                return new EffectSpawnData
                {
                    WorldPosition = point.transform.position + point.transform.TransformDirection(point.localOffset),
                    WorldRotation = point.transform.rotation,
                    Scale = Vector3.one * defaultScale,
                    AttachTarget = point.transform,
                    AttachBoneName = attachPointName,
                    LocalOffset = point.localOffset
                };
            }

            // Пробуем найти кость по имени в Animator
            var animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                var bone = FindBoneByName(animator.transform, attachPointName);
                if (bone != null)
                {
                    return new EffectSpawnData
                    {
                        WorldPosition = bone.position,
                        WorldRotation = bone.rotation,
                        Scale = Vector3.one * defaultScale,
                        AttachTarget = bone,
                        AttachBoneName = attachPointName
                    };
                }
            }

            // Fallback на дефолт
            ProtoLogger.Log("effects_manager", LogCategory.Runtime, LogLevel.Warnings, $"Точка привязки '{attachPointName}' не найдена на {name}, используем default");
            return GetEffectSpawnData();
        }

        /// <summary>
        /// Возвращает список доступных точек привязки
        /// </summary>
        public string[] GetAvailableAttachPoints()
        {
            var points = new List<string> { "default" };

            if (attachPoints != null)
            {
                foreach (var point in attachPoints)
                {
                    if (!string.IsNullOrEmpty(point.name))
                    {
                        points.Add(point.name);
                    }
                }
            }

            // Добавляем кости из аниматора
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                points.Add("Head");
                points.Add("Spine");
                points.Add("Hips");
                points.Add("LeftHand");
                points.Add("RightHand");
                points.Add("LeftFoot");
                points.Add("RightFoot");
            }

            return points.ToArray();
        }

        private Transform FindBoneByName(Transform root, string boneName)
        {
            if (root.name == boneName) return root;

            foreach (Transform child in root)
            {
                var found = FindBoneByName(child, boneName);
                if (found != null) return found;
            }

            return null;
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Проверяет настройки на ошибки (для Editor)
        /// </summary>
        public bool HasLoopWarning(out string[] loopedEvents)
        {
            var looped = new List<string>();
            
            if (reactToEvents == null || forwardEvents == null)
            {
                loopedEvents = looped.ToArray();
                return false;
            }

            var reactSet = new HashSet<string>(reactToEvents);
            foreach (var fwd in forwardEvents)
            {
                if (!string.IsNullOrEmpty(fwd) && reactSet.Contains(fwd))
                {
                    looped.Add(fwd);
                }
            }

            loopedEvents = looped.ToArray();
            return looped.Count > 0;
        }

        private void OnDrawGizmosSelected()
        {
            // Рисуем точки привязки
            Gizmos.color = Color.cyan;
            
            var defaultTarget = defaultAttachPoint != null ? defaultAttachPoint : transform;
            var pos = defaultTarget.position + defaultTarget.TransformDirection(defaultOffset);
            Gizmos.DrawWireSphere(pos, 0.1f);
            UnityEditor.Handles.Label(pos, "Default");

            if (attachPoints != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var point in attachPoints)
                {
                    if (point.transform != null)
                    {
                        var pointPos = point.transform.position + point.transform.TransformDirection(point.localOffset);
                        Gizmos.DrawWireSphere(pointPos, 0.05f);
                        UnityEditor.Handles.Label(pointPos, point.name);
                    }
                }
            }
        }

        private void OnValidate()
        {
            // Проверка на зацикливание в Editor
            if (HasLoopWarning(out var looped))
            {
                ProtoLogger.Log("effects_manager", LogCategory.Runtime, LogLevel.Warnings, $"Обнаружено зацикливание событий на {name}: {string.Join(", ", looped)}. Эти события будут проигнорированы при запуске.");
            }
        }
        #endif
    }
}
