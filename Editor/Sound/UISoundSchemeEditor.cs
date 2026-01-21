using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Кастомный редактор для UISoundScheme
    /// </summary>
    [CustomEditor(typeof(UISoundScheme))]
    public class UISoundSchemeEditor : UnityEditor.Editor
    {
        private bool _showWindowEvents = false;
        private bool _showButtonEvents = true;
        private bool _showNavigation = true;
        private bool _showFeedback = true;
        private bool _showInputControls = true;
        private bool _showSnapshots = false;
        private bool _showOverrides = false;
        
        // Кэш для валидации
        private HashSet<string> _validSoundIds;
        private SoundLibrary _cachedLibrary;
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var scheme = (UISoundScheme)target;
            
            // ===== HEADER =====
            DrawHeader(scheme);
            
            EditorGUILayout.Space(10);
            
            // Window Events
            _showWindowEvents = EditorGUILayout.Foldout(_showWindowEvents, "🪟 Window Events", true, EditorStyles.foldoutHeader);
            if (_showWindowEvents)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Звуки открытия/закрытия окон. Модальные окна могут иметь отдельные звуки.", MessageType.None);
                DrawValidatedSoundField("windowOpen", "Window Open");
                DrawValidatedSoundField("windowClose", "Window Close");
                DrawValidatedSoundField("modalOpen", "Modal Open");
                DrawValidatedSoundField("modalClose", "Modal Close");
                EditorGUI.indentLevel--;
            }
            
            // Button Events
            _showButtonEvents = EditorGUILayout.Foldout(_showButtonEvents, "🔘 Button Events", true, EditorStyles.foldoutHeader);
            if (_showButtonEvents)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Основные звуки кнопок. Click — самый частый звук в UI.", MessageType.None);
                DrawValidatedSoundField("buttonClick", "Click");
                DrawValidatedSoundField("buttonHover", "Hover");
                DrawValidatedSoundField("buttonDisabled", "Disabled Click");
                EditorGUI.indentLevel--;
            }
            
            // Navigation
            _showNavigation = EditorGUILayout.Foldout(_showNavigation, "🧭 Navigation", true, EditorStyles.foldoutHeader);
            if (_showNavigation)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Звуки перемещения по меню (геймпад/клавиатура).", MessageType.None);
                DrawValidatedSoundField("navigate", "Navigate");
                DrawValidatedSoundField("back", "Back");
                DrawValidatedSoundField("tabSwitch", "Tab Switch");
                EditorGUI.indentLevel--;
            }
            
            // Feedback
            _showFeedback = EditorGUILayout.Foldout(_showFeedback, "💬 Feedback", true, EditorStyles.foldoutHeader);
            if (_showFeedback)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Звуки обратной связи для действий пользователя.", MessageType.None);
                DrawValidatedSoundField("success", "Success");
                DrawValidatedSoundField("error", "Error");
                DrawValidatedSoundField("warning", "Warning");
                DrawValidatedSoundField("notification", "Notification");
                EditorGUI.indentLevel--;
            }
            
            // Input Controls
            _showInputControls = EditorGUILayout.Foldout(_showInputControls, "🎛 Input Controls", true, EditorStyles.foldoutHeader);
            if (_showInputControls)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Звуки для слайдеров, чекбоксов, выпадающих списков.", MessageType.None);
                DrawValidatedSoundField("sliderChange", "Slider Change");
                DrawValidatedSoundField("toggleOn", "Toggle On");
                DrawValidatedSoundField("toggleOff", "Toggle Off");
                DrawValidatedSoundField("dropdownOpen", "Dropdown Open");
                DrawValidatedSoundField("dropdownSelect", "Dropdown Select");
                EditorGUI.indentLevel--;
            }
            
            // Snapshots
            _showSnapshots = EditorGUILayout.Foldout(_showSnapshots, "📸 Snapshots", true, EditorStyles.foldoutHeader);
            if (_showSnapshots)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Audio Mixer Snapshots применяются при открытии модальных окон и паузы.", MessageType.None);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("modalSnapshot"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("pauseSnapshot"));
                EditorGUI.indentLevel--;
            }
            
            // Window Overrides
            _showOverrides = EditorGUILayout.Foldout(_showOverrides, "⚙ Per-Window Overrides", true, EditorStyles.foldoutHeader);
            if (_showOverrides)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Переопределения для конкретных окон по их ID.", MessageType.None);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("windowOverrides"), true);
                EditorGUI.indentLevel--;
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawHeader(UISoundScheme scheme)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("🎨 UI Sound Scheme", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Связывает UI события с звуками из Sound Library. UISystem автоматически воспроизводит эти звуки.", EditorStyles.wordWrappedMiniLabel);
            
            EditorGUILayout.Space(5);
            
            // Validation status
            RefreshValidSoundIds();
            var validation = ValidateScheme(scheme);
            
            if (validation.missingIds.Count == 0)
            {
                if (validation.emptyFields > 0)
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    EditorGUILayout.LabelField($"○ {validation.emptyFields} полей не заполнено (будут без звука)", EditorStyles.miniLabel);
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = new Color(0.5f, 0.9f, 0.5f);
                    EditorGUILayout.LabelField("✓ Все ID найдены в Sound Library", EditorStyles.boldLabel);
                    GUI.color = Color.white;
                }
            }
            else
            {
                GUI.color = new Color(1f, 0.6f, 0.4f);
                EditorGUILayout.LabelField($"✗ {validation.missingIds.Count} ID не найдено в Sound Library:", EditorStyles.boldLabel);
                GUI.color = Color.white;
                
                EditorGUI.indentLevel++;
                foreach (var id in validation.missingIds.Take(5))
                {
                    EditorGUILayout.LabelField($"• {id}", EditorStyles.miniLabel);
                }
                if (validation.missingIds.Count > 5)
                {
                    EditorGUILayout.LabelField($"  ... и ещё {validation.missingIds.Count - 5}", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(5);
            
            // Buttons
            EditorGUILayout.BeginHorizontal();
            
            if (_cachedLibrary != null)
            {
                if (GUILayout.Button("📚 Open Library", GUILayout.Height(22)))
                {
                    Selection.activeObject = _cachedLibrary;
                }
            }
            
            if (validation.missingIds.Count > 0 && _cachedLibrary != null)
            {
                if (GUILayout.Button($"➕ Create {validation.missingIds.Count} Missing", GUILayout.Height(22)))
                {
                    CreateMissingSounds(validation.missingIds);
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawValidatedSoundField(string propertyName, string label)
        {
            var prop = serializedObject.FindProperty(propertyName);
            string soundId = prop.stringValue;
            
            EditorGUILayout.BeginHorizontal();
            
            // Show warning icon if ID is set but not found
            bool isEmpty = string.IsNullOrEmpty(soundId);
            bool isValid = isEmpty || (_validSoundIds != null && _validSoundIds.Contains(soundId));
            
            if (!isValid)
            {
                GUI.color = new Color(1f, 0.6f, 0.4f);
            }
            
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            
            if (!isValid)
            {
                GUI.color = Color.white;
                EditorGUILayout.LabelField("⚠", GUILayout.Width(20));
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void RefreshValidSoundIds()
        {
            // Find SoundLibrary in project
            var guids = AssetDatabase.FindAssets("t:SoundLibrary");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(path);
                
                if (library != _cachedLibrary)
                {
                    _cachedLibrary = library;
                    _validSoundIds = new HashSet<string>();
                    
                    if (library != null)
                    {
                        foreach (var entry in library.coreEntries)
                        {
                            if (!string.IsNullOrEmpty(entry.id))
                                _validSoundIds.Add(entry.id);
                        }
                    }
                }
            }
            else
            {
                _cachedLibrary = null;
                _validSoundIds = null;
            }
        }
        
        private (List<string> missingIds, int emptyFields) ValidateScheme(UISoundScheme scheme)
        {
            var missingIds = new List<string>();
            int emptyFields = 0;
            
            string[] fields = {
                scheme.windowOpen, scheme.windowClose, scheme.modalOpen, scheme.modalClose,
                scheme.buttonClick, scheme.buttonHover, scheme.buttonDisabled,
                scheme.navigate, scheme.back, scheme.tabSwitch,
                scheme.success, scheme.error, scheme.warning, scheme.notification,
                scheme.sliderChange, scheme.toggleOn, scheme.toggleOff, scheme.dropdownOpen, scheme.dropdownSelect
            };
            
            foreach (var id in fields)
            {
                if (string.IsNullOrEmpty(id))
                {
                    emptyFields++;
                }
                else if (_validSoundIds != null && !_validSoundIds.Contains(id))
                {
                    missingIds.Add(id);
                }
            }
            
            return (missingIds.Distinct().ToList(), emptyFields);
        }
        
        private void CreateMissingSounds(List<string> missingIds)
        {
            if (_cachedLibrary == null) return;
            
            Undo.RecordObject(_cachedLibrary, "Add missing sounds");
            
            foreach (var id in missingIds)
            {
                // Check if already exists
                if (_cachedLibrary.coreEntries.Any(e => e.id == id))
                    continue;
                
                var entry = new SoundEntry
                {
                    id = id,
                    category = SoundCategory.UI,
                    volume = 0.5f,
                    pitch = 1f
                };
                
                _cachedLibrary.coreEntries.Add(entry);
            }
            
            EditorUtility.SetDirty(_cachedLibrary);
            AssetDatabase.SaveAssets();
            
            // Refresh cache
            _cachedLibrary = null;
            RefreshValidSoundIds();
            
            Debug.Log($"✅ Added {missingIds.Count} sound entries to SoundLibrary. Don't forget to assign AudioClips!");
        }
    }
}
