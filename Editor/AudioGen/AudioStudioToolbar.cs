// Кнопка вызова ИИ Аудио-студии в ГЛАВНОМ тулбаре редактора, рядом с кнопкой Арт-студии.
// Точка в подписи — статус: подключён ли конфиг студии (AudioStudioConfig):
//   ● зелёная бронза — конфиг найден; ○ серый — конфига нет (вкладки соберутся из наборов).
//
// Полный близнец IconStudioToolbar — см. комментарии там (API MainToolbarElement, кэш
// набора элементов при старте редактора, показ через ToolbarVisibility).

#if UNITY_6000_7_OR_NEWER

using ProtoSystem.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    [InitializeOnLoad]
    public static class AudioStudioToolbar
    {
        private const string ElementId = "ProtoSystem/AudioStudio";
        private const string UssName   = "protosystem-audiostudio-button";

        private static readonly Color Ready    = new(0.24f, 0.42f, 0.34f);   // патина — конфиг подключён
        private static readonly Color NoConfig = new(0.32f, 0.32f, 0.32f);   // серый — конфига нет

        private static bool _hasConfig;

        static AudioStudioToolbar()
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
            defaultDockIndex = -3,
            ussName = UssName)]
        private static MainToolbarElement CreateElement()
        {
            _hasConfig = HasConfig();

            var content = new MainToolbarContent(
                _hasConfig ? "🎵 Звук ●" : "🎵 Звук ○",
                _hasConfig
                    ? "Открыть ИИ Аудио-студию (конфиг студии подключён)."
                    : "Открыть ИИ Аудио-студию (конфиг студии не найден — вкладки соберутся из наборов).");

            var button = new MainToolbarButton(content, AudioStudioWindow.Open);
            EditorApplication.delayCall += Restyle;
            return button;
        }

        private static bool HasConfig()
        {
            try { return AssetDatabase.FindAssets("t:AudioStudioConfig").Length > 0; }
            catch { return false; }
        }

        private static void Restyle() => ToolbarButtonPainter.Paint(UssName, _hasConfig ? Ready : NoConfig);
    }
}

#endif
