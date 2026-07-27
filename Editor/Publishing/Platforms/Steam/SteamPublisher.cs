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
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Издатель для Steam.
    /// Steam Guard: промпт SteamCMD выводится БЕЗ \n и через OutputDataReceived невидим,
    /// поэтому отличить «введите код» от «подтвердите на телефоне» по выводу нельзя.
    /// Схема: если логина нет через SteamGuardTimeoutSeconds — показываем окно кода,
    /// НЕ убивая сессию (SteamCMD в это время сам ждёт подтверждения в Steam Mobile app):
    ///  • подтвердил на телефоне → логин завершается в ТОЙ ЖЕ сессии, окно закрывается само;
    ///  • ввёл код → сессия перезапускается с кодом в аргументах (+login user pass code);
    ///  • отменил → операция прерывается.
    /// После первого успешного логина SteamCMD кэширует сессию — дальше без запросов.
    /// </summary>
    public class SteamPublisher : IPlatformPublisher
    {
        private readonly SteamConfig _config;

        /// <summary>Время ожидания логина перед показом окна Steam Guard.</summary>
        private const int SteamGuardTimeoutSeconds = 15;

        /// <summary>Общий таймаут на операцию (загрузка может быть долгой).</summary>
        private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(30);

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
                return PublishResult.Fail(error);

            try
            {
                progress?.Report(new PublishProgress { Status = "Preparing upload...", Progress = 0.1f });

                if (!Directory.Exists(buildPath))
                    return PublishResult.Fail($"Build path not found: {buildPath}");

                progress?.Report(new PublishProgress { Status = "Generating VDF files...", Progress = 0.2f });
                var vdfPath = SteamVDFGenerator.GenerateAppBuild(_config, branch, description);
                Debug.Log($"[Steam] Generated VDF: {vdfPath}");

                var password = SecureCredentials.GetPassword("steam", _config.username);
                if (string.IsNullOrEmpty(password))
                    return PublishResult.Fail("Steam password not set. Configure in Build Publisher window.");

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
                return PublishResult.Fail(error);

            if (string.IsNullOrEmpty(_config.username))
                return PublishResult.Fail("Steam username not set.");

            var password = SecureCredentials.GetPassword("steam", _config.username);
            if (string.IsNullOrEmpty(password))
                return PublishResult.Fail("Steam password not set. Configure in Build Publisher window.");

            progress?.Report(new PublishProgress
            {
                Status = "Checking Steam authentication...", Progress = 0.1f, IsIndeterminate = true
            });
            return await RunSteamCmdAsync(password, string.Empty, progress);
        }

        // ═══════════════════════════════════════════════════════════
        // SteamCMD process management
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Запустить SteamCMD с автоматической обработкой Steam Guard:
        /// 1. Попытка логина без кода
        /// 2. Если за 15 сек нет логина → считаем что нужен Steam Guard
        /// 3. Убить процесс, запросить код
        /// 4. Перезапустить с кодом в аргументах (+login user pass code)
        /// </summary>
        private async Task<PublishResult> RunSteamCmdAsync(string password, string actionArgs,
            IProgress<PublishProgress> progress)
        {
            // Attempt 1: без кода. Если Steam Guard — окно кода показывается ВНУТРИ,
            // параллельно живой сессии (мобильное подтверждение завершает её логин само).
            Debug.Log("[SteamCMD] Attempt 1: login without auth code...");
            var (result, needsSteamGuard, codeFromPrompt) =
                await ExecuteSteamCmd(password, null, actionArgs, progress);

            if (!needsSteamGuard)
                return result;

            // Сюда попадаем, когда пользователь ввёл код (или SteamCMD вышел сам,
            // распознав Steam Guard в выводе, — тогда код ещё не спрашивали).
            var code = codeFromPrompt;
            if (string.IsNullOrWhiteSpace(code))
            {
                Debug.Log("[SteamCMD] Steam Guard detected, prompting for code...");
                progress?.Report(new PublishProgress { Status = "Steam Guard code required...", Progress = 0.35f });
                code = await PromptSteamGuardCode();
            }

            if (string.IsNullOrWhiteSpace(code))
                return PublishResult.Fail("Upload cancelled — Steam Guard code not provided.");

            // Attempt 2: with auth code
            Debug.Log("[SteamCMD] Attempt 2: login with auth code...");
            progress?.Report(new PublishProgress { Status = "Authenticating with Steam Guard...", Progress = 0.4f });

            var (retryResult, _, _) = await ExecuteSteamCmd(password, code.Trim(), actionArgs, progress);
            return retryResult;
        }

        private const string SteamGuardPromptMessage =
            "Steam Guard is enabled for this account.\n\n" +
            "If the Steam Mobile app asked you to CONFIRM this sign-in — " +
            "just approve it on your phone and wait, this window will close itself.\n\n" +
            "Otherwise enter the code here (Steam app → Steam Guard tab, or email):";

        /// <summary>
        /// Показать окно ввода Steam Guard на главном потоке.
        /// </summary>
        private static Task<string> PromptSteamGuardCode()
        {
            return SteamGuardCodePromptWindow.PromptAsync("Steam Guard Required", SteamGuardPromptMessage);
        }

        /// <summary>
        /// Результат запуска SteamCMD.
        /// </summary>
        private struct SteamCmdOutcome
        {
            public PublishResult Result;
            public bool NeedsSteamGuard;
        }

        /// <summary>
        /// Запустить один экземпляр SteamCMD и дождаться завершения.
        /// Если authCode == null и логина нет через SteamGuardTimeoutSeconds, показывается
        /// окно кода ПАРАЛЛЕЛЬНО живому процессу: подтверждение в Steam Mobile app завершает
        /// логин в этой же сессии (окно закрывается само), ввод кода — убивает сессию и
        /// возвращает (needsSteamGuard: true, codeFromPrompt: код) для перезапуска.
        /// </summary>
        private async Task<(PublishResult result, bool needsSteamGuard, string codeFromPrompt)> ExecuteSteamCmd(
            string password, string authCode, string actionArgs, IProgress<PublishProgress> progress)
        {
            var output = new StringBuilder();
            var buildId = "";
            var loginSuccess = false;
            var steamGuardDetectedInOutput = false;
            var promptShown = false;
            // Контекст главного потока: события процесса приходят с фоновых потоков,
            // а окно Steam Guard закрывать можно только с главного.
            var mainContext = SynchronizationContext.Current;
            var processExitedTcs = new TaskCompletionSource<int>();
            // Результат окна кода: строка — введённый код, null — отмена (или тихое закрытие
            // после успешного логина — тогда loginSuccess уже true и результат игнорируется).
            var codeEnteredTcs = new TaskCompletionSource<string>();

            // Build login command with optional auth code
            var loginPart = string.IsNullOrEmpty(authCode)
                ? $"+login \"{_config.username}\" \"{password}\""
                : $"+login \"{_config.username}\" \"{password}\" \"{authCode}\"";

            var args = loginPart + " " +
                       (string.IsNullOrEmpty(actionArgs) ? "" : actionArgs + " ") +
                       "+quit";

            var (stdoutEncoding, stderrEncoding) = GetSteamCmdEncodings();

            var process = new Process
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

            // Output handlers
            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                var line = SanitizeSteamCmdLine(e.Data);
                output.AppendLine(line);
                Debug.Log($"[SteamCMD] {line}");

                // Detect Steam Guard in output (fast path)
                if (IsSteamGuardPrompt(line))
                    steamGuardDetectedInOutput = true;

                // Track login success
                if (line.Contains("Logged in OK") || line.Contains("Waiting for user info"))
                {
                    loginSuccess = true;
                    // Логин прошёл (подтверждение на телефоне) — окно кода больше не нужно
                    if (promptShown)
                        mainContext?.Post(_ => SteamGuardCodePromptWindow.CloseActiveQuietly(), null);
                    progress?.Report(new PublishProgress
                    {
                        Status = "Logged in, processing...", Progress = 0.45f
                    });
                }

                // Track upload progress
                if (line.Contains("Uploading"))
                {
                    progress?.Report(new PublishProgress
                    {
                        Status = "Uploading to Steam...", Progress = 0.5f, IsIndeterminate = true
                    });
                }
                else if (line.Contains("Successfully"))
                {
                    progress?.Report(new PublishProgress { Status = "Upload complete!", Progress = 1f });
                }

                // Extract Build ID
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

                if (IsSteamGuardPrompt(line))
                    steamGuardDetectedInOutput = true;
            };

            process.Exited += (sender, e) =>
            {
                // Small delay for final output events
                Thread.Sleep(200);
                try
                {
                    processExitedTcs.TrySetResult(process.ExitCode);
                }
                catch
                {
                    processExitedTcs.TrySetResult(-1);
                }
            };

            // ─── Start process ───

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                process.Dispose();
                Debug.LogError($"[SteamCMD] Failed to start: {ex.Message}");
                return (PublishResult.Fail($"Failed to start SteamCMD: {ex.Message}"), false, null);
            }

            // ─── Steam Guard: окно кода поверх ЖИВОЙ сессии (first attempt only) ───
            // Промпт SteamCMD печатается без \n и в событиях не виден, поэтому по таймеру.
            // Процесс не убиваем: если у аккаунта мобильное подтверждение, SteamCMD прямо
            // сейчас ждёт approve — нажатие на телефоне завершит логин в этой сессии.

            if (string.IsNullOrEmpty(authCode))
            {
                // Без Task.Run: после await продолжаем в Unity-контексте (главный поток),
                // откуда безопасно показывать EditorWindow.
                async Task GuardPromptAfterDelayAsync()
                {
                    try
                    {
                        await Task.Delay(SteamGuardTimeoutSeconds * 1000);
                        if (loginSuccess || process.HasExited) return;

                        promptShown = true;
                        Debug.Log($"[SteamCMD] No login after {SteamGuardTimeoutSeconds}s — " +
                                  "Steam Guard assumed. Approve on phone OR enter code (session kept alive)");
                        progress?.Report(new PublishProgress
                        {
                            Status = "Steam Guard: approve on phone or enter code...",
                            Progress = 0.35f, IsIndeterminate = true
                        });

                        var code = await SteamGuardCodePromptWindow.PromptAsync(
                            "Steam Guard Required", SteamGuardPromptMessage);
                        codeEnteredTcs.TrySetResult(code);
                    }
                    catch { /* process gone */ }
                }

                _ = GuardPromptAfterDelayAsync();
            }

            // ─── Wait: выход процесса / глобальный таймаут / ввод кода в окне ───

            var timeoutTask = Task.Delay(OperationTimeout);
            var codeTask = codeEnteredTcs.Task;

            while (true)
            {
                var completedTask = await Task.WhenAny(processExitedTcs.Task, timeoutTask, codeTask);

                if (completedTask == timeoutTask)
                {
                    try { process.Kill(); }
                    catch { }
                    process.Dispose();
                    return (PublishResult.Fail("Operation timed out (30 minutes)"), false, null);
                }

                if (completedTask == codeTask && !processExitedTcs.Task.IsCompleted)
                {
                    var enteredCode = codeTask.Result;
                    // Окно обработано — дальше ждём только процесс (иначе завершённый
                    // codeTask выигрывал бы WhenAny в вечном цикле).
                    codeTask = new TaskCompletionSource<string>().Task;

                    // Логин уже прошёл (подтвердили на телефоне, окно закрылось само) —
                    // результат окна не важен, ждём завершения операции в этой же сессии.
                    if (loginSuccess)
                        continue;

                    try { process.Kill(); }
                    catch { /* already exited */ }
                    process.Dispose();

                    if (string.IsNullOrWhiteSpace(enteredCode))
                        return (PublishResult.Fail("Upload cancelled — Steam Guard code not provided."),
                            false, null);

                    // Перезапуск с кодом сделает RunSteamCmdAsync
                    return (null, true, enteredCode.Trim());
                }

                break; // процесс завершился
            }

            var exitCode = await processExitedTcs.Task;
            process.Dispose();

            // Окно могло остаться открытым, если процесс умер сам (например, exit code 5)
            if (promptShown && !loginSuccess)
                SteamGuardCodePromptWindow.CloseActiveQuietly();

            // ─── Interpret results ───

            // Steam Guard needed? (SteamCMD сам вышел, распознав Guard в выводе)
            if (string.IsNullOrEmpty(authCode) && !loginSuccess &&
                (steamGuardDetectedInOutput || exitCode == 5))
            {
                return (null, true, null);
            }

            // Normal result
            var combined = output.ToString();
            var hasError = combined.IndexOf("FAILED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           (combined.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            !combined.Contains("Error registering"));

            var success = exitCode == 0 && !hasError;

            if (success || loginSuccess)
            {
                // For auth check (empty actionArgs), login success is enough
                if (string.IsNullOrEmpty(actionArgs) && loginSuccess)
                {
                    return (PublishResult.Ok("Authentication successful"), false, null);
                }

                var message = string.IsNullOrEmpty(buildId)
                    ? "Successfully completed Steam operation"
                    : $"Successfully uploaded to Steam (Build ID: {buildId})";
                return (PublishResult.Ok(message, buildId), false, null);
            }

            return (PublishResult.Fail(ExtractError(combined)), false, null);
        }

        // ═══════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════

        private static bool IsSteamGuardPrompt(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;

            var lower = line.ToLowerInvariant();
            return lower.Contains("steam guard") ||
                   lower.Contains("two-factor") ||
                   lower.Contains("2fa") ||
                   lower.Contains("enter the current code") ||
                   lower.Contains("two factor code") ||
                   lower.Contains("auth code") ||
                   lower.Contains("please check your email") ||
                   lower.Contains("enter the code") ||
                   lower.Contains("login token");
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
                    continue;

                // Replace box drawing characters with ASCII
                if (ch >= '\u2500' && ch <= '\u259F')
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
                return "Upload failed. Check console for details.";

            if (output.Contains("Invalid Password"))
                return "Invalid Steam password";
            if (output.Contains("Rate Limit"))
                return "Steam rate limit exceeded. Try again later.";
            if (output.Contains("Steam Guard") && !output.Contains("Logged in OK"))
                return "Steam Guard authentication failed. Code may be incorrect.";
            if (output.Contains("not find") || output.Contains("No such file"))
                return "SteamCMD could not find required files";
            if (output.Contains("Login Failure"))
                return "Steam login failed. Check username and password.";
            if (output.Contains("Access is denied"))
                return "Access denied. Check file permissions.";
            if (output.Contains("Invalid Login Auth Code"))
                return "Steam Guard code is invalid or expired. Try again.";
            if (output.Contains("Expired Login Token"))
                return "Login token expired. Try again.";

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
                return PublishResult.Fail("News publishing not enabled in config");

            await Task.Delay(100);
            return PublishResult.Fail("Steam Web API news publishing not yet implemented");
        }

        public async Task<PublishResult> SetLiveAsync(string buildId, string branch,
            IProgress<PublishProgress> progress = null)
        {
            await Task.Delay(100);
            return PublishResult.Fail(
                "Use Steamworks Partner Site to set builds live, or enable 'Auto Set Live' in config");
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
