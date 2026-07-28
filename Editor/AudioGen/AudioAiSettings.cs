using UnityEditor;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Настройки AI-инструментов генерации звука: чекпоинты SFX/музыки, путь к ffmpeg,
    /// дефолтный стиль-пресет. Всё в EditorPrefs — правится в «Project Settings ▸ AI Audio Tools».
    ///
    /// Адрес сервера ComfyUI и файл запуска НАМЕРЕННО читаются из ключей арт-студии
    /// (ProtoIcon.ComfyServer/ComfyLaunch): сервер один на обе студии, настраивается один раз.
    /// Свои ключи — с префиксом ProtoAudio.*; ядро не знает про Last Convoy.
    ///
    /// Значения кэшируются в статиках (ленивое чтение + write-through): EditorPrefs на
    /// Windows — это реестр, дёргать его из UI каждый кадр нельзя.
    /// </summary>
    public static class AudioAiSettings
    {
        // ── Кэшированные обёртки над EditorPrefs ──

        private static string GetS(ref string cache, string key, string def)
            => cache ??= EditorPrefs.GetString(key, def);

        private static void SetS(ref string cache, string key, string value)
        {
            cache = value;
            EditorPrefs.SetString(key, value);
        }

        // ── ComfyUI — общий с арт-студией ──

        private static string _comfyServer;
        public static string ComfyServer
        {
            get => GetS(ref _comfyServer, "ProtoIcon.ComfyServer", "http://127.0.0.1:8188");
            set => SetS(ref _comfyServer, "ProtoIcon.ComfyServer", value);
        }

        /// <summary>Чем запускать ComfyUI (.bat/.exe). Пусто — кнопка запуска недоступна.</summary>
        private static string _comfyLaunch;
        public static string ComfyLaunch
        {
            get => GetS(ref _comfyLaunch, "ProtoIcon.ComfyLaunch", "");
            set => SetS(ref _comfyLaunch, "ProtoIcon.ComfyLaunch", value);
        }

        // ── Чекпоинты аудио-моделей (models/checkpoints на сервере) ──

        /// <summary>Модель SFX/эмбиентов: Stable Audio Open (text-to-audio до ~47 с).</summary>
        private static string _sfxCheckpoint;
        public static string SfxCheckpoint
        {
            get => GetS(ref _sfxCheckpoint, "ProtoAudio.SfxCheckpoint", "stable-audio-open-1.0.safetensors");
            set => SetS(ref _sfxCheckpoint, "ProtoAudio.SfxCheckpoint", value);
        }

        /// <summary>
        /// Текстовый энкодер Stable Audio (models/text_encoders): T5 идёт отдельным
        /// файлом — в чекпоинте-репаке его нет.
        /// </summary>
        private static string _sfxTextEncoder;
        public static string SfxTextEncoder
        {
            get => GetS(ref _sfxTextEncoder, "ProtoAudio.SfxTextEncoder", "t5_base.safetensors");
            set => SetS(ref _sfxTextEncoder, "ProtoAudio.SfxTextEncoder", value);
        }

        /// <summary>Модель музыки: ACE-Step (полные треки, теги + лирика).</summary>
        private static string _musicCheckpoint;
        public static string MusicCheckpoint
        {
            get => GetS(ref _musicCheckpoint, "ProtoAudio.MusicCheckpoint", "ace_step_v1_3.5b.safetensors");
            set => SetS(ref _musicCheckpoint, "ProtoAudio.MusicCheckpoint", value);
        }

        // ── ffmpeg: конвертация FLAC (отдаёт ComfyUI) → WAV (понимает Unity) ──

        /// <summary>Путь к ffmpeg.exe или просто "ffmpeg", если он в PATH.</summary>
        private static string _ffmpegPath;
        public static string FfmpegPath
        {
            get => GetS(ref _ffmpegPath, "ProtoAudio.FfmpegPath", "ffmpeg");
            set => SetS(ref _ffmpegPath, "ProtoAudio.FfmpegPath", value);
        }

        // ── ElevenLabs (облачный движок SFX) ──

        /// <summary>
        /// API-ключ ElevenLabs. Хранится ТОЛЬКО в EditorPrefs (реестр) — в файлы проекта
        /// и git не попадает: ElevenLabs автоматически гасит засветившиеся ключи.
        /// </summary>
        private static string _elevenLabsKey;
        public static string ElevenLabsApiKey
        {
            get => GetS(ref _elevenLabsKey, "ProtoAudio.ElevenLabsKey", "");
            set => SetS(ref _elevenLabsKey, "ProtoAudio.ElevenLabsKey", value);
        }

        // ── Qwen3-TTS (локальный движок озвучки, qwentts.cpp) ──

        private static string _qwenTtsExe;
        public static string QwenTtsExe
        {
            get => GetS(ref _qwenTtsExe, "ProtoAudio.QwenTtsExe", @"E:\AI\qwentts\build\Release\qwen-tts.exe");
            set => SetS(ref _qwenTtsExe, "ProtoAudio.QwenTtsExe", value);
        }

        private static string _qwenTalker;
        public static string QwenTalkerModel
        {
            get => GetS(ref _qwenTalker, "ProtoAudio.QwenTalker",
                @"E:\AI\qwentts\models\qwen-talker-1.7b-voicedesign-Q8_0.gguf");
            set => SetS(ref _qwenTalker, "ProtoAudio.QwenTalker", value);
        }

        private static string _qwenCodec;
        public static string QwenCodecModel
        {
            get => GetS(ref _qwenCodec, "ProtoAudio.QwenCodec",
                @"E:\AI\qwentts\models\qwen-tokenizer-12hz-Q8_0.gguf");
            set => SetS(ref _qwenCodec, "ProtoAudio.QwenCodec", value);
        }

        // ── Дефолтный стиль-пресет (GUID ассета AudioStylePreset) ──

        private static string _defaultStyleGuid;
        public static string DefaultStyleGuid
        {
            get => GetS(ref _defaultStyleGuid, "ProtoAudio.DefaultStyleGuid", "");
            set => SetS(ref _defaultStyleGuid, "ProtoAudio.DefaultStyleGuid", value);
        }
    }
}
