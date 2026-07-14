// Запекание стартового окна в сцену.
//
// Зачем: UISystem — обычная система, она поднимается в общей очереди инициализации и открывает
// стартовое окно только в конце. Всё это время камера уже рисует 3D-мир, и игрок видит сцену
// (у Last Convoy — вращающийся глобус) до того, как появится меню.
//
// Запечённое окно — обычный экземпляр окна, сохранённый прямо в сцене активным. Его UIDocument
// рисует панель с первого кадра, поэтому мир закрыт сразу. При инициализации UISystem не создаёт
// окно заново, а забирает этот экземпляр (UISystem.AdoptBakedWindows -> UIWindowFactory.RegisterBaked)
// и показывает его без fade-in.
//
// ВАЖНО: до Show() у окна не вызывается OnBuildUI, поэтому в первом кадре видно ровно то, что
// описано в UXML/USS. Фон, который окно назначает кодом из спрайта, нужно продублировать в USS —
// иначе запечённое окно будет прозрачным и мир проступит сквозь него.

using System.IO;
using ProtoSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.Editor.UI
{
    public static class BakeStartWindow
    {
        private const string GraphPath  = "ProtoSystem/UIWindowGraph";
        private const string ConfigPath = "ProtoSystem/UISystemConfig";

        [MenuItem("ProtoSystem/UI/Запечь стартовое окно в сцену", false, 120)]
        public static void Bake()
        {
            var graph = Resources.Load<UIWindowGraph>(GraphPath);
            if (graph == null || string.IsNullOrEmpty(graph.startWindowId))
            {
                EditorUtility.DisplayDialog("Запекание стартового окна",
                    "Не найден UIWindowGraph или в нём не задан startWindowId.", "Ок");
                return;
            }

            var definition = graph.GetWindow(graph.startWindowId);
            if (definition?.prefab == null)
            {
                EditorUtility.DisplayDialog("Запекание стартового окна",
                    $"В графе нет префаба для стартового окна '{graph.startWindowId}'.", "Ок");
                return;
            }

            var existing = FindBaked(definition.id);
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                EditorGUIUtility.PingObject(existing);
                EditorUtility.DisplayDialog("Запекание стартового окна",
                    $"Окно '{definition.id}' уже запечено в этой сцене.", "Ок");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(definition.prefab);
            if (instance == null) return;

            instance.name = $"[Baked] {definition.id}";
            instance.SetActive(true);
            Undo.RegisterCreatedObjectUndo(instance, "Запечь стартовое окно");

            AssignPanelSettings(instance, definition);

            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);

            Debug.Log($"[ProtoSystem] Стартовое окно '{definition.id}' запечено в сцену " +
                      $"'{instance.scene.name}'. Сохраните сцену. " +
                      "Помните: до Show() рисуется только то, что задано в UXML/USS.");
        }

        [MenuItem("ProtoSystem/UI/Убрать запечённое стартовое окно", false, 121)]
        public static void Unbake()
        {
            var graph = Resources.Load<UIWindowGraph>(GraphPath);
            if (graph == null) return;

            var existing = FindBaked(graph.startWindowId);
            if (existing == null)
            {
                EditorUtility.DisplayDialog("Запекание стартового окна",
                    "Запечённого окна в этой сцене нет.", "Ок");
                return;
            }

            var scene = existing.scene;
            Undo.DestroyObjectImmediate(existing);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// Ищем запечённый экземпляр по компоненту окна (имя объекта могли поменять).
        /// </summary>
        private static GameObject FindBaked(string windowId)
        {
            if (string.IsNullOrEmpty(windowId)) return null;

            var windows = Object.FindObjectsByType<UIWindowBase>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var window in windows)
            {
                var attribute = window.GetType()
                    .GetCustomAttributes(typeof(UIWindowAttribute), false);
                if (attribute.Length == 0) continue;

                if (((UIWindowAttribute)attribute[0]).WindowId == windowId)
                    return window.gameObject;
            }
            return null;
        }

        /// <summary>
        /// В рантайме PanelSettings окну назначает фабрика — но запечённое окно должно рисоваться
        /// ДО неё, поэтому панель нужно проставить прямо в сцене. Берём отдельный ассет на слой
        /// (копию шаблона из UISystemConfig с sortingOrder слоя): фабрика затем примет его как
        /// панель всего слоя, и окно не будет мигать при переключении панелей.
        /// </summary>
        private static void AssignPanelSettings(GameObject instance, WindowDefinition definition)
        {
            var doc = instance.GetComponent<UIDocument>();
            if (doc == null || doc.panelSettings != null) return;   // uGUI-окно или своя панель

            var config = Resources.Load<UISystemConfig>(ConfigPath);
            var template = config != null ? config.panelSettings : null;
            if (template == null)
            {
                Debug.LogWarning("[ProtoSystem] В UISystemConfig не задан шаблон PanelSettings — " +
                                 "назначьте PanelSettings запечённому окну вручную, иначе оно " +
                                 "не будет видно до инициализации UI-системы.");
                return;
            }

            string templatePath = AssetDatabase.GetAssetPath(template);
            string directory = Path.GetDirectoryName(templatePath)?.Replace('\\', '/');
            string assetPath = $"{directory}/PanelSettings_Baked_{definition.layer}.asset";

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(assetPath);
            if (panel == null)
            {
                panel = Object.Instantiate(template);
                panel.sortingOrder = (int)definition.layer;   // та же шкала, что у Canvas-слоёв
                AssetDatabase.CreateAsset(panel, assetPath);
                AssetDatabase.SaveAssets();
            }

            doc.panelSettings = panel;
            EditorUtility.SetDirty(doc);
        }
    }
}
