using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Кастомный редактор для SoundLibrary
    /// </summary>
    [CustomEditor(typeof(SoundLibrary))]
    public class SoundLibraryEditor : UnityEditor.Editor
    {
        private string _searchFilter = "";
        private SoundCategory? _categoryFilter = null;
        private Vector2 _scrollPosition;
        private HashSet<int> _expandedEntries = new();
        private AudioSource _previewSource;
        
        private SerializedProperty _coreEntries;
        private SerializedProperty _banks;
        
        private GUIStyle _entryStyle;
        private GUIStyle _headerStyle;
        
        private void OnEnable()
        {
            _coreEntries = serializedObject.FindProperty("coreEntries");
            _banks = serializedObject.FindProperty("banks");
        }
        
        private void OnDisable()
        {
            StopPreview();
        }
        
        private void InitStyles()
        {
            if (_entryStyle != null) return;
            
            _entryStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(0, 0, 2, 2)
            };
            
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };
        }
        
        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            var library = (SoundLibrary)target;

            // ===== HEADER =====
            DrawHeader(library);

            EditorGUILayout.Space(10);

            // ===== TOOLBAR =====
            DrawToolbar();

            EditorGUILayout.Space(5);

            // ===== SOUNDS LIST =====
            DrawSoundsList(library);

            EditorGUILayout.Space(10);

            // ===== BANKS =====
            DrawBanksSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader(SoundLibrary library)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("📚 Sound Library", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Центральное хранилище всех звуков проекта. Каждый звук имеет уникальный ID, который используется в коде и схемах.", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(5);

            // Stats
            int totalSounds = library.coreEntries.Count;
            int missingClips = 0;
            foreach (var entry in library.coreEntries)
            {
                if (entry.clip == null && string.IsNullOrEmpty(entry.fmodEvent))
                    missingClips++;
            }

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"Звуков: {totalSounds}", GUILayout.Width(80));

            if (missingClips > 0)
            {
                GUI.color = new Color(1f, 0.8f, 0.4f);
                EditorGUILayout.LabelField($"⚠ {missingClips} без AudioClip", GUILayout.Width(120));
                GUI.color = Color.white;
            }
            else if (totalSounds > 0)
            {
                GUI.color = new Color(0.5f, 0.9f, 0.5f);
                EditorGUILayout.LabelField("✓ Все звуки настроены", GUILayout.Width(140));
                GUI.color = Color.white;
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            // Quick tips for empty library
            if (totalSounds == 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "Как добавить звуки:\n" +
                    "1. Нажмите '+ Add Sound' в тулбаре\n" +
                    "2. Укажите уникальный ID (например: ui_click, sfx_explosion)\n" +
                    "3. Назначьте AudioClip\n\n" +
                    "Или используйте Sound Setup Wizard для генерации готовых UI звуков.",
                    MessageType.Info
                );

                if (GUILayout.Button("🔧 Open Sound Setup Wizard", GUILayout.Height(24)))
                {
                    SoundSetupWizard.ShowWindow();
                }
            }

            EditorGUILayout.EndVertical();
        }
        
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // Search
            EditorGUI.BeginChangeCheck();
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.MinWidth(100));
            if (EditorGUI.EndChangeCheck())
            {
                SoundIdDrawer.InvalidateCache();
            }
            
            // Category filter
            string[] categoryNames = new[] { "All" }.Concat(System.Enum.GetNames(typeof(SoundCategory))).ToArray();
            int categoryIndex = _categoryFilter.HasValue ? (int)_categoryFilter.Value + 1 : 0;
            int newCategoryIndex = EditorGUILayout.Popup(categoryIndex, categoryNames, EditorStyles.toolbarPopup, GUILayout.Width(70));
            _categoryFilter = newCategoryIndex == 0 ? null : (SoundCategory?)(newCategoryIndex - 1);
            
            GUILayout.FlexibleSpace();
            
            // Add button
            if (GUILayout.Button("+ Add Sound", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                AddNewEntry();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawSoundsList(SoundLibrary library)
        {
            int totalVisible = 0;
            int totalCount = _coreEntries.arraySize;
            int missingClips = 0;

            // Count visible entries
            for (int i = 0; i < _coreEntries.arraySize; i++)
            {
                var entry = _coreEntries.GetArrayElementAtIndex(i);
                if (PassesFilter(entry))
                {
                    totalVisible++;
                    if (entry.FindPropertyRelative("clip").objectReferenceValue == null)
                        missingClips++;
                }
            }

            // Header with count
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Sounds ({totalVisible}/{totalCount})", _headerStyle);

            if (missingClips > 0)
            {
                GUI.color = new Color(1f, 0.8f, 0.3f);
                EditorGUILayout.LabelField($"⚠ {missingClips} missing", GUILayout.Width(80));
                GUI.color = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // Empty state
            if (totalCount == 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(60));
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("No sounds added yet", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ Add First Sound", GUILayout.Width(120)))
                {
                    AddNewEntry();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            // Sound entries (no scroll view - let inspector scroll naturally)
            for (int i = 0; i < _coreEntries.arraySize; i++)
            {
                var entry = _coreEntries.GetArrayElementAtIndex(i);

                if (!PassesFilter(entry)) continue;

                DrawSoundEntry(entry, i);
            }

            // Footer
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Validate All", GUILayout.Width(80)))
            {
                ValidateLibrary(library);
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private bool PassesFilter(SerializedProperty entry)
        {
            var idProp = entry.FindPropertyRelative("id");
            var categoryProp = entry.FindPropertyRelative("category");
            
            string id = idProp.stringValue;
            SoundCategory category = (SoundCategory)categoryProp.enumValueIndex;
            
            if (!string.IsNullOrEmpty(_searchFilter) && !id.ToLower().Contains(_searchFilter.ToLower()))
                return false;
            if (_categoryFilter.HasValue && category != _categoryFilter.Value)
                return false;
            
            return true;
        }
        
        private void DrawSoundEntry(SerializedProperty entry, int index)
        {
            var idProp = entry.FindPropertyRelative("id");
            var categoryProp = entry.FindPropertyRelative("category");
            var clipProp = entry.FindPropertyRelative("clip");
            
            string id = idProp.stringValue;
            SoundCategory category = (SoundCategory)categoryProp.enumValueIndex;
            AudioClip clip = clipProp.objectReferenceValue as AudioClip;
            
            bool isExpanded = _expandedEntries.Contains(index);
            
            EditorGUILayout.BeginVertical(_entryStyle);
            
            // === HEADER ROW ===
            EditorGUILayout.BeginHorizontal();
            
            // Foldout
            var foldoutRect = GUILayoutUtility.GetRect(16, 18, GUILayout.Width(16));
            bool newExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, GUIContent.none, true);
            if (newExpanded != isExpanded)
            {
                if (newExpanded) _expandedEntries.Add(index);
                else _expandedEntries.Remove(index);
            }
            
            // Category color bar
            var colorRect = GUILayoutUtility.GetRect(4, 18, GUILayout.Width(4));
            EditorGUI.DrawRect(colorRect, GetCategoryColor(category));
            
            GUILayout.Space(4);
            
            // ID
            EditorGUILayout.LabelField(id, EditorStyles.boldLabel, GUILayout.MinWidth(120));
            
            // Category
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            EditorGUILayout.LabelField(category.ToString(), EditorStyles.miniLabel, GUILayout.Width(55));
            GUI.color = Color.white;
            
            // Clip info / warning
            if (clip != null)
            {
                EditorGUILayout.LabelField($"{clip.length:F1}s", EditorStyles.miniLabel, GUILayout.Width(35));
                
                // Preview
                if (GUILayout.Button("▶", GUILayout.Width(22), GUILayout.Height(18)))
                {
                    float vol = entry.FindPropertyRelative("volume").floatValue;
                    float pit = entry.FindPropertyRelative("pitch").floatValue;
                    float pitVar = entry.FindPropertyRelative("pitchVariation").floatValue;
                    PlayPreview(clip, vol, pit, pitVar);
                }
            }
            else
            {
                GUI.color = new Color(1f, 0.7f, 0.3f);
                EditorGUILayout.LabelField("⚠ no clip", EditorStyles.miniLabel, GUILayout.Width(55));
                GUI.color = Color.white;
            }
            
            // Delete
            GUI.color = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(18)))
            {
                if (EditorUtility.DisplayDialog("Delete Sound", $"Delete '{id}'?", "Delete", "Cancel"))
                {
                    _coreEntries.DeleteArrayElementAtIndex(index);
                    SoundIdDrawer.InvalidateCache();
                    GUIUtility.ExitGUI();
                }
            }
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            // === EXPANDED DETAILS ===
            if (isExpanded)
            {
                EditorGUILayout.Space(4);
                
                EditorGUI.indentLevel++;
                
                // Basic
                EditorGUILayout.PropertyField(idProp);
                EditorGUILayout.PropertyField(categoryProp);
                EditorGUILayout.PropertyField(clipProp);
                
                var variantsProp = entry.FindPropertyRelative("clipVariants");
                if (variantsProp != null)
                {
                    EditorGUILayout.PropertyField(variantsProp, new GUIContent("Clip Variants"), true);
                }
                
                EditorGUILayout.Space(4);
                
                // Audio
                EditorGUILayout.LabelField("Audio", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("volume"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("pitch"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("pitchVariation"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("loop"));
                
                EditorGUILayout.Space(4);
                
                // 3D
                EditorGUILayout.LabelField("3D Sound", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("spatial"), new GUIContent("Enable 3D"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("minDistance"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("maxDistance"));
                
                EditorGUILayout.Space(4);
                
                // Priority
                EditorGUILayout.LabelField("Playback", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("priority"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("cooldown"));
                
                EditorGUILayout.Space(4);
                
                // FMOD
                EditorGUILayout.LabelField("FMOD (optional)", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("fmodEvent"), new GUIContent("Event Path"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("bankId"), new GUIContent("Bank ID"));
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawBanksSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("📦 Sound Banks", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Банки для ленивой загрузки звуков. Используйте для оптимизации памяти в больших проектах.",
                EditorStyles.wordWrappedMiniLabel
            );

            EditorGUILayout.Space(5);

            if (_banks.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "Банки не добавлены. Для большинства проектов банки не нужны — " +
                    "все звуки загружаются из Core Entries.\n\n" +
                    "Используйте банки когда:\n" +
                    "• Много звуков (100+) и нужна оптимизация памяти\n" +
                    "• Звуки привязаны к конкретным сценам/уровням\n" +
                    "• Используете FMOD и хотите ленивую загрузку банков",
                    MessageType.None
                );

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ Add Bank", GUILayout.Width(100)))
                {
                    _banks.InsertArrayElementAtIndex(_banks.arraySize);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.PropertyField(_banks, GUIContent.none, true);
            }

            EditorGUILayout.EndVertical();
        }
        
        private void AddNewEntry()
        {
            _coreEntries.InsertArrayElementAtIndex(_coreEntries.arraySize);
            var newEntry = _coreEntries.GetArrayElementAtIndex(_coreEntries.arraySize - 1);
            
            // Generate unique ID
            string baseId = "new_sound";
            int counter = 1;
            var existingIds = new HashSet<string>();
            for (int i = 0; i < _coreEntries.arraySize - 1; i++)
            {
                existingIds.Add(_coreEntries.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue);
            }
            
            string newId = baseId;
            while (existingIds.Contains(newId))
            {
                newId = $"{baseId}_{counter++}";
            }
            
            newEntry.FindPropertyRelative("id").stringValue = newId;
            newEntry.FindPropertyRelative("category").enumValueIndex = (int)SoundCategory.SFX;
            newEntry.FindPropertyRelative("volume").floatValue = 1f;
            newEntry.FindPropertyRelative("pitch").floatValue = 1f;
            newEntry.FindPropertyRelative("pitchVariation").floatValue = 0f;
            newEntry.FindPropertyRelative("loop").boolValue = false;
            newEntry.FindPropertyRelative("spatial").boolValue = false;
            newEntry.FindPropertyRelative("minDistance").floatValue = 1f;
            newEntry.FindPropertyRelative("maxDistance").floatValue = 50f;
            newEntry.FindPropertyRelative("priority").enumValueIndex = (int)SoundPriority.Normal;
            newEntry.FindPropertyRelative("cooldown").floatValue = 0f;
            
            _expandedEntries.Add(_coreEntries.arraySize - 1);
            SoundIdDrawer.InvalidateCache();
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void ValidateLibrary(SoundLibrary library)
        {
            int errors = 0;
            var ids = new HashSet<string>();
            
            foreach (var entry in library.coreEntries)
            {
                if (string.IsNullOrEmpty(entry.id))
                {
                    Debug.LogWarning("[SoundLibrary] Entry with empty ID found");
                    errors++;
                    continue;
                }
                
                if (!ids.Add(entry.id))
                {
                    Debug.LogWarning($"[SoundLibrary] Duplicate ID: {entry.id}");
                    errors++;
                }
                
                if (entry.clip == null && string.IsNullOrEmpty(entry.fmodEvent))
                {
                    Debug.LogWarning($"[SoundLibrary] No clip or FMOD event for: {entry.id}");
                    errors++;
                }
            }
            
            if (errors == 0)
            {
                Debug.Log($"[SoundLibrary] Validation passed ✓ ({library.coreEntries.Count} sounds)");
                EditorUtility.DisplayDialog("Validation", "All sounds are valid!", "OK");
            }
            else
            {
                Debug.LogWarning($"[SoundLibrary] Validation found {errors} issues");
                EditorUtility.DisplayDialog("Validation", $"Found {errors} issues.\nCheck Console for details.", "OK");
            }
        }
        
        private Color GetCategoryColor(SoundCategory category)
        {
            return category switch
            {
                SoundCategory.Music => new Color(0.6f, 0.4f, 0.8f),
                SoundCategory.SFX => new Color(0.4f, 0.7f, 0.4f),
                SoundCategory.Voice => new Color(0.8f, 0.6f, 0.4f),
                SoundCategory.Ambient => new Color(0.4f, 0.6f, 0.8f),
                SoundCategory.UI => new Color(0.8f, 0.8f, 0.4f),
                _ => Color.gray
            };
        }
        
        private void PlayPreview(AudioClip clip, float volume = 1f, float pitch = 1f, float pitchVariation = 0f)
        {
            StopPreview();

            if (_previewSource == null)
            {
                var go = new GameObject("SoundPreview");
                go.hideFlags = HideFlags.HideAndDontSave;
                _previewSource = go.AddComponent<AudioSource>();
            }

            _previewSource.clip = clip;
            _previewSource.volume = volume;
            _previewSource.pitch = pitchVariation > 0f
                ? pitch + Random.Range(-pitchVariation, pitchVariation)
                : pitch;
            _previewSource.Play();
        }
        
        private void StopPreview()
        {
            if (_previewSource != null)
            {
                _previewSource.Stop();
                DestroyImmediate(_previewSource.gameObject);
                _previewSource = null;
            }
        }
    }
}
