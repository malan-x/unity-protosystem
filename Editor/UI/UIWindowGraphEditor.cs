// Packages/com.protosystem.core/Editor/UI/UIWindowGraphEditor.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProtoSystem.UI;

namespace ProtoSystem.Editor.UI
{
    /// <summary>
    /// Визуальный редактор графа UI окон
    /// </summary>
    public class UIWindowGraphEditor : UnityEditor.EditorWindow
    {
        private UIWindowGraph _graph;
        private Vector2 _scrollPosition;
        private Vector2 _canvasOffset;
        private float _zoom = 1f;
        private bool _isDragging;
        private Vector2 _dragStart;
        
        // Выбранные элементы
        private WindowDefinition _selectedWindow;
        private TransitionDefinition _selectedTransition;
        
        // Режим создания связи
        private bool _isCreatingTransition;
        private string _transitionFromWindowId;
        
        // Стили
        private GUIStyle _windowStyle;
        private GUIStyle _selectedWindowStyle;
        private GUIStyle _modalWindowStyle;
        private GUIStyle _codeWindowStyle;
        
        // Цвета
        private readonly Color _normalColor = new Color(0.2f, 0.6f, 0.9f);
        private readonly Color _modalColor = new Color(0.9f, 0.5f, 0.2f);
        private readonly Color _overlayColor = new Color(0.5f, 0.9f, 0.5f);
        private readonly Color _codeColor = new Color(0.7f, 0.7f, 0.7f);
        private readonly Color _selectedColor = new Color(1f, 0.9f, 0.2f);
        private readonly Color _connectionColor = new Color(0.8f, 0.8f, 0.8f);
        private readonly Color _globalConnectionColor = new Color(0.5f, 0.9f, 0.5f);

        // Размеры
        private const float NodeWidth = 150f;
        private const float NodeHeight = 80f;
        private const float GridSize = 20f;

