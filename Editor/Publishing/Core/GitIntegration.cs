// Packages/com.protosystem.core/Editor/Publishing/Core/GitIntegration.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Информация о коммите
    /// </summary>
    public class GitCommitInfo
    {
        public string Hash { get; set; }
        public string ShortHash { get; set; }
        public string Author { get; set; }
        public DateTime Date { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    /// <summary>
    /// Интеграция с Git
    /// </summary>
    public static class GitIntegration
    {
        private static string _gitPath;

        /// <summary>
        /// Проверить наличие Git
        /// </summary>
        public static bool IsGitAvailable()
        {
            try
            {
                var result = RunGitCommand("--version");
                return result.success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Проверить, что папка проекта является Git репозиторием
        /// </summary>
        public static bool IsGitRepository()
        {
            var result = RunGitCommand("rev-parse --is-inside-work-tree");
            return result.success && result.output.Trim() == "true";
        }

        /// <summary>
        /// Получить текущую ветку
        /// </summary>
        public static string GetCurrentBranch()
        {
            var result = RunGitCommand("rev-parse --abbrev-ref HEAD");
            return result.success ? result.output.Trim() : null;
        }

        /// <summary>
        /// Получить последний тег
        /// </summary>
        public static string GetLastTag()
        {
            // Используем тихий режим - не выводим warning если тегов нет
            var result = RunGitCommandSilent("describe --tags --abbrev=0");
            return result.success ? result.output.Trim() : null;
        }

        /// <summary>
        /// Получить все теги
        /// </summary>
        public static List<string> GetAllTags()
        {
            var result = RunGitCommand("tag --sort=-version:refname");
            if (!result.success) return new List<string>();
            
            var tags = new List<string>();
            foreach (var line in result.output.Split('\n'))
            {
                var tag = line.Trim();
                if (!string.IsNullOrEmpty(tag))
                    tags.Add(tag);
            }
            return tags;
        }

        /// <summary>
        /// Создать тег
        /// </summary>
        public static bool CreateTag(string tagName, string message = null)
        {
            var cmd = string.IsNullOrEmpty(message)
                ? $"tag {tagName}"
                : $"tag -a {tagName} -m \"{message}\"";
                
            var result = RunGitCommand(cmd);
            return result.success;
        }

        /// <summary>
        /// Запушить тег
        /// </summary>
        public static bool PushTag(string tagName, string remote = "origin")
        {
            var result = RunGitCommand($"push {remote} {tagName}");
            return result.success;
        }

        /// <summary>
        /// Получить коммиты после тега
        /// </summary>
        public static List<GitCommitInfo> GetCommitsSinceTag(string tag)
        {
            var range = string.IsNullOrEmpty(tag) ? "HEAD~50..HEAD" : $"{tag}..HEAD";
            return GetCommits(range);
        }

        /// <summary>
        /// Получить коммиты в диапазоне
        /// </summary>
        public static List<GitCommitInfo> GetCommits(string range, int limit = 100)
        {
            // Используем уникальный разделитель и отключаем экранирование не-ASCII
            const string SEP = "<<|>>";
            var format = $"%H{SEP}%h{SEP}%an{SEP}%ai{SEP}%s{SEP}%b";

            // -c core.quotepath=false - отключает экранирование кириллицы
            // --encoding=UTF-8 - принудительно UTF-8
            var result = RunGitCommand($"-c core.quotepath=false log {range} --encoding=UTF-8 --format=\"{format}\" -n {limit}");

            var commits = new List<GitCommitInfo>();
            if (!result.success) return commits;

            var entries = result.output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var entry in entries)
            {
                var parts = entry.Split(new[] { SEP }, 6, StringSplitOptions.None);
                if (parts.Length < 5) continue;

                var commit = new GitCommitInfo
                {
                    Hash = parts[0],
                    ShortHash = parts[1],
                    Author = parts[2],
                    Subject = parts[4],
                    Body = parts.Length > 5 ? parts[5] : ""
                };

                if (DateTime.TryParse(parts[3], out var date))
                    commit.Date = date;

                commits.Add(commit);
            }

            return commits;
        }

        /// <summary>
        /// Получить количество коммитов после тега
        /// </summary>
        public static int GetCommitCountSinceTag(string tag)
        {
            var range = string.IsNullOrEmpty(tag) ? "HEAD" : $"{tag}..HEAD";
            var result = RunGitCommand($"rev-list --count {range}");
            
            if (result.success && int.TryParse(result.output.Trim(), out var count))
                return count;
            
            return 0;
        }

        /// <summary>
        /// Парсить коммиты в записи патчноутов
        /// </summary>
        public static List<CommitEntry> ParseCommitsToEntries(List<GitCommitInfo> commits, CommitTagConfig tagConfig)
        {
            var entries = new List<CommitEntry>();

            // Паттерн для тега: [TAG] или [TAG1][TAG2]
            var tagPattern = new Regex(@"\[([A-Za-z0-9_]+)\]", RegexOptions.Compiled);

            foreach (var commit in commits)
            {
                // Парсим subject
                ParseLine(commit.Subject, commit, tagConfig, tagPattern, entries);

                // Парсим body (каждую строку отдельно)
                if (!string.IsNullOrEmpty(commit.Body))
                {
                    foreach (var line in commit.Body.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            ParseLine(trimmed, commit, tagConfig, tagPattern, entries);
                        }
                    }
                }
            }

            return entries;
        }

        private static void ParseLine(string line, GitCommitInfo commit, CommitTagConfig tagConfig, 
            Regex tagPattern, List<CommitEntry> entries)
        {
            var matches = tagPattern.Matches(line);
            if (matches.Count == 0) return;

            // Собираем все теги
            var tags = new List<string>();
            foreach (Match match in matches)
            {
                var tagName = match.Groups[1].Value;
                var tag = tagConfig?.FindTag(tagName);

                // Добавляем тег если он есть в конфиге или конфиг пустой
                if (tag != null || tagConfig == null || tagConfig.tags.Count == 0)
                {
                    tags.Add(tagName.ToUpper());
                }
            }

            if (tags.Count == 0) return;

            // Удаляем теги из сообщения
            var message = tagPattern.Replace(line, "").Trim();
            if (string.IsNullOrEmpty(message)) return;

            // Определяем включать ли в публичные
            bool includeInPublic = true;
            foreach (var tagName in tags)
            {
                var tag = tagConfig?.FindTag(tagName);
                if (tag != null && !tag.includeInPublic)
                {
                    includeInPublic = false;
                    break;
                }
            }

            entries.Add(new CommitEntry
            {
                tags = tags,
                message = message,
                commitHash = commit.Hash,
                shortHash = commit.ShortHash,
                author = commit.Author,
                date = commit.Date.ToString("yyyy-MM-dd"),
                includeInPublic = includeInPublic
            });
        }

        /// <summary>
        /// Проверить есть ли незакоммиченные изменения
        /// </summary>
        public static bool HasUncommittedChanges()
        {
            var result = RunGitCommand("status --porcelain");
            return result.success && !string.IsNullOrEmpty(result.output.Trim());
        }

        /// <summary>
        /// Выполнить Git команду
        /// </summary>
        private static (bool success, string output) RunGitCommand(string arguments)
        {
            return RunGitCommandInternal(arguments, logErrors: true);
        }

        private static (bool success, string output) RunGitCommandSilent(string arguments)
        {
            return RunGitCommandInternal(arguments, logErrors: false);
        }

        private static (bool success, string output) RunGitCommandInternal(string arguments, bool logErrors)
        {
            try
            {
                var projectPath = System.IO.Path.GetDirectoryName(Application.dataPath);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = projectPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                // Устанавливаем переменные окружения для UTF-8
                startInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
                startInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";
                startInfo.EnvironmentVariables["LESSCHARSET"] = "utf-8";
                startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
                startInfo.EnvironmentVariables["CHCP"] = "65001";

                var process = new Process { StartInfo = startInfo };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(10000);

                if (logErrors && process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning($"[Git] {error}");
                }

                return (process.ExitCode == 0, output);
            }
            catch (Exception ex)
            {
                if (logErrors)
                {
                    Debug.LogError($"[Git] Failed to run command: {ex.Message}");
                }
                return (false, "");
            }
        }
    }
}
