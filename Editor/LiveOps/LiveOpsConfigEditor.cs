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

        private static readonly Color ColorOk   = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color ColorFail = new Color(0.8f, 0.3f, 0.2f);
        private static readonly Color ColorGray = new Color(0.6f, 0.6f, 0.6f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            DrawConnectionSection();
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
    }
}
