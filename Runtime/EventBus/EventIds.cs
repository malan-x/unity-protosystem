// События ProtoSystem пакета
// Использование: EventBus.Publish(Evt.UI.WindowOpened, windowId);
// Диапазон ID: 10000+ (не конфликтует с проектными событиями)
//
// Evt — старые имена событий. Модули пакета со временем завели свои события
// EventBus.<Модуль> (UI, Settings, SceneFlow, Session, Localization, Lobby, CursorEvents)
// и заняли диапазоны, которые здесь были объявлены под другие события. Поэтому:
// если у события Evt есть настоящее событие модуля, константа — его алиас;
// если нет — у неё собственный id в свободном диапазоне. Один номер не может
// означать два разных события: это проверяет EventIdsTests.

namespace ProtoSystem
{
    /// <summary>
    /// ID событий ProtoSystem пакета
    /// </summary>
    public static class Evt
    {
        /// <summary>
        /// События UI системы.
        /// WindowOpened/WindowClosed — алиасы реально публикуемых навигатором
        /// EventBus.UI.* (10200+). Раньше здесь были собственные id 10001/10002,
        /// которые навигатор НЕ публиковал — подписчики Evt.UI.WindowOpened молча
        /// не получали событий (payload — WindowEventData).
        /// </summary>
        public static class UI
        {
            public const int WindowOpened = EventBus.UI.WindowOpened;   // 10200
            public const int WindowClosed = EventBus.UI.WindowClosed;   // 10201
            public const int WindowShown = 10003;
            public const int WindowHidden = 10004;
            public const int NavigationBack = 10005;
            public const int ButtonClicked = 10010;
            public const int ButtonHovered = 10011;
            public const int SliderChanged = 10012;
            public const int ToggleChanged = 10013;
            public const int DropdownChanged = 10014;
        }
        
        /// <summary>
        /// События настроек — алиасы EventBus.Settings.* (10100-10199), которые публикует SettingsSystem.
        /// Раньше здесь были собственные id того же диапазона: MasterVolumeChanged (10101)
        /// совпадал с Settings.Saved, MuteChanged (10110) — с Settings.Audio.MasterChanged и т.д.
        /// </summary>
        public static class Settings
        {
            public static class Audio
            {
                public const int MasterVolumeChanged = EventBus.Settings.Audio.MasterChanged;
                public const int MusicVolumeChanged = EventBus.Settings.Audio.MusicChanged;
                public const int SFXVolumeChanged = EventBus.Settings.Audio.SFXChanged;
                public const int VoiceVolumeChanged = EventBus.Settings.Audio.VoiceChanged;
                public const int AmbientVolumeChanged = EventBus.Settings.Audio.AmbientChanged;
                public const int UIVolumeChanged = EventBus.Settings.Audio.UIChanged;
                public const int MuteChanged = EventBus.Settings.Audio.MuteChanged;
            }

            public static class Video
            {
                public const int ResolutionChanged = EventBus.Settings.Video.ResolutionChanged;
                public const int QualityChanged = EventBus.Settings.Video.QualityChanged;
                public const int FullscreenChanged = EventBus.Settings.Video.FullscreenChanged;
                public const int VSyncChanged = EventBus.Settings.Video.VSyncChanged;
            }

            public const int SettingsSaved = EventBus.Settings.Saved;
            public const int SettingsLoaded = EventBus.Settings.Loaded;
            public const int SettingsReset = EventBus.Settings.ResetToDefaults;
        }

        /// <summary>
        /// События игровой сессии — алиасы EventBus.Session.* (10600-10699), которые публикует GameSessionSystem.
        /// Раньше здесь были собственные id 10201-10208: GameSessionSystem их не публиковал,
        /// а 10201-10206 совпадали с EventBus.UI.* — подписчик Evt.Session.Started получал
        /// закрытие окна, Evt.Session.Ended — фокус окна, Evt.Session.Paused — завершение навигации.
        /// </summary>
        public static class Session
        {
            public const int Started = EventBus.Session.Started;
            public const int Ended = EventBus.Session.Ended;
            public const int Reset = EventBus.Session.Reset;
            public const int Paused = EventBus.Session.Paused;
            public const int Resumed = EventBus.Session.Resumed;
            public const int StateChanged = EventBus.Session.StateChanged;
            public const int ReturnedToMenu = EventBus.Session.ReturnedToMenu;
            public const int RestartRequested = EventBus.Session.RestartRequested;
        }

