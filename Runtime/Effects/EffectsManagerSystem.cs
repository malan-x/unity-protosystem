using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using ProtoSystem;

// Опциональная интеграция с MoreMountains.Tools для аудио
#if MM_TOOLS_FOR_EFFECTS
using MoreMountains.Tools;
#endif

namespace ProtoSystem.Effects
{
    /// <summary>
    /// Менеджер эффектов как часть ProtoSystem.
    /// Управляет пулом эффектов, конфигурацией через ScriptableObject,
    /// интеграцией с EventBus для триггеров.
    /// 
    /// Для использования MMSoundManager добавьте define symbol: MM_TOOLS_FOR_EFFECTS
    /// </summary>
    [ProtoSystemComponent("Effects Manager", "Система управления визуальными и звуковыми эффектами", "Effects", "✨", 20)]
    public class EffectsManagerSystem : InitializableSystemBase
    {
        public override string SystemId => "effects_manager";
        public override string DisplayName => "Effects Manager";
        public override string Description => "Воспроизводит VFX, аудио и UI-эффекты с пулингом объектов и автоматическими триггерами.";

        [Header("Пул эффектов")]
        [Tooltip("Максимальный размер пула эффектов")]
        [SerializeField] private int maxPoolSize = 50;

        [Tooltip("Начальный размер пула")]
        [SerializeField] private int initialPoolSize = 10;

        [Header("Конфигурация эффектов")]
        [Tooltip("Контейнер с эффектами для этого менеджера")]
        [SerializeField] private EffectContainer effectContainer;

        [Header("UI эффекты")]
        [Tooltip("Canvas для UI эффектов. Если не назначен, создаётся автоматически.")]
        [SerializeField] private Canvas uiEffectsCanvas;

        [Header("Аудио (без MoreMountains)")]
        [Tooltip("AudioSource для воспроизведения звуков. Создаётся автоматически если не назначен.")]
        [SerializeField] private AudioSource audioSource;

        // Пул для переиспользования инстансов эффектов
        private ObjectPool<EffectInstance> _effectPool;

        // Активные эффекты для управления
        private Dictionary<string, List<EffectInstance>> _activeEffects = new();

        // Кэш, чтобы не аллоцировать каждый кадр
        private readonly List<string> _effectsToAutoStop = new();

        // Автоматические триггеры эффектов (eventId -> список эффектов)
        private Dictionary<int, List<EffectConfig>> _autoTriggerEffects = new();
        private Dictionary<int, List<EffectConfig>> _autoStopEffects = new();

        // Класс события эффектов - нужно определить в проекте
        private int _playEventId;
        private int _stopEventId;
        private int _stopAllEventId;

        protected override void InitEvents()
        {
            // Пытаемся найти события эффектов через рефлексию
            _playEventId = EventPathResolver.Resolve("Эффекты.Воспроизвести");
            _stopEventId = EventPathResolver.Resolve("Эффекты.Остановить");
            _stopAllEventId = EventPathResolver.Resolve("Эффекты.Остановить_все");

            // Подписка на события эффектов если они определены
            if (_playEventId > 0) AddEvent(_playEventId, OnPlayEffect);
            if (_stopEventId > 0) AddEvent(_stopEventId, OnStopEffect);
            if (_stopAllEventId > 0) AddEvent(_stopAllEventId, OnStopAllEffects);

            SetupAutoTriggers();
        }

        public override System.Threading.Tasks.Task<bool> InitializeAsync()
        {
            // Проверить контейнер эффектов
            if (effectContainer == null)
            {
                Debug.LogError("[EffectsManagerSystem] EffectContainer не назначен!");
                return System.Threading.Tasks.Task.FromResult(false);
            }

            if (!effectContainer.IsValid())
            {
                Debug.LogError($"[EffectsManagerSystem] EffectContainer '{effectContainer.ContainerName}' содержит ошибки!");
                return System.Threading.Tasks.Task.FromResult(false);
            }
            InitializePool();
            EnsureUICanvas();
            EnsureAudioSource();
            ReportProgress(1f);
            return System.Threading.Tasks.Task.FromResult(true);
        }

        private void OnDestroy()
        {
            // Очищаем статический кеш инстансов при уничтожении менеджера
            EffectInstance.ClearCache();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (_activeEffects.Count == 0) return;

            var now = Time.time;
            _effectsToAutoStop.Clear();

            foreach (var kvp in _activeEffects)
            {
                var list = kvp.Value;
                if (list == null || list.Count == 0)
                {
                    _effectsToAutoStop.Add(kvp.Key);
                    continue;
                }

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var instance = list[i];
                    var autoReleaseAt = instance.AutoReleaseAt;
                    if (autoReleaseAt > 0f && now >= autoReleaseAt)
                    {
                        if (_effectPool == null)
                        {
                            InitializePool();
                        }
                        instance.Stop();
                        _effectPool.Release(instance);
                        list.RemoveAt(i);
                    }
                }

                if (list.Count == 0)
                {
                    _effectsToAutoStop.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _effectsToAutoStop.Count; i++)
            {
                _activeEffects.Remove(_effectsToAutoStop[i]);
            }
        }

