using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Очередь генерации звука через ComfyUI: фоновая (Progress API, не модальная),
    /// группировка по чекпоинту (своп моделей в VRAM дорог), сохранение результата
    /// FLAC → ffmpeg → WAV → импорт AudioClip. Зеркало AiIconGenerator.
    /// </summary>
    public static class AiAudioGenerator
    {
        private static readonly List<AudioGenRequest> Queue = new();

        private static System.Threading.Tasks.Task<byte[]> _current;
        private static AudioGenRequest _currentReq;
        private static ComfyAudioClient.AudioRequest _currentComposed;
        private static string _currentStyleName;
        private static bool _currentTrimSilence;
        private static float _currentCutSeconds;
        private static string _lastCheckpoint;

        /// <summary>
        /// Stable Audio обучена на длинных окнах — на латенте в 1-2 с выдаёт кашу, промпт
        /// не соблюдается. One-shot'ы генерим минимум этой длины, а до целевой режет ffmpeg.
        /// </summary>
        private const float MinStableAudioSeconds = 5f;

        /// <summary>Облачные движки ElevenLabs (без чекпоинтов и ComfyUI).</summary>
        public static bool IsCloud(AudioEngine engine)
            => engine == AudioEngine.ElevenLabs || engine == AudioEngine.ElevenLabsTts;

        private static int _total, _done, _cancelled;
        private static int _progressId = -1;
        private static bool _reloadLocked;

        public static bool IsRunning => _current != null || Queue.Count > 0;
        public static int Pending => Queue.Count + (_current != null ? 1 : 0);

        public static event Action QueueChanged;

        public static int Done => _done;
        public static int Total => _total;
        public static string CurrentLabel => _current != null ? _currentReq.DisplayName : "";

        public static IReadOnlyList<AudioGenRequest> PendingRequests => Queue;

        public static void Enqueue(AudioGenRequest request) => Enqueue(new[] { request });

        public static void Enqueue(IEnumerable<AudioGenRequest> requests)
        {
            foreach (var r in requests)
            {
                if (string.IsNullOrEmpty(r.AssetPath)) continue;
                if (string.IsNullOrWhiteSpace(r.Prompt) && r.DirectRequest == null) continue;
                Queue.Add(r);
            }

            _total = _done + Queue.Count + (_current != null ? 1 : 0);
            if (_total == 0) return;

            if (_progressId < 0)
            {
                _progressId = Progress.Start("Генерация звука (ComfyUI)", null, Progress.Options.Managed);
                Progress.RegisterCancelCallback(_progressId, () => { CancelAll(); return true; });

                // Очередь статическая — domain reload её молча убивает (недосчитались
                // половины пачки). Блокируем перезагрузку сборок до конца очереди;
                // компиляция просто подождёт. Снятие — в Finish.
                EditorApplication.LockReloadAssemblies();
                _reloadLocked = true;
            }

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            QueueChanged?.Invoke();
        }

        public static void CancelAll()
        {
            _cancelled += Queue.Count;
            Queue.Clear();
            QueueChanged?.Invoke();
            // текущая задача дорабатывает: ComfyUI отменять по одной дороже, чем дождаться
        }

        public static bool CancelAt(int index)
        {
            if (index < 0 || index >= Queue.Count) return false;
            Queue.RemoveAt(index);
            _cancelled++;
            QueueChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Следующий запрос — предпочтительно с тем же чекпоинтом, что предыдущий:
        /// своп Stable Audio ↔ ACE-Step в VRAM стоит десятки секунд. FIFO внутри модели.
        /// </summary>
        private static AudioGenRequest DequeueNext()
        {
            int pick = 0;
            if (!string.IsNullOrEmpty(_lastCheckpoint))
            {
                for (int i = 0; i < Queue.Count; i++)
                {
                    var composed = ComposeFor(Queue[i]);
                    if (composed.EffCheckpoint == _lastCheckpoint) { pick = i; break; }
                }
            }
            var req = Queue[pick];
            Queue.RemoveAt(pick);
            return req;
        }

        private static ComfyAudioClient.AudioRequest ComposeFor(in AudioGenRequest req)
        {
            if (req.DirectRequest.HasValue) return req.DirectRequest.Value;
            var style = req.ResolveStyle();
            var composed = style != null
                ? style.Compose(req.Prompt, req.Seconds, req.ResolveSeed())
                : new ComfyAudioClient.AudioRequest { Positive = req.Prompt, Seconds = req.Seconds, Seed = req.ResolveSeed(), Steps = 50, Cfg = 5f };
            if (!string.IsNullOrEmpty(req.CheckpointOverride))
            {
                composed.CheckpointOverride = req.CheckpointOverride;
                composed.HasModelOverride = true;
            }
            composed.Loop = req.Loop;

            // Модель ведёт за собой движок: выбрали ACE-чекпоинт на SFX-наборе — собираем
            // ACE-граф (и наоборот), иначе сервер падает ('NoneType' has no attribute 'shape').
            // Касается только ComfyUI-движков (у облака и Qwen чекпоинтов ComfyUI нет).
            if (composed.Engine == AudioEngine.StableAudio || composed.Engine == AudioEngine.AceStep)
            {
                bool wantAce = ComfyAudioClient.IsAceCheckpoint(composed.EffCheckpoint);
                if (wantAce != (composed.Engine == AudioEngine.AceStep))
                {
                    composed.Engine = wantAce ? AudioEngine.AceStep : AudioEngine.StableAudio;
                    composed.Sampler = "";      // дефолты сэмплера у движков разные
                    composed.Scheduler = "";
                }
            }
            return composed;
        }

        private static void Tick()
        {
            if (_current == null)
            {
                if (Queue.Count == 0) { Finish(); return; }

                _currentReq = DequeueNext();
                _currentComposed = ComposeFor(_currentReq);

                var style = _currentReq.ResolveStyle();
                _currentStyleName = _currentReq.DirectRequest.HasValue
                    ? "(рецепт)"
                    : (style != null ? style.name : "(без стиля)");
                _currentTrimSilence = !_currentReq.Loop &&
                    (_currentReq.DirectRequest.HasValue || style == null || style.trimTailSilence);

                // Короткий one-shot: генерим длиннее (модели комфортно), режем до цели
                _currentCutSeconds = 0f;
                if (_currentComposed.Engine == AudioEngine.StableAudio && !_currentReq.Loop &&
                    _currentComposed.Seconds > 0f && _currentComposed.Seconds < MinStableAudioSeconds)
                {
                    _currentCutSeconds = _currentComposed.Seconds;
                    _currentComposed.Seconds = MinStableAudioSeconds;
                }

                _lastCheckpoint = _currentComposed.EffCheckpoint;
                _current = _currentComposed.Engine switch
                {
                    AudioEngine.ElevenLabs => ElevenLabsClient.GenerateAsync(_currentComposed),
                    AudioEngine.ElevenLabsTts => ElevenLabsClient.TextToSpeechAsync(_currentComposed),
                    AudioEngine.QwenTts => QwenTtsClient.GenerateAsync(_currentComposed),
                    _ => ComfyAudioClient.GenerateAsync(_currentComposed),
                };

                if (_progressId >= 0)
                    Progress.Report(_progressId, _done, _total,
                        $"{_currentReq.DisplayName} ({_done + 1}/{_total})");
                QueueChanged?.Invoke();
                return;
            }

            if (!_current.IsCompleted) return;

            var task = _current;
            var req = _currentReq;
            var composed = _currentComposed;
            var styleName = _currentStyleName;
            bool trim = _currentTrimSilence;
            float cutSeconds = _currentCutSeconds;
            _current = null;

            if (task.IsFaulted)
            {
                Debug.LogError($"[AudioStudio] «{req.DisplayName}»: " +
                               (task.Exception?.GetBaseException().Message ?? "неизвестная ошибка"));
            }
            else
            {
                try { SaveResult(req, composed, styleName, trim, cutSeconds, task.Result); }
                catch (Exception e)
                {
                    Debug.LogError($"[AudioStudio] «{req.DisplayName}»: сохранение не удалось — {e.Message}");
                }
            }

            _done++;
            QueueChanged?.Invoke();
        }

        /// <summary>FLAC/MP3 от движка → WAV в проект → импорт → AudioClip → OnDone (история вариантов).</summary>
        private static void SaveResult(in AudioGenRequest req, in ComfyAudioClient.AudioRequest composed,
                                       string styleName, bool trimSilence, float cutSeconds, byte[] audio)
        {
            EnsureFolder(Path.GetDirectoryName(req.AssetPath)?.Replace('\\', '/'));

            string abs = Path.GetFullPath(req.AssetPath);
            string ext = composed.Engine == AudioEngine.QwenTts ? "wav"
                : IsCloud(composed.Engine) ? "mp3" : "flac";
            AudioConvert.ToWav(audio, abs, trimSilence, maxSeconds: cutSeconds, sourceExt: ext,
                extraFilter: composed.PostFilter, targetLufs: composed.TargetLufs);

            AssetDatabase.ImportAsset(req.AssetPath, ImportAssetOptions.ForceUpdate);
            ConfigureAsClip(req.AssetPath);

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(req.AssetPath);
            if (clip == null)
                throw new Exception($"WAV импортирован, но AudioClip не загрузился: {req.AssetPath}");

            var result = new AudioGenResult
            {
                Subject = req.Prompt,
                Positive = composed.Positive,
                Negative = composed.Negative,
                Lyrics = composed.Lyrics,
                LyricsStrength = composed.LyricsStrength,
                Engine = (int)composed.Engine,
                Seconds = composed.Seconds,
                Seed = composed.Seed,
                Steps = composed.Steps,
                Cfg = composed.Cfg,
                Sampler = composed.Sampler,
                Scheduler = composed.Scheduler,
                Checkpoint = composed.EffCheckpoint,
                StyleName = styleName,
                TrimSilence = trimSilence,
                VoiceId = composed.VoiceId,
                TtsModelId = composed.TtsModelId,
                TtsStability = composed.TtsStability,
                TtsSimilarity = composed.TtsSimilarity,
                TtsLanguage = composed.TtsLanguage,
                PostFilter = composed.PostFilter,
                TargetLufs = composed.TargetLufs,
                RunId = req.RunId,
                GeneratedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                WavAssetPath = req.AssetPath,
            };

            if (req.UndoTarget != null)
                Undo.RecordObject(req.UndoTarget, req.UndoLabel ?? "AI audio");

            req.OnDone?.Invoke(clip, result);

            if (req.UndoTarget != null)
            {
                EditorUtility.SetDirty(req.UndoTarget);
                AssetDatabase.SaveAssetIfDirty(req.UndoTarget);
            }
        }

        /// <summary>
        /// Настройка импорта клипа под проектный стандарт: DecompressOnLoad + Vorbis,
        /// без preload (как у всех звуков игры).
        /// </summary>
        public static void ConfigureAsClip(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not AudioImporter importer) return;

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 1f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.SaveAndReimport();
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            if (_progressId >= 0)
            {
                Progress.Remove(_progressId);
                _progressId = -1;
            }
            if (_reloadLocked)
            {
                _reloadLocked = false;
                EditorApplication.UnlockReloadAssemblies();
            }
            _total = _done = _cancelled = 0;
            _lastCheckpoint = null;
            QueueChanged?.Invoke();
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
