// Packages/com.protosystem.core/Editor/Publishing/Core/SteamCmdLogTail.cs
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Хвост лог-файла SteamCMD (logs/console_log.txt, logs/connection_log.txt).
    /// stdout SteamCMD через пайп буферизуется блоками и приходит одним куском только по
    /// завершении процесса, а лог-файлы пишутся на каждый вывод — включая строки без \n
    /// (промпт кода, «Waiting for user info...»). Поэтому живой прогресс берём отсюда.
    /// Читает только то, что дописано после создания объекта; префикс «[дата время] » срезает.
    /// </summary>
    internal sealed class SteamCmdLogTail
    {
        private static readonly Regex TimestampPrefix =
            new Regex(@"^\[\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\] ?", RegexOptions.Compiled);

        private readonly string _path;
        private readonly StringBuilder _partial = new StringBuilder();
        private readonly Decoder _decoder = new UTF8Encoding(false).GetDecoder();
        private long _position;

        /// <summary>Папка логов существует — SteamCMD будет писать сюда.</summary>
        public bool IsAvailable { get; }

        public SteamCmdLogTail(string path)
        {
            _path = path;
            var dir = Path.GetDirectoryName(path);
            IsAvailable = !string.IsNullOrEmpty(dir) && Directory.Exists(dir);
            _position = File.Exists(path) ? new FileInfo(path).Length : 0;
        }

        /// <summary>Новые полные строки с момента прошлого вызова (без префикса времени).</summary>
        public List<string> ReadNewLines()
        {
            var lines = new List<string>();
            if (!IsAvailable || !File.Exists(_path)) return lines;

            try
            {
                using (var fs = new FileStream(_path, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length < _position) _position = 0;   // файл пересоздан/обрезан
                    if (fs.Length == _position) return lines;

                    fs.Seek(_position, SeekOrigin.Begin);
                    var buffer = new byte[fs.Length - _position];
                    var read = fs.Read(buffer, 0, buffer.Length);
                    if (read <= 0) return lines;
                    _position += read;

                    var chars = new char[_decoder.GetCharCount(buffer, 0, read)];
                    var count = _decoder.GetChars(buffer, 0, read, chars, 0);
                    _partial.Append(chars, 0, count);
                }
            }
            catch (IOException)
            {
                return lines;   // файл занят на запись — дочитаем на следующем тике
            }

            var text = _partial.ToString();
            var start = 0;
            int newline;
            while ((newline = text.IndexOf('\n', start)) >= 0)
            {
                var line = text.Substring(start, newline - start).TrimEnd('\r');
                lines.Add(TimestampPrefix.Replace(line, string.Empty));
                start = newline + 1;
            }

            _partial.Clear();
            if (start < text.Length)
                _partial.Append(text, start, text.Length - start);

            return lines;
        }
    }
}
