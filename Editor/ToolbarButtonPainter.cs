// Покраска кнопок главного тулбара, добавленных пакетом (студии, TODO и пр.).
//
// MainToolbarButton не даёт доступа к своему VisualElement — кнопку ищем в дереве окна тулбара
// по ussName из [MainToolbarElement] (на что он вешается — класс или name — не документировано,
// поэтому оба варианта). MainToolbar.window — internal, отсюда рефлексия.

#if UNITY_6000_7_OR_NEWER

using System.Reflection;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.Editor
{
    public static class ToolbarButtonPainter
    {
        /// <summary>
        /// Покрасить кнопку тулбара. false — тулбар ещё не построен или кнопка скрыта
        /// (вызывающий повторит позже, обычно на следующем delayCall).
        /// </summary>
        public static bool Paint(string ussName, Color background)
        {
            var windowProp = typeof(MainToolbar).GetProperty("window",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (windowProp?.GetValue(null) is not EditorWindow window) return false;

            var root = window.rootVisualElement;
            var button = root?.Q(className: ussName) ?? root?.Q(name: ussName);
            if (button == null) return false;

            button.style.backgroundColor = background;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;
            button.style.paddingLeft = 7;
            button.style.paddingRight = 7;
            button.style.marginLeft = 2;
            button.style.marginRight = 2;
            button.style.alignSelf = Align.Center;
            button.style.color = Color.white;
            button.style.fontSize = 12;
            button.Query<TextElement>().ForEach(l =>
            {
                l.style.color = Color.white;
                l.style.fontSize = 12;
            });
            return true;
        }
    }
}

#endif
