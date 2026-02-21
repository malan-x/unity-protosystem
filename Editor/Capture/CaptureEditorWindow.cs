// Packages/com.protosystem.core/Editor/Capture/CaptureEditorWindow.cs
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine.UIElements;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;

namespace ProtoSystem.Editor
{
    // ==========================================
    // Toolbar Buttons — Screenshots
    // ==========================================

    [EditorToolbarElement(Id, typeof(SceneView))]
    public class CaptureWithUIButton : EditorToolbarButton
    {
        public const string Id = "ProtoSystem/CaptureWithUI";

        public CaptureWithUIButton()
        {
            text = "📸 +UI";
            tooltip = "Screenshot with UI (Game View) — F12";
            clicked += () => CaptureEditorCapture.CaptureFromEditor(includeUI: true);

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
    public class CaptureCleanButton : EditorToolbarButton
    {
        public const string Id = "ProtoSystem/CaptureClean";

        public CaptureCleanButton()
        {
            text = "📸 −UI";
            tooltip = "Screenshot without UI (Game View) — Shift+F12";
            clicked += () => CaptureEditorCapture.CaptureFromEditor(includeUI: false);

            style.backgroundColor = new Color(0.15f, 0.45f, 0.7f);
            style.color = Color.white;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            style.borderTopLeftRadius = 0;
            style.borderTopRightRadius = 0;
            style.borderBottomLeftRadius = 0;
            style.borderBottomRightRadius = 0;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 2;
            style.paddingBottom = 2;
        }
    }

    // ==========================================
    // Toolbar Buttons — Video
    // ==========================================

    [EditorToolbarElement(Id, typeof(SceneView))]
    public class VideoRecordButton : EditorToolbarButton
    {
        public const string Id = "ProtoSystem/VideoRecord";

        public VideoRecordButton()
        {
            text = "⏺ REC";
            tooltip = "Start/Stop Video Recording — Ctrl+F9 (Play Mode only)";
            clicked += OnClick;

            style.color = Color.white;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            style.borderTopLeftRadius = 0;
            style.borderTopRightRadius = 0;
            style.borderBottomLeftRadius = 0;
            style.borderBottomRightRadius = 0;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 2;
            style.paddingBottom = 2;

            UpdateVisual();
            EditorApplication.update += UpdateVisual;
        }

        private void OnClick()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Capture] Видеозапись доступна только в Play Mode");
                return;
            }

            var system = CaptureSystem.Instance;
            if (system == null)
            {
                Debug.LogWarning("[Capture] CaptureSystem не найдена");
                return;
            }

            system.ToggleRecording();
        }

