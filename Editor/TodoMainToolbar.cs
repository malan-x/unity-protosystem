// TODO-кнопка в ГЛАВНОМ тулбаре редактора, рядом с Play/Pause/Step.
//
// Раньше такой возможности не было — кнопку приходилось вешать оверлеем на SceneView
// (см. TodoToolbarButton/TodoOverlay в TodoListWindow.cs). Unity добавила публичный API
// главного тулбара: атрибут [MainToolbarElement] на СТАТИЧЕСКОМ методе, возвращающем
// MainToolbarElement, и MainToolbarDockPosition.Middle — это как раз зона Play.
//
// Под #if: на версиях без этого API файл компилируется в пустоту, а оверлей в SceneView
// остаётся рабочим — пакет по-прежнему собирается на старых Unity.

#if UNITY_6000_7_OR_NEWER

using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.Editor
{
    public static class TodoMainToolbar
    {
        private const string ElementId = "ProtoSystem/TodoMainToolbar";

        /// <summary>
        /// Middle — блок с Play/Pause/Step. defaultDockIndex задаёт место внутри блока:
        /// отрицательный — левее кнопок воспроизведения.
        /// Пользователь может перетащить элемент в другую зону — Unity это запомнит.
        /// </summary>
        [MainToolbarElement(ElementId,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = -1)]
        private static MainToolbarElement CreateTodoElement() => new TodoMainToolbarElement();

        private sealed class TodoMainToolbarElement : MainToolbarElement
        {
            protected override VisualElement CreateElement()
            {
                int active = TodoListWindow.GetActiveCount();

                var button = new EditorToolbarButton(
                    active > 0 ? $"TODO {active}" : "TODO",
                    () => TodoListWindow.ShowWindow())
                {
                    tooltip = active > 0
                        ? $"Открыть TODO List — активных задач: {active} (Ctrl+Shift+T)"
                        : "Открыть TODO List (Ctrl+Shift+T)"
                };

                // Акцент — только когда есть незакрытые задачи, иначе кнопка не мозолит глаз
                if (active > 0)
                {
                    button.style.color = new Color(1f, 0.62f, 0.25f);
                    button.style.unityFontStyleAndWeight = FontStyle.Bold;
                }

                return button;
            }
        }
    }
}

#endif
