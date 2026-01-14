// Packages/com.protosystem.core/Editor/Publishing/BuildPublisherWindow.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Главное окно Build Publisher
    /// </summary>
    public class BuildPublisherWindow : EditorWindow
    {
        // Вкладки платформ
        private enum PlatformTab { Steam, Itch, Epic, GOG }
        private PlatformTab _currentTab = PlatformTab.Steam;

        // Конфиги
        private PublishingConfig _mainConfig;
        private SteamConfig _steamConfig;
        private PatchNotesData _patchNotesData;
        private CommitTagConfig _tagConfig;

        // UI State
        private Vector2 _scrollPos;
        private Vector2 _historyScrollPos;
        private bool _foldoutPlatform = true;
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
        private int _selectedHistoryIndex = -1;

        // Git
        private string _currentBranch;
        private string _lastTag;
        private int _commitCount;
        private List<GitCommitInfo> _recentCommits;
        private bool _createGitTag = true;
        private bool _pushGitTag = true;

        // SDK Search
        private List<SDKSearchResult> _steamCmdResults;
        private bool _showSdkSearch;

        // Status
        private string _statusMessage = "Ready";
        private Color _statusColor = Color.green;
        private bool _isProcessing;

        [MenuItem("ProtoSystem/Publishing/Build Publisher", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<BuildPublisherWindow>("Build Publisher");
            window.minSize = new Vector2(550, 700);
            window.Show();
        }

        private void OnEnable()
        {
            LoadConfigs();
            RefreshGitInfo();
            LoadCurrentEntry();
        }

        private void LoadConfigs()
        {
            // Ищем главный конфиг
            var guids = AssetDatabase.FindAssets("t:PublishingConfig");
            if (guids.Length > 0)
            {
                _mainConfig = AssetDatabase.LoadAssetAtPath<PublishingConfig>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            // Ищем Steam конфиг
            guids = AssetDatabase.FindAssets("t:SteamConfig");
            if (guids.Length > 0)
            {
                _steamConfig = AssetDatabase.LoadAssetAtPath<SteamConfig>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            // Ищем PatchNotesData
            guids = AssetDatabase.FindAssets("t:PatchNotesData");
            if (guids.Length > 0)
            {
                _patchNotesData = AssetDatabase.LoadAssetAtPath<PatchNotesData>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            // Ищем TagConfig
            guids = AssetDatabase.FindAssets("t:CommitTagConfig");
            if (guids.Length > 0)
            {
                _tagConfig = AssetDatabase.LoadAssetAtPath<CommitTagConfig>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }

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

        private void LoadCurrentEntry()
        {
            if (_patchNotesData != null)
            {
                _currentEntry = _patchNotesData.GetOrCreateCurrent();
                EditorUtility.SetDirty(_patchNotesData);
            }
        }

        private void RefreshGitInfo()
        {
            if (!GitIntegration.IsGitAvailable() || !GitIntegration.IsGitRepository())
            {
                _currentBranch = "Not a Git repository";
                return;
            }

            _currentBranch = GitIntegration.GetCurrentBranch();
            _lastTag = GitIntegration.GetLastTag();
            
            // Считаем коммиты после последнего сохранённого патчноута
            var lastProcessedCommit = GetLastProcessedCommitHash();
            if (!string.IsNullOrEmpty(lastProcessedCommit))
            {
                _recentCommits = GitIntegration.GetCommits($"{lastProcessedCommit}..HEAD");
            }
            else if (!string.IsNullOrEmpty(_lastTag))
            {
                _recentCommits = GitIntegration.GetCommitsSinceTag(_lastTag);
            }
            else
            {
                _recentCommits = GitIntegration.GetCommits("HEAD~50..HEAD", 50);
            }
            
            _commitCount = _recentCommits?.Count ?? 0;
        }

        /// <summary>
        /// Получить хеш последнего обработанного коммита из истории патчноутов
        /// </summary>
        private string GetLastProcessedCommitHash()
        {
            if (_patchNotesData == null || _patchNotesData.entries == null) 
                return null;

            foreach (var entry in _patchNotesData.entries)
            {
                if (entry.commitHashes != null && entry.commitHashes.Count > 0)
                {
                    return entry.commitHashes[0]; // Первый = самый новый
                }
            }
            return null;
        }

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
                RefreshGitInfo();
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

        private void DrawSteamTab()
        {
            _foldoutPlatform = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPlatform, "Platform Config");
            
            if (_foldoutPlatform)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Config:", GUILayout.Width(50));
                _steamConfig = (SteamConfig)EditorGUILayout.ObjectField(_steamConfig, typeof(SteamConfig), false);
                
                if (GUILayout.Button("Edit", GUILayout.Width(40)) && _steamConfig != null)
                {
                    Selection.activeObject = _steamConfig;
                }
                
                if (GUILayout.Button("New", GUILayout.Width(40)))
                {
                    CreateNewSteamConfig();
                }
                EditorGUILayout.EndHorizontal();

                if (_steamConfig != null)
                {
                    EditorGUILayout.Space(5);
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"App ID: {_steamConfig.appId}", GUILayout.Width(150));
                    
                    if (_steamConfig.branches != null && _steamConfig.branches.Count > 0)
                    {
                        var branchNames = _steamConfig.branches.Select(b => b.name).ToArray();
                        _selectedBranchIndex = EditorGUILayout.Popup("Branch:", _selectedBranchIndex, branchNames);
                    }
                    EditorGUILayout.EndHorizontal();

                    if (_steamConfig.depotConfig != null)
                    {
                        var depots = _steamConfig.depotConfig.GetEnabledDepots();
                        if (depots.Count > 0)
                        {
                            var depotNames = depots.Select(d => $"{d.displayName} ({d.depotId})").ToArray();
                            _selectedDepotIndex = EditorGUILayout.Popup("Depot:", _selectedDepotIndex, depotNames);
                        }
                    }

                    var isValid = _steamConfig.Validate(out var error);
                    var statusStyle = new GUIStyle(EditorStyles.label);
                    statusStyle.normal.textColor = isValid ? Color.green : Color.red;
                    EditorGUILayout.LabelField($"Status: {(isValid ? "✓ Ready" : $"✗ {error}")}", statusStyle);

                    if (string.IsNullOrEmpty(_steamConfig.steamCmdPath) || !File.Exists(_steamConfig.steamCmdPath))
                    {
                        EditorGUILayout.Space(5);
                        DrawSteamCmdSearch();
                    }

                    var hasPassword = SecureCredentials.HasPassword("steam", _steamConfig.username ?? "");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Password: {(hasPassword ? "✓ Saved" : "✗ Not set")}", GUILayout.Width(120));
                    
                    if (GUILayout.Button(hasPassword ? "Change" : "Set", GUILayout.Width(60)))
                    {
                        ShowPasswordDialog();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _foldoutBuild = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutBuild, "Build Settings");
            
            if (_foldoutBuild)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                _buildDescription = EditorGUILayout.TextField("Description:", _buildDescription);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Preview Mode:", GUILayout.Width(100));
                if (_steamConfig != null)
                {
                    _steamConfig.previewMode = EditorGUILayout.Toggle(_steamConfig.previewMode);
                    EditorGUILayout.LabelField("(No actual upload)", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Auto Set Live:", GUILayout.Width(100));
                if (_steamConfig != null)
                {
                    _steamConfig.autoSetLive = EditorGUILayout.Toggle(_steamConfig.autoSetLive);
                }
                EditorGUILayout.EndHorizontal();

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

        private void DrawSteamCmdSearch()
        {
            _showSdkSearch = EditorGUILayout.Foldout(_showSdkSearch, "⚠️ SteamCMD not found - Click to search");
            
            if (_showSdkSearch)
            {
                if (_steamCmdResults == null)
                {
                    _steamCmdResults = SDKPathFinder.FindSteamCmd();
                }

                if (_steamCmdResults.Count > 0)
                {
                    EditorGUILayout.LabelField("Found installations:", EditorStyles.boldLabel);
                    
                    foreach (var result in _steamCmdResults)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"[{result.Source}] {result.Path}", EditorStyles.miniLabel);
                        
                        if (GUILayout.Button("Use", GUILayout.Width(40)))
                        {
                            _steamConfig.steamCmdPath = result.Path;
                            EditorUtility.SetDirty(_steamConfig);
                            SDKPathFinder.SavePath("SteamCmd", result.Path);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("SteamCMD not found.", MessageType.Warning);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Browse..."))
                {
                    var path = EditorUtility.OpenFilePanel("Select SteamCMD", "", 
                        Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "sh");
                    
                    if (!string.IsNullOrEmpty(path))
                    {
                        _steamConfig.steamCmdPath = path;
                        EditorUtility.SetDirty(_steamConfig);
                    }
                }
                
                if (GUILayout.Button("Refresh"))
                {
                    _steamCmdResults = SDKPathFinder.FindSteamCmd();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPatchNotesSection()
        {
            _foldoutPatchNotes = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPatchNotes, "Version & Patch Notes");
            
            if (_foldoutPatchNotes)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // PatchNotesData
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Data:", GUILayout.Width(40));
                var newData = (PatchNotesData)EditorGUILayout.ObjectField(_patchNotesData, typeof(PatchNotesData), false);
                if (newData != _patchNotesData)
                {
                    _patchNotesData = newData;
                    LoadCurrentEntry();
                }
                
                if (GUILayout.Button("New", GUILayout.Width(40)))
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
                
                var newVersion = EditorGUILayout.TextField(_patchNotesData.currentVersion, GUILayout.Width(80));
                if (newVersion != _patchNotesData.currentVersion)
                {
                    _patchNotesData.currentVersion = newVersion;
                    EditorUtility.SetDirty(_patchNotesData);
                }

                if (GUILayout.Button("+0.0.1", GUILayout.Width(50)))
                {
                    IncrementVersion(VersionIncrement.Patch);
                }
                if (GUILayout.Button("+0.1.0", GUILayout.Width(50)))
                {
                    IncrementVersion(VersionIncrement.Minor);
                }
                if (GUILayout.Button("+1.0.0", GUILayout.Width(50)))
                {
                    IncrementVersion(VersionIncrement.Major);
                }
                
                EditorGUILayout.EndHorizontal();

                // Current entry info
                if (_currentEntry != null)
                {
                    EditorGUILayout.Space(3);
                    
                    var infoStyle = EditorStyles.miniLabel;
                    var authorsStr = _currentEntry.authors?.Count > 0 
                        ? string.Join(", ", _currentEntry.authors) 
                        : "None";
                    var commitsStr = _currentEntry.commitHashes?.Count.ToString() ?? "0";
                    
                    EditorGUILayout.LabelField($"Commits: {commitsStr} | Authors: {authorsStr}", infoStyle);
                }

                EditorGUILayout.Space(5);

                // Template
                if (_patchNotesData.templates != null && _patchNotesData.templates.Count > 0)
                {
                    var templateNames = _patchNotesData.templates.Select(t => t.name).ToArray();
                    _selectedTemplateIndex = EditorGUILayout.Popup("Template:", _selectedTemplateIndex, templateNames);
                }

                // Content
                EditorGUILayout.LabelField("Patch Notes (Markdown):");
                
                if (_currentEntry != null)
                {
                    _currentEntry.content = EditorGUILayout.TextArea(_currentEntry.content ?? "", GUILayout.MinHeight(100));
                }

                // Actions
                EditorGUILayout.BeginHorizontal();
                
                GUI.enabled = _commitCount > 0;
                if (GUILayout.Button($"From Git ({_commitCount} commits)"))
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
            if (_patchNotesData == null || _patchNotesData.entries == null || _patchNotesData.entries.Count == 0)
                return;

            _foldoutHistory = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutHistory, 
                $"History ({_patchNotesData.entries.Count} versions)");
            
            if (_foldoutHistory)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                _historyScrollPos = EditorGUILayout.BeginScrollView(_historyScrollPos, GUILayout.MaxHeight(150));
                
                for (int i = 0; i < _patchNotesData.entries.Count; i++)
                {
                    var entry = _patchNotesData.entries[i];
                    var isCurrent = entry.version == _patchNotesData.currentVersion;
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    var style = isCurrent ? EditorStyles.boldLabel : EditorStyles.label;
                    var prefix = isCurrent ? "► " : "  ";
                    var authorsCount = entry.authors?.Count ?? 0;
                    var commitsCount = entry.commitHashes?.Count ?? 0;
                    
                    EditorGUILayout.LabelField($"{prefix}{entry.version}", style, GUILayout.Width(80));
                    EditorGUILayout.LabelField(entry.date ?? "", GUILayout.Width(80));
                    EditorGUILayout.LabelField($"{commitsCount} commits, {authorsCount} authors", 
                        EditorStyles.miniLabel);
                    
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

        private void DrawGitSection()
        {
            _foldoutGit = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutGit, "Git Integration");
            
            if (_foldoutGit)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (!GitIntegration.IsGitAvailable())
                {
                    EditorGUILayout.HelpBox("Git not found in PATH", MessageType.Warning);
                }
                else if (!GitIntegration.IsGitRepository())
                {
                    EditorGUILayout.HelpBox("Not a Git repository", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Branch: {_currentBranch}", GUILayout.Width(200));
                    EditorGUILayout.LabelField($"Last Tag: {_lastTag ?? "None"}");
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField($"New commits: {_commitCount}");

                    EditorGUILayout.Space(5);

                    _createGitTag = EditorGUILayout.Toggle("Create tag on publish", _createGitTag);
                    
                    GUI.enabled = _createGitTag;
                    _pushGitTag = EditorGUILayout.Toggle("Push tag to remote", _pushGitTag);
                    GUI.enabled = true;

                    if (_commitCount > 0 && GUILayout.Button($"View {_commitCount} commits"))
                    {
                        ShowCommitsPopup();
                    }
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawActionButtons()
        {
            GUI.enabled = !_isProcessing && _steamConfig != null;

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
            
            var statusStyle = new GUIStyle(EditorStyles.label);
            statusStyle.normal.textColor = _statusColor;
            
            EditorGUILayout.LabelField($"● {_statusMessage}", statusStyle);
            
            EditorGUILayout.EndHorizontal();
        }

        #region Actions

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
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Steam Config", "SteamConfig", "asset",
                "Select location for Steam config");

            if (!string.IsNullOrEmpty(path))
            {
                var config = SteamConfig.CreateDefault();
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                _steamConfig = config;
                Selection.activeObject = config;
            }
        }

        private void CreateNewPatchNotesData()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Patch Notes Data", "PatchNotesData", "asset",
                "Select location");

            if (!string.IsNullOrEmpty(path))
            {
                var data = PatchNotesData.CreateDefault();
                AssetDatabase.CreateAsset(data, path);
                AssetDatabase.SaveAssets();
                _patchNotesData = data;
                LoadCurrentEntry();
                Selection.activeObject = data;
            }
        }

        private void ShowPasswordDialog()
        {
            if (_steamConfig == null || string.IsNullOrEmpty(_steamConfig.username))
            {
                EditorUtility.DisplayDialog("Error", "Set username in Steam config first", "OK");
                return;
            }

            var password = EditorInputDialog.Show(
                "Steam Password",
                $"Enter password for {_steamConfig.username}:",
                "", true);

            if (!string.IsNullOrEmpty(password))
            {
                SecureCredentials.SetPassword("steam", _steamConfig.username, password);
                SetStatus("Password saved", Color.green);
            }
        }

        private void GenerateFromGitCommits()
        {
            if (_recentCommits == null || _recentCommits.Count == 0)
            {
                EditorUtility.DisplayDialog("No Commits", "No new commits found", "OK");
                return;
            }

            if (_currentEntry == null)
            {
                LoadCurrentEntry();
            }

            // Очищаем предыдущие данные
            _currentEntry.commitEntries.Clear();
            _currentEntry.commitHashes.Clear();
            _currentEntry.authors.Clear();
            _currentEntry.date = DateTime.Now.ToString("yyyy-MM-dd");

            // Парсим коммиты в записи (только с тегами)
            var taggedEntries = GitIntegration.ParseCommitsToEntries(_recentCommits, _tagConfig);

            // Добавляем все коммиты (для сохранения хешей и авторов)
            foreach (var commit in _recentCommits)
            {
                _currentEntry.commitHashes.Add(commit.Hash);

                // Добавляем автора если его ещё нет
                if (!string.IsNullOrEmpty(commit.Author) && !_currentEntry.authors.Contains(commit.Author))
                {
                    _currentEntry.authors.Add(commit.Author);
                }
            }

            // Если есть теговые записи - используем их
            if (taggedEntries.Count > 0)
            {
                _currentEntry.commitEntries = taggedEntries;
                _currentEntry.GenerateContentFromCommits(_tagConfig, true);
            }
            else
            {
                // Иначе создаём записи из всех коммитов
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### Changes");
                sb.AppendLine();

                foreach (var commit in _recentCommits.Take(50))
                {
                    sb.AppendLine($"- {commit.Subject}");

                    // Создаём CommitEntry без тега
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
            Repaint();
        }

        private void SavePatchNotes()
        {
            if (_patchNotesData == null || _currentEntry == null) return;
            
            _currentEntry.date = DateTime.Now.ToString("yyyy-MM-dd");
            
            // Убеждаемся что запись в списке
            if (!_patchNotesData.entries.Contains(_currentEntry))
            {
                _patchNotesData.entries.Insert(0, _currentEntry);
            }
            
            EditorUtility.SetDirty(_patchNotesData);
            AssetDatabase.SaveAssets();
            
            // Обновляем Git info - теперь эти коммиты обработаны
            RefreshGitInfo();
            
            SetStatus("Patch notes saved", Color.green);
        }

        private void ShowCommitsPopup()
        {
            if (_recentCommits == null) return;
            
            var sb = new System.Text.StringBuilder();
            
            foreach (var commit in _recentCommits.Take(20))
            {
                sb.AppendLine($"[{commit.ShortHash}] {commit.Author}: {commit.Subject}");
            }
            
            if (_recentCommits.Count > 20)
            {
                sb.AppendLine($"... and {_recentCommits.Count - 20} more");
            }
            
            EditorUtility.DisplayDialog("New Commits", sb.ToString(), "OK");
        }

        private async void BuildOnly()
        {
            SetStatus("Building...", Color.yellow);
            _isProcessing = true;
            
            await System.Threading.Tasks.Task.Delay(100);
            
            _isProcessing = false;
            SetStatus("Build not yet implemented", Color.red);
        }

        private async void UploadOnly()
        {
            string error = "";
            if (_steamConfig == null || !_steamConfig.Validate(out error))
            {
                SetStatus($"Config error: {error}", Color.red);
                return;
            }

            _isProcessing = true;
            SetStatus("Uploading to Steam...", Color.yellow);

            try
            {
                var publisher = new SteamPublisher(_steamConfig);
                var branch = _steamConfig.branches[_selectedBranchIndex].name;
                var depot = _steamConfig.depotConfig.GetEnabledDepots()[_selectedDepotIndex];
                
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
                        
                        if (GitIntegration.CreateTag(tagName, _buildDescription))
                        {
                            if (_pushGitTag)
                            {
                                GitIntegration.PushTag(tagName);
                            }
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
            RefreshGitInfo();
        }

        private async void BuildAndPublish()
        {
            SetStatus("Build & Publish not yet implemented", Color.yellow);
            _isProcessing = true;
            
            await System.Threading.Tasks.Task.Delay(100);
            
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

    /// <summary>
    /// Простой диалог ввода
    /// </summary>
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
            instance._confirmed = false;
            instance.minSize = new Vector2(300, 80);
            instance.maxSize = new Vector2(400, 100);
            
            instance.ShowModalUtility();
            
            return instance._confirmed ? instance._value : null;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(_message);
            
            GUI.SetNextControlName("InputField");
            _value = _isPassword 
                ? EditorGUILayout.PasswordField(_value) 
                : EditorGUILayout.TextField(_value);
            
            EditorGUI.FocusTextInControl("InputField");

            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("OK") || 
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            {
                _confirmed = true;
                Close();
            }
            
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
            
            EditorGUILayout.EndHorizontal();
        }
    }
}
