using System.IO;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.Editor.Capture
{
    /// <summary>
    /// Инспектор CaptureSystem: базовый инспектор системы + блок «Скриншоты по языкам».
    /// Кнопка в инспекторе (а не хоткей/меню) — клик не уходит в игру и не сбивает кадр.
    /// </summary>
    [CustomEditor(typeof(CaptureSystem))]
    public class CaptureSystemEditor : ProtoSystem.InitializableSystemEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI(); // поле config + база системы

            var system = (CaptureSystem)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📸 Скриншоты на всех языках", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Снимает ТЕКУЩИЙ экран на каждом языке (меняет язык → ждёт перестройки UI → кадр). " +
                "Открой нужный экран заранее. Настройки папки/префикса — в конфиге захвата.",
                EditorStyles.wordWrappedMiniLabel);

#if UNITY_6000_7_OR_NEWER
            // Галочка «вынести кнопку на верхнюю панель редактора» — инструмент специфический,
            // держать его в тулбаре постоянно незачем, поэтому по умолчанию скрыт. Доступно и вне
            // Play Mode (тулбар настраивают заранее), поэтому — до раннего выхода ниже.
            bool showTb = MultiLangCaptureToolbar.ShowButton;
            bool newShowTb = EditorGUILayout.ToggleLeft(
                new GUIContent("Кнопка на верхней панели редактора (рядом с TODO)",
                    "Вынести «📸 Языки» в главный тулбар. Появится сразу; при первой установке " +
                    "пакета может потребоваться перезапуск редактора."),
                showTb);
            if (newShowTb != showTb) MultiLangCaptureToolbar.ShowButton = newShowTb;
#endif

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Доступно только в Play Mode.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            bool ready = ProtoSystem.Loc.IsReady;
            if (!ready)
                EditorGUILayout.HelpBox("Локализация ещё не готова.", MessageType.Warning);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Папка:", system.GetMultiLangDirectory(), EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUI.DisabledScope(!ready || system.IsCapturingAllLanguages))
            {
                if (GUILayout.Button(
                        system.IsCapturingAllLanguages ? "⏳ Идёт съёмка…" : "📸 Снять на всех языках",
                        GUILayout.Height(30)))
                {
                    system.CaptureAllLanguages();
                }
            }

            if (GUILayout.Button("📂 Открыть папку", GUILayout.Height(20)))
            {
                string dir = system.GetMultiLangDirectory();
                try { Directory.CreateDirectory(dir); } catch { /* покажем как есть */ }
                EditorUtility.RevealInFinder(dir);
            }

            if (system.IsCapturingAllLanguages)
                Repaint(); // обновляем состояние кнопки во время прогона

            EditorGUILayout.EndVertical();
        }
    }
}
