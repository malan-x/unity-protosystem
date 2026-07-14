// Операции запекания окна в сцену. UI живёт в инспекторе UISystem (см. UISystemEditor).
//
// Зачем: UISystem открывает стартовое окно только в конце общей очереди инициализации — до этого
// камера рисует голый 3D-мир. Запечённое окно (экземпляр в сцене, флаг UIWindowBase.bakedInScene)
// рисуется с первого кадра и закрывает эту дыру; система потом забирает его вместо создания нового.
//
// Штатный кандидат на запекание — окно заставки (ProtoSystem.UI.SplashWindow): у него нет кнопок,
// поэтому «мёртвого интерактива» до Show() не возникает. Запекать окно с кнопками можно, но игрок
// увидит живое на вид меню, у которого до Show() ничего не подписано.

using System.IO;
using ProtoSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.Editor.UI
{
    public static class BakeTools
    {
        public const string GraphResource = "ProtoSystem/UIWindowGraph";
        public const string ConfigResource = "ProtoSystem/UISystemConfig";

        #region Запечённое окно

        /// <summary>Ищем запечённый экземпляр по флагу — имя объекта могли поменять.</summary>
        public static UIWindowBase FindBakedWindow(string windowId = null)
        {
            var windows = Object.FindObjectsByType<UIWindowBase>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var window in windows)
            {
                if (!window.BakedInScene) continue;
                if (string.IsNullOrEmpty(windowId)) return window;

                var attributes = window.GetType()
                    .GetCustomAttributes(typeof(UIWindowAttribute), false);
                if (attributes.Length == 0) continue;

                if (((UIWindowAttribute)attributes[0]).WindowId == windowId)
                    return window;
            }
            return null;
        }

        public static void BakeWindow(WindowDefinition definition)
        {
            if (definition?.prefab == null) return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(definition.prefab);
            if (instance == null) return;

            instance.name = $"[Baked] {definition.id}";
            instance.SetActive(true);
            Undo.RegisterCreatedObjectUndo(instance, "Запечь окно");

            // Флаг: по нему окно не прячет себя в Awake, а UISystem находит его и забирает
            var window = instance.GetComponent<UIWindowBase>();
            if (window != null)
            {
                var so = new SerializedObject(window);
                var prop = so.FindProperty("bakedInScene");
                if (prop != null)
                {
                    prop.boolValue = true;
                    so.ApplyModifiedProperties();
                }
            }

            AssignWindowPanelSettings(instance, definition);

            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);

            Debug.Log($"[ProtoSystem] Окно '{definition.id}' запечено в сцену '{instance.scene.name}'. " +
                      "Сохраните сцену. До Show() рисуется только то, что задано в UXML/USS.");
        }

        public static void UnbakeWindow(UIWindowBase window)
        {
            if (window == null) return;

            var scene = window.gameObject.scene;
            Undo.DestroyObjectImmediate(window.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// В рантайме PanelSettings окну назначает фабрика — но запечённое окно рисуется ДО неё,
        /// поэтому панель нужна прямо в сцене. Отдельный ассет на слой; фабрика затем примет его
        /// как панель всего слоя, и окно не будет мигать при переключении панелей.
        /// </summary>
        private static void AssignWindowPanelSettings(GameObject instance, WindowDefinition definition)
        {
            var doc = instance.GetComponent<UIDocument>();
            if (doc == null || doc.panelSettings != null) return;   // uGUI-окно или своя панель

            doc.panelSettings = GetOrCreatePanelSettings(
                $"PanelSettings_Baked_{definition.layer}", (int)definition.layer);
            EditorUtility.SetDirty(doc);
        }

        #endregion

        #region Окно заставки

        /// <summary>
        /// Префаб окна заставки. Дерево оно строит кодом, поэтому UXML-ассет не нужен —
        /// в префабе только UIDocument и сам SplashWindow.
        /// Метки берём из UISystemConfig: по ним окно попадает в граф.
        /// </summary>
        public static GameObject CreateSplashPrefab()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Префаб окна заставки", "SplashWindow", "prefab",
                "Куда сохранить префаб окна заставки?");

            if (string.IsNullOrEmpty(path)) return null;

            var go = new GameObject("SplashWindow");
            go.AddComponent<UIDocument>();      // PanelSettings назначит фабрика (или запекание)
            go.AddComponent<SplashWindow>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            if (prefab == null) return null;

            var config = Resources.Load<UISystemConfig>(ConfigResource);
            var labels = config != null ? config.windowPrefabLabels : null;
            if (labels != null && labels.Count > 0)
                AssetDatabase.SetLabels(prefab, labels.ToArray());
            else
                Debug.LogWarning("[ProtoSystem] В UISystemConfig не заданы windowPrefabLabels — " +
                                 "пометьте префаб заставки вручную, иначе он не попадёт в граф окон.");

            AssetDatabase.SaveAssets();
            UIWindowGraphBuilder.RebuildGraph();

            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[ProtoSystem] Создан {path}. Назначьте кадры заставки в префабе, " +
                      "сделайте 'Splash' стартовым окном графа и запеките его в стартовую сцену.");
            return prefab;
        }

        #endregion

        /// <summary>Копия шаблона PanelSettings из UISystemConfig с нужным порядком сортировки.</summary>
        private static PanelSettings GetOrCreatePanelSettings(string assetName, int sortingOrder)
        {
            var config = Resources.Load<UISystemConfig>(ConfigResource);
            var template = config != null ? config.panelSettings : null;
            if (template == null)
            {
                Debug.LogWarning("[ProtoSystem] В UISystemConfig не задан шаблон PanelSettings — " +
                                 "назначьте PanelSettings вручную, иначе панель не будет видна.");
                return null;
            }

            string directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(template))
                ?.Replace('\\', '/');
            string assetPath = $"{directory}/{assetName}.asset";

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(assetPath);
            if (panel != null) return panel;

            panel = Object.Instantiate(template);
            panel.sortingOrder = sortingOrder;
            AssetDatabase.CreateAsset(panel, assetPath);
            AssetDatabase.SaveAssets();
            return panel;
        }
    }
}