        /// <summary>
        /// Создаёт Canvas для UI эффектов, если он не назначен
        /// </summary>
        private void EnsureUICanvas()
        {
            if (uiEffectsCanvas != null) return;

            // Создаём дочерний объект с Canvas
            var canvasGO = new GameObject("[UI Effects Canvas]");
            canvasGO.transform.SetParent(transform, false);

            // Canvas
            uiEffectsCanvas = canvasGO.AddComponent<Canvas>();
            uiEffectsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiEffectsCanvas.sortingOrder = 100; // Поверх остального UI

            // CanvasScaler для корректного масштабирования
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // GraphicRaycaster для интерактивности (если UI эффекты интерактивные)
            canvasGO.AddComponent<GraphicRaycaster>();

            Debug.Log("[EffectsManagerSystem] Создан Canvas для UI эффектов");
        }

        /// <summary>
        /// Создаёт AudioSource если не назначен (для режима без MMSoundManager)
        /// </summary>
        private void EnsureAudioSource()
        {
#if !MM_TOOLS_FOR_EFFECTS
            if (audioSource != null) return;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            Debug.Log("[EffectsManagerSystem] Создан AudioSource для эффектов");
#endif
        }

        /// <summary>
        /// Возвращает Canvas для UI эффектов (создаёт если нужно)
        /// </summary>
        public Canvas GetUICanvas()
        {
            EnsureUICanvas();
            return uiEffectsCanvas;
        }

        /// <summary>
        /// Возвращает AudioSource для аудио эффектов
        /// </summary>
        public AudioSource GetAudioSource()
        {
            EnsureAudioSource();
            return audioSource;
        }

        private void InitializePool()
        {
            // Устанавливаем корень кеша для инстансов
            EffectInstance.SetCacheRoot(transform);
            EffectInstance.SetEffectsManagerSystem(this);

            // Инициализация пула
            _effectPool = new ObjectPool<EffectInstance>(
                createFunc: () => new EffectInstance(),
                actionOnGet: (effect) => effect.Reset(),
                actionOnRelease: (effect) => effect.Cleanup(),
                actionOnDestroy: (effect) => effect.Destroy(),
                collectionCheck: false,
                defaultCapacity: initialPoolSize,
                maxSize: maxPoolSize
            );

            // Предварительное заполнение пула
            var prewarmEffects = new EffectInstance[initialPoolSize];
            for (int i = 0; i < initialPoolSize; i++)
            {
                prewarmEffects[i] = _effectPool.Get();
                _effectPool.Release(prewarmEffects[i]);
            }
        }

        /// <summary>
        /// Настраивает автоматические триггеры эффектов на основе конфигураций
        /// </summary>
        private void SetupAutoTriggers()
        {
            if (effectContainer == null || effectContainer.Effects == null)
                return;

            _autoTriggerEffects.Clear();
            _autoStopEffects.Clear();

            foreach (var effectConfig in effectContainer.Effects)
            {
                if (effectConfig == null) continue;

                // Настраиваем триггер запуска (используем новый метод GetTriggerEventId)
                if (effectConfig.HasAutoTrigger())
                {
                    var triggerEventId = effectConfig.GetTriggerEventId();
                    if (triggerEventId > 0)
                    {
                        if (!_autoTriggerEffects.ContainsKey(triggerEventId))
                            _autoTriggerEffects[triggerEventId] = new List<EffectConfig>();

                        _autoTriggerEffects[triggerEventId].Add(effectConfig);
                    }
                }

                // Настраиваем триггер остановки (используем новый метод GetStopEventId)
                if (effectConfig.HasAutoStop())
                {
                    var stopEventId = effectConfig.GetStopEventId();
                    if (stopEventId > 0)
                    {
                        if (!_autoStopEffects.ContainsKey(stopEventId))
                            _autoStopEffects[stopEventId] = new List<EffectConfig>();

                        _autoStopEffects[stopEventId].Add(effectConfig);
                    }
                }
            }

            // Подписываемся на все уникальные события триггеров запуска
            foreach (var eventId in _autoTriggerEffects.Keys)
            {
                AddEvent(eventId, data => OnAutoTriggerEffect(eventId, data));
            }

            // Подписываемся на все уникальные события триггеров остановки
            foreach (var eventId in _autoStopEffects.Keys)
            {
                AddEvent(eventId, data => OnAutoStopEffect(eventId, data));
            }

            Debug.Log($"[EffectsManagerSystem] Настроено {_autoTriggerEffects.Count} триггеров запуска и {_autoStopEffects.Count} триггеров остановки");
        }

