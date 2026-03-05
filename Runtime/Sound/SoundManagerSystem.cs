using System;
using System.Threading.Tasks;
using UnityEngine;
using ProtoSystem.Settings;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Центральная система управления звуком
    /// </summary>
    [ProtoSystemComponent("Sound Manager", "Управление звуком, музыкой и audio snapshots", "Core", "🔊", 12)]
    public class SoundManagerSystem : InitializableSystemBase, IResettable
    {
        public override string SystemId => "sound_manager";
        public override string DisplayName => "Sound Manager";
        public override string Description => "Центральная система управления звуком. Воспроизводит звуки, музыку, управляет громкостью и snapshots.";
        
        [Header("Configuration")]
        [SerializeField, InlineConfig] private SoundManagerConfig config;
        
        // === Зависимости ===
        [Dependency(required: false, description: "Интеграция с UI")]
        private UI.UISystem _uiSystem;
        
        [Dependency(required: false, description: "Интеграция с игровой сессией")]
        private GameSessionSystem _gameSessionSystem;
        
        [Dependency(required: false, description: "Интеграция с системой настроек")]
        private Settings.SettingsSystem _settingsSystem;
        
        // === Провайдер ===
        private ISoundProvider _provider;
        
        // === Синглтон ===
        private static SoundManagerSystem _instance;
        public static SoundManagerSystem Instance => _instance;
        
        // === Публичные свойства ===
        public SoundManagerConfig Config => config;
        public ISoundProvider Provider => _provider;
        public bool IsInitialized => _provider != null;
        
        // === События ===
        public event Action<SoundCategory, float> OnVolumeChanged;
        public event Action<bool> OnMuteChanged;
        public event Action<string> OnMusicChanged;
        
        // === Lifecycle ===
        
        protected override void Awake()
        {
            base.Awake();
            
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }
        
        protected override void InitEvents()
        {
            // UI события
            AddEvent(EventBus.UI.WindowOpened, OnWindowOpened);
            AddEvent(EventBus.UI.WindowClosed, OnWindowClosed);

            // GameSession события
            AddEvent(Evt.Session.Started, OnSessionStarted);
            AddEvent(Evt.Session.Ended, OnSessionEnded);
            AddEvent(Evt.Session.Paused, OnSessionPaused);
            AddEvent(Evt.Session.Resumed, OnSessionResumed);
            AddEvent(Evt.Session.StateChanged, OnSessionStateChanged);

            // Settings события - используем EventBus.Settings.Audio (из SettingsEvents.cs)
            // с проверкой инициализации и типа payload
            AddEvent(EventBus.Settings.Audio.MasterChanged, OnMasterVolumeChanged);
            AddEvent(EventBus.Settings.Audio.MusicChanged, OnMusicVolumeChanged);
            AddEvent(EventBus.Settings.Audio.SFXChanged, OnSFXVolumeChanged);
            AddEvent(EventBus.Settings.Audio.VoiceChanged, OnVoiceVolumeChanged);
            AddEvent(EventBus.Settings.Audio.AmbientChanged, OnAmbientVolumeChanged);
            AddEvent(EventBus.Settings.Audio.UIChanged, OnUIVolumeChanged);

            // Scene события
            AddEvent(Evt.Scene.LoadStarted, OnSceneLoadStarted);
            AddEvent(Evt.Scene.LoadCompleted, OnSceneLoadCompleted);
            AddEvent(Evt.Scene.Unloaded, OnSceneUnloaded);

            // Sound события (для внешнего управления через EventBus)
            AddEvent(Evt.Sound.Play, OnPlaySoundEvent);
            AddEvent(Evt.Sound.Stop, OnStopSoundEvent);
            AddEvent(Evt.Sound.PlayMusic, OnPlayMusicEvent);
            AddEvent(Evt.Sound.StopMusic, OnStopMusicEvent);
        }

        private void OnMasterVolumeChanged(object payload)
        {
            if (!IsInitialized) return;
            if (payload is SettingChangedData<float> data)
                SetVolume(SoundCategory.Master, data.Value);
        }

        private void OnMusicVolumeChanged(object payload)
        {
            if (!IsInitialized) return;
            if (payload is SettingChangedData<float> data)
                SetVolume(SoundCategory.Music, data.Value);
        }

        private void OnSFXVolumeChanged(object payload)
        {
            if (!IsInitialized) return;
            if (payload is SettingChangedData<float> data)
                SetVolume(SoundCategory.SFX, data.Value);
        }

        private void OnVoiceVolumeChanged(object payload)
        {
            if (!IsInitialized) return;
            if (payload is SettingChangedData<float> data)
                SetVolume(SoundCategory.Voice, data.Value);
        }

        private void OnAmbientVolumeChanged(object payload)
        {
            if (!IsInitialized) return;
            if (payload is SettingChangedData<float> data)
                SetVolume(SoundCategory.Ambient, data.Value);
        }

        private void OnUIVolumeChanged(object payload)
        {
            if (!IsInitialized) return;
            if (payload is SettingChangedData<float> data)
                SetVolume(SoundCategory.UI, data.Value);
        }
        
        public override async Task<bool> InitializeAsync()
        {
            ReportProgress(0.1f);
            
            if (config == null)
            {
                LogError("SoundManagerConfig not assigned!");
                return false;
            }
            
            if (config.soundLibrary == null)
            {
                LogError("SoundLibrary not assigned in config!");
                return false;
            }
            
            ReportProgress(0.3f);
            
            // Создать провайдер
            _provider = CreateProvider();
            if (_provider == null)
            {
                LogError("Failed to create sound provider!");
                return false;
            }
            
            ReportProgress(0.5f);
            
            // Инициализировать провайдер
            _provider.Initialize(config, config.soundLibrary);
            
            ReportProgress(0.7f);
            
            // Применить настройки из SettingsSystem
            ApplySettingsFromSettingsSystem();
            
            ReportProgress(1.0f);
            
            LogMessage($"Initialized with {config.providerType} provider");
            return true;
        }
        
        private void Update()
        {
            _provider?.Update();
        }
        
        private void OnDestroy()
        {
            _provider?.Dispose();
            _provider = null;
            
            if (_instance == this)
                _instance = null;
        }
        
        // === Создание провайдера ===
        
        private ISoundProvider CreateProvider()
        {
            return config.providerType switch
            {
                SoundProviderType.Unity => new UnitySoundProvider(),
                SoundProviderType.FMOD => CreateFMODProvider(),
                SoundProviderType.Wwise => CreateWwiseProvider(),
                _ => new UnitySoundProvider()
            };
        }
        
        private ISoundProvider CreateFMODProvider()
        {
            // Попытка создать FMOD провайдер через рефлексию
            var type = Type.GetType("ProtoSystem.Sound.FMOD.FMODSoundProvider, ProtoSystem.Sound.FMOD");
            if (type != null)
            {
                return (ISoundProvider)Activator.CreateInstance(type);
            }
            
            LogWarning("FMOD provider not found, falling back to Unity");
            return new UnitySoundProvider();
        }
        
        private ISoundProvider CreateWwiseProvider()
        {
            // Заглушка для Wwise
            LogWarning("Wwise provider not implemented, falling back to Unity");
            return new UnitySoundProvider();
        }
        
        // === Статические методы (удобный доступ) ===
        
        /// <summary>
        /// Воспроизвести звук
        /// </summary>
        public static SoundHandle Play(string id, Vector3? position = null, float volumeMultiplier = 1f)
        {
            return Instance?._provider?.Play(id, position, volumeMultiplier) ?? SoundHandle.Invalid;
        }
        
        /// <summary>
        /// Воспроизвести звук (короткая форма)
        /// </summary>
        public static SoundHandle Play(string id, float volumeMultiplier)
        {
            return Play(id, null, volumeMultiplier);
        }
        
        /// <summary>
        /// Остановить звук
        /// </summary>
        public static void Stop(SoundHandle handle)
        {
            Instance?._provider?.Stop(handle);
        }
        
        /// <summary>
        /// Воспроизвести музыку
        /// </summary>
        public static void PlayMusic(string id, float fadeIn = 0f)
        {
            Instance?._provider?.PlayMusic(id, fadeIn);
            Instance?.OnMusicChanged?.Invoke(id);
        }
        
        /// <summary>
        /// Остановить музыку
        /// </summary>
        public static void StopMusic(float fadeOut = 0f)
        {
            Instance?._provider?.StopMusic(fadeOut);
            Instance?.OnMusicChanged?.Invoke(null);
        }
        
        /// <summary>
        /// Кроссфейд к музыке
        /// </summary>
        public static void CrossfadeMusic(string id, float time = 2f)
        {
            Instance?._provider?.CrossfadeMusic(id, time);
            Instance?.OnMusicChanged?.Invoke(id);
        }
        
        /// <summary>
        /// Установить параметр музыки
        /// </summary>
        public static void SetMusicParameter(string parameter, float value)
        {
            Instance?._provider?.SetMusicParameter(parameter, value);
        }
        
        /// <summary>
        /// Установить громкость категории
        /// </summary>
        public static void SetVolume(SoundCategory category, float volume)
        {
            Instance?._provider?.SetVolume(category, volume);
            Instance?.OnVolumeChanged?.Invoke(category, volume);
        }
        
        /// <summary>
        /// Получить громкость категории
        /// </summary>
        public static float GetVolume(SoundCategory category)
        {
            return Instance?._provider?.GetVolume(category) ?? 1f;
        }
        
        /// <summary>
        /// Установить mute
        /// </summary>
        public static void SetMute(bool muted)
        {
            Instance?._provider?.SetMute(muted);
            Instance?.OnMuteChanged?.Invoke(muted);
        }
        
        /// <summary>
        /// Активировать snapshot
        /// </summary>
        public static void SetSnapshot(SoundSnapshotId snapshot, float transitionTime = 0.5f)
        {
            Instance?._provider?.SetSnapshot(snapshot, transitionTime);
        }
        
        /// <summary>
        /// Деактивировать snapshot
        /// </summary>
        public static void ClearSnapshot(SoundSnapshotId snapshot, float transitionTime = 0.5f)
        {
            Instance?._provider?.ClearSnapshot(snapshot, transitionTime);
        }
        
        /// <summary>
        /// Загрузить банк
        /// </summary>
        public static Task<bool> LoadBankAsync(string bankId)
        {
            return Instance?._provider?.LoadBankAsync(bankId) ?? Task.FromResult(false);
        }
        
        /// <summary>
        /// Выгрузить банк
        /// </summary>
        public static void UnloadBank(string bankId)
        {
            Instance?._provider?.UnloadBank(bankId);
        }
        
        /// <summary>
        /// Установить процессор звуков (для occlusion и т.д.)
        /// </summary>
        public static void SetSoundProcessor(ISoundProcessor processor)
        {
            Instance?._provider?.SetSoundProcessor(processor);
        }
        
        // === IResettable ===
        
        public void ResetState(object resetData = null)
        {
            _provider?.StopAll();
            _provider?.ClearAllSnapshots(0);
            
            // Воспроизвести музыку меню
            if (config?.sessionScheme != null)
            {
                var menuMusic = config.sessionScheme.menuMusic;
                if (!string.IsNullOrEmpty(menuMusic))
                {
                    PlayMusic(menuMusic, config.sessionScheme.musicFadeTime);
                }
            }
        }
        
        // === Интеграция с SettingsSystem ===
        
        private void ApplySettingsFromSettingsSystem()
        {
            // Запрашиваем начальные значения из SettingsSystem
            // События не используем - они придут при изменении настроек пользователем
            if (_settingsSystem?.Audio == null)
            {
                LogMessage("SettingsSystem not available, using default volumes");
                return;
            }

            var audio = _settingsSystem.Audio;
            SetVolume(SoundCategory.Master, audio.MasterVolume);
            SetVolume(SoundCategory.Music, audio.MusicVolume);
            SetVolume(SoundCategory.SFX, audio.SFXVolume);
            SetVolume(SoundCategory.Voice, audio.VoiceVolume);

            // Ambient и UI — из defaultVolumes конфига, т.к. они не в AudioSettings
            if (config != null)
            {
                SetVolume(SoundCategory.Ambient, config.defaultVolumes.ambient);
                SetVolume(SoundCategory.UI, config.defaultVolumes.ui);
            }

            LogMessage($"Applied audio settings from SettingsSystem: Master={audio.MasterVolume:F2}, Music={audio.MusicVolume:F2}, SFX={audio.SFXVolume:F2}, Voice={audio.VoiceVolume:F2}");
        }
        
        // === Обработчики событий UI ===
        
        private void OnWindowOpened(object payload)
        {
            if (config?.uiScheme == null) return;

            string windowId = null;
            bool isModal = false;

            // Парсим payload
            if (payload is UI.WindowEventData eventData)
            {
                windowId = eventData.WindowId;
                isModal = eventData.Type == UI.WindowType.Modal;
            }
            else if (payload is string id)
            {
                windowId = id;
            }

            if (string.IsNullOrEmpty(windowId)) return;

            // Воспроизвести звук
            var sound = config.uiScheme.GetOpenSound(windowId, isModal);
            if (!string.IsNullOrEmpty(sound))
            {
                Play(sound);
            }

            // Применить snapshot для модальных окон
            if (isModal && !config.uiScheme.modalSnapshot.IsEmpty)
            {
                SetSnapshot(config.uiScheme.modalSnapshot);
            }
        }
        
        private void OnWindowClosed(object payload)
        {
            if (config?.uiScheme == null) return;

            string windowId = null;
            bool isModal = false;

            // Парсим payload
            if (payload is UI.WindowEventData eventData)
            {
                windowId = eventData.WindowId;
                isModal = eventData.Type == UI.WindowType.Modal;
            }
            else if (payload is string id)
            {
                windowId = id;
            }

            if (string.IsNullOrEmpty(windowId)) return;

            // Воспроизвести звук
            var sound = config.uiScheme.GetCloseSound(windowId, isModal);
            if (!string.IsNullOrEmpty(sound))
            {
                Play(sound);
            }

            // Снять snapshot для модальных окон
            if (isModal && !config.uiScheme.modalSnapshot.IsEmpty)
            {
                ClearSnapshot(config.uiScheme.modalSnapshot);
            }
        }
        
        // === Обработчики событий GameSession ===
        
        private void OnSessionStarted(object payload)
        {
            if (config?.sessionScheme == null) return;
            
            // Stinger
            var stinger = config.sessionScheme.sessionStartStinger;
            if (!string.IsNullOrEmpty(stinger))
            {
                Play(stinger);
            }
            
            // Музыка
            var music = config.sessionScheme.gameplayMusic;
            if (!string.IsNullOrEmpty(music))
            {
                CrossfadeMusic(music, config.sessionScheme.musicFadeTime);
            }
        }
        
        private void OnSessionEnded(object payload)
        {
            if (config?.sessionScheme == null) return;
            
            bool isVictory = false;
            if (payload is SessionEndedData data)
            {
                isVictory = data.IsVictory;
            }
            
            // Stinger
            var stinger = isVictory 
                ? config.sessionScheme.victoryStinger 
                : config.sessionScheme.defeatStinger;
            if (!string.IsNullOrEmpty(stinger))
            {
                Play(stinger);
            }
            
            // Музыка
            var music = isVictory 
                ? config.sessionScheme.victoryMusic 
                : config.sessionScheme.defeatMusic;
            if (!string.IsNullOrEmpty(music))
            {
                CrossfadeMusic(music, config.sessionScheme.musicFadeTime);
            }
            
            // Snapshot
            var snapshot = config.sessionScheme.gameOverSnapshot;
            if (!snapshot.IsEmpty)
            {
                SetSnapshot(snapshot);
            }
        }
        
        private void OnSessionPaused(object payload)
        {
            if (config?.sessionScheme == null) return;
            
            var snapshot = config.sessionScheme.pauseSnapshot;
            if (!snapshot.IsEmpty)
            {
                SetSnapshot(snapshot);
            }
            
            // Музыка паузы
            var pauseMusic = config.sessionScheme.pauseMusic;
            if (!string.IsNullOrEmpty(pauseMusic))
            {
                CrossfadeMusic(pauseMusic, config.sessionScheme.musicFadeTime);
            }
        }
        
        private void OnSessionResumed(object payload)
        {
            if (config?.sessionScheme == null) return;
            
            var snapshot = config.sessionScheme.pauseSnapshot;
            if (!snapshot.IsEmpty)
            {
                ClearSnapshot(snapshot);
            }
            
            // Вернуть геймплейную музыку
            if (!string.IsNullOrEmpty(config.sessionScheme.pauseMusic))
            {
                CrossfadeMusic(config.sessionScheme.gameplayMusic, config.sessionScheme.musicFadeTime);
            }
        }
        
        private void OnSessionStateChanged(object payload)
        {
            if (config?.sessionScheme == null) return;
            if (payload is not SessionStateChangedData data) return;
            
            var music = config.sessionScheme.GetMusicForState(data.NewState);
            if (!string.IsNullOrEmpty(music))
            {
                CrossfadeMusic(music, config.sessionScheme.musicFadeTime);
            }
            
            // Очистить старый snapshot
            var oldSnapshot = config.sessionScheme.GetSnapshotForState(data.OldState);
            if (!oldSnapshot.IsEmpty)
            {
                ClearSnapshot(oldSnapshot);
            }
            
            // Установить новый snapshot
            var newSnapshot = config.sessionScheme.GetSnapshotForState(data.NewState);
            if (!newSnapshot.IsEmpty)
            {
                SetSnapshot(newSnapshot);
            }
        }
        
        // === Обработчики событий Scene ===
        
        private void OnSceneLoadStarted(object payload)
        {
            var sceneName = payload as string;
            if (string.IsNullOrEmpty(sceneName)) return;
            
            // Загрузить банки для сцены
            if (config?.soundLibrary != null)
            {
                foreach (var bank in config.soundLibrary.GetBanksForScene(sceneName))
                {
                    _ = LoadBankAsync(bank.bankId);
                }
            }
        }
        
        private void OnSceneLoadCompleted(object payload)
        {
            // Можно добавить логику после загрузки сцены
        }
        
        private void OnSceneUnloaded(object payload)
        {
            var sceneName = payload as string;
            if (string.IsNullOrEmpty(sceneName)) return;
            
            // Выгрузить банки, которые больше не нужны
            // TODO: Проверить не используются ли банки другими сценами
        }
        
        // === Обработчики Sound событий ===
        
        private void OnPlaySoundEvent(object payload)
        {
            if (payload is string id)
            {
                Play(id);
            }
            else if (payload is SoundPlayRequest request)
            {
                Play(request.Id, request.Position, request.VolumeMultiplier);
            }
        }
        
        private void OnStopSoundEvent(object payload)
        {
            if (payload is SoundHandle handle)
            {
                Stop(handle);
            }
        }
        
        private void OnPlayMusicEvent(object payload)
        {
            if (payload is string id)
            {
                PlayMusic(id);
            }
            else if (payload is MusicPlayRequest request)
            {
                if (request.Crossfade)
                    CrossfadeMusic(request.Id, request.FadeTime);
                else
                    PlayMusic(request.Id, request.FadeTime);
            }
        }
        
        private void OnStopMusicEvent(object payload)
        {
            float fadeOut = 0f;
            if (payload is float f) fadeOut = f;
            StopMusic(fadeOut);
        }
        
        // === Debug ===
        
        [ContextMenu("Debug/Play Test Sound")]
        private void DebugPlayTestSound()
        {
            Play("ui_click");
        }
        
        [ContextMenu("Debug/Stop All")]
        private void DebugStopAll()
        {
            _provider?.StopAll();
        }
        
        [ContextMenu("Debug/Print Stats")]
        private void DebugPrintStats()
        {
            if (_provider != null)
            {
                LogRuntime($"Active: {_provider.ActiveSoundCount}/{_provider.MaxSimultaneousSounds}");
            }
        }
    }
    
    // === Вспомогательные структуры ===
    
    /// <summary>
    /// Запрос на воспроизведение звука через EventBus
    /// </summary>
    public struct SoundPlayRequest
    {
        public string Id;
        public Vector3? Position;
        public float VolumeMultiplier;
        
        public SoundPlayRequest(string id, Vector3? position = null, float volumeMultiplier = 1f)
        {
            Id = id;
            Position = position;
            VolumeMultiplier = volumeMultiplier;
        }
    }
    
    /// <summary>
    /// Запрос на воспроизведение музыки через EventBus
    /// </summary>
    public struct MusicPlayRequest
    {
        public string Id;
        public float FadeTime;
        public bool Crossfade;
        
        public MusicPlayRequest(string id, float fadeTime = 2f, bool crossfade = true)
        {
            Id = id;
            FadeTime = fadeTime;
            Crossfade = crossfade;
        }
    }
    
    /// <summary>
    /// Данные смены состояния сессии
    /// </summary>
    public struct SessionStateChangedData
    {
        public SessionState OldState;
        public SessionState NewState;
    }
    
    /// <summary>
    /// Данные завершения сессии
    /// </summary>
    public struct SessionEndedData
    {
        public SessionEndReason Reason;
        public bool IsVictory;
    }
}
