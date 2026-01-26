using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Генератор SoundLibrary из папки с AudioClip'ами
    /// </summary>
    public class SoundLibraryGenerator : EditorWindow
    {
        private DefaultAsset _sourceFolder;
        private SoundLibrary _targetLibrary;
        private SoundCategory _defaultCategory = SoundCategory.SFX;
        private bool _recursive = true;
        private bool _overwriteExisting = false;
        private string _idPrefix = "";
        
        private Vector2 _previewScroll;
        private List<AudioClipInfo> _foundClips = new();
        
        [MenuItem("ProtoSystem/Sound/Sound Library Generator", priority = 110)]
        public static void ShowWindow()
        {
            var window = GetWindow<SoundLibraryGenerator>("Sound Library Generator");
            window.minSize = new Vector2(400, 500);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Sound Library Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generate SoundLibrary entries from a folder of AudioClips.\n" +
                "Clip names become sound IDs (lowercase, spaces → underscores).",
                MessageType.Info
            );
            
            EditorGUILayout.Space(10);
            
            // Source folder
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            _sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Audio Folder",
                _sourceFolder,
                typeof(DefaultAsset),
                false
            );
            
            _recursive = EditorGUILayout.Toggle("Include Subfolders", _recursive);
            
            EditorGUILayout.Space(5);
            
            // Target
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            _targetLibrary = (SoundLibrary)EditorGUILayout.ObjectField(
                "Sound Library",
                _targetLibrary,
                typeof(SoundLibrary),
                false
            );
            
            if (_targetLibrary == null)
            {
                if (GUILayout.Button("Create New Sound Library"))
                {
                    CreateNewLibrary();
                }
            }
            
            EditorGUILayout.Space(5);
            
            // Options
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            _defaultCategory = (SoundCategory)EditorGUILayout.EnumPopup("Default Category", _defaultCategory);
            _idPrefix = EditorGUILayout.TextField("ID Prefix", _idPrefix);
            _overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", _overwriteExisting);
            
            EditorGUILayout.Space(10);
            
            // Scan button
            EditorGUI.BeginDisabledGroup(_sourceFolder == null);
            if (GUILayout.Button("Scan Folder", GUILayout.Height(30)))
            {
                ScanFolder();
            }
            EditorGUI.EndDisabledGroup();
            
            // Preview
            if (_foundClips.Count > 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField($"Found Clips: {_foundClips.Count}", EditorStyles.boldLabel);
                
                _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.MaxHeight(200));
                
                foreach (var info in _foundClips)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    
                    info.include = EditorGUILayout.Toggle(info.include, GUILayout.Width(20));
                    EditorGUILayout.LabelField(info.id, GUILayout.MinWidth(100));
                    
                    GUI.color = GetCategoryColor(info.category);
                    info.category = (SoundCategory)EditorGUILayout.EnumPopup(info.category, GUILayout.Width(70));
                    GUI.color = Color.white;
                    
                    EditorGUILayout.LabelField($"{info.clip.length:F1}s", GUILayout.Width(40));
                    
                    if (info.exists)
                    {
                        GUI.color = Color.yellow;
                        EditorGUILayout.LabelField("exists", GUILayout.Width(40));
                        GUI.color = Color.white;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
                
                EditorGUILayout.Space(5);
                
                // Select all / none
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All"))
                {
                    foreach (var info in _foundClips) info.include = true;
                }
                if (GUILayout.Button("Select None"))
                {
                    foreach (var info in _foundClips) info.include = false;
                }
                if (GUILayout.Button("Select New Only"))
                {
                    foreach (var info in _foundClips) info.include = !info.exists;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(10);
                
                // Generate button
                int selectedCount = _foundClips.Count(c => c.include);
                EditorGUI.BeginDisabledGroup(_targetLibrary == null || selectedCount == 0);
                if (GUILayout.Button($"Generate ({selectedCount} sounds)", GUILayout.Height(30)))
                {
                    GenerateEntries();
                }
                EditorGUI.EndDisabledGroup();
            }
        }
        
        private void ScanFolder()
        {
            _foundClips.Clear();
            
            if (_sourceFolder == null) return;
            
            string folderPath = AssetDatabase.GetAssetPath(_sourceFolder);
            
            // Получить все AudioClip'ы
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath });
            
            // Собрать существующие ID
            HashSet<string> existingIds = new();
            if (_targetLibrary != null)
            {
                _targetLibrary.Initialize();
                foreach (var id in _targetLibrary.GetAllIds())
                {
                    existingIds.Add(id);
                }
            }
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Проверить рекурсию
                if (!_recursive)
                {
                    string clipFolder = Path.GetDirectoryName(path).Replace("\\", "/");
                    if (clipFolder != folderPath)
                        continue;
                }
                
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                
                string id = GenerateId(clip.name);
                
                // Определить категорию по папке
                SoundCategory category = DetectCategory(path);
                
                _foundClips.Add(new AudioClipInfo
                {
                    clip = clip,
                    path = path,
                    id = id,
                    category = category,
                    include = !existingIds.Contains(id),
                    exists = existingIds.Contains(id)
                });
            }
            
            // Сортировать по имени
            _foundClips = _foundClips.OrderBy(c => c.id).ToList();
        }
        
        private void GenerateEntries()
        {
            if (_targetLibrary == null) return;
            
            Undo.RecordObject(_targetLibrary, "Generate Sound Entries");
            
            int added = 0;
            int updated = 0;
            
            foreach (var info in _foundClips)
            {
                if (!info.include) continue;
                
                // Проверить существует ли
                var existing = _targetLibrary.coreEntries.Find(e => e.id == info.id);
                
                if (existing != null)
                {
                    if (_overwriteExisting)
                    {
                        existing.clip = info.clip;
                        existing.category = info.category;
                        updated++;
                    }
                }
                else
                {
                    var entry = new SoundEntry
                    {
                        id = info.id,
                        clip = info.clip,
                        category = info.category,
                        volume = 1f,
                        pitch = 1f,
                        priority = SoundPriority.Normal
                    };
                    
                    _targetLibrary.coreEntries.Add(entry);
                    added++;
                }
            }
            
            EditorUtility.SetDirty(_targetLibrary);
            AssetDatabase.SaveAssets();
            
            SoundIdDrawer.InvalidateCache();
            
            Debug.Log($"[SoundLibraryGenerator] Added: {added}, Updated: {updated}");
            
            // Очистить preview
            _foundClips.Clear();
        }
        
        private void CreateNewLibrary()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Sound Library",
                "SoundLibrary",
                "asset",
                "Choose location for the Sound Library"
            );
            
            if (string.IsNullOrEmpty(path)) return;
            
            var library = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(library, path);
            AssetDatabase.SaveAssets();
            
            _targetLibrary = library;
            Selection.activeObject = library;
        }
        
        private string GenerateId(string clipName)
        {
            string id = clipName
                .ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_")
                .Replace(".", "_");
            
            // Удалить двойные подчёркивания
            while (id.Contains("__"))
            {
                id = id.Replace("__", "_");
            }
            
            // Добавить префикс
            if (!string.IsNullOrEmpty(_idPrefix))
            {
                id = _idPrefix + "_" + id;
            }
            
            return id;
        }
        
        private SoundCategory DetectCategory(string path)
        {
            string lower = path.ToLowerInvariant();
            
            if (lower.Contains("/music/") || lower.Contains("_music")) return SoundCategory.Music;
            if (lower.Contains("/voice/") || lower.Contains("/dialog")) return SoundCategory.Voice;
            if (lower.Contains("/ambient/") || lower.Contains("/ambience")) return SoundCategory.Ambient;
            if (lower.Contains("/ui/") || lower.Contains("/interface")) return SoundCategory.UI;
            if (lower.Contains("/sfx/") || lower.Contains("/sound")) return SoundCategory.SFX;
            
            return _defaultCategory;
        }
        
        private Color GetCategoryColor(SoundCategory category)
        {
            return category switch
            {
                SoundCategory.Music => new Color(0.7f, 0.5f, 0.9f),
                SoundCategory.SFX => new Color(0.5f, 0.8f, 0.5f),
                SoundCategory.Voice => new Color(0.9f, 0.7f, 0.5f),
                SoundCategory.Ambient => new Color(0.5f, 0.7f, 0.9f),
                SoundCategory.UI => new Color(0.9f, 0.9f, 0.5f),
                _ => Color.white
            };
        }
        
        private class AudioClipInfo
        {
            public AudioClip clip;
            public string path;
            public string id;
            public SoundCategory category;
            public bool include;
            public bool exists;
        }
    }
}
