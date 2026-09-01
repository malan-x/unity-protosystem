// Packages/com.protosystem.core/Editor/LiveOps/WishlistPromptConfigEditor.cs
using UnityEditor;
using UnityEngine;
using ProtoSystem.LiveOps;

namespace ProtoSystem.Editor.LiveOps
{
    /// <summary>
    /// Инспектор конфига панели вишлиста: настройки, состояние показа и кнопки сброса.
    ///
    /// Редактор висит именно на КОНФИГЕ, а не на системе: систему рисует общий
    /// InitializableSystemEditor, который разворачивает [InlineConfig] через
    /// CreateEditor(config). Свой редактор системы перебил бы его, и вместо
    /// содержимого ассета осталась бы одна ссылка. А так и поля, и кнопки
    /// оказываются внутри инспектора системы сами собой.
    /// </summary>
    [CustomEditor(typeof(WishlistPromptConfig))]
    public class WishlistPromptConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (WishlistPromptConfig)target;

            // Тихие поломки, из-за которых панель молча не появится
            if (config.template == null)
                EditorGUILayout.HelpBox("Не задан шаблон (VisualTreeAsset) — панель не покажется.",
                                        MessageType.Warning);
            if (config.triggers == null || config.triggers.Count == 0)
                EditorGUILayout.HelpBox("Нет ни одного триггера — панель нечему показать.",
                                        MessageType.Warning);
            if (config.steamAppId == 0 && string.IsNullOrWhiteSpace(config.ResolveStoreUrl()))
                EditorGUILayout.HelpBox("Ни AppID, ни ссылки на магазин — кнопке «Добавить» некуда вести.",
                                        MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Состояние показа", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                WishlistPromptState.Decided
                    ? "Игрок нажал «Добавить» или «Уже добавил» — панель больше не появится."
                    : $"Показов сделано: {WishlistPromptState.Shows} из {config.maxShows}. Решение не принято.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Сбросить показы и решение"))
                {
                    WishlistPromptState.Reset();
                    Repaint();
                }

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Показать сейчас"))
                    {
                        var system = Object.FindFirstObjectByType<WishlistPromptSystem>(FindObjectsInactive.Include);
                        if (system != null) system.ShowNow();
                        else Debug.LogWarning("[WishlistPrompt] Система не найдена в сцене — показывать нечем.");
                    }
                }
            }

            EditorGUILayout.LabelField(
                Application.isPlaying
                    ? "Состояние живёт в PlayerPrefs: общее у редактора и билда, своё на каждой машине."
                    : "«Показать сейчас» — только в Play Mode.",
                EditorStyles.miniLabel);

            // Счётчик меняется в рантайме — иначе цифры застынут на момент открытия
            if (Application.isPlaying) Repaint();
        }
    }
}
