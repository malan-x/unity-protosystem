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

        // Ключи PlayerPrefs общие для всех проектов: панель одна на игру
        private const string PrefDecided = "wishlist_prompt_decided";
        private const string PrefShows   = "wishlist_prompt_shows";

        [Dependency(required: false)] private LiveOpsSystem _liveOps;

        private readonly Dictionary<int, int> _hits = new();               // eventId -> сколько раз пришло
        private readonly List<System.Action<object>> _handlers = new();    // держим делегаты: RemoveEvent требует тот же экземпляр
        private readonly ToolkitLocalization _localization = new();

        private GameObject _overlay;
        private UIDocument _document;
        private PanelSettings _panelSettings;   // клон: порядок отрисовки свой, исходный ассет не трогаем
        private bool _visible;
        private Coroutine _pending;

        private bool Decided => !ignoreSavedDecision && PlayerPrefs.GetInt(PrefDecided, 0) == 1;
        private int  Shows   => PlayerPrefs.GetInt(PrefShows, 0);

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
            _pending = null;
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

            PlayerPrefs.SetInt(PrefShows, Shows + 1);
            PlayerPrefs.Save();

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

            // Фокус на «Добавить»: без него геймпад и Steam Deck остаются без курсора
            add?.Focus();
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
            _visible = false;
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
            PlayerPrefs.SetInt(PrefDecided, 1);
            PlayerPrefs.Save();
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
        public bool IsDecided => PlayerPrefs.GetInt(PrefDecided, 0) == 1;

        /// <summary>
        /// Забыть решение игрока и счётчик показов. Нужно для проверки: панель
        /// по замыслу одноразовая, и без сброса второй раз её не увидеть — а
        /// проверять приходится на каждой машине заново.
        /// </summary>
        public void ResetPromptState()
        {
            PlayerPrefs.DeleteKey(PrefDecided);
            PlayerPrefs.DeleteKey(PrefShows);
            PlayerPrefs.Save();
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
