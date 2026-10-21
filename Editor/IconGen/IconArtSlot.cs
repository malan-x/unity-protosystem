using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Переиспользуемый UI-слот арта для инспекторов/окон: миниатюра + ObjectField + кнопки
    /// (вставка из буфера, вырез фона, дочистка) + опциональное поле промпта + кнопка генерации.
    /// Вся привязка к сущности — через колбэки в Args, поэтому подключается в любой редактор
    /// одним вызовом IconArtSlot.Draw(...).
    /// </summary>
    public static class IconArtSlot
    {
        public struct Args
        {
            public string Title;               // подпись слота («Иконка 64×64»)
            public Sprite Current;             // текущий спрайт
            public string AssetPath;           // куда сохранять, "Assets/..."
            public Vector2Int StorageSize;     // размер в проекте (по умолч. 256×256)
            public Object UndoTarget;          // объект для Undo/SetDirty
            public Action<Sprite> Assign;      // куда присвоить результат

            // Промпт (опционально): если оба заданы — рисуется поле промпта и кнопка генерации.
            public Func<string> GetPrompt;
            public Action<string> SetPrompt;
            public IconStylePreset Style;      // null — дефолт реестра
            public string DisplayName;         // метка прогресса/логов
        }

        private const float Thumb = 88f;

        public static void Draw(in Args args)
        {
            var size = args.StorageSize.x > 0 && args.StorageSize.y > 0
                ? args.StorageSize : new Vector2Int(256, 256);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Миниатюра слева
            var rect = GUILayoutUtility.GetRect(Thumb, Thumb, GUILayout.Width(Thumb), GUILayout.Height(Thumb));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            if (args.Current != null && args.Current.texture != null)
                GUI.DrawTexture(rect, args.Current.texture, ScaleMode.ScaleToFit, true);

            // Правая колонка
            EditorGUILayout.BeginVertical();
            if (!string.IsNullOrEmpty(args.Title))
                EditorGUILayout.LabelField(args.Title, EditorStyles.miniBoldLabel);

            var picked = (Sprite)EditorGUILayout.ObjectField(args.Current, typeof(Sprite), false);
            if (picked != args.Current)
            {
                RecordUndo(args.UndoTarget, "Set art");
                args.Assign?.Invoke(picked);
                Dirty(args.UndoTarget);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Из буфера"))
                PasteFromClipboard(args, size, cutBg: false);
            using (new EditorGUI.DisabledScope(!RembgProcessor.IsAvailable))
            {
                if (GUILayout.Button("Из буфера + вырез фона"))
                    PasteFromClipboard(args, size, cutBg: true);
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(!RembgProcessor.IsAvailable || args.Current == null))
            {
                if (GUILayout.Button("Убрать фон у текущей"))
                    CutBackgroundInPlace(args, size);
            }

            // Блок промпта + генерация (если сущность хранит промпт)
            if (args.GetPrompt != null && args.SetPrompt != null)
            {
                EditorGUI.BeginChangeCheck();
                string prompt = EditorGUILayout.TextArea(args.GetPrompt() ?? "", GUILayout.MinHeight(40));
                if (EditorGUI.EndChangeCheck())
                {
                    RecordUndo(args.UndoTarget, "Edit prompt");
                    args.SetPrompt(prompt);
                    Dirty(args.UndoTarget);
                }

                using (new EditorGUI.DisabledScope(AiIconGenerator.IsRunning ||
                                                   string.IsNullOrWhiteSpace(args.GetPrompt())))
                {
                    if (GUILayout.Button("Сгенерировать (ComfyUI)"))
                        Generate(args, size);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private static void Generate(in Args args, Vector2Int size)
        {
            var a = args; // копия для замыкания
            AiIconGenerator.Enqueue(new IconGenRequest
            {
                Prompt = a.GetPrompt(),
                AssetPath = a.AssetPath,
                DisplayName = a.DisplayName,
                StorageSize = size,
                Style = a.Style,
                CutBackground = true,
                UndoTarget = a.UndoTarget,
                UndoLabel = "Generate icon",
                OnDone = (s, _) => a.Assign?.Invoke(s),
            });
        }

        private static void PasteFromClipboard(in Args args, Vector2Int size, bool cutBg)
        {
            Sprite sprite;
            if (cutBg)
            {
                var raw = ClipboardImageImporter.GrabClipboardTexture();
                if (raw == null) return;
                sprite = RembgProcessor.ProcessBytes(raw.EncodeToPNG(), args.AssetPath, size.x, size.y);
                Object.DestroyImmediate(raw);
            }
            else
            {
                sprite = ClipboardImageImporter.PasteAsSprite(args.AssetPath,
                    new ClipboardImageImporter.ImportOptions { maxSize = Mathf.Max(size.x, size.y) });
            }
            if (sprite == null) return;

            RecordUndo(args.UndoTarget, "Paste art");
            args.Assign?.Invoke(sprite);
            Dirty(args.UndoTarget);
            AssetDatabase.SaveAssets();
        }

        private static void CutBackgroundInPlace(in Args args, Vector2Int size)
        {
            var sprite = RembgProcessor.ProcessAssetInPlace(args.AssetPath, size.x, size.y);
            if (sprite == null) return;

            RecordUndo(args.UndoTarget, "Cut background");
            args.Assign?.Invoke(sprite);
            Dirty(args.UndoTarget);
            AssetDatabase.SaveAssets();
        }

        private static void RecordUndo(Object target, string label)
        {
            if (target != null) Undo.RecordObject(target, label);
        }

        private static void Dirty(Object target)
        {
            if (target != null) EditorUtility.SetDirty(target);
        }
    }
}
