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
    ///
    /// Логин. Парольный вход в новой схеме авторизации Steam ВСЕГДА требует Steam Guard
    /// (подтверждение на телефоне или код), даже если сессия уже кэширована. Поэтому первая
    /// попытка — «+login user» БЕЗ пароля: SteamCMD берёт refresh-токен из config/config.vdf
    /// и входит без вопросов за несколько секунд. Пароль идёт в ход, только если кэша нет
    /// или токен протух; после такого входа токен кэшируется снова.
    ///
    /// Вывод. stdout SteamCMD через пайп буферизуется блоками и приходит одним куском по
    /// завершении процесса — по нему нельзя ни показать прогресс, ни заметить запрос кода.
    /// Зато logs/console_log.txt и logs/connection_log.txt пишутся на каждый вывод, поэтому
    /// читаем их хвост (<see cref="SteamCmdLogTail"/>): оттуда прогресс сканирования/загрузки,
    /// момент входа, промпт кода и «Waiting for confirmation» (ждёт подтверждения на телефоне).
    ///
    /// Steam Guard при парольном входе: окно кода показывается, когда SteamCMD реально ждёт
    /// (промпт в консоли или подтверждение на телефоне), и НЕ убивает живую сессию:
    ///  • подтвердил на телефоне → логин завершается в той же сессии, окно закрывается само;
    ///  • ввёл код → сессия перезапускается с кодом в аргументах (+login user pass code);
    ///  • отменил → операция прерывается.
    /// </summary>
    public class SteamPublisher : IPlatformPublisher
    {
        private readonly SteamConfig _config;

        /// <summary>Логин по кэшу обычно занимает 3–6 с; дольше — токена нет, идём с паролем.</summary>
        private const int CachedLoginTimeoutSeconds = 45;

        /// <summary>
        /// Резервный таймер окна Steam Guard при парольном входе — на случай, если в логах
        /// не появилось ни промпта кода, ни «Waiting for confirmation».
        /// </summary>
        private const int SteamGuardFallbackSeconds = 25;

        /// <summary>Пауза между тиками чтения логов SteamCMD.</summary>
        private const int LogPollMs = 150;

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

                // Пароль нужен только если кэшированной сессии нет — проверяется внутри.
                var password = SecureCredentials.GetPassword("steam", _config.username);

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

            progress?.Report(new PublishProgress
            {
                Status = "Checking Steam authentication...", Progress = 0.1f, IsIndeterminate = true
            });
            return await RunSteamCmdAsync(password, string.Empty, progress);
        }

        // ═══════════════════════════════════════════════════════════
        // SteamCMD process management
        // ═══════════════════════════════════════════════════════════

        private enum LoginKind
        {
            /// <summary>+login user — кэшированный refresh-токен, без Steam Guard.</summary>
            Cached,
            /// <summary>+login user pass — Steam Guard спросит подтверждение/код.</summary>
            Password,
            /// <summary>+login user pass code — с кодом из окна.</summary>
            PasswordWithCode
        }

        private enum RunOutcome
        {
            /// <summary>Операция завершена — результат в Result.</summary>
            Completed,
            /// <summary>Кэшированной сессии нет/протухла — нужен парольный вход.</summary>
            CachedLoginFailed,
            /// <summary>Нужен код Steam Guard (введённый код — в третьем элементе кортежа).</summary>
            SteamGuardRequired
        }

        private enum GuardReason
        {
            /// <summary>connection_log: «Waiting for confirmation» — ждёт одобрения на телефоне.</summary>
            MobileConfirm,
            /// <summary>console_log: «Steam Guard code:» — ждёт код (почтовый/мобильный).</summary>
            CodePrompt,
            /// <summary>Логина нет дольше SteamGuardFallbackSeconds, причина в логах не видна.</summary>
            Timeout
        }

        /// <summary>
        /// Запустить SteamCMD, начиная с кэшированной сессии:
        /// 0. +login user (без пароля) — кэш, никакого Steam Guard;
        /// 1. +login user pass — если кэша нет; окно кода поверх живой сессии;
        /// 2. +login user pass code — если пользователь ввёл код.
        /// </summary>
        private async Task<PublishResult> RunSteamCmdAsync(string password, string actionArgs,
            IProgress<PublishProgress> progress)
        {
            Debug.Log("[SteamCMD] Attempt 0: cached session (no password)...");
            var (cachedResult, cachedOutcome, _) =
                await ExecuteSteamCmd(LoginKind.Cached, null, null, actionArgs, progress);
            if (cachedOutcome == RunOutcome.Completed)
                return cachedResult;

            Debug.Log("[SteamCMD] No cached session — falling back to password login");
            if (string.IsNullOrEmpty(password))
                return PublishResult.Fail(
                    "Кэшированной сессии Steam нет, а пароль не задан. Укажи пароль в Build Publisher — " +
                    "после первого входа он больше не понадобится.");

            Debug.Log("[SteamCMD] Attempt 1: password login without auth code...");
            var (result, outcome, codeFromPrompt) =
                await ExecuteSteamCmd(LoginKind.Password, password, null, actionArgs, progress);

            if (outcome != RunOutcome.SteamGuardRequired)
                return result;

            // Сюда попадаем, когда пользователь ввёл код (или SteamCMD вышел сам,
            // распознав Steam Guard в выводе, — тогда код ещё не спрашивали).
            var code = codeFromPrompt;
            if (string.IsNullOrWhiteSpace(code))
            {
                Debug.Log("[SteamCMD] Steam Guard detected, prompting for code...");
                progress?.Report(new PublishProgress { Status = "Steam Guard code required...", Progress = 0.35f });
                code = await SteamGuardCodePromptWindow.PromptAsync("Steam Guard Required",
                    GuardMessage(GuardReason.CodePrompt));
            }

            if (string.IsNullOrWhiteSpace(code))
                return PublishResult.Fail("Upload cancelled — Steam Guard code not provided.");

            Debug.Log("[SteamCMD] Attempt 2: login with auth code...");
            progress?.Report(new PublishProgress { Status = "Authenticating with Steam Guard...", Progress = 0.4f });

            var (retryResult, _, _) =
                await ExecuteSteamCmd(LoginKind.PasswordWithCode, password, code.Trim(), actionArgs, progress);
            return retryResult;
        }

        private static string GuardMessage(GuardReason reason)
        {
            const string codeHint =
                "Код: приложение Steam → вкладка-щит «Steam Guard» — крупный код из 5 символов.\n" +
                "Введи его сюда и нажми Enter, пока не сменился (таймер-полоска под кодом).";

            switch (reason)
            {
                case GuardReason.MobileConfirm:
                    return "Steam ждёт подтверждения входа в приложении Steam на телефоне.\n" +
                           "Одобри там — окно закроется само, загрузка начнётся в этой же сессии.\n\n" +
                           "Уведомление не пришло? " + codeHint;
                case GuardReason.CodePrompt:
                    return "SteamCMD запросил код Steam Guard.\n\n" + codeHint;
                default:
                    return "SteamCMD ждёт входа, загрузка ещё не идёт.\n\n" +
                           "• Пришло подтверждение на телефон? Одобри и просто жди —\n" +
                           "  окно закроется само.\n" +
                           "• Уведомления нет? " + codeHint;
            }
        }

        /// <summary>
        /// Запустить один экземпляр SteamCMD и дождаться завершения.
        /// Прогресс и события берутся из хвоста лог-файлов SteamCMD (см. класс).
        /// Для Password: окно кода показывается ПАРАЛЛЕЛЬНО живому процессу; подтверждение
        /// на телефоне завершает логин в этой же сессии (окно закрывается само), ввод кода —
        /// убивает сессию и возвращает (SteamGuardRequired, код) для перезапуска.
        /// Для Cached: если входа нет — (CachedLoginFailed) для перехода к паролю.
        /// </summary>
        private async Task<(PublishResult result, RunOutcome outcome, string code)> ExecuteSteamCmd(
            LoginKind kind, string password, string authCode, string actionArgs,
            IProgress<PublishProgress> progress)
        {
            var output = new StringBuilder();
            var buildId = "";
            var loginSuccess = false;
            var steamGuardDetected = false;
            var mobileConfirmPending = false;
            var promptShown = false;
            var uploadingPhase = false;
            var stopTail = false;

            // Обработчики строк выполняются на главном потоке (тик чтения логов идёт через
            // await в Unity-контексте); события stdout приходят с фоновых потоков — через Post.
            var mainContext = SynchronizationContext.Current;
            var processExitedTcs = new TaskCompletionSource<int>();
            var cachedFailedTcs = new TaskCompletionSource<bool>();
            // Результат окна кода: строка — введённый код, null — отмена (или тихое закрытие
            // после успешного логина — тогда loginSuccess уже true и результат игнорируется).
            var codeEnteredTcs = new TaskCompletionSource<string>();

            string loginPart;
            switch (kind)
            {
                case LoginKind.Cached:
                    loginPart = $"+login \"{_config.username}\"";
                    break;
                case LoginKind.Password:
                    loginPart = $"+login \"{_config.username}\" \"{password}\"";
                    break;
                default:
                    loginPart = $"+login \"{_config.username}\" \"{password}\" \"{authCode}\"";
                    break;
            }

            var args = loginPart + " " +
                       (string.IsNullOrEmpty(actionArgs) ? "" : actionArgs + " ") +
                       "+quit";

            var (stdoutEncoding, stderrEncoding) = GetSteamCmdEncodings();

            // Хвосты логов фиксируем ДО старта процесса — читаем только его вывод.
            var logsDir = Path.Combine(Path.GetDirectoryName(_config.steamCmdPath) ?? "", "logs");
            var consoleTail = new SteamCmdLogTail(Path.Combine(logsDir, "console_log.txt"));
            var connectionTail = new SteamCmdLogTail(Path.Combine(logsDir, "connection_log.txt"));
            var tailActive = consoleTail.IsAvailable;
            if (!tailActive)
                Debug.LogWarning($"[SteamCMD] Logs dir not found ({logsDir}) — live progress unavailable, " +
                                 "falling back to buffered stdout");

            // ─── Прогресс: статус держим, строку лога добавляем ───

            var lastStatus = kind == LoginKind.Cached ? "Logging in (cached session)..." : "Logging in...";
            var lastProgress = 0.3f;
            var lastIndeterminate = true;

            void Report(string status, float prog, bool indeterminate = false, string logLine = null)
            {
                if (status != null)
                {
                    lastStatus = status;
                    lastProgress = prog;
                    lastIndeterminate = indeterminate;
                }

                progress?.Report(new PublishProgress
                {
                    Status = lastStatus, Progress = lastProgress,
                    IsIndeterminate = lastIndeterminate, LogLine = logLine
                });
            }

            Report(lastStatus, lastProgress, lastIndeterminate);

            // ─── Окно Steam Guard (только парольный вход) ───

            void ShowGuardPrompt(GuardReason reason)
            {
                if (kind != LoginKind.Password || promptShown || loginSuccess) return;
                promptShown = true;

                Debug.Log($"[SteamCMD] Steam Guard ({reason}) — approve on phone OR enter code (session kept alive)");
                Report("Steam Guard: approve on phone or enter code...", 0.35f, true);

                var message = GuardMessage(reason);
                mainContext?.Post(async _ =>
                {
                    try
                    {
                        var code = await SteamGuardCodePromptWindow.PromptAsync("Steam Guard Required", message);
                        codeEnteredTcs.TrySetResult(code);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[SteamCMD] Steam Guard prompt failed: {ex.Message}");
                        codeEnteredTcs.TrySetResult(null);
                    }
                }, null);
            }

            // ─── Строки console_log / stdout ───

            void HandleConsoleLine(string raw)
            {
                var line = SanitizeSteamCmdLine(raw);
                if (string.IsNullOrWhiteSpace(line) || line.Trim() == ".") return;   // точки сканирования

                output.AppendLine(line);
                Debug.Log($"[SteamCMD] {line}");
                Report(null, 0, false, line);

                if (promptShown)
                    SteamGuardCodePromptWindow.SetStatus($"SteamCMD: {line}");

                if (IsSteamGuardPrompt(line))
                {
                    steamGuardDetected = true;
                    ShowGuardPrompt(GuardReason.CodePrompt);
                }

                if (!loginSuccess && IsLoginFailure(line) && kind == LoginKind.Cached)
                    cachedFailedTcs.TrySetResult(true);

                if (!loginSuccess && (line.Contains("Logged in OK") ||
                                      line.Contains("Waiting for client config") ||
                                      line.Contains("Waiting for user info")))
                {
                    loginSuccess = true;
                    if (promptShown)
                    {
                        SteamGuardCodePromptWindow.SetStatus(
                            "SteamCMD: вход выполнен! Загрузка началась, окно закрывается…");
                        SteamGuardCodePromptWindow.CloseActiveQuietly();
                    }

                    Report("Logged in, preparing build...", 0.45f);
                }

                if (loginSuccess)
                {
                    if (line.Contains("Scanning content"))
                        Report("Scanning content...", 0.5f);
                    else if (line.Contains("Uploading content"))
                    {
                        uploadingPhase = true;
                        Report("Uploading to Steam...", 0.6f);
                    }

                    // «961.2MB (99%)» — сканирование, затем загрузка; показываем как в консоли
                    var pct = Regex.Match(line, @"(\d{1,3})\s*%");
                    if (pct.Success && int.TryParse(pct.Groups[1].Value, out var percent) &&
                        percent >= 0 && percent <= 100)
                    {
                        var detail = line.Trim();
                        Report(uploadingPhase ? $"Uploading to Steam... {detail}" : $"Scanning content... {detail}",
                            uploadingPhase ? 0.6f + 0.35f * (percent / 100f) : 0.5f + 0.1f * (percent / 100f));
                    }

                    if (line.Contains("Successfully finished"))
                        Report("Upload complete!", 1f);
                }

                if (line.Contains("BuildID"))
                {
                    var match = Regex.Match(line, @"BuildID\s*(\d+)");
                    if (match.Success)
                    {
                        buildId = match.Groups[1].Value;
                        Debug.Log($"[SteamCMD] Build ID: {buildId}");
                    }
                }
            }

            // ─── Строки connection_log: только для распознавания Steam Guard ───

            void HandleConnectionLine(string line)
            {
                if (loginSuccess || string.IsNullOrEmpty(line)) return;

                if (line.Contains("Waiting for confirmation"))
                {
                    if (!mobileConfirmPending)
                        Debug.Log("[SteamCMD] Steam is waiting for confirmation in the Steam Mobile app");
                    mobileConfirmPending = true;
                    ShowGuardPrompt(GuardReason.MobileConfirm);
                }
                else if (line.Contains("has refresh token") && promptShown)
                {
                    SteamGuardCodePromptWindow.SetStatus("SteamCMD: подтверждение получено, входим…");
                }
            }

            void PumpLogs()
            {
                if (!tailActive) return;
                foreach (var line in consoleTail.ReadNewLines()) HandleConsoleLine(line);
                foreach (var line in connectionTail.ReadNewLines()) HandleConnectionLine(line);
            }

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

            // stdout/stderr: при живом хвосте логов они дублируют console_log и игнорируются;
            // без логов — единственный (буферизованный) источник.
            process.OutputDataReceived += (sender, e) =>
            {
                if (tailActive || string.IsNullOrEmpty(e.Data)) return;
                var data = e.Data;
                mainContext?.Post(_ => HandleConsoleLine(data), null);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                var line = SanitizeSteamCmdLine(e.Data);
                output.AppendLine($"ERROR: {line}");
                Debug.LogWarning($"[SteamCMD] {line}");
                if (IsSteamGuardPrompt(line))
                    steamGuardDetected = true;
            };

            process.Exited += (sender, e) =>
            {
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
                return (PublishResult.Fail($"Failed to start SteamCMD: {ex.Message}"), RunOutcome.Completed, null);
            }

            // ─── Тик чтения логов (главный поток через Unity-контекст) ───

            var tailDone = new TaskCompletionSource<bool>();

            async Task TailLoopAsync()
            {
                try
                {
                    while (!stopTail && !processExitedTcs.Task.IsCompleted)
                    {
                        PumpLogs();
                        await Task.Delay(LogPollMs);
                    }

                    // Хвост после выхода: последние строки могли дописаться позже
                    await Task.Delay(300);
                    PumpLogs();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SteamCMD] Log tail stopped: {ex.Message}");
                }
                finally
                {
                    tailDone.TrySetResult(true);
                }
            }

            _ = TailLoopAsync();

            // ─── Сторожа: кэш без входа → к паролю; пароль без событий → окно по таймеру ───

            if (kind == LoginKind.Cached)
            {
                async Task CachedWatchdogAsync()
                {
                    await Task.Delay(CachedLoginTimeoutSeconds * 1000);
                    if (!loginSuccess && !processExitedTcs.Task.IsCompleted)
                        cachedFailedTcs.TrySetResult(true);
                }

                _ = CachedWatchdogAsync();
            }
            else if (kind == LoginKind.Password)
            {
                async Task FallbackPromptAsync()
                {
                    await Task.Delay(SteamGuardFallbackSeconds * 1000);
                    if (!loginSuccess && !processExitedTcs.Task.IsCompleted)
                        ShowGuardPrompt(GuardReason.Timeout);
                }

                _ = FallbackPromptAsync();
            }

            // ─── Wait: выход процесса / глобальный таймаут / код из окна / провал кэша ───

            var timeoutTask = Task.Delay(OperationTimeout);
            var codeTask = codeEnteredTcs.Task;
            var cachedFailedTask = cachedFailedTcs.Task;

            async Task KillAsync()
            {
                stopTail = true;
                try { process.Kill(); }
                catch { /* already exited */ }
                await tailDone.Task;
                process.Dispose();
            }

            while (true)
            {
                var completedTask = await Task.WhenAny(processExitedTcs.Task, timeoutTask, codeTask, cachedFailedTask);

                if (completedTask == timeoutTask)
                {
                    await KillAsync();
                    return (PublishResult.Fail("Operation timed out (30 minutes)"), RunOutcome.Completed, null);
                }

                if (completedTask == cachedFailedTask && !processExitedTcs.Task.IsCompleted)
                {
                    cachedFailedTask = new TaskCompletionSource<bool>().Task;
                    if (loginSuccess) continue;   // успели войти — сторож опоздал

                    Debug.Log("[SteamCMD] Cached login did not complete — killing session");
                    await KillAsync();
                    return (null, RunOutcome.CachedLoginFailed, null);
                }

                if (completedTask == codeTask && !processExitedTcs.Task.IsCompleted)
                {
                    var enteredCode = codeTask.Result;
                    // Окно обработано — дальше ждём только процесс (иначе завершённый
                    // codeTask выигрывал бы WhenAny в вечном цикле).
                    codeTask = new TaskCompletionSource<string>().Task;

                    // Логин мог пройти в последние секунды (подтвердили на телефоне и тут же
                    // ввели код) — даём логам дописаться и проверяем ещё раз, чтобы не убить
                    // сессию, которая уже грузит билд.
                    if (!loginSuccess && !string.IsNullOrWhiteSpace(enteredCode))
                    {
                        await Task.Delay(1500);
                        PumpLogs();
                    }

                    if (loginSuccess)
                    {
                        Debug.Log("[SteamCMD] Login already completed — ignoring entered code, session continues");
                        continue;
                    }

                    await KillAsync();

                    if (string.IsNullOrWhiteSpace(enteredCode))
                        return (PublishResult.Fail("Upload cancelled — Steam Guard code not provided."),
                            RunOutcome.Completed, null);

                    // Перезапуск с кодом сделает RunSteamCmdAsync
                    return (null, RunOutcome.SteamGuardRequired, enteredCode.Trim());
                }

                if (processExitedTcs.Task.IsCompleted)
                    break;
            }

            var exitCode = await processExitedTcs.Task;
            await tailDone.Task;
            process.Dispose();

            // Окно могло остаться открытым, если процесс умер сам (например, exit code 5)
            if (promptShown && !loginSuccess)
                SteamGuardCodePromptWindow.CloseActiveQuietly();

            // ─── Interpret results ───

            if (kind == LoginKind.Cached && !loginSuccess)
                return (null, RunOutcome.CachedLoginFailed, null);

            // Steam Guard needed? (SteamCMD сам вышел, распознав Guard в выводе)
            if (kind == LoginKind.Password && !loginSuccess &&
                (steamGuardDetected || mobileConfirmPending || exitCode == 5))
            {
                return (null, RunOutcome.SteamGuardRequired, null);
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
                    var how = kind == LoginKind.Cached ? "cached session" : "password + Steam Guard";
                    return (PublishResult.Ok($"Authentication successful ({how})"), RunOutcome.Completed, null);
                }

                if (!success)
                    return (PublishResult.Fail(ExtractError(combined)), RunOutcome.Completed, null);

                var message = string.IsNullOrEmpty(buildId)
                    ? "Successfully completed Steam operation"
                    : $"Successfully uploaded to Steam (Build ID: {buildId})";
                return (PublishResult.Ok(message, buildId), RunOutcome.Completed, null);
            }

            return (PublishResult.Fail(ExtractError(combined)), RunOutcome.Completed, null);
        }

        // ═══════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════

        private static bool IsSteamGuardPrompt(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;

            var lower = line.ToLowerInvariant();
            return lower.Contains("steam guard code") ||
                   lower.Contains("not been authenticated for your account") ||
                   lower.Contains("two-factor") ||
                   lower.Contains("2fa") ||
                   lower.Contains("enter the current code") ||
                   lower.Contains("two factor code") ||
                   lower.Contains("auth code") ||
                   lower.Contains("please check your email") ||
                   lower.Contains("enter the code");
        }

        /// <summary>Строка SteamCMD, означающая провал входа (до успешного логина).</summary>
        private static bool IsLoginFailure(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;

            var lower = line.ToLowerInvariant().Trim();
            return lower.StartsWith("failed") ||
                   lower.Contains("login failure") ||
                   lower.Contains("invalid password") ||
                   lower.Contains("expired login token") ||
                   lower.Contains("cached credentials not found") ||
                   lower.Contains("rate limit") ||
                   lower.Contains("logon denied") ||
                   lower.EndsWith("password:");   // просит пароль — кэша нет
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
                if (ch >= '─' && ch <= '▟')
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
            if (output.Contains("Steam Guard") && !output.Contains("Waiting for client config"))
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
