// Packages/com.protosystem.core/Editor/UI/UIToolkitWindowGenerator.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using PStyles = ProtoSystem.Editor.ProtoEditorStyles;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Wizard + генератор окон на UI Toolkit.
    /// ProtoSystem → UI → UI Toolkit Setup & Generator
    ///
    /// Шаг 1 (Setup): создаёт PanelSettings-шаблон и прописывает его в UISystemConfig —
    ///   это «включение» UI Toolkit для UISystem (фабрика клонирует шаблон по слоям).
    /// Шаг 2 (Generate): генерирует выбранные базовые окна В ПРОЕКТ — C#-класс
    ///   (наследник UIToolkitWindowBase с теми же WindowId/переходами, что у uGUI-версий),
    ///   UXML с конвенцией локализации «#ключ» и общий USS. После компиляции автоматически
    ///   создаются префабы (UIDocument + компонент окна) с меткой UIWindow для авто-скана.
    ///
    /// Дальше UXML/USS правятся руками — перегенерация не нужна.
    /// </summary>
    public class UIToolkitWindowGenerator : EditorWindow
    {
        private const string PendingPrefabsKey = "ProtoSystem.UIToolkit.PendingPrefabs";

        private string _outputPath = "Assets/UI/Toolkit";
        private Vector2 _scroll;

        private UISystemConfig _config;

        private static readonly string[] WindowNames =
            { "MainMenu", "PauseMenu", "Settings", "Loading", "GameOver", "Credits", "GameHUD" };
        private bool[] _windowToggles;

        [MenuItem("ProtoSystem/UI/UI Toolkit Setup && Generator", false, 201)]
        public static void ShowWindow()
        {
            var window = GetWindow<UIToolkitWindowGenerator>("UI Toolkit");
            window.minSize = new Vector2(440, 480);
            window.Show();
        }

        private void OnEnable()
        {
            _outputPath = EditorPrefs.GetString("ProtoSystem.UIToolkit.OutputPath", "Assets/UI/Toolkit");
            _config = FindConfig();
            _windowToggles ??= Enumerable.Repeat(true, WindowNames.Length).ToArray();
        }

        private void OnDisable()
        {
            EditorPrefs.SetString("ProtoSystem.UIToolkit.OutputPath", _outputPath);
        }

        private void OnGUI()
        {
            PStyles.Header("🧩 UI Toolkit для UISystem",
                "Окна на UXML/USS живут в общем графе с uGUI-окнами: стек, модалки, геймпад, локализация.");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawSetupCard();
            DrawGenerateCard();
            DrawScaffoldHintCard();

            EditorGUILayout.EndScrollView();
        }

        // ──────────────── Setup ────────────────

        private void DrawSetupCard()
        {
            PStyles.BeginCard("1. Установка (PanelSettings)");

            _config = (UISystemConfig)EditorGUILayout.ObjectField("UISystemConfig", _config,
                typeof(UISystemConfig), false);

            if (_config == null)
            {
                EditorGUILayout.HelpBox("UISystemConfig не найден. Создайте его (Create → ProtoSystem → UI → System Config).",
                    MessageType.Warning);
                PStyles.EndCard();
                return;
            }

            bool ready = _config.panelSettings != null;

            EditorGUILayout.BeginHorizontal();
            PStyles.DrawBadge(ready ? "✓" : "—", ready ? PStyles.Ok : PStyles.Warn, 24);
            EditorGUILayout.LabelField(ready
                    ? $"UI Toolkit включён: {_config.panelSettings.name}"
                    : "UI Toolkit не настроен — нужен шаблон PanelSettings в конфиге.",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (!ready && PStyles.AccentButton("⚙️ Установить UI Toolkit (создать PanelSettings)", PStyles.Accent))
            {
                SetupPanelSettings();
            }

            PStyles.EndCard();
        }

        private void SetupPanelSettings()
        {
            EnsureFolder(_outputPath);

            // Тема: ищем существующую, иначе просим создать стандартную
            var themeGuid = AssetDatabase.FindAssets("t:ThemeStyleSheet").FirstOrDefault();
            ThemeStyleSheet theme = null;
            if (!string.IsNullOrEmpty(themeGuid))
                theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(themeGuid));

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            if (theme != null) panel.themeStyleSheet = theme;

            string path = $"{_outputPath}/PanelSettings_Proto.asset";
            AssetDatabase.CreateAsset(panel, path);

            _config.panelSettings = panel;
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();

            if (theme == null)
            {
                EditorUtility.DisplayDialog("Нужна тема",
                    "PanelSettings создан, но в проекте нет ThemeStyleSheet.\n\n" +
                    "Создайте: Assets → Create → UI Toolkit → Default Runtime Theme,\n" +
                    "затем назначьте её в PanelSettings_Proto (поле Theme Style Sheet).", "OK");
            }

            ProtoLogger.Log("UISystem", LogCategory.Initialization, LogLevel.Info,
                $"UI Toolkit установлен: {path} → UISystemConfig.panelSettings");
        }

        // ──────────────── Generate base windows ────────────────

        private void DrawGenerateCard()
        {
            PStyles.BeginCard("2. Базовые окна (генерация в проект)");

            EditorGUILayout.LabelField(
                "Классы наследуют UIToolkitWindowBase, WindowId и переходы совпадают с uGUI-версиями.\n" +
                "В UISystemConfig регистрируйте ЛИБО uGUI-, ЛИБО toolkit-префаб каждого окна.",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(3);
            _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);

            EditorGUILayout.Space(3);
            int col = 0;
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < WindowNames.Length; i++)
            {
                _windowToggles[i] = EditorGUILayout.ToggleLeft(WindowNames[i], _windowToggles[i], GUILayout.Width(100));
                if (++col % 4 == 0) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            bool any = _windowToggles.Any(t => t);
            if (PStyles.AccentButton("🧩 Сгенерировать окна (UXML + C# + префабы)", PStyles.Accent, any))
            {
                GenerateSelected();
            }

            // Ожидающие префабы (после компиляции)
            var pending = UnityEditor.SessionState.GetString(PendingPrefabsKey, "");
            if (!string.IsNullOrEmpty(pending))
            {
                EditorGUILayout.HelpBox("Ожидается компиляция — префабы будут созданы автоматически после неё.",
                    MessageType.Info);
            }

            PStyles.EndCard();
        }

        private void DrawScaffoldHintCard()
        {
            PStyles.BeginCard("Свои окна");
            EditorGUILayout.LabelField(
                "Новое окно = класс-наследник UIToolkitWindowBase с [UIWindow(...)] + UXML/USS + префаб\n" +
                "(GameObject → UIDocument с вашим UXML → компонент окна; PanelSettings можно не задавать —\n" +
                "фабрика назначит слой автоматически). Тексты в UXML: text=\"#ваш.лок.ключ\".\n" +
                "Метка префаба UIWindow — для авто-скана в UISystemConfig.",
                EditorStyles.miniLabel);
            PStyles.EndCard();
        }

        private void GenerateSelected()
        {
            string ns = GetProjectNamespace();
            EnsureFolder(_outputPath);
            EnsureFolder($"{_outputPath}/UXML");
            EnsureFolder($"{_outputPath}/Scripts");
            EnsureFolder($"{_outputPath}/Prefabs");

            // Общий USS
            WriteIfMissing($"{_outputPath}/UXML/ProtoWindow.uss", SharedUss);

            var pending = new List<string>();

            for (int i = 0; i < WindowNames.Length; i++)
            {
                if (!_windowToggles[i]) continue;
                string name = WindowNames[i];

                var t = Templates[name];
                string className = $"{name}ToolkitWindow";

                string uxmlPath = $"{_outputPath}/UXML/{name}.uxml";
                WriteIfMissing(uxmlPath, t.uxml);

                string csPath = $"{_outputPath}/Scripts/{className}.cs";
                WriteIfMissing(csPath, BuildClassCode(ns, className, t));

                // Префаб создадим после компиляции класса
                pending.Add($"{ns}.{className}|{_outputPath}/Prefabs/{className}.prefab|{uxmlPath}|{t.defaultFocus}");
            }

            UnityEditor.SessionState.SetString(PendingPrefabsKey, string.Join(";", pending));
            AssetDatabase.Refresh();

            // Если классы уже существовали (перегенерация) — компиляции не будет,
            // создаём префабы сразу; иначе доделает InitializeOnLoadMethod после reload.
            if (!EditorApplication.isCompiling)
                ProcessPendingPrefabs();

            ProtoLogger.Log("UISystem", LogCategory.Initialization, LogLevel.Info,
                $"UI Toolkit: сгенерировано окон: {pending.Count}. Префабы будут созданы после компиляции.");
        }

        // ──────────────── Prefab creation (после компиляции) ────────────────

        [InitializeOnLoadMethod]
        private static void ProcessPendingPrefabs()
        {
            EditorApplication.delayCall += () =>
            {
                var pending = UnityEditor.SessionState.GetString(PendingPrefabsKey, "");
                if (string.IsNullOrEmpty(pending)) return;

                if (EditorApplication.isCompiling) return; // придём сюда снова после компиляции

                UnityEditor.SessionState.EraseString(PendingPrefabsKey);
                int created = 0;

                foreach (var entry in pending.Split(';'))
                {
                    var parts = entry.Split('|');
                    if (parts.Length < 4) continue;

                    string typeName = parts[0], prefabPath = parts[1], uxmlPath = parts[2], focus = parts[3];

                    var type = TypeCache.GetTypesDerivedFrom<UIToolkitWindowBase>()
                        .FirstOrDefault(x => x.FullName == typeName);
                    if (type == null)
                    {
                        Debug.LogWarning($"[UIToolkit] Тип {typeName} не найден после компиляции — префаб не создан. " +
                                         "Проверьте ошибки компиляции и перезапустите генерацию.");
                        continue;
                    }

                    var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

                    var go = new GameObject(type.Name);
                    var doc = go.AddComponent<UIDocument>();
                    doc.visualTreeAsset = uxml;
                    // panelSettings не задаём — UIWindowFactory назначит по WindowLayer

                    var component = go.AddComponent(type);
                    if (!string.IsNullOrEmpty(focus))
                    {
                        var so = new SerializedObject(component);
                        var prop = so.FindProperty("defaultFocusName");
                        if (prop != null) { prop.stringValue = focus; so.ApplyModifiedPropertiesWithoutUndo(); }
                    }

                    var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    UnityEngine.Object.DestroyImmediate(go);

                    if (prefab != null)
                    {
                        AssetDatabase.SetLabels(prefab, new[] { "UIWindow" });
                        created++;
                    }
                }

                if (created > 0)
                {
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[UIToolkit] Создано префабов окон: {created}. " +
                              "Добавьте их в UISystemConfig (Scan & Add Prefabs) вместо uGUI-версий.");
                }
            };
        }

        // ──────────────── Шаблоны ────────────────

        private struct WindowTemplate
        {
            public string attrs;        // [UIWindow]/[UITransition] строки
            public string bindings;     // тело OnBuildUI
            public string extra;        // дополнительные члены класса
            public string uxml;
            public string defaultFocus;
        }

        private static string BuildClassCode(string ns, string className, WindowTemplate t) =>
