// Packages/com.protosystem.core/Editor/Screenshot/ScreenshotEditorWindow.cs
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;

namespace ProtoSystem.Editor
{
    // ==========================================
    // Toolbar Buttons
    // ==========================================

    [EditorToolbarElement(Id, typeof(SceneView))]
    public class ScreenshotWithUIButton : EditorToolbarButton
    {
        public const string Id = "ProtoSystem/ScreenshotWithUI";

        public ScreenshotWithUIButton()
        {
            text = "📸 +UI";
            tooltip = "Screenshot with UI (Game View) — F12";
            clicked += () => ScreenshotEditorCapture.CaptureFromEditor(includeUI: true);

            style.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
            style.color = Color.white;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            style.borderTopLeftRadius = 4;
            style.borderTopRightRadius = 0;
            style.borderBottomLeftRadius = 4;
            style.borderBottomRightRadius = 0;
            style.paddingLeft = 8;
            style.paddingRight = 6;
            style.paddingTop = 2;
            style.paddingBottom = 2;
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    public class ScreenshotCleanButton : EditorToolbarButton
    {
        public const string Id = "ProtoSystem/ScreenshotClean";

        public ScreenshotCleanButton()
        {
            text = "📸 −UI";
            tooltip = "Screenshot without UI (Game View) — Shift+F12";
            clicked += () => ScreenshotEditorCapture.CaptureFromEditor(includeUI: false);

            style.backgroundColor = new Color(0.15f, 0.45f, 0.7f);
            style.color = Color.white;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            style.borderTopLeftRadius = 0;
            style.borderTopRightRadius = 4;
            style.borderBottomLeftRadius = 0;
            style.borderBottomRightRadius = 4;
            style.paddingLeft = 6;
            style.paddingRight = 8;
            style.paddingTop = 2;
            style.paddingBottom = 2;
        }
    }

    // ==========================================
    // SceneView Overlay
    // ==========================================

    [Overlay(typeof(SceneView), "Screenshot", defaultDisplay = true)]
    public class ScreenshotSceneOverlay : ToolbarOverlay
    {
        ScreenshotSceneOverlay() : base(
            ScreenshotWithUIButton.Id,
            ScreenshotCleanButton.Id
        ) { }
    }

    // ==========================================
    // Menu Items
    // ==========================================

    public static class ScreenshotMenuItems
    {
        [MenuItem("ProtoSystem/Screenshot/Take Screenshot (with UI) %#F12")]
        public static void TakeWithUI()
        {
            ScreenshotEditorCapture.CaptureFromEditor(includeUI: true);
        }

        [MenuItem("ProtoSystem/Screenshot/Take Screenshot (without UI) %&F12")]
        public static void TakeWithoutUI()
        {
            ScreenshotEditorCapture.CaptureFromEditor(includeUI: false);
        }

        [MenuItem("ProtoSystem/Screenshot/Open Screenshots Folder")]
        public static void OpenFolder()
        {
            string dir = GetScreenshotDir();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        [MenuItem("ProtoSystem/Screenshot/Create Config")]
        public static void CreateConfig()
        {
            const string path = "Assets/Settings/ScreenshotConfig.asset";
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (AssetDatabase.LoadAssetAtPath<ScreenshotConfig>(path) != null)
            {
                Debug.Log($"[Screenshot] Конфиг уже существует: {path}");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<ScreenshotConfig>(path);
                return;
            }

            var config = ScriptableObject.CreateInstance<ScreenshotConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            Debug.Log($"[Screenshot] Конфиг создан: {path}");
        }

        internal static ScreenshotConfig FindConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScreenshotConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<ScreenshotConfig>(path);
            }
            return null;
        }

        internal static string GetScreenshotDir()
        {
            var config = FindConfig();
            string subfolder = config != null ? config.subfolder : "Screenshots";
            return Path.Combine(Application.persistentDataPath, subfolder);
        }
    }

    // ==========================================
    // Custom Inspector for ScreenshotConfig
    // ==========================================

