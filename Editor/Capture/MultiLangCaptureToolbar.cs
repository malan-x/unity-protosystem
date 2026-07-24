// Кнопка «скриншот на всех языках» в ГЛАВНОМ тулбаре редактора, рядом с TODO.
//
// Кнопка специфическая (снимаем ассеты для постов/Steam), держать её в полосе постоянно
// незачем — поэтому по умолчанию скрыта, включается галочкой в инспекторе Capture System.
//
// Тот же API главного тулбара, что и у TodoMainToolbar (подробные заметки там):
// [MainToolbarElement] на СТАТИЧЕСКОМ методе → готовый MainToolbarButton (наследовать нельзя).
// Набор элементов Unity собирает при СТАРТЕ редактора и кэширует — после установки/обновления
// пакета кнопка появится только после ПЕРЕЗАПУСКА редактора (domain reload мало).
//
// Видимость по галочке: сам элемент зарегистрирован всегда, а показываем/прячем его через
// style.display у VisualElement — так галочка срабатывает без перезапуска. EnsureShownOnce
// один раз вводит элемент в видимую полосу (иначе Unity держит новые элементы скрытыми).
//
// Под #if: на версиях без этого API файл компилируется в пустоту — кнопка в инспекторе
// Capture System остаётся рабочей, пакет по-прежнему собирается на старых Unity.

#if UNITY_6000_7_OR_NEWER

using System.Reflection;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.Editor.Capture
{
    [InitializeOnLoad]
    public static class MultiLangCaptureToolbar
    {
        private const string ElementId = "ProtoSystem/MultiLangCapture";
        private const string UssName   = "protosystem-multilang-button";

        /// <summary>EditorPrefs: показывать ли кнопку в тулбаре (галочка в инспекторе Capture System).</summary>
        public const string ShowPrefKey = "ProtoSystem.Capture.MultiLangToolbarButton";

        private static readonly Color Accent = new(0.20f, 0.55f, 0.75f);   // синевато — «локализация»

        /// <summary>Показывать ли кнопку. Смена сразу перерисовывает тулбар — без перезапуска.</summary>
        public static bool ShowButton
        {
            get => EditorPrefs.GetBool(ShowPrefKey, false);
            set
            {
                if (value == ShowButton) return;
                EditorPrefs.SetBool(ShowPrefKey, value);
                MainToolbar.Refresh(ElementId);
                EditorApplication.delayCall += Restyle;
            }
        }

        static MultiLangCaptureToolbar()
        {
            // Unity добавляет новые элементы тулбара СКРЫТЫМИ — вводим в полосу при первом появлении
            EditorApplication.delayCall += () => ToolbarVisibility.EnsureShownOnce(ElementId);
            // Тулбар пересоздаётся после каждой перекомпиляции — красим/прячем заново
            EditorApplication.delayCall += Restyle;
        }

        /// <summary>
        /// Middle — блок Play/Pause/Step; defaultDockIndex = -3 ставит кнопку левее TODO(-1)/Flavor(-2).
        /// Позицию можно поменять перетаскиванием — Unity запомнит.
        /// </summary>
        [MainToolbarElement(ElementId,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = -3,
            ussName = UssName)]
        private static MainToolbarElement CreateElement()
        {
            var content = new MainToolbarContent(
                "📸 Языки",
                "Снять ТЕКУЩИЙ экран на всех языках (только Play Mode).\n" +
                "Открой нужное окно заранее. Папка и шаблон имени файла — в Capture Config.");

            var button = new MainToolbarButton(content, RunCapture);
            EditorApplication.delayCall += Restyle;
            return button;
        }

        private static void RunCapture()
        {
            var sys = CaptureSystem.Instance;
            if (sys == null)
            {
                EditorUtility.DisplayDialog("Скриншоты по языкам",
                    "Нужен CaptureSystem в сцене и запущенный Play Mode.", "Ок");
                return;
            }
            // CaptureAllLanguages сам проверит Play Mode / готовность локали / повторный запуск и залогирует
            sys.CaptureAllLanguages();
        }

        /// <summary>
        /// Красим или прячем кнопку (та же механика поиска в окне тулбара, что у TODO/MCP:
        /// MainToolbarButton не даёт доступа к VisualElement — ищем по ussName в дереве окна).
        /// </summary>
        private static void Restyle()
        {
            var windowProp = typeof(MainToolbar).GetProperty("window",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (windowProp?.GetValue(null) is not EditorWindow window) return;

            var root = window.rootVisualElement;
            var button = root?.Q(className: UssName) ?? root?.Q(name: UssName);
            if (button == null) return;

            button.style.display = ShowButton ? DisplayStyle.Flex : DisplayStyle.None;
            if (!ShowButton) return;

            button.style.backgroundColor = Accent;
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
        }
    }
}

#endif
