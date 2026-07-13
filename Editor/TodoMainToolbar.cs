// TODO-кнопка в ГЛАВНОМ тулбаре редактора, рядом с Play/Pause/Step.
//
// Раньше публичного API главного тулбара не было — кнопку приходилось вешать оверлеем
// на SceneView (TodoToolbarButton/TodoOverlay в TodoListWindow.cs). Unity его добавила:
// атрибут [MainToolbarElement] на СТАТИЧЕСКОМ методе, возвращающем MainToolbarElement,
// и MainToolbarDockPosition.Middle — это зона Play/Pause/Step.
//
// Важно: сам MainToolbarElement наследовать НЕЛЬЗЯ — его CreateElement() internal.
// Возвращать нужно готовые типы: MainToolbarButton/Dropdown/Toggle/Label/Slider.
// Они лежат в сборке UnityEditor.EditorToolbarModule, а не в UnityEditor.
//
// Под #if: на версиях без этого API файл компилируется в пустоту, а оверлей в SceneView
// остаётся рабочим — пакет по-прежнему собирается на старых Unity.

#if UNITY_6000_7_OR_NEWER

using UnityEditor.Toolbars;

namespace ProtoSystem.Editor
{
    public static class TodoMainToolbar
    {
        private const string ElementId = "ProtoSystem/TodoMainToolbar";

        /// <summary>
        /// Middle — блок Play/Pause/Step; defaultDockIndex = -1 ставит кнопку слева от них.
        /// Место можно поменять перетаскиванием — Unity запомнит выбор пользователя.
        /// </summary>
        [MainToolbarElement(ElementId,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = -1)]
        private static MainToolbarElement CreateTodoElement()
        {
            int active = TodoListWindow.GetActiveCount();

            var content = new MainToolbarContent(
                active > 0 ? $"TODO {active}" : "TODO",
                active > 0
                    ? $"Открыть TODO List — активных задач: {active} (Ctrl+Shift+T)"
                    : "Открыть TODO List (Ctrl+Shift+T)");

            return new MainToolbarButton(content, TodoListWindow.ShowWindow);
        }
    }
}

#endif
