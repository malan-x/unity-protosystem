// Packages/com.protosystem.core/Runtime/LiveOps/WishlistPrompt/WishlistPromptSystem.cs
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using ProtoSystem.UI;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Когда просить игрока добавить игру в желаемое.
    ///
    /// Система решает ТОЛЬКО момент: ловит события шины по конфигу и открывает
    /// окно WishlistPrompt через UISystem. Сам показ, модальность, затемнение,
    /// фокус и возврат к предыдущему окну — забота UISystem, как у любого
    /// другого модального окна.
    ///
    /// Почему через шину, а не вызовом из кода игры: момент «игроку сейчас
    /// хорошо» у каждого проекта свой (взял сектор, прошёл босса, построил
    /// здание), и зашивать его в пакет нельзя. Триггеры перечисляются в ассете
    /// конфига, который лежит В ПРОЕКТЕ.
    ///
    /// Решение игрока необратимо: нажал любую из двух кнопок — окно больше
    /// не появится никогда. Крестик решением НЕ считается (окно придёт на
    /// следующем триггере), поэтому число показов ограничено maxShows.
    /// </summary>
    [ProtoSystemComponent("Wishlist Prompt", "Просьба добавить игру в желаемое в удачный момент",
        "LiveOps", "⭐", 155)]
    public class WishlistPromptSystem : InitializableSystemBase
    {
        #region InitializableSystemBase

        public override string SystemId => "wishlist_prompt";
        public override string DisplayName => "Wishlist Prompt";
        public override string Description => "Окно «Добавить в желаемое» по событиям шины";

        #endregion

        #region Serialized

        [SerializeField, InlineConfig] private WishlistPromptConfig config;

        [Header("Debug")]
        [Tooltip("Игнорировать сохранённое решение игрока — окно показывается каждый раз. Только для отладки.")]
        [SerializeField] private bool ignoreSavedDecision = false;

        #endregion

        #region State

        [Dependency(required: false)] private UISystem _ui;
        [Dependency(required: false)] private LiveOpsSystem _liveOps;

        /// <summary>Сколько ждём закрытия чужого модального окна, прежде чем отступиться.</summary>
        private const float QuietWaitSeconds = 60f;

        private readonly Dictionary<int, int> _hits = new();               // eventId -> сколько раз пришло
        private readonly List<System.Action<object>> _handlers = new();    // держим делегаты: RemoveEvent требует тот же экземпляр

        private bool _visible;
        private Coroutine _pending;

        private bool Decided => !ignoreSavedDecision && WishlistPromptState.Decided;
        private int  Shows   => WishlistPromptState.Shows;

        #endregion

        #region MonoEventBus

        protected override void InitEvents()
        {
            _handlers.Clear();
            if (config == null) return;

            // Одна подписка на уникальный eventId: триггеров с одним событием
            // может быть несколько (разные occurrence)
            var subscribed = new HashSet<int>();
            foreach (var trigger in config.triggers)
            {
                if (trigger == null || trigger.eventId == 0) continue;
                if (!subscribed.Add(trigger.eventId)) continue;

                int id = trigger.eventId;
                System.Action<object> handler = _ => OnTriggerEvent(id);
                _handlers.Add(handler);
                AddEvent(id, handler);
            }

            foreach (var id in config.cancelEventIds)
            {
                if (id == 0 || !subscribed.Add(id)) continue;
                System.Action<object> handler = _ => CancelPending();
                _handlers.Add(handler);
                AddEvent(id, handler);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            WishlistPromptWindow.Answered += OnAnswered;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            WishlistPromptWindow.Answered -= OnAnswered;
        }

        #endregion

        public override Task<bool> InitializeAsync()
        {
            if (config == null)
            {
                LogWarning("Конфиг не назначен — окно не появится.");
                return Task.FromResult(true);
            }

            if (Decided)
                LogInit("Игрок уже решил — окно отключено.");
            else
                LogInit($"Триггеров: {config.triggers.Count}, показов сделано: {Shows}/{config.maxShows}");

            return Task.FromResult(true);
        }

        #region Триггеры

        private void OnTriggerEvent(int eventId)
        {
            if (Decided || _visible || _pending != null) return;
            if (Shows >= config.maxShows) return;

            _hits.TryGetValue(eventId, out int count);
            _hits[eventId] = ++count;

            foreach (var trigger in config.triggers)
            {
                if (trigger == null || trigger.eventId != eventId) continue;
                if (trigger.occurrence != count) continue;

                _pending = StartCoroutine(ShowAfter(trigger.delaySeconds));
                return;
            }
        }

        /// <summary>
        /// Отменить отложенный показ: игрок ушёл из спокойного места (начал новый
        /// забег), и просьба там уже некстати. Счётчик показов не тратится —
        /// покажем на следующем триггере.
        /// </summary>
        private void CancelPending()
        {
            if (_pending == null) return;
            StopCoroutine(_pending);
            _pending = null;
            LogRuntime("Отложенный показ окна вишлиста отменён — момент уже не тот");
        }

        private IEnumerator ShowAfter(float delay)
        {
            // Realtime: событие часто приходит на паузе (окно базы, экран итогов)
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            // Не лезем поверх чужой модалки. Технически UISystem справится — окна
            // встают в стек, — но просьба о вишлисте поверх экрана итогов
            // перекрывает его кнопки и раздражает: ждём, пока игрок разберётся
            if (config.waitForQuietMoment)
            {
                float waited = 0f;
                while (_ui != null && _ui.HasModal && waited < QuietWaitSeconds)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    waited += 0.5f;
                }

                if (_ui != null && _ui.HasModal)
                {
                    _pending = null;
                    yield break;   // игрок завис в меню — лучше промолчать
                }
            }

            _pending = null;
            Show();
        }

        #endregion

        #region Показ

        private void Show()
        {
            if (_visible || config == null) return;

            var result = UISystem.Open(WishlistPromptWindow.WindowId, config);
            if (result != NavigationResult.Success)
            {
                LogWarning($"Окно «{WishlistPromptWindow.WindowId}» не открылось ({result}). " +
                           "Создан ли префаб окна (ProtoSystem → LiveOps → Создать префаб окна вишлиста)?");
                return;
            }

            _visible = true;
            WishlistPromptState.RegisterShow();
            Track("wishlist_prompt_shown");
            LogRuntime($"Показ окна вишлиста ({Shows}/{config.maxShows})");
        }

        private void OnAnswered(WishlistPromptAnswer answer)
        {
            _visible = false;

            switch (answer)
            {
                case WishlistPromptAnswer.Added:
                    WishlistPromptState.MarkDecided();
                    Track("wishlist_prompt_added");
                    break;

                case WishlistPromptAnswer.AlreadyHas:
                    WishlistPromptState.MarkDecided();
                    Track("wishlist_prompt_already");
                    break;

                // Крестик — не решение: окно вернётся на следующем триггере,
                // пока не исчерпан maxShows
                case WishlistPromptAnswer.Dismissed:
                    Track("wishlist_prompt_dismissed");
                    break;
            }
        }

        #endregion

        #region Отладка (кнопки в инспекторе конфига)

        /// <summary>Сколько раз окно уже показывалось.</summary>
        public int ShownCount => Shows;

        /// <summary>Нажал ли игрок одну из двух кнопок — после этого окно молчит навсегда.</summary>
        public bool IsDecided => WishlistPromptState.Decided;

        /// <summary>
        /// Забыть решение игрока и счётчик показов. Нужно для проверки: окно
        /// по замыслу одноразовое, и без сброса второй раз его не увидеть.
        /// </summary>
        public void ResetPromptState()
        {
            WishlistPromptState.Reset();
            _hits.Clear();
            LogRuntime("Состояние окна вишлиста сброшено: показы и решение забыты");
        }

        /// <summary>Показать окно немедленно, минуя триггеры, лимит и ожидание тишины.</summary>
        public void ShowNow()
        {
            if (!Application.isPlaying) return;
            _visible = false;
            Show();
        }

        #endregion

        private void Track(string eventName)
        {
            _liveOps?.TrackEvent(eventName);
        }
    }
}
