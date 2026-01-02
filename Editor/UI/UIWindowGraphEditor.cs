// Packages/com.protosystem.core/Editor/UI/UIWindowGraphEditor.cs
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Кастомный инспектор для UIWindowGraph
    /// </summary>
    [CustomEditor(typeof(UIWindowGraph))]
    public class UIWindowGraphEditor : UnityEditor.Editor
    {
        private bool _showWindows = true;
        private bool _showTransitions = true;
        private bool _showGlobalTransitions = true;

        public override void OnInspectorGUI()
        {
            var graph = (UIWindowGraph)target;

            // Build Info
            EditorGUILayout.LabelField("Build Info", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Last Build", graph.lastBuildTime ?? "Never");
                EditorGUILayout.IntField("Windows", graph.windowCount);
                EditorGUILayout.IntField("Transitions", graph.transitionCount);
            }

            EditorGUILayout.Space(10);

            // Settings
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            
            var startWindowProp = serializedObject.FindProperty("startWindowId");
            
            // Dropdown для выбора стартового окна
            var windowIds = new string[graph.windows.Count + 1];
            windowIds[0] = "(None)";
            int currentIndex = 0;
            
            for (int i = 0; i < graph.windows.Count; i++)
            {
                windowIds[i + 1] = graph.windows[i].id;
                if (graph.windows[i].id == startWindowProp.stringValue)
                    currentIndex = i + 1;
            }

            int newIndex = EditorGUILayout.Popup("Start Window", currentIndex, windowIds);
            if (newIndex != currentIndex)
            {
                startWindowProp.stringValue = newIndex == 0 ? "" : windowIds[newIndex];
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(10);

            // Buttons
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("🔄 Rebuild", GUILayout.Height(30)))
            {
                UIWindowGraphBuilder.RebuildGraph();
            }
            
            if (GUILayout.Button("✓ Validate", GUILayout.Height(30)))
            {
                UIWindowGraphBuilder.ValidateGraph();
            }
            
            if (GUILayout.Button("🗺️ Open Viewer", GUILayout.Height(30)))
            {
                UIWindowGraphViewer.ShowWindow();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Windows List
            _showWindows = EditorGUILayout.Foldout(_showWindows, $"Windows ({graph.windows.Count})", true);
            if (_showWindows)
            {
                EditorGUI.indentLevel++;
                foreach (var window in graph.windows)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    // Иконка статуса prefab
                    var icon = window.prefab != null ? "✓" : "⚠";
                    var color = window.prefab != null ? Color.green : Color.yellow;
                    
                    var style = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
                    EditorGUILayout.LabelField(icon, style, GUILayout.Width(20));
                    
                    EditorGUILayout.LabelField(window.id, GUILayout.Width(150));
                    EditorGUILayout.LabelField(window.type.ToString(), GUILayout.Width(80));
                    
                    // Кнопка для выбора prefab
                    var newPrefab = (GameObject)EditorGUILayout.ObjectField(window.prefab, typeof(GameObject), false);
                    if (newPrefab != window.prefab)
                    {
                        window.prefab = newPrefab;
                        EditorUtility.SetDirty(graph);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Transitions List
            _showTransitions = EditorGUILayout.Foldout(_showTransitions, $"Transitions ({graph.transitions.Count})", true);
            if (_showTransitions)
            {
                EditorGUI.indentLevel++;
                foreach (var t in graph.transitions)
                {
                    EditorGUILayout.LabelField($"{t.fromWindowId} --[{t.trigger}]--> {t.toWindowId}");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Global Transitions
            _showGlobalTransitions = EditorGUILayout.Foldout(_showGlobalTransitions, $"Global Transitions ({graph.globalTransitions.Count})", true);
            if (_showGlobalTransitions)
            {
                EditorGUI.indentLevel++;
                foreach (var t in graph.globalTransitions)
                {
                    EditorGUILayout.LabelField($"* --[{t.trigger}]--> {t.toWindowId}");
                }
                EditorGUI.indentLevel--;
            }
        }
    }
}
