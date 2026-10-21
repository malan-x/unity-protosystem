using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Поиск использований sound id по проекту: сериализованные ассеты (профили звука,
    /// схемы, PlaySoundOn в префабах/сценах) + строковые литералы в коде. Отвечает на
    /// вопрос «этот звук вообще где-то триггерится в игре?» — записи SoundLibrary без
    /// единой ссылки в бою не прозвучат никогда.
    ///
    /// Исключаются сами хранилища звуков (SoundLibrary, данные студии, варианты,
    /// локализация, доки) — иначе каждый звук «используется» в самом себе. Проектные
    /// хранилища добавляются в AudioStudioExtensions.UsageScanExcludes.
    /// Скан по запросу (кнопка в студии), результат кэшируется до следующего скана.
    /// </summary>
    public static class SoundUsageScanner
    {
        private static Dictionary<string, List<string>> _usages;

        public static bool HasResults => _usages != null;
        public static bool IsScanning { get; private set; }

        /// <summary>null — не сканировано или id вне проверки (динамические, напр. voice_*).</summary>
        public static bool? IsUsed(string id)
        {
            if (_usages == null || string.IsNullOrEmpty(id)) return null;
            if (IsDynamic(id)) return null;   // играются по константам, литералом в коде не встречаются
            return _usages.TryGetValue(id, out var refs) && refs.Count > 0;
        }

        public static IReadOnlyList<string> UsagesOf(string id)
            => _usages != null && _usages.TryGetValue(id, out var refs) ? refs : null;

        private static bool IsDynamic(string id)
        {
            foreach (var prefix in AudioStudioExtensions.DynamicIdPrefixes)
                if (!string.IsNullOrEmpty(prefix) && id.StartsWith(prefix)) return true;
            return false;
        }

        private static readonly string[] ScanExtensions = { ".cs", ".asset", ".prefab", ".unity" };

        /// <summary>Фрагменты путей, которые скан не читает. Собирается на главном потоке перед сканом.</summary>
        private static List<string> ExcludedPathParts()
        {
            var parts = new List<string> { "Assets/Localization/", "/Docs/" };

            var soundLibrary = AudioVariantStore.SoundLibrary;
            if (soundLibrary != null) parts.Add(AssetDatabase.GetAssetPath(soundLibrary));

            // Данные студии (библиотека вариантов, паки, наборы) лежат рядом с библиотекой
            string libDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(AudioVariantStore.Library));
            if (!string.IsNullOrEmpty(libDir)) parts.Add(libDir.Replace('\\', '/') + "/");

            // Папки вариантов наборов
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(AudioCollectionAsset)))
            {
                var col = AssetDatabase.LoadAssetAtPath<AudioCollectionAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (col != null && !string.IsNullOrEmpty(col.wavFolder))
                    parts.Add(col.wavFolder.TrimEnd('/') + "/");
            }

            parts.AddRange(AudioStudioExtensions.UsageScanExcludes);
            return parts;
        }

        public static async Task ScanAsync(IEnumerable<string> soundIds)
        {
            if (IsScanning) return;
            IsScanning = true;
            try
            {
                var ids = new List<string>();
                foreach (var id in soundIds)
                    if (!string.IsNullOrEmpty(id) && !IsDynamic(id) && !ids.Contains(id))
                        ids.Add(id);

                // Список файлов — на главном потоке (AssetDatabase), чтение — в фоне
                var excludedParts = ExcludedPathParts();
                var files = new List<string>();
                foreach (var path in AssetDatabase.GetAllAssetPaths())
                {
                    if (!path.StartsWith("Assets/")) continue;
                    bool extOk = false;
                    foreach (var ext in ScanExtensions)
                        if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) { extOk = true; break; }
                    if (!extOk) continue;

                    bool excluded = false;
                    foreach (var part in excludedParts)
                        if (path.Contains(part)) { excluded = true; break; }
                    if (!excluded) files.Add(path);
                }

                var result = new Dictionary<string, List<string>>();
                foreach (var id in ids) result[id] = new List<string>();

                // ConfigureAwait(false): завершение НЕ требует главного потока — иначе
                // любое блокирующее ожидание задачи (скрипт, тест) дедлочит редактор.
                await Task.Run(() =>
                {
                    foreach (var path in files)
                    {
                        string content;
                        try
                        {
                            var info = new FileInfo(path);
                            if (!info.Exists || info.Length > 64L * 1024 * 1024) continue;
                            content = File.ReadAllText(path);
                        }
                        catch { continue; }

                        foreach (var id in ids)
                            if (content.Contains(id))
                                result[id].Add(path);
                    }
                }).ConfigureAwait(false);

                _usages = result;
            }
            finally { IsScanning = false; }
        }
    }
}
