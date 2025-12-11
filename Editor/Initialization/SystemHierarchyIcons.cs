#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;
using ProtoSystem;

namespace ProtoSystem.ProEditor
{
    /// <summary>
    /// Отображает иконки в иерархии для объектов с компонентами, реализующими IInitializableSystem
    /// </summary>
    [InitializeOnLoad]
    public static class SystemHierarchyIcons
    {
        // Настройки отображения
        private static bool showIcons = true;
        private static bool showStatusText = true;
        private static bool showOnlyInPlayMode = false;

        // Кэш для оптимизации
        private static System.Collections.Generic.Dictionary<int, SystemInfo> systemCache =
            new System.Collections.Generic.Dictionary<int, SystemInfo>();

        // Информация о системе для кэширования
        private struct SystemInfo
        {
            public bool hasSystem;
            public InitializationStatus status;
            public string systemName;
            public bool isEnabled;
            public int componentCount;
            public float lastUpdateTime;
        }

        // Цвета для разных статусов
        private static readonly Color NotStartedColor = new Color(0.7f, 0.7f, 0.7f, 1f);      // Серый
        private static readonly Color InProgressColor = new Color(1f, 0.8f, 0f, 1f);          // Оранжевый  
        private static readonly Color CompletedColor = new Color(0.2f, 0.8f, 0.2f, 1f);       // Зеленый
        private static readonly Color FailedColor = new Color(1f, 0.2f, 0.2f, 1f);            // Красный
        private static readonly Color DisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);      // Темно-серый