        private void UpdateVisual()
        {
            var system = CaptureSystem.Instance;
            bool isRecording = system != null && system.CurrentRecordingState == RecordingState.Recording;

            if (isRecording)
            {
                text = "⏹ STOP";
                style.backgroundColor = new Color(0.8f, 0.15f, 0.15f);
            }
            else
            {
                text = "⏺ REC";
                style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
            }
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    public class ReplaySaveButton : EditorToolbarButton
    {
        public const string Id = "ProtoSystem/ReplaySave";

        public ReplaySaveButton()
        {
            text = "💾 Replay";
            tooltip = "Save Replay Buffer — Ctrl+F8 (Play Mode only)";
            clicked += OnClick;

            style.backgroundColor = new Color(0.2f, 0.5f, 0.3f);
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

        private void OnClick()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Capture] Replay buffer доступен только в Play Mode");
                return;
            }

            var system = CaptureSystem.Instance;
            if (system == null)
            {
                Debug.LogWarning("[Capture] CaptureSystem не найдена");
                return;
            }

            system.SaveReplayBuffer();
        }
    }

    // ==========================================
    // SceneView Overlay
    // ==========================================

    [Overlay(typeof(SceneView), "Capture", defaultDisplay = true)]
    public class CaptureSceneOverlay : ToolbarOverlay
    {
        CaptureSceneOverlay() : base(
            CaptureWithUIButton.Id,
            CaptureCleanButton.Id,
            VideoRecordButton.Id,
            ReplaySaveButton.Id
        ) { }
    }

    // ==========================================
    // Menu Items
    // ==========================================

    public static class CaptureMenuItems
    {
        [MenuItem("ProtoSystem/Capture/Take Screenshot (with UI) %#F12")]
        public static void TakeWithUI()
        {
            CaptureEditorCapture.CaptureFromEditor(includeUI: true);
        }

        [MenuItem("ProtoSystem/Capture/Take Screenshot (without UI) %&F12")]
        public static void TakeWithoutUI()
        {
            CaptureEditorCapture.CaptureFromEditor(includeUI: false);
        }

        [MenuItem("ProtoSystem/Capture/Open Screenshots Folder")]
        public static void OpenScreenshotsFolder()
        {
            string dir = GetScreenshotDir();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        [MenuItem("ProtoSystem/Capture/Start-Stop Recording %#F9")]
        public static void ToggleRecording()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Capture] Видеозапись доступна только в Play Mode");
                return;
            }

            var system = CaptureSystem.Instance;
            if (system != null)
                system.ToggleRecording();
            else
                Debug.LogWarning("[Capture] CaptureSystem не найдена");
        }

        [MenuItem("ProtoSystem/Capture/Save Replay Buffer %#F8")]
        public static void SaveReplay()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Capture] Replay buffer доступен только в Play Mode");
                return;
            }

            var system = CaptureSystem.Instance;
            if (system != null)
                system.SaveReplayBuffer();
            else
                Debug.LogWarning("[Capture] CaptureSystem не найдена");
        }

        [MenuItem("ProtoSystem/Capture/Open Videos Folder")]
        public static void OpenVideosFolder()
        {
            string dir = GetVideoDir();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        [MenuItem("ProtoSystem/Capture/Create Config")]
        public static void CreateConfig()
        {
            const string path = "Assets/Settings/CaptureConfig.asset";
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (AssetDatabase.LoadAssetAtPath<CaptureConfig>(path) != null)
            {
                Debug.Log($"[Capture] Конфиг уже существует: {path}");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<CaptureConfig>(path);
                return;
            }

            var config = ScriptableObject.CreateInstance<CaptureConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            Debug.Log($"[Capture] Конфиг создан: {path}");
        }

        internal static CaptureConfig FindConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:CaptureConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<CaptureConfig>(path);
            }
            return null;
        }

        internal static string GetScreenshotDir()
        {
            var config = FindConfig();
            string subfolder = config != null ? config.subfolder : "Screenshots";
            return Path.Combine(Application.persistentDataPath, subfolder);
        }

        internal static string GetVideoDir()
        {
            var config = FindConfig();
            string subfolder = config != null ? config.videoSubfolder : "Videos";
            return Path.Combine(Application.persistentDataPath, subfolder);
        }
    }

    // ==========================================
    // Custom Inspector for CaptureConfig
    // ==========================================

    [CustomEditor(typeof(CaptureConfig))]
    public class CaptureConfigEditor : UnityEditor.Editor
    {
        private static bool? _hasRecorder;
        private static bool _installingRecorder;

        private static bool HasRecorder
        {
            get
            {
                if (_hasRecorder == null)
                {
#if PROTO_HAS_RECORDER
                    _hasRecorder = true;
#else
                    _hasRecorder = false;
#endif
                }
                return _hasRecorder.Value;
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (CaptureConfig)target;

            EditorGUILayout.Space(10);

            // ─── Screenshots Section ───
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("📂 Open Screenshots Folder", GUILayout.Height(28)))
            {
                string dir = CaptureMenuItems.GetScreenshotDir();
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                EditorUtility.RevealInFinder(dir);
            }

            string screenshotFolder = CaptureMenuItems.GetScreenshotDir();
            if (Directory.Exists(screenshotFolder))
            {
                int count = Directory.GetFiles(screenshotFolder, "screenshot_*").Length;
                EditorGUILayout.LabelField($"{count} files", EditorStyles.miniLabel, GUILayout.Width(60));
            }

            EditorGUILayout.EndHorizontal();

            // ─── Video Section ───
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("📂 Open Videos Folder", GUILayout.Height(28)))
            {
                string dir = CaptureMenuItems.GetVideoDir();
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                EditorUtility.RevealInFinder(dir);
            }

            string videoFolder = CaptureMenuItems.GetVideoDir();
            if (Directory.Exists(videoFolder))
            {
                int count = Directory.GetFiles(videoFolder, "*.mp4").Length;
                EditorGUILayout.LabelField($"{count} files", EditorStyles.miniLabel, GUILayout.Width(60));
            }

            EditorGUILayout.EndHorizontal();

            // ─── Unity Recorder Status ───
            if (!HasRecorder)
            {
                EditorGUILayout.Space(5);

                EditorGUILayout.HelpBox(
                    "Unity Recorder не установлен. Ручная запись видео (Ctrl+F9) недоступна.\n" +
                    "Replay buffer работает без Recorder.",
                    MessageType.Warning);

                EditorGUI.BeginDisabledGroup(_installingRecorder);
                if (GUILayout.Button(_installingRecorder ? "Установка..." : "Установить Unity Recorder", GUILayout.Height(28)))
                {
                    _installingRecorder = true;
                    var request = Client.Add("com.unity.recorder@5.1.1");
                    EditorApplication.update += () => CheckInstallProgress(request);
                }
                EditorGUI.EndDisabledGroup();
            }

            // ─── Replay Buffer Memory Estimate ───
            if (config.videoMode == VideoRecordingMode.ReplayBuffer)
            {
                float estimatedKbPerFrame = 150f; // ~150 KB at q75
                float totalFrames = config.replayBufferSeconds * config.videoFps;
                float estimatedMb = totalFrames * estimatedKbPerFrame / 1024f;

                EditorGUILayout.Space(5);

                if (estimatedMb > 400)
                {
                    EditorGUILayout.HelpBox(
                        $"Replay buffer: ~{estimatedMb:F0} МБ ({totalFrames:F0} кадров). " +
                        "Большой объём памяти! Рекомендуется уменьшить длительность или FPS.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"Replay buffer: ~{estimatedMb:F0} МБ ({totalFrames:F0} кадров)",
                        MessageType.Info);
                }
            }
        }

        private static void CheckInstallProgress(UnityEditor.PackageManager.Requests.AddRequest request)
        {
            if (!request.IsCompleted) return;

            EditorApplication.update -= () => CheckInstallProgress(request);
            _installingRecorder = false;
            _hasRecorder = null; // сбросить кеш

            if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
            {
                Debug.Log("[Capture] Unity Recorder установлен. Перекомпиляция...");
            }
            else
            {
                Debug.LogError($"[Capture] Ошибка установки Unity Recorder: {request.Error?.message}");
            }
        }
    }

    // ==========================================
    // Editor Capture Logic
    // ==========================================

    public static class CaptureEditorCapture
    {
        public static void CaptureFromEditor(bool includeUI)
        {
            if (EditorApplication.isPlaying)
            {
                var system = CaptureSystem.Instance;
                if (system != null)
                {
                    system.TakeAndSave(includeUI);
                    return;
                }
            }

            CaptureGameViewDirect(includeUI);
        }

        private static void CaptureGameViewDirect(bool includeUI)
        {
            var config = CaptureMenuItems.FindConfig();
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
                Debug.LogWarning("[Capture] Камера не найдена");
                return;
            }

            Canvas[] disabledCanvases = null;
            if (!includeUI)
            {
                var allCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                disabledCanvases = System.Array.FindAll(allCanvases, c => c.enabled);
                foreach (var canvas in disabledCanvases)
                    canvas.enabled = false;
            }

            var gameViewSize = GetGameViewSize();
            int width = (int)gameViewSize.x * superSampling;
            int height = (int)gameViewSize.y * superSampling;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prevRT = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevRT;

            if (disabledCanvases != null)
            {
                foreach (var canvas in disabledCanvases)
                    if (canvas != null) canvas.enabled = true;
            }

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);

            string dir = Path.Combine(Application.persistentDataPath, subfolder);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string ext = format == ScreenshotFormat.PNG ? "png" : "jpg";
            string path = Path.Combine(dir, $"screenshot_{timestamp}.{ext}");

            byte[] data = format == ScreenshotFormat.PNG ? tex.EncodeToPNG() : tex.EncodeToJPG(jpgQuality);
            File.WriteAllBytes(path, data);

            if (copyClipboard)
                ClipboardHelper.CopyTexture(tex);

            UnityEngine.Object.DestroyImmediate(tex);

            string mode = includeUI ? "+UI" : "-UI";
            Debug.Log($"[Capture] {mode} → {path}");
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
