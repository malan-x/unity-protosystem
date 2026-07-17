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
        /// Пин на ТЕГ, а не на main: пакет активно развивается, и произвольный коммит
        /// из main способен сломать сборку проекта. Обновление версии — осознанный шаг.
        /// </summary>
        private const string PackageUrl =
            "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.0.0";

        /// <summary>Конфиг MCP-сервера для Claude Code. Мост пакета слушает HTTP на 8080.</summary>
        private const string ClaudeConfigJson =
            "{\n" +
            "  \"mcpServers\": {\n" +
            "    \"unity-mcp\": {\n" +
            "      \"type\": \"http\",\n" +
            "      \"url\": \"http://127.0.0.1:8080/mcp\"\n" +
            "    }\n" +
            "  }\n" +
            "}\n";

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
                    "(CoplayDev). Ставится версия v10.0.0.",
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
            Debug.Log("[ProtoSystem] Создан .mcp.json — Claude Code подхватит unity-mcp при старте сессии.");
        }

        [MenuItem("ProtoSystem/MCP for Unity/Установить (Claude Code управляет редактором)", true)]
        private static bool InstallValidate() => !IsInstalled;
    }
}
