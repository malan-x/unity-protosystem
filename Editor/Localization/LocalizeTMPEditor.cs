// Packages/com.protosystem.core/Editor/Localization/LocalizeTMPEditor.cs
using UnityEngine;
using UnityEditor;
using TMPro;

namespace ProtoSystem.Editor
{
    [CustomEditor(typeof(LocalizeTMP))]
    public class LocalizeTMPEditor : UnityEditor.Editor
    {
        private SerializedProperty _table;
        private SerializedProperty _key;
        private SerializedProperty _fallback;
        private SerializedProperty _toUpperCase;
        
        private void OnEnable()
        {
            _table = serializedObject.FindProperty("table");
            _key = serializedObject.FindProperty("key");
            _fallback = serializedObject.FindProperty("fallback");
            _toUpperCase = serializedObject.FindProperty("toUpperCase");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.PropertyField(_table);
            EditorGUILayout.PropertyField(_key);
            EditorGUILayout.PropertyField(_fallback);
            EditorGUILayout.PropertyField(_toUpperCase);
            
            serializedObject.ApplyModifiedProperties();
            
            // Preview
            EditorGUILayout.Space(5);
            
            var comp = (LocalizeTMP)target;
            var tmp = comp.GetComponent<TMP_Text>();
            
            if (tmp != null && !string.IsNullOrEmpty(_key.stringValue))
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                
                using (new EditorGUI.DisabledGroupScope(true))
                {
                    string currentText = tmp.text;
                    EditorGUILayout.TextField("Current", currentText);
                }
                
                if (Application.isPlaying && Loc.IsReady)
                {
                    EditorGUILayout.HelpBox(
                        $"Language: {Loc.CurrentLanguage}", MessageType.None);
                    
                    if (GUILayout.Button("Refresh"))
                    {
                        comp.UpdateText();
                    }
                }
            }
            else if (string.IsNullOrEmpty(_key.stringValue))
            {
                EditorGUILayout.HelpBox("Key not set", MessageType.Warning);
            }
        }
    }
}
