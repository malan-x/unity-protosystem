using UnityEditor;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>«Project Settings ▸ AI Audio Tools» — настройки генерации звука.</summary>
    public static class AudioToolsSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/AI Audio Tools", SettingsScope.Project)
            {
                label = "AI Audio Tools",
                keywords = new[] { "audio", "sound", "comfy", "ai", "sfx", "ffmpeg" },
                guiHandler = _ =>
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("ComfyUI (общий с арт-студией)", EditorStyles.boldLabel);
                    AudioAiSettings.ComfyServer =
                        EditorGUILayout.TextField("Адрес сервера", AudioAiSettings.ComfyServer);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        AudioAiSettings.ComfyLaunch =
                            EditorGUILayout.TextField("Запуск (.bat/.exe)", AudioAiSettings.ComfyLaunch);
                        if (GUILayout.Button("…", GUILayout.Width(28)))
                        {
                            string picked = EditorUtility.OpenFilePanel(
                                "Файл запуска ComfyUI", "", "bat,exe,cmd");
                            if (!string.IsNullOrEmpty(picked)) AudioAiSettings.ComfyLaunch = picked;
                        }
                    }

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Модели (models/checkpoints)", EditorStyles.boldLabel);
                    AudioAiSettings.SfxCheckpoint = EditorGUILayout.TextField(
                        new GUIContent("SFX/эмбиент", "Stable Audio 3 (stable_audio_3_*) или Open 1.0 — граф по имени"),
                        AudioAiSettings.SfxCheckpoint);
                    AudioAiSettings.Sa3TextEncoder = EditorGUILayout.TextField(
                        new GUIContent("Энкодер SA3", "models/text_encoders — T5Gemma для Stable Audio 3"),
                        AudioAiSettings.Sa3TextEncoder);
                    AudioAiSettings.SfxTextEncoder = EditorGUILayout.TextField(
                        new GUIContent("T5 для SAO 1.0", "models/text_encoders — энкодер Stable Audio Open 1.0"),
                        AudioAiSettings.SfxTextEncoder);
                    AudioAiSettings.MusicCheckpoint = EditorGUILayout.TextField(
                        new GUIContent("Музыка", "ACE-Step 1.5 или v1 — полные треки, граф по имени"),
                        AudioAiSettings.MusicCheckpoint);

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("ElevenLabs (облачный SFX-движок)", EditorStyles.boldLabel);
                    AudioAiSettings.ElevenLabsApiKey = EditorGUILayout.PasswordField(
                        new GUIContent("API-ключ", "Хранится только в EditorPrefs — в git не попадает"),
                        AudioAiSettings.ElevenLabsApiKey);
                    DrawReserveKeys();

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Qwen3-TTS (локальная озвучка, qwentts.cpp)", EditorStyles.boldLabel);
                    AudioAiSettings.QwenTtsExe = EditorGUILayout.TextField("qwen-tts.exe", AudioAiSettings.QwenTtsExe);
                    AudioAiSettings.QwenTalkerModel = EditorGUILayout.TextField("Talker GGUF", AudioAiSettings.QwenTalkerModel);
                    AudioAiSettings.QwenCodecModel = EditorGUILayout.TextField("Codec GGUF", AudioAiSettings.QwenCodecModel);

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("ffmpeg (FLAC → WAV)", EditorStyles.boldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string prev = AudioAiSettings.FfmpegPath;
                        AudioAiSettings.FfmpegPath =
                            EditorGUILayout.TextField("Путь к ffmpeg", AudioAiSettings.FfmpegPath);
                        if (prev != AudioAiSettings.FfmpegPath) AudioConvert.InvalidateProbe();
                        if (GUILayout.Button("…", GUILayout.Width(28)))
                        {
                            string picked = EditorUtility.OpenFilePanel("ffmpeg.exe", "", "exe");
                            if (!string.IsNullOrEmpty(picked))
                            {
                                AudioAiSettings.FfmpegPath = picked;
                                AudioConvert.InvalidateProbe();
                            }
                        }
                    }
                    if (!AudioConvert.IsAvailable)
                        EditorGUILayout.HelpBox(
                            "ffmpeg не найден — без него WAV не собрать (ComfyUI отдаёт FLAC). " +
                            "Укажите путь или добавьте ffmpeg в PATH.", MessageType.Warning);

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Стиль по умолчанию", EditorStyles.boldLabel);
                    var def = (AudioStylePreset)EditorGUILayout.ObjectField(
                        "Дефолтный стиль", AudioStyleRegistry.Default, typeof(AudioStylePreset), false);
                    if (def != AudioStyleRegistry.Default) AudioStyleRegistry.Default = def;
                },
            };
        }

        /// <summary>
        /// Запасные ключи ElevenLabs: кончились кредиты у текущего — запрос уходит со следующим.
        /// Ключ другого аккаунта видит только свои голоса: голоса из библиотеки добавьте и туда.
        /// </summary>
        private static void DrawReserveKeys()
        {
            var reserve = AudioAiSettings.ElevenLabsReserveKeys;
            bool changed = false;
            int remove = -1;
            for (int i = 0; i < reserve.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string v = EditorGUILayout.PasswordField(
                        new GUIContent($"Запасной ключ {i + 1}",
                            "Берётся, когда у предыдущих ключей кончились кредиты. Только EditorPrefs"),
                        reserve[i]);
                    if (v != reserve[i]) { reserve[i] = v; changed = true; }
                    if (GUILayout.Button("×", GUILayout.Width(28))) remove = i;
                }
            }
            if (remove >= 0) { reserve.RemoveAt(remove); changed = true; }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth);
                if (GUILayout.Button("+ Запасной ключ", GUILayout.Width(140))) { reserve.Add(""); changed = true; }
                int count = ElevenLabsClient.KeyCount;
                if (count > 1)
                    GUILayout.Label($"сейчас работает ключ {ElevenLabsClient.ActiveKeyIndex + 1} из {count}",
                        EditorStyles.miniLabel);
            }
            if (changed)
            {
                AudioAiSettings.SaveElevenLabsReserveKeys();
                ElevenLabsClient.InvalidateCredits();
            }
            if (reserve.Count > 0)
                EditorGUILayout.HelpBox(
                    "Кончились кредиты у текущего ключа — студия повторяет запрос со следующим. " +
                    "Ключ другого аккаунта видит только свои голоса: голоса из библиотеки ElevenLabs добавьте и туда.",
                    MessageType.None);
        }
    }
}
