using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Стиль генерации звука: движок, шаблон промпта с плейсхолдером {subject}, негатив,
    /// параметры сэмплера, дефолтная длительность. Один стиль — «SFX дизель-панк»,
    /// «UI клики», «эмбиент пустоши», «музыка меню»…
    /// </summary>
    [CreateAssetMenu(menuName = "ProtoSystem/Audio Style Preset", fileName = "AudioStyle_")]
    public class AudioStylePreset : ScriptableObject
    {
        public AudioEngine engine = AudioEngine.StableAudio;

        [Tooltip("Шаблон промпта. {subject} заменяется промптом сущности. Для ACE-Step это строка тегов.")]
        [TextArea(2, 6)]
        public string positiveTemplate = "{subject}";

        [Tooltip("Негатив (только Stable Audio).")]
        [TextArea(2, 4)]
        public string negative = "";

        [Tooltip("Лирика ACE-Step. Пусто — [inst], инструментал.")]
        [TextArea(2, 6)]
        public string lyrics = "";

        [Tooltip("Длительность по умолчанию, сек (сущность/сет могут переопределить).")]
        public float seconds = 5f;

        public int steps = 50;
        public float cfg = 5f;

        [Tooltip("Пусто — дефолт движка: StableAudio dpmpp_3m_sde_gpu, AceStep euler.")]
        public string sampler = "";

        [Tooltip("Пусто — дефолт движка: StableAudio exponential, AceStep simple.")]
        public string scheduler = "";

        [Tooltip("Срезать хвостовую тишину one-shot'ов (для лупов выключается автоматически).")]
        public bool trimTailSilence = true;

        [Tooltip("ElevenLabs: строгость следования промпту 0..1 (выше — точнее, но однообразнее).")]
        [Range(0f, 1f)]
        public float promptInfluence = 0.4f;

        [Header("ElevenLabs TTS (озвучка)")]
        [Tooltip("ID голоса (выбирается дропдауном в студии). Для TTS шаблон держите {subject} — текст реплики идёт в синтез как есть.")]
        public string voiceId = "";

        [Tooltip("Модель TTS. eleven_multilingual_v2 — стабильная мультиязычная.")]
        public string ttsModelId = "eleven_multilingual_v2";

        [Tooltip("Стабильность подачи: ниже — эмоциональнее и разнообразнее, выше — ровнее.")]
        [Range(0f, 1f)]
        public float ttsStability = 0.45f;

        [Tooltip("Похожесть на исходный голос.")]
        [Range(0f, 1f)]
        public float ttsSimilarity = 0.75f;

        [Tooltip("Qwen3-TTS (локальный): язык синтеза — Russian, English… Пусто — auto. " +
                 "Описание голоса (voice design) — поле «Лирика».")]
        public string ttsLanguage = "";

        [Header("Постобработка")]
        [Tooltip("ffmpeg-фильтр после генерации (эффект рации и т.п.). Пусто — без обработки. " +
                 "Пример рации: highpass=f=250,lowpass=f=3400,acrusher=bits=10:mode=log:aa=1,acompressor=threshold=-18dB:ratio=4")]
        public string postFilterChain = "";

        /// <summary>Собрать запрос ComfyUI из промпта сущности.</summary>
        public ComfyAudioClient.AudioRequest Compose(string subject, float effSeconds, int seed)
        {
            string template = string.IsNullOrWhiteSpace(positiveTemplate) ? "{subject}" : positiveTemplate;
            return new ComfyAudioClient.AudioRequest
            {
                Engine = engine,
                Positive = template.Replace("{subject}", subject ?? ""),
                Negative = negative,
                Lyrics = lyrics,
                LyricsStrength = 1f,
                Seconds = effSeconds > 0f ? effSeconds : seconds,
                Seed = seed,
                Steps = steps,
                Cfg = cfg,
                Sampler = sampler,
                Scheduler = scheduler,
                PromptInfluence = promptInfluence,
                VoiceId = voiceId,
                TtsModelId = ttsModelId,
                TtsStability = ttsStability,
                TtsSimilarity = ttsSimilarity,
                TtsLanguage = ttsLanguage,
                PostFilter = postFilterChain,
            };
        }
    }
}
