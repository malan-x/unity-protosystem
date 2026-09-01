// Packages/com.protosystem.core/Runtime/LiveOps/WishlistPrompt/WishlistPromptSystem.cs
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using ProtoSystem.UI;

#if STEAMWORKS_NET
using Steamworks;
#endif

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
                _document.panelSettings = ResolvePanelSettings();
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
        /// PanelSettings обязателен для UIDocument. Свой в конфиге надёжнее,
        /// но если его забыли, берём любой из проекта: панель с чужим
        /// масштабированием лучше, чем невидимая панель.
        /// </summary>
        private PanelSettings ResolvePanelSettings()
        {
            if (config.panelSettings != null) return config.panelSettings;

            var found = Resources.FindObjectsOfTypeAll<PanelSettings>();
            if (found != null && found.Length > 0)
            {
                LogWarning($"В конфиге нет PanelSettings — взяли «{found[0].name}» из проекта.");
                return found[0];
            }

            LogWarning("PanelSettings не найдены — панель не отрисуется.");
            return null;
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
        /// Открывает страницу игры в оверлее Steam — там игрок жмёт «В желаемое»
        /// сам, не выходя из игры.
        ///
        /// Добавить в вишлист напрямую нельзя: у Steamworks такого API нет вовсе
        /// (EOverlayToStoreFlag умеет только корзину — None / AddToCart /
        /// AddToCartAndShow), и это сознательное ограничение Valve. Поэтому
        /// максимум, что доступно, — довести человека до кнопки в один клик.
        ///
        /// Оверлей доступен не всегда (выключен игроком, не-Steam сборка) —
        /// тогда открываем страницу магазина в браузере.
        /// </summary>
        private void OpenStore()
        {
#if STEAMWORKS_NET
            if (config.steamAppId > 0 && SteamInitProvider.IsInitialized && SteamUtils.IsOverlayEnabled())
            {
                SteamFriends.ActivateGameOverlayToStore(
                    new AppId_t(config.steamAppId),
                    EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
                return;
            }
#endif
            var url = config.ResolveStoreUrl();
            if (string.IsNullOrEmpty(url))
            {
                LogWarning("Ни AppID, ни URL магазина не заданы — открывать нечего.");
                return;
            }
            Application.OpenURL(url);
        }

        #endregion

        private void Track(string eventName)
        {
            _liveOps?.TrackEvent(eventName);
        }
    }
}
