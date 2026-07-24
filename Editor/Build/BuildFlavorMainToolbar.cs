// Дропдаун выбора СИМУЛИРУЕМОГО типа сборки в ГЛАВНОМ тулбаре редактора, рядом с кнопкой TODO.
// Позволяет быстро переключить Normal/Demo/Playtest и запустить Play Mode в нужном режиме —
// чтобы посмотреть, как контент-ограничения будут выглядеть в билде, без реальной сборки.
//
// Тот же API главного тулбара, что и у TodoMainToolbar (см. подробные заметки там):
// [MainToolbarElement] на статическом методе → возвращаем готовый MainToolbarDropdown
// (наследовать MainToolbarElement нельзя). Набор элементов Unity кэширует при старте —
// после установки/обновления пакета элемент появится после ПЕРЕЗАПУСКА редактора.
//
// Под #if: на версиях без API файл компилируется в пустоту (меню-симуляция остаётся).

#if UNITY_6000_7_OR_NEWER

using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace ProtoSystem.Editor
{
    [InitializeOnLoad]
    public static class BuildFlavorMainToolbar
    {
        private const string ElementId = "ProtoSystem/BuildFlavorMainToolbar";
        private const string UssName = "protosystem-buildflavor-dropdown";

        static BuildFlavorMainToolbar()
        {
            // Новые элементы тулбара Unity добавляет скрытыми — показать при первом появлении
            EditorApplication.delayCall += () => ToolbarVisibility.EnsureShownOnce(ElementId);
            // Сменили режим (меню или дропдаун) — обновить подпись кнопки
            BuildFlavorEditor.Changed += () => MainToolbar.Refresh(ElementId);
        }

        /// <summary>
        /// Middle — блок Play/Pause/Step; defaultDockIndex = -2 ставит дропдаун слева от TODO (-1).
        /// Позицию можно поменять перетаскиванием — Unity запомнит.
        /// </summary>
        [MainToolbarElement(ElementId,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = -2,
            ussName = UssName)]
        private static MainToolbarElement CreateElement()
        {
            var flavor = BuildFlavorEditor.Simulated;
            string label = flavor == BuildFlavor.Normal ? "Flavor: Normal" : $"● Flavor: {flavor}";

            var content = new MainToolbarContent(
                label,
                "Симулируемый тип сборки в редакторе (Normal/Demo/Playtest).\n" +
                "Влияет на контент-ограничения в Play Mode. В билд не попадает.");

            return new MainToolbarDropdown(content, OpenMenu);
        }

        private static void OpenMenu(Rect rect)
        {
            var current = BuildFlavorEditor.Simulated;
            var menu = new GenericMenu();

            AddItem(menu, "Normal (полная игра)", BuildFlavor.Normal, current);
            AddItem(menu, "Demo", BuildFlavor.Demo, current);
            AddItem(menu, "Playtest", BuildFlavor.Playtest, current);

            menu.DropDown(rect);
        }

        private static void AddItem(GenericMenu menu, string label, BuildFlavor flavor, BuildFlavor current)
        {
            menu.AddItem(new GUIContent(label), current == flavor,
                () => BuildFlavorEditor.SetSimulated(flavor));
        }
    }
}

#endif
