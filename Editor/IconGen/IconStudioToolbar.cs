// Кнопка вызова ИИ Арт-студии в ГЛАВНОМ тулбаре редактора, рядом с TODO.
// Точка в подписи — статус: подключён ли конфиг студии (IconStudioConfig):
//   ● латунь — конфиг найден; ○ серый — конфига нет (вкладки соберутся из наборов).
//
// Тот же API главного тулбара, что и у TodoMainToolbar: [MainToolbarElement] на СТАТИЧЕСКОМ
// методе → готовый MainToolbarButton (наследовать нельзя). Набор элементов Unity кэширует при
// СТАРТЕ редактора — после установки пакета кнопка появится после ПЕРЕЗАПУСКА. Новые элементы
// Unity добавляет скрытыми — ToolbarVisibility включает кнопку один раз.
//
// Под #if: на версиях без API — пустышка (студия открывается из меню «ProtoSystem/ИИ студии»).

#if UNITY_6000_7_OR_NEWER

using ProtoSystem.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace ProtoSystem.IconGen.Editor
{
    [InitializeOnLoad]
    public static class IconStudioToolbar
    {
        private const string ElementId = "ProtoSystem/IconStudio";
        private const string UssName   = "protosystem-iconstudio-button";

        private static readonly Color Ready    = new(0.55f, 0.40f, 0.20f);   // латунь — конфиг подключён
        private static readonly Color NoConfig = new(0.32f, 0.32f, 0.32f);   // серый — конфига нет

        private static bool _hasConfig;

        static IconStudioToolbar()
        {
            EditorApplication.delayCall += () => ToolbarVisibility.EnsureShownOnce(ElementId);
            EditorApplication.delayCall += Restyle;
            // Конфиг студии могли создать/удалить — освежим статус при возврате фокуса в редактор.
            // Только по фокусу, не на update: FindAssets на каждый тик — лишняя работа.
            EditorApplication.focusChanged += OnFocusChanged;
        }

        private static void OnFocusChanged(bool focused)
        {
            if (!focused) return;
            EditorApplication.delayCall += () =>
            {
                bool has = HasConfig();
                if (has == _hasConfig) return;   // статус не менялся — не дёргаем тулбар
                _hasConfig = has;
                MainToolbar.Refresh(ElementId);
                EditorApplication.delayCall += Restyle;
            };
        }

        /// <summary>
        /// Middle — блок Play/Pause/Step; defaultDockIndex = -4 ставит кнопку левее остальных студий.
        /// Позицию можно поменять перетаскиванием — Unity запомнит.
        /// </summary>
        [MainToolbarElement(ElementId,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = -4,
            ussName = UssName)]
        private static MainToolbarElement CreateElement()
        {
            _hasConfig = HasConfig();

            var content = new MainToolbarContent(
                _hasConfig ? "🎨 Арт ●" : "🎨 Арт ○",
                _hasConfig
                    ? "Открыть ИИ Арт-студию (конфиг студии подключён)."
                    : "Открыть ИИ Арт-студию (конфиг студии не найден — вкладки соберутся из наборов).");

            var button = new MainToolbarButton(content, IconStudioWindow.Open);
            EditorApplication.delayCall += Restyle;
            return button;
        }

        /// <summary>Есть ли хоть один IconStudioConfig в проекте.</summary>
        private static bool HasConfig()
        {
            try { return AssetDatabase.FindAssets("t:IconStudioConfig").Length > 0; }
            catch { return false; }
        }

        private static void Restyle() => ToolbarButtonPainter.Paint(UssName, _hasConfig ? Ready : NoConfig);
    }
}

#endif
