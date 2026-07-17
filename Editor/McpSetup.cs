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
        /// Версия закреплена (пин на тег/PyPI-версию, не на main): пакет активно развивается,
        /// и произвольный коммит способен сломать сборку. Обновление — осознанный шаг;
        /// Unity-пакет и Python-сервер обновляются ВМЕСТЕ (одна константа).
        /// </summary>
        private const string McpVersion = "10.0.0";

        private const string PackageUrl =
            "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v" + McpVersion;

        /// <summary>
        /// Конфиг MCP-сервера для Claude Code — транспорт STDIO: каждая сессия поднимает
        /// СВОЙ Python-сервер (uvx с PyPI), который находит Unity этого проекта по
        /// статус-файлам ~/.unity-mcp (порт 6400+ на проект). В отличие от http-варианта
        /// с общим сервером на фиксированном порту, несколько одновременно открытых
        /// проектов изолированы из коробки. Требование: в окне MCP For Unity транспорт
        /// тоже должен быть Stdio — WriteClaudeConfig переключает его сам.
        /// </summary>
        private const string ClaudeConfigJson =
            "{\n" +
            "  \"mcpServers\": {\n" +
            "    \"unity-mcp\": {\n" +
            "      \"type\": \"stdio\",\n" +
            "      \"command\": \"uvx\",\n" +
            "      \"args\": [\"--from\", \"mcpforunityserver==" + McpVersion + "\", \"mcp-for-unity\"]\n" +
            "    }\n" +
            "  }\n" +
            "}\n";

        // Ключ пакета MCP (строкой — McpSetup сознательно не ссылается на его сборки):
        // false = транспорт Stdio в окне MCP For Unity. Ключ глобальный для пользователя,
        // но для stdio это и нужно — все проекты в одном режиме.
        private const string UseHttpTransportPrefKey = "MCPForUnity.UseHttpTransport";

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
                    "(CoplayDev). Ставится версия v" + McpVersion + ", транспорт stdio — " +
                    "у каждого проекта свой сервер, несколько открытых проектов не конфликтуют.",
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
        /// Регистрирует сервер в Claude Code (.mcp.json в корне проекта) и переключает
        /// мост пакета на stdio. Окно самого пакета для CLI этого не делает — пишем руками.
        /// Старый http-конфиг (общий сервер на 8080) переписывается с подтверждением.
        /// </summary>
        [MenuItem("ProtoSystem/MCP for Unity/Зарегистрировать сервер в Claude Code (stdio)", priority = 401)]
        public static void WriteClaudeConfig()
        {
            const string path = ".mcp.json";
            string existing = File.Exists(path) ? File.ReadAllText(path) : "";

            bool hasStdio = existing.Contains("unity-mcp") && existing.Contains("\"stdio\"");
            if (hasStdio)
            {
                Debug.Log("[ProtoSystem] .mcp.json уже содержит stdio-конфиг unity-mcp — не трогаю.");
            }
            else if (existing.Length > 0 &&
                !EditorUtility.DisplayDialog(".mcp.json уже существует",
                    existing.Contains("unity-mcp")
                        ? "В .mcp.json старая (http) регистрация unity-mcp. Переписать на stdio — " +
                          "у каждого проекта будет свой сервер, без конфликтов портов?"
                        : "В корне проекта есть .mcp.json с другими серверами. Перезаписать его " +
                          "конфигурацией unity-mcp?",
                    "Переписать", "Отмена"))
            {
                return;
            }
            else
            {
                File.WriteAllText(path, ClaudeConfigJson);
                Debug.Log("[ProtoSystem] Создан .mcp.json (stdio) — Claude Code подхватит unity-mcp " +
                          "при старте сессии.");
            }

            // Мост редактора — тоже в stdio, иначе сервер сессии не найдёт Unity
            if (EditorPrefs.GetBool(UseHttpTransportPrefKey, true))
            {
                EditorPrefs.SetBool(UseHttpTransportPrefKey, false);
                Debug.Log("[ProtoSystem] Транспорт MCP For Unity переключён на Stdio — " +
                          "перезапустите Unity, чтобы мост пересоздался.");
            }
        }

        [MenuItem("ProtoSystem/MCP for Unity/Установить (Claude Code управляет редактором)", true)]
        private static bool InstallValidate() => !IsInstalled;
    }
}