        [MenuItem("ProtoSystem/UI Window Graph Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<UIWindowGraphEditor>("UI Window Graph");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            InitStyles();
            RefreshFromCode();
        }

        private void InitStyles()
        {
            _windowStyle = new GUIStyle();
            _windowStyle.normal.background = MakeTexture(2, 2, new Color(0.25f, 0.25f, 0.25f));
            _windowStyle.border = new RectOffset(4, 4, 4, 4);
            _windowStyle.padding = new RectOffset(8, 8, 8, 8);
            
            _selectedWindowStyle = new GUIStyle(_windowStyle);
            _selectedWindowStyle.normal.background = MakeTexture(2, 2, new Color(0.4f, 0.4f, 0.2f));
            
            _modalWindowStyle = new GUIStyle(_windowStyle);
            _modalWindowStyle.normal.background = MakeTexture(2, 2, new Color(0.35f, 0.25f, 0.2f));
            
            _codeWindowStyle = new GUIStyle(_windowStyle);
            _codeWindowStyle.normal.background = MakeTexture(2, 2, new Color(0.2f, 0.2f, 0.2f));
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            
            var texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawCanvas();
            DrawSidebar();
            
            ProcessEvents();
        }

        #region Toolbar

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Выбор графа
            var newGraph = (UIWindowGraph)EditorGUILayout.ObjectField(_graph, typeof(UIWindowGraph), false, GUILayout.Width(200));
            if (newGraph != _graph)
            {
                _graph = newGraph;
                RefreshFromCode();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshFromCode();
            }

            if (_graph != null && GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                var result = _graph.Validate();
                if (result.isValid)
                    EditorUtility.DisplayDialog("Validation", "Graph is valid!", "OK");
                else
                    EditorUtility.DisplayDialog("Validation Failed", result.ToString(), "OK");
            }

            if (_graph != null && GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                AutoLayoutNodes();
            }

            GUILayout.FlexibleSpace();

            // Zoom
            GUILayout.Label("Zoom:", EditorStyles.miniLabel);
            _zoom = EditorGUILayout.Slider(_zoom, 0.5f, 2f, GUILayout.Width(100));

            if (GUILayout.Button("Reset View", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                _canvasOffset = Vector2.zero;
                _zoom = 1f;
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Canvas

        private void DrawCanvas()
        {
            if (_graph == null)
            {
                EditorGUILayout.HelpBox("Select or create a UIWindowGraph asset", MessageType.Info);
                return;
            }

            var canvasRect = new Rect(0, EditorStyles.toolbar.fixedHeight, 
                position.width - 250, position.height - EditorStyles.toolbar.fixedHeight);

            GUI.Box(canvasRect, "", EditorStyles.helpBox);

            // Сетка
            DrawGrid(canvasRect, GridSize * _zoom, 0.2f, Color.gray);
            DrawGrid(canvasRect, GridSize * _zoom * 5, 0.4f, Color.gray);

            // Начинаем область для масштабирования
            GUI.BeginClip(canvasRect);
            
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(_canvasOffset.x, _canvasOffset.y, 0),
                Quaternion.identity,
                new Vector3(_zoom, _zoom, 1f)
            );

            // Рисуем связи
            DrawTransitions();

            // Рисуем узлы
            DrawNodes();

            // Рисуем создаваемую связь
            if (_isCreatingTransition)
            {
                var fromWindow = _graph.GetWindow(_transitionFromWindowId);
                if (fromWindow != null)
                {
                    var startPos = GetNodeCenter(fromWindow);
                    var endPos = Event.current.mousePosition / _zoom - _canvasOffset / _zoom;
                    DrawConnection(startPos, endPos, Color.yellow, true);
                }
            }

            GUI.matrix = matrix;
            GUI.EndClip();
        }

        private void DrawGrid(Rect rect, float spacing, float opacity, Color color)
        {
            int widthDivs = Mathf.CeilToInt(rect.width / spacing);
            int heightDivs = Mathf.CeilToInt(rect.height / spacing);

            Handles.BeginGUI();
            Handles.color = new Color(color.r, color.g, color.b, opacity);

            var newOffset = new Vector3(_canvasOffset.x % spacing, _canvasOffset.y % spacing, 0);

            for (int i = 0; i <= widthDivs; i++)
            {
                Handles.DrawLine(
                    new Vector3(spacing * i + newOffset.x + rect.x, rect.y, 0),
                    new Vector3(spacing * i + newOffset.x + rect.x, rect.y + rect.height, 0)
                );
            }

            for (int j = 0; j <= heightDivs; j++)
            {
                Handles.DrawLine(
                    new Vector3(rect.x, spacing * j + newOffset.y + rect.y, 0),
                    new Vector3(rect.x + rect.width, spacing * j + newOffset.y + rect.y, 0)
                );
            }

            Handles.color = Color.white;
            Handles.EndGUI();
        }

        private void DrawNodes()
        {
            foreach (var window in _graph.windows)
            {
                DrawNode(window);
            }
        }

        private void DrawNode(WindowDefinition window)
        {
            var rect = GetNodeRect(window);
            
            // Выбор стиля
            var style = window == _selectedWindow ? _selectedWindowStyle :
                       window.fromCode ? _codeWindowStyle :
                       window.type == WindowType.Modal ? _modalWindowStyle :
                       _windowStyle;

            // Цвет рамки
            var borderColor = window == _selectedWindow ? _selectedColor :
                             window.fromCode ? _codeColor :
                             window.type == WindowType.Modal ? _modalColor :
                             window.type == WindowType.Overlay ? _overlayColor :
                             _normalColor;

            // Рамка
            EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), borderColor);

            // Фон
            GUI.Box(rect, "", style);

            // Контент
            GUILayout.BeginArea(rect);
            GUILayout.Space(5);

            // ID
            GUILayout.Label(window.id, EditorStyles.boldLabel);

            // Тип
            string typeLabel = window.type.ToString();
            if (window.fromCode) typeLabel += " [Code]";
            GUILayout.Label(typeLabel, EditorStyles.miniLabel);

            // Иконки
            GUILayout.BeginHorizontal();
            if (window.pauseGame) GUILayout.Label("⏸", GUILayout.Width(20));
            if (!window.allowBack) GUILayout.Label("🚫", GUILayout.Width(20));
            if (window.prefab != null) GUILayout.Label("📦", GUILayout.Width(20));
            GUILayout.EndHorizontal();

            // Start marker
            if (window.id == _graph.startWindowId)
            {
                GUILayout.Label("► START", EditorStyles.centeredGreyMiniLabel);
            }

            GUILayout.EndArea();
        }

