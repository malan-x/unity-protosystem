using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Локальный TTS-движок: qwentts.cpp (Qwen3-TTS, GGUF, CPU/GPU) — бесплатные итерации
    /// без облака. VoiceDesign-режим: голос описывается словами (--instruct = Lyrics запроса).
    /// Текст уходит в stdin СЫРЫМИ UTF-8 байтами (BaseStream) — консольная кодировка
    /// Windows иначе калечит кириллицу. Выход: WAV 24 кГц моно → общий конвейер ffmpeg.
    /// Аудио-теги [эмоций] ElevenLabs вырезаются — Qwen их не понимает и озвучил бы скобки.
    /// </summary>
    public static class QwenTtsClient
    {
        public static bool IsAvailable =>
            File.Exists(AudioAiSettings.QwenTtsExe) &&
            File.Exists(AudioAiSettings.QwenTalkerModel) &&
            File.Exists(AudioAiSettings.QwenCodecModel);

        public static Task<byte[]> GenerateAsync(ComfyAudioClient.AudioRequest request)
        {
            // Настройки — на ГЛАВНОМ потоке: EditorPrefs из Task.Run бросает
            // «GetString can only be called from the main thread»
            string exe = AudioAiSettings.QwenTtsExe;
            string talker = AudioAiSettings.QwenTalkerModel;
            string codec = AudioAiSettings.QwenCodecModel;
            return Task.Run(() => Generate(request, exe, talker, codec));
        }

        private static byte[] Generate(ComfyAudioClient.AudioRequest request,
                                       string exe, string talker, string codec)
        {
            if (!File.Exists(exe) || !File.Exists(talker) || !File.Exists(codec))
                throw new Exception("Qwen3-TTS: не найден qwen-tts.exe или GGUF-модели " +
                                    "(Project Settings ▸ AI Audio Tools).");

            string text = StripEmotionTags(request.Positive);
            if (string.IsNullOrWhiteSpace(text))
                throw new Exception("Qwen3-TTS: пустой текст реплики.");

            string outWav = Path.Combine(Path.GetTempPath(),
                "protoaudio_qwen_" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                var args = new StringBuilder();
                args.Append("--model \"").Append(talker).Append('"');
                args.Append(" --codec \"").Append(codec).Append('"');
                if (!string.IsNullOrEmpty(request.TtsLanguage))
                    args.Append(" --lang ").Append(request.TtsLanguage);
                if (!string.IsNullOrWhiteSpace(request.Lyrics))
                    args.Append(" --instruct \"").Append(request.Lyrics.Replace("\"", "'")).Append('"');
                if (request.Seed != 0)
                    args.Append(" --seed ").Append(Math.Abs(request.Seed));
                args.Append(" -o \"").Append(outWav).Append('"');

                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args.ToString(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });

                var utf8 = Encoding.UTF8.GetBytes(text);
                p.StandardInput.BaseStream.Write(utf8, 0, utf8.Length);
                p.StandardInput.BaseStream.Flush();
                p.StandardInput.Close();

                string err = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(600000))
                {
                    try { p.Kill(); } catch { /* уже завершился */ }
                    throw new Exception("Qwen3-TTS: таймаут синтеза (10 мин).");
                }

                if (p.ExitCode != 0 || !File.Exists(outWav) || new FileInfo(outWav).Length < 100)
                    throw new Exception($"Qwen3-TTS (код {p.ExitCode}): {Tail(err)}");

                return File.ReadAllBytes(outWav);
            }
            finally
            {
                try { File.Delete(outWav); } catch { /* мусор в темпе не критичен */ }
            }
        }

        /// <summary>Вырезать [аудио-теги] ElevenLabs — Qwen озвучил бы их текстом.</summary>
        private static string StripEmotionTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return System.Text.RegularExpressions.Regex
                .Replace(text, @"\[[^\]]*\]\s*", "").Trim();
        }

        private static string Tail(string s)
            => string.IsNullOrEmpty(s) ? "см. вывод qwen-tts" : (s.Length > 300 ? "…" + s.Substring(s.Length - 300) : s);
    }
}
