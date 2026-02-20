// События ProtoSystem пакета
// Использование: EventBus.Publish(Evt.UI.WindowOpened, windowId);
// Диапазон ID: 10000+ (не конфликтует с проектными событиями)

namespace ProtoSystem
{
    /// <summary>
    /// ID событий ProtoSystem пакета
    /// </summary>
    public static class Evt
    {
        /// <summary>
        /// События UI системы (10000-10099)
        /// </summary>
        public static class UI
        {
            public const int WindowOpened = 10001;
            public const int WindowClosed = 10002;
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
        /// События настроек (10100-10199)
        /// </summary>
        public static class Settings
        {
            public static class Audio
            {
                public const int MasterVolumeChanged = 10101;
                public const int MusicVolumeChanged = 10102;
                public const int SFXVolumeChanged = 10103;
                public const int VoiceVolumeChanged = 10104;
                public const int AmbientVolumeChanged = 10105;
                public const int UIVolumeChanged = 10106;
                public const int MuteChanged = 10110;
            }
            
            public static class Video
            {
                public const int ResolutionChanged = 10120;
                public const int QualityChanged = 10121;
                public const int FullscreenChanged = 10122;
                public const int VSyncChanged = 10123;
            }
            
            public const int SettingsSaved = 10190;
            public const int SettingsLoaded = 10191;
            public const int SettingsReset = 10192;
        }
        
        /// <summary>
        /// События игровой сессии (10200-10299)
        /// </summary>
        public static class Session
        {
            public const int Started = 10201;
            public const int Ended = 10202;
            public const int Reset = 10203;
            public const int Paused = 10204;
            public const int Resumed = 10205;
            public const int StateChanged = 10206;
            public const int ReturnedToMenu = 10207;
            public const int RestartRequested = 10208;
        }
        
        /// <summary>
        /// События сцен (10300-10399)
        /// </summary>
        public static class Scene
        {
            public const int LoadStarted = 10301;
            public const int LoadProgress = 10302;
            public const int LoadCompleted = 10303;
            public const int Unloaded = 10304;
            public const int TransitionStarted = 10310;
            public const int TransitionCompleted = 10311;
        }
        
        /// <summary>
        /// События звуковой системы (10400-10499)
        /// </summary>
        public static class Sound
        {
            public const int Play = 10401;
            public const int Stop = 10402;
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
        /// События сети (10500-10599)
        /// </summary>
        public static class Network
        {
            public const int Connected = 10501;
            public const int Disconnected = 10502;
            public const int ServerStarted = 10503;
            public const int ServerStopped = 10504;
            public const int ClientConnected = 10510;
            public const int ClientDisconnected = 10511;
            public const int LobbyCreated = 10520;
            public const int LobbyJoined = 10521;
            public const int LobbyLeft = 10522;
        }
        
        /// <summary>
        /// События системы инициализации (10600-10699)
        /// </summary>
        public static class Initialization
        {
            public const int Started = 10601;
            public const int Progress = 10602;
            public const int Completed = 10603;
            public const int Failed = 10604;
            public const int SystemInitialized = 10610;
            public const int SystemFailed = 10611;
        }
        
        /// <summary>
        /// События эффектов (10700-10799)
        /// </summary>
        public static class Effects
        {
            public const int Play = 10701;
            public const int Stop = 10702;
            public const int StopAll = 10703;
        }
        
        /// <summary>
        /// События курсора (10800-10899)
        /// </summary>
        public static class Cursor
        {
            public const int ModeChanged = 10801;
            public const int VisibilityChanged = 10802;
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
    }
}