        /// <summary>
        /// События сцен — алиасы EventBus.SceneFlow.* (10300-10399), которые публикует SceneFlowSystem.
        /// Раньше id были сдвинуты на единицу: Evt.Scene.LoadStarted (10301) совпадал с
        /// SceneFlow.LoadProgress, LoadCompleted (10303) — с SceneFlow.LoadFailed.
        /// </summary>
        public static class Scene
        {
            public const int LoadStarted = EventBus.SceneFlow.LoadStarted;
            public const int LoadProgress = EventBus.SceneFlow.LoadProgress;
            public const int LoadCompleted = EventBus.SceneFlow.LoadCompleted;
            public const int Unloaded = EventBus.SceneFlow.UnloadCompleted;
            public const int TransitionStarted = EventBus.SceneFlow.TransitionStarted;
            public const int TransitionCompleted = EventBus.SceneFlow.TransitionCompleted;
        }

        /// <summary>
        /// События звуковой системы (10403-10499).
        /// 10400-10402 заняты EventBus.CursorEvents: Play и Stop раньше были 10401/10402
        /// и совпадали с CursorEvents.VisibilityChanged/CursorChanged.
        /// </summary>
        public static class Sound
        {
            public const int Play = 10403;
            public const int Stop = 10404;
            public const int PlayMusic = 10410;
            public const int StopMusic = 10411;
            public const int CrossfadeMusic = 10412;
            public const int VolumeChanged = 10420;
            public const int MuteChanged = 10421;
            public const int SnapshotActivated = 10430;
            public const int SnapshotDeactivated = 10431;
            public const int BankLoaded = 10440;
            public const int BankUnloaded = 10441;
        }
        
        /// <summary>
        /// События сети (11400-11499).
        /// Пакет их не публикует: события лобби — EventBus.Lobby.* (10500-10599).
        /// Диапазон перенесён с 10500-х — там те же номера означали другие события лобби
        /// (Evt.Network.Connected = Lobby.ClientConnecting, LobbyJoined = Lobby.LobbyClosed).
        /// </summary>
        public static class Network
        {
            public const int Connected = 11401;
            public const int Disconnected = 11402;
            public const int ServerStarted = 11403;
            public const int ServerStopped = 11404;
            public const int ClientConnected = 11410;
            public const int ClientDisconnected = 11411;
            public const int LobbyCreated = 11420;
            public const int LobbyJoined = 11421;
            public const int LobbyLeft = 11422;
        }
        
        /// <summary>
        /// События системы инициализации (11200-11299).
        /// ВНИМАНИЕ: диапазон перенесён с 10600-х — те заняты GameSession
        /// (Initialization.Completed = 10603 совпадал с Session.Paused, и подписчик
        /// на «завершение инициализации» получал паузы игры). События публикует
        /// SystemInitializationManager.
        /// </summary>
        public static class Initialization
        {
            public const int Started = 11201;
            public const int Progress = 11202;
            public const int Completed = 11203;   // payload: bool allSucceeded
            public const int Failed = 11204;
            public const int SystemInitialized = 11210;   // payload: string systemName
            public const int SystemFailed = 11211;        // payload: string systemName
        }
        
        /// <summary>
        /// События эффектов (11300-11399).
        /// Диапазон перенесён с 10700-х — они заняты EventBus.Localization: Play (10701)
        /// совпадал с Localization.Ready, Stop (10702) — с Localization.TableLoaded.
        /// </summary>
        public static class Effects
        {
            public const int Play = 11301;
            public const int Stop = 11302;
            public const int StopAll = 11303;
        }

        /// <summary>
        /// События курсора — алиасы EventBus.CursorEvents.* (10400-10402), которые публикует CursorManagerSystem.
        /// Раньше здесь были собственные id 10801/10802, которые никто не публиковал.
        /// </summary>
        public static class Cursor
        {
            public const int ModeChanged = EventBus.CursorEvents.ModeChanged;
            public const int VisibilityChanged = EventBus.CursorEvents.VisibilityChanged;
        }
        
        /// <summary>
        /// События игры (общие) (10900-10999)
        /// </summary>
        public static class Game
        {
            public const int Started = 10901;
            public const int Paused = 10902;
            public const int Resumed = 10903;
            public const int Ended = 10904;
        }

        /// <summary>
        /// События захвата (11000-11099)
        /// </summary>
        public static class Capture
        {
            public const int ScreenshotTaken  = 11001;
            public const int RecordingStarted = 11002;
            public const int RecordingStopped = 11003;
            public const int ReplaySaved      = 11004;
        }

        /// <summary>
        /// События LiveOps / Community Panel (11100-11199)
        /// </summary>
        public static class LiveOps
        {
            /// <summary>
            /// Данные LiveOps обновлены. Payload: LiveOpsDataPayload { Type, Data }.
            /// Type — константа из LiveOpsDataType ("panel_config", "polls", etc.)
            /// </summary>
            public const int DataUpdated = 11100;

            /// <summary>
            /// Layout панели изменился (expand/collapse, conversation open/close, cards populated).
            /// Payload: string — идентификатор причины ("expanded", "collapsed", "conversation_open",
            /// "conversation_close", "cards_populated").
            /// Позволяет родительскому окну перестроить навигацию.
            /// </summary>
            public const int LayoutChanged = 11101;
        }
    }
}
