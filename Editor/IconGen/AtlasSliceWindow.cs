using UnityEditor;
using UnityEngine;
using ProtoSystem.IconGen.Editor;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Окно настроек нарезки атласа: метод (по границам объектов / фиксированная сетка),
    /// число колонок для сетки, вырезать ли фон rembg (если фон нужен — снять галку).
    /// Держит взятую из буфера картинку; по «Нарезать» зовёт AtlasTools.Slice.
    /// </summary>
    public class AtlasSliceWindow : EditorWindow
    {
        private IconCollection _col;
        private Texture2D _atlas;
        private int _expected;
        private int _foundSmart;

        private bool _smart = true;
        private int _columns = 4;
        private AtlasTools.BgMode _bgMode = AtlasTools.BgMode.EdgeFlood;
        private int _edgeThreshold = 24;
        private int _rembgModelIdx = 0;

        public static void Open(IconCollection col, Texture2D atlas)
        {
            var win = CreateInstance<AtlasSliceWindow>();
            win._col = col;
            win._atlas = atlas;
            win._expected = col.GetItems?.Invoke()?.Count ?? 0;
            win._foundSmart = AtlasTools.CountSmart(atlas);
            win._smart = win._foundSmart == win._expected;   // авто, если совпало
            win._columns = AtlasTools.DefaultColumns(win._expected);
            win.titleContent = new GUIContent("Нарезка атласа");
            win.minSize = new Vector2(380, 420);
            win.ShowUtility();
        }

        private void OnDisable()
        {
            if (_atlas != null) { DestroyImmediate(_atlas); _atlas = null; }
        }

        private void OnGUI()
        {
            if (_atlas == null) { Close(); return; }

            EditorGUILayout.LabelField($"«{_col.Title}» — {_expected} сущностей",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Картинка: {_atlas.width}×{_atlas.height}");
            EditorGUILayout.Space(4);

            // Превью атласа
            float aspect = _atlas.height > 0 ? _atlas.width / (float)_atlas.height : 1f;
            float pw = Mathf.Min(position.width - 20, 320);
            var pr = GUILayoutUtility.GetRect(pw, pw / Mathf.Max(0.1f, aspect));
            EditorGUI.DrawTextureTransparent(pr, _atlas, ScaleMode.ScaleToFit);
            EditorGUILayout.Space(6);

            // Метод
            _smart = EditorGUILayout.Toggle(new GUIContent("По границам объектов",
                "Нарезать по «островам» непрозрачности — точнее, если фон прозрачный/однотонный. " +
                "Снять — фиксированная сетка."), _smart);

            if (_smart)
            {
                string msg = _foundSmart < 0
                    ? "Объекты не распознаны — будет сетка."
                    : _foundSmart == _expected
                        ? $"Найдено {_foundSmart} иконок — совпадает."
                        : $"Найдено {_foundSmart}, а не {_expected} — будет сетка. " +
                          "(иконки слиплись или фон мешает?)";
                EditorGUILayout.HelpBox(msg,
                    _foundSmart == _expected ? MessageType.Info : MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(_smart && _foundSmart == _expected))
            {
                _columns = EditorGUILayout.IntSlider(new GUIContent("Колонки сетки",
                    "Число колонок для фиксированной сетки (fallback)."), _columns, 1, _expected);
                int rows = Mathf.CeilToInt(_expected / (float)Mathf.Max(1, _columns));
                EditorGUILayout.LabelField(" ", $"сетка {_columns}×{rows}" +
                    (_columns * rows > _expected ? $" ({_columns * rows - _expected} пустых)" : ""));
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Удаление фона", EditorStyles.boldLabel);
            _bgMode = (AtlasTools.BgMode)EditorGUILayout.EnumPopup(new GUIContent("Метод",
                "Нет — фон как есть. По краям — убрать только фон, связный с границей кадра " +
                "(детали внутри силуэта целы). rembg — ИИ-сегментация для сложного фона."), _bgMode);

            switch (_bgMode)
            {
                case AtlasTools.BgMode.EdgeFlood:
                    _edgeThreshold = EditorGUILayout.IntSlider(new GUIContent("Порог цвета",
                        "Насколько цвет пикселя может отличаться от фона, чтобы считаться фоном. " +
                        "Больше — агрессивнее убирает; меньше — бережнее к краям."), _edgeThreshold, 4, 96);
                    EditorGUILayout.HelpBox("Убирает только фон от краёв кадра — внутренние " +
                        "детали силуэта не трогаются. Лучше для однотонного фона.", MessageType.Info);
                    break;
                case AtlasTools.BgMode.Rembg:
                    if (RembgProcessor.IsAvailable)
                    {
                        _rembgModelIdx = EditorGUILayout.Popup(new GUIContent("Модель",
                            "Разные модели по-разному держат детали. birefnet-general часто " +
                            "цельнее isnet."), _rembgModelIdx, AtlasTools.RembgModels);
                        EditorGUILayout.HelpBox("ИИ угадывает объект — может «съесть» светлые " +
                            "детали внутри. Для сложного/неоднородного фона.", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("rembg недоступен (Project Settings ▸ AI Icon Tools).",
                            MessageType.Warning);
                    }
                    break;
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Отмена")) Close();
                if (GUILayout.Button("Нарезать", GUILayout.Height(24)))
                {
                    string model = AtlasTools.RembgModels[Mathf.Clamp(_rembgModelIdx, 0, AtlasTools.RembgModels.Length - 1)];
                    AtlasTools.Slice(_col, _atlas, _smart, _columns, _bgMode, _edgeThreshold, model);
                    DestroyImmediate(_atlas);   // Slice не владеет текстурой
                    _atlas = null;
                    Close();
                }
            }
        }
    }
}
