// Packages/com.protosystem.core/Editor/LiveOps/WishlistPromptWindowGenerator.cs
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ProtoSystem.UI;

namespace ProtoSystem.Editor.LiveOps
{
    /// <summary>
    /// Создаёт префаб окна вишлиста в проекте.
    ///
    /// Класс окна и разметка живут в пакете, а префаб обязан лежать в проекте:
    /// UIWindowFactory поднимает окна из префабов, и подхватывает их по метке
    /// (UISystemConfig.windowPrefabLabels) — регистрировать руками не нужно.
    ///
    /// Префаб минимальный: UIDocument с UXML пакета плюс компонент окна.
    /// PanelSettings НЕ назначается намеренно — фабрика подставит его по слою
    /// из UISystemConfig, как у остальных окон.
    /// </summary>
    public static class WishlistPromptWindowGenerator
    {
        private const string UxmlPath =
            "Packages/com.protosystem.core/Runtime/UI/Toolkit/WishlistPrompt.uxml";

        private const string DefaultFolder = "Assets/ProtoSystem/Prefabs/UI";
        private const string WindowLabel   = "UIWindow";

        [MenuItem("ProtoSystem/LiveOps/Создать префаб окна вишлиста", false, 200)]
        public static void CreatePrefab()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[WishlistPrompt] Не найден {UxmlPath}");
                return;
            }

            Directory.CreateDirectory(DefaultFolder);
            AssetDatabase.Refresh();

            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultFolder}/WishlistPromptWindow.prefab");

            var go = new GameObject("WishlistPromptWindow");
            var doc = go.AddComponent<UIDocument>();
            doc.visualTreeAsset = uxml;

            var window = go.AddComponent<WishlistPromptWindow>();

            // Стартовый фокус — «Добавить»: без него окно открывается без
            // выделения, и с геймпада выбрать вариант нельзя
            var so = new SerializedObject(window);
            var focus = so.FindProperty("defaultFocusName");
            if (focus != null) focus.stringValue = "add-button";
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            if (prefab == null)
            {
                Debug.LogError("[WishlistPrompt] Не удалось сохранить префаб окна.");
                return;
            }

            // Метка — то, по чему UISystem найдёт окно без ручной регистрации
            AssetDatabase.SetLabels(prefab, new[] { WindowLabel });
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(prefab);

            Debug.Log($"[WishlistPrompt] Создан {path} с меткой «{WindowLabel}». " +
                      "Если в UISystemConfig окна перечислены списком, добавьте префаб и туда.");
        }
    }
}
