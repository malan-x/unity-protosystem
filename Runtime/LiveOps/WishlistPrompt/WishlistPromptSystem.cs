// Packages/com.protosystem.core/Runtime/LiveOps/WishlistPrompt/WishlistPromptSystem.cs
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using ProtoSystem.UI;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Панель «Добавить в желаемое»: ловит игровые события из шины и в удачный
    /// момент просит игрока вишлистнуть игру.
    ///
    /// Почему через шину, а не вызовом из кода игры: момент «игроку сейчас
    /// хорошо» у каждого проекта свой (взял сектор, прошёл босса, построил
    /// здание), и зашивать его в пакет нельзя. Триггеры перечисляются в ассете
    /// конфига, который лежит В ПРОЕКТЕ, — пакет остаётся универсальным.
    ///
    /// Решение игрока необратимо: нажал любую из двух кнопок — панель больше
    /// не появится никогда. Крестик решением НЕ считается (панель придёт на
    /// следующем триггере), поэтому число показов ограничено maxShows.
    ///
    /// «Уже добавил» проверить нельзя: API вишлиста у Steam нет ни на чтение,
    /// ни на запись. Кнопка нужна не для учёта, а чтобы не доставать человека,
    /// который уже согласился. По той же причине «Добавить» лишь открывает
    /// страницу игры в оверлее — жать кнопку всё равно игроку.
    /// </summary>
    [ProtoSystemComponent("Wishlist Prompt", "Просьба добавить игру в желаемое в удачный момент",
        "LiveOps", "⭐", 155)]
    public class WishlistPromptSystem : InitializableSystemBase
    {
        #region InitializableSystemBase

        public override string SystemId => "wishlist_prompt";
        public override string DisplayName => "Wishlist Prompt";
        public override string Description => "Панель «Добавить в желаемое» по событиям шины";

        #endregion

        #region Serialized

        [SerializeField, InlineConfig] private WishlistPromptConfig config;

        [Header("Debug")]
        [Tooltip("Игнорировать сохранённое решение игрока — панель показывается каждый раз. Только для отладки.")]
        [SerializeField] private bool ignoreSavedDecision = false;

        #endregion

        #region State

        [Dependency(required: false)] private LiveOpsSystem _liveOps;
        [Dependency(required: false)] private UISystem _ui;

        /// <summary>Сколько ждём закрытия модального окна игры, прежде чем сдаться.</summary>
        private const float ModalWaitSeconds = 60f;

        private readonly Dictionary<int, int> _hits = new();               // eventId -> сколько раз пришло
        private readonly List<System.Action<object>> _handlers = new();    // держим делегаты: RemoveEvent требует тот же экземпляр
        private readonly ToolkitLocalization _localization = new();

        private GameObject _overlay;
        private UIDocument _document;
        private PanelSettings _panelSettings;   // клон: порядок отрисовки свой, исходный ассет не трогаем
        private VisualElement _modalRoot;       // затемнение: держит фокус и ловит клики
        private Button _focusTarget;
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

            // Отменяющие события: игрок ушёл из спокойного места, пока панель
            // ждала своего момента
            foreach (var id in config.cancelEventIds)
            {
                if (id == 0 || !subscribed.Add(id)) continue;
                System.Action<object> handler = _ => CancelPending();
                _handlers.Add(handler);
                AddEvent(id, handler);
            }
        }

        /// <summary>
        /// Забыть отложенный показ. Вызывается, когда игрок начал новый забег
        /// (или другое событие из cancelEventIds): панель дождалась закрытия
        /// экрана итогов, но показывать её посреди боя уже некстати — там фокус
        /// у игрового HUD, и выбрать вариант с геймпада всё равно нельзя.
        /// Счётчик показов при этом не тратится: покажем на следующем триггере.
        /// </summary>
        private void CancelPending()
        {
            if (_pending == null) return;
            StopCoroutine(_pending);
            _pending = null;
            LogRuntime("Отложенный показ панели отменён — момент уже не тот");
        }

        #endregion

        public override Task<bool> InitializeAsync()
        {
            if (config == null)
            {
                LogWarning("Конфиг не назначен — панель не появится.");
                return Task.FromResult(true);
            }

            if (Decided)
                LogInit("Игрок уже решил — панель отключена.");
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

        private IEnumerator ShowAfter(float delay)
        {
            // Realtime: событие часто приходит на паузе (окно базы, экран итогов)
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            // Ждём, пока закроются модальные окна игры. Триггеры вроде «забег
            // завершён» приходят ровно тогда, когда открыт экран итогов, и
            // панель поверх него перекрывала его же кнопки: выйти было нечем,
            // а затемнение оставалось висеть чёрным экраном.
            float waited = 0f;
            while (_ui != null && _ui.HasModal && waited < ModalWaitSeconds)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                waited += 0.5f;
            }

            _pending = null;

            // Игрок завис в меню — лучше не показать вовсе, чем выскочить
            // спустя минуту посреди следующего боя
            if (_ui != null && _ui.HasModal) yield break;

            Show();
        }

        #endregion

        #region Показ

        private void Show()
        {
            if (_visible || config == null) return;
            if (config.template == null)
            {
                LogWarning("В конфиге нет шаблона (VisualTreeAsset) — показывать нечего.");
                return;
            }

            BuildOverlay();
            _visible = true;

            WishlistPromptState.RegisterShow();

            Track("wishlist_prompt_shown");
            LogRuntime($"Показ панели вишлиста ({Shows}/{config.maxShows})");
        }

        private void BuildOverlay()
        {
            if (_overlay == null)
            {
                _overlay = new GameObject("WishlistPromptOverlay");
                Object.DontDestroyOnLoad(_overlay);
                _document = _overlay.AddComponent<UIDocument>();
                _document.panelSettings = BuildPanelSettings();
                _document.sortingOrder  = config.sortingOrder;
            }

            _overlay.SetActive(true);

            var root = _document.rootVisualElement;
            root.Clear();
            config.template.CloneTree(root);

            SetText(root, "title", config.titleText);
            SetText(root, "body",  config.bodyText);

            var add     = root.Q<Button>("add-button");
            var already = root.Q<Button>("already-button");
            var close   = root.Q<Button>("close-button");

            if (add != null)
            {
                add.text = config.addText;
                add.clicked += OnAddClicked;
            }
            if (already != null)
            {
                already.text = config.alreadyText;
                already.clicked += OnAlreadyClicked;
            }
            if (close != null)
            {
                close.style.display = config.showCloseButton ? DisplayStyle.Flex : DisplayStyle.None;
                close.clicked += OnCloseClicked;
            }

            _localization.Localize(root);

            MakeModal(root, add);
        }

        /// <summary>
        /// Мягкая модальность: затемнение растянуто на весь экран и ловит клики,
        /// поэтому мышью сквозь панель до игры не добраться.
        ///
        /// Фокус НЕ удерживаем силой. Первая версия возвращала его в панель на
        /// каждый FocusOut — и ломала навигацию геймпадом между самими кнопками
        /// панели. От «ушёл и не вернулся» защищаемся иначе: панель ждёт, пока
        /// закроются модальные окна игры, и не наслаивается на них.
        ///
        /// Растяжку и цвет задаём кодом, а не только в USS: со своим шаблоном
        /// в проекте панель осталась бы прозрачной для кликов.
        /// </summary>
        private void MakeModal(VisualElement root, Button focusTarget)
        {
            _modalRoot = root.Q("wishlist-root") ?? root;

            _modalRoot.style.position = Position.Absolute;
            _modalRoot.style.left = 0;
            _modalRoot.style.top = 0;
            _modalRoot.style.right = 0;
            _modalRoot.style.bottom = 0;
            _modalRoot.style.backgroundColor = config.scrimColor;
            _modalRoot.pickingMode = PickingMode.Position;   // затемнение ловит клики

            _modalRoot.RegisterCallback<NavigationCancelEvent>(OnModalCancel);

            _focusTarget = focusTarget;
            _modalRoot.focusable = true;

            // Фокус ставим следующим кадром: сразу после CloneTree дерево ещё не
            // разложено, Focus() уходит в пустоту — панель появлялась без фокуса,
            // и с геймпада выбрать вариант было нельзя. Сначала забираем фокус на
            // затемнение (панель UI Toolkit становится активной), потом на кнопку.
            _modalRoot.Focus();
            _modalRoot.schedule.Execute(() =>
            {
                if (!_visible) return;
                if (_focusTarget != null) _focusTarget.Focus();
                else _modalRoot?.Focus();
            }).ExecuteLater(16);
        }


        /// <summary>
        /// Esc и «кружок» геймпада закрывают панель — но только если крестик
        /// разрешён. Иначе выхода не будет вовсе, а запирать игрока в просьбе
        /// о вишлисте — худшее, что можно сделать с этой панелью.
        /// </summary>
        private void OnModalCancel(NavigationCancelEvent evt)
        {
            if (!_visible || !config.showCloseButton) return;
            evt.StopPropagation();
            OnCloseClicked();
        }

        private void SetText(VisualElement root, string name, string value)
        {
            var label = root.Q<Label>(name);
            if (label != null) label.text = value;
        }

        /// <summary>
        /// Отдельный клон PanelSettings с поднятым порядком отрисовки.
        ///
        /// Грабли, на которых панель молча не появлялась: UIDocument.sortingOrder
        /// упорядочивает документы только ВНУТРИ одной панели, а порядок между
        /// разными PanelSettings задаёт их собственный sortingOrder. Игровые окна
        /// живут на своих ассетах с высоким порядком, и панель на общем
        /// (sortingOrder = 0) создавалась, считалась показанной и рисовалась под
        /// ними. Поэтому клонируем ассет и ставим порядок из конфига.
        ///
        /// Клон нужен именно клон: правка исходного ассета в рантайме утечёт в
        /// проект в редакторе и поменяет порядок всем, кто им пользуется.
        /// </summary>
        private PanelSettings BuildPanelSettings()
        {
            var source = config.panelSettings;

            if (source == null)
            {
                var found = Resources.FindObjectsOfTypeAll<PanelSettings>();
                if (found != null && found.Length > 0)
                {
                    source = found[0];
                    LogWarning($"В конфиге нет PanelSettings — взяли «{source.name}» из проекта.");
                }
            }

            if (source == null)
            {
                LogWarning("PanelSettings не найдены — панель не отрисуется.");
                return null;
            }

            _panelSettings = Instantiate(source);
            _panelSettings.name = source.name + " (WishlistPrompt)";
            _panelSettings.sortingOrder = config.sortingOrder;
            return _panelSettings;
        }

        private void OnDestroy()
        {
            // Клон PanelSettings живёт ровно столько, сколько оверлей
            if (_panelSettings != null) Destroy(_panelSettings);
            if (_overlay != null) Destroy(_overlay);
        }

        private void Hide()
        {
            // Сначала снимаем модальность: иначе FocusOut при скрытии утащит
            // фокус обратно в уже закрытую панель
            _visible = false;
            _modalRoot = null;
            _focusTarget = null;

            if (_overlay != null) _overlay.SetActive(false);
        }

        #endregion

        #region Кнопки

        private void OnAddClicked()
        {
            OpenStore();
            Decide("wishlist_prompt_added");
        }

        private void OnAlreadyClicked()
        {
            Decide("wishlist_prompt_already");
        }

        private void OnCloseClicked()
        {
            // Крестик — не решение: панель вернётся на следующем триггере,
            // пока не исчерпан maxShows
            Track("wishlist_prompt_dismissed");
            Hide();
        }

        private void Decide(string telemetryEvent)
        {
            WishlistPromptState.MarkDecided();
            Track(telemetryEvent);
            Hide();
        }

        /// <summary>
        /// Открывает страницу игры (оверлей Steam или браузер) — см. StoreLink,
        /// он же используется кнопкой вишлиста в меню, чтобы поведение совпадало.
        /// </summary>
        private void OpenStore()
        {
            if (!StoreLink.OpenStorePage(config))
                LogWarning("Ни AppID, ни URL магазина не заданы — открывать нечего.");
        }

        #endregion

        private void Track(string eventName)
        {
            _liveOps?.TrackEvent(eventName);
        }

        #region Отладка (кнопки в инспекторе)

        /// <summary>Сколько раз панель уже показывалась.</summary>
        public int ShownCount => Shows;

        /// <summary>Нажал ли игрок одну из двух кнопок — после этого панель молчит навсегда.</summary>
        public bool IsDecided => WishlistPromptState.Decided;

        /// <summary>
        /// Забыть решение игрока и счётчик показов. Нужно для проверки: панель
        /// по замыслу одноразовая, и без сброса второй раз её не увидеть — а
        /// проверять приходится на каждой машине заново.
        /// </summary>
        public void ResetPromptState()
        {
            WishlistPromptState.Reset();
            _hits.Clear();
            LogRuntime("Состояние панели сброшено: показы и решение забыты");
        }

        /// <summary>
        /// Показать панель немедленно, минуя триггеры и лимит показов.
        /// Счётчик при этом растёт как при обычном показе — чтобы проверять
        /// именно то поведение, которое увидит игрок.
        /// </summary>
        public void ShowNow()
        {
            if (!Application.isPlaying) return;
            _visible = false;
            Show();
        }

        #endregion
    }
}
