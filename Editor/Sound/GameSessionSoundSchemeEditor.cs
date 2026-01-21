using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Кастомный редактор для GameSessionSoundScheme
    /// </summary>
    [CustomEditor(typeof(GameSessionSoundScheme))]
    public class GameSessionSoundSchemeEditor : UnityEditor.Editor
    {
        private bool _showMusic = true;
        private bool _showStingers = true;
        private bool _showTransitions = false;
        private bool _showSnapshots = false;
        private bool _showOverrides = false;
        
        // Кэш для валидации
        private HashSet<string> _validSoundIds;
        private SoundLibrary _cachedLibrary;
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var scheme = (GameSessionSoundScheme)target;
            
            // ===== HEADER =====
            DrawHeader(scheme);
            
            EditorGUILayout.Space(10);
            
            // Music
            _showMusic = EditorGUILayout.Foldout(_showMusic, "🎵 Music", true, EditorStyles.foldoutHeader);
            if (_showMusic)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Музыкальные треки для разных состояний игры. Автоматически переключаются через события GameSession.", MessageType.None);
                DrawValidatedSoundField("menuMusic", "Menu");
                DrawValidatedSoundField("gameplayMusic", "Gameplay");
                DrawValidatedSoundField("pauseMusic", "Pause (optional)");
                DrawValidatedSoundField("victoryMusic", "Victory");
                DrawValidatedSoundField("defeatMusic", "Defeat");
                EditorGUI.indentLevel--;
            }
            
            // Stingers
            _showStingers = EditorGUILayout.Foldout(_showStingers, "⚡ Stingers", true, EditorStyles.foldoutHeader);
            if (_showStingers)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Короткие акцентные звуки для событий. Играют поверх музыки с автоматическим ducking.", MessageType.None);
                DrawValidatedSoundField("sessionStartStinger", "Session Start");
                DrawValidatedSoundField("victoryStinger", "Victory");
                DrawValidatedSoundField("defeatStinger", "Defeat");
                DrawValidatedSoundField("checkpointStinger", "Checkpoint");
                EditorGUI.indentLevel--;
            }
            
            // Transitions
            _showTransitions = EditorGUILayout.Foldout(_showTransitions, "⏱ Transitions", true, EditorStyles.foldoutHeader);
            if (_showTransitions)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Настройки плавности переходов между треками и ducking для stinger'ов.", MessageType.None);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("musicFadeTime"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("stingerDuckAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("stingerDuckDuration"));
                EditorGUI.indentLevel--;
            }
            
            // Snapshots
            _showSnapshots = EditorGUILayout.Foldout(_showSnapshots, "📸 Snapshots", true, EditorStyles.foldoutHeader);
            if (_showSnapshots)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Audio Mixer Snapshots для паузы и конца игры.", MessageType.None);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("pauseSnapshot"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gameOverSnapshot"));
                EditorGUI.indentLevel--;
            }
            
            // State Overrides
            _showOverrides = EditorGUILayout.Foldout(_showOverrides, "⚙ State Overrides", true, EditorStyles.foldoutHeader);
            if (_showOverrides)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Переопределения музыки для конкретных состояний GameSession.", MessageType.None);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("stateOverrides"), true);
                EditorGUI.indentLevel--;
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawHeader(GameSessionSoundScheme scheme)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("🎮 Game Session Sound Scheme", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Связывает состояния игровой сессии с музыкой и звуками. GameSessionSystem автоматически переключает треки.", EditorStyles.wordWrappedMiniLabel);
            
            EditorGUILayout.Space(5);
            
            // Validation status
            RefreshValidSoundIds();
            var validation = ValidateScheme(scheme);
            
            if (validation.missingIds.Count == 0)
            {
                if (validation.emptyFields > 0)
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    EditorGUILayout.LabelField($"○ {validation.emptyFields} полей не заполнено (опционально)", EditorStyles.miniLabel);
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
        
        private (List<string> missingIds, int emptyFields) ValidateScheme(GameSessionSoundScheme scheme)
        {
            var missingIds = new List<string>();
            int emptyFields = 0;
            
            string[] fields = {
                scheme.menuMusic, scheme.gameplayMusic, scheme.pauseMusic, 
                scheme.victoryMusic, scheme.defeatMusic,
                scheme.sessionStartStinger, scheme.victoryStinger, 
                scheme.defeatStinger, scheme.checkpointStinger
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
                if (_cachedLibrary.coreEntries.Any(e => e.id == id))
                    continue;
                
                // Determine category by ID prefix
                SoundCategory category = SoundCategory.Music;
                if (id.Contains("stinger"))
                    category = SoundCategory.SFX;
                
                var entry = new SoundEntry
                {
                    id = id,
                    category = category,
                    volume = 0.8f,
                    pitch = 1f,
                    loop = category == SoundCategory.Music
                };
                
                _cachedLibrary.coreEntries.Add(entry);
            }
            
            EditorUtility.SetDirty(_cachedLibrary);
            AssetDatabase.SaveAssets();
            
            _cachedLibrary = null;
            RefreshValidSoundIds();
            
            Debug.Log($"✅ Added {missingIds.Count} sound entries to SoundLibrary. Don't forget to assign AudioClips!");
        }
    }
}
