// Полуавтоматическая установка MCP for Unity (Claude Code управляет редактором).
//
// Живёт в ОСНОВНОЙ Editor-сборке и НЕ ссылается на MCP-пакет: этот код должен работать
// именно тогда, когда пакета ещё нет. Сама интеграция (кнопка состояния моста в тулбаре)
// лежит в отдельной сборке ProtoSystem.Editor.MCP и компилируется только при его наличии.
//
// Почему MCP не в dependencies пакета: он требует Python 3.10+ и uv, установленные В СИСТЕМЕ
// (менеджер пакетов Unity их не поставит), и шлёт телеметрию. Навязывать это каждому проекту
// на ProtoSystem нельзя — ставится по кнопке, осознанно.

using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProtoSystem.Editor
{
    public static class McpSetup
    {
        public const string PackageName = "com.coplaydev.unity-mcp";

        /// <summary>
        /// Наш форк (malan-x/unity-mcp, корень репозитория = пакет): v10.0.1 делает ключ
        /// порта HTTP-сервера пер-проектным — два открытых проекта больше не делят один
        /// сервер и не перетирают настройку друг друга (у upstream ключ глобальный).
        /// Пин на ТЕГ, а не на main: обновление версии — осознанный шаг.
        /// </summary>
        private const string PackageUrl =
            "https://github.com/malan-x/unity-mcp.git#v10.0.1";

        /// <summary>
        /// Пер-проектный HTTP-порт: 8080 + сдвиг из SHA1 пути проекта (8080–8179).
        /// Детерминирован — у проекта всегда один и тот же порт, у разных проектов
        /// (почти наверняка) разные. Тот же порт пишется и в .mcp.json, и в
        /// пер-проектный ключ EditorPrefs форка — сервер и Claude сходятся сами.
        /// </summary>
        public static int ProjectPort => 8080 + ProjectHashByte() % 100;

        public static string ProjectHttpUrl => $"http://127.0.0.1:{ProjectPort}";

        private static string ClaudeConfigJson =>
            "{\n" +
            "  \"mcpServers\": {\n" +
            "    \"unity-mcp\": {\n" +
            "      \"type\": \"http\",\n" +
            $"      \"url\": \"{ProjectHttpUrl}/mcp\"\n" +
            "    }\n" +
            "  }\n" +
            "}\n";

        // Ключ форка: "MCPForUnity.HttpUrl." + 8 hex-символов SHA1(dataPath) —
        // ДОЛЖЕН совпадать с EditorPrefKeys.HttpBaseUrl форка (см. FORK.md пакета).
        private static string ForkHttpUrlPrefKey => "MCPForUnity.HttpUrl." + ProjectHashHex8();

        private static byte ProjectHashByte()
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            return sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Application.dataPath ?? ""))[0];
        }

        private static string ProjectHashHex8()
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hash = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Application.dataPath ?? ""));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 4; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        private static AddRequest _addRequest;

        public static bool IsInstalled =>
            File.Exists(Path.Combine("Packages", "manifest.json")) &&
            File.ReadAllText(Path.Combine("Packages", "manifest.json")).Contains(PackageName);

        [MenuItem("ProtoSystem/MCP for Unity/Установить (Claude Code управляет редактором)", priority = 400)]
        public static void Install()
        {
            if (IsInstalled)
            {
                EditorUtility.DisplayDialog("MCP for Unity",
                    "Пакет уже установлен.\n\nСостояние моста — кнопка MCP в главном тулбаре, " +
                    "рядом с TODO.", "Ок");
                return;
            }

            if (!EditorUtility.DisplayDialog("Установить MCP for Unity?",
                    "Claude Code сможет управлять редактором: читать консоль, править сцену и префабы, " +
                    "запускать пункты меню.\n\n" +
                    "ТРЕБОВАНИЯ (ставятся в системе, не через Unity):\n" +
                    "  • Python 3.10+\n" +
                    "  • uv (менеджер окружений Astral)\n\n" +
                    "Пакет разворачивает локальный Python-сервер и шлёт телеметрию разработчику " +
                    "(CoplayDev). Ставится наш форк v10.0.1 (пер-проектный порт сервера).",
                    "Установить", "Отмена"))
                return;

            _addRequest = Client.Add(PackageUrl);
            EditorApplication.update += TrackInstall;
        }

        private static void TrackInstall()
        {
            if (_addRequest == null || !_addRequest.IsCompleted) return;

            EditorApplication.update -= TrackInstall;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[ProtoSystem] MCP for Unity установлен: {_addRequest.Result.packageId}");
                WriteClaudeConfig();

                EditorUtility.DisplayDialog("MCP for Unity установлен",
                    "Готово.\n\n" +
                    "1. Файл .mcp.json создан в корне проекта — Claude Code подхватит сервер " +
                    "при следующем запуске сессии.\n" +
                    "2. Кнопка MCP появится в главном тулбаре ПОСЛЕ перезапуска Unity " +
                    "(набор элементов тулбара кэшируется при старте).", "Ок");
            }
            else
            {
                Debug.LogError($"[ProtoSystem] Не удалось установить MCP for Unity: {_addRequest.Error?.message}");
            }

            _addRequest = null;
        }

        /// <summary>
        /// Регистрирует сервер в Claude Code. Окно самого пакета этого не делает для CLI —
        /// приходится писать .mcp.json руками. Транспорт именно http: мост пакета слушает
        /// 127.0.0.1:8080, а stdio-вариант ищет Unity по TCP и не находит.
        /// </summary>
        [MenuItem("ProtoSystem/MCP for Unity/Зарегистрировать сервер в Claude Code", priority = 401)]
        public static void WriteClaudeConfig()
        {
            const string path = ".mcp.json";

            if (File.Exists(path) && File.ReadAllText(path).Contains("unity-mcp"))
            {
                Debug.Log("[ProtoSystem] .mcp.json уже содержит unity-mcp — не трогаю.");
                return;
            }

            if (File.Exists(path) &&
                !EditorUtility.DisplayDialog(".mcp.json уже существует",
                    "В корне проекта есть .mcp.json с другими серверами. Перезаписать его " +
                    "конфигурацией unity-mcp?", "Перезаписать", "Отмена"))
                return;

            File.WriteAllText(path, ClaudeConfigJson);
            EditorPrefs.SetString(ForkHttpUrlPrefKey, ProjectHttpUrl);
            Debug.Log($"[ProtoSystem] Создан .mcp.json (порт {ProjectPort}) — Claude Code подхватит " +
                      "unity-mcp при старте сессии.");
        }

        /// <summary>
        /// Миграция на пер-проектный порт для УЖЕ настроенных проектов: перезаписывает
        /// url в .mcp.json и ставит пер-проектный ключ EditorPrefs форка. После —
        /// перезапустить редактор (сервер) и Claude-сессию.
        /// </summary>
        [MenuItem("ProtoSystem/MCP for Unity/Перевести на пер-проектный порт (мульти-проект)", priority = 402)]
        public static void MigrateToProjectPort()
        {
            const string path = ".mcp.json";
            if (!EditorUtility.DisplayDialog("Пер-проектный порт MCP",
                    $"Порт этого проекта: {ProjectPort} (выводится из пути проекта, стабилен).\n\n" +
                    "Будут обновлены .mcp.json и настройка сервера. Требуется форк пакета " +
                    "v10.0.1+ (ProtoSystem ставит именно его).\n\n" +
                    "После — перезапустите Unity и Claude-сессию.",
                    "Перевести", "Отмена"))
                return;

            File.WriteAllText(path, ClaudeConfigJson);
            EditorPrefs.SetString(ForkHttpUrlPrefKey, ProjectHttpUrl);
            Debug.Log($"[ProtoSystem] Проект переведён на порт {ProjectPort}: .mcp.json и " +
                      $"настройка сервера ({ForkHttpUrlPrefKey}) обновлены.");
        }

        [MenuItem("ProtoSystem/MCP for Unity/Установить (Claude Code управляет редактором)", true)]
        private static bool InstallValidate() => !IsInstalled;
    }
}
