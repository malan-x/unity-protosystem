// Packages/com.protosystem.core/Editor/LiveOps/LiveOpsConfigEditor.cs
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ProtoSystem.LiveOps
{
    [CustomEditor(typeof(LiveOpsConfig))]
    public class LiveOpsConfigEditor : UnityEditor.Editor
    {
        // Состояния проверки
        private enum CheckState { Idle, Checking, Ok, Fail }

        private CheckState _state       = CheckState.Idle;
        private string     _statusMsg   = "";
        private double     _lastCheckAt = 0;

        // Debug
        private string _debugSteamName = "";
        private int    _debugRatingScore = 5;
        private string _ratingStatus = "";
        private bool   _ratingBusy;

        private static readonly Color ColorOk   = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color ColorFail = new Color(0.8f, 0.3f, 0.2f);
        private static readonly Color ColorGray = new Color(0.6f, 0.6f, 0.6f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            DrawConnectionSection();

            EditorGUILayout.Space(8);
            DrawDebugSection();
        }

        // ── Секция проверки подключения ────────────────────────────────────

        private void DrawConnectionSection()
        {
            var config = (LiveOpsConfig)target;

            EditorGUILayout.LabelField("Подключение", EditorStyles.boldLabel);

            bool hasUrl = !string.IsNullOrWhiteSpace(config.serverUrl);

            // Статус
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Статус:", GUILayout.Width(60));

            var prevColor = GUI.color;
            switch (_state)
            {
                case CheckState.Ok:
                    GUI.color = ColorOk;
                    EditorGUILayout.LabelField($"✓ {_statusMsg}", EditorStyles.boldLabel);
                    break;
                case CheckState.Fail:
                    GUI.color = ColorFail;
                    EditorGUILayout.LabelField($"✗ {_statusMsg}", EditorStyles.boldLabel);
                    break;
                case CheckState.Checking:
                    GUI.color = ColorGray;
                    EditorGUILayout.LabelField("⏳ Проверяю...", EditorStyles.boldLabel);
                    break;
                default:
                    GUI.color = ColorGray;
                    EditorGUILayout.LabelField("— не проверялось", EditorStyles.miniLabel);
                    break;
            }
            GUI.color = prevColor;
            EditorGUILayout.EndHorizontal();

            // Время последней проверки
            if (_state != CheckState.Idle)
            {
                double elapsed = EditorApplication.timeSinceStartup - _lastCheckAt;
                string ago = elapsed < 5   ? "только что"
                           : elapsed < 60  ? $"{(int)elapsed} сек. назад"
                           : elapsed < 3600 ? $"{(int)(elapsed / 60)} мин. назад"
                           : $"{(int)(elapsed / 3600)} ч. назад";

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("", GUILayout.Width(60));
                EditorGUILayout.LabelField(ago, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);

            // Кнопки
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = hasUrl && _state != CheckState.Checking;

            if (GUILayout.Button("🌐 Проверить доступность", GUILayout.Height(24)))
                _ = RunCheckAsync(config, ping: true);

            if (Application.isPlaying)
            {
                if (GUILayout.Button("📥 Fetch данных", GUILayout.Height(24), GUILayout.Width(120)))
                    _ = RunCheckAsync(config, ping: false);
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (!hasUrl)
                EditorGUILayout.HelpBox("Укажите Server Url в конфиге.", MessageType.Warning);

            // Оффлайн-подсказка
            if (!Application.isPlaying)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(
                    "Ping работает в Edit Mode и Play Mode.\n" +
                    "Fetch данных — только в Play Mode (нужен инициализированный LiveOpsSystem).",
                    MessageType.Info);
            }
        }

        // ── Асинхронная проверка ───────────────────────────────────────────

        private async Task RunCheckAsync(LiveOpsConfig config, bool ping)
        {
            _state     = CheckState.Checking;
            _statusMsg = "";
            _lastCheckAt = EditorApplication.timeSinceStartup;
            Repaint();

            if (ping)
            {
                await PingServerAsync(config);
            }
            else
            {
                await FetchViaSystemAsync(config);
            }

            _lastCheckAt = EditorApplication.timeSinceStartup;
            Repaint();
        }

        /// <summary>HEAD-запрос к /api/health (или корню). Работает в любом режиме.</summary>
        private async Task PingServerAsync(LiveOpsConfig config)
        {
            string url = config.serverUrl.TrimEnd('/') + "/api/health";

            using var req = UnityWebRequest.Head(url);
            req.timeout = Mathf.CeilToInt(config.healthCheckTimeoutSeconds);

            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success ||
                req.responseCode is >= 200 and < 500)
            {
                _state     = CheckState.Ok;
                _statusMsg = $"Сервер доступен ({req.responseCode})";
            }
            else
            {
                _state     = CheckState.Fail;
                _statusMsg = $"{req.error} ({req.responseCode})";
            }
        }

        /// <summary>Fetch через LiveOpsSystem. Только в Play Mode.</summary>
        private async Task FetchViaSystemAsync(LiveOpsConfig config)
        {
            if (!Application.isPlaying)
            {
                _state     = CheckState.Fail;
                _statusMsg = "Только в Play Mode";
                return;
            }

            // Ищем LiveOpsSystem в сцене
            var system = Object.FindFirstObjectByType<LiveOpsSystem>();
            if (system == null)
            {
                _state     = CheckState.Fail;
                _statusMsg = "LiveOpsSystem не найден в сцене";
                return;
            }

            try
            {
                await system.FetchAsync();
                int msgs   = system.Messages?.Count ?? 0;
                int polls  = system.Polls?.Count ?? 0;
                _state     = CheckState.Ok;
                _statusMsg = $"OK — сообщений: {msgs}, опросов: {polls}";
            }
            catch (System.Exception ex)
            {
                _state     = CheckState.Fail;
                _statusMsg = ex.Message;
            }
        }

        // ── Debug-секция ─────────────────────────────────────────────────

        private void DrawDebugSection()
        {
            EditorGUILayout.LabelField("Debug (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Debug-инструменты доступны только в Play Mode.", MessageType.Info);
                return;
            }

            var system = Object.FindFirstObjectByType<LiveOpsSystem>();
            if (system == null)
            {
                EditorGUILayout.HelpBox("LiveOpsSystem не найден в сцене.", MessageType.Warning);
                return;
            }

            // ── Steam Nickname ──────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            _debugSteamName = EditorGUILayout.TextField("Steam Nickname", _debugSteamName);
            GUI.enabled = !string.IsNullOrWhiteSpace(_debugSteamName);
            if (GUILayout.Button("Set", GUILayout.Width(40)))
            {
                system.SetPlayerId(_debugSteamName);
                Debug.Log($"[LiveOps Debug] PlayerId → {_debugSteamName}");
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Current ID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Current ID");
            EditorGUILayout.SelectableLabel(system.PlayerId ?? "—",
                EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── Rating ──────────────────────────────────────────────────
            EditorGUILayout.LabelField("Rating", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            _debugRatingScore = EditorGUILayout.IntSlider(_debugRatingScore, 1, 10);
            GUI.enabled = !_ratingBusy;
            if (GUILayout.Button($"Send {_debugRatingScore}", GUILayout.Width(70), GUILayout.Height(20)))
                _ = SubmitRatingDebugAsync(system, _debugRatingScore);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Quick buttons 1–10
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !_ratingBusy;
            for (int i = 1; i <= 10; i++)
            {
                int score = i;
                if (GUILayout.Button(score.ToString(), GUILayout.Height(22)))
                    _ = SubmitRatingDebugAsync(system, score);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_ratingStatus))
                EditorGUILayout.HelpBox(_ratingStatus, MessageType.Info);
        }

        private async Task SubmitRatingDebugAsync(LiveOpsSystem system, int score)
        {
            _ratingBusy = true;
            _ratingStatus = $"Отправка оценки {score}...";
            Repaint();

            // ── Диагностика перед отправкой ──────────────────────────────
            var config = (LiveOpsConfig)target;
            Debug.Log($"[LiveOps Debug] === SubmitRating({score}) ===");
            Debug.Log($"[LiveOps Debug] config null: {config == null}");
            if (config != null)
            {
                Debug.Log($"[LiveOps Debug] serverUrl: '{config.serverUrl}'");
                Debug.Log($"[LiveOps Debug] projectId: '{config.projectId}'");
                Debug.Log($"[LiveOps Debug] enableRating: {config.enableRating}");
                Debug.Log($"[LiveOps Debug] provider from config: {(config.GetProvider() == null ? "NULL" : config.GetProvider().GetType().Name)}");
            }

            // Проверяем _provider в системе через reflection
            var providerField = typeof(LiveOpsSystem).GetField("_provider",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var provider = providerField?.GetValue(system);
            Debug.Log($"[LiveOps Debug] system._provider: {(provider == null ? "NULL" : provider.GetType().Name)}");
            Debug.Log($"[LiveOps Debug] system.PlayerId: '{system.PlayerId}'");
            Debug.Log($"[LiveOps Debug] Application.version: '{Application.version}'");

            try
            {
                var result = await system.SubmitRatingAsync(score);
                if (result != null)
                {
                    Debug.Log($"[LiveOps Debug] ✓ result.ok={result.ok}, avg={result.avg}, count={result.count}");
                    _ratingStatus = $"✓ Оценка {score} — avg: {result.avg:F2}, count: {result.count}";
                }
                else
                {
                    Debug.LogWarning($"[LiveOps Debug] ✗ result = null (см. логи выше для root cause)");
                    _ratingStatus = "✗ result = null (см. Console)";
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LiveOps Debug] Exception: {ex}");
                _ratingStatus = $"✗ {ex.Message}";
            }

            _ratingBusy = false;
            Repaint();
        }
    }
}
