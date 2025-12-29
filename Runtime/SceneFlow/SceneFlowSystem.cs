// Packages/com.protosystem.core/Runtime/SceneFlow/SceneFlowSystem.cs
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProtoSystem.SceneFlow
{
    /// <summary>
    /// Система управления загрузкой сцен.
    /// Поддерживает асинхронную загрузку, переходы и loading screen.
    /// </summary>
    [ProtoSystemComponent("Scene Flow System", "Загрузка сцен с переходами и loading screen", "Core", "🎬", 15)]
    public class SceneFlowSystem : InitializableSystemBase
    {
        public override string SystemId => "SceneFlowSystem";
        public override string DisplayName => "Scene Flow System";

        [Header("Configuration")]
        [SerializeField] private SceneFlowConfig config;

        [Header("Loading Screen")]
        [SerializeField] private GameObject loadingScreenPrefab;
        [SerializeField] private bool useLoadingScreen = true;
        [SerializeField] private float minimumLoadingTime = 0.5f;

        [Header("Transitions")]
        [SerializeField] private TransitionType defaultTransition = TransitionType.Fade;
        [SerializeField] private float transitionDuration = 0.3f;

        // Состояние
        private bool _isLoading;
        private float _currentProgress;
        private GameObject _loadingScreenInstance;
        private ILoadingScreen _loadingScreen;
        private CanvasGroup _transitionOverlay;

        #region Singleton

        private static SceneFlowSystem _instance;
        public static SceneFlowSystem Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<SceneFlowSystem>();
                return _instance;
            }
        }

        #endregion

        #region Properties

        /// <summary>Идёт ли загрузка</summary>
        public bool IsLoading => _isLoading;

        /// <summary>Текущий прогресс загрузки (0-1)</summary>
        public float Progress => _currentProgress;

        /// <summary>Текущая активная сцена</summary>
        public string CurrentScene => SceneManager.GetActiveScene().name;

        #endregion

        #region Initialization

        protected override void Awake()
        {
            base.Awake();
            _instance = this;
        }

        protected override void InitEvents()
        {
            // Подписка на события если нужно
        }

        public override Task<bool> InitializeAsync()
        {
            LogMessage("Initializing Scene Flow System...");

            // Создаём overlay для переходов
            CreateTransitionOverlay();

            // Загружаем конфиг
            if (config == null)
            {
                config = SceneFlowConfig.CreateDefault();
            }

            LogMessage("Scene Flow System initialized");
            return Task.FromResult(true);
        }

        private void CreateTransitionOverlay()
        {
            var overlayObj = new GameObject("TransitionOverlay");
            overlayObj.transform.SetParent(transform, false);

            var canvas = overlayObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            overlayObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var imageObj = new GameObject("Fade");
            imageObj.transform.SetParent(overlayObj.transform, false);

            var rect = imageObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageObj.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.black;
            image.raycastTarget = true;

            _transitionOverlay = imageObj.AddComponent<CanvasGroup>();
            _transitionOverlay.alpha = 0;
            _transitionOverlay.blocksRaycasts = false;
        }

        #endregion

        #region Static API

        /// <summary>Загрузить сцену</summary>
        public static void Load(string sceneName, Action onComplete = null)
            => Instance?.LoadScene(sceneName, onComplete);

        /// <summary>Загрузить сцену с переходом</summary>
        public static void LoadWithTransition(string sceneName, TransitionType transition = TransitionType.Fade, Action onComplete = null)
            => Instance?.LoadSceneWithTransition(sceneName, transition, onComplete);

        /// <summary>Перезагрузить текущую сцену</summary>
        public static void Reload(Action onComplete = null)
            => Instance?.LoadScene(Instance.CurrentScene, onComplete);

        /// <summary>Добавить сцену (аддитивно)</summary>
        public static void LoadAdditive(string sceneName, Action onComplete = null)
            => Instance?.LoadSceneAdditive(sceneName, onComplete);

        /// <summary>Выгрузить сцену</summary>
        public static void Unload(string sceneName, Action onComplete = null)
            => Instance?.UnloadScene(sceneName, onComplete);

        #endregion

        #region Load Methods

        /// <summary>
        /// Загрузить сцену
        /// </summary>
        public void LoadScene(string sceneName, Action onComplete = null)
        {
            if (_isLoading)
            {
                LogWarning($"Already loading a scene, ignoring request for '{sceneName}'");
                return;
            }

            StartCoroutine(LoadSceneRoutine(sceneName, LoadSceneMode.Single, false, onComplete));
        }

        /// <summary>
        /// Загрузить сцену с переходом
        /// </summary>
        public void LoadSceneWithTransition(string sceneName, TransitionType transition, Action onComplete = null)
        {
            if (_isLoading)
            {
                LogWarning($"Already loading a scene, ignoring request for '{sceneName}'");
                return;
            }

            StartCoroutine(LoadSceneWithTransitionRoutine(sceneName, transition, onComplete));
        }

        /// <summary>
        /// Загрузить сцену аддитивно
        /// </summary>
        public void LoadSceneAdditive(string sceneName, Action onComplete = null)
        {
            if (_isLoading)
            {
                LogWarning($"Already loading a scene, ignoring request for '{sceneName}'");
                return;
            }

            StartCoroutine(LoadSceneRoutine(sceneName, LoadSceneMode.Additive, false, onComplete));
        }

        /// <summary>
        /// Выгрузить сцену
        /// </summary>
        public void UnloadScene(string sceneName, Action onComplete = null)
        {
            StartCoroutine(UnloadSceneRoutine(sceneName, onComplete));
        }

        #endregion

        #region Coroutines

        private IEnumerator LoadSceneRoutine(string sceneName, LoadSceneMode mode, bool showLoading, Action onComplete)
        {
            _isLoading = true;
            _currentProgress = 0f;

            EventBus.Publish(EventBus.SceneFlow.LoadStarted, new SceneLoadEventData
            {
                SceneName = sceneName,
                Progress = 0f
            });

            // Показываем loading screen
            if (showLoading && useLoadingScreen)
            {
                yield return ShowLoadingScreen();
            }

            float startTime = Time.realtimeSinceStartup;

            // Загружаем сцену
            var operation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (operation == null)
            {
                LogError($"Failed to load scene '{sceneName}'");
                _isLoading = false;
                
                EventBus.Publish(EventBus.SceneFlow.LoadFailed, new SceneLoadEventData
                {
                    SceneName = sceneName,
                    Success = false,
                    ErrorMessage = "Scene not found"
                });
                
                yield break;
            }

            operation.allowSceneActivation = false;

            // Ожидаем загрузку
            while (operation.progress < 0.9f)
            {
                _currentProgress = operation.progress / 0.9f;
                
                EventBus.Publish(EventBus.SceneFlow.LoadProgress, new SceneLoadEventData
                {
                    SceneName = sceneName,
                    Progress = _currentProgress
                });

                _loadingScreen?.SetProgress(_currentProgress);
                
                yield return null;
            }

            // Минимальное время загрузки
            float elapsed = Time.realtimeSinceStartup - startTime;
            if (elapsed < minimumLoadingTime)
            {
                yield return new WaitForSecondsRealtime(minimumLoadingTime - elapsed);
            }

            _currentProgress = 1f;
            _loadingScreen?.SetProgress(1f);

            // Активируем сцену
            operation.allowSceneActivation = true;

            while (!operation.isDone)
            {
                yield return null;
            }

            // Скрываем loading screen
            if (showLoading && useLoadingScreen)
            {
                yield return HideLoadingScreen();
            }

            _isLoading = false;

            EventBus.Publish(EventBus.SceneFlow.LoadCompleted, new SceneLoadEventData
            {
                SceneName = sceneName,
                Progress = 1f,
                Success = true
            });

            onComplete?.Invoke();
        }

        private IEnumerator LoadSceneWithTransitionRoutine(string sceneName, TransitionType transition, Action onComplete)
        {
            _isLoading = true;

            // Fade out
            yield return PlayTransition(transition, true);

            EventBus.Publish(EventBus.SceneFlow.TransitionStarted, new TransitionEventData
            {
                Type = transition,
                Duration = transitionDuration
            });

            // Загружаем сцену
            var operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                LogError($"Failed to load scene '{sceneName}'");
                yield return PlayTransition(transition, false);
                _isLoading = false;
                yield break;
            }

            while (!operation.isDone)
            {
                _currentProgress = operation.progress;
                yield return null;
            }

            // Небольшая пауза
            yield return new WaitForSecondsRealtime(0.1f);

            // Fade in
            yield return PlayTransition(transition, false);

            EventBus.Publish(EventBus.SceneFlow.TransitionCompleted, new TransitionEventData
            {
                Type = transition,
                Duration = transitionDuration
            });

            _isLoading = false;

            EventBus.Publish(EventBus.SceneFlow.LoadCompleted, new SceneLoadEventData
            {
                SceneName = sceneName,
                Progress = 1f,
                Success = true
            });

            onComplete?.Invoke();
        }

        private IEnumerator UnloadSceneRoutine(string sceneName, Action onComplete)
        {
            var operation = SceneManager.UnloadSceneAsync(sceneName);
            if (operation == null)
            {
                LogWarning($"Failed to unload scene '{sceneName}'");
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }

            EventBus.Publish(EventBus.SceneFlow.UnloadCompleted, new SceneUnloadEventData
            {
                SceneName = sceneName
            });

            onComplete?.Invoke();
        }

        #endregion

        #region Transition

        private IEnumerator PlayTransition(TransitionType type, bool fadeOut)
        {
            if (type == TransitionType.None)
                yield break;

            float startAlpha = fadeOut ? 0f : 1f;
            float endAlpha = fadeOut ? 1f : 0f;
            float elapsed = 0f;

            _transitionOverlay.blocksRaycasts = true;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / transitionDuration;
                _transitionOverlay.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                yield return null;
            }

            _transitionOverlay.alpha = endAlpha;
            _transitionOverlay.blocksRaycasts = fadeOut;
        }

        #endregion

        #region Loading Screen

        private IEnumerator ShowLoadingScreen()
        {
            if (loadingScreenPrefab == null)
                yield break;

            _loadingScreenInstance = Instantiate(loadingScreenPrefab, transform);
            _loadingScreen = _loadingScreenInstance.GetComponent<ILoadingScreen>();
            _loadingScreen?.Show();

            yield return new WaitForSecondsRealtime(0.2f);
        }

        private IEnumerator HideLoadingScreen()
        {
            if (_loadingScreen != null)
            {
                _loadingScreen.Hide();
                yield return new WaitForSecondsRealtime(0.3f);
            }

            if (_loadingScreenInstance != null)
            {
                Destroy(_loadingScreenInstance);
                _loadingScreenInstance = null;
                _loadingScreen = null;
            }
        }

        #endregion

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }

    /// <summary>
    /// Тип перехода
    /// </summary>
    public enum TransitionType
    {
        None,
        Fade,
        CrossFade,
        SlideLeft,
        SlideRight
    }

    /// <summary>
    /// Интерфейс loading screen
    /// </summary>
    public interface ILoadingScreen
    {
        void Show();
        void Hide();
        void SetProgress(float progress);
        void SetMessage(string message);
    }
}