$@"// Сгенерировано ProtoSystem UI Toolkit Generator. Правьте свободно — перегенерация не перезапишет файл.
using UnityEngine;
using UnityEngine.UIElements;
using ProtoSystem.UI;

namespace {ns}
{{
{t.attrs}
    public class {className} : UIToolkitWindowBase
    {{
        protected override void OnBuildUI(VisualElement root)
        {{
{t.bindings}
        }}
{t.extra}
    }}
}}
";

        private static string Uxml(string rootClass, string body) =>
$@"<ui:UXML xmlns:ui=""UnityEngine.UIElements"">
    <Style src=""ProtoWindow.uss"" />
    <ui:VisualElement name=""window-root"" class=""window-root {rootClass}"">
{body}
    </ui:VisualElement>
</ui:UXML>
";

        private static readonly Dictionary<string, WindowTemplate> Templates = new()
        {
            ["MainMenu"] = new WindowTemplate
            {
                attrs =
@"    [UIWindow(""MainMenu"", WindowType.Normal, WindowLayer.Windows)]
    [UITransition(""play"", ""GameHUD"")]
    [UITransition(""settings"", ""Settings"")]
    [UITransition(""credits"", ""Credits"")]",
                bindings =
@"            root.Q<Button>(""play-button"")?.RegisterCallback<ClickEvent>(_ => Navigate(""play""));
            root.Q<Button>(""settings-button"")?.RegisterCallback<ClickEvent>(_ => Navigate(""settings""));
            root.Q<Button>(""credits-button"")?.RegisterCallback<ClickEvent>(_ => Navigate(""credits""));
            root.Q<Button>(""quit-button"")?.RegisterCallback<ClickEvent>(_ => Quit());

            var version = root.Q<Label>(""version"");
            if (version != null) version.text = $""v{Application.version}"";",
                extra =
@"
        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }",
                defaultFocus = "play-button",
                uxml = Uxml("mainmenu",
@"        <ui:Label name=""title"" class=""window-title"" text=""#ui.mainmenu.title"" />
        <ui:VisualElement name=""buttons"" class=""menu-buttons"">
            <ui:Button name=""play-button"" class=""menu-button"" text=""#ui.common.play"" />
            <ui:Button name=""settings-button"" class=""menu-button"" text=""#ui.common.settings"" />
            <ui:Button name=""credits-button"" class=""menu-button"" text=""#ui.common.credits"" />
            <ui:Button name=""quit-button"" class=""menu-button"" text=""#ui.common.quit"" />
        </ui:VisualElement>
        <ui:Label name=""version"" class=""version-label"" text=""v0.0"" />")
            },

            ["PauseMenu"] = new WindowTemplate
            {
                attrs =
@"    [UIWindow(""PauseMenu"", WindowType.Normal, WindowLayer.Windows, Level = 1, PauseGame = true, ShowInGraph = false)]
    [UITransition(""settings"", ""Settings"")]
    [UITransition(""mainmenu"", ""MainMenu"")]",
                bindings =
@"            root.Q<Button>(""resume-button"")?.RegisterCallback<ClickEvent>(_ => Close());
            root.Q<Button>(""settings-button"")?.RegisterCallback<ClickEvent>(_ => Navigate(""settings""));
            root.Q<Button>(""mainmenu-button"")?.RegisterCallback<ClickEvent>(_ => Navigate(""mainmenu""));",
                extra = "",
                defaultFocus = "resume-button",
                uxml = Uxml("pausemenu",
@"        <ui:Label name=""title"" class=""window-title"" text=""#ui.pause.title"" />
        <ui:VisualElement name=""buttons"" class=""menu-buttons"">
            <ui:Button name=""resume-button"" class=""menu-button"" text=""#ui.pause.resume"" />
            <ui:Button name=""settings-button"" class=""menu-button"" text=""#ui.common.settings"" />
            <ui:Button name=""mainmenu-button"" class=""menu-button"" text=""#ui.pause.mainmenu"" />
        </ui:VisualElement>")
            },

            ["Settings"] = new WindowTemplate
            {
                attrs =
@"    [UIWindow(""Settings"", WindowType.Normal, WindowLayer.Windows, Level = 1, PauseGame = true, CursorMode = WindowCursorMode.Visible)]",
                bindings =
@"            root.Q<Button>(""back-button"")?.RegisterCallback<ClickEvent>(_ => Close());
            // TODO: постройте контролы настроек в ""settings-content"" (SettingsSystem)",
                extra = "",
                defaultFocus = "back-button",
                uxml = Uxml("settings",
@"        <ui:Label name=""title"" class=""window-title"" text=""#ui.settings.title"" />
        <ui:ScrollView name=""settings-content"" class=""settings-content"" />
        <ui:Button name=""back-button"" class=""menu-button"" text=""#ui.common.back"" />")
            },

            ["Loading"] = new WindowTemplate
            {
                attrs =
@"    [UIWindow(""Loading"", WindowType.Overlay, WindowLayer.Loading)]",
                bindings =
@"            _progressBar = root.Q<ProgressBar>(""progress"");
            _statusLabel = root.Q<Label>(""status"");",
                extra =
@"
        private ProgressBar _progressBar;
        private Label _statusLabel;

        public void SetProgress(float progress01)
        {
            if (_progressBar != null) _progressBar.value = Mathf.Clamp01(progress01) * 100f;
        }

        public void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.text = text;
        }",
                defaultFocus = "",
                uxml = Uxml("loading",
@"        <ui:Label name=""title"" class=""window-title"" text=""#ui.loading.title"" />
        <ui:ProgressBar name=""progress"" class=""loading-progress"" value=""0"" high-value=""100"" />
        <ui:Label name=""status"" class=""loading-status"" text="""" />")
            },

            ["GameOver"] = new WindowTemplate
            {
                attrs =
@"    [UIWindow(""GameOver"", WindowType.Normal, WindowLayer.Windows, Level = 1, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
    [UITransition(""mainmenu"", ""MainMenu"")]
    [UITransition(""restart"", ""GameHUD"")]",
                bindings =
@"            root.Q<Button>(""restart-button"")?.RegisterCallback<ClickEvent>(_ => Navigate(""restart""));
            root.Q<Button>(""mainmenu-button"")?.RegisterCallback<ClickEvent>(_ => Navigate(""mainmenu""));",
                extra = "",
                defaultFocus = "restart-button",
                uxml = Uxml("gameover",
@"        <ui:Label name=""title"" class=""window-title"" text=""#ui.gameover.title"" />
        <ui:VisualElement name=""buttons"" class=""menu-buttons"">
            <ui:Button name=""restart-button"" class=""menu-button"" text=""#ui.gameover.restart"" />
            <ui:Button name=""mainmenu-button"" class=""menu-button"" text=""#ui.pause.mainmenu"" />
        </ui:VisualElement>")
            },

            ["Credits"] = new WindowTemplate
            {
                attrs =
@"    [UIWindow(""Credits"", WindowType.Normal, WindowLayer.Windows, Level = 1)]",
                bindings =
@"            root.Q<Button>(""back-button"")?.RegisterCallback<ClickEvent>(_ => Close());",
                extra = "",
                defaultFocus = "back-button",
                uxml = Uxml("credits",
@"        <ui:Label name=""title"" class=""window-title"" text=""#ui.credits.title"" />
        <ui:ScrollView name=""credits-content"" class=""credits-content"">
            <ui:Label name=""credits-text"" class=""credits-text"" text=""#ui.credits.text"" />
        </ui:ScrollView>
        <ui:Button name=""back-button"" class=""menu-button"" text=""#ui.common.back"" />")
            },

            ["GameHUD"] = new WindowTemplate
            {
                attrs =
@"    [UIWindow(""GameHUD"", WindowType.Normal, WindowLayer.HUD, Level = 0, ShowInGraph = false)]
    [UITransition(""pause"", ""PauseMenu"")]",
                bindings =
@"            root.Q<Button>(""pause-button"")?.RegisterCallback<ClickEvent>(_ => Navigate(""pause""));
            // TODO: постройте HUD в ""hud-root""",
                extra =
@"
        public override void OnBackPressed()
        {
            Navigate(""pause"");
        }",
                defaultFocus = "",
                uxml = Uxml("gamehud",
@"        <ui:VisualElement name=""hud-root"" class=""hud-root"">
            <ui:Button name=""pause-button"" class=""hud-pause-button"" text=""II"" />
        </ui:VisualElement>")
            },
        };

        private const string SharedUss =
@"/* ProtoSystem UI Toolkit — базовые стили окон. Правьте под проект. */

.window-root {
    flex-grow: 1;
    align-items: center;
    justify-content: center;
    background-color: rgba(12, 14, 18, 0.92);
}

.window-title {
    font-size: 42px;
    -unity-font-style: bold;
    color: rgb(235, 235, 235);
    margin-bottom: 32px;
}

.menu-buttons {
    align-items: stretch;
    min-width: 320px;
}

.menu-button {
    height: 48px;
    margin: 6px 0;
    font-size: 20px;
    color: rgb(230, 230, 230);
    background-color: rgba(255, 255, 255, 0.06);
    border-width: 1px;
    border-color: rgba(255, 255, 255, 0.15);
    border-radius: 6px;
}

.menu-button:hover {
    background-color: rgba(255, 255, 255, 0.12);
}

/* ВИДИМЫЙ ФОКУС — обязателен для геймпада/Steam Deck */
.menu-button:focus,
.hud-pause-button:focus,
Button:focus {
    border-color: rgb(66, 138, 245);
    border-width: 2px;
    background-color: rgba(66, 138, 245, 0.18);
}

.version-label {
    position: absolute;
    bottom: 12px;
    right: 16px;
    font-size: 12px;
    color: rgba(255, 255, 255, 0.4);
}

/* Состояния, которые вешает UIToolkitWindowBase */
.window-blurred {
    opacity: 0.6;
}

.window-opaque {
    background-color: rgb(12, 14, 18);
}

/* HUD */
.hud-root {
    flex-grow: 1;
    align-self: stretch;
}

.hud-pause-button {
    position: absolute;
    top: 16px;
    right: 16px;
    width: 44px;
    height: 44px;
}

/* Loading */
.loading-progress { min-width: 420px; margin-top: 12px; }
.loading-status   { margin-top: 8px; color: rgba(255,255,255,0.6); }

/* Credits / Settings */
.credits-content, .settings-content { max-height: 60%; min-width: 480px; margin-bottom: 16px; }
.credits-text { white-space: normal; color: rgb(220,220,220); }

/* Шрифты по языкам: UIToolkitWindowBase вешает lang-{code} на корень.
   Пример: .lang-ja .window-title { -unity-font-definition: url(...); } */
";

        // ──────────────── Helpers ────────────────

        private static string GetProjectNamespace()
        {
            var projectConfig = Resources.Load<ProjectConfig>("ProjectConfig");
            if (projectConfig != null && !string.IsNullOrEmpty(projectConfig.projectNamespace))
                return projectConfig.projectNamespace + ".UI";
            return "Game.UI";
        }

        private static UISystemConfig FindConfig()
        {
            var guid = AssetDatabase.FindAssets("t:UISystemConfig").FirstOrDefault();
            return string.IsNullOrEmpty(guid)
                ? null
                : AssetDatabase.LoadAssetAtPath<UISystemConfig>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        private static void WriteIfMissing(string path, string content)
        {
            if (File.Exists(path))
            {
                Debug.Log($"[UIToolkit] {path} уже существует — пропущен (правки не перезаписываются).");
                return;
            }
            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        }
    }
}
