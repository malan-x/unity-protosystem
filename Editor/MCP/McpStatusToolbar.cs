// Кнопка состояния MCP-моста в главном тулбаре, рядом с TODO.
//
// Живёт в ОТДЕЛЬНОЙ сборке ProtoSystem.Editor.MCP: она компилируется только когда в проекте
// есть пакет com.coplaydev.unity-mcp (versionDefines -> PROTOSYSTEM_MCP + defineConstraints).
// Нет пакета — нет сборки; ProtoSystem собирается как раньше, ничего не тянет за собой.
//
// Зелёная — мост поднят, серая — нет. Клик: поднять мост (или остановить, если уже запущен).

#if UNITY_6000_7_OR_NEWER

using System.Reflection;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.Editor.MCP
{
    [InitializeOnLoad]
    public static class McpStatusToolbar
    {
        private const string ElementId = "ProtoSystem/McpStatus";
        private const string UssName   = "protosystem-mcp-button";

        private static readonly Color Online  = new(0.30f, 0.69f, 0.31f);   // мост поднят
        private static readonly Color Offline = new(0.32f, 0.32f, 0.32f);   // мост лежит

        private static bool _lastKnownRunning;

        static McpStatusToolbar()
        {
            // Unity добавляет новые элементы тулбара СКРЫТЫМИ — включаем при первом появлении
            EditorApplication.delayCall += () => ToolbarVisibility.EnsureShownOnce(ElementId);
            EditorApplication.delayCall += Restyle;

            // ВАЖНО: НИКАКОГО опроса моста на EditorApplication.update.
            // Bridge.IsRunning лезет в транспорт, а тот сам обрабатывает очередь команд MCP —
            // опрос каждый тик вставал в клинч с TransportCommandDispatcher.ProcessQueue
            // и вешал редактор («Waiting for user code in MCPForUnity.Editor.dll»).
            // Состояние читаем только когда элемент создаётся и по клику.
        }

        [MainToolbarElement(ElementId,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = -1,
            ussName = UssName)]
        private static MainToolbarElement CreateElement()
        {
            bool running = IsBridgeRunning();
            _lastKnownRunning = running;

            var content = new MainToolbarContent(
                running ? "MCP ●" : "MCP ○",
                running
                    ? $"MCP-мост работает (порт {CurrentPort()}). Клик — остановить."
                    : "MCP-мост не запущен. Клик — запустить.");

            var button = new MainToolbarButton(content, ToggleBridge);
            EditorApplication.delayCall += Restyle;
            return button;
        }

        /// <summary>
        /// Клик: мост лежит — поднимаем; мост работает — просто обновляем состояние кнопки
        /// (останавливать по случайному клику нельзя: оборвём Claude Code посреди работы —
        /// для остановки есть окно MCP for Unity).
        /// </summary>
        private static void ToggleBridge()
        {
            var bridge = MCPServiceLocator.Bridge;
            if (bridge == null) return;

            if (!IsBridgeRunning())
                _ = bridge.StartAsync();

            // Состояние меняется асинхронно — обновим подпись, когда оно устаканится
            EditorApplication.delayCall += () =>
            {
                _lastKnownRunning = IsBridgeRunning();
                MainToolbar.Refresh(ElementId);
                EditorApplication.delayCall += Restyle;
            };
        }

        private static bool IsBridgeRunning()
        {
            try { return MCPServiceLocator.Bridge?.IsRunning ?? false; }
            catch { return false; }   // сервис может быть не готов на старте домена
        }

        private static int CurrentPort()
        {
            try { return MCPServiceLocator.Bridge?.CurrentPort ?? 0; }
            catch { return 0; }
        }

        /// <summary>
        /// MainToolbarButton не даёт доступа к своему VisualElement — находим кнопку в окне
        /// тулбара и красим инлайн (та же механика, что у TODO-кнопки).
        /// </summary>
        private static void Restyle()
        {
            var windowProp = typeof(MainToolbar).GetProperty("window",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            if (windowProp?.GetValue(null) is not EditorWindow window) return;

            var root = window.rootVisualElement;
            var button = root?.Q(className: UssName) ?? root?.Q(name: UssName);
            if (button == null) return;

            button.style.backgroundColor = _lastKnownRunning ? Online : Offline;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;
            button.style.paddingLeft = 7;
            button.style.paddingRight = 7;
            button.style.marginLeft = 2;
            button.style.marginRight = 2;
            button.style.alignSelf = Align.Center;
            button.style.color = Color.white;
            button.style.fontSize = 12;

            button.Query<TextElement>().ForEach(label =>
            {
                label.style.color = Color.white;
                label.style.fontSize = 12;
            });
        }
    }
}

#endif
