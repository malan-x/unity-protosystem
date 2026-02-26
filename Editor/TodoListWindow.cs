using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// TODO List window for project task management
    /// </summary>
    public class TodoListWindow : EditorWindow
    {
        private static TodoListData _data;
        private Vector2 _scrollPos;
        private string _newTaskText = "";
        private int _selectedCategory = 0;
        private bool _showCompleted = false;
        private int _filterCategory = -1;
        private int _filterPriority = -1;
        private string _searchText = "";
        private readonly HashSet<TodoTask> _expandedTasks = new HashSet<TodoTask>();

        private static readonly string[] Categories = 
        {
            "🎮 Gameplay",
            "🎨 Art/UI", 
            "🔊 Audio",
            "🌍 Localization",
            "🐛 Bugs",
            "📝 Other"
        };
        
        private static readonly Color[] CategoryColors =
        {
            new Color(0.4f, 0.7f, 0.4f),
            new Color(0.7f, 0.5f, 0.8f),
            new Color(0.5f, 0.7f, 0.9f),
            new Color(0.9f, 0.7f, 0.4f),
            new Color(0.9f, 0.4f, 0.4f),
            new Color(0.6f, 0.6f, 0.6f)
        };
        
        [MenuItem("ProtoSystem/TODO List %#t", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<TodoListWindow>("📋 TODO List");
            window.minSize = new Vector2(400, 300);
        }
        
        private void OnEnable()
        {
            _data = TodoListData.GetOrCreate();
        }
        
        private void OnGUI()
        {
            if (_data == null) _data = TodoListData.GetOrCreate();

            if (_data == null)
            {
                EditorGUILayout.HelpBox(
                    "TodoList asset failed to load. Try deleting Assets/TodoList.asset and reopening the window.",
                    MessageType.Warning);
                return;
            }

            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label($"Tasks: {_data.GetActiveCount()}/{_data.tasks.Count}", GUILayout.Width(80));
                
                GUILayout.FlexibleSpace();
                
                _showCompleted = GUILayout.Toggle(_showCompleted, "Done", EditorStyles.toolbarButton, GUILayout.Width(50));
                
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45)))
                {
                    if (EditorUtility.DisplayDialog("Clear Completed", "Remove all completed tasks?", "Yes", "No"))
                    {
                        _data.tasks.RemoveAll(t => t.done);
                        _data.Save();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Filter bar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                // Search
                GUILayout.Label("🔍", GUILayout.Width(20));
                _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(100));
                
                GUILayout.Space(10);
                
                // Category filter
                var catOptions = new[] { "All" }.Concat(Categories).ToArray();
                _filterCategory = EditorGUILayout.Popup(_filterCategory + 1, catOptions, EditorStyles.toolbarPopup, GUILayout.Width(110)) - 1;
                
                // Priority filter
                var priOptions = new[] { "All Priority", "⬇ Low", "● Normal", "⬆ High" };
                _filterPriority = EditorGUILayout.Popup(_filterPriority + 1, priOptions, EditorStyles.toolbarPopup, GUILayout.Width(85)) - 1;
                
                GUILayout.FlexibleSpace();
                
                // Reset filters
                if (_filterCategory >= 0 || _filterPriority >= 0 || !string.IsNullOrEmpty(_searchText))
                {
                    if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22)))
                    {
                        _filterCategory = -1;
                        _filterPriority = -1;
                        _searchText = "";
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Add new task
            EditorGUILayout.BeginHorizontal();
            {
                _selectedCategory = EditorGUILayout.Popup(_selectedCategory, Categories, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                GUI.enabled = !string.IsNullOrWhiteSpace(_newTaskText);
                if (GUILayout.Button("+", GUILayout.Width(25)))
                {
                    AddTask(_newTaskText, _selectedCategory);
                    _newTaskText = "";
                    GUI.FocusControl(null);
                }
                GUI.enabled = true;
            }
            EditorGUILayout.EndHorizontal();

            var inputStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            _newTaskText = EditorGUILayout.TextArea(_newTaskText, inputStyle, GUILayout.MinHeight(36));
            
            EditorGUILayout.Space(5);
            
            // Task list
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            {
                int visibleCount = 0;
                for (int i = 0; i < _data.tasks.Count; i++)
                {
                    var task = _data.tasks[i];
                    
                    if (!PassesFilter(task)) continue;
                    
                    DrawTask(task, i);
                    visibleCount++;
                }
                
                if (_data.tasks.Count == 0)
                {
                    EditorGUILayout.HelpBox("No tasks yet. Add one above!", MessageType.Info);
                }
                else if (visibleCount == 0)
                {
                    EditorGUILayout.HelpBox("No tasks match current filter.", MessageType.Info);
                }
            }
            EditorGUILayout.EndScrollView();
            
            // Footer
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                // Select asset button
                if (GUILayout.Button("Select Asset", EditorStyles.toolbarButton, GUILayout.Width(75)))
                {
                    Selection.activeObject = _data;
                    EditorGUIUtility.PingObject(_data);
                }
                
                GUILayout.FlexibleSpace();
                GUILayout.Label("ProtoSystem", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawTask(TodoTask task, int index)
        {
            bool isExpanded = _expandedTasks.Contains(task);
            var bgColor = task.done ? new Color(0.3f, 0.3f, 0.3f, 0.3f) : new Color(0.2f, 0.2f, 0.2f, 0.5f);

            EditorGUILayout.BeginVertical(GetBoxStyle(bgColor));
            {
                EditorGUILayout.BeginHorizontal();
                {
                    // Checkbox
                    var newDone = EditorGUILayout.Toggle(task.done, GUILayout.Width(20));
                    if (newDone != task.done)
                    {
                        task.done = newDone;
                        _data.Save();
                    }

                    // Category color dot
                    var catColor = CategoryColors[Mathf.Clamp(task.category, 0, CategoryColors.Length - 1)];
                    GUI.color = catColor;
                    GUILayout.Label("●", GUILayout.Width(15));
                    GUI.color = Color.white;

                    // Text — clipped single line, click to expand
                    var textStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };
                    if (task.done) { textStyle.fontStyle = FontStyle.Italic; GUI.color = Color.gray; }
                    var textRect = GUILayoutUtility.GetRect(GUIContent.none, textStyle,
                        GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    GUI.Label(textRect, task.text, textStyle);
                    GUI.color = Color.white;

                    // Click / double-click on text
                    if (Event.current.type == EventType.MouseDown && textRect.Contains(Event.current.mousePosition))
                    {
                        if (Event.current.clickCount >= 2)
                        {
                            EditorGUIUtility.systemCopyBuffer = task.text;
                            ShowNotification(new GUIContent("Copied!"), 0.5);
                        }
                        else
                        {
                            if (isExpanded) _expandedTasks.Remove(task);
                            else _expandedTasks.Add(task);
                        }
                        Event.current.Use();
                    }

                    // Priority
                    if (!task.done)
                    {
                        var newPriority = EditorGUILayout.IntPopup(task.priority,
                            new[] { "Low", "Normal", "High" },
                            new[] { 0, 1, 2 },
                            GUILayout.Width(60));
                        if (newPriority != task.priority)
                        {
                            task.priority = newPriority;
                            _data.Save();
                        }
                    }

                    // Copy button
                    if (GUILayout.Button("C", EditorStyles.miniButton, GUILayout.Width(20)))
                    {
                        EditorGUIUtility.systemCopyBuffer = task.text;
                        ShowNotification(new GUIContent("Copied!"), 0.5);
                    }

                    // Delete
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        _expandedTasks.Remove(task);
                        _data.tasks.RemoveAt(index);
                        _data.Save();
                        GUIUtility.ExitGUI();
                    }
                    GUI.color = Color.white;
                }
                EditorGUILayout.EndHorizontal();

                // Expanded text below the row
                if (isExpanded)
                {
                    var wrapStyle = GetWordWrapStyle(task.done);
                    if (task.done) GUI.color = Color.gray;
                    EditorGUILayout.LabelField(task.text, wrapStyle);
                    GUI.color = Color.white;
                }
            }
            EditorGUILayout.EndVertical();
        }
        
        private void AddTask(string text, int category)
        {
            _data.tasks.Insert(0, new TodoTask
            {
                text = text,
                category = category,
                priority = 1,
                done = false,
                created = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });
            _data.Save();
        }
        
        private bool PassesFilter(TodoTask task)
        {
            if (!_showCompleted && task.done) return false;
            if (_filterCategory >= 0 && task.category != _filterCategory) return false;
            if (_filterPriority >= 0 && task.priority != _filterPriority) return false;
            if (!string.IsNullOrEmpty(_searchText))
            {
                if (!task.text.ToLower().Contains(_searchText.ToLower())) return false;
            }
            return true;
        }
        
        private static GUIStyle GetBoxStyle(Color color)
        {
            var style = new GUIStyle(EditorStyles.helpBox);
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            style.normal.background = tex;
            style.margin = new RectOffset(2, 2, 1, 1);
            style.padding = new RectOffset(5, 5, 3, 3);
            return style;
        }
        
        private static GUIStyle GetWordWrapStyle(bool done)
        {
            var style = new GUIStyle(EditorStyles.label);
            style.wordWrap = true;
            style.richText = false;
            if (done)
                style.fontStyle = FontStyle.Italic;
            return style;
        }
        
        public static int GetActiveCount()
        {
            if (_data == null) _data = TodoListData.GetOrCreate();
            return _data?.GetActiveCount() ?? 0;
        }
    }
    
    /// <summary>
    /// TODO button for SceneView toolbar
    /// </summary>
    [EditorToolbarElement(id, typeof(SceneView))]
    public class TodoToolbarButton : EditorToolbarButton
    {
        public const string id = "ProtoSystem/TodoButton";
        
        public TodoToolbarButton()
        {
            text = "📋 TODO";
            tooltip = "Open TODO List (Ctrl+Shift+T)";
            clicked += () => TodoListWindow.ShowWindow();
            
            style.backgroundColor = new Color(1f, 0.5f, 0.2f);
            style.color = Color.white;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            style.borderTopLeftRadius = 4;
            style.borderTopRightRadius = 4;
            style.borderBottomLeftRadius = 4;
            style.borderBottomRightRadius = 4;
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.paddingTop = 2;
            style.paddingBottom = 2;
        }
    }
    
    /// <summary>
    /// Toolbar Overlay containing TODO button
    /// </summary>
    [Overlay(typeof(SceneView), "TODO", defaultDisplay = true)]
    public class TodoOverlay : ToolbarOverlay
    {
        TodoOverlay() : base(TodoToolbarButton.id) { }
    }
}
