// Показ элементов главного тулбара, добавленных пакетом.
//
// Unity добавляет НОВЫЕ элементы тулбара скрытыми: определение регистрируется
// ([MainToolbarElement] находится), но кнопка не рисуется, пока её не включат.
// Ручного способа для пользователя нет — только API, и он internal, отсюда рефлексия.
//
// Включаем ОДИН РАЗ на элемент (флаг в EditorPrefs): если пользователь потом сам спрячет
// кнопку через кастомизацию тулбара, мы не будем возвращать её при каждом запуске.

#if UNITY_6000_7_OR_NEWER

using System.Reflection;
using UnityEditor;

namespace ProtoSystem.Editor
{
    public static class ToolbarVisibility
    {
        private const string PrefPrefix = "ProtoSystem.Toolbar.Shown.";

        /// <summary>
        /// Показать элемент тулбара, если это его первое появление в проекте.
        /// </summary>
        public static void EnsureShownOnce(string elementId)
        {
            string pref = PrefPrefix + elementId;
            if (EditorPrefs.GetBool(pref, false)) return;   // уже показывали — решает пользователь

            if (!Show(elementId)) return;                    // тулбар ещё не готов — попробуем позже

            EditorPrefs.SetBool(pref, true);
        }

        private static bool Show(string elementId)
        {
            var toolbar = typeof(UnityEditor.Toolbars.MainToolbar);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

            var showAll = toolbar.GetMethod("ShowAll", flags);
            var refresh = toolbar.GetMethod("Refresh", flags);
            if (showAll == null || refresh == null) return false;   // API поменялось — не падаем

            try
            {
                showAll.Invoke(null, new object[] { elementId });
                refresh.Invoke(null, new object[] { elementId });
                return true;
            }
            catch
            {
                return false;   // тулбар ещё не построен
            }
        }
    }
}

#endif
