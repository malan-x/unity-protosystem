using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Реализация аудио-провайдера на Unity AudioSource
    /// </summary>
    public class UnitySoundProvider : ISoundProvider
    {
        private SoundManagerConfig _config;
        private SoundLibrary _library;
        private Transform _root;
        private AudioListener _listener;
        private ISoundProcessor _processor;
        
        // Пул AudioSource
        private List<AudioSource> _sourcePool;
        private List<ActiveSound> _activeSounds;
        private Queue<AudioSource> _availableSources;
        
        // Музыка
        private AudioSource _musicSourceA;
        private AudioSource _musicSourceB;
        private bool _musicUsingA = true;
        private string _currentMusicId;
        private Coroutine _crossfadeCoroutine;
        
        // Музыкальные параметры
        private Dictionary<string, float> _musicParameters;
        private Dictionary<string, float> _musicParameterTargets;
        
        // Громкость
        private VolumeSettings _volumes;
        private bool _isMuted;
        private HashSet<SoundCategory> _pausedCategories;
        
        // Cooldown
        private Dictionary<string, float> _lastPlayTime;
        private Dictionary<string, int> _activeSoundCount;
        
        // Хэндлы
        private int _nextHandleId = 1;
        private int _currentGeneration = 1;
        
        public int ActiveSoundCount => _activeSounds?.Count ?? 0;
        public int MaxSimultaneousSounds => _config?.maxSimultaneousSounds ?? 32;
        
        public void Initialize(SoundManagerConfig config, SoundLibrary library)
        {
            _config = config;
            _library = library;
            
            // Создать root объект
            var rootGo = new GameObject("[SoundProvider]");
            UnityEngine.Object.DontDestroyOnLoad(rootGo);
            _root = rootGo.transform;
            
            // Инициализация коллекций
            _activeSounds = new List<ActiveSound>();
            _availableSources = new Queue<AudioSource>();
            _sourcePool = new List<AudioSource>();
            _pausedCategories = new HashSet<SoundCategory>();
            _lastPlayTime = new Dictionary<string, float>();
            _activeSoundCount = new Dictionary<string, int>();
            _musicParameters = new Dictionary<string, float>();
            _musicParameterTargets = new Dictionary<string, float>();
            
            // Скопировать дефолтные громкости
            _volumes = config.defaultVolumes.Clone();
            
            // Создать пул AudioSource
            CreateSourcePool(config.audioSourcePoolSize);
            
            // Создать музыкальные источники
            CreateMusicSources();
            
            // Инициализировать музыкальные параметры
            if (config.musicConfig != null)
            {
                foreach (var param in config.musicConfig.parameters)
                {
                    _musicParameters[param.name] = param.defaultValue;
                    _musicParameterTargets[param.name] = param.defaultValue;
                }
            }
            
            // Найти AudioListener
            _listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
            
            // Инициализировать библиотеку
            library?.Initialize();
            
            ProtoLogger.Log("sound_manager", LogCategory.Runtime, LogLevel.Info, $"Initialized with pool size {config.audioSourcePoolSize}");
        }
        
        public void Update()
        {
            if (_activeSounds == null) return;
            
            var listenerPos = _listener != null ? _listener.transform.position : Vector3.zero;
            
            // Обновить активные звуки
            for (int i = _activeSounds.Count - 1; i >= 0; i--)
            {
                var sound = _activeSounds[i];
                
                // Проверить завершение
                if (!sound.Source.isPlaying && !_pausedCategories.Contains(sound.Category))
                {
                    ReleaseSound(sound);
                    _activeSounds.RemoveAt(i);
                    continue;
                }
                
                // Применить процессор (occlusion и т.д.)
                if (_processor != null && sound.Entry.spatial)
                {
                    float distance = Vector3.Distance(listenerPos, sound.Position);
                    var info = new ActiveSoundInfo(
                        sound.Entry.id,
                        sound.Position,
                        sound.Category,
                        sound.Entry.volume,
                        sound.Entry.spatial,
                        distance
                    );
                    
                    _processor.ProcessActiveSound(ref info, listenerPos);
                    
                    sound.Source.volume = sound.BaseVolume * info.VolumeMultiplier * GetCategoryVolume(sound.Category);
                    sound.Source.pitch = sound.BasePitch * info.PitchMultiplier;
                    
                    // Low-pass filter
                    if (info.LowPassCutoff > 0 && sound.LowPassFilter != null)
                    {
                        sound.LowPassFilter.enabled = true;
                        sound.LowPassFilter.cutoffFrequency = info.LowPassCutoff;
                    }
                    else if (sound.LowPassFilter != null)
                    {
                        sound.LowPassFilter.enabled = false;
                    }
                }
            }
            
            // Обновить музыкальные параметры (плавный переход)
            UpdateMusicParameters();
        }
        
        // === Воспроизведение ===
        
        public SoundHandle Play(string id, Vector3? position = null, float volumeMultiplier = 1f)
        {
            if (string.IsNullOrEmpty(id)) return SoundHandle.Invalid;
            if (_isMuted) return SoundHandle.Invalid;
            
            var entry = _library?.Get(id);
            if (entry == null)
            {
                ProtoLogger.Log("sound_manager", LogCategory.Runtime, LogLevel.Warnings, $"Sound not found: {id}");
                return SoundHandle.Invalid;
            }
            
            // Проверить cooldown
            if (!CheckCooldown(entry))
            {
                return SoundHandle.Invalid;
            }
            
            // Проверить приоритет
            if (!CanPlaySound(entry))
            {
                return SoundHandle.Invalid;
            }
            
            // Получить источник
            var source = GetAvailableSource();
            if (source == null)
            {
                ProtoLogger.Log("sound_manager", LogCategory.Runtime, LogLevel.Warnings, "No available audio sources");
                return SoundHandle.Invalid;
            }
            
            // Настроить источник
            var clip = entry.GetRandomClip();
            if (clip == null)
            {
                ReturnSourceToPool(source);
                return SoundHandle.Invalid;
            }
            
            source.clip = clip;
            source.volume = entry.volume * volumeMultiplier * GetCategoryVolume(entry.category);
            source.pitch = entry.GetRandomPitch();
            source.loop = entry.loop;
            source.spatialBlend = entry.spatial ? 1f : 0f;
            
            if (entry.spatial)
            {
                source.transform.position = position ?? Vector3.zero;
                source.minDistance = entry.minDistance > 0 ? entry.minDistance : _config.default3DMinDistance;
                source.maxDistance = entry.maxDistance > 0 ? entry.maxDistance : _config.default3DMaxDistance;
                source.rolloffMode = _config.rolloffMode;
            }
            
            // Применить mixer group
            source.outputAudioMixerGroup = GetMixerGroup(entry.category);
            
            // Создать ActiveSound
            var handle = new SoundHandle(_nextHandleId++, _currentGeneration);
            var activeSound = new ActiveSound
            {
                Handle = handle,
                Source = source,
                Entry = entry,
                Category = entry.category,
                BaseVolume = entry.volume * volumeMultiplier,
                BasePitch = source.pitch,
                Position = position ?? Vector3.zero,
                StartTime = Time.unscaledTime
            };
            
            // Добавить LowPassFilter если нужен процессор
            if (_processor != null && entry.spatial)
            {
                activeSound.LowPassFilter = source.gameObject.GetComponent<AudioLowPassFilter>();
                if (activeSound.LowPassFilter == null)
                {
                    activeSound.LowPassFilter = source.gameObject.AddComponent<AudioLowPassFilter>();
                }
                activeSound.LowPassFilter.enabled = false;
            }
            
            _activeSounds.Add(activeSound);
            
            // Обновить cooldown tracking
            _lastPlayTime[id] = Time.unscaledTime;
            _activeSoundCount[id] = _activeSoundCount.GetValueOrDefault(id) + 1;
            
            source.Play();
            
            return handle;
        }
        
        public void Stop(SoundHandle handle)
        {
            if (!handle.IsValid) return;
            
            for (int i = 0; i < _activeSounds.Count; i++)
            {
                if (_activeSounds[i].Handle == handle)
                {
                    var sound = _activeSounds[i];
                    sound.Source.Stop();
                    ReleaseSound(sound);
                    _activeSounds.RemoveAt(i);
                    return;
                }
            }
        }
        
        public void StopAll(SoundCategory category)
        {
            for (int i = _activeSounds.Count - 1; i >= 0; i--)
            {
                if (_activeSounds[i].Category == category)
                {
                    var sound = _activeSounds[i];
                    sound.Source.Stop();
                    ReleaseSound(sound);
                    _activeSounds.RemoveAt(i);
                }
            }
        }
        
        public void StopAll()
        {
            foreach (var sound in _activeSounds)
            {
                sound.Source.Stop();
                ReleaseSound(sound);
            }
            _activeSounds.Clear();
            
            StopMusic(0);
        }
        
        public bool IsPlaying(SoundHandle handle)
        {
            if (!handle.IsValid) return false;
            
            foreach (var sound in _activeSounds)
            {
                if (sound.Handle == handle)
                    return sound.Source.isPlaying;
            }
            return false;
        }
        
        // === Музыка ===
        
        public void PlayMusic(string id, float fadeInTime = 0f)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (id == _currentMusicId) return;
            
            var entry = _library?.Get(id);
            if (entry == null)
            {
                ProtoLogger.Log("sound_manager", LogCategory.Runtime, LogLevel.Warnings, $"Music not found: {id}");
                return;
            }
            
            var clip = entry.GetRandomClip();
            if (clip == null) return;
            
            _currentMusicId = id;
            
            var targetSource = _musicUsingA ? _musicSourceA : _musicSourceB;
            targetSource.clip = clip;
            targetSource.volume = 0;
            targetSource.loop = true;
            targetSource.Play();
            
            if (fadeInTime > 0)
            {
                StartCoroutine(FadeIn(targetSource, entry.volume * GetCategoryVolume(SoundCategory.Music), fadeInTime));
            }
            else
            {
                targetSource.volume = entry.volume * GetCategoryVolume(SoundCategory.Music);
            }
        }
        
        public void StopMusic(float fadeOutTime = 0f)
        {
            var currentSource = _musicUsingA ? _musicSourceA : _musicSourceB;
            
            if (fadeOutTime > 0 && currentSource.isPlaying)
            {
                StartCoroutine(FadeOutAndStop(currentSource, fadeOutTime));
            }
            else
            {
                currentSource.Stop();
            }
            
            _currentMusicId = null;
        }
        
        public void CrossfadeMusic(string id, float crossfadeTime)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (id == _currentMusicId) return;
            
            var entry = _library?.Get(id);
            if (entry == null) return;
            
            var clip = entry.GetRandomClip();
            if (clip == null) return;
            
            var oldSource = _musicUsingA ? _musicSourceA : _musicSourceB;
            _musicUsingA = !_musicUsingA;
            var newSource = _musicUsingA ? _musicSourceA : _musicSourceB;
            
            newSource.clip = clip;
            newSource.volume = 0;
            newSource.loop = true;
            newSource.Play();
            
            _currentMusicId = id;
            
            float targetVolume = entry.volume * GetCategoryVolume(SoundCategory.Music);
            StartCoroutine(Crossfade(oldSource, newSource, targetVolume, crossfadeTime));
        }
        
        public void SetMusicParameter(string parameter, float value)
        {
            _musicParameterTargets[parameter] = Mathf.Clamp01(value);
        }
        
        public float GetMusicParameter(string parameter)
        {
            return _musicParameters.GetValueOrDefault(parameter, 0f);
        }
        
        private void UpdateMusicParameters()
        {
            if (_config.musicConfig == null) return;
            
            foreach (var param in _config.musicConfig.parameters)
            {
                if (!_musicParameterTargets.TryGetValue(param.name, out float target)) continue;
                if (!_musicParameters.TryGetValue(param.name, out float current)) continue;
                
                if (Mathf.Approximately(current, target)) continue;
                
                if (param.smoothTransition)
                {
                    float newValue = Mathf.MoveTowards(current, target, param.transitionSpeed * Time.unscaledDeltaTime);
                    _musicParameters[param.name] = newValue;
                }
                else
                {
                    _musicParameters[param.name] = target;
                }
            }
            
            // TODO: Apply parameters to music layers
        }
        
        // === Громкость ===
        
        public void SetVolume(SoundCategory category, float volume)
        {
            volume = Mathf.Clamp01(volume);
            _volumes.SetVolume(category, volume);
            
            // Применить к активным звукам
            foreach (var sound in _activeSounds)
            {
                if (sound.Category == category || category == SoundCategory.Master)
                {
                    sound.Source.volume = sound.BaseVolume * GetCategoryVolume(sound.Category);
                }
            }
            
            // Применить к музыке
            if (category == SoundCategory.Music || category == SoundCategory.Master)
            {
                float musicVol = GetCategoryVolume(SoundCategory.Music);
                if (_musicSourceA != null && _musicSourceA.isPlaying)
                    _musicSourceA.volume = musicVol;
                if (_musicSourceB != null && _musicSourceB.isPlaying)
                    _musicSourceB.volume = musicVol;
            }
            
            // Применить к AudioMixer
            ApplyVolumeToMixer(category, volume);
        }
        
        public float GetVolume(SoundCategory category)
        {
            return _volumes.GetVolume(category);
        }
        
        public void SetMute(bool muted)
        {
            _isMuted = muted;
            
            if (muted)
            {
                PauseAll();
            }
            else
            {
                ResumeAll();
            }
        }
        
        public bool IsMuted() => _isMuted;
        
        private float GetCategoryVolume(SoundCategory category)
        {
            return _volumes.GetVolume(category) * _volumes.master;
        }
        
        private void ApplyVolumeToMixer(SoundCategory category, float volume)
        {
            if (_config.masterMixer == null) return;
            
            string paramName = _config.mixerGroupNames.GetParameterName(category);
            float db = volume > 0.0001f ? Mathf.Log10(volume) * 20f : -80f;
            _config.masterMixer.SetFloat(paramName, db);
        }
        
        // === Пауза ===
        
        public void Pause(SoundCategory category)
        {
            _pausedCategories.Add(category);
            
            foreach (var sound in _activeSounds)
            {
                if (sound.Category == category)
                    sound.Source.Pause();
            }
            
            if (category == SoundCategory.Music)
            {
                _musicSourceA?.Pause();
                _musicSourceB?.Pause();
            }
        }
        
        public void Resume(SoundCategory category)
        {
            _pausedCategories.Remove(category);
            
            foreach (var sound in _activeSounds)
            {
                if (sound.Category == category)
                    sound.Source.UnPause();
            }
            
            if (category == SoundCategory.Music)
            {
                _musicSourceA?.UnPause();
                _musicSourceB?.UnPause();
            }
        }
        
        public void PauseAll()
        {
            foreach (SoundCategory cat in Enum.GetValues(typeof(SoundCategory)))
            {
                Pause(cat);
            }
        }
        
        public void ResumeAll()
        {
            foreach (SoundCategory cat in Enum.GetValues(typeof(SoundCategory)))
            {
                Resume(cat);
            }
        }
        
        // === Snapshots ===
        
        public void SetSnapshot(SoundSnapshotId snapshot, float transitionTime = 0.5f)
        {
            if (snapshot.IsEmpty) return;
            if (_config.masterMixer == null) return;
            
            var snapshotObj = _config.masterMixer.FindSnapshot(snapshot.Name);
            if (snapshotObj != null)
            {
                snapshotObj.TransitionTo(transitionTime);
            }
            else
            {
                ProtoLogger.Log("sound_manager", LogCategory.Runtime, LogLevel.Warnings, $"Snapshot not found: {snapshot.Name}");
            }
        }
        
        public void ClearSnapshot(SoundSnapshotId snapshot, float transitionTime = 0.5f)
        {
            // Unity AudioMixer не поддерживает стек snapshot'ов
            // Переход к дефолтному snapshot
            if (_config.masterMixer == null) return;
            
            var defaultSnapshot = _config.masterMixer.FindSnapshot("Default");
            defaultSnapshot?.TransitionTo(transitionTime);
        }
        
        public void ClearAllSnapshots(float transitionTime = 0.5f)
        {
            ClearSnapshot(default, transitionTime);
        }
        
        // === Банки ===
        
        public Task<bool> LoadBankAsync(string bankId)
        {
            bool result = _library?.LoadBank(bankId) ?? false;
            return Task.FromResult(result);
        }
        
        public void UnloadBank(string bankId)
        {
            _library?.UnloadBank(bankId);
        }
        
        public bool IsBankLoaded(string bankId)
        {
            return _library?.IsBankLoaded(bankId) ?? false;
        }
        
        // === Расширение ===
        
        public void SetSoundProcessor(ISoundProcessor processor)
        {
            _processor = processor;
        }
        
        // === Внутренние методы ===
        
        private void CreateSourcePool(int size)
        {
            var poolGo = new GameObject("AudioSourcePool");
            poolGo.transform.SetParent(_root);
            
            for (int i = 0; i < size; i++)
            {
                var go = new GameObject($"AudioSource_{i}");
                go.transform.SetParent(poolGo.transform);
                
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                
                _sourcePool.Add(source);
                _availableSources.Enqueue(source);
            }
        }
        
        private void CreateMusicSources()
        {
            var musicGo = new GameObject("MusicSources");
            musicGo.transform.SetParent(_root);
            
            var goA = new GameObject("MusicSource_A");
            goA.transform.SetParent(musicGo.transform);
            _musicSourceA = goA.AddComponent<AudioSource>();
            _musicSourceA.playOnAwake = false;
            _musicSourceA.loop = true;
            _musicSourceA.outputAudioMixerGroup = GetMixerGroup(SoundCategory.Music);
            
            var goB = new GameObject("MusicSource_B");
            goB.transform.SetParent(musicGo.transform);
            _musicSourceB = goB.AddComponent<AudioSource>();
            _musicSourceB.playOnAwake = false;
            _musicSourceB.loop = true;
            _musicSourceB.outputAudioMixerGroup = GetMixerGroup(SoundCategory.Music);
        }
        
        private AudioSource GetAvailableSource()
        {
            if (_availableSources.Count > 0)
            {
                return _availableSources.Dequeue();
            }
            
            // Попробовать расширить пул
            if (_sourcePool.Count < _config.maxSimultaneousSounds)
            {
                var go = new GameObject($"AudioSource_{_sourcePool.Count}");
                go.transform.SetParent(_root.GetChild(0)); // AudioSourcePool
                
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                
                _sourcePool.Add(source);
                return source;
            }
            
            return null;
        }
        
        private void ReturnSourceToPool(AudioSource source)
        {
            source.clip = null;
            source.outputAudioMixerGroup = null;
            _availableSources.Enqueue(source);
        }
        
        private void ReleaseSound(ActiveSound sound)
        {
            // Обновить счётчик
            if (_activeSoundCount.ContainsKey(sound.Entry.id))
            {
                _activeSoundCount[sound.Entry.id] = Mathf.Max(0, _activeSoundCount[sound.Entry.id] - 1);
            }
            
            ReturnSourceToPool(sound.Source);
        }
        
        private bool CheckCooldown(SoundEntry entry)
        {
            if (!_config.cooldown.enabled) return true;
            
            string id = entry.id;
            float now = Time.unscaledTime;
            
            // Проверка времени
            if (_lastPlayTime.TryGetValue(id, out float lastTime))
            {
                float cooldown = entry.cooldown > 0 ? entry.cooldown : _config.cooldown.defaultCooldown;
                if (now - lastTime < cooldown)
                    return false;
            }
            
            // Проверка количества
            if (_activeSoundCount.TryGetValue(id, out int count))
            {
                if (count >= _config.cooldown.maxSameSoundSimultaneous)
                    return false;
            }
            
            return true;
        }
        
        private bool CanPlaySound(SoundEntry entry)
        {
            if (!_config.priority.enabled) return true;
            if (_activeSounds.Count < _config.maxSimultaneousSounds) return true;
            
            if (entry.priority == SoundPriority.Critical)
            {
                // Убить самый низкоприоритетный
                StopLowestPriority();
                return true;
            }
            
            // Найти звук с меньшим приоритетом
            ActiveSound lowest = null;
            foreach (var sound in _activeSounds)
            {
                if ((int)sound.Entry.priority < (int)entry.priority)
                {
                    if (lowest == null || (int)sound.Entry.priority < (int)lowest.Entry.priority)
                    {
                        lowest = sound;
                    }
                }
            }
            
            if (lowest != null)
            {
                Stop(lowest.Handle);
                return true;
            }
            
            return false;
        }
        
        private void StopLowestPriority()
        {
            ActiveSound lowest = null;
            float oldestTime = float.MaxValue;
            
            foreach (var sound in _activeSounds)
            {
                if (sound.Entry.priority == SoundPriority.Critical) continue;
                
                if (lowest == null || 
                    (int)sound.Entry.priority < (int)lowest.Entry.priority ||
                    ((int)sound.Entry.priority == (int)lowest.Entry.priority && sound.StartTime < oldestTime))
                {
                    lowest = sound;
                    oldestTime = sound.StartTime;
                }
            }
            
            if (lowest != null)
            {
                Stop(lowest.Handle);
            }
        }
        
        private AudioMixerGroup GetMixerGroup(SoundCategory category)
        {
            if (_config.masterMixer == null) return null;
            
            string groupName = category switch
            {
                SoundCategory.Music => "Music",
                SoundCategory.SFX => "SFX",
                SoundCategory.Voice => "Voice",
                SoundCategory.Ambient => "Ambient",
                SoundCategory.UI => "UI",
                _ => "Master"
            };
            
            var groups = _config.masterMixer.FindMatchingGroups(groupName);
            return groups.Length > 0 ? groups[0] : null;
        }
        
        // === Coroutine helpers ===
        
        private void StartCoroutine(System.Collections.IEnumerator routine)
        {
            // Используем MonoBehaviour хелпер
            CoroutineRunner.Instance?.StartCoroutine(routine);
        }
        
        private System.Collections.IEnumerator FadeIn(AudioSource source, float targetVolume, float duration)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(0, targetVolume, elapsed / duration);
                yield return null;
            }
            source.volume = targetVolume;
        }
        
        private System.Collections.IEnumerator FadeOutAndStop(AudioSource source, float duration)
        {
            float startVolume = source.volume;
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVolume, 0, elapsed / duration);
                yield return null;
            }
            source.Stop();
            source.volume = 0;
        }
        
        private System.Collections.IEnumerator Crossfade(AudioSource from, AudioSource to, float targetVolume, float duration)
        {
            float fromStart = from.volume;
            float elapsed = 0;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                
                if (_config.musicConfig?.crossfadeCurve != null)
                    t = _config.musicConfig.crossfadeCurve.Evaluate(t);
                
                from.volume = Mathf.Lerp(fromStart, 0, t);
                to.volume = Mathf.Lerp(0, targetVolume, t);
                yield return null;
            }
            
            from.Stop();
            from.volume = 0;
            to.volume = targetVolume;
        }
        
        public void Dispose()
        {
            StopAll();
            
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
            
            _activeSounds?.Clear();
            _availableSources?.Clear();
            _sourcePool?.Clear();
            _lastPlayTime?.Clear();
            _activeSoundCount?.Clear();
            _musicParameters?.Clear();
            _musicParameterTargets?.Clear();
        }
        
        // === Вложенные классы ===
        
        private class ActiveSound
        {
            public SoundHandle Handle;
            public AudioSource Source;
            public SoundEntry Entry;
            public SoundCategory Category;
            public float BaseVolume;
            public float BasePitch;
            public Vector3 Position;
            public float StartTime;
            public AudioLowPassFilter LowPassFilter;
        }
    }
    
    /// <summary>
    /// Хелпер для запуска корутин вне MonoBehaviour
    /// </summary>
    internal class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;
        
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[CoroutineRunner]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CoroutineRunner>();
                }
                return _instance;
            }
        }
    }
}
