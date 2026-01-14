// Packages/com.protosystem.core/Editor/Publishing/Core/SDKPathFinder.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Результат поиска SDK
    /// </summary>
    public class SDKSearchResult
    {
        public string Path { get; set; }
        public string Version { get; set; }
        public bool IsValid { get; set; }
        public string Source { get; set; } // "Registry", "Disk", "Environment", "Manual"
    }

    /// <summary>
    /// Поиск путей к SDK различных платформ
    /// </summary>
    public static class SDKPathFinder
    {
        private const string PREFS_PREFIX = "ProtoSystem.Publishing.SDK.";

        #region SteamCMD

        /// <summary>
        /// Найти SteamCMD
        /// </summary>
        public static List<SDKSearchResult> FindSteamCmd()
        {
            var results = new List<SDKSearchResult>();
            
            // 1. Проверяем сохранённый путь
            var saved = EditorPrefs.GetString(PREFS_PREFIX + "SteamCmd", "");
            if (!string.IsNullOrEmpty(saved) && File.Exists(saved))
            {
                results.Add(new SDKSearchResult 
                { 
                    Path = saved, 
                    IsValid = true, 
                    Source = "Saved" 
                });
            }

            // 2. Типичные места установки
            var candidates = GetSteamCmdCandidates();
            foreach (var path in candidates)
            {
                if (File.Exists(path) && !results.Exists(r => r.Path == path))
                {
                    results.Add(new SDKSearchResult
                    {
                        Path = path,
                        IsValid = true,
                        Source = "Disk"
                    });
                }
            }

            // 3. Поиск через PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                var paths = pathEnv.Split(Path.PathSeparator);
                foreach (var dir in paths)
                {
                    var steamCmd = Path.Combine(dir, GetSteamCmdExecutable());
                    if (File.Exists(steamCmd) && !results.Exists(r => r.Path == steamCmd))
                    {
                        results.Add(new SDKSearchResult
                        {
                            Path = steamCmd,
                            IsValid = true,
                            Source = "Environment"
                        });
                    }
                }
            }

            return results;
        }

        private static List<string> GetSteamCmdCandidates()
        {
            var candidates = new List<string>();
            var executable = GetSteamCmdExecutable();

#if UNITY_EDITOR_WIN
            // Windows
            candidates.Add($@"C:\SteamCMD\{executable}");
            candidates.Add($@"C:\steamcmd\{executable}");
            candidates.Add($@"D:\SteamCMD\{executable}");
            candidates.Add($@"D:\steamcmd\{executable}");
            
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            
            candidates.Add(Path.Combine(programFiles, "SteamCMD", executable));
            candidates.Add(Path.Combine(programFilesX86, "SteamCMD", executable));
            
            // Steam installation
            var steamPath = GetSteamInstallPath();
            if (!string.IsNullOrEmpty(steamPath))
            {
                candidates.Add(Path.Combine(steamPath, executable));
            }
#elif UNITY_EDITOR_OSX
            // macOS
            candidates.Add($"/usr/local/bin/{executable}");
            candidates.Add($"/opt/homebrew/bin/{executable}");
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                "steamcmd", executable));
#else
            // Linux
            candidates.Add($"/usr/bin/{executable}");
            candidates.Add($"/usr/local/bin/{executable}");
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                "steamcmd", executable));
#endif

            return candidates;
        }

        private static string GetSteamCmdExecutable()
        {
#if UNITY_EDITOR_WIN
            return "steamcmd.exe";
#else
            return "steamcmd.sh";
#endif
        }

        private static string GetSteamInstallPath()
        {
#if UNITY_EDITOR_WIN
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
                return key?.GetValue("SteamPath")?.ToString();
            }
            catch
            {
                return null;
            }
#else
            return null;
