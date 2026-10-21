// Кнопка вызова ИИ 3D-студии в ГЛАВНОМ тулбаре редактора, рядом с Арт- и Аудио-студиями.
// Точка в подписи — статус: ● — у студии есть библиотека вариантов (ModelVariantLibrary),
// то есть студия в проекте уже работала; ○ — ещё нет.
//
// Полный близнец IconStudioToolbar — см. комментарии там
// (API MainToolbarElement, кэш набора элементов при старте, показ через ToolbarVisibility).

#if UNITY_6000_7_OR_NEWER

using ProtoSystem.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace ProtoSystem.ModelGen.Editor
{
    [InitializeOnLoad]
    public static class ModelStudioToolbar
    {
        private const string ElementId = "ProtoSystem/ModelStudio";
        private const string UssName   = "protosystem-modelstudio-button";

        private static readonly Color Ready    = new(0.26f, 0.36f, 0.46f);   // сталь — студия в работе
        private static readonly Color NoConfig = new(0.32f, 0.32f, 0.32f);   // серый — ещё не открывалась

        private static bool _hasConfig;

        static ModelStudioToolbar()
        {
            EditorApplication.delayCall += () => ToolbarVisibility.EnsureShownOnce(ElementId);
            EditorApplication.delayCall += Restyle;
            EditorApplication.focusChanged += OnFocusChanged;
        }

        private static void OnFocusChanged(bool focused)
        {
            if (!focused) return;
            EditorApplication.delayCall += () =>
            {
                bool has = HasConfig();
                if (has == _hasConfig) return;
                _hasConfig = has;
                MainToolbar.Refresh(ElementId);
                EditorApplication.delayCall += Restyle;
            };
        }

        [MainToolbarElement(ElementId,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = -2,
            ussName = UssName)]
        private static MainToolbarElement CreateElement()
        {
            _hasConfig = HasConfig();

            var content = new MainToolbarContent(
                _hasConfig ? "🧊 3D ●" : "🧊 3D ○",
                _hasConfig
                    ? "Открыть ИИ 3D-студию."
                    : "Открыть ИИ 3D-студию (сущности дают ассеты с IModelContentProvider).");

            var button = new MainToolbarButton(content, ModelStudioWindow.Open);
            EditorApplication.delayCall += Restyle;
            return button;
        }

        private static bool HasConfig()
        {
            try { return AssetDatabase.FindAssets("t:ModelVariantLibrary").Length > 0; }
            catch { return false; }
        }

        private static void Restyle() => ToolbarButtonPainter.Paint(UssName, _hasConfig ? Ready : NoConfig);
    }
}

#endif
