// Packages/com.protosystem.core/Editor/Publishing/Platforms/Steam/SteamPublisher.cs
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
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
                return await RunSteamCmdAsync(password, $"+run_app_build \"{vdfPath}\" ", progress);
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
            // Just login + quit.
            return await RunSteamCmdAsync(password, string.Empty, progress);
        }

        private async Task<PublishResult> RunSteamCmdAsync(string password, string steamCmdActionArgs,
            IProgress<PublishProgress> progress)
        {
            // First attempt without Steam Guard code; if required, prompt and retry once with code.
            var firstAttempt = await RunSteamCmdOnceAsync(password, steamCmdActionArgs, steamGuardCode: null, progress);
            if (firstAttempt.Success)
            {
                return firstAttempt;
            }

            if (!IsSteamGuardRequired(firstAttempt.Error))
            {
                return firstAttempt;
            }

            progress?.Report(new PublishProgress { Status = "Steam Guard code required...", Progress = 0.35f });
            var code = await SteamGuardCodePromptWindow.PromptAsync(
                "Steam Guard Required",
                "Steam Guard is enabled for this account. Enter the Steam Guard / 2FA code and submit to continue, or cancel to abort.\n\n" +
                "Tip: the code may come from email or the Steam mobile app.");

            if (string.IsNullOrWhiteSpace(code))
            {
                return PublishResult.Fail("Upload cancelled (Steam Guard code not provided).");
            }

            return await RunSteamCmdOnceAsync(password, steamCmdActionArgs, code.Trim(), progress);
        }

        private async Task<PublishResult> RunSteamCmdOnceAsync(string password, string steamCmdActionArgs, string steamGuardCode,
            IProgress<PublishProgress> progress)
        {
            var tcs = new TaskCompletionSource<PublishResult>();

            var loginArgs = string.IsNullOrEmpty(steamGuardCode)
                ? $"+login \"{_config.username}\" \"{password}\" "
                : $"+login \"{_config.username}\" \"{password}\" \"{steamGuardCode}\" ";

            var args = loginArgs +
                       (string.IsNullOrEmpty(steamCmdActionArgs) ? string.Empty : steamCmdActionArgs) +
                       "+quit";

            var (stdoutEncoding, stderrEncoding) = GetSteamCmdEncodings();
            var output = new StringBuilder();
            var buildId = "";
            var steamGuardDetected = false;
            Process process = null;
            void TryResolveSteamGuard()
            {
                if (steamGuardDetected) return;
                steamGuardDetected = true;
                try { process.Kill(); } catch { }
                tcs.TrySetResult(PublishResult.Fail("Steam Guard authentication required."));
            }

            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _config.steamCmdPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = stdoutEncoding,
                    StandardErrorEncoding = stderrEncoding
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                var line = SanitizeSteamCmdLine(e.Data);

                output.AppendLine(line);
                Debug.Log($"[SteamCMD] {line}");

                if (line.IndexOf("Steam Guard", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    TryResolveSteamGuard();
                    return;
                }

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

                if (line.Contains("BuildID"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"BuildID\s*(\d+)");
                    if (match.Success)
                    {
                        buildId = match.Groups[1].Value;
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                var line = SanitizeSteamCmdLine(e.Data);
                output.AppendLine($"ERROR: {line}");
                Debug.LogError($"[SteamCMD] {line}");

                if (line.IndexOf("Steam Guard", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    TryResolveSteamGuard();
                }
            };

            process.Exited += (sender, e) =>
            {
                try
                {
                    if (steamGuardDetected)
                    {
                        return;
                    }

                    var combined = output.ToString();
                    var success = process.ExitCode == 0 &&
                                  combined.IndexOf("FAILED", StringComparison.OrdinalIgnoreCase) < 0 &&
                                  combined.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0;

                    if (success)
                    {
                        tcs.TrySetResult(PublishResult.Ok(
                            $"Successfully uploaded to Steam (Branch: {_config.defaultBranch})",
                            buildId));
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
                    try { process.Dispose(); } catch { }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(30));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                try { process.Kill(); } catch { }
                return PublishResult.Fail("Upload timed out (30 minutes)");
            }

            return await tcs.Task;
        }

        private static string SanitizeSteamCmdLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;

            // SteamCMD sometimes writes progress bars / box-drawing characters; keep logs readable.
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

                // Replace common box drawing/block elements with simple ASCII.
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
            if (output.Contains("Steam Guard"))
                return "Steam Guard authentication required.";
            if (output.Contains("not find"))
                return "SteamCMD could not find required files";

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
            // When output is redirected (no console), most Windows apps emit using ANSI codepage (ACP).
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

        private static bool IsSteamGuardRequired(string error)
        {
            if (string.IsNullOrEmpty(error)) return false;
            return error.IndexOf("Steam Guard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("two-factor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("2fa", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