        private void DrawTransitions()
        {
            // Обычные переходы
            foreach (var transition in _graph.transitions)
            {
                var fromWindow = _graph.GetWindow(transition.fromWindowId);
                var toWindow = _graph.GetWindow(transition.toWindowId);
                
                if (fromWindow != null && toWindow != null)
                {
                    var color = transition.fromCode ? _codeColor : _connectionColor;
                    DrawConnection(GetNodeCenter(fromWindow), GetNodeCenter(toWindow), color, false);
                    
                    // Подпись триггера
                    var midPoint = (GetNodeCenter(fromWindow) + GetNodeCenter(toWindow)) / 2;
                    var labelRect = new Rect(midPoint.x - 40, midPoint.y - 10, 80, 20);
                    GUI.Label(labelRect, transition.trigger, EditorStyles.miniLabel);
                }
            }

            // Глобальные переходы
            foreach (var transition in _graph.globalTransitions)
            {
                var toWindow = _graph.GetWindow(transition.toWindowId);
                if (toWindow != null)
                {
                    // Рисуем стрелку сверху
                    var toPos = GetNodeCenter(toWindow);
                    var fromPos = new Vector2(toPos.x, toPos.y - 100);
                    DrawConnection(fromPos, toPos, _globalConnectionColor, false);
                    
                    var labelRect = new Rect(fromPos.x - 40, fromPos.y - 15, 80, 20);
                    GUI.Label(labelRect, $"[G] {transition.trigger}", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawConnection(Vector2 start, Vector2 end, Color color, bool dashed)
        {
            Handles.BeginGUI();
            Handles.color = color;

            var dir = (end - start).normalized;
            var tangent = Vector2.Perpendicular(dir) * 50f;

            // Bezier curve
            Handles.DrawBezier(
                new Vector3(start.x, start.y, 0),
                new Vector3(end.x, end.y, 0),
                new Vector3(start.x + tangent.x, start.y + tangent.y, 0),
                new Vector3(end.x - tangent.x, end.y - tangent.y, 0),
                color,
                null,
                2f
            );

            // Arrow
            var arrowSize = 10f;
            var arrowPos = end - dir * 15f;
            var arrowLeft = arrowPos + Vector2.Perpendicular(dir) * arrowSize - dir * arrowSize;
            var arrowRight = arrowPos - Vector2.Perpendicular(dir) * arrowSize - dir * arrowSize;

            Handles.DrawLine(new Vector3(end.x, end.y, 0), new Vector3(arrowLeft.x, arrowLeft.y, 0));
            Handles.DrawLine(new Vector3(end.x, end.y, 0), new Vector3(arrowRight.x, arrowRight.y, 0));

            Handles.color = Color.white;
            Handles.EndGUI();
        }

        #endregion

        #region Sidebar

        private void DrawSidebar()
        {
            var sidebarRect = new Rect(position.width - 250, EditorStyles.toolbar.fixedHeight, 
                250, position.height - EditorStyles.toolbar.fixedHeight);

            GUILayout.BeginArea(sidebarRect);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_graph != null)
            {
                EditorGUILayout.LabelField("Graph Settings", EditorStyles.boldLabel);
                
                EditorGUI.BeginChangeCheck();
                _graph.startWindowId = EditorGUILayout.TextField("Start Window", _graph.startWindowId);
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(_graph);

                EditorGUILayout.Space(10);

                if (_selectedWindow != null)
                {
                    DrawWindowProperties();
                }
                else if (_selectedTransition != null)
                {
                    DrawTransitionProperties();
                }
                else
                {
                    DrawGraphOverview();
                }
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawWindowProperties()
        {
            EditorGUILayout.LabelField("Window Properties", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(_selectedWindow.fromCode);
            
            EditorGUI.BeginChangeCheck();
            
            _selectedWindow.id = EditorGUILayout.TextField("ID", _selectedWindow.id);
            _selectedWindow.prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", _selectedWindow.prefab, typeof(GameObject), false);
            _selectedWindow.type = (WindowType)EditorGUILayout.EnumPopup("Type", _selectedWindow.type);
            _selectedWindow.layer = (WindowLayer)EditorGUILayout.EnumPopup("Layer", _selectedWindow.layer);
            _selectedWindow.pauseGame = EditorGUILayout.Toggle("Pause Game", _selectedWindow.pauseGame);
            _selectedWindow.hideBelow = EditorGUILayout.Toggle("Hide Below", _selectedWindow.hideBelow);
            _selectedWindow.allowBack = EditorGUILayout.Toggle("Allow Back", _selectedWindow.allowBack);

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_graph);

            EditorGUI.EndDisabledGroup();

            if (_selectedWindow.fromCode)
            {
                EditorGUILayout.HelpBox("This window is defined in code via [UIWindow] attribute", MessageType.Info);
            }

            EditorGUILayout.Space(10);

            // Переходы из этого окна
            EditorGUILayout.LabelField("Transitions From", EditorStyles.boldLabel);
            var transitions = _graph.transitions.Where(t => t.fromWindowId == _selectedWindow.id).ToList();
            foreach (var t in transitions)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"→ {t.toWindowId}", GUILayout.Width(120));
                EditorGUILayout.LabelField($"({t.trigger})", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawTransitionProperties()
        {
            EditorGUILayout.LabelField("Transition Properties", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            
            _selectedTransition.trigger = EditorGUILayout.TextField("Trigger", _selectedTransition.trigger);
            _selectedTransition.fromWindowId = EditorGUILayout.TextField("From", _selectedTransition.fromWindowId);
            _selectedTransition.toWindowId = EditorGUILayout.TextField("To", _selectedTransition.toWindowId);
            _selectedTransition.animation = (TransitionAnimation)EditorGUILayout.EnumPopup("Animation", _selectedTransition.animation);

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_graph);
        }

        private void DrawGraphOverview()
        {
            EditorGUILayout.LabelField("Overview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Windows: {_graph.windows.Count}");
            EditorGUILayout.LabelField($"Transitions: {_graph.transitions.Count}");
            EditorGUILayout.LabelField($"Global Transitions: {_graph.globalTransitions.Count}");

            var fromCode = _graph.windows.Count(w => w.fromCode);
            if (fromCode > 0)
            {
                EditorGUILayout.LabelField($"From Code: {fromCode}");
            }
        }

        #endregion

        #region Events

        private void ProcessEvents()
        {
            var e = Event.current;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        var clickedWindow = GetWindowAtPosition(e.mousePosition);
                        if (clickedWindow != null)
                        {
                            _selectedWindow = clickedWindow;
                            _selectedTransition = null;
                            
                            if (e.clickCount == 2 && clickedWindow.prefab != null)
                            {
                                Selection.activeObject = clickedWindow.prefab;
                            }
                        }
                        else
                        {
                            _selectedWindow = null;
                        }
                        Repaint();
                    }
                    else if (e.button == 2) // Middle mouse
                    {
                        _isDragging = true;
                        _dragStart = e.mousePosition;
                    }
                    break;

                case EventType.MouseUp:
                    _isDragging = false;
                    _isCreatingTransition = false;
                    break;

                case EventType.MouseDrag:
                    if (_isDragging)
                    {
                        _canvasOffset += e.mousePosition - _dragStart;
                        _dragStart = e.mousePosition;
                        Repaint();
                    }
                    else if (_selectedWindow != null && !_selectedWindow.fromCode && e.button == 0)
                    {
                        _selectedWindow.editorPosition += e.delta / _zoom;
                        EditorUtility.SetDirty(_graph);
                        Repaint();
                    }
                    break;

                case EventType.ScrollWheel:
                    _zoom -= e.delta.y * 0.05f;
                    _zoom = Mathf.Clamp(_zoom, 0.5f, 2f);
                    Repaint();
                    break;

                case EventType.ContextClick:
                    ShowContextMenu(e.mousePosition);
                    break;
            }
        }

        private void ShowContextMenu(Vector2 position)
        {
            var menu = new GenericMenu();
            
            var clickedWindow = GetWindowAtPosition(position);
            
            if (clickedWindow != null)
            {
                menu.AddItem(new GUIContent("Create Transition From..."), false, () =>
                {
                    _isCreatingTransition = true;
                    _transitionFromWindowId = clickedWindow.id;
                });

                if (!clickedWindow.fromCode)
                {
                    menu.AddItem(new GUIContent("Delete Window"), false, () =>
                    {
                        _graph.windows.Remove(clickedWindow);
                        EditorUtility.SetDirty(_graph);
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Delete Window (defined in code)"));
                }
            }
            else
            {
                menu.AddItem(new GUIContent("Add Window"), false, () =>
                {
                    var canvasPos = (position - _canvasOffset) / _zoom;
                    _graph.windows.Add(new WindowDefinition
                    {
                        id = "NewWindow",
                        editorPosition = canvasPos
                    });
                    EditorUtility.SetDirty(_graph);
                });
            }

            menu.ShowAsContext();
        }

        #endregion

        #region Helpers

        private Rect GetNodeRect(WindowDefinition window)
        {
            return new Rect(
                window.editorPosition.x,
                window.editorPosition.y,
                NodeWidth,
                NodeHeight
            );
        }

        private Vector2 GetNodeCenter(WindowDefinition window)
        {
            return new Vector2(
                window.editorPosition.x + NodeWidth / 2,
                window.editorPosition.y + NodeHeight / 2
            );
        }

        private WindowDefinition GetWindowAtPosition(Vector2 screenPos)
        {
            // Преобразуем из экранных координат в координаты канваса
            var canvasPos = (screenPos - _canvasOffset - new Vector2(0, EditorStyles.toolbar.fixedHeight)) / _zoom;

            foreach (var window in _graph.windows)
            {
                var rect = GetNodeRect(window);
                if (rect.Contains(canvasPos))
                    return window;
            }
            return null;
        }

        private void RefreshFromCode()
        {
            if (_graph == null) return;

            // Собираем атрибуты из кода
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.StartsWith("System") || 
                    assembly.FullName.StartsWith("Unity") ||
                    assembly.FullName.StartsWith("mscorlib"))
                    continue;

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!typeof(UIWindowBase).IsAssignableFrom(type) || type.IsAbstract)
                            continue;

                        var windowAttr = (UIWindowAttribute)Attribute.GetCustomAttribute(type, typeof(UIWindowAttribute));
                        if (windowAttr == null) continue;

                        var existing = _graph.windows.FirstOrDefault(w => w.id == windowAttr.WindowId);
                        if (existing == null)
                        {
                            _graph.windows.Add(new WindowDefinition
                            {
                                id = windowAttr.WindowId,
                                type = windowAttr.Type,
                                layer = windowAttr.Layer,
                                pauseGame = windowAttr.PauseGame,
                                hideBelow = windowAttr.HideBelow,
                                allowBack = windowAttr.AllowBack,
                                fromCode = true
                            });
                        }

                        // Переходы
                        var transitionAttrs = (UITransitionAttribute[])Attribute.GetCustomAttributes(type, typeof(UITransitionAttribute));
                        foreach (var transAttr in transitionAttrs)
                        {
                            var existingTrans = _graph.transitions.FirstOrDefault(
                                t => t.fromWindowId == windowAttr.WindowId && t.trigger == transAttr.Trigger);
                            
                            if (existingTrans == null)
                            {
                                _graph.transitions.Add(new TransitionDefinition
                                {
                                    fromWindowId = windowAttr.WindowId,
                                    toWindowId = transAttr.ToWindowId,
                                    trigger = transAttr.Trigger,
                                    animation = transAttr.Animation,
                                    fromCode = true
                                });
                            }
                        }
                    }
                }
                catch { }
            }

            EditorUtility.SetDirty(_graph);
            Repaint();
        }

        private void AutoLayoutNodes()
        {
            // Простой алгоритм: располагаем узлы по уровням от стартового
            var visited = new HashSet<string>();
            var levels = new Dictionary<string, int>();
            var levelCounts = new Dictionary<int, int>();

            void SetLevel(string windowId, int level)
            {
                if (string.IsNullOrEmpty(windowId) || visited.Contains(windowId)) return;
                visited.Add(windowId);
                levels[windowId] = level;

                if (!levelCounts.ContainsKey(level))
                    levelCounts[level] = 0;
                levelCounts[level]++;

                foreach (var trans in _graph.GetTransitionsFrom(windowId))
                {
                    SetLevel(trans.toWindowId, level + 1);
                }
            }

            SetLevel(_graph.startWindowId, 0);

            // Располагаем узлы
            var levelPositions = new Dictionary<int, int>();
            foreach (var window in _graph.windows)
            {
                int level = levels.TryGetValue(window.id, out int l) ? l : 0;
                int posInLevel = levelPositions.TryGetValue(level, out int p) ? p : 0;
                levelPositions[level] = posInLevel + 1;

                window.editorPosition = new Vector2(
                    level * (NodeWidth + 100) + 50,
                    posInLevel * (NodeHeight + 50) + 50
                );
            }

            EditorUtility.SetDirty(_graph);
            Repaint();
        }

        #endregion
    }
}