        /// <summary>
        /// Воспроизвести эффект по ID
        /// </summary>
        public void PlayEffect(string effectId, Vector3 position = default, Transform parent = null)
        {
            var config = GetEffectConfig(effectId);
            if (config == null)
            {
                Debug.LogWarning($"[EffectsManagerSystem] Эффект '{effectId}' не найден!");
                return;
            }

            PlayEffect(config, position, parent);
        }

        /// <summary>
        /// Воспроизвести эффект по конфигурации
        /// </summary>
        public void PlayEffect(EffectConfig config, Vector3 position = default, Transform parent = null)
        {
            if (config == null) return;

            if (_effectPool == null)
            {
                InitializePool();
            }

            // UI-эффекты спавнятся на Canvas
            if (config.effectType == EffectConfig.EffectType.UI || 
                (config.effectType == EffectConfig.EffectType.Combined && config.uiPrefab != null))
            {
                if (parent == null)
                {
                    EnsureUICanvas();
                    parent = uiEffectsCanvas.transform;
                }
            }
            // WorldSpace-эффекты по умолчанию живут под менеджером, а не в корне сцены
            else if (parent == null && config.spaceMode == EffectSpaceMode.WorldSpace &&
                (config.effectType == EffectConfig.EffectType.VFX || config.effectType == EffectConfig.EffectType.Particle || config.effectType == EffectConfig.EffectType.Combined || config.category == EffectCategory.Spatial))
            {
                parent = transform;
            }

            // Получить инстанс из пула
            var effectInstance = _effectPool.Get();
            effectInstance.Initialize(config, position, parent, transform);

            // Запустить эффект
            effectInstance.Play();

            // Зарегистрировать как активный
            if (!_activeEffects.TryGetValue(config.effectId, out var list) || list == null)
            {
                list = new List<EffectInstance>(2);
                _activeEffects[config.effectId] = list;
            }
            list.Add(effectInstance);
        }

        /// <summary>
        /// Остановить эффект по ID
        /// </summary>
        public void StopEffect(string effectId)
        {
            if (_activeEffects.TryGetValue(effectId, out var list) && list != null)
            {
                if (_effectPool == null)
                {
                    InitializePool();
                }

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var effectInstance = list[i];
                    effectInstance.Stop();
                    _effectPool.Release(effectInstance);
                }

                list.Clear();
                _activeEffects.Remove(effectId);
            }
        }

