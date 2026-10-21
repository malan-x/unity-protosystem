using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Мини-окошко ввода произвольного размера рендишена. EditorUtility даёт диалоги
    /// только с фиксированными кнопками — а нам нужен любой размер, который попросит
    /// окно-потребитель, поэтому своё поле ввода.
    /// </summary>
    public class RenditionSizePrompt : EditorWindow
    {
        private Action<int> _onOk;

        public static void Show(Action<int> onOk)
        {
            var win = CreateInstance<RenditionSizePrompt>();
            win._onOk = onOk;
            win.titleContent = new GUIContent("Размер рендишена");
            win.minSize = win.maxSize = new Vector2(240, 76);
            // По центру курсора, модально
            var pos = GUIUtility.GUIToScreenPoint(Event.current?.mousePosition ?? Vector2.zero);
            win.position = new Rect(pos.x, pos.y, 240, 76);
            win.ShowModalUtility();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 6;

            var field = new IntegerField("Размер, px") { value = 32 };
            field.style.marginBottom = 6;
            root.Add(field);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };
            buttons.Add(new Button(Close) { text = "Отмена" });
            buttons.Add(new Button(() =>
            {
                if (field.value > 0) _onOk?.Invoke(field.value);
                Close();
            }) { text = "OK" });
            root.Add(buttons);

            // Enter — подтвердить
            root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    if (field.value > 0) _onOk?.Invoke(field.value);
                    Close();
                }
                else if (e.keyCode == KeyCode.Escape) Close();
            });
            field.Focus();
        }
    }
}
