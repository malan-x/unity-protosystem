// Packages/com.protosystem.core/Runtime/UI/Core/UIWindowBase.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Базовый класс для всех UI окон.
    /// Наследники должны иметь атрибут [UIWindow].
    ///
    /// Для окон на uGUI требуется CanvasGroup (добавляется генератором).
    /// Для окон на UI Toolkit используйте наследника UIToolkitWindowBase —
    /// CanvasGroup/RectTransform ему не нужны, все обращения null-tolerant.
    /// </summary>
    public abstract class UIWindowBase : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] protected TransitionAnimation showAnimation = TransitionAnimation.Fade;
        [SerializeField] protected TransitionAnimation hideAnimation = TransitionAnimation.Fade;
        [SerializeField] protected float animationDuration = 0.25f;
        [SerializeField] protected AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Back Navigation")]
        [SerializeField] protected Button[] backButtons;

        // Компоненты
        protected CanvasGroup canvasGroup;
        protected RectTransform rectTransform;

        // Состояние
        public WindowState State { get; private set; } = WindowState.Hidden;

        /// <summary>Смена состояния окна (для наследников, реализующих свой Show/Hide)</summary>
        protected void SetState(WindowState state) => State = state;
        
        /// <summary>Окно открыто (Visible или Blurred)</summary>
        public bool IsOpen => State == WindowState.Visible || State == WindowState.Blurred;
        public string WindowId { get; internal set; }
        public WindowType WindowType { get; internal set; }
        public WindowLayer Layer { get; internal set; }
        public bool AllowBack { get; internal set; } = true;

        /// <summary>
        /// Окно уже на экране (запечено в сцену) — ближайший Show() должен пройти без fade-in,
        /// иначе картинка моргнёт из прозрачности в тот момент, когда UISystem его подхватит.
        /// Сбрасывается этим же Show(). См. UIWindowFactory.RegisterBaked.
        /// </summary>
        internal bool SkipShowAnimation { get; set; }

        [Header("Запекание в сцену")]
        [Tooltip("Экземпляр окна сохранён прямо в сцене и должен быть виден с первого кадра — " +
                 "ещё до того, как поднимется UISystem (иначе игрок успевает увидеть голый 3D-мир). " +
                 "Ставится командой ProtoSystem/UI/Запечь окно в сцену.")]
        [SerializeField] private bool bakedInScene;

        /// <summary>
        /// Окно запечено в сцену: оно уже нарисовано и НЕ должно прятать себя при Awake.
        /// По этому флагу UISystem находит такие экземпляры и забирает их вместо создания новых.
        /// </summary>
        public bool BakedInScene => bakedInScene;

        // Корутина анимации
        private Coroutine _animationCoroutine;

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();

            // Привязка кнопок Back
            if (backButtons != null)
            {
                foreach (var btn in backButtons)
                {
                    if (btn != null)
                        btn.onClick.AddListener(OnBackButton);
                }
            }

            // Изначально скрыто
            SetVisibility(false);
        }

        protected virtual void OnDestroy()
        {
            if (backButtons != null)
            {
                foreach (var btn in backButtons)
                {
                    if (btn != null)
                        btn.onClick.RemoveListener(OnBackButton);
                }
            }
        }

        #endregion

        #region Show/Hide

        /// <summary>
        /// Показать окно
        /// </summary>
        public virtual void Show(Action onComplete = null)
                {
                    if (State == WindowState.Visible || State == WindowState.Showing)
                    {
                        onComplete?.Invoke();
                        return;
                    }

                    gameObject.SetActive(true);

                    if (_animationCoroutine != null)
                        StopCoroutine(_animationCoroutine);

                    _animationCoroutine = StartCoroutine(ShowRoutine(onComplete));
                }

        /// <summary>
        /// Скрыть окно
        /// </summary>
        public virtual void Hide(Action onComplete = null)
                {
                    if (State == WindowState.Hidden || State == WindowState.Hiding)
                    {
                        onComplete?.Invoke();
                        return;
                    }

                    if (_animationCoroutine != null)
                        StopCoroutine(_animationCoroutine);

                    _animationCoroutine = StartCoroutine(HideRoutine(onComplete));
                }

        /// <summary>
        /// Окно потеряло фокус (открылось другое поверх)
        /// </summary>
        internal void Blur() => Blur(dim: true);

        /// <summary>
        /// Окно потеряло фокус.
        /// <paramref name="dim"/> — визуально приглушить окно (модалка поверх него).
        /// Для Normal-окна, которое просто перекрыли сверху, приглушать не нужно:
        /// ввод и фокус глушим, но вид не трогаем (uGUI визуал и так не меняет,
        /// а UIToolkitWindowBase иначе вешает класс window-blurred).
        /// </summary>
        internal virtual void Blur(bool dim)
        {
            if (State != WindowState.Visible) return;

            State = WindowState.Blurred;
            if (canvasGroup) canvasGroup.interactable = false;
            OnBlur();
        }

        /// <summary>
        /// Окно вернуло фокус
        /// </summary>
        internal virtual void Focus()
        {
            if (State != WindowState.Blurred) return;

            State = WindowState.Visible;
            if (canvasGroup) canvasGroup.interactable = true;
            OnFocus();
        }

        /// <summary>
        /// Сделать фон окна непрозрачным (вызывается навигатором для Normal-окон Level 1+,
        /// чтобы нижние окна не просвечивали). uGUI-реализация красит UITwoColorImage/Image;
        /// UIToolkitWindowBase переопределяет под USS.
        /// </summary>
        public virtual void ApplyOpaqueBackground()
        {
            // Проверяем UITwoColorImage (приоритет)
            var twoColor = GetComponent<UITwoColorImage>();
            if (twoColor != null)
            {
                var fill = twoColor.FillColor;
                fill.a = 1f;
                twoColor.FillColor = fill;
                return;
            }

            // Fallback на обычный Image
            var image = GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                var color = image.color;
                color.a = 1f;
                image.color = color;
            }
        }

        /// <summary>
        /// Доставка полезной нагрузки окну (вызывается навигатором до Show).
        /// </summary>
        internal void DeliverPayload(object payload) => OnPayload(payload);

        /// <summary>
        /// Переопределите для приёма параметров, переданных в UISystem.Open/Navigate.
        /// Вызывается до Show — сохраните данные и используйте в OnShow/OnBuildUI.
        /// </summary>
        protected virtual void OnPayload(object payload) { }

        #endregion

        #region Animation Routines

        private IEnumerator ShowRoutine(Action onComplete)
        {
            State = WindowState.Showing;
            OnBeforeShow();

            yield return PlayAnimation(showAnimation, false);

            State = WindowState.Visible;
            if (canvasGroup)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            OnShow();
            onComplete?.Invoke();
            _animationCoroutine = null;
        }

        private IEnumerator HideRoutine(Action onComplete)
        {
            State = WindowState.Hiding;
            if (canvasGroup) canvasGroup.interactable = false;
            OnBeforeHide();

            yield return PlayAnimation(hideAnimation, true);

            State = WindowState.Hidden;
            if (canvasGroup) canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            
            OnHide();
            onComplete?.Invoke();
            _animationCoroutine = null;
        }

        private IEnumerator PlayAnimation(TransitionAnimation anim, bool reverse)
        {
            // Без CanvasGroup/RectTransform (не-uGUI окно) анимировать нечего
            if (anim == TransitionAnimation.None || animationDuration <= 0 ||
                canvasGroup == null || rectTransform == null)
            {
                SetVisibility(!reverse);
                yield break;
            }

            float elapsed = 0f;
            Vector2 startPos = rectTransform.anchoredPosition;
            Vector2 targetOffset = GetAnimationOffset(anim);

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = animationCurve.Evaluate(elapsed / animationDuration);
                
                if (reverse) t = 1f - t;

                ApplyAnimation(anim, t, startPos, targetOffset);
                yield return null;
            }

            // Финальное состояние
            ApplyAnimation(anim, reverse ? 0f : 1f, startPos, targetOffset);
        }

        private Vector2 GetAnimationOffset(TransitionAnimation anim)
        {
            float width = rectTransform.rect.width;
            float height = rectTransform.rect.height;

            return anim switch
            {
                TransitionAnimation.SlideLeft => new Vector2(-width, 0),
                TransitionAnimation.SlideRight => new Vector2(width, 0),
                TransitionAnimation.SlideUp => new Vector2(0, height),
                TransitionAnimation.SlideDown => new Vector2(0, -height),
                _ => Vector2.zero
            };
        }

        private void ApplyAnimation(TransitionAnimation anim, float t, Vector2 startPos, Vector2 offset)
        {
            switch (anim)
            {
                case TransitionAnimation.Fade:
                    canvasGroup.alpha = t;
                    break;

                case TransitionAnimation.Scale:
                    float scale = Mathf.Lerp(0.8f, 1f, t);
                    transform.localScale = new Vector3(scale, scale, 1f);
                    canvasGroup.alpha = t;
                    break;

                case TransitionAnimation.SlideLeft:
                case TransitionAnimation.SlideRight:
                case TransitionAnimation.SlideUp:
                case TransitionAnimation.SlideDown:
                    rectTransform.anchoredPosition = Vector2.Lerp(startPos + offset, startPos, t);
                    canvasGroup.alpha = t;
                    break;
            }
        }

        private void SetVisibility(bool visible)
        {
            if (canvasGroup)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            transform.localScale = Vector3.one;
        }

        #endregion

        #region Navigation Helpers

        /// <summary>
        /// Выполнить навигацию по триггеру
        /// </summary>
        protected void Navigate(string trigger)
        {
            UISystem.Navigate(trigger);
        }

        /// <summary>
        /// Закрыть текущее окно (Back)
        /// </summary>
        protected void Close()
        {
            UISystem.Back();
        }

        private void OnBackButton()
                {
                    if (AllowBack)
                        OnBackPressed();
                }

                /// <summary>
                /// Вызывается при нажатии Back/Escape. Переопределите для кастомного поведения.
                /// </summary>
                public virtual void OnBackPressed()
                {
                    Close();
                }

        #endregion

        #region Virtual Lifecycle Methods

        /// <summary>Вызывается перед началом анимации показа</summary>
        protected virtual void OnBeforeShow() { }
        
        /// <summary>Вызывается после завершения анимации показа</summary>
        protected virtual void OnShow() { }
        
        /// <summary>Вызывается перед началом анимации скрытия</summary>
        protected virtual void OnBeforeHide() { }
        
        /// <summary>Вызывается после завершения анимации скрытия</summary>
        protected virtual void OnHide() { }
        
        /// <summary>Вызывается когда окно получает фокус</summary>
        protected virtual void OnFocus() { }
        
        /// <summary>Вызывается когда окно теряет фокус</summary>
        protected virtual void OnBlur() { }

        #endregion
    }
}
