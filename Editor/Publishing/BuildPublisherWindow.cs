// Packages/com.protosystem.core/Editor/Publishing/BuildPublisherWindow.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.Publishing.Editor
{
    public class BuildPublisherWindow : EditorWindow
    {
        #region Enums & Constants
        
        private enum PlatformTab { Steam, Itch, Epic, GOG }
        
        private const string STEAMWORKS_BUILDS_URL = "https://partner.steamgames.com/apps/builds/";
        private const string STEAMWORKS_DEPOTS_URL = "https://partner.steamgames.com/apps/depots/";
        private const string STEAM_HELP_URL = "https://partner.steamgames.com/doc/sdk/uploading";
        
        #endregion

        #region State
        
        private PlatformTab _currentTab = PlatformTab.Steam;
        
        // Configs
        private PublishingConfig _mainConfig;
        private SteamConfig _steamConfig;
        private PatchNotesData _patchNotesData;
        private CommitTagConfig _tagConfig;

        // UI State
        private Vector2 _scrollPos;
        private Vector2 _historyScrollPos;
        private bool _foldoutPlatform = true;
        private bool _foldoutSteamSetup = false;
        private bool _foldoutBuild = true;
        private bool _foldoutPatchNotes = true;
        private bool _foldoutGit = true;
        private bool _foldoutHistory = false;

        // Build settings
        private int _selectedDepotIndex;
        private int _selectedBranchIndex;
        private string _buildDescription = "";

        // Patch notes
        private int _selectedTemplateIndex;
        private PatchNotesEntry _currentEntry;
        private bool _showBBCodePreview;

        // Git
        private bool _createGitTag = true;
        private bool _pushGitTag = true;

        // Status
        private string _statusMessage = "Ready";
        private Color _statusColor = Color.green;
        private bool _isProcessing;

        // SDK Search
        private List<SDKSearchResult> _steamCmdResults;
        private bool _isSearchingSteamCmd;
        private string _searchProgress = "";

        #endregion

        #region Cached Data (updated only on changes)
        
        private class CachedData
        {
            // Git
            public string currentBranch;
            public string lastTag;
            public int commitCount;
            public List<GitCommitInfo> recentCommits;
            public bool gitAvailable;
            public bool isGitRepo;
            
            // Steam
            public bool steamConfigValid;
            public string steamConfigError;
            public string[] branchNames;
            public string[] depotNames;
            public bool hasSteamPassword;
            
            // Patch notes
            public string[] templateNames;
            public string historyInfo;
            
            // Timestamps
            public double lastGitRefresh;
            public double lastSteamRefresh;
        }
        
        private CachedData _cache = new CachedData();
        private const double CACHE_REFRESH_INTERVAL = 5.0; // seconds
        
        #endregion

        [MenuItem("ProtoSystem/Publishing/Build Publisher", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<BuildPublisherWindow>("Build Publisher");
            window.minSize = new Vector2(600, 750);
            window.Show();
        }

        private void OnEnable()
        {
            LoadConfigs();
            RefreshAllCache(force: true);
            LoadCurrentEntry();
        }

        private void OnFocus()
        {
            RefreshAllCache(force: false);
        }

        #region Config Loading
        
        private void LoadConfigs()
        {
            _mainConfig = FindAssetOfType<PublishingConfig>();
            _steamConfig = FindAssetOfType<SteamConfig>();
            _patchNotesData = FindAssetOfType<PatchNotesData>();
            _tagConfig = FindAssetOfType<CommitTagConfig>();

            if (_mainConfig != null)
            {
                _steamConfig = _mainConfig.steamConfig ?? _steamConfig;
                _patchNotesData = _mainConfig.patchNotesData ?? _patchNotesData;
                _createGitTag = _mainConfig.createGitTag;
                _pushGitTag = _mainConfig.pushGitTag;
            }

            if (_patchNotesData != null)
            {
                _tagConfig = _patchNotesData.tagConfig ?? _tagConfig;
            }
        }

        private T FindAssetOfType<T>() where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            return null;
        }

        private void LoadCurrentEntry()
        {
            if (_patchNotesData != null)
            {
                _currentEntry = _patchNotesData.GetOrCreateCurrent();
                EditorUtility.SetDirty(_patchNotesData);
            }
        }

        #endregion

        #region Cache Management
        
        private void RefreshAllCache(bool force)
        {
            var now = EditorApplication.timeSinceStartup;
            
            if (force || now - _cache.lastGitRefresh > CACHE_REFRESH_INTERVAL)
            {
                RefreshGitCache();
                _cache.lastGitRefresh = now;
            }
            
            if (force || now - _cache.lastSteamRefresh > CACHE_REFRESH_INTERVAL)
            {
                RefreshSteamCache();
                _cache.lastSteamRefresh = now;
            }
            
            RefreshPatchNotesCache();
        }

        private void RefreshGitCache()
        {
            _cache.gitAvailable = GitIntegration.IsGitAvailable();
            _cache.isGitRepo = _cache.gitAvailable && GitIntegration.IsGitRepository();
            
            if (!_cache.isGitRepo)
            {
                _cache.currentBranch = "Not a Git repository";
                _cache.recentCommits = new List<GitCommitInfo>();
                _cache.commitCount = 0;
                return;
            }

            _cache.currentBranch = GitIntegration.GetCurrentBranch();
            _cache.lastTag = GitIntegration.GetLastTag();
            
            var lastProcessedCommit = GetLastProcessedCommitHash();
            if (!string.IsNullOrEmpty(lastProcessedCommit))
            {
                _cache.recentCommits = GitIntegration.GetCommits($"{lastProcessedCommit}..HEAD");
            }
            else if (!string.IsNullOrEmpty(_cache.lastTag))
            {
                _cache.recentCommits = GitIntegration.GetCommitsSinceTag(_cache.lastTag);
            }
            else
            {
                _cache.recentCommits = GitIntegration.GetCommits("HEAD~50..HEAD", 50);
            }
            
            _cache.commitCount = _cache.recentCommits?.Count ?? 0;
        }

        private void RefreshSteamCache()
        {
            if (_steamConfig == null)
            {
                _cache.steamConfigValid = false;
                _cache.steamConfigError = "No config";
                _cache.branchNames = new string[0];
                _cache.depotNames = new string[0];
                return;
            }
            
            _cache.steamConfigValid = _steamConfig.Validate(out _cache.steamConfigError);
            
            _cache.branchNames = _steamConfig.branches?.Select(b => b.name).ToArray() ?? new string[0];
            
            var depots = _steamConfig.depotConfig?.GetEnabledDepots();
            _cache.depotNames = depots?.Select(d => $"{d.displayName} ({d.depotId})").ToArray() ?? new string[0];
            
            _cache.hasSteamPassword = SecureCredentials.HasPassword("steam", _steamConfig.username ?? "");
        }

        private void RefreshPatchNotesCache()
        {
            _cache.templateNames = _patchNotesData?.templates?.Select(t => t.name).ToArray() ?? new string[0];
            
            var entryCount = _patchNotesData?.entries?.Count ?? 0;
            _cache.historyInfo = $"History ({entryCount} versions)";
        }

        private string GetLastProcessedCommitHash()
        {
            if (_patchNotesData?.entries == null) return null;

            foreach (var entry in _patchNotesData.entries)
            {
                if (entry.commitHashes?.Count > 0)
                    return entry.commitHashes[0];
            }
            return null;
        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            DrawPlatformTabs();
            
            EditorGUILayout.Space(10);

            switch (_currentTab)
            {
                case PlatformTab.Steam:
                    DrawSteamTab();
                    break;
                default:
                    DrawNotImplementedTab();
                    break;
            }

            EditorGUILayout.Space(10);
            DrawPatchNotesSection();
            
            EditorGUILayout.Space(10);
            DrawHistorySection();
            
            EditorGUILayout.Space(10);
            DrawGitSection();

            EditorGUILayout.Space(10);
            DrawActionButtons();

            EditorGUILayout.Space(10);
            DrawStatusBar();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Build Publisher", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                LoadConfigs();
                RefreshAllCache(force: true);
                LoadCurrentEntry();
                Repaint();
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        private void DrawPlatformTabs()
        {
            EditorGUILayout.BeginHorizontal();

            var tabs = new[] { "Steam", "itch.io", "Epic", "GOG" };
            var icons = new[] { "●", "○", "○", "○" };

            for (int i = 0; i < tabs.Length; i++)
            {
                var selected = _currentTab == (PlatformTab)i;
                GUI.backgroundColor = selected ? new Color(0.7f, 0.9f, 1f) : Color.white;
                
                if (GUILayout.Button($"{icons[i]} {tabs[i]}", EditorStyles.toolbarButton, GUILayout.Height(25)))
                {
                    _currentTab = (PlatformTab)i;
                }
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Steam Tab

        private void DrawSteamTab()
        {
            // Quick actions bar
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            GUI.enabled = !string.IsNullOrEmpty(_steamConfig?.appId);
            if (GUILayout.Button("📂 Open Steamworks Builds", GUILayout.Height(24)))
            {
                Application.OpenURL(STEAMWORKS_BUILDS_URL + _steamConfig.appId);
            }
            if (GUILayout.Button("📋 Open Depots", GUILayout.Height(24)))
            {
                Application.OpenURL(STEAMWORKS_DEPOTS_URL + _steamConfig.appId);
            }
            GUI.enabled = true;
            
            if (GUILayout.Button("❓ Help", GUILayout.Width(50), GUILayout.Height(24)))
            {
                Application.OpenURL(STEAM_HELP_URL);
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Config selection
            _foldoutPlatform = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPlatform, "Steam Config");
            
            if (_foldoutPlatform)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Config asset
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Config:", GUILayout.Width(50));
                
                var newConfig = (SteamConfig)EditorGUILayout.ObjectField(_steamConfig, typeof(SteamConfig), false);
                if (newConfig != _steamConfig)
                {
                    _steamConfig = newConfig;
                    RefreshSteamCache();
                }
                
                if (GUILayout.Button("New", GUILayout.Width(45)))
                {
                    CreateNewSteamConfig();
                }
                EditorGUILayout.EndHorizontal();

                if (_steamConfig != null)
                {
                    EditorGUILayout.Space(5);
                    
                    // Status
                    var statusColor = _cache.steamConfigValid ? Color.green : Color.red;
                    var statusIcon = _cache.steamConfigValid ? "✓" : "✗";
                    GUI.contentColor = statusColor;
                    EditorGUILayout.LabelField($"{statusIcon} {(_cache.steamConfigValid ? "Ready to upload" : _cache.steamConfigError)}");
                    GUI.contentColor = Color.white;

                    EditorGUILayout.Space(5);

                    // Quick settings (always visible)
                    if (_cache.branchNames.Length > 0)
                    {
                        _selectedBranchIndex = Mathf.Clamp(_selectedBranchIndex, 0, _cache.branchNames.Length - 1);
                        _selectedBranchIndex = EditorGUILayout.Popup("Branch:", _selectedBranchIndex, _cache.branchNames);
                    }
                    
                    if (_cache.depotNames.Length > 0)
                    {
                        _selectedDepotIndex = Mathf.Clamp(_selectedDepotIndex, 0, _cache.depotNames.Length - 1);
                        _selectedDepotIndex = EditorGUILayout.Popup("Depot:", _selectedDepotIndex, _cache.depotNames);
                    }
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Setup panel (expandable)
            DrawSteamSetupPanel();

            // Build Settings
            DrawBuildSettingsPanel();
        }

        private void DrawSteamSetupPanel()
        {
            _foldoutSteamSetup = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSteamSetup, 
                "⚙️ Steam Setup (Click to configure)");

            if (_foldoutSteamSetup)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (_steamConfig == null)
                {
                    EditorGUILayout.HelpBox("Create Steam Config first", MessageType.Warning);
                    if (GUILayout.Button("Create Steam Config"))
                    {
                        CreateNewSteamConfig();
                    }
                }
                else
                {
                    EditorGUI.BeginChangeCheck();

                    // App ID
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("App ID *", 
                        "Your Steam App ID from Steamworks Partner site.\nFind it at: partner.steamgames.com"), 
                        GUILayout.Width(120));
                    _steamConfig.appId = EditorGUILayout.TextField(_steamConfig.appId);
                    if (GUILayout.Button("?", GUILayout.Width(20)))
                    {
                        Application.OpenURL("https://partner.steamgames.com/apps");
                    }
                    EditorGUILayout.EndHorizontal();

                    // App Name
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("App Name", 
                        "Display name (optional, for your reference)"), GUILayout.Width(120));
                    _steamConfig.appName = EditorGUILayout.TextField(_steamConfig.appName);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Account", EditorStyles.boldLabel);

                    // Username
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Username *", 
                        "Steam account with upload permissions.\nRecommended: create separate account for CI/CD"), 
                        GUILayout.Width(120));
                    _steamConfig.username = EditorGUILayout.TextField(_steamConfig.username);
                    EditorGUILayout.EndHorizontal();

                    // Password
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Password", 
                        "⚠️ Stored in EditorPrefs (not secure!).\nUse account without valuable items."), 
                        GUILayout.Width(120));

                    var passStatus = _cache.hasSteamPassword ? "✓ Saved" : "Not set";
                    EditorGUILayout.LabelField(passStatus, GUILayout.Width(80));

                    if (GUILayout.Button(_cache.hasSteamPassword ? "Change" : "Set"))
                    {
                        ShowPasswordDialog();
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("SteamCMD", EditorStyles.boldLabel);

                    // SteamCMD Path
                    DrawSteamCmdPathField();

                    EditorGUILayout.Space(10);

                    // Depots inline editor
                    DrawDepotsEditor();

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);

                    // Preview Mode
                    _steamConfig.previewMode = EditorGUILayout.Toggle(
                        new GUIContent("Preview Mode", "Validates upload without actually uploading"), 
                        _steamConfig.previewMode);

                    // Auto Set Live
                    _steamConfig.autoSetLive = EditorGUILayout.Toggle(
                        new GUIContent("Auto Set Live", "Automatically set build live on selected branch after upload"), 
                        _steamConfig.autoSetLive);

                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(_steamConfig);
                        if (_steamConfig.depotConfig != null)
                            EditorUtility.SetDirty(_steamConfig.depotConfig);
                        RefreshSteamCache();
                    }
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawDepotsEditor()
        {
            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Depots", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(_steamConfig.appId) && GUILayout.Button("Open in Steamworks", GUILayout.Width(120)))
            {
                Application.OpenURL(STEAMWORKS_DEPOTS_URL + _steamConfig.appId);
            }
            EditorGUILayout.EndHorizontal();

            // Depot Config asset (hidden, auto-created)
            if (_steamConfig.depotConfig == null)
            {
                EditorGUILayout.HelpBox("No depot configuration. Click 'Add Depot' to create one.", MessageType.Info);

                if (GUILayout.Button("+ Add Depot"))
                {
                    CreateDepotConfigIfNeeded();
                    AddNewDepot();
                }
                return;
            }

            // Depots list
            var depots = _steamConfig.depotConfig.depots;
            int removeIndex = -1;

            for (int i = 0; i < depots.Count; i++)
            {
                var depot = depots[i];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Header row
                EditorGUILayout.BeginHorizontal();

                depot.enabled = EditorGUILayout.Toggle(depot.enabled, GUILayout.Width(20));

                GUI.enabled = depot.enabled;

                // Status icon
                var pathExists = !string.IsNullOrEmpty(depot.buildPath) && Directory.Exists(depot.buildPath);
                var statusIcon = pathExists ? "✓" : "⚠";
                var statusTooltip = pathExists ? "Build folder exists" : "Build folder not found";

                EditorGUILayout.LabelField(new GUIContent($"{statusIcon} {depot.displayName}", statusTooltip), 
                    EditorStyles.boldLabel, GUILayout.Width(150));

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField(depot.depotId, EditorStyles.miniLabel, GUILayout.Width(80));

                GUI.enabled = true;
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                {
                    if (EditorUtility.DisplayDialog("Remove Depot", 
                        $"Remove depot '{depot.displayName}'?", "Remove", "Cancel"))
                    {
                        removeIndex = i;
                    }
                }
                EditorGUILayout.EndHorizontal();

                GUI.enabled = depot.enabled;

                // Row 1: Name + Depot ID
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Name", "Display name for this depot"), GUILayout.Width(50));
                depot.displayName = EditorGUILayout.TextField(depot.displayName, GUILayout.Width(120));

                EditorGUILayout.LabelField(new GUIContent("Depot ID", 
                    "Find at Steamworks → App → Depots.\nUsually App ID + 1, +2, etc."), GUILayout.Width(55));
                depot.depotId = EditorGUILayout.TextField(depot.depotId, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();

                // Row 2: Platform
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Platform", GUILayout.Width(50));
                depot.buildTarget = (BuildTarget)EditorGUILayout.EnumPopup(depot.buildTarget);
                EditorGUILayout.EndHorizontal();

                // Row 3: Build Path
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Build Path", "Relative path to build output folder"), 
                    GUILayout.Width(65));
                depot.buildPath = EditorGUILayout.TextField(depot.buildPath);

                if (GUILayout.Button("...", GUILayout.Width(25)))
                {
                    var startPath = string.IsNullOrEmpty(depot.buildPath) ? "Builds" : depot.buildPath;
                    var path = EditorUtility.OpenFolderPanel("Select Build Folder", startPath, "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var projectPath = Path.GetDirectoryName(Application.dataPath);
                        if (path.StartsWith(projectPath))
                        {
                            path = path.Substring(projectPath.Length + 1);
                        }
                        depot.buildPath = path;
                    }
                }

                if (GUILayout.Button("Open", GUILayout.Width(45)))
                {
                    var fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), depot.buildPath);
                    if (Directory.Exists(fullPath))
                    {
                        EditorUtility.RevealInFinder(fullPath);
                    }
                    else
                    {
                        if (EditorUtility.DisplayDialog("Folder Not Found", 
                            $"Create folder '{depot.buildPath}'?", "Create", "Cancel"))
                        {
                            Directory.CreateDirectory(fullPath);
                            EditorUtility.RevealInFinder(fullPath);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                // Row 4: File filters (expandable)
                var hasFilters = !string.IsNullOrEmpty(depot.includePatterns) || 
                                !string.IsNullOrEmpty(depot.excludePatterns);
                var filterKey = $"BuildPublisher_DepotFilter_{i}";
                var showFilters = EditorPrefs.GetBool(filterKey, hasFilters);

                var newShowFilters = EditorGUILayout.Foldout(showFilters, "File Filters", true);
                if (newShowFilters != showFilters)
                {
                    EditorPrefs.SetBool(filterKey, newShowFilters);
                }

                if (newShowFilters)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Include", 
                        "Comma-separated patterns to include.\nEmpty = include all files.\nExample: *.exe, *.dll"), 
                        GUILayout.Width(50));
                    depot.includePatterns = EditorGUILayout.TextField(depot.includePatterns);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Exclude", 
                        "Comma-separated patterns to exclude.\nExample: *.pdb, *.log, *.meta"), 
                        GUILayout.Width(50));
                    depot.excludePatterns = EditorGUILayout.TextField(depot.excludePatterns);
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel--;
                }

                GUI.enabled = true;

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            // Remove depot if requested
            if (removeIndex >= 0)
            {
                depots.RemoveAt(removeIndex);
                EditorUtility.SetDirty(_steamConfig.depotConfig);
                RefreshSteamCache();
            }

            // Add depot button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Depot", GUILayout.Width(100)))
            {
                AddNewDepot();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void CreateDepotConfigIfNeeded()
        {
            if (_steamConfig.depotConfig != null) return;

            // Auto-create depot config next to steam config
            var steamConfigPath = AssetDatabase.GetAssetPath(_steamConfig);
            var directory = Path.GetDirectoryName(steamConfigPath);
            var depotConfigPath = Path.Combine(directory, "DepotConfig.asset");

            // Ensure unique name
            depotConfigPath = AssetDatabase.GenerateUniqueAssetPath(depotConfigPath);

            var config = ScriptableObject.CreateInstance<DepotConfig>();
            AssetDatabase.CreateAsset(config, depotConfigPath);

            _steamConfig.depotConfig = config;
            EditorUtility.SetDirty(_steamConfig);
            AssetDatabase.SaveAssets();
        }

        private void AddNewDepot()
        {
            CreateDepotConfigIfNeeded();

            var depots = _steamConfig.depotConfig.depots;

            // Calculate default depot ID
            var defaultDepotId = "";
            if (!string.IsNullOrEmpty(_steamConfig.appId) && long.TryParse(_steamConfig.appId, out var appId))
            {
                defaultDepotId = (appId + depots.Count + 1).ToString();
            }

            // Determine platform based on existing depots
            var platform = BuildTarget.StandaloneWindows64;
            var platformName = "Windows x64";

            if (depots.Any(d => d.buildTarget == BuildTarget.StandaloneWindows64))
            {
                if (!depots.Any(d => d.buildTarget == BuildTarget.StandaloneLinux64))
                {
                    platform = BuildTarget.StandaloneLinux64;
                    platformName = "Linux x64";
                }
                else if (!depots.Any(d => d.buildTarget == BuildTarget.StandaloneOSX))
                {
                    platform = BuildTarget.StandaloneOSX;
                    platformName = "macOS";
                }
            }

            depots.Add(new DepotEntry
            {
                depotId = defaultDepotId,
                displayName = platformName,
                buildTarget = platform,
                buildPath = $"Builds/{platformName.Replace(" ", "")}",
                enabled = true
            });

            EditorUtility.SetDirty(_steamConfig.depotConfig);
            RefreshSteamCache();
        }

        private void DrawSteamCmdPathField()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("SteamCMD Path *", 
                "Path to steamcmd.exe\nDownload from: partner.steamgames.com/doc/sdk"), 
                GUILayout.Width(120));
            
            if (_steamConfig != null)
            {
                _steamConfig.steamCmdPath = EditorGUILayout.TextField(_steamConfig.steamCmdPath);
            }
            
            if (GUILayout.Button("...", GUILayout.Width(25)))
            {
                var path = EditorUtility.OpenFilePanel("Select SteamCMD", "", 
                    Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "sh");
                if (!string.IsNullOrEmpty(path) && _steamConfig != null)
                {
                    _steamConfig.steamCmdPath = path;
                    EditorUtility.SetDirty(_steamConfig);
                }
            }
            EditorGUILayout.EndHorizontal();

            // Search button
            if (_steamConfig != null && (string.IsNullOrEmpty(_steamConfig.steamCmdPath) || 
                !File.Exists(_steamConfig.steamCmdPath)))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(124);
                
                GUI.enabled = !_isSearchingSteamCmd;
                if (GUILayout.Button(_isSearchingSteamCmd ? _searchProgress : "🔍 Search on all drives"))
                {
                    SearchSteamCmdAsync();
                }
                GUI.enabled = true;
                
                if (GUILayout.Button("Download", GUILayout.Width(70)))
                {
                    Application.OpenURL("https://developer.valvesoftware.com/wiki/SteamCMD#Downloading_SteamCMD");
                }
                EditorGUILayout.EndHorizontal();

                // Show search results
                if (_steamCmdResults != null && _steamCmdResults.Count > 0)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField("Found:", EditorStyles.boldLabel);
                    
                    foreach (var result in _steamCmdResults.Take(5))
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(result.Path, EditorStyles.miniLabel);
                        if (GUILayout.Button("Use", GUILayout.Width(40)))
                        {
                            _steamConfig.steamCmdPath = result.Path;
                            EditorUtility.SetDirty(_steamConfig);
                            _steamCmdResults = null;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    if (_steamCmdResults.Count > 5)
                    {
                        EditorGUILayout.LabelField($"... and {_steamCmdResults.Count - 5} more", EditorStyles.miniLabel);
                    }
                    
                    EditorGUILayout.EndVertical();
                }
            }
            else if (_steamConfig != null && File.Exists(_steamConfig.steamCmdPath))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(124);
                GUI.contentColor = Color.green;
                EditorGUILayout.LabelField("✓ Found", EditorStyles.miniLabel);
                GUI.contentColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawBuildSettingsPanel()
        {
            _foldoutBuild = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutBuild, "Build Settings");
            
            if (_foldoutBuild)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                _buildDescription = EditorGUILayout.TextField(
                    new GUIContent("Description", "Build description shown in Steamworks"), 
                    _buildDescription);

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawNotImplementedTab()
        {
            EditorGUILayout.HelpBox(
                $"{_currentTab} integration is not yet implemented.\n\n" +
                "Planned features:\n" +
                "• Configuration management\n" +
                "• Build upload\n" +
                "• Patch notes publishing",
                MessageType.Info);
        }

        #endregion

        #region Patch Notes Section

        private void DrawPatchNotesSection()
        {
            _foldoutPatchNotes = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPatchNotes, "Version & Patch Notes");
            
            if (_foldoutPatchNotes)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Data asset
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Data:", GUILayout.Width(40));
                var newData = (PatchNotesData)EditorGUILayout.ObjectField(_patchNotesData, typeof(PatchNotesData), false);
                if (newData != _patchNotesData)
                {
                    _patchNotesData = newData;
                    LoadCurrentEntry();
                    RefreshPatchNotesCache();
                }
                
                if (GUILayout.Button("New", GUILayout.Width(45)))
                {
                    CreateNewPatchNotesData();
                }
                EditorGUILayout.EndHorizontal();

                if (_patchNotesData == null)
                {
                    EditorGUILayout.HelpBox("Create or assign PatchNotesData asset", MessageType.Warning);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndFoldoutHeaderGroup();
                    return;
                }

                EditorGUILayout.Space(5);

                // Version controls
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Version:", GUILayout.Width(55));
                
                EditorGUI.BeginChangeCheck();
                var newVersion = EditorGUILayout.TextField(_patchNotesData.currentVersion, GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck() && newVersion != _patchNotesData.currentVersion)
                {
                    _patchNotesData.currentVersion = newVersion;
                    EditorUtility.SetDirty(_patchNotesData);
                }

                if (GUILayout.Button("+0.0.1", GUILayout.Width(50))) IncrementVersion(VersionIncrement.Patch);
                if (GUILayout.Button("+0.1.0", GUILayout.Width(50))) IncrementVersion(VersionIncrement.Minor);
                if (GUILayout.Button("+1.0.0", GUILayout.Width(50))) IncrementVersion(VersionIncrement.Major);
                
                EditorGUILayout.EndHorizontal();

                // Entry info
                if (_currentEntry != null)
                {
                    var authorsStr = _currentEntry.authors?.Count > 0 
                        ? string.Join(", ", _currentEntry.authors.Take(3)) 
                        : "None";
                    if (_currentEntry.authors?.Count > 3) authorsStr += "...";
                    
                    EditorGUILayout.LabelField($"Commits: {_currentEntry.commitHashes?.Count ?? 0} | Authors: {authorsStr}", 
                        EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(5);

                // Template
                if (_cache.templateNames.Length > 0)
                {
                    _selectedTemplateIndex = Mathf.Clamp(_selectedTemplateIndex, 0, _cache.templateNames.Length - 1);
                    _selectedTemplateIndex = EditorGUILayout.Popup("Template:", _selectedTemplateIndex, _cache.templateNames);
                }

                // Content
                EditorGUILayout.LabelField("Patch Notes (Markdown):");
                
                if (_currentEntry != null)
                {
                    EditorGUI.BeginChangeCheck();
                    _currentEntry.content = EditorGUILayout.TextArea(_currentEntry.content ?? "", 
                        GUILayout.MinHeight(100));
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(_patchNotesData);
                    }
                }

                // Actions
                EditorGUILayout.BeginHorizontal();
                
                GUI.enabled = _cache.commitCount > 0;
                if (GUILayout.Button($"From Git ({_cache.commitCount} commits)"))
                {
                    GenerateFromGitCommits();
                }
                GUI.enabled = true;
                
                _showBBCodePreview = GUILayout.Toggle(_showBBCodePreview, "BBCode", "Button", GUILayout.Width(60));
                
                if (GUILayout.Button("Save", GUILayout.Width(50)))
                {
                    SavePatchNotes();
                }
                
                EditorGUILayout.EndHorizontal();

                // BBCode preview
                if (_showBBCodePreview && _currentEntry != null && !string.IsNullOrEmpty(_currentEntry.content))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("BBCode Preview:", EditorStyles.boldLabel);
                    
                    var bbcode = MarkdownToBBCode.Convert(_currentEntry.content);
                    EditorGUILayout.TextArea(bbcode, GUILayout.MinHeight(60));
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawHistorySection()
        {
            if (_patchNotesData?.entries == null || _patchNotesData.entries.Count == 0)
                return;

            _foldoutHistory = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutHistory, _cache.historyInfo);
            
            if (_foldoutHistory)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                _historyScrollPos = EditorGUILayout.BeginScrollView(_historyScrollPos, GUILayout.MaxHeight(150));
                
                foreach (var entry in _patchNotesData.entries)
                {
                    var isCurrent = entry.version == _patchNotesData.currentVersion;
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    var prefix = isCurrent ? "► " : "  ";
                    var style = isCurrent ? EditorStyles.boldLabel : EditorStyles.label;
                    
                    EditorGUILayout.LabelField($"{prefix}{entry.version}", style, GUILayout.Width(80));
                    EditorGUILayout.LabelField(entry.date ?? "", GUILayout.Width(80));
                    EditorGUILayout.LabelField($"{entry.commitHashes?.Count ?? 0}c, {entry.authors?.Count ?? 0}a", 
                        EditorStyles.miniLabel, GUILayout.Width(60));
                    
                    if (!isCurrent && GUILayout.Button("Load", GUILayout.Width(45)))
                    {
                        _patchNotesData.currentVersion = entry.version;
                        _currentEntry = entry;
                        EditorUtility.SetDirty(_patchNotesData);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        #endregion

        #region Git Section

        private void DrawGitSection()
        {
            _foldoutGit = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutGit, "Git Integration");
            
            if (_foldoutGit)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (!_cache.gitAvailable)
                {
                    EditorGUILayout.HelpBox("Git not found in PATH", MessageType.Warning);
                }
                else if (!_cache.isGitRepo)
                {
                    EditorGUILayout.HelpBox("Not a Git repository", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Branch: {_cache.currentBranch}", GUILayout.Width(200));
                    EditorGUILayout.LabelField($"Last Tag: {_cache.lastTag ?? "None"}");
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField($"New commits: {_cache.commitCount}");

                    EditorGUILayout.Space(5);

                    _createGitTag = EditorGUILayout.Toggle("Create tag on publish", _createGitTag);
                    
                    GUI.enabled = _createGitTag;
                    _pushGitTag = EditorGUILayout.Toggle("Push tag to remote", _pushGitTag);
                    GUI.enabled = true;

                    if (_cache.commitCount > 0 && GUILayout.Button($"View {_cache.commitCount} commits"))
                    {
                        ShowCommitsPopup();
                    }
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        #endregion

        #region Action Buttons

        private void DrawActionButtons()
        {
            GUI.enabled = !_isProcessing && _steamConfig != null && _cache.steamConfigValid;

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Build Only", GUILayout.Height(30)))
            {
                BuildOnly();
            }

            if (GUILayout.Button("Upload Only", GUILayout.Height(30)))
            {
                UploadOnly();
            }

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Build & Publish", GUILayout.Height(30)))
            {
                BuildAndPublish();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            GUI.enabled = true;
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            GUI.contentColor = _statusColor;
            EditorGUILayout.LabelField($"● {_statusMessage}");
            GUI.contentColor = Color.white;
            
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Actions

        private async void SearchSteamCmdAsync()
        {
            _isSearchingSteamCmd = true;
            _steamCmdResults = new List<SDKSearchResult>();
            _searchProgress = "Searching...";
            Repaint();

            await Task.Run(() =>
            {
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                    .ToArray();

                foreach (var drive in drives)
                {
                    _searchProgress = $"Searching {drive.Name}...";
                    
                    try
                    {
                        SearchInDirectory(drive.RootDirectory.FullName, "steamcmd.exe", 4);
                    }
                    catch { }
                }
            });

            // Add known paths
            var knownPaths = SDKPathFinder.FindSteamCmd();
            foreach (var path in knownPaths)
            {
                if (!_steamCmdResults.Any(r => r.Path == path.Path))
                {
                    _steamCmdResults.Insert(0, path);
                }
            }

            _isSearchingSteamCmd = false;
            _searchProgress = "";
            Repaint();
        }

        private void SearchInDirectory(string path, string fileName, int maxDepth)
        {
            if (maxDepth <= 0) return;

            try
            {
                var files = Directory.GetFiles(path, fileName, SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    _steamCmdResults.Add(new SDKSearchResult { Path = file, IsValid = true, Source = "Disk" });
                }

                foreach (var dir in Directory.GetDirectories(path))
                {
                    var dirName = Path.GetFileName(dir).ToLower();
                    
                    // Skip system and hidden directories
                    if (dirName.StartsWith(".") || dirName == "windows" || dirName == "$recycle.bin" ||
                        dirName == "system volume information" || dirName == "programdata")
                        continue;

                    SearchInDirectory(dir, fileName, maxDepth - 1);
                }
            }
            catch { }
        }

        private void IncrementVersion(VersionIncrement increment)
        {
            if (_patchNotesData == null) return;
            
            _patchNotesData.IncrementVersion(increment);
            _currentEntry = _patchNotesData.GetOrCreateCurrent();
            
            EditorUtility.SetDirty(_patchNotesData);
            AssetDatabase.SaveAssets();
            
            SetStatus($"Version: {_patchNotesData.currentVersion}", Color.green);
            Repaint();
        }

        private void CreateNewSteamConfig()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Steam Config", "SteamConfig", "asset", "");

            if (!string.IsNullOrEmpty(path))
            {
                var config = SteamConfig.CreateDefault();
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                _steamConfig = config;
                RefreshSteamCache();
                Selection.activeObject = config;
            }
        }



        private void CreateNewPatchNotesData()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Patch Notes Data", "PatchNotesData", "asset", "");

            if (!string.IsNullOrEmpty(path))
            {
                var data = PatchNotesData.CreateDefault();
                AssetDatabase.CreateAsset(data, path);
                AssetDatabase.SaveAssets();
                _patchNotesData = data;
                LoadCurrentEntry();
                RefreshPatchNotesCache();
                Selection.activeObject = data;
            }
        }

        private void ShowPasswordDialog()
        {
            if (_steamConfig == null || string.IsNullOrEmpty(_steamConfig.username))
            {
                EditorUtility.DisplayDialog("Error", "Set username first", "OK");
                return;
            }

            var password = EditorInputDialog.Show(
                "Steam Password",
                $"Enter password for {_steamConfig.username}:\n\n⚠️ Warning: Password stored insecurely!",
                "", true);

            if (!string.IsNullOrEmpty(password))
            {
                SecureCredentials.SetPassword("steam", _steamConfig.username, password);
                RefreshSteamCache();
                SetStatus("Password saved", Color.green);
            }
        }

        private void GenerateFromGitCommits()
        {
            if (_cache.recentCommits == null || _cache.recentCommits.Count == 0)
            {
                EditorUtility.DisplayDialog("No Commits", "No new commits found", "OK");
                return;
            }

            if (_currentEntry == null) LoadCurrentEntry();

            _currentEntry.commitEntries.Clear();
            _currentEntry.commitHashes.Clear();
            _currentEntry.authors.Clear();
            _currentEntry.date = DateTime.Now.ToString("yyyy-MM-dd");

            var taggedEntries = GitIntegration.ParseCommitsToEntries(_cache.recentCommits, _tagConfig);
            
            foreach (var commit in _cache.recentCommits)
            {
                _currentEntry.commitHashes.Add(commit.Hash);
                if (!string.IsNullOrEmpty(commit.Author) && !_currentEntry.authors.Contains(commit.Author))
                {
                    _currentEntry.authors.Add(commit.Author);
                }
            }
            
            if (taggedEntries.Count > 0)
            {
                _currentEntry.commitEntries = taggedEntries;
                _currentEntry.GenerateContentFromCommits(_tagConfig, true);
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### Changes");
                sb.AppendLine();
                
                foreach (var commit in _cache.recentCommits.Take(50))
                {
                    sb.AppendLine($"- {commit.Subject}");
                    
                    _currentEntry.commitEntries.Add(new CommitEntry
                    {
                        message = commit.Subject,
                        commitHash = commit.Hash,
                        shortHash = commit.ShortHash,
                        author = commit.Author,
                        date = commit.Date.ToString("yyyy-MM-dd"),
                        includeInPublic = true
                    });
                }
                
                _currentEntry.content = sb.ToString();
            }
            
            _currentEntry.authors.Sort();
            EditorUtility.SetDirty(_patchNotesData);
            
            SetStatus($"Generated: {_currentEntry.commitEntries.Count} entries, {_currentEntry.authors.Count} authors", Color.green);
        }

        private void SavePatchNotes()
        {
            if (_patchNotesData == null || _currentEntry == null) return;
            
            _currentEntry.date = DateTime.Now.ToString("yyyy-MM-dd");
            
            if (!_patchNotesData.entries.Contains(_currentEntry))
            {
                _patchNotesData.entries.Insert(0, _currentEntry);
            }
            
            EditorUtility.SetDirty(_patchNotesData);
            AssetDatabase.SaveAssets();
            
            RefreshGitCache();
            RefreshPatchNotesCache();
            
            SetStatus("Patch notes saved", Color.green);
        }

        private void ShowCommitsPopup()
        {
            if (_cache.recentCommits == null) return;
            
            var sb = new System.Text.StringBuilder();
            
            foreach (var commit in _cache.recentCommits.Take(20))
            {
                sb.AppendLine($"[{commit.ShortHash}] {commit.Author}: {commit.Subject}");
            }
            
            if (_cache.recentCommits.Count > 20)
            {
                sb.AppendLine($"... and {_cache.recentCommits.Count - 20} more");
            }
            
            EditorUtility.DisplayDialog("New Commits", sb.ToString(), "OK");
        }

        private async void BuildOnly()
        {
            SetStatus("Building...", Color.yellow);
            _isProcessing = true;
            
            await Task.Delay(100);
            
            _isProcessing = false;
            SetStatus("Build not yet implemented", Color.red);
        }

        private async void UploadOnly()
        {
            _isProcessing = true;
            SetStatus("Uploading to Steam...", Color.yellow);

            try
            {
                var publisher = new SteamPublisher(_steamConfig);
                var branch = _cache.branchNames[_selectedBranchIndex];
                var depots = _steamConfig.depotConfig.GetEnabledDepots();
                var depot = depots[_selectedDepotIndex];
                
                var progress = new Progress<PublishProgress>(p =>
                {
                    SetStatus(p.Status, Color.yellow);
                    Repaint();
                });

                var result = await publisher.UploadAsync(depot.buildPath, branch, _buildDescription, progress);

                if (result.Success)
                {
                    SetStatus($"✓ {result.Message}", Color.green);
                    
                    if (_createGitTag && _patchNotesData != null)
                    {
                        var tagName = _mainConfig?.GetGitTag(_patchNotesData.currentVersion) 
                            ?? $"v{_patchNotesData.currentVersion}";
                        
                        if (GitIntegration.CreateTag(tagName, _buildDescription) && _pushGitTag)
                        {
                            GitIntegration.PushTag(tagName);
                        }
                    }
                }
                else
                {
                    SetStatus($"✗ {result.Error}", Color.red);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"✗ {ex.Message}", Color.red);
            }

            _isProcessing = false;
            RefreshGitCache();
        }

        private async void BuildAndPublish()
        {
            SetStatus("Build & Publish not yet implemented", Color.yellow);
            _isProcessing = true;
            await Task.Delay(100);
            _isProcessing = false;
        }

        private void SetStatus(string message, Color color)
        {
            _statusMessage = message;
            _statusColor = color;
            Repaint();
        }

        #endregion
    }

    public class EditorInputDialog : EditorWindow
    {
        private string _value = "";
        private string _message;
        private bool _isPassword;
        private bool _confirmed;

        public static string Show(string title, string message, string defaultValue = "", bool isPassword = false)
        {
            var instance = CreateInstance<EditorInputDialog>();
            instance.titleContent = new GUIContent(title);
            instance._message = message;
            instance._value = defaultValue;
            instance._isPassword = isPassword;
            instance.minSize = new Vector2(350, 100);
            instance.maxSize = new Vector2(400, 120);
            
            instance.ShowModalUtility();
            
            return instance._confirmed ? instance._value : null;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(5);
            
            GUI.SetNextControlName("InputField");
            _value = _isPassword ? EditorGUILayout.PasswordField(_value) : EditorGUILayout.TextField(_value);
            EditorGUI.FocusTextInControl("InputField");

            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("OK") || 
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            {
                _confirmed = true;
                Close();
            }
            
            if (GUILayout.Button("Cancel")) Close();
            
            EditorGUILayout.EndHorizontal();
        }
    }
}