    [CustomEditor(typeof(ScreenshotConfig))]
    public class ScreenshotConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("📂 Open Screenshots Folder", GUILayout.Height(28)))
            {
                string dir = ScreenshotMenuItems.GetScreenshotDir();
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                EditorUtility.RevealInFinder(dir);
            }

            // Показать количество файлов
            string folder = ScreenshotMenuItems.GetScreenshotDir();
            if (Directory.Exists(folder))
            {
                int count = Directory.GetFiles(folder, "screenshot_*").Length;
                EditorGUILayout.LabelField($"{count} files", EditorStyles.miniLabel, GUILayout.Width(60));
            }
            
            EditorGUILayout.EndHorizontal();
        }
    }

    // ==========================================
    // Editor Capture Logic
    // ==========================================

    public static class ScreenshotEditorCapture
    {
        public static void CaptureFromEditor(bool includeUI)
        {
            // В Play Mode — через ScreenshotSystem если доступен
            if (EditorApplication.isPlaying)
            {
                var system = ScreenshotSystem.Instance;
                if (system != null)
                {
                    system.TakeAndSave(includeUI);
                    return;
                }
            }

            // Вне Play Mode или без системы — захват через камеру
            CaptureGameViewDirect(includeUI);
        }

        /// <summary>
        /// Захват Game View напрямую через камеру (работает вне Play Mode)
        /// </summary>
        private static void CaptureGameViewDirect(bool includeUI)
        {
            var config = ScreenshotMenuItems.FindConfig();
            int superSampling = config != null ? config.superSampling : 1;
            string subfolder = config != null ? config.subfolder : "Screenshots";
            var format = config != null ? config.format : ScreenshotFormat.PNG;
            int jpgQuality = config != null ? config.jpgQuality : 90;
            bool copyClipboard = config == null || config.copyToClipboard;

            Camera cam = Camera.main;
            if (cam == null)
                cam = UnityEngine.Object.FindFirstObjectByType<Camera>();

            if (cam == null)
            {
                Debug.LogWarning("[Screenshot] Камера не найдена");
                return;
            }

            // Отключаем Canvas для clean mode
            Canvas[] disabledCanvases = null;
            if (!includeUI)
            {
                var allCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                disabledCanvases = System.Array.FindAll(allCanvases, c => c.enabled);
                foreach (var canvas in disabledCanvases)
                    canvas.enabled = false;
            }

            // Определяем размер Game View
            var gameViewSize = GetGameViewSize();
            int width = (int)gameViewSize.x * superSampling;
            int height = (int)gameViewSize.y * superSampling;

            // Рендерим в RT
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prevRT = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevRT;

            // Возвращаем Canvas
            if (disabledCanvases != null)
            {
                foreach (var canvas in disabledCanvases)
                    if (canvas != null) canvas.enabled = true;
            }

            // Читаем пиксели
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);

            // Сохраняем
            string dir = Path.Combine(Application.persistentDataPath, subfolder);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string ext = format == ScreenshotFormat.PNG ? "png" : "jpg";
            string path = Path.Combine(dir, $"screenshot_{timestamp}.{ext}");

            byte[] data = format == ScreenshotFormat.PNG ? tex.EncodeToPNG() : tex.EncodeToJPG(jpgQuality);
            File.WriteAllBytes(path, data);

            // Clipboard
            if (copyClipboard)
                ClipboardHelper.CopyTexture(tex);

            UnityEngine.Object.DestroyImmediate(tex);

            string mode = includeUI ? "+UI" : "-UI";
            Debug.Log($"[Screenshot] {mode} → {path}");
        }

        private static Vector2 GetGameViewSize()
        {
            try
            {
                var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType != null)
                {
                    var window = EditorWindow.GetWindow(gameViewType, false, null, false);
                    if (window != null)
                    {
                        var pos = window.position;
                        return new Vector2(pos.width, pos.height);
                    }
                }
            }
            catch { }

            return new Vector2(Screen.width, Screen.height);
        }
    }
}
