using System;
using System.Diagnostics;
using System.IO;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Конвертация звука через ffmpeg. Нужна потому, что ComfyUI сохраняет аудио только в
    /// FLAC/MP3/Opus, а Unity импортирует WAV/MP3/OGG — берём lossless-цепочку FLAC → WAV.
    /// Путь к ffmpeg — в AudioAiSettings.FfmpegPath ("ffmpeg", если он в PATH).
    /// </summary>
    public static class AudioConvert
    {
        private static bool? _available;

        /// <summary>Доступен ли ffmpeg (кэшируется до перезапуска редактора).</summary>
        public static bool IsAvailable
        {
            get
            {
                _available ??= Probe();
                return _available.Value;
            }
        }

        public static void InvalidateProbe() => _available = null;

        private static bool Probe()
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = AudioAiSettings.FfmpegPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });
                p.WaitForExit(10000);
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        /// <summary>Совместимость: старое имя для FLAC-входа.</summary>
        public static void FlacToWav(byte[] flacBytes, string wavAbsolutePath,
                                     bool trimTailSilence = false, int fadeMs = 15,
                                     float maxSeconds = 0f)
            => ToWav(flacBytes, wavAbsolutePath, trimTailSilence, fadeMs, maxSeconds, "flac");

        /// <summary>
        /// Аудио-байты (FLAC от ComfyUI, MP3 от ElevenLabs — ffmpeg сам разберётся по
        /// sourceExt) → WAV-файл (44.1 кГц, 16 бит). trimTailSilence срезает тишину в начале
        /// И в хвосте — важно для one-shot SFX: модель заполняет запрошенную длительность,
        /// тишина по краям даёт «лаг» при частом проигрывании. Порог -55 дБ щадит реверб-хвосты.
        /// fadeMs — короткий фейд в конце (анти-щелчок после обрезки).
        /// maxSeconds > 0 — жёстко ограничить длительность (генерим длинно ради качества
        /// Stable Audio, целевая длина короче) с фейдом на срезе.
        /// </summary>
        public static void ToWav(byte[] audioBytes, string wavAbsolutePath,
                                 bool trimTailSilence = false, int fadeMs = 15,
                                 float maxSeconds = 0f, string sourceExt = "flac",
                                 string extraFilter = null)
        {
            string tmp = Path.Combine(Path.GetTempPath(),
                "protoaudio_" + Guid.NewGuid().ToString("N") + "." + sourceExt);
            File.WriteAllBytes(tmp, audioBytes);
            try
            {
                var ic = System.Globalization.CultureInfo.InvariantCulture;
                var filters = new System.Collections.Generic.List<string>();

                // Эффект (рация и т.п.) — ПЕРЕД тримом: порог тишины -55dB должен мерить
                // уже обработанный сигнал, иначе bandpass «оживит» отрезанные края.
                // Сначала нормализуем частоту до 44.1к: фильтры вроде asetrate=44100*x
                // предполагают её на входе, а движки отдают разное (Qwen — 24 кГц:
                // без ресемпла питч-даун превращался в ускорение ×1.7).
                if (!string.IsNullOrWhiteSpace(extraFilter))
                {
                    filters.Add("aresample=44100");
                    filters.Add(extraFilter.Trim());
                }

                if (trimTailSilence)
                {
                    // Тишина в начале — silenceremove умеет только начало…
                    filters.Add("silenceremove=start_periods=1:start_threshold=-55dB");
                    // …поэтому хвост режем через разворот. fade-IN на развёрнутом звуке =
                    // fade-out хвоста в итоге (t=out здесь заглушил бы весь клип).
                    string fade = fadeMs > 0
                        ? ",afade=t=in:st=0:d=" + (fadeMs / 1000f).ToString(ic) + ":curve=tri"
                        : "";
                    filters.Add("areverse,silenceremove=start_periods=1:start_threshold=-55dB" + fade + ",areverse");
                }

                string durArg = "";
                if (maxSeconds > 0f)
                {
                    // фейд у среза, чтобы жёсткий -t не щёлкал
                    float fadeStart = Math.Max(0f, maxSeconds - 0.05f);
                    filters.Add("afade=t=out:st=" + fadeStart.ToString(ic) + ":d=0.05");
                    durArg = " -t " + maxSeconds.ToString(ic);
                }

                string filterArg = filters.Count == 0 ? "" : $" -af \"{string.Join(",", filters)}\"";
                Run($"-y -i \"{tmp}\" -ar 44100 -sample_fmt s16{filterArg}{durArg} \"{wavAbsolutePath}\"");

                if (!File.Exists(wavAbsolutePath) || new FileInfo(wavAbsolutePath).Length < 100)
                    throw new Exception("ffmpeg: WAV не создан или пуст.");
            }
            finally
            {
                try { File.Delete(tmp); } catch { /* мусор в темпе не критичен */ }
            }
        }

        private static void Run(string args)
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = AudioAiSettings.FfmpegPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit(120000);
            if (p.ExitCode != 0)
                throw new Exception($"ffmpeg (код {p.ExitCode}): {Tail(err)}");
        }

        private static string Tail(string s)
            => string.IsNullOrEmpty(s) ? "" : (s.Length > 400 ? "…" + s.Substring(s.Length - 400) : s);
    }
}
