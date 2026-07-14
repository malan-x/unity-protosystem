// Запекание окна в сцену.
//
// Зачем: UISystem — обычная система, она поднимается в общей очереди инициализации и открывает
// стартовое окно только в конце. Всё это время камера уже рисует 3D-мир, и игрок видит сцену
// (в Last Convoy — вращающийся глобус) до того, как появится меню.
//
// Запечённое окно — обычный экземпляр окна, сохранённый прямо в сцене активным и помеченный
// флагом UIWindowBase.bakedInScene. По флагу окно НЕ прячет себя в Awake, поэтому его UIDocument
// рисует картинку с первого кадра. При инициализации UISystem не создаёт окно заново, а забирает
// этот экземпляр (AdoptBakedWindows -> UIWindowFactory.RegisterBaked) и показывает без fade-in.
//
// ВАЖНО: до Show() у окна не вызывается OnBuildUI, поэтому в первом кадре видно ровно то, что
// описано в UXML/USS. Фон, который окно назначает кодом из спрайта, нужно продублировать в USS —
// иначе запечённое окно будет прозрачным и мир проступит сквозь него.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.Editor.UI
{
    public class BakeStartWindow : EditorWindow
    {
        private const string GraphPath = "ProtoSystem/UIWindowGraph";
        private const string ConfigPath = "ProtoSystem/UISystemConfig";

        private UIWindowGraph _graph;
        private string[] _windowIds = System.Array.Empty<string>();
        private int _selected;

        [MenuItem("ProtoSystem/UI/Запечь окно в сцену", false, 120)]
        public static void Open()
        {
            var window = GetWindow<BakeStartWindow>(true, "Запекание окна в сцену");
            window.minSize = new Vector2(460, 260);
            window.Reload();
        }

        private void Reload()
        {
            _graph = Resources.Load<UIWindowGraph>(GraphPath);
            if (_graph == null) return;

            _windowIds = _graph.windows
                .Where(w => w != null && w.prefab != null && !string.IsNullOrEmpty(w.id))
                .Select(w => w.id)
                .OrderBy(id => id)
                .ToArray();

            // По умолчанию — стартовое окно графа: чаще всего запекают именно его
            int index = System.Array.IndexOf(_windowIds, _graph.startWindowId);
            _selected = index >= 0 ? index : 0;
        }

        private void OnGUI()
        {
            if (_graph == null)
            {
                EditorGUILayout.HelpBox("Не найден UIWindowGraph (Resources/ProtoSystem/UIWindowGraph).",
                    MessageType.Error);
                if (GUILayout.Button("Перечитать")) Reload();
                return;
            }

            if (_windowIds.Length == 0)
            {
                EditorGUILayout.HelpBox("В графе нет окон с префабами.", MessageType.Warning);
                if (GUILayout.Button("Перечитать")) Reload();
                return;
            }

            EditorGUILayout.HelpBox(
                "Запечённое окно лежит в сцене активным и рисуется с первого кадра — до того, " +
                "как поднимется UISystem. Это убирает мелькание 3D-сцены перед стартовым окном.\n\n" +
                "До Show() у окна не вызывается OnBuildUI: в первых кадрах видно только то, что " +
                "задано в UXML/USS. Фон, который окно назначает кодом из спрайта, продублируйте в USS.",
                MessageType.Info);

            EditorGUILayout.Space();
            _selected = EditorGUILayout.Popup("Окно", _selected, _windowIds);

            string id = _windowIds[_selected];
            bool isStart = id == _graph.startWindowId;

            if (!isStart)
            {
                EditorGUILayout.HelpBox(
                    $"'{id}' не является стартовым окном графа (сейчас это '{_graph.startWindowId}'). " +
                    "UISystem погасит его при инициализации — видимым останется только стартовое.",
                    MessageType.Warning);
            }

            var baked = FindBaked(id);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(baked != null))
            {
                if (GUILayout.Button(baked != null ? "Уже запечено в этой сцене" : "Запечь в текущую сцену",
                        GUILayout.Height(28)))
                {
                    Bake(_graph.GetWindow(id));
                }
            }

            using (new EditorGUI.DisabledScope(baked == null))
            {
                if (GUILayout.Button("Убрать запечённое окно", GUILayout.Height(22)))
                    Unbake(baked);
            }

            if (baked != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.ObjectField("В сцене", baked, typeof(GameObject), true);
            }
        }

        private static void Bake(WindowDefinition definition)
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

            AssignPanelSettings(instance, definition);

            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);

            Debug.Log($"[ProtoSystem] Окно '{definition.id}' запечено в сцену '{instance.scene.name}'. " +
                      "Сохраните сцену. До Show() рисуется только то, что задано в UXML/USS.");
        }

        private static void Unbake(GameObject baked)
        {
            if (baked == null) return;

            var scene = baked.scene;
            Undo.DestroyObjectImmediate(baked);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>Ищем запечённый экземпляр по компоненту окна — имя объекта могли поменять.</summary>
        private static GameObject FindBaked(string windowId)
        {
            if (string.IsNullOrEmpty(windowId)) return null;

            var windows = Object.FindObjectsByType<UIWindowBase>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var window in windows)
            {
                if (!window.BakedInScene) continue;

                var attributes = window.GetType()
                    .GetCustomAttributes(typeof(UIWindowAttribute), false);
                if (attributes.Length == 0) continue;

                if (((UIWindowAttribute)attributes[0]).WindowId == windowId)
                    return window.gameObject;
            }
            return null;
        }

        /// <summary>
        /// В рантайме PanelSettings окну назначает фабрика — но запечённое окно рисуется ДО неё,
        /// поэтому панель нужно проставить прямо в сцене. Берём отдельный ассет на слой (копию
        /// шаблона из UISystemConfig с sortingOrder слоя): фабрика затем примет его как панель
        /// всего слоя, и окно не будет мигать при переключении панелей.
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
                                 "назначьте PanelSettings запечённому окну вручную, иначе оно не будет " +
                                 "видно до инициализации UI-системы.");
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
