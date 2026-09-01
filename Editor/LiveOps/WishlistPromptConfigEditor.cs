// Packages/com.protosystem.core/Editor/LiveOps/WishlistPromptConfigEditor.cs
using UnityEditor;
using UnityEngine;
using ProtoSystem.LiveOps;
using ProtoSystem.UI;

namespace ProtoSystem.Editor.LiveOps
{
    /// <summary>
    /// Инспектор конфига просьбы о вишлисте: настройки, состояние показа и кнопки сброса.
    ///
    /// Редактор висит именно на КОНФИГЕ, а не на системе: систему рисует общий
    /// InitializableSystemEditor, который разворачивает [InlineConfig] через
    /// CreateEditor(config). Свой редактор системы перебил бы его, и вместо
    /// содержимого ассета осталась бы одна ссылка.
    /// </summary>
    [CustomEditor(typeof(WishlistPromptConfig))]
    public class WishlistPromptConfigEditor : UnityEditor.Editor
    {
        private static bool _prefabFound;
        private static double _prefabCheckedAt;
        private const double PrefabCheckSeconds = 2.0;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (WishlistPromptConfig)target;

            // Тихие поломки, из-за которых окно молча не появится
            if (config.triggers == null || config.triggers.Count == 0)
                EditorGUILayout.HelpBox("Нет ни одного триггера — окно нечему показать.",
                                        MessageType.Warning);

            if (config.steamAppId == 0 && string.IsNullOrWhiteSpace(config.ResolveStoreUrl()))
                EditorGUILayout.HelpBox("Ни AppID, ни ссылки на магазин — кнопке «Добавить» некуда вести.",
                                        MessageType.Warning);

            if (!HasWindowPrefab())
            {
                EditorGUILayout.HelpBox(
                    "В проекте нет префаба окна — UISystem не сможет его открыть.\n" +
                    "Создайте: ProtoSystem → LiveOps → Создать префаб окна вишлиста.",
                    MessageType.Warning);

                if (GUILayout.Button("Создать префаб окна"))
                {
                    WishlistPromptWindowGenerator.CreatePrefab();
                    _prefabCheckedAt = 0;   // проверить заново на следующей отрисовке
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Состояние показа", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                WishlistPromptState.Decided
                    ? "Игрок нажал «Добавить» или «Уже добавил» — окно больше не появится."
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

        /// <summary>
        /// Есть ли в проекте префаб окна. Поиск по базе ассетов недёшев, а
        /// OnInspectorGUI зовётся каждый кадр — результат держим пару секунд.
        /// </summary>
        private static bool HasWindowPrefab()
        {
            if (EditorApplication.timeSinceStartup - _prefabCheckedAt < PrefabCheckSeconds)
                return _prefabFound;

            _prefabCheckedAt = EditorApplication.timeSinceStartup;
            _prefabFound = false;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab WishlistPromptWindow"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponent<WishlistPromptWindow>() != null)
                {
                    _prefabFound = true;
                    break;
                }
            }

            return _prefabFound;
        }
    }
}
