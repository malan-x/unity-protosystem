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
        /// MainToolbarButton не даёт доступа к своему VisualElement — красить приходится
        /// «снаружи»: найти кнопку в дереве окна тулбара и выставить инлайн-стили.
        ///
        /// Тонкости, из-за которых наивный вариант не работал:
        /// - тулбар строится ПОЗЖЕ, чем срабатывает delayCall, поэтому ищем повторно
        ///   на EditorApplication.update, пока не найдём (и не дольше StyleTimeout);
        /// - на что вешается ussName (USS-класс или name) — не документировано, поэтому
        ///   ищем по классу, по имени И по тексту кнопки;
        /// - MainToolbar.window — internal, отсюда рефлексия.
        /// </summary>
        private static void StyleButton()
        {
            _styleDeadline = EditorApplication.timeSinceStartup + StyleTimeout;
            EditorApplication.update -= TryStyle;
            EditorApplication.update += TryStyle;
        }

        private const double StyleTimeout = 10.0;   // сек: тулбар успевает построиться
        private static double _styleDeadline;

        private static void TryStyle()
        {
            if (EditorApplication.timeSinceStartup > _styleDeadline)
            {
                EditorApplication.update -= TryStyle;
                return;
            }

            var button = FindToolbarButton();
            if (button == null) return;   // тулбар ещё не построен — пробуем на следующем кадре

            EditorApplication.update -= TryStyle;
            Paint(button);
        }

        private static VisualElement FindToolbarButton()
        {
            var windowProp = typeof(MainToolbar).GetProperty("window",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            if (windowProp?.GetValue(null) is not EditorWindow window) return null;

            var root = window.rootVisualElement;
            if (root == null) return null;

            // 1) по USS-классу, 2) по имени — что из этого делает ussName, не документировано
            var button = root.Q(className: UssName) ?? root.Q(name: UssName);
            if (button != null) return button;

            // 3) fallback: по подписи кнопки (её задаём мы сами)
            VisualElement found = null;
            root.Query<TextElement>().ForEach(label =>
            {
                if (found != null || label.text == null || !label.text.StartsWith("TODO")) return;
                // сама кнопка — родитель подписи (EditorToolbarButton)
                found = label.parent ?? label;
            });
            return found;
        }

        private static void Paint(VisualElement button)
        {
            bool hasTasks = TodoListWindow.GetActiveCount() > 0;

            // Кнопка соседствует с Play/Pause/Step: высоту и вертикальное выравнивание берём
            // как у них, иначе фиксированная height без alignSelf уводит её вверх по полосе.
            button.style.backgroundColor = hasTasks ? Accent : AccentIdle;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;
            button.style.paddingLeft = 7;
            button.style.paddingRight = 7;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.marginLeft = 2;
            button.style.marginRight = 2;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;

            button.style.height = StyleKeyword.Null;        // высоту диктует полоса тулбара
            button.style.alignSelf = Align.Center;          // без этого кнопка съезжает вверх
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;

            // Цвет текста задаём и кнопке, и её подписи: у EditorToolbarButton подпись —
            // вложенный TextElement со своим стилем из темы, инлайн на родителе его не перебьёт
            button.style.color = Color.white;
            button.style.fontSize = 12;
            button.Query<TextElement>().ForEach(label =>
            {
                label.style.color = Color.white;
                label.style.fontSize = 12;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
            });
        }
    }
}

#endif