        static SystemHierarchyIcons()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            // Добавляем пункт меню для настроек
            Menu.SetChecked(MenuPath, showIcons);
        }

        private const string MenuPath = "Tools/System Icons/Show System Icons in Hierarchy";
        private const string StatusTextMenuPath = "Tools/System Icons/Show Status Text";
        private const string PlayModeOnlyMenuPath = "Tools/System Icons/Show Only in Play Mode";

        [MenuItem(MenuPath)]
        private static void ToggleSystemIcons()
        {
            showIcons = !showIcons;
            Menu.SetChecked(MenuPath, showIcons);
            EditorApplication.RepaintHierarchyWindow();

            // Сохраняем настройку
            EditorPrefs.SetBool("SystemHierarchyIcons.ShowIcons", showIcons);
        }

        [MenuItem(StatusTextMenuPath)]
        private static void ToggleStatusText()
        {
            showStatusText = !showStatusText;
            Menu.SetChecked(StatusTextMenuPath, showStatusText);
            EditorApplication.RepaintHierarchyWindow();

            EditorPrefs.SetBool("SystemHierarchyIcons.ShowStatusText", showStatusText);
        }

        [MenuItem(PlayModeOnlyMenuPath)]
        private static void TogglePlayModeOnly()
        {
            showOnlyInPlayMode = !showOnlyInPlayMode;
            Menu.SetChecked(PlayModeOnlyMenuPath, showOnlyInPlayMode);
            EditorApplication.RepaintHierarchyWindow();

            EditorPrefs.SetBool("SystemHierarchyIcons.PlayModeOnly", showOnlyInPlayMode);
        }

        [MenuItem("Tools/System Icons/Clear Icon Cache")]
        private static void ClearCache()
        {
            systemCache.Clear();
            EditorApplication.RepaintHierarchyWindow();
            Debug.Log("Кэш иконок систем очищен");
        }

        [MenuItem("Tools/System Icons/Settings")]
        private static void ShowSettings()
        {
            SystemIconsSettingsWindow.ShowWindow();
        }

        // Загружаем настройки при инициализации
        [InitializeOnLoadMethod]
        private static void LoadSettings()
        {
            showIcons = EditorPrefs.GetBool("SystemHierarchyIcons.ShowIcons", true);
            showStatusText = EditorPrefs.GetBool("SystemHierarchyIcons.ShowStatusText", true);
            showOnlyInPlayMode = EditorPrefs.GetBool("SystemHierarchyIcons.PlayModeOnly", false);

            Menu.SetChecked(MenuPath, showIcons);
            Menu.SetChecked(StatusTextMenuPath, showStatusText);
            Menu.SetChecked(PlayModeOnlyMenuPath, showOnlyInPlayMode);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Очищаем кэш при смене режима игры
            systemCache.Clear();
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            if (!showIcons) return;

            if (showOnlyInPlayMode && !Application.isPlaying) return;

            GameObject gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject == null) return;

            // Используем кэш для оптимизации
            SystemInfo systemInfo = GetSystemInfo(gameObject, instanceID);

            if (!systemInfo.hasSystem) return;

            // Рассчитываем позицию для иконки
            Rect iconRect = new Rect(selectionRect.xMax - 20, selectionRect.y, 16, 16);

            // Рисуем иконку в зависимости от статуса
            DrawSystemIcon(iconRect, systemInfo);

            // Рисуем дополнительную информацию, если включено
            if (showStatusText && systemInfo.componentCount > 0)
            {
                DrawSystemStatusText(selectionRect, systemInfo);
            }

            // Рисуем счетчик систем, если их несколько
            if (systemInfo.componentCount > 1)
            {
                DrawSystemCounter(selectionRect, systemInfo.componentCount);
            }
        }

        private static SystemInfo GetSystemInfo(GameObject gameObject, int instanceID)
        {
            // Проверяем кэш
            float currentTime = Time.realtimeSinceStartup;
            if (systemCache.TryGetValue(instanceID, out SystemInfo cached))
            {
                // Обновляем кэш каждые 0.5 секунды в play mode или каждые 2 секунды в edit mode
                float updateInterval = Application.isPlaying ? 0.5f : 2f;
                if (currentTime - cached.lastUpdateTime < updateInterval)
                {
                    return cached;
                }
            }

            // Обновляем информацию о системе
            SystemInfo info = new SystemInfo();
            info.lastUpdateTime = currentTime;

            // Ищем компоненты, реализующие IInitializableSystem
            var systems = gameObject.GetComponents<MonoBehaviour>()
                .Where(c => c is IInitializableSystem)
                .Cast<IInitializableSystem>()
                .ToArray();

            info.hasSystem = systems != null && systems.Length > 0;
            info.componentCount = systems?.Length ?? 0;

            if (info.hasSystem)
            {
                // Берем первую систему для отображения статуса
                var primarySystem = systems[0];
                info.status = primarySystem.Status;
                info.systemName = primarySystem.DisplayName;
                info.isEnabled = primarySystem.enabled;

                // Если есть несколько систем, показываем наихудший статус
                if (systems.Length > 1)
                {
                    foreach (var system in systems)
                    {
                        if (system.Status == InitializationStatus.Failed)
                        {
                            info.status = InitializationStatus.Failed;
                            break;
                        }
                        else if (system.Status == InitializationStatus.InProgress)
                        {
                            info.status = InitializationStatus.InProgress;
                        }
                    }
                }
            }

            // Сохраняем в кэш
            systemCache[instanceID] = info;
            return info;
        }

        private static void DrawSystemIcon(Rect iconRect, SystemInfo systemInfo)
        {
            Color iconColor = GetStatusColor(systemInfo.status, systemInfo.isEnabled);
            string iconSymbol = GetStatusSymbol(systemInfo.status);

            // Создаем стиль для иконки
            GUIStyle iconStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = iconColor }
            };

            // Рисуем фон для иконки
            Color bgColor = iconColor;
            bgColor.a = 0.2f;
            EditorGUI.DrawRect(iconRect, bgColor);

            // Рисуем символ
            GUI.Label(iconRect, iconSymbol, iconStyle);

            // Добавляем рамку
            Color borderColor = iconColor;
            borderColor.a = 0.8f;
            DrawRectBorder(iconRect, borderColor, 1);
        }

        private static void DrawSystemStatusText(Rect selectionRect, SystemInfo systemInfo)
        {
            if (string.IsNullOrEmpty(systemInfo.systemName)) return;

            // Позиция для текста статуса
            Rect textRect = new Rect(selectionRect.xMax - 200, selectionRect.y, 175, selectionRect.height);

            Color textColor = GetStatusColor(systemInfo.status, systemInfo.isEnabled);
            textColor.a = 0.7f;

            GUIStyle textStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 9,
                fontStyle = FontStyle.Italic,
                normal = { textColor = textColor }
            };

            string statusText = GetStatusText(systemInfo.status);
            GUI.Label(textRect, statusText, textStyle);
        }

        private static void DrawSystemCounter(Rect selectionRect, int count)
        {
            // Позиция для счетчика
            Rect counterRect = new Rect(selectionRect.xMax - 40, selectionRect.y + selectionRect.height - 12, 18, 10);

            GUIStyle counterStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            // Фон для счетчика
            EditorGUI.DrawRect(counterRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));

            GUI.Label(counterRect, count.ToString(), counterStyle);
        }

        private static Color GetStatusColor(InitializationStatus status, bool isEnabled)
        {
            if (!isEnabled) return DisabledColor;

            switch (status)
            {
                case InitializationStatus.NotStarted:
                    return NotStartedColor;
                case InitializationStatus.InProgress:
                    return InProgressColor;
                case InitializationStatus.Completed:
                    return CompletedColor;
                case InitializationStatus.Failed:
                    return FailedColor;
                default:
                    return NotStartedColor;
            }
        }

        private static string GetStatusSymbol(InitializationStatus status)
        {
            switch (status)
            {
                case InitializationStatus.NotStarted:
                    return "⭕";
                case InitializationStatus.InProgress:
                    return "⏳";
                case InitializationStatus.Completed:
                    return "✅";
                case InitializationStatus.Failed:
                    return "❌";
                default:
                    return "❓";
            }
        }

        private static string GetStatusText(InitializationStatus status)
        {
            switch (status)
            {
                case InitializationStatus.NotStarted:
                    return "не запущена";
                case InitializationStatus.InProgress:
                    return "инициализация...";
                case InitializationStatus.Completed:
                    return "готова";
                case InitializationStatus.Failed:
                    return "ошибка";
                default:
                    return "неизвестно";
            }
        }

        private static void DrawRectBorder(Rect rect, Color color, int thickness)
        {
            // Верхняя линия
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            // Нижняя линия  
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
            // Левая линия
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            // Правая линия
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), color);
        }
    }

    /// <summary>
    /// Окно настроек для иконок системы в иерархии
    /// </summary>
    public class SystemIconsSettingsWindow : EditorWindow
    {
        private bool showIcons = true;
        private bool showStatusText = true;
        private bool showOnlyInPlayMode = false;

        public static void ShowWindow()
        {
            var window = GetWindow<SystemIconsSettingsWindow>("Настройки иконок систем");
            window.minSize = new Vector2(300, 200);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            showIcons = EditorPrefs.GetBool("SystemHierarchyIcons.ShowIcons", true);
            showStatusText = EditorPrefs.GetBool("SystemHierarchyIcons.ShowStatusText", true);
            showOnlyInPlayMode = EditorPrefs.GetBool("SystemHierarchyIcons.PlayModeOnly", false);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("⚙️ Настройки отображения иконок систем", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginVertical("Box");

            EditorGUI.BeginChangeCheck();

            showIcons = EditorGUILayout.Toggle(new GUIContent("🎯 Показывать иконки",
                "Отображать иконки статуса рядом с объектами, содержащими системы"), showIcons);

            GUI.enabled = showIcons;

            showStatusText = EditorGUILayout.Toggle(new GUIContent("📝 Показывать текст статуса",
                "Отображать текстовое описание статуса системы"), showStatusText);

            showOnlyInPlayMode = EditorGUILayout.Toggle(new GUIContent("🎮 Только в режиме игры",
                "Показывать иконки только когда игра запущена"), showOnlyInPlayMode);

            GUI.enabled = true;

            if (EditorGUI.EndChangeCheck())
            {
                SaveSettings();
                EditorApplication.RepaintHierarchyWindow();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Информация о статусах
            EditorGUILayout.LabelField("ℹ️ Обозначения статусов:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("Box");

            EditorGUILayout.LabelField("⭕ Серый - система не запущена");
            EditorGUILayout.LabelField("⏳ Оранжевый - инициализация в процессе");
            EditorGUILayout.LabelField("✅ Зеленый - система готова");
            EditorGUILayout.LabelField("❌ Красный - ошибка инициализации");

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Кнопки действий
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🔄 Обновить иконки"))
            {
                // Принудительно обновляем иерархию
                EditorApplication.RepaintHierarchyWindow();
            }

            if (GUILayout.Button("🗑️ Очистить кэш"))
            {
                EditorApplication.delayCall += () =>
                {
                    EditorApplication.ExecuteMenuItem("Tools/System Icons/Clear Icon Cache");
                };
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "💡 Совет: Иконки автоматически обновляются во время работы игры. " +
                "В режиме редактора обновление происходит реже для оптимизации производительности.",
                MessageType.Info);
        }

        private void SaveSettings()
        {
            EditorPrefs.SetBool("SystemHierarchyIcons.ShowIcons", showIcons);
            EditorPrefs.SetBool("SystemHierarchyIcons.ShowStatusText", showStatusText);
            EditorPrefs.SetBool("SystemHierarchyIcons.PlayModeOnly", showOnlyInPlayMode);

            // Обновляем меню
            Menu.SetChecked("Tools/System Icons/Show System Icons in Hierarchy", showIcons);
            Menu.SetChecked("Tools/System Icons/Show Status Text", showStatusText);
            Menu.SetChecked("Tools/System Icons/Show Only in Play Mode", showOnlyInPlayMode);
        }
    }
}
#endif