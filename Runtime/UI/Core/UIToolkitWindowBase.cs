// Packages/com.protosystem.core/Runtime/UI/Core/UIToolkitWindowBase.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Базовый класс окон на UI Toolkit. Полноценный участник UISystem:
    /// граф, стек, модальность, Level-логика, пауза и курсор работают как для uGUI.
    ///
    /// Отличия от uGUI-окна:
    /// - Визуал — UXML/USS через UIDocument на этом же GameObject (правится руками, без генератора).
    /// - Show/Hide — display + fade через style.opacity (CanvasGroup не нужен).
    /// - Blur/Focus — pickingMode + менеджмент фокуса (геймпад/клавиатура/Steam Deck):
    ///   при открытии фокусируется defaultFocusName (или первый focusable),
    ///   при Blur фокус запоминается, при Focus — восстанавливается.
    /// - Кнопка Cancel (Esc/геймпад B) → OnBackPressed().
    /// - Локализация — конвенция «#ключ» в текстах/tooltip (см. ToolkitLocalization),
    ///   перелокализация по Evt.Localization.LanguageChanged, USS-класс lang-{code} на корне.
    ///
    /// Жизненный цикл контента: UIDocument пересоздаёт rootVisualElement при повторной
    /// активации (пул окон!), поэтому весь динамический контент стройте в OnBuildUI(root) —
    /// он вызывается при каждом Show на свежем дереве.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public abstract class UIToolkitWindowBase : UIWindowBase
    {
        [Header("UI Toolkit")]
        [Tooltip("Имя элемента (name в UXML), получающего фокус при открытии. Пусто — первый focusable.")]
        [SerializeField] protected string defaultFocusName = "";

        /// <summary>USS-классы, которые вешает база (стилизуйте в своём USS).</summary>
        public const string ClassBlurred = "window-blurred";
        public const string ClassOpaque  = "window-opaque";

        protected UIDocument document;
        public UIDocument Document => document;

        /// <summary>Корень визуального дерева (null, если документ не активен).</summary>
        public VisualElement Root => document != null ? document.rootVisualElement : null;

        /// <summary>Локализатор дерева (доступен наследникам для динамического контента).</summary>
        protected ToolkitLocalization Localization { get; } = new();

        private Focusable _lastFocused;
        private Coroutine _fadeCoroutine;
        private bool _rootPrepared;
        private PanelEventHandler _panelEventHandler;

        /// <summary>Элементы, у которых Blur() снял focusable (вернём в Focus()).</summary>
        private readonly List<VisualElement> _suspendedFocusables = new();

        #region Unity Lifecycle

        protected override void Awake()
        {
            document = GetComponent<UIDocument>();
            base.Awake();

            // Скрываем до первого Show — иначе UXML мигнёт на кадр после Instantiate
            var root = Root;
            if (root != null)
            {
                root.style.display = DisplayStyle.None;
                root.style.opacity = 0f;
            }
        }

        protected virtual void OnEnable()
        {
            EventBus.Subscribe(EventBus.Localization.LanguageChanged, OnLanguageChanged);
        }

        protected virtual void OnDisable()
        {
            EventBus.Unsubscribe(EventBus.Localization.LanguageChanged, OnLanguageChanged);
            _rootPrepared = false; // дерево будет пересоздано при следующей активации
            _panelEventHandler = null;
            // Элементы старого дерева умрут вместе с ним — держать на них ссылки нельзя
            _suspendedFocusables.Clear();
        }

        private void OnLanguageChanged(object _)
        {
            if (IsOpen && Root != null)
                Localization.Localize(Root);
        }

        #endregion

        #region Show/Hide (UI Toolkit)

        public override void Show(Action onComplete = null)
        {
            if (State == WindowState.Visible || State == WindowState.Showing)
            {
                onComplete?.Invoke();
                return;
            }

            gameObject.SetActive(true); // активация UIDocument (пересоздаёт дерево после пула)

            var root = Root;
            if (root == null)
            {
                ProtoLogger.LogError("UISystem", $"{name}: UIDocument.rootVisualElement == null (нет visualTreeAsset/panelSettings?)");
                onComplete?.Invoke();
                return;
            }

            SetState(WindowState.Showing);
            OnBeforeShow();

            PrepareRoot(root);
            OnBuildUI(root);
            Localization.Localize(root);

            root.style.display = DisplayStyle.Flex;
            root.pickingMode = PickingMode.Position;

            StartFade(root, show: true, () =>
            {
                SetState(WindowState.Visible);
                FocusDefaultElement();
                OnShow();
                onComplete?.Invoke();
            });
        }

        public override void Hide(Action onComplete = null)
        {
            if (State == WindowState.Hidden || State == WindowState.Hiding)
            {
                onComplete?.Invoke();
                return;
            }

            var root = Root;
            SetState(WindowState.Hiding);
            OnBeforeHide();

            if (root == null)
            {
                FinishHide(onComplete);
                return;
            }

            root.pickingMode = PickingMode.Ignore;

            StartFade(root, show: false, () =>
            {
                root.style.display = DisplayStyle.None;
                FinishHide(onComplete);
            });
        }

        private void FinishHide(Action onComplete)
        {
            SetState(WindowState.Hidden);
            gameObject.SetActive(false);
            OnHide();
            onComplete?.Invoke();
        }

        private void StartFade(VisualElement root, bool show, Action onComplete)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);

            var anim = show ? showAnimation : hideAnimation;
            if (anim == TransitionAnimation.None || animationDuration <= 0f || !gameObject.activeInHierarchy)
            {
                root.style.opacity = show ? 1f : 0f;
                onComplete?.Invoke();
                return;
            }

            _fadeCoroutine = StartCoroutine(FadeRoutine(root, show, onComplete));
        }

        private IEnumerator FadeRoutine(VisualElement root, bool show, Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = animationCurve.Evaluate(Mathf.Clamp01(elapsed / animationDuration));
                root.style.opacity = show ? t : 1f - t;
                yield return null;
            }
            root.style.opacity = show ? 1f : 0f;
            _fadeCoroutine = null;
            onComplete?.Invoke();
        }

        #endregion

        #region Blur/Focus (ввод и фокус для геймпада)

        internal override void Blur()
        {
            if (State != WindowState.Visible) return;
            SetState(WindowState.Blurred);

            var root = Root;
            if (root != null)
            {
                // Запоминаем фокус, глушим ввод панели
                _lastFocused = root.focusController?.focusedElement;
                (_lastFocused as VisualElement)?.Blur();
                SetPicking(root, PickingMode.Ignore);
                SuspendFocusTree(root);
                root.AddToClassList(ClassBlurred);
            }

            OnBlur();
        }

        internal override void Focus()
        {
            if (State != WindowState.Blurred) return;
            SetState(WindowState.Visible);

            var root = Root;
            if (root != null)
            {
                SetPicking(root, PickingMode.Position);
                RestoreFocusTree();
                root.RemoveFromClassList(ClassBlurred);

                EnsurePanelSelected();

                if (_lastFocused is VisualElement ve && ve.panel != null)
                    ve.Focus();
                else
                    FocusDefaultElement();
            }

            OnFocus();
        }

        private static void SetPicking(VisualElement root, PickingMode mode)
        {
            root.pickingMode = mode;
            foreach (var child in root.Children())
                child.pickingMode = mode;
        }

        /// <summary>
        /// Снять focusable со всего поддерева blurred-окна и запомнить, кому вернуть.
        ///
        /// Зачем: фабрика создаёт ОДИН PanelSettings на WindowLayer, поэтому все окна слоя
        /// живут в одной панели с общим FocusController. pickingMode глушит только мышь —
        /// клавиатура/геймпад продолжали уводить фокус в окно под модалкой.
        /// Через focusable (а не SetEnabled(false)), чтобы не ловить :disabled из темы
        /// поверх .window-blurred — иначе фон затемняется дважды.
        /// </summary>
        private void SuspendFocusTree(VisualElement root)
        {
            _suspendedFocusables.Clear();
            root.Query<VisualElement>().ForEach(ve =>
            {
                if (!ve.focusable) return;
                _suspendedFocusables.Add(ve);
                ve.focusable = false;
            });
        }

        private void RestoreFocusTree()
        {
            foreach (var ve in _suspendedFocusables)
            {
                if (ve != null) ve.focusable = true;
            }
            _suspendedFocusables.Clear();
        }

        /// <summary>
        /// Сфокусировать стартовый элемент окна (defaultFocusName или первый focusable).
        /// Обязательно для управления геймпадом: без фокуса навигация стиком мертва.
        /// </summary>
        protected void FocusDefaultElement()
        {
            var root = Root;
            if (root == null) return;

            EnsurePanelSelected();

            VisualElement target = null;
            if (!string.IsNullOrEmpty(defaultFocusName))
                target = root.Q(defaultFocusName);
            target ??= FindFirstFocusable(root);
            target?.Focus();
        }

        /// <summary>
        /// Направляет геймпад/клавиатуру в панель этого окна: EventSystem раздаёт
        /// Move/Submit/Cancel только выбранному объекту, а toolkit-панель получает их
        /// через свой PanelEventHandler. Мышь выбирает его кликом сама, геймпад — никогда,
        /// поэтому выбираем явно при показе/фокусе окна. Работает с любым input-модулем
        /// (StandaloneInputModule, InputSystemUIInputModule, RewiredStandaloneInputModule).
        /// </summary>
        private void EnsurePanelSelected()
        {
            var root = Root;
            if (root?.panel == null) return;

            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null) return;

            if (_panelEventHandler == null || _panelEventHandler.panel != root.panel)
            {
                _panelEventHandler = null;
                var handlers = FindObjectsByType<PanelEventHandler>(FindObjectsSortMode.None);
                foreach (var handler in handlers)
                {
                    if (handler.panel == root.panel)
                    {
                        _panelEventHandler = handler;
                        break;
                    }
                }
            }

            if (_panelEventHandler != null &&
                eventSystem.currentSelectedGameObject != _panelEventHandler.gameObject)
            {
                eventSystem.SetSelectedGameObject(_panelEventHandler.gameObject);
            }
        }

        /// <summary>
        /// Подписка на кнопку по имени. Используйте вместо RegisterCallback&lt;ClickEvent&gt;:
        /// clicked срабатывает и от указателя, и от геймпадного Submit (ClickEvent — только от мыши).
        /// Вызывается из OnBuildUI — дерево пересоздаётся, дубликатов подписок не будет.
        /// </summary>
        protected static void OnClick(VisualElement root, string buttonName, Action action)
        {
            var button = root?.Q<Button>(buttonName);
            if (button != null && action != null)
                button.clicked += action;
        }

        private static VisualElement FindFirstFocusable(VisualElement element)
        {
            foreach (var child in element.Children())
            {
                if (child.focusable && child.enabledInHierarchy &&
                    child.resolvedStyle.display != DisplayStyle.None)
                    return child;

                var nested = FindFirstFocusable(child);
                if (nested != null) return nested;
            }
            return null;
        }

        #endregion

        #region Root Setup

        /// <summary>
        /// Одноразовая (на жизнь дерева) настройка корня: Cancel → Back.
        /// </summary>
        private void PrepareRoot(VisualElement root)
        {
            if (_rootPrepared) return;
            _rootPrepared = true;

            root.RegisterCallback<NavigationCancelEvent>(evt =>
            {
                if (State != WindowState.Visible) return;
                if (!AllowBack) return;
                OnBackPressed();
                evt.StopPropagation();
            });
        }

        /// <summary>
        /// Построение/обновление контента окна. Вызывается при КАЖДОМ Show на актуальном
        /// дереве (после пула UIDocument пересоздаёт rootVisualElement — колбэки и
        /// динамические элементы отсюда переживают пересоздание).
        /// Статическая разметка из UXML уже загружена; локализация применится после.
        /// </summary>
        protected virtual void OnBuildUI(VisualElement root) { }

        public override void ApplyOpaqueBackground()
        {
            var root = Root;
            if (root == null) return;

            root.AddToClassList(ClassOpaque);

            // Если у корня уже задан полупрозрачный фон — делаем непрозрачным
            var bg = root.resolvedStyle.backgroundColor;
            if (bg.a > 0f && bg.a < 1f)
                root.style.backgroundColor = new Color(bg.r, bg.g, bg.b, 1f);
        }

        #endregion
    }
}
