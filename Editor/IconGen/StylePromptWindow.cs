using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ProtoSystem.IconGen.Editor;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Отдельное окно для полного чтения/правки позитив-шаблона и негатива стиль-пресета —
    /// в левой панели студии поля тесные. Пишет в тот же ассет стиля (SetDirty).
    /// </summary>
    public class StylePromptWindow : EditorWindow
    {
        private IconStylePreset _style;

        public static void Open(IconStylePreset style)
        {
            var w = GetWindow<StylePromptWindow>(true, "Промпт стиля");
            w._style = style;
            w.minSize = new Vector2(520, 420);
            w.Rebuild();
        }

        private void Rebuild()
        {
            rootVisualElement.Clear();
            if (_style == null) { rootVisualElement.Add(new Label("Стиль не задан.")); return; }

            var root = rootVisualElement;
            root.style.paddingTop = 8; root.style.paddingBottom = 8;
            root.style.paddingLeft = 8; root.style.paddingRight = 8;

            var head = new Label($"Стиль: {_style.name}");
            head.style.unityFontStyleAndWeight = FontStyle.Bold;
            head.style.marginBottom = 6;
            root.Add(head);

            root.Add(BigField("Позитив-шаблон ({subject} = промпт сущности)",
                _style.positiveTemplate, 200, v =>
                {
                    Undo.RecordObject(_style, "Style positive");
                    _style.positiveTemplate = v;
                    EditorUtility.SetDirty(_style); AssetDatabase.SaveAssetIfDirty(_style);
                }));

            root.Add(BigField("Негатив (что НЕ рисовать)", _style.negative, 200, v =>
                {
                    Undo.RecordObject(_style, "Style negative");
                    _style.negative = v;
                    EditorUtility.SetDirty(_style); AssetDatabase.SaveAssetIfDirty(_style);
                }));
        }

        private static VisualElement BigField(string label, string value, int height, System.Action<string> onChange)
        {
            var wrap = new VisualElement { style = { marginBottom = 10 } };
            wrap.Add(new Label(label) { style = { marginBottom = 2 } });
            var tf = new TextField { value = value, multiline = true };
            tf.style.minHeight = height;
            tf.style.whiteSpace = WhiteSpace.Normal;
            var input = tf.Q(className: "unity-text-field__input");
            if (input != null) input.style.whiteSpace = WhiteSpace.Normal;
            tf.RegisterValueChangedCallback(e => onChange(e.newValue));
            wrap.Add(tf);
            return wrap;
        }
    }
}
