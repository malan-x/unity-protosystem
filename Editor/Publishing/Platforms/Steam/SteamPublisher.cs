// Packages/com.protosystem.core/Editor/Publishing/Platforms/Steam/SteamPublisher.cs
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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

                if (!Directory.Exists(buildPath))
                {
                    return PublishResult.Fail($"Build path not found: {buildPath}");
                }

                progress?.Report(new PublishProgress { Status = "Generating VDF files...", Progress = 0.2f });
                var vdfPath = SteamVDFGenerator.GenerateAppBuild(_config, branch, description);
                Debug.Log($"[Steam] Generated VDF: {vdfPath}");

                var password = SecureCredentials.GetPassword("steam", _config.username);
                if (string.IsNullOrEmpty(password))
                {
                    return PublishResult.Fail("Steam password not set. Configure in Build Publisher window.");
                }

                progress?.Report(new PublishProgress { Status = "Starting SteamCMD...", Progress = 0.3f });
                return await RunSteamCmdAsync(password, $"+run_app_build \"{vdfPath}\"", progress);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Steam] Upload failed: {ex.Message}");
                return PublishResult.Fail(ex.Message);
            }
        }

        public async Task<PublishResult> CheckAuthenticationAsync(IProgress<PublishProgress> progress = null)
        {
            if (!ValidateConfig(out var error))
            {
                return PublishResult.Fail(error);
            }

            if (string.IsNullOrEmpty(_config.username))
            {
                return PublishResult.Fail("Steam username not set.");
            }

            var password = SecureCredentials.GetPassword("steam", _config.username);
            if (string.IsNullOrEmpty(password))
            {
                return PublishResult.Fail("Steam password not set. Configure in Build Publisher window.");
            }

            progress?.Report(new PublishProgress { Status = "Checking Steam authentication...", Progress = 0.1f, IsIndeterminate = true });
            return await RunSteamCmdAsync(password, string.Empty, progress);
        }

        private async Task<PublishResult> RunSteamCmdAsync(string password, string actionArgs,
            IProgress<PublishProgress> progress)
        {
            var tcs = new TaskCompletionSource<PublishResult>();
            var output = new StringBuilder();
            var buildId = "";
            var steamGuardPrompted = false;
            var steamGuardCodeSent = false;
            var loginSuccess = false;
            Process process = null;
            StreamWriter stdin = null;

            var (stdoutEncoding, stderrEncoding) = GetSteamCmdEncodings();

            // Build arguments - login без кода, код будет отправлен через stdin если потребуется
            var args = $"+login \"{_config.username}\" \"{password}\" " +
                       (string.IsNullOrEmpty(actionArgs) ? "" : actionArgs + " ") +
                       "+quit";

            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _config.steamCmdPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true, // Важно для Steam Guard!
                    CreateNoWindow = true,
                    StandardOutputEncoding = stdoutEncoding,
                    StandardErrorEncoding = stderrEncoding
                },
                EnableRaisingEvents = true
            };

            // Обработка Steam Guard через stdin
            async void HandleSteamGuardPrompt()
            {
                if (steamGuardCodeSent) return;
                steamGuardCodeSent = true;

                Debug.Log("[SteamCMD] Steam Guard code required, prompting user...");
                progress?.Report(new PublishProgress { Status = "Steam Guard code required...", Progress = 0.35f });

                try
                {
                    var code = await SteamGuardCodePromptWindow.PromptAsync(
                        "Steam Guard Required",
                        "Steam Guard is enabled for this account.\n\n" +
                        "Enter the code from your email or Steam Mobile app:");

                    if (string.IsNullOrWhiteSpace(code))
                    {
                        Debug.Log("[SteamCMD] Steam Guard cancelled by user");
                        try { process?.Kill(); } catch { }
                        tcs.TrySetResult(PublishResult.Fail("Upload cancelled (Steam Guard code not provided)."));
                        return;
                    }

                    Debug.Log($"[SteamCMD] Sending Steam Guard code...");
                    progress?.Report(new PublishProgress { Status = "Authenticating with Steam Guard...", Progress = 0.4f });

                    // Отправляем код в stdin процесса
                    if (stdin != null && !process.HasExited)
                    {
                        await stdin.WriteLineAsync(code.Trim());
                        await stdin.FlushAsync();
                        Debug.Log("[SteamCMD] Steam Guard code sent to stdin");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SteamCMD] Error handling Steam Guard: {ex.Message}");
                    tcs.TrySetResult(PublishResult.Fail($"Steam Guard error: {ex.Message}"));
                }
            }

            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                var line = SanitizeSteamCmdLine(e.Data);
                output.AppendLine(line);
                Debug.Log($"[SteamCMD] {line}");

                // Проверяем нужен ли Steam Guard
                if (!steamGuardPrompted && IsSteamGuardPrompt(line))
                {
                    steamGuardPrompted = true;
                    // Запускаем в главном потоке Unity
                    UnityEditor.EditorApplication.delayCall += HandleSteamGuardPrompt;
                    return;
                }

                // Отслеживаем успешный логин
                if (line.Contains("Logged in OK") || line.Contains("Waiting for user info"))
                {
                    loginSuccess = true;
                    progress?.Report(new PublishProgress { Status = "Logged in, preparing upload...", Progress = 0.45f });
                }

                // Отслеживаем прогресс загрузки
                if (line.Contains("Uploading"))
                {
                    progress?.Report(new PublishProgress
                    {
                        Status = "Uploading to Steam...",
                        Progress = 0.5f,
                        IsIndeterminate = true
                    });
                }
                else if (line.Contains("Successfully"))
                {
                    progress?.Report(new PublishProgress { Status = "Upload complete!", Progress = 1f });
                }

                // Извлекаем Build ID
                if (line.Contains("BuildID"))
                {
                    var match = Regex.Match(line, @"BuildID\s*(\d+)");
                    if (match.Success)
                    {
                        buildId = match.Groups[1].Value;
                        Debug.Log($"[SteamCMD] Build ID: {buildId}");
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                var line = SanitizeSteamCmdLine(e.Data);
                output.AppendLine($"ERROR: {line}");
                Debug.LogWarning($"[SteamCMD] {line}");

                if (!steamGuardPrompted && IsSteamGuardPrompt(line))
                {
                    steamGuardPrompted = true;
                    UnityEditor.EditorApplication.delayCall += HandleSteamGuardPrompt;
                }
            };

            process.Exited += (sender, e) =>
            {
                try
                {
                    // Даём время на обработку последних сообщений
                    Thread.Sleep(100);

                    var combined = output.ToString();
                    var exitCode = process.ExitCode;

                    Debug.Log($"[SteamCMD] Process exited with code: {exitCode}");

                    // Проверяем успех
                    var hasError = combined.IndexOf("FAILED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   (combined.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                    !combined.Contains("Error registering")); // Игнорируем это сообщение

                    var success = exitCode == 0 && !hasError;

                    // Если запрашивался Steam Guard но код не был отправлен - это ошибка
                    if (steamGuardPrompted && !steamGuardCodeSent)
                    {
                        tcs.TrySetResult(PublishResult.Fail("Steam Guard authentication was required but not completed."));
                        return;
                    }

                    if (success)
                    {
                        var message = string.IsNullOrEmpty(buildId)
                            ? "Successfully completed Steam operation"
                            : $"Successfully uploaded to Steam (Build ID: {buildId})";
                        tcs.TrySetResult(PublishResult.Ok(message, buildId));
                    }
                    else
                    {
                        var errorMsg = ExtractError(combined);
                        tcs.TrySetResult(PublishResult.Fail(errorMsg));
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetResult(PublishResult.Fail(ex.Message));
                }
                finally
                {
                    try { stdin?.Dispose(); } catch { }
                    try { process?.Dispose(); } catch { }
                }
            };

            try
            {
                process.Start();
                stdin = process.StandardInput;
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
            catch (Exception ex)
            {
                Debug.LogError($"[SteamCMD] Failed to start: {ex.Message}");
                return PublishResult.Fail($"Failed to start SteamCMD: {ex.Message}");
            }
        }

        private static bool IsSteamGuardPrompt(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;

            var lower = line.ToLowerInvariant();
            return lower.Contains("steam guard") ||
                   lower.Contains("two-factor") ||
                   lower.Contains("2fa") ||
                   lower.Contains("enter the current code") ||
                   lower.Contains("two factor code") ||
                   lower.Contains("auth code");
        }

        private static string SanitizeSteamCmdLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;

            var sb = new StringBuilder(line.Length);
            foreach (var ch in line)
            {
                if (ch == '\t' || ch == ' ')
                {
                    sb.Append(ch);
                    continue;
                }

                if (char.IsControl(ch))
                {
                    continue;
                }

                // Replace box drawing characters with ASCII
                if ((ch >= '\u2500' && ch <= '\u259F') || (ch >= '\u2580' && ch <= '\u259F'))
                {
                    sb.Append('#');
                    continue;
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }

        private string ExtractError(string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                return "Upload failed. Check console for details.";
            }

            if (output.Contains("Invalid Password"))
                return "Invalid Steam password";
            if (output.Contains("Rate Limit"))
                return "Steam rate limit exceeded. Try again later.";
            if (output.Contains("Steam Guard") && !output.Contains("Logged in OK"))
                return "Steam Guard authentication failed.";
            if (output.Contains("not find") || output.Contains("No such file"))
                return "SteamCMD could not find required files";
            if (output.Contains("Login Failure"))
                return "Steam login failed. Check username and password.";
            if (output.Contains("Access is denied"))
                return "Access denied. Check file permissions.";

            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("FAILED") || 
                    (line.Contains("Error") && !line.Contains("Error registering")))
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

            await Task.Delay(100);
            return PublishResult.Fail("Steam Web API news publishing not yet implemented");
        }

        public async Task<PublishResult> SetLiveAsync(string buildId, string branch,
            IProgress<PublishProgress> progress = null)
        {
            await Task.Delay(100);
            return PublishResult.Fail("Use Steamworks Partner Site to set builds live, or enable 'Auto Set Live' in config");
        }

        private static (Encoding stdout, Encoding stderr) GetSteamCmdEncodings()
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    var ansi = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
                    return (ansi, ansi);
                }
            }
            catch
            {
                // ignore
            }

            return (Encoding.UTF8, Encoding.UTF8);
        }
    }
}