#endif
        }

        #endregion

        #region Steamworks SDK

        /// <summary>
        /// Найти Steamworks SDK
        /// </summary>
        public static List<SDKSearchResult> FindSteamworksSDK()
        {
            var results = new List<SDKSearchResult>();
            
            // 1. Сохранённый путь
            var saved = EditorPrefs.GetString(PREFS_PREFIX + "SteamworksSDK", "");
            if (!string.IsNullOrEmpty(saved) && ValidateSteamworksSDK(saved))
            {
                results.Add(new SDKSearchResult
                {
                    Path = saved,
                    IsValid = true,
                    Source = "Saved",
                    Version = GetSteamworksVersion(saved)
                });
            }

            // 2. В папке проекта
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            var projectCandidates = new[]
            {
                Path.Combine(projectPath, "steamworks_sdk"),
                Path.Combine(projectPath, "Steamworks"),
                Path.Combine(projectPath, "SDK", "Steamworks"),
                Path.Combine(projectPath, "..", "steamworks_sdk")
            };

            foreach (var path in projectCandidates)
            {
                var fullPath = Path.GetFullPath(path);
                if (ValidateSteamworksSDK(fullPath) && !results.Exists(r => r.Path == fullPath))
                {
                    results.Add(new SDKSearchResult
                    {
                        Path = fullPath,
                        IsValid = true,
                        Source = "Project",
                        Version = GetSteamworksVersion(fullPath)
                    });
                }
            }

            // 3. Типичные места
#if UNITY_EDITOR_WIN
            var commonCandidates = new[]
            {
                @"C:\steamworks_sdk",
                @"D:\steamworks_sdk",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                    "Downloads", "steamworks_sdk")
            };
#else
            var commonCandidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                    "steamworks_sdk"),
                "/opt/steamworks_sdk"
            };
#endif

            foreach (var path in commonCandidates)
            {
                if (ValidateSteamworksSDK(path) && !results.Exists(r => r.Path == path))
                {
                    results.Add(new SDKSearchResult
                    {
                        Path = path,
                        IsValid = true,
                        Source = "Disk",
                        Version = GetSteamworksVersion(path)
                    });
                }
            }

            return results;
        }

        private static bool ValidateSteamworksSDK(string path)
        {
            if (!Directory.Exists(path)) return false;
            
            // Проверяем наличие характерных файлов
            var markers = new[]
            {
                Path.Combine(path, "sdk", "tools", "ContentBuilder"),
                Path.Combine(path, "tools", "ContentBuilder"),
                Path.Combine(path, "public", "steam")
            };

            foreach (var marker in markers)
            {
                if (Directory.Exists(marker)) return true;
            }

            return false;
        }

        private static string GetSteamworksVersion(string path)
        {
            var readmePath = Path.Combine(path, "Readme.txt");
            if (File.Exists(readmePath))
            {
                try
                {
                    var content = File.ReadAllText(readmePath);
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"v(\d+)");
                    if (match.Success) return match.Groups[1].Value;
                }
                catch { }
            }
            return "Unknown";
        }

        #endregion

        #region Butler (itch.io)

        /// <summary>
        /// Найти Butler CLI
        /// </summary>
        public static List<SDKSearchResult> FindButler()
        {
            var results = new List<SDKSearchResult>();
            var executable = GetButlerExecutable();
            
            // 1. Сохранённый путь
            var saved = EditorPrefs.GetString(PREFS_PREFIX + "Butler", "");
            if (!string.IsNullOrEmpty(saved) && File.Exists(saved))
            {
                results.Add(new SDKSearchResult { Path = saved, IsValid = true, Source = "Saved" });
            }

            // 2. itch.io app installation
#if UNITY_EDITOR_WIN
            var itchPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "itch", "apps", "butler", executable);
#elif UNITY_EDITOR_OSX
            var itchPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "itch", "apps", "butler", executable);
#else
            var itchPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "itch", "apps", "butler", executable);
#endif

            if (File.Exists(itchPath) && !results.Exists(r => r.Path == itchPath))
            {
                results.Add(new SDKSearchResult { Path = itchPath, IsValid = true, Source = "itch.io App" });
            }

            return results;
        }

        private static string GetButlerExecutable()
        {
#if UNITY_EDITOR_WIN
            return "butler.exe";
#else
            return "butler";
#endif
        }

        #endregion

        #region Saving

        /// <summary>
        /// Сохранить путь к SDK
        /// </summary>
        public static void SavePath(string sdkType, string path)
        {
            EditorPrefs.SetString(PREFS_PREFIX + sdkType, path);
        }

        /// <summary>
        /// Получить сохранённый путь
        /// </summary>
        public static string GetSavedPath(string sdkType)
        {
            return EditorPrefs.GetString(PREFS_PREFIX + sdkType, "");
        }

        #endregion
    }
}
