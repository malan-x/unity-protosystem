// Экран заставок: последовательность полноэкранных кадров (логотипы, арты) перед меню.
//
// Это ОБЫЧНОЕ окно UISystem, а не отдельная сущность: заставок бывает несколько, они могут
// сменять друг друга и пропускаться — вся эта логика ничем не отличается от логики любого
// другого экрана.
//
// Ключевой приём — окно ЗАПЕКАЕТСЯ в сцену (UIWindowBase.bakedInScene, инспектор UISystem):
// его экземпляр лежит в сцене активным, поэтому UIDocument рисует картинку с ПЕРВОГО кадра —
// задолго до того, как поднимется UISystem. Иначе игрок успевает увидеть голый 3D-мир: UISystem
// открывает стартовое окно только в конце общей очереди инициализации.
//
// Чтобы это работало, окно должно быть СТАРТОВЫМ в графе: тогда UISystem не создаёт его заново,
// а забирает запечённый экземпляр и показывает без fade-in (он уже на экране). Прочие запечённые
// окна система гасит.
//
// Первый кадр применяется в Awake — до Show() у окна не вызывается OnBuildUI, а картинка нужна
// сразу. Остальные кадры крутит корутина после Show.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.UI
{
    [UIWindow("Splash", WindowType.Normal, WindowLayer.Windows,
        Level = 0, PauseGame = false, CursorMode = WindowCursorMode.Inherit, AllowBack = false)]
    public class SplashWindow : UIToolkitWindowBase
    {
        [Serializable]
        public class Frame
        {
            [Tooltip("Картинка кадра. Растягивается по экрану (cover).")]
            public Sprite image;

            [Tooltip("Сколько держать кадр на экране, секунды.")]
            public float duration = 2f;

            [Tooltip("Затухание при уходе кадра, секунды. 0 — сменить мгновенно.")]
            public float fadeOut = 0.4f;
        }

        [Header("Кадры заставки")]
        [SerializeField] private List<Frame> frames = new();

        [Tooltip("Подложка под картинкой: видна, пока кадр не назначен, и по краям, " +
                 "если пропорции не совпали.")]
        [SerializeField] private Color backgroundColor = new(0.06f, 0.05f, 0.04f, 1f);

        [Header("Дальше")]
        [Tooltip("Окно, которое открыть после последнего кадра.")]
        [SerializeField] private string nextWindowId = "MainMenu";

        [Tooltip("Разрешить пропуск любой кнопкой/кликом.")]
        [SerializeField] private bool skippable = true;

        [Tooltip("Страховка: уйти дальше, даже если что-то пошло не так и кадры не доиграли.")]
        [SerializeField] private float maxLifetime = 60f;

        private VisualElement _image;
        private Coroutine _sequence;
        private bool _left;   // уже ушли дальше — второй раз не навигируем

        /// <summary>
        /// Заставка живёт ДО инициализации UI (её экземпляр запечён в сцене), поэтому первый кадр
        /// рисуем уже здесь: OnBuildUI вызовется только при Show(), а картинка нужна сразу.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            var root = Root;
            if (root == null) return;

            BuildImage(root);
            ShowFrame(0);
        }

        protected override void OnBuildUI(VisualElement root)
        {
            BuildImage(root);   // после пула дерево пересоздаётся — ссылку берём заново
            ShowFrame(0);
        }

        private void BuildImage(VisualElement root)
        {
            root.style.backgroundColor = backgroundColor;
            root.pickingMode = PickingMode.Ignore;

            _image = root.Q<VisualElement>("splash-image");
            if (_image != null) return;

            _image = new VisualElement { name = "splash-image", pickingMode = PickingMode.Ignore };
            _image.style.position = Position.Absolute;
            _image.style.left = 0;
            _image.style.top = 0;
            _image.style.right = 0;
            _image.style.bottom = 0;
            root.Add(_image);
        }

        protected override void OnShow()
        {
            base.OnShow();

            _left = false;
            _sequence = StartCoroutine(PlaySequence());
        }

        protected override void OnHide()
        {
            base.OnHide();

            if (_sequence != null) StopCoroutine(_sequence);
            _sequence = null;
        }

        private void Update()
        {
            if (!skippable || _left || State != WindowState.Visible) return;

            if (Input.anyKeyDown)
                Leave();
        }

        private IEnumerator PlaySequence()
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, maxLifetime);

            for (int i = 0; i < frames.Count; i++)
            {
                ShowFrame(i);

                var frame = frames[i];
                float elapsed = 0f;

                while (elapsed < frame.duration)
                {
                    if (_left) yield break;
                    if (Time.realtimeSinceStartup > deadline) break;

                    // Заставка играет во время инициализации: timeScale может быть любым
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (frame.fadeOut > 0f && _image != null)
                    yield return Fade(frame.fadeOut);
            }

            Leave();
        }

        private IEnumerator Fade(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (_left) yield break;

                elapsed += Time.unscaledDeltaTime;
                _image.style.opacity = Mathf.Clamp01(1f - elapsed / duration);
                yield return null;
            }
        }

        private void ShowFrame(int index)
        {
            if (_image == null || index < 0 || index >= frames.Count) return;

            var sprite = frames[index].image;
            _image.style.opacity = 1f;

            if (sprite == null)
            {
                _image.style.backgroundImage = StyleKeyword.None;
                return;
            }

            _image.style.backgroundImage = new StyleBackground(sprite);
            _image.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            _image.style.unityBackgroundImageTintColor = Color.white;
        }

        /// <summary>Уйти на следующее окно. Второй раз не сработает — заставка одноразовая.</summary>
        private void Leave()
        {
            if (_left) return;
            _left = true;

            if (string.IsNullOrEmpty(nextWindowId))
            {
                ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Errors,
                    "SplashWindow: не задано следующее окно (nextWindowId) — заставку некуда закрыть.");
                return;
            }

            UISystem.Open(nextWindowId);
        }
    }
}
