// Кнопка вызова генератора спрайтов в ГЛАВНОМ тулбаре редактора,
// рядом с Арт-, Аудио- и 3D-студиями.
// Точка в подписи — статус: ● — в проекте есть хоть один SpriteGenConfig; ○ — нет.
//
// Полный близнец IconStudioToolbar — см. комментарии там
// (API MainToolbarElement, кэш набора элементов при старте, показ через ToolbarVisibility).

#if UNITY_6000_7_OR_NEWER

using ProtoSystem.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace ProtoSystem.SpriteGen.Editor
{
    [InitializeOnLoad]
    public static class SpriteGenToolbar
    {
        private const string ElementId = "ProtoSystem/SpriteGen";
        private const string UssName   = "protosystem-spritegen-button";

        private static readonly Color Ready    = new(0.26f, 0.36f, 0.46f);   // сталь — конфиги есть
        private static readonly Color NoConfig = new(0.32f, 0.32f, 0.32f);   // серый — конфигов нет

        private static bool _hasConfig;

        static SpriteGenToolbar()
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
                _hasConfig ? "🎞 Спрайты ●" : "🎞 Спрайты ○",
                _hasConfig
                    ? "Открыть генератор спрайтов из 3D-моделей (конфиги найдены)."
                    : "Открыть генератор спрайтов из 3D-моделей (конфигов пока нет — перетащи модель в окно).");

            var button = new MainToolbarButton(content, SpriteGeneratorWindow.ShowWindow);
            EditorApplication.delayCall += Restyle;
            return button;
        }

        private static bool HasConfig()
        {
            try { return AssetDatabase.FindAssets("t:SpriteGenConfig").Length > 0; }
            catch { return false; }
        }

        private static void Restyle() => ToolbarButtonPainter.Paint(UssName, _hasConfig ? Ready : NoConfig);
    }
}

#endif
