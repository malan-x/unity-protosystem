// TODO-кнопка в ГЛАВНОМ тулбаре редактора, рядом с Play/Pause/Step.
//
// Раньше публичного API главного тулбара не было — кнопку приходилось вешать оверлеем
// на SceneView (TodoToolbarButton/TodoOverlay в TodoListWindow.cs). Unity его добавила:
// атрибут [MainToolbarElement] на СТАТИЧЕСКОМ методе, возвращающем MainToolbarElement,
// и MainToolbarDockPosition.Middle — это зона Play/Pause/Step.
//
// Две неочевидности этого API:
// 1) MainToolbarElement наследовать НЕЛЬЗЯ (его CreateElement() — internal). Возвращать
//    нужно готовые типы: MainToolbarButton/Dropdown/Toggle/Label/Slider. Лежат они в сборке
//    UnityEditor.EditorToolbarModule, а не в UnityEditor.
// 2) Набор элементов тулбара Unity собирает при СТАРТЕ редактора и кэширует — после
//    установки пакета кнопка появится только после перезапуска Unity (domain reload мало).
//
// Под #if: на версиях без этого API файл компилируется в пустоту, а оверлей в SceneView
// остаётся рабочим — пакет по-прежнему собирается на старых Unity.

#if UNITY_6000_7_OR_NEWER

using System.Reflection;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.Editor
{
    [InitializeOnLoad]
    public static class TodoMainToolbar
    {
        private const string ElementId = "ProtoSystem/TodoMainToolbar";

        /// <summary>USS-класс, который Unity вешает на элемент — по нему находим кнопку и красим.</summary>
        private const string UssName = "protosystem-todo-button";

        private static readonly Color Accent     = new(0.90f, 0.47f, 0.13f);   // оранжевый
        private static readonly Color AccentIdle = new(0.35f, 0.24f, 0.15f);   // приглушённый (задач нет)

        static TodoMainToolbar()
        {
            // Тулбар пересоздаётся после каждой перекомпиляции — красим заново
            EditorApplication.delayCall += StyleButton;
        }

        /// <summary>
        /// Middle — блок Play/Pause/Step; defaultDockIndex = -1 ставит кнопку слева от них.
        /// Место можно поменять перетаскиванием — Unity запомнит выбор пользователя.
        /// </summary>
        [MainToolbarElement(ElementId,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = -1,
            ussName = UssName)]
        private static MainToolbarElement CreateTodoElement()
        {
            int active = TodoListWindow.GetActiveCount();

            var content = new MainToolbarContent(
                active > 0 ? $"TODO {active}" : "TODO",
                active > 0
                    ? $"Открыть TODO List — активных задач: {active} (Ctrl+Shift+T)"
                    : "Открыть TODO List (Ctrl+Shift+T)");

            var button = new MainToolbarButton(content, () =>
            {
                TodoListWindow.ShowWindow();
                // Счётчик мог измениться — обновим подпись
                EditorApplication.delayCall += () => MainToolbar.Refresh(ElementId);
            });

            EditorApplication.delayCall += StyleButton;
            return button;
        }

        /// <summary>
        /// MainToolbarButton не даёт доступа к своему VisualElement, поэтому находим кнопку
        /// в дереве окна тулбара по USS-классу (ussName из атрибута) и красим инлайн-стилями.
        /// MainToolbar.window — internal, отсюда рефлексия.
        /// </summary>
        private static void StyleButton()
        {
            var toolbarType = typeof(MainToolbar);
            var windowProp = toolbarType.GetProperty("window",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            if (windowProp?.GetValue(null) is not EditorWindow window) return;

            var root = window.rootVisualElement;
            var button = root?.Q(className: UssName);
            if (button == null) return;

            bool hasTasks = TodoListWindow.GetActiveCount() > 0;

            button.style.backgroundColor = hasTasks ? Accent : AccentIdle;
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.marginLeft = 4;
            button.style.marginRight = 4;

            // Подпись у EditorToolbarButton — вложенный TextElement, цвет нужен и ему
            var label = button.Q<TextElement>();
            if (label != null) label.style.color = Color.white;
        }
    }
}

#endif
