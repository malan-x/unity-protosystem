// Операции запекания в сцену. UI живёт в инспекторе UISystem (см. UISystemEditor).
//
// Два способа закрыть 3D-мир на старте, пока UISystem ещё поднимается:
//
// 1. Boot Splash — полноэкранная картинка (ProtoSystem.UI.BootSplash). Ничего не обещает игроку
//    и гаснет, когда открылось первое окно. Способ по умолчанию.
//
// 2. Запечённое окно — экземпляр настоящего окна, сохранённый в сцене активным. Видно с первого
//    кадра, но до Show() у него не вызывается OnBuildUI: кнопки ни на что не подписаны, данные
//    не подтянуты. Игрок видит «живое» меню и жмёт мёртвые кнопки — поэтому осознанный выбор.

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

        /// <summary>Порядок панели заставки: выше любого слоя окон — она перекрывает всё.</summary>
        private const int SplashSortingOrder = 32000;

        #region Boot Splash

        public static BootSplash FindSplash()
        {
            return Object.FindFirstObjectByType<BootSplash>(FindObjectsInactive.Include);
        }

        public static BootSplash CreateSplash()
        {
            var existing = FindSplash();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                return existing;
            }

            var go = new GameObject("[Boot Splash]");
            Undo.RegisterCreatedObjectUndo(go, "Создать Boot Splash");

            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = GetOrCreatePanelSettings("PanelSettings_BootSplash", SplashSortingOrder);
            doc.sortingOrder = SplashSortingOrder;

            var splash = go.AddComponent<BootSplash>();

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);

            Debug.Log("[ProtoSystem] Boot Splash создан. Назначьте ему картинку в инспекторе " +
                      "и сохраните сцену.");
            return splash;
        }

        public static void RemoveSplash()
        {
            var splash = FindSplash();
            if (splash == null) return;

            var scene = splash.gameObject.scene;
            Undo.DestroyObjectImmediate(splash.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        #endregion

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