        /// <summary>
        /// Остановить все эффекты
        /// </summary>
        public void StopAllEffects()
        {
            if (_effectPool == null)
            {
                InitializePool();
            }
            foreach (var kvp in _activeEffects)
            {
                var list = kvp.Value;
                if (list == null) continue;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var instance = list[i];
                    instance.Stop();
                    _effectPool.Release(instance);
                }
            }
            _activeEffects.Clear();
        }

        /// <summary>
        /// Проверить, активен ли эффект
        /// </summary>
        public bool IsEffectActive(string effectId)
        {
            return _activeEffects.TryGetValue(effectId, out var list) && list != null && list.Count > 0;
        }

        /// <summary>
        /// Получить конфигурацию эффекта по ID (для отладки и тестирования)
        /// </summary>
        public EffectConfig GetEffectConfig(string effectId)
        {
            return effectContainer?.FindEffect(effectId);
        }

        /// <summary>
        /// Добавить конфигурацию эффекта в контейнер (для динамического добавления)
        /// </summary>
        public void AddEffectConfig(EffectConfig config)
        {
            if (effectContainer != null && config != null)
            {
                effectContainer.AddEffect(config);
            }
        }

        /// <summary>
        /// Удалить конфигурацию эффекта из контейнера (для динамического удаления)
        /// </summary>
        public void RemoveEffectConfig(EffectConfig config)
        {
            if (effectContainer != null && config != null)
            {
                effectContainer.RemoveEffect(config);
            }
        }

        /// <summary>
        /// Установить контейнер эффектов
        /// </summary>
        public void SetEffectContainer(EffectContainer container)
        {
            effectContainer = container;
        }

        /// <summary>
        /// Получить текущий контейнер эффектов
        /// </summary>
        public EffectContainer GetEffectContainer()
        {
            return effectContainer;
        }

        // Обработчики событий EventBus

        private void OnPlayEffect(object data)
        {
            if (data is EffectPlayData playData)
            {
                PlayEffect(playData.effectId, playData.position, playData.parent);
            }
        }

        private void OnStopEffect(object data)
        {
            if (data is EffectStopData stopData)
            {
                StopEffect(stopData.effectId);
            }
        }

        private void OnStopAllEffects(object data)
        {
            StopAllEffects();
        }

        /// <summary>
        /// Обработчик автоматического запуска эффектов
        /// </summary>
        private void OnAutoTriggerEffect(int eventId, object data)
        {
            if (_autoTriggerEffects.TryGetValue(eventId, out var effects))
            {
                foreach (var effectConfig in effects)
                {
                    // Проверяем условие триггера если указано
                    if (!string.IsNullOrEmpty(effectConfig.triggerCondition) &&
                        !effectConfig.CheckTriggerCondition(data))
                    {
                        continue; // Условие не выполнено, пропускаем
                    }

                    // Определяем данные для спавна эффекта
                    EffectSpawnData spawnData = default;
                    bool hasSpawnData = false;

                    if (effectConfig.passEventData && data != null)
                    {
                        // Приоритет 1: EffectEventData (новый формат)
                        if (data is EffectEventData effectEventData)
                        {
                            spawnData = effectEventData.SpawnData;
                            hasSpawnData = true;
                        }
                        // Приоритет 2: EffectSpawnData напрямую
                        else if (data is EffectSpawnData directSpawnData)
                        {
                            spawnData = directSpawnData;
                            hasSpawnData = true;
                        }
                        // Приоритет 3: IEffectTarget — объект сам предоставляет данные
                        else if (data is IEffectTarget effectTarget)
                        {
                            spawnData = effectTarget.GetEffectSpawnData();
                            hasSpawnData = true;
                        }
                        // Fallback: legacy форматы
                        else if (data is Vector3 pos)
                        {
                            spawnData = EffectSpawnData.AtPosition(pos);
                            hasSpawnData = true;
                        }
                        else if (data is Transform trans)
                        {
                            // Проверяем есть ли IEffectTarget
                            var target = trans.GetComponent<IEffectTarget>();
                            if (target != null)
                            {
                                spawnData = target.GetEffectSpawnData();
                            }
                            else
                            {
                                spawnData = EffectSpawnData.AttachedTo(trans);
                            }
                            hasSpawnData = true;
                        }
                        else if (data is GameObject go)
                        {
                            // Проверяем есть ли IEffectTarget
                            var target = go.GetComponent<IEffectTarget>();
                            if (target != null)
                            {
                                spawnData = target.GetEffectSpawnData();
                            }
                            else
                            {
                                spawnData = EffectSpawnData.AttachedTo(go.transform);
                            }
                            hasSpawnData = true;
                        }
                        else if (data is Component comp)
                        {
                            // Проверяем есть ли IEffectTarget
                            var target = comp.GetComponent<IEffectTarget>();
                            if (target != null)
                            {
                                spawnData = target.GetEffectSpawnData();
                            }
                            else
                            {
                                spawnData = EffectSpawnData.AttachedTo(comp.transform);
                            }
                            hasSpawnData = true;
                        }
                    }

                    // Проверяем требования категории эффекта
                    if (effectConfig.RequiresEffectTarget() && !hasSpawnData)
                    {
                        Debug.LogWarning($"[EffectsManagerSystem] Эффект '{effectConfig.effectId}' требует IEffectTarget, но данные события не содержат позицию. Пропускаем.");
                        continue;
                    }

                    // Запускаем эффект с данными
                    PlayEffectWithSpawnData(effectConfig, hasSpawnData ? spawnData : default, hasSpawnData);
                    Debug.Log($"[EffectsManagerSystem] Автоматически запущен эффект '{effectConfig.effectId}' по событию {eventId}");
                }
            }
        }

        /// <summary>
        /// Воспроизводит эффект с учётом EffectSpawnData
        /// </summary>
        private void PlayEffectWithSpawnData(EffectConfig config, EffectSpawnData spawnData, bool hasSpawnData)
        {
            if (config == null) return;

            Vector3 position = hasSpawnData ? spawnData.WorldPosition : Vector3.zero;
            Transform parent = null;

            // Определяем родителя в зависимости от режима пространства
            if (config.spaceMode == EffectSpaceMode.LocalSpace && hasSpawnData && spawnData.AttachTarget != null)
            {
                // Для LocalSpace: находим кость если указана
                if (!string.IsNullOrEmpty(spawnData.AttachBoneName) || !string.IsNullOrEmpty(config.attachBoneName))
                {
                    var boneName = !string.IsNullOrEmpty(spawnData.AttachBoneName) 
                        ? spawnData.AttachBoneName 
                        : config.attachBoneName;
                    
                    parent = FindBone(spawnData.AttachTarget, boneName) ?? spawnData.AttachTarget;
                }
                else
                {
                    parent = spawnData.AttachTarget;
                }

                // Позиция с учётом локального смещения
                var offset = spawnData.LocalOffset != Vector3.zero ? spawnData.LocalOffset : config.localOffset;
                position = parent.position + parent.TransformDirection(offset);
            }

            // Воспроизводим эффект
            PlayEffect(config, position, parent);
        }

        /// <summary>
        /// Находит кость по имени в иерархии
        /// </summary>
        private Transform FindBone(Transform root, string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return null;
            if (root.name == boneName) return root;

            foreach (Transform child in root)
            {
                var found = FindBone(child, boneName);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// Обработчик автоматической остановки эффектов
        /// </summary>
        private void OnAutoStopEffect(int eventId, object data)
        {
            if (_autoStopEffects.TryGetValue(eventId, out var effects))
            {
                foreach (var effectConfig in effects)
                {
                    // Проверяем условие триггера если указано
                    if (!string.IsNullOrEmpty(effectConfig.triggerCondition) &&
                        !effectConfig.CheckTriggerCondition(data))
                    {
                        continue; // Условие не выполнено, пропускаем
                    }

                    // Останавливаем эффект
                    StopEffect(effectConfig.effectId);
                    Debug.Log($"[EffectsManagerSystem] Автоматически остановлен эффект '{effectConfig.effectId}' по событию {eventId}");
                }
            }
        }

        // Структуры данных для событий

        public struct EffectPlayData
        {
            public string effectId;
            public Vector3 position;
            public Transform parent;
        }

        public struct EffectStopData
        {
            public string effectId;
        }
    }

    /// <summary>
    /// Инстанс эффекта для пула
    /// </summary>
    public class EffectInstance
    {
        private EffectConfig _config;
        private GameObject _instance;
        private Transform _parent;
        private Vector3 _position;
        private float _startTime;

        private Transform _poolRoot;
        private Object _sourcePrefab;
        private float _autoReleaseAt;

        // Статический кеш инстансов по префабу для переиспользования
        private static Dictionary<Object, Queue<GameObject>> _prefabInstanceCache = new();
        private static Transform _cacheRoot;
        private static EffectsManagerSystem _effectsManager;

        // Анимация UI
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Coroutine _animationCoroutine;
        private MonoBehaviour _coroutineRunner;
        private Vector2 _targetPosition;
        private Vector3 _targetScale;
        private bool _isAnimating;

        public float AutoReleaseAt => _autoReleaseAt;
        public bool IsAnimating => _isAnimating;

        public void Initialize(EffectConfig config, Vector3 position, Transform parent, Transform poolRoot = null)
        {
            _config = config;
            _position = position;
            _parent = parent;
            _startTime = Time.time;
            _poolRoot = poolRoot;
            _autoReleaseAt = 0f;
            _isAnimating = false;
            
            // Получаем MonoBehaviour для корутин (используем EffectsManagerSystem если есть)
            if (_coroutineRunner == null && poolRoot != null)
            {
                _coroutineRunner = poolRoot.GetComponent<MonoBehaviour>();
            }
        }

        public void Play()
        {
            if (_config == null) return;

            switch (_config.effectType)
            {
                case EffectConfig.EffectType.VFX:
                    PlayVFX();
                    break;
                case EffectConfig.EffectType.Audio:
                    PlayAudio();
                    break;
                case EffectConfig.EffectType.UI:
                    PlayUI();
                    break;
                case EffectConfig.EffectType.Combined:
                    PlayVFX();
                    PlayAudio();
                    PlayUI();
                    break;
            }
        }

        public void Stop()
        {
            if (_instance != null)
            {
                // Для VFX - остановить ParticleSystem
                var particleSystem = _instance.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    particleSystem.Stop();
                }

                // Для Audio - остановить AudioSource
                var audioSource = _instance.GetComponent<AudioSource>();
                if (audioSource != null)
                {
                    audioSource.Stop();
                }

                // Для UI - скрыть
                _instance.SetActive(false);

                if (_poolRoot != null)
                {
                    _instance.transform.SetParent(_poolRoot, true);
                }
            }
        }

        public void Reset()
        {
            _config = null;
            _parent = null;
            _position = Vector3.zero;
            _startTime = 0f;
            _autoReleaseAt = 0f;
        }

        public void Cleanup()
        {
            if (_instance != null)
            {
                _instance.SetActive(false);
                
                // Возвращаем инстанс в кеш для переиспользования
                if (_sourcePrefab != null)
                {
                    if (!_prefabInstanceCache.TryGetValue(_sourcePrefab, out var queue))
                    {
                        queue = new Queue<GameObject>();
                        _prefabInstanceCache[_sourcePrefab] = queue;
                    }
                    
                    if (_poolRoot != null)
                    {
                        _instance.transform.SetParent(_poolRoot, false);
                    }
                    queue.Enqueue(_instance);
                }
                
                _instance = null;
            }
        }

        public void Destroy()
        {
            // Не уничтожаем инстанс - возвращаем в кеш через Cleanup
            Cleanup();
            _sourcePrefab = null;
        }

        /// <summary>
        /// Получает или создаёт инстанс для указанного префаба
        /// </summary>
        private GameObject GetOrCreateInstance(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            // Пробуем взять из кеша
            if (_prefabInstanceCache.TryGetValue(prefab, out var queue) && queue.Count > 0)
            {
                var cached = queue.Dequeue();
                if (cached != null)
                {
                    cached.transform.SetParent(parent, false);
                    cached.transform.position = position;
                    cached.transform.rotation = rotation;
                    cached.SetActive(true);
                    return cached;
                }
            }

            // Создаём новый
            return Object.Instantiate(prefab, position, rotation, parent);
        }

        /// <summary>
        /// Устанавливает корневой transform для кеша (вызывается из EffectsManagerSystem)
        /// </summary>
        public static void SetCacheRoot(Transform root)
        {
            _cacheRoot = root;
        }

        /// <summary>
        /// Устанавливает ссылку на EffectsManagerSystem
        /// </summary>
        public static void SetEffectsManagerSystem(EffectsManagerSystem manager)
        {
            _effectsManager = manager;
        }

        /// <summary>
        /// Очищает кеш инстансов (вызывать при смене сцены)
        /// </summary>
        public static void ClearCache()
        {
            foreach (var kvp in _prefabInstanceCache)
            {
                while (kvp.Value.Count > 0)
                {
                    var instance = kvp.Value.Dequeue();
                    if (instance != null)
                    {
                        Object.Destroy(instance);
                    }
                }
            }
            _prefabInstanceCache.Clear();
        }

        private void PlayVFX()
        {
            if (_config.vfxPrefab == null) return;

            // Если текущий инстанс от другого префаба - вернуть в кеш
            if (_instance != null && _sourcePrefab != _config.vfxPrefab)
            {
                ReturnInstanceToCache();
            }

            if (_instance == null)
            {
                _instance = GetOrCreateInstance(_config.vfxPrefab, _position, Quaternion.Euler(_config.rotation), _parent);
                _sourcePrefab = _config.vfxPrefab;
            }
            else
            {
                _instance.transform.SetParent(_parent, false);
                _instance.transform.position = _position;
                _instance.transform.rotation = Quaternion.Euler(_config.rotation);
                _instance.SetActive(true);
            }

            _instance.transform.localScale = _config.scale;

            var particleSystem = _instance.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }

            if (_config.lifetime > 0f)
            {
                _autoReleaseAt = _startTime + _config.lifetime;
            }
        }

        /// <summary>
        /// Возвращает текущий инстанс в кеш
        /// </summary>
        private void ReturnInstanceToCache()
        {
            if (_instance == null || _sourcePrefab == null) return;

            _instance.SetActive(false);
            
            if (!_prefabInstanceCache.TryGetValue(_sourcePrefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _prefabInstanceCache[_sourcePrefab] = queue;
            }
            
            if (_poolRoot != null)
            {
                _instance.transform.SetParent(_poolRoot, false);
            }
            queue.Enqueue(_instance);
            _instance = null;
        }

        private void PlayAudio()
        {
            if (_config.audioClip == null) return;

#if MM_TOOLS_FOR_EFFECTS
            // Использовать MMSoundManager для аудио
            var audioSource = MMSoundManager.Instance.PlaySound(
                _config.audioClip,
                _config.spatial ? MMSoundManager.MMSoundManagerTracks.Sfx : MMSoundManager.MMSoundManagerTracks.UI,
                _position,
                false, // loop
                _config.volume,
                0, // ID
                false, // fade
                0f, // fadeInitialVolume
                0f, // fadeDuration
                null, // fadeTween
                false, // persistent
                null, // recycleAudioSource
                null, // audioGroup
                _config.pitch,
                0f, // panStereo
                _config.spatial ? 1f : 0f // spatialBlend
            );

            // Сохранить ссылку на AudioSource для управления
            if (audioSource != null && _instance == null)
            {
                _instance = audioSource.gameObject;
            }
#else
            // Простой fallback без MMSoundManager
            if (_effectsManager != null)
            {
                var audioSource = _effectsManager.GetAudioSource();
                if (audioSource != null)
                {
                    audioSource.clip = _config.audioClip;
                    audioSource.volume = _config.volume;
                    audioSource.pitch = _config.pitch;
                    audioSource.spatialBlend = _config.spatial ? 1f : 0f;
                    audioSource.transform.position = _position;
                    audioSource.Play();
                }
            }
#endif

            if (_config.audioClip != null)
            {
                _autoReleaseAt = _startTime + Mathf.Max(0.01f, _config.audioClip.length);
            }
        }

        private void PlayUI()
        {
            if (_config.uiPrefab == null) return;

            // Если текущий инстанс от другого префаба - вернуть в кеш
            if (_instance != null && _sourcePrefab != _config.uiPrefab)
            {
                ReturnInstanceToCache();
            }

            if (_instance == null)
            {
                _instance = GetOrCreateInstance(_config.uiPrefab, _config.uiPosition, Quaternion.identity, _parent);
                _sourcePrefab = _config.uiPrefab;
            }
            else
            {
                _instance.transform.SetParent(_parent, false);
                _instance.SetActive(true);
            }

            // Кешируем компоненты
            _rectTransform = _instance.GetComponent<RectTransform>();
            _canvasGroup = _instance.GetComponent<CanvasGroup>();
            
            // Создаём CanvasGroup если нужна анимация с Fade
            if (_canvasGroup == null && NeedsFade(_config.uiShowAnimation, _config.uiHideAnimation))
            {
                _canvasGroup = _instance.AddComponent<CanvasGroup>();
            }

            // Сохраняем целевые значения
            _targetPosition = _config.uiPosition;
            _targetScale = _config.uiScale;

            // Устанавливаем начальное состояние для анимации
            if (_config.uiShowAnimation != UIAnimationType.None)
            {
                SetupShowAnimationStartState();
                
                // Запускаем анимацию появления
                if (_coroutineRunner != null)
                {
                    _isAnimating = true;
                    _animationCoroutine = _coroutineRunner.StartCoroutine(AnimateShow());
                }
                else
                {
                    // Без корутин - сразу финальное состояние
                    ApplyFinalShowState();
                }
            }
            else
            {
                // Без анимации - сразу финальное состояние
                ApplyFinalShowState();
            }

            // Время отображения + время анимации исчезновения
            if (_config.uiDisplayTime > 0f)
            {
                var totalTime = _config.uiDisplayTime + _config.uiShowDuration;
                if (_config.uiHideAnimation != UIAnimationType.None)
                {
                    totalTime += _config.uiHideDuration;
                }
                _autoReleaseAt = _startTime + totalTime;
            }
        }

        private bool NeedsFade(UIAnimationType show, UIAnimationType hide)
        {
            return show == UIAnimationType.Fade || show == UIAnimationType.ScaleAndFade ||
                   hide == UIAnimationType.Fade || hide == UIAnimationType.ScaleAndFade;
        }

        private void SetupShowAnimationStartState()
        {
            switch (_config.uiShowAnimation)
            {
                case UIAnimationType.Scale:
                case UIAnimationType.Bounce:
                    _instance.transform.localScale = Vector3.zero;
                    break;
                    
                case UIAnimationType.Fade:
                    if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                    _instance.transform.localScale = _targetScale;
                    break;
                    
                case UIAnimationType.ScaleAndFade:
                    _instance.transform.localScale = Vector3.zero;
                    if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                    break;
                    
                case UIAnimationType.SlideLeft:
                    if (_rectTransform != null)
                        _rectTransform.anchoredPosition = _targetPosition + new Vector2(300f, 0f);
                    break;
                    
                case UIAnimationType.SlideRight:
                    if (_rectTransform != null)
                        _rectTransform.anchoredPosition = _targetPosition + new Vector2(-300f, 0f);
                    break;
                    
                case UIAnimationType.SlideUp:
                    if (_rectTransform != null)
                        _rectTransform.anchoredPosition = _targetPosition + new Vector2(0f, -300f);
                    break;
                    
                case UIAnimationType.SlideDown:
                    if (_rectTransform != null)
                        _rectTransform.anchoredPosition = _targetPosition + new Vector2(0f, 300f);
                    break;
                    
                case UIAnimationType.Rotate:
                    _instance.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                    _instance.transform.localScale = Vector3.zero;
                    break;
            }
        }

        private void ApplyFinalShowState()
        {
            if (_rectTransform != null)
                _rectTransform.anchoredPosition = _targetPosition;
            _instance.transform.localScale = _targetScale;
            _instance.transform.localRotation = Quaternion.identity;
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        }

        private System.Collections.IEnumerator AnimateShow()
        {
            var startScale = _instance.transform.localScale;
            var startPos = _rectTransform != null ? _rectTransform.anchoredPosition : Vector2.zero;
            var startRot = _instance.transform.localRotation;
            var startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
            
            var elapsed = 0f;
            var duration = _config.uiShowDuration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = ApplyEasing(t, _config.uiShowEase);

                ApplyAnimationFrame(_config.uiShowAnimation, eased, startScale, startPos, startRot, startAlpha, true);
                
                yield return null;
            }

            ApplyFinalShowState();
            _isAnimating = false;

            // Ждём время отображения и запускаем анимацию исчезновения
            if (_config.uiDisplayTime > 0f && _config.uiHideAnimation != UIAnimationType.None)
            {
                yield return new WaitForSeconds(_config.uiDisplayTime);
                yield return AnimateHide();
            }
        }

        private System.Collections.IEnumerator AnimateHide()
        {
            _isAnimating = true;
            
            var startScale = _instance.transform.localScale;
            var startPos = _rectTransform != null ? _rectTransform.anchoredPosition : Vector2.zero;
            var startRot = _instance.transform.localRotation;
            var startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;

            var elapsed = 0f;
            var duration = _config.uiHideDuration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = ApplyEasing(t, _config.uiHideEase);

                ApplyAnimationFrame(_config.uiHideAnimation, eased, startScale, startPos, startRot, startAlpha, false);
                
                yield return null;
            }

            _isAnimating = false;
        }

        private void ApplyAnimationFrame(UIAnimationType animType, float t, Vector3 startScale, Vector2 startPos, Quaternion startRot, float startAlpha, bool isShow)
        {
            var progress = isShow ? t : 1f - t;
            
            switch (animType)
            {
                case UIAnimationType.Scale:
                    _instance.transform.localScale = isShow 
                        ? Vector3.Lerp(Vector3.zero, _targetScale, t)
                        : Vector3.Lerp(_targetScale, Vector3.zero, t);
                    break;
                    
                case UIAnimationType.Fade:
                    if (_canvasGroup != null)
                        _canvasGroup.alpha = isShow ? t : 1f - t;
                    break;
                    
                case UIAnimationType.ScaleAndFade:
                    _instance.transform.localScale = isShow 
                        ? Vector3.Lerp(Vector3.zero, _targetScale, t)
                        : Vector3.Lerp(_targetScale, Vector3.zero, t);
                    if (_canvasGroup != null)
                        _canvasGroup.alpha = isShow ? t : 1f - t;
                    break;
                    
                case UIAnimationType.SlideLeft:
                case UIAnimationType.SlideRight:
                case UIAnimationType.SlideUp:
                case UIAnimationType.SlideDown:
                    if (_rectTransform != null)
                        _rectTransform.anchoredPosition = Vector2.Lerp(startPos, _targetPosition, isShow ? t : 0f);
                    break;
                    
                case UIAnimationType.Bounce:
                    var bounceScale = isShow ? t : 1f - t;
                    // Добавляем небольшой overshoot для bounce эффекта
                    if (isShow && t > 0.7f)
                    {
                        var bounceT = (t - 0.7f) / 0.3f;
                        bounceScale = 1f + Mathf.Sin(bounceT * Mathf.PI) * 0.1f;
                    }
                    _instance.transform.localScale = _targetScale * bounceScale;
                    break;
                    
                case UIAnimationType.Rotate:
                    var rotProgress = isShow ? t : 1f - t;
                    _instance.transform.localRotation = Quaternion.Lerp(
                        Quaternion.Euler(0f, 0f, 180f), 
                        Quaternion.identity, 
                        rotProgress);
                    _instance.transform.localScale = Vector3.Lerp(Vector3.zero, _targetScale, rotProgress);
                    break;
            }
        }

        private float ApplyEasing(float t, UIEaseType easeType)
        {
            switch (easeType)
            {
                case UIEaseType.Linear:
                    return t;
                    
                case UIEaseType.EaseInQuad:
                    return t * t;
                    
                case UIEaseType.EaseOutQuad:
                    return 1f - (1f - t) * (1f - t);
                    
                case UIEaseType.EaseInOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                    
                case UIEaseType.EaseInCubic:
                    return t * t * t;
                    
                case UIEaseType.EaseOutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                    
                case UIEaseType.EaseInOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                    
                case UIEaseType.EaseInBack:
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    return c3 * t * t * t - c1 * t * t;
                    
                case UIEaseType.EaseOutBack:
                    const float c1b = 1.70158f;
                    const float c3b = c1b + 1f;
                    return 1f + c3b * Mathf.Pow(t - 1f, 3f) + c1b * Mathf.Pow(t - 1f, 2f);
                    
                case UIEaseType.EaseInOutBack:
                    const float c2 = 1.70158f * 1.525f;
                    return t < 0.5f
                        ? (Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2)) / 2f
                        : (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;
                    
                case UIEaseType.EaseOutBounce:
                    return EaseOutBounce(t);
                    
                case UIEaseType.EaseOutElastic:
                    if (t == 0f) return 0f;
                    if (t == 1f) return 1f;
                    const float c4 = (2f * Mathf.PI) / 3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                    
                default:
                    return t;
            }
        }

        private float EaseOutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
                return n1 * t * t;
            else if (t < 2f / d1)
                return n1 * (t -= 1.5f / d1) * t + 0.75f;
            else if (t < 2.5f / d1)
                return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            else
                return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }
    }
}
