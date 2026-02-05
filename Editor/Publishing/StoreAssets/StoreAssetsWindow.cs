// Packages/com.protosystem.core/Editor/Publishing/StoreAssets/StoreAssetsWindow.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.Publishing.Editor
{
    public class StoreAssetsWindow : EditorWindow
    {
        #region Enums & Constants

        private enum PlatformTab { Steam, Itch, Epic, GOG }
        private enum SteamSubTab { Graphics, Texts }

        private const string STEAMWORKS_ASSETS_URL = "https://partner.steamgames.com/apps/config/";
        
        #endregion

        #region State

        private PlatformTab _currentTab = PlatformTab.Steam;
        private SteamSubTab _steamSubTab = SteamSubTab.Graphics;
        private StoreAssetsConfig _config;
        private SteamConfig _steamConfig;
        private Vector2 _scrollPos;
        private string _statusMessage = "Ready";
        private Color _statusColor = Color.green;
        
        // Texts tab state
        private string _selectedLanguage = "english";
        private Vector2 _languageListScroll;
        private Vector2 _aboutTextScroll;
        private bool _showSysReqs;
        
        // Cached textures for preview
        private Dictionary<string, Texture2D> _previewCache = new Dictionary<string, Texture2D>();
        
        // Cached image sizes (avoid reading files every frame)
        private Dictionary<string, Vector2Int> _imageSizeCache = new Dictionary<string, Vector2Int>();
        
        // Current focused asset for paste
        private string _focusedAssetId;
        private StoreAssetDefinition _focusedAssetDef;
        private List<StoreAssetEntry> _focusedAssetEntries;
        
        // Cached styles (avoid GC alloc every frame)
        private GUIStyle _hintStyle;
        private GUIStyle _urlFieldStyle;
        private bool _stylesInitialized;

        #endregion

        [MenuItem("ProtoSystem/Publishing/Store Assets", priority = 101)]
        public static void ShowWindow()
        {
            var window = GetWindow<StoreAssetsWindow>("Store Assets");
            window.minSize = new Vector2(650, 600);
            window.Show();
        }

        private void OnEnable()
        {
            LoadConfig();
        }

        private void OnDisable()
        {
            ClearPreviewCache();
        }

        private void LoadConfig()
        {
            var guids = AssetDatabase.FindAssets("t:StoreAssetsConfig");
            if (guids.Length > 0)
            {
                _config = AssetDatabase.LoadAssetAtPath<StoreAssetsConfig>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            
            // Load SteamConfig for URL generation
            _steamConfig = FindSteamConfig();
        }

        private void ClearPreviewCache()
        {
            foreach (var tex in _previewCache.Values)
            {
                if (tex != null) DestroyImmediate(tex);
            }
            _previewCache.Clear();
            _imageSizeCache.Clear();
        }
        
        private void InitStyles()
        {
            if (_stylesInitialized) return;
            
            _hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontStyle = FontStyle.Italic
            };
            
            _stylesInitialized = true;
        }

        #region GUI

        private void OnGUI()
        {
            // Init cached styles once
            InitStyles();
            
            // Handle keyboard events (Ctrl+V)
            HandleKeyboardEvents();
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            DrawPlatformTabs();

            EditorGUILayout.Space(10);

            // Config selection
            DrawConfigSection();

            if (_config != null)
            {
                EditorGUILayout.Space(10);

                switch (_currentTab)
                {
                    case PlatformTab.Steam:
                        DrawSteamTab();
                        break;
                    case PlatformTab.Itch:
                        DrawItchTab();
                        break;
                    default:
                        DrawNotImplementedTab();
                        break;
                }
            }

            EditorGUILayout.Space(10);
            DrawStatusBar();

            EditorGUILayout.EndScrollView();
        }
        
        private void HandleKeyboardEvents()
        {
            var e = Event.current;
            
            // Ctrl+V / Cmd+V
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.V && 
                (e.control || e.command))
            {
                if (_focusedAssetDef != null && _focusedAssetEntries != null && ClipboardImageHelper.HasImage())
                {
                    PasteImageFromClipboard(_focusedAssetDef, _focusedAssetEntries);
                    e.Use();
                }
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Store Assets", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                LoadConfig();
                ClearPreviewCache();
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        private void DrawPlatformTabs()
        {
            EditorGUILayout.BeginHorizontal();

            var tabs = new[] { "Steam", "itch.io", "Epic", "GOG" };

            for (int i = 0; i < tabs.Length; i++)
            {
                var selected = _currentTab == (PlatformTab)i;
                
                // Count ready assets for each platform
                var readyCount = 0;
                var totalCount = 0;
                
                if (_config != null)
                {
                    switch ((PlatformTab)i)
                    {
                        case PlatformTab.Steam:
                            totalCount = StoreAssetsConfig.SteamAssetDefinitions.Count(d => d.required);
                            readyCount = _config.steamAssets.Count(a => a.isReady && 
                                StoreAssetsConfig.SteamAssetDefinitions.Any(d => d.id == a.assetId && d.required));
                            break;
                        case PlatformTab.Itch:
                            totalCount = StoreAssetsConfig.ItchAssetDefinitions.Count(d => d.required);
                            readyCount = _config.itchAssets.Count(a => a.isReady &&
                                StoreAssetsConfig.ItchAssetDefinitions.Any(d => d.id == a.assetId && d.required));
                            break;
                    }
                }

                var statusIcon = totalCount > 0 
                    ? (readyCount == totalCount ? "✓" : $"{readyCount}/{totalCount}") 
                    : "○";

                GUI.backgroundColor = selected ? new Color(0.7f, 0.9f, 1f) : Color.white;

                if (GUILayout.Button($"{statusIcon} {tabs[i]}", EditorStyles.toolbarButton, GUILayout.Height(25)))
                {
                    _currentTab = (PlatformTab)i;
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Config:", GUILayout.Width(50));

            var newConfig = (StoreAssetsConfig)EditorGUILayout.ObjectField(
                _config, typeof(StoreAssetsConfig), false);
            
            if (newConfig != _config)
            {
                _config = newConfig;
                ClearPreviewCache();
            }

            if (GUILayout.Button("New", GUILayout.Width(45)))
            {
                CreateNewConfig();
            }

            EditorGUILayout.EndHorizontal();

            if (_config != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Output:", GUILayout.Width(50));
                _config.outputFolder = EditorGUILayout.TextField(_config.outputFolder);
                
                if (GUILayout.Button("Open", GUILayout.Width(45)))
                {
                    var path = _config.GetOutputPath();
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    EditorUtility.RevealInFinder(path);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Steam Tab

        private void DrawSteamTab()
        {
            // App ID status
            var hasAppId = _steamConfig != null && !string.IsNullOrEmpty(_steamConfig.appId);
            
            if (!hasAppId)
            {
                EditorGUILayout.HelpBox(
                    "App ID не задан. Настройте SteamConfig в Build Publisher для автогенерации URL.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"App ID: {_steamConfig.appId}", EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(_steamConfig.appName))
                {
                    EditorGUILayout.LabelField($"({_steamConfig.appName})", EditorStyles.miniLabel);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            
            // Sub-tabs
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = _steamSubTab == SteamSubTab.Graphics ? new Color(0.6f, 0.85f, 1f) : Color.white;
            if (GUILayout.Button("🖼 Graphics", GUILayout.Height(24)))
            {
                _steamSubTab = SteamSubTab.Graphics;
            }
            
            // Count filled languages
            var filledCount = StoreAssetsConfig.SteamLanguages.Count(l => _config.IsLanguageFilled(l));
            var textsLabel = filledCount > 0 ? $"📝 Texts ({filledCount}/{StoreAssetsConfig.SteamLanguages.Length})" : "📝 Texts";
            
            GUI.backgroundColor = _steamSubTab == SteamSubTab.Texts ? new Color(0.6f, 0.85f, 1f) : Color.white;
            if (GUILayout.Button(textsLabel, GUILayout.Height(24)))
            {
                _steamSubTab = SteamSubTab.Texts;
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            switch (_steamSubTab)
            {
                case SteamSubTab.Graphics:
                    DrawSteamGraphicsSubTab(hasAppId);
                    break;
                case SteamSubTab.Texts:
                    DrawSteamTextsSubTab(hasAppId);
                    break;
            }
        }
        
        private void DrawSteamGraphicsSubTab(bool hasAppId)
        {
            // Quick actions
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            if (GUILayout.Button("📂 Open Steamworks Assets", GUILayout.Height(24)))
            {
                var url = hasAppId
                    ? $"{STEAMWORKS_ASSETS_URL}{_steamConfig.appId}"
                    : "https://partner.steamgames.com/";
                Application.OpenURL(url);
            }

            if (GUILayout.Button("📋 Asset Guidelines", GUILayout.Height(24)))
            {
                Application.OpenURL("https://partner.steamgames.com/doc/store/assets/standard");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Asset categories
            DrawAssetCategory("Изображения для магазина", StoreAssetsConfig.SteamAssetDefinitions
                .Where(d => d.id.Contains("capsule") || d.id == "page_background").ToArray(), _config.steamAssets);

            DrawScreenshotsSection(hasAppId);

            DrawAssetCategory("Иконка сообщества", StoreAssetsConfig.SteamAssetDefinitions
                .Where(d => d.id == "community_icon").ToArray(), _config.steamAssets);
        }
        
        private void DrawSteamTextsSubTab(bool hasAppId)
        {
            // Quick actions
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            if (GUILayout.Button("📂 Open Steamworks Store Page", GUILayout.Height(24)))
            {
                var url = hasAppId
                    ? $"https://partner.steamgames.com/apps/landing/{_steamConfig.appId}"
                    : "https://partner.steamgames.com/";
                Application.OpenURL(url);
            }
            
            GUI.enabled = hasAppId && _config.steamTexts.Count > 0;
            if (GUILayout.Button("📤 Export JSON", GUILayout.Height(24), GUILayout.Width(110)))
            {
                ExportSteamLocalizationJson();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
            
            // Import drop zone
            DrawImportDropZone();

            EditorGUILayout.Space(5);
            
            // Two-column layout
            EditorGUILayout.BeginHorizontal();
            
            // Left: language list
            DrawLanguageList();
            
            // Right: editor
            EditorGUILayout.BeginVertical();
            DrawTextEditor();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawLanguageList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(200));
            
            EditorGUILayout.LabelField("Languages", EditorStyles.boldLabel);
            
            _languageListScroll = EditorGUILayout.BeginScrollView(_languageListScroll, GUILayout.Height(450));
            
            foreach (var langCode in StoreAssetsConfig.SteamLanguages)
            {
                var isFilled = _config.IsLanguageFilled(langCode);
                var isSelected = _selectedLanguage == langCode;
                
                string displayName;
                StoreAssetsConfig.LanguageDisplayNames.TryGetValue(langCode, out displayName);
                if (string.IsNullOrEmpty(displayName)) displayName = langCode;
                
                var icon = isFilled ? "✓" : "○";
                var label = $"{icon} {displayName}";
                
                GUI.backgroundColor = isSelected ? new Color(0.5f, 0.75f, 1f) : (isFilled ? new Color(0.85f, 1f, 0.85f) : Color.white);
                
                if (GUILayout.Button(label, EditorStyles.miniButton))
                {
                    _selectedLanguage = langCode;
                }
            }
            
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        
        private GUIStyle _wordWrapTextArea;
        
        private void DrawTextEditor()
        {
            // Init word wrap style
            if (_wordWrapTextArea == null)
            {
                _wordWrapTextArea = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true
                };
            }
            
            var text = _config.GetOrCreateLocalizedText(_selectedLanguage);
            
            string displayName;
            StoreAssetsConfig.LanguageDisplayNames.TryGetValue(_selectedLanguage, out displayName);
            if (string.IsNullOrEmpty(displayName)) displayName = _selectedLanguage;
            
            EditorGUILayout.LabelField($"🌐 {displayName}", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(5);
            
            // Short description
            EditorGUILayout.LabelField("Short Description (200-300 chars):");
            
            EditorGUI.BeginChangeCheck();
            var newShort = EditorGUILayout.TextArea(text.shortDescription ?? "", _wordWrapTextArea, GUILayout.Height(60));
            if (EditorGUI.EndChangeCheck())
            {
                text.shortDescription = newShort;
                EditorUtility.SetDirty(_config);
            }
            
            // Character counter
            var charCount = text.shortDescription?.Length ?? 0;
            var counterColor = charCount < 200 ? Color.yellow : (charCount > 300 ? Color.red : Color.green);
            GUI.contentColor = counterColor;
            EditorGUILayout.LabelField($"{charCount}/300", EditorStyles.miniLabel);
            GUI.contentColor = Color.white;
            
            EditorGUILayout.Space(10);
            
            // About text
            EditorGUILayout.LabelField("About (Full Description, BBCode supported):");
            
            _aboutTextScroll = EditorGUILayout.BeginScrollView(_aboutTextScroll, GUILayout.Height(200));
            
            EditorGUI.BeginChangeCheck();
            var newAbout = EditorGUILayout.TextArea(text.about ?? "", _wordWrapTextArea, GUILayout.ExpandHeight(true));
            if (EditorGUI.EndChangeCheck())
            {
                text.about = newAbout;
                EditorUtility.SetDirty(_config);
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            
            // System requirements
            _showSysReqs = EditorGUILayout.Foldout(_showSysReqs, "⚙ System Requirements (Windows)", true);
            
            if (_showSysReqs)
            {
                var english = _selectedLanguage != "english" 
                    ? _config.steamTexts.Find(t => t.languageCode == "english") 
                    : null;
                var hasEnglishMin = english != null && !string.IsNullOrEmpty(english.sysreqsMinOS);
                var hasEnglishRec = english != null && !string.IsNullOrEmpty(english.sysreqsRecOS);
                
                EditorGUI.indentLevel++;
                
                // Minimum
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Minimum:", EditorStyles.miniBoldLabel);
                
                var minEmpty = string.IsNullOrEmpty(text.sysreqsMinOS);
                if (minEmpty && hasEnglishMin)
                {
                    if (GUILayout.Button("📋 Copy from EN", EditorStyles.miniButton, GUILayout.Width(100)))
                    {
                        CopySysReqsFromEnglish(text, true, false);
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                DrawSysReqFields(text, true);
                
                EditorGUILayout.Space(5);
                
                // Recommended
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Recommended:", EditorStyles.miniBoldLabel);
                
                var recEmpty = string.IsNullOrEmpty(text.sysreqsRecOS);
                if (recEmpty && hasEnglishRec)
                {
                    if (GUILayout.Button("📋 Copy from EN", EditorStyles.miniButton, GUILayout.Width(100)))
                    {
                        CopySysReqsFromEnglish(text, false, true);
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                DrawSysReqFields(text, false);
                
                EditorGUI.indentLevel--;
                
                // Copy all from English button
                if (_selectedLanguage != "english" && (hasEnglishMin || hasEnglishRec))
                {
                    EditorGUILayout.Space(5);
                    if (GUILayout.Button("📋 Copy All System Reqs from English", GUILayout.Height(22)))
                    {
                        CopySysReqsFromEnglish(text, true, true);
                    }
                }
            }
        }
        
        private void DrawSysReqFields(StoreLocalizedText text, bool isMin)
        {
            EditorGUI.BeginChangeCheck();
            
            if (isMin)
            {
                text.sysreqsMinOS = EditorGUILayout.TextField("OS", text.sysreqsMinOS ?? "");
                text.sysreqsMinProcessor = EditorGUILayout.TextField("Processor", text.sysreqsMinProcessor ?? "");
                text.sysreqsMinMemory = EditorGUILayout.TextField("Memory", text.sysreqsMinMemory ?? "");
                text.sysreqsMinGraphics = EditorGUILayout.TextField("Graphics", text.sysreqsMinGraphics ?? "");
                text.sysreqsMinStorage = EditorGUILayout.TextField("Storage", text.sysreqsMinStorage ?? "");
            }
            else
            {
                text.sysreqsRecOS = EditorGUILayout.TextField("OS", text.sysreqsRecOS ?? "");
                text.sysreqsRecProcessor = EditorGUILayout.TextField("Processor", text.sysreqsRecProcessor ?? "");
                text.sysreqsRecMemory = EditorGUILayout.TextField("Memory", text.sysreqsRecMemory ?? "");
                text.sysreqsRecGraphics = EditorGUILayout.TextField("Graphics", text.sysreqsRecGraphics ?? "");
                text.sysreqsRecStorage = EditorGUILayout.TextField("Storage", text.sysreqsRecStorage ?? "");
            }
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_config);
            }
        }
        
        private void CopySysReqsFromEnglish(StoreLocalizedText target, bool copyMin = true, bool copyRec = true)
        {
            var english = _config.steamTexts.Find(t => t.languageCode == "english");
            if (english == null)
            {
                SetStatus("✗ English not found", Color.red);
                return;
            }
            
            if (copyMin)
            {
                target.sysreqsMinOS = english.sysreqsMinOS;
                target.sysreqsMinProcessor = english.sysreqsMinProcessor;
                target.sysreqsMinMemory = english.sysreqsMinMemory;
                target.sysreqsMinGraphics = english.sysreqsMinGraphics;
                target.sysreqsMinStorage = english.sysreqsMinStorage;
            }
            
            if (copyRec)
            {
                target.sysreqsRecOS = english.sysreqsRecOS;
                target.sysreqsRecProcessor = english.sysreqsRecProcessor;
                target.sysreqsRecMemory = english.sysreqsRecMemory;
                target.sysreqsRecGraphics = english.sysreqsRecGraphics;
                target.sysreqsRecStorage = english.sysreqsRecStorage;
            }
            
            EditorUtility.SetDirty(_config);
            
            var what = copyMin && copyRec ? "all" : (copyMin ? "minimum" : "recommended");
            SetStatus($"✓ Copied {what} system requirements", Color.green);
        }
        
        private void ExportSteamLocalizationJson()
        {
            var appId = _steamConfig.appId;
            var sb = new StringBuilder();
            
            sb.Append("{\"itemid\":\"");
            sb.Append(appId);
            sb.Append("\",\"languages\":{");
            
            bool first = true;
            foreach (var langCode in StoreAssetsConfig.SteamLanguages)
            {
                if (!first) sb.Append(",");
                first = false;
                
                var text = _config.steamTexts.Find(t => t.languageCode == langCode);
                
                sb.Append("\"");
                sb.Append(langCode);
                sb.Append("\":{");
                
                // about
                sb.Append("\"app[content][about]\":\"");
                sb.Append(EscapeJson(text?.about ?? ""));
                sb.Append("\",");
                
                // short_description
                sb.Append("\"app[content][short_description]\":\"");
                sb.Append(EscapeJson(text?.shortDescription ?? ""));
                sb.Append("\",");
                
                // sysreqs min
                sb.Append("\"app[content][sysreqs][windows][min][osversion]\":\"");
                sb.Append(EscapeJson(text?.sysreqsMinOS ?? ""));
                sb.Append("\",");
                
                sb.Append("\"app[content][sysreqs][windows][min][processor]\":\"");
                sb.Append(EscapeJson(text?.sysreqsMinProcessor ?? ""));
                sb.Append("\",");
                
                sb.Append("\"app[content][sysreqs][windows][min][graphics]\":\"");
                sb.Append(EscapeJson(text?.sysreqsMinGraphics ?? ""));
                sb.Append("\"");
                
                sb.Append("}");
            }
            
            sb.Append("}}");
            
            // Save
            var outputDir = _config.GetOutputPath();
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            
            var fileName = $"storepage_{appId}_all.json";
            var filePath = Path.Combine(outputDir, fileName);
            
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            
            AssetDatabase.Refresh();
            SetStatus($"✓ Exported: {fileName}", Color.green);
            EditorUtility.RevealInFinder(filePath);
        }
        
        private string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            
            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r\n", "\\r\\n")
                .Replace("\n", "\\r\\n")
                .Replace("\r", "")
                .Replace("\t", "\\t")
                .Replace("/", "\\/");
        }
        
        private void DrawImportDropZone()
        {
            var dropRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(36));
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("↓ Drop JSON here to import or", _hintStyle, GUILayout.Width(160));
            
            if (GUILayout.Button("📥 Import JSON", GUILayout.Height(20), GUILayout.Width(100)))
            {
                var path = EditorUtility.OpenFilePanel("Import Steam Localization JSON", "", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    ImportLocalizationJson(path);
                }
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            // Handle drag & drop
            HandleJsonDragAndDrop(dropRect);
        }
        
        private void HandleJsonDragAndDrop(Rect dropArea)
        {
            var e = Event.current;
            
            if (!dropArea.Contains(e.mousePosition))
                return;
                
            switch (e.type)
            {
                case EventType.DragUpdated:
                    if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                    {
                        var path = DragAndDrop.paths[0];
                        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            e.Use();
                        }
                    }
                    break;
                    
                case EventType.DragPerform:
                    if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                    {
                        var path = DragAndDrop.paths[0];
                        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            DragAndDrop.AcceptDrag();
                            ImportLocalizationJson(path);
                            e.Use();
                        }
                    }
                    break;
            }
        }
        
        private void ImportLocalizationJson(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                
                // Simple JSON parsing (no external dependencies)
                var importedCount = 0;
                
                // Find "languages":{ section
                var langStart = json.IndexOf("\"languages\":", StringComparison.Ordinal);
                if (langStart < 0)
                {
                    SetStatus("✗ Invalid JSON: no 'languages' found", Color.red);
                    return;
                }
                
                foreach (var langCode in StoreAssetsConfig.SteamLanguages)
                {
                    // Find language section: "english":{
                    var langPattern = $"\"{langCode}\":{{";
                    var langIndex = json.IndexOf(langPattern, StringComparison.Ordinal);
                    if (langIndex < 0) continue;
                    
                    // Extract values for this language
                    var shortDesc = ExtractJsonValue(json, langIndex, "app[content][short_description]");
                    var about = ExtractJsonValue(json, langIndex, "app[content][about]");
                    var sysOS = ExtractJsonValue(json, langIndex, "app[content][sysreqs][windows][min][osversion]");
                    var sysProc = ExtractJsonValue(json, langIndex, "app[content][sysreqs][windows][min][processor]");
                    var sysGfx = ExtractJsonValue(json, langIndex, "app[content][sysreqs][windows][min][graphics]");
                    
                    // Skip if no content
                    if (string.IsNullOrEmpty(shortDesc) && string.IsNullOrEmpty(about))
                        continue;
                    
                    // Update config
                    var text = _config.GetOrCreateLocalizedText(langCode);
                    
                    if (!string.IsNullOrEmpty(shortDesc))
                        text.shortDescription = UnescapeJson(shortDesc);
                    if (!string.IsNullOrEmpty(about))
                        text.about = UnescapeJson(about);
                    if (!string.IsNullOrEmpty(sysOS))
                        text.sysreqsMinOS = UnescapeJson(sysOS);
                    if (!string.IsNullOrEmpty(sysProc))
                        text.sysreqsMinProcessor = UnescapeJson(sysProc);
                    if (!string.IsNullOrEmpty(sysGfx))
                        text.sysreqsMinGraphics = UnescapeJson(sysGfx);
                    
                    importedCount++;
                }
                
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssets();
                
                SetStatus($"✓ Imported {importedCount} languages from {Path.GetFileName(filePath)}", Color.green);
            }
            catch (Exception ex)
            {
                SetStatus($"✗ Import failed: {ex.Message}", Color.red);
                Debug.LogError($"[StoreAssets] Import failed: {ex}");
            }
        }
        
        private string ExtractJsonValue(string json, int startIndex, string key)
        {
            // Find key pattern: "key":"
            var keyPattern = $"\"{key}\":\"";
            var keyIndex = json.IndexOf(keyPattern, startIndex, StringComparison.Ordinal);
            
            // Limit search to this language section (next closing brace at same level)
            var nextLangIndex = json.IndexOf("},\"", startIndex + 10, StringComparison.Ordinal);
            if (nextLangIndex > 0 && keyIndex > nextLangIndex)
                return null;
            
            if (keyIndex < 0) return null;
            
            var valueStart = keyIndex + keyPattern.Length;
            
            // Find closing quote (handle escaped quotes)
            var valueEnd = valueStart;
            while (valueEnd < json.Length)
            {
                var quoteIndex = json.IndexOf('"', valueEnd);
                if (quoteIndex < 0) break;
                
                // Check if escaped
                var backslashCount = 0;
                for (int i = quoteIndex - 1; i >= valueStart && json[i] == '\\'; i--)
                    backslashCount++;
                
                if (backslashCount % 2 == 0) // Not escaped
                {
                    valueEnd = quoteIndex;
                    break;
                }
                
                valueEnd = quoteIndex + 1;
            }
            
            if (valueEnd <= valueStart) return null;
            
            return json.Substring(valueStart, valueEnd - valueStart);
        }
        
        private string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            
            return s
                .Replace("\\/", "/")
                .Replace("\\t", "\t")
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        private void DrawScreenshotsSection(bool hasAppId)
        {
            EditorGUILayout.LabelField("Скриншоты", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header with count and upload link
            EditorGUILayout.BeginHorizontal();
            
            var count = _config.steamScreenshots.Count;
            var statusIcon = count >= 5 ? "✓" : "○";
            var statusColor = count >= 5 ? Color.green : Color.yellow;
            
            GUI.contentColor = statusColor;
            EditorGUILayout.LabelField($"{statusIcon} {count}/5 (минимум 5)", GUILayout.Width(120));
            GUI.contentColor = Color.white;
            
            EditorGUILayout.LabelField("1920×1080 или больше", EditorStyles.miniLabel);
            
            GUILayout.FlexibleSpace();
            
            if (hasAppId && GUILayout.Button("↑ Upload", GUILayout.Width(70)))
            {
                Application.OpenURL(GetSteamworksUrl("graphicalassets", _steamConfig.appId));
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Screenshot list
            for (int i = 0; i < _config.steamScreenshots.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(25));
                
                var path = _config.steamScreenshots[i];
                var newPath = EditorGUILayout.TextField(path);
                if (newPath != path)
                {
                    _config.steamScreenshots[i] = newPath;
                    EditorUtility.SetDirty(_config);
                }
                
                if (GUILayout.Button("...", GUILayout.Width(25)))
                {
                    var selected = EditorUtility.OpenFilePanel("Select Screenshot", Application.dataPath, "png,jpg,jpeg");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        _config.steamScreenshots[i] = GetRelativePath(selected);
                        EditorUtility.SetDirty(_config);
                    }
                }
                
                // Show size if exists
                var fullPath = GetFullPath(path);
                if (!string.IsNullOrEmpty(path) && File.Exists(fullPath))
                {
                    var size = GetImageSize(fullPath);
                    var sizeOk = size.x >= 1920 && size.y >= 1080;
                    GUI.contentColor = sizeOk ? Color.green : Color.red;
                    EditorGUILayout.LabelField($"{size.x}×{size.y}", EditorStyles.miniLabel, GUILayout.Width(70));
                    GUI.contentColor = Color.white;
                }
                
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("✕", GUILayout.Width(20)))
                {
                    _config.steamScreenshots.RemoveAt(i);
                    EditorUtility.SetDirty(_config);
                    i--;
                }
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.EndHorizontal();
            }
            
            // Add button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("+ Добавить скриншот", GUILayout.Width(150)))
            {
                var selected = EditorUtility.OpenFilePanel("Select Screenshot", Application.dataPath, "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(selected))
                {
                    _config.steamScreenshots.Add(GetRelativePath(selected));
                    EditorUtility.SetDirty(_config);
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
        }

        private void DrawItchTab()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            if (GUILayout.Button("📂 Open itch.io Dashboard", GUILayout.Height(24)))
            {
                Application.OpenURL("https://itch.io/dashboard");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            DrawAssetCategory("Cover & Banner", StoreAssetsConfig.ItchAssetDefinitions, _config.itchAssets);
        }

        private void DrawNotImplementedTab()
        {
            EditorGUILayout.HelpBox(
                $"{_currentTab} support is planned for future versions.",
                MessageType.Info);
        }

        #endregion

        #region Asset Drawing

        private void DrawAssetCategory(string title, StoreAssetDefinition[] definitions, List<StoreAssetEntry> entries)
        {
            if (definitions.Length == 0) return;

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            foreach (var def in definitions)
            {
                DrawAssetEntry(def, entries);
            }

            EditorGUILayout.Space(10);
        }

        private void DrawAssetEntry(StoreAssetDefinition def, List<StoreAssetEntry> entries)
        {
            var entry = _config.GetOrCreateEntry(def.id, entries);
            
            // Cache paths and existence checks (computed once per frame)
            var sourcePath = !string.IsNullOrEmpty(entry.sourcePath) ? GetFullPath(entry.sourcePath) : null;
            var hasSource = sourcePath != null && File.Exists(sourcePath);
            var sourceSize = hasSource ? GetImageSize(sourcePath) : Vector2Int.zero;
            var sizeOk = sourceSize.x >= def.width && sourceSize.y >= def.height;

            // Get the rect for drag & drop
            var boxRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Handle Drag & Drop
            HandleDragAndDrop(boxRect, def, entry);

            // Header row
            EditorGUILayout.BeginHorizontal();

            // Status icon (use cached values)
            var statusIcon = "○";
            var statusColor = Color.gray;

            if (hasSource)
            {
                if (sizeOk)
                {
                    if (entry.isReady)
                    {
                        statusIcon = "✓";
                        statusColor = Color.green;
                    }
                    else
                    {
                        statusIcon = "◐";
                        statusColor = Color.yellow;
                    }
                }
                else
                {
                    statusIcon = "⚠";
                    statusColor = new Color(1f, 0.5f, 0f); // Orange
                }
            }

            GUI.contentColor = statusColor;
            EditorGUILayout.LabelField(statusIcon, GUILayout.Width(20));
            GUI.contentColor = Color.white;

            // Name and size
            var requiredMark = def.required ? "*" : "";
            EditorGUILayout.LabelField($"{def.displayName}{requiredMark}", EditorStyles.boldLabel, GUILayout.Width(150));
            EditorGUILayout.LabelField($"{def.width}×{def.height}", EditorStyles.miniLabel, GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            // Ready toggle
            var newReady = EditorGUILayout.Toggle(entry.isReady, GUILayout.Width(20));
            if (newReady != entry.isReady)
            {
                entry.isReady = newReady;
                EditorUtility.SetDirty(_config);
            }

            EditorGUILayout.EndHorizontal();

            // Description
            if (!string.IsNullOrEmpty(def.description))
            {
                EditorGUILayout.LabelField(def.description, EditorStyles.wordWrappedMiniLabel);
            }
            
            // Hint for drag & drop / paste
            if (string.IsNullOrEmpty(entry.sourcePath))
            {
                EditorGUILayout.LabelField("↓ Drop image here or Ctrl+V to paste", _hintStyle);
            }

            // Source image row with paste support
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Source:", GUILayout.Width(50));

            // Track focus for Ctrl+V
            var textFieldName = $"source_{def.id}";
            GUI.SetNextControlName(textFieldName);
            var newPath = EditorGUILayout.TextField(entry.sourcePath);
            
            // Check if this field is focused
            if (GUI.GetNameOfFocusedControl() == textFieldName)
            {
                _focusedAssetId = def.id;
                _focusedAssetDef = def;
                _focusedAssetEntries = entries;
            }
            
            if (newPath != entry.sourcePath)
            {
                entry.sourcePath = newPath;
                EditorUtility.SetDirty(_config);
                // Clear preview cache for this entry
                if (_previewCache.ContainsKey(def.id))
                {
                    DestroyImmediate(_previewCache[def.id]);
                    _previewCache.Remove(def.id);
                }
            }
            
            // Paste button
            GUI.enabled = ClipboardImageHelper.HasImage();
            if (GUILayout.Button("⎘", GUILayout.Width(22))) // Paste icon
            {
                PasteImageFromClipboard(def, entries);
            }
            GUI.enabled = true;

            if (GUILayout.Button("...", GUILayout.Width(25)))
            {
                var path = EditorUtility.OpenFilePanel("Select Image", 
                    Application.dataPath, "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(path))
                {
                    entry.sourcePath = GetRelativePath(path);
                    EditorUtility.SetDirty(_config);
                    if (_previewCache.ContainsKey(def.id))
                    {
                        DestroyImmediate(_previewCache[def.id]);
                        _previewCache.Remove(def.id);
                    }
                }
            }
            
            // Open source folder
            GUI.enabled = hasSource;
            if (GUILayout.Button("↱", GUILayout.Width(22))) // Open source
            {
                EditorUtility.RevealInFinder(sourcePath);
            }
            
            // Delete source
            var prevBgColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(20))) // Delete source
            {
                if (EditorUtility.DisplayDialog("Удалить исходник?", 
                    $"Удалить {Path.GetFileName(entry.sourcePath)}?", "Да", "Нет"))
                {
                    if (hasSource)
                    {
                        File.Delete(sourcePath);
                        AssetDatabase.Refresh();
                    }
                    entry.sourcePath = "";
                    EditorUtility.SetDirty(_config);
                    ClearPreviewForAsset(def.id);
                    SetStatus($"✗ Удалено: {def.displayName} source", Color.yellow);
                }
            }
            GUI.backgroundColor = prevBgColor;
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // Show current size if image exists (use cached values)
            if (hasSource)
            {
                var sizeStatus = sizeOk
                    ? $"✓ {sourceSize.x}×{sourceSize.y}"
                    : $"⚠ {sourceSize.x}×{sourceSize.y} (нужно минимум {def.width}×{def.height})";

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(54);
                
                var prevColor = GUI.contentColor;
                GUI.contentColor = sizeOk ? Color.green : Color.red;
                EditorGUILayout.LabelField(sizeStatus, EditorStyles.miniLabel);
                GUI.contentColor = prevColor;
                
                EditorGUILayout.EndHorizontal();
            }

            // Prompt field
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Prompt:", GUILayout.Width(50));
            
            var newPrompt = EditorGUILayout.TextField(entry.prompt);
            if (newPrompt != entry.prompt)
            {
                entry.prompt = newPrompt;
                EditorUtility.SetDirty(_config);
            }

            if (GUILayout.Button("Copy", GUILayout.Width(45)))
            {
                if (!string.IsNullOrEmpty(entry.prompt))
                {
                    GUIUtility.systemCopyBuffer = entry.prompt;
                    SetStatus("Prompt copied!", Color.green);
                }
            }

            EditorGUILayout.EndHorizontal();
            
            // Upload URL (auto-generated from SteamConfig)
            if (_currentTab == PlatformTab.Steam && !string.IsNullOrEmpty(def.steamworksUrl))
            {
                var hasAppId = _steamConfig != null && !string.IsNullOrEmpty(_steamConfig.appId);
                var uploadUrl = hasAppId 
                    ? GetSteamworksUrl(def.steamworksUrl, _steamConfig.appId)
                    : "";
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Upload:", GUILayout.Width(50));
                
                if (hasAppId)
                {
                    // Readonly text field with URL
                    EditorGUILayout.SelectableLabel(uploadUrl, EditorStyles.textField, GUILayout.Height(18));
                    
                    // Copy button
                    if (GUILayout.Button("⎘", GUILayout.Width(22))) // Copy icon
                    {
                        GUIUtility.systemCopyBuffer = uploadUrl;
                        SetStatus("✓ URL скопирован", Color.green);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("⚠ Set App ID in SteamConfig", EditorStyles.miniLabel);
                    if (GUILayout.Button("Setup", GUILayout.Width(50)))
                    {
                        if (_steamConfig != null)
                            Selection.activeObject = _steamConfig;
                        else
                            EditorApplication.ExecuteMenuItem("ProtoSystem/Publishing/Build Publisher");
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }

            // Cache output check
            var outputPath = !string.IsNullOrEmpty(entry.outputPath) ? GetFullPath(entry.outputPath) : null;
            var hasOutput = outputPath != null && File.Exists(outputPath);
            
            // Action buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(54);

            GUI.enabled = hasSource;
            if (GUILayout.Button("Prepare", GUILayout.Width(70)))
            {
                PrepareAsset(def, entry);
            }

            if (GUILayout.Button("Preview", GUILayout.Width(60)))
            {
                PreviewAsset(def, entry);
            }
            GUI.enabled = true;
            
            // Open output folder
            GUI.enabled = hasOutput;
            if (GUILayout.Button("Open Output", GUILayout.Width(80)))
            {
                EditorUtility.RevealInFinder(outputPath);
            }
            
            // Delete output
            var prevColorOut = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(20)))
            {
                if (EditorUtility.DisplayDialog("Удалить output?", 
                    $"Удалить {Path.GetFileName(entry.outputPath)}?", "Да", "Нет"))
                {
                    if (hasOutput)
                    {
                        File.Delete(outputPath);
                        AssetDatabase.Refresh();
                    }
                    entry.outputPath = "";
                    entry.isReady = false;
                    EditorUtility.SetDirty(_config);
                    SetStatus($"✗ Удалено: {def.displayName} output", Color.yellow);
                }
            }
            GUI.backgroundColor = prevColorOut;
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Actions

        private void HandleDragAndDrop(Rect dropArea, StoreAssetDefinition def, StoreAssetEntry entry)
        {
            var e = Event.current;
            
            if (!dropArea.Contains(e.mousePosition))
                return;
                
            switch (e.type)
            {
                case EventType.DragUpdated:
                    // Check if dragging valid image files
                    if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                    {
                        var path = DragAndDrop.paths[0];
                        var ext = Path.GetExtension(path).ToLower();
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            e.Use();
                        }
                    }
                    break;
                    
                case EventType.DragPerform:
                    if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        var sourcePath = DragAndDrop.paths[0];
                        var ext = Path.GetExtension(sourcePath).ToLower();
                        
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                        {
                            // Copy to sources folder and update entry
                            var savedPath = SaveImageToSources(sourcePath, def);
                            if (!string.IsNullOrEmpty(savedPath))
                            {
                                entry.sourcePath = savedPath;
                                EditorUtility.SetDirty(_config);
                                ClearPreviewForAsset(def.id);
                                SetStatus($"✓ Dropped: {def.displayName}", Color.green);
                            }
                        }
                        e.Use();
                    }
                    break;
            }
        }
        
        private void PasteImageFromClipboard(StoreAssetDefinition def, List<StoreAssetEntry> entries)
        {
            var texture = ClipboardImageHelper.GetImage();
            if (texture == null)
            {
                SetStatus("No image in clipboard", Color.red);
                return;
            }
            
            try
            {
                // Save to sources folder
                var sourcesFolder = GetSourcesFolder();
                if (!Directory.Exists(sourcesFolder))
                {
                    Directory.CreateDirectory(sourcesFolder);
                }
                
                var fileName = $"{def.id}_source.png";
                var fullPath = Path.Combine(sourcesFolder, fileName);
                
                if (ClipboardImageHelper.SaveAsPng(texture, fullPath))
                {
                    var entry = _config.GetOrCreateEntry(def.id, entries);
                    entry.sourcePath = GetRelativePath(fullPath);
                    EditorUtility.SetDirty(_config);
                    ClearPreviewForAsset(def.id);
                    
                    AssetDatabase.Refresh();
                    SetStatus($"✓ Pasted: {def.displayName} ({texture.width}×{texture.height})", Color.green);
                }
                else
                {
                    SetStatus("Failed to save pasted image", Color.red);
                }
            }
            finally
            {
                DestroyImmediate(texture);
            }
        }
        
        private string SaveImageToSources(string sourcePath, StoreAssetDefinition def)
        {
            try
            {
                var sourcesFolder = GetSourcesFolder();
                if (!Directory.Exists(sourcesFolder))
                {
                    Directory.CreateDirectory(sourcesFolder);
                }
                
                var ext = Path.GetExtension(sourcePath);
                var fileName = $"{def.id}_source{ext}";
                var destPath = Path.Combine(sourcesFolder, fileName);
                
                File.Copy(sourcePath, destPath, overwrite: true);
                AssetDatabase.Refresh();
                
                return GetRelativePath(destPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StoreAssets] Failed to copy image: {ex.Message}");
                SetStatus($"Error: {ex.Message}", Color.red);
                return null;
            }
        }
        
        private string GetSourcesFolder()
        {
            // Store sources in: Assets/<Namespace>/Publishing/StoreAssets/Sources/<platform>/
            var projectNamespace = GetProjectNamespace();
            var platform = _currentTab.ToString().ToLower();
            return Path.Combine(Application.dataPath, projectNamespace, "Publishing", "StoreAssets", "Sources", platform);
        }
        
        private void ClearPreviewForAsset(string assetId)
        {
            if (_previewCache.ContainsKey(assetId))
            {
                DestroyImmediate(_previewCache[assetId]);
                _previewCache.Remove(assetId);
            }
            // Clear size cache for this asset
            var keysToRemove = _imageSizeCache.Keys.Where(k => k.Contains(assetId)).ToList();
            foreach (var key in keysToRemove)
                _imageSizeCache.Remove(key);
        }

        private void PrepareAsset(StoreAssetDefinition def, StoreAssetEntry entry)
        {
            var sourcePath = GetFullPath(entry.sourcePath);
            if (!File.Exists(sourcePath))
            {
                SetStatus($"Source file not found: {entry.sourcePath}", Color.red);
                return;
            }

            var sourceSize = GetImageSize(sourcePath);
            if (sourceSize.x < def.width || sourceSize.y < def.height)
            {
                EditorUtility.DisplayDialog("Image Too Small",
                    $"Source image ({sourceSize.x}×{sourceSize.y}) is smaller than required ({def.width}×{def.height}).\n\n" +
                    "Please provide a larger source image.",
                    "OK");
                return;
            }

            try
            {
                // Create output directory
                var outputDir = Path.Combine(_config.GetOutputPath(), _currentTab.ToString().ToLower());
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Load source texture
                var bytes = File.ReadAllBytes(sourcePath);
                var sourceTex = new Texture2D(2, 2);
                sourceTex.LoadImage(bytes);

                // Resize
                var resizedTex = ResizeTexture(sourceTex, def.width, def.height);

                // Save
                var outputFileName = $"{def.id}.png";
                var outputPath = Path.Combine(outputDir, outputFileName);
                
                var pngBytes = def.allowTransparency 
                    ? resizedTex.EncodeToPNG() 
                    : EncodeToJPGorPNG(resizedTex, def.allowTransparency);
                
                File.WriteAllBytes(outputPath, pngBytes);

                // Update entry
                entry.outputPath = GetRelativePath(outputPath);
                entry.lastModified = DateTime.Now;
                entry.isReady = true;
                EditorUtility.SetDirty(_config);

                // Cleanup
                DestroyImmediate(sourceTex);
                DestroyImmediate(resizedTex);

                SetStatus($"✓ Prepared: {def.displayName} ({def.width}×{def.height})", Color.green);
                
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}", Color.red);
                Debug.LogError($"[StoreAssets] Failed to prepare {def.displayName}: {ex}");
            }
        }

        private byte[] EncodeToJPGorPNG(Texture2D tex, bool allowTransparency)
        {
            return allowTransparency ? tex.EncodeToPNG() : tex.EncodeToJPG(95);
        }

        private Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            // Calculate scale to fit and cover (crop)
            float scaleX = (float)targetWidth / source.width;
            float scaleY = (float)targetHeight / source.height;
            float scale = Mathf.Max(scaleX, scaleY);

            int scaledWidth = Mathf.RoundToInt(source.width * scale);
            int scaledHeight = Mathf.RoundToInt(source.height * scale);

            // Create RenderTexture for high quality resize
            var rt = RenderTexture.GetTemporary(scaledWidth, scaledHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;

            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            // Create result texture
            var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            
            // Calculate crop offset (center)
            int offsetX = (scaledWidth - targetWidth) / 2;
            int offsetY = (scaledHeight - targetHeight) / 2;
            
            result.ReadPixels(new Rect(offsetX, offsetY, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        private void PreviewAsset(StoreAssetDefinition def, StoreAssetEntry entry)
        {
            var sourcePath = GetFullPath(entry.sourcePath);
            if (!File.Exists(sourcePath))
            {
                SetStatus("Source file not found", Color.red);
                return;
            }

            // Open in system viewer
            System.Diagnostics.Process.Start(sourcePath);
        }

        private void CreateNewConfig()
        {
            // Получаем namespace из ProjectConfig
            var projectNamespace = GetProjectNamespace();
            
            var defaultFolder = $"Assets/{projectNamespace}/Settings/Publishing";
            
            // Создаём папку если не существует
            var fullDefaultFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), defaultFolder);
            if (!Directory.Exists(fullDefaultFolder))
            {
                Directory.CreateDirectory(fullDefaultFolder);
                AssetDatabase.Refresh();
            }
            
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Store Assets Config", 
                "StoreAssetsConfig", "asset", 
                "Select location for Store Assets Config",
                defaultFolder);

            if (!string.IsNullOrEmpty(path))
            {
                var config = StoreAssetsConfig.CreateDefault(projectNamespace);
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                _config = config;
                Selection.activeObject = config;
            }
        }

        /// <summary>
        /// Получить namespace проекта из ProjectConfig
        /// </summary>
        private string GetProjectNamespace()
        {
            // Ищем ProjectConfig в проекте
            var guids = AssetDatabase.FindAssets("t:ProjectConfig");
            if (guids.Length > 0)
            {
                var projectConfig = AssetDatabase.LoadAssetAtPath<ProtoSystem.ProjectConfig>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
                if (projectConfig != null && !string.IsNullOrEmpty(projectConfig.projectNamespace))
                {
                    return projectConfig.projectNamespace;
                }
            }
            
            // Fallback: используем productName
            var productName = PlayerSettings.productName;
            if (string.IsNullOrEmpty(productName) || productName == "New Unity Project")
            {
                return "Game";
            }
            return Regex.Replace(productName, @"[^a-zA-Z0-9]", "");
        }

        #endregion

        #region Helpers

        private SteamConfig FindSteamConfig()
        {
            var guids = AssetDatabase.FindAssets("t:SteamConfig");
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<SteamConfig>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            return null;
        }
        
        private string GetSteamworksUrl(string assetType, string appId)
        {
            switch (assetType)
            {
                case "graphicalassets":
                    return $"https://partner.steamgames.com/admin/game/edit/{appId}?activetab=tab_graphicalassets";
                case "clientimages":
                    return $"https://partner.steamgames.com/apps/clientimages/{appId}";
                default:
                    return $"https://partner.steamgames.com/admin/game/edit/{appId}?activetab=tab_graphicalassets";
            }
        }

        private string GetFullPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return "";
            
            if (Path.IsPathRooted(relativePath))
                return relativePath;
                
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), relativePath);
        }

        private string GetRelativePath(string fullPath)
        {
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            if (fullPath.StartsWith(projectPath))
            {
                return fullPath.Substring(projectPath.Length + 1);
            }
            return fullPath;
        }

        private Vector2Int GetImageSizeCached(string path)
        {
            if (string.IsNullOrEmpty(path)) return Vector2Int.zero;
            
            // Check cache first
            if (_imageSizeCache.TryGetValue(path, out var cachedSize))
                return cachedSize;
            
            // Load and cache
            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                var size = new Vector2Int(tex.width, tex.height);
                DestroyImmediate(tex);
                _imageSizeCache[path] = size;
                return size;
            }
            catch
            {
                _imageSizeCache[path] = Vector2Int.zero;
                return Vector2Int.zero;
            }
        }
        
        private Vector2Int GetImageSize(string path)
        {
            return GetImageSizeCached(path);
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUI.contentColor = _statusColor;
            EditorGUILayout.LabelField($"● {_statusMessage}");
            GUI.contentColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void SetStatus(string message, Color color)
        {
            _statusMessage = message;
            _statusColor = color;
            Repaint();
        }

        #endregion
    }
}
