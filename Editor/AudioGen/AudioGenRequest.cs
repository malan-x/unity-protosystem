using System;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Слепок фактических параметров завершённой генерации — из него собирается рецепт
    /// варианта (перегенерация той же моделью с теми же параметрами).
    /// </summary>
    [Serializable]
    public struct AudioGenResult
    {
        public string Subject;          // «сырой» промпт сущности (без шаблона стиля)
        public string Positive;         // полный промпт/теги после шаблона
        public string Negative;
        public string Lyrics;
        public float LyricsStrength;
        public int Engine;              // (int)AudioEngine
        public float Seconds;
        public int Seed;
        public int Steps;
        public float Cfg;
        public string Sampler;
        public string Scheduler;
        public string Checkpoint;
        public string StyleName;
        public bool TrimSilence;

        // TTS (ElevenLabsTts / QwenTts)
        public string VoiceId;
        public string TtsModelId;
        public float TtsStability;
        public float TtsSimilarity;
        public string TtsLanguage;

        public string PostFilter;
        public string RunId;
        public string GeneratedAtUtc;
        public string WavAssetPath;
    }

    /// <summary>Единица работы очереди генерации звука.</summary>
    public struct AudioGenRequest
    {
        /// <summary>«Сырой» промпт сущности — стиль оборачивает его шаблоном.</summary>
        public string Prompt;

        /// <summary>Куда сохранить WAV (asset-путь). Папки создаются автоматически.</summary>
        public string AssetPath;

        public int Seed;
        public string DisplayName;

        /// <summary>Длительность, сек. 0 — дефолт стиля.</summary>
        public float Seconds;

        /// <summary>Луп: подсказка модели в промпте + без обрезки хвостовой тишины.</summary>
        public bool Loop;

        public AudioStylePreset Style;

        /// <summary>Готовый запрос по рецепту — минуя стиль (перегенерация варианта).</summary>
        public ComfyAudioClient.AudioRequest? DirectRequest;

        public string RunId;

        /// <summary>Оверрайд чекпоинта (из профиля набора). Пусто — глобальная настройка движка.</summary>
        public string CheckpointOverride;

        public UnityEngine.Object UndoTarget;
        public string UndoLabel;

        /// <summary>Вызывается после импорта WAV: клип + слепок параметров для рецепта.</summary>
        public Action<AudioClip, AudioGenResult> OnDone;

        public int ResolveSeed()
        {
            if (Seed != 0) return Seed;
            // Стабильный положительный сид из пути — воспроизводимо между сессиями
            return Mathf.Abs((AssetPath ?? "").GetHashCode() % 99999989) + 1;
        }

        public AudioStylePreset ResolveStyle() => Style != null ? Style : AudioStyleRegistry.Default;
    }
}
