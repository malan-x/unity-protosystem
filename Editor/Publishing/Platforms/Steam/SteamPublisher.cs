// Packages/com.protosystem.core/Editor/Publishing/Platforms/Steam/SteamPublisher.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Издатель для Steam
    /// </summary>
    public class SteamPublisher : IPlatformPublisher
    {
        private readonly SteamConfig _config;

        public string PlatformId => "steam";
        public string DisplayName => "Steam";
        public bool IsSupported => true;

        public SteamPublisher(SteamConfig config)
        {
            _config = config;
        }

        public bool ValidateConfig(out string error)
        {
            if (_config == null)
            {
                error = "Steam config not set";
                return false;
            }
            return _config.Validate(out error);
        }

        public async Task<PublishResult> UploadAsync(string buildPath, string branch, string description,
            IProgress<PublishProgress> progress = null)
        {
            if (!ValidateConfig(out var error))
            {
                return PublishResult.Fail(error);
            }

            try
            {
                progress?.Report(new PublishProgress { Status = "Preparing upload...", Progress = 0.1f });

                // Проверяем путь к билду
                if (!Directory.Exists(buildPath))
                {
                    return PublishResult.Fail($"Build path not found: {buildPath}");
                }

                // Генерируем VDF
                progress?.Report(new PublishProgress { Status = "Generating VDF files...", Progress = 0.2f });
                
                var vdfPath = SteamVDFGenerator.GenerateAppBuild(_config, branch, description);
                Debug.Log($"[Steam] Generated VDF: {vdfPath}");

                // Получаем пароль
                var password = SecureCredentials.GetPassword("steam", _config.username);
                if (string.IsNullOrEmpty(password))
                {
                    return PublishResult.Fail("Steam password not set. Configure in Build Publisher window.");
                }

                // Запускаем SteamCMD
                progress?.Report(new PublishProgress { Status = "Starting SteamCMD...", Progress = 0.3f });

                var result = await RunSteamCmdAsync(vdfPath, password, progress);
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Steam] Upload failed: {ex.Message}");
                return PublishResult.Fail(ex.Message);
            }
        }

        private async Task<PublishResult> RunSteamCmdAsync(string vdfPath, string password,
            IProgress<PublishProgress> progress)
        {
            var tcs = new TaskCompletionSource<PublishResult>();

            // Формируем аргументы
            var args = $"+login \"{_config.username}\" \"{password}\" " +
                      $"+run_app_build \"{vdfPath}\" " +
                      "+quit";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _config.steamCmdPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            var output = "";
            var buildId = "";
            var uploadStarted = false;

            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                
                output += e.Data + "\n";
                Debug.Log($"[SteamCMD] {e.Data}");

                // Парсим прогресс
                if (e.Data.Contains("Uploading"))
                {
                    uploadStarted = true;
                    progress?.Report(new PublishProgress 
                    { 
                        Status = "Uploading to Steam...", 
                        Progress = 0.5f,
                        IsIndeterminate = true
                    });
                }
                else if (e.Data.Contains("Successfully"))
                {
                    progress?.Report(new PublishProgress { Status = "Upload complete!", Progress = 1f });
                }

                // Извлекаем Build ID
                if (e.Data.Contains("BuildID"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(e.Data, @"BuildID\s*(\d+)");
                    if (match.Success)
                    {
                        buildId = match.Groups[1].Value;
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output += $"ERROR: {e.Data}\n";
                    Debug.LogError($"[SteamCMD] {e.Data}");
                }
            };

            process.Exited += (sender, e) =>
            {
                var success = process.ExitCode == 0 && 
                             !output.Contains("FAILED") && 
                             !output.Contains("error");

                if (success)
                {
                    tcs.SetResult(PublishResult.Ok(
                        $"Successfully uploaded to Steam (Branch: {_config.defaultBranch})", 
                        buildId));
                }
                else
                {
                    var errorMsg = ExtractError(output);
                    tcs.SetResult(PublishResult.Fail(errorMsg));
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Таймаут 30 минут
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(30));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                try { process.Kill(); } catch { }
                return PublishResult.Fail("Upload timed out (30 minutes)");
            }

            return await tcs.Task;
        }

        private string ExtractError(string output)
        {
            // Ищем типичные ошибки Steam
            if (output.Contains("Invalid Password"))
                return "Invalid Steam password";
            if (output.Contains("Rate Limit"))
                return "Steam rate limit exceeded. Try again later.";
            if (output.Contains("Steam Guard"))
                return "Steam Guard authentication required. Check your email.";
            if (output.Contains("not find"))
                return "SteamCMD could not find required files";
                
            // Ищем строку с FAILED
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("FAILED") || line.Contains("Error"))
                {
                    return line.Trim();
                }
            }
            
            return "Upload failed. Check console for details.";
        }

        public async Task<PublishResult> PublishNewsAsync(PatchNotesEntry entry, 
            IProgress<PublishProgress> progress = null)
        {
            if (!_config.publishNews)
            {
                return PublishResult.Fail("News publishing not enabled in config");
            }

            // TODO: Реализовать через Steam Web API
            // Требует: Steam Web API Key, корректный App ID
            
            await Task.Delay(100); // Placeholder
            return PublishResult.Fail("Steam Web API news publishing not yet implemented");
        }

        public async Task<PublishResult> SetLiveAsync(string buildId, string branch,
            IProgress<PublishProgress> progress = null)
        {
            // SteamCMD не поддерживает установку live напрямую
            // Это делается через Steamworks Partner Site или autoSetLive в VDF
            
            await Task.Delay(100);
            return PublishResult.Fail("Use Steamworks Partner Site to set builds live, or enable 'Auto Set Live' in config");
        }
    }
}
