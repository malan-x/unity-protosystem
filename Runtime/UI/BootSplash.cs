// Стартовая заставка: полноэкранная картинка, видная с первого кадра.
//
// Зачем: UISystem поднимается в общей очереди инициализации и открывает стартовое окно только
// в конце. Всё это время камера уже рисует 3D-мир, и игрок видит сцену (в Last Convoy —
// вращающийся глобус) до появления меню.
//
// Почему заставка, а не запечённое меню: запечённое окно выглядит рабочим, но до Show() у него
// не вызывается OnBuildUI — кнопки ни на что не подписаны, данные не подтянуты. Игрок видит
// «живое» меню и жмёт мёртвые кнопки. Заставка честнее: она ничего не обещает.
//
// Живёт вне UISystem: это обычный UIDocument в сцене, поэтому панель рисуется сразу, без
// ожидания систем. Гаснет, когда открылось первое окно (EventBus.UI.WindowOpened).

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class BootSplash : MonoBehaviour
    {
        [Header("Картинка")]
        [Tooltip("Фон заставки. Растягивается по экрану (cover), как фон окна.")]
        [SerializeField] private Sprite background;

        [Tooltip("Цвет подложки под картинкой — виден, пока спрайт не назначен, " +
                 "и по краям, если пропорции не совпали.")]
        [SerializeField] private Color backgroundColor = new(0.10f, 0.09f, 0.08f, 1f);

        [Header("Исчезновение")]
        [Tooltip("Длительность затухания, секунды.")]
        [SerializeField] private float fadeOut = 0.35f;

        [Tooltip("Страховка: скрыть заставку, даже если стартовое окно так и не открылось.")]
        [SerializeField] private float maxLifetime = 30f;

        private UIDocument _document;
        private VisualElement _root;
        private bool _hiding;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            Build();
        }

        private void OnEnable()
        {
            EventBus.Subscribe(EventBus.UI.WindowOpened, OnWindowOpened);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe(EventBus.UI.WindowOpened, OnWindowOpened);
        }

        private void Start()
        {
            if (maxLifetime > 0f)
                StartCoroutine(FailSafe());
        }

        /// <summary>
        /// Дерево строится кодом: заставке хватает одного элемента, а UXML-ассет пришлось бы
        /// тащить в пакет и следить за его .meta.
        /// </summary>
        private void Build()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null) return;

            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _root.style.backgroundColor = backgroundColor;
            _root.pickingMode = PickingMode.Ignore;   // заставка ничего не ловит

            if (background != null)
            {
                _root.style.backgroundImage = new StyleBackground(background);
                _root.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                _root.style.unityBackgroundImageTintColor = Color.white;
            }
        }

        /// <summary>Первое открытое окно — значит UI поднялся и можно уходить.</summary>
        private void OnWindowOpened(object _)
        {
            Hide();
        }

        public void Hide()
        {
            if (_hiding || !isActiveAndEnabled) return;
            _hiding = true;
            StartCoroutine(FadeOutAndDisable());
        }

        private IEnumerator FadeOutAndDisable()
        {
            if (_root == null || fadeOut <= 0f)
            {
                gameObject.SetActive(false);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeOut)
            {
                // Заставка живёт до и во время инициализации — Time.timeScale может быть чем угодно
                elapsed += Time.unscaledDeltaTime;
                _root.style.opacity = Mathf.Clamp01(1f - elapsed / fadeOut);
                yield return null;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator FailSafe()
        {
            yield return new WaitForSecondsRealtime(maxLifetime);

            if (_hiding) yield break;

            ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Errors,
                $"BootSplash: за {maxLifetime:F0} с не открылось ни одного окна — " +
                "убираю заставку, чтобы она не закрывала экран навсегда.");
            Hide();
        }
    }
}
