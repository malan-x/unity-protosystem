using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Окно очереди генерации: что рендерится сейчас, что ждёт, отмена всего или
    /// отдельных запросов. Текущий рендер точечно не прерывается (ComfyUI уже занят) —
    /// его снимает только «Отменить всё» (докатывает текущий и останавливается).
    /// Открывается кликом по счётчику очереди в студии или из меню Window.
    /// </summary>
    public class IconGenQueueWindow : EditorWindow
    {
        [MenuItem("Window/ProtoSystem/Очередь генерации иконок")]
        public static void Open()
        {
            var win = GetWindow<IconGenQueueWindow>("Очередь генерации");
            win.minSize = new Vector2(320, 200);
            win.Show();
        }

        private ScrollView _list;

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 6;

            var header = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 },
            };
            var title = new Label("Очередь генерации")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 },
            };
            header.Add(title);
            header.Add(new Button(AiIconGenerator.CancelAll)
            {
                text = "Отменить всё",
                tooltip = "Докатить текущий рендер и остановиться, очередь очищается",
            });
            root.Add(header);

            _list = new ScrollView();
            root.Add(_list);

            AiIconGenerator.QueueChanged += Rebuild;
            Rebuild();
        }

        private void OnDisable() => AiIconGenerator.QueueChanged -= Rebuild;

        private void Rebuild()
        {
            if (_list == null) return;
            _list.Clear();

            if (!AiIconGenerator.IsRunning)
            {
                _list.Add(new Label("Очередь пуста.")
                {
                    style = { color = new Color(0.6f, 0.6f, 0.6f), marginTop = 4 },
                });
                return;
            }

            // Текущий рендер — без точечной отмены
            if (!string.IsNullOrEmpty(AiIconGenerator.CurrentLabel))
            {
                var current = MakeRow(
                    $"⏳ {AiIconGenerator.CurrentLabel}",
                    $"{AiIconGenerator.Done + 1}/{AiIconGenerator.Total}",
                    cancel: null);
                current.style.unityFontStyleAndWeight = FontStyle.Bold;
                _list.Add(current);
            }

            var pending = AiIconGenerator.PendingRequests;
            for (int i = 0; i < pending.Count; i++)
            {
                int index = i;   // замыкание на позицию в очереди на момент клика
                string label = string.IsNullOrEmpty(pending[i].DisplayName) ? "иконка" : pending[i].DisplayName;
                _list.Add(MakeRow($"{i + 1}. {label}", "", () => AiIconGenerator.CancelAt(index)));
            }
        }

        private static VisualElement MakeRow(string text, string note, System.Action cancel)
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 },
            };
            row.Add(new Label(text) { style = { flexGrow = 1 } });
            if (!string.IsNullOrEmpty(note))
                row.Add(new Label(note) { style = { color = new Color(0.6f, 0.6f, 0.6f), marginRight = 4 } });
            if (cancel != null)
                row.Add(new Button(cancel) { text = "✕", tooltip = "Убрать из очереди" });
            return row;
        }
    }
}
