// Packages/com.protosystem.core/Editor/UI/UIWindowGraphViewer.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Визуальный редактор графа UI окон (node-based view)
    /// </summary>
    public class UIWindowGraphViewer : EditorWindow
    {
        private UIWindowGraph _graph;
        private Vector2 _scrollPosition;
        private Vector2 _drag;
        private float _zoom = 1f;
        
        private Dictionary<string, Rect> _nodeRects = new();
        private WindowDefinition _selectedNode;
        private bool _isDraggingNode;
        private string _draggingNodeId;

        // Стили
        private GUIStyle _nodeStyle;
        private GUIStyle _selectedNodeStyle;
        private GUIStyle _startNodeStyle;
        private GUIStyle _modalNodeStyle;
        private GUIStyle _labelStyle;

        private const float NODE_WIDTH = 160;
        private const float NODE_HEIGHT = 60;
        private const float GRID_SIZE = 20;

        [MenuItem("ProtoSystem/UI/Window Graph Viewer", priority = 110)]
        public static void ShowWindow()
        {
            var window = GetWindow<UIWindowGraphViewer>();
            window.titleContent = new GUIContent("UI Graph", EditorGUIUtility.IconContent("d_SceneViewFx").image);
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            LoadGraph();
            InitStyles();
        }

        private void LoadGraph()
        {
            _graph = Resources.Load<UIWindowGraph>(UIWindowGraph.RESOURCE_PATH);
            
            if (_graph != null)
            {
                // Инициализируем позиции узлов если не заданы
                ArrangeNodes();
            }
        }

        private void InitStyles()
        {
            _nodeStyle = new GUIStyle();
            _nodeStyle.normal.background = MakeTexture(2, 2, new Color(0.25f, 0.25f, 0.25f, 1f));
            _nodeStyle.border = new RectOffset(12, 12, 12, 12);
            _nodeStyle.padding = new RectOffset(10, 10, 10, 10);
            _nodeStyle.alignment = TextAnchor.MiddleCenter;
            _nodeStyle.normal.textColor = Color.white;
            _nodeStyle.fontStyle = FontStyle.Bold;

            _selectedNodeStyle = new GUIStyle(_nodeStyle);
            _selectedNodeStyle.normal.background = MakeTexture(2, 2, new Color(0.3f, 0.5f, 0.8f, 1f));

            _startNodeStyle = new GUIStyle(_nodeStyle);
            _startNodeStyle.normal.background = MakeTexture(2, 2, new Color(0.2f, 0.6f, 0.3f, 1f));

            _modalNodeStyle = new GUIStyle(_nodeStyle);
            _modalNodeStyle.normal.background = MakeTexture(2, 2, new Color(0.6f, 0.3f, 0.2f, 1f));

            _labelStyle = new GUIStyle(EditorStyles.label);
            _labelStyle.alignment = TextAnchor.MiddleCenter;
            _labelStyle.normal.textColor = Color.white;
            _labelStyle.fontSize = 10;
        }

        private void OnGUI()
        {
            if (_graph == null)
            {
                DrawNoGraphMessage();
                return;
            }

            DrawToolbar();
            DrawGrid();
            DrawConnections();
            DrawNodes();
            
            ProcessEvents();
        }

        private void DrawNoGraphMessage()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("No UIWindowGraph found", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Create / Rebuild Graph", GUILayout.Height(30)))
            {
                UIWindowGraphBuilder.RebuildGraph();
                LoadGraph();
            }
            
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Button("Rebuild", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                UIWindowGraphBuilder.RebuildGraph();
                LoadGraph();
            }
            
            if (GUILayout.Button("Arrange", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ArrangeNodes();
            }
            
            if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                UIWindowGraphBuilder.ValidateGraph();
            }

            GUILayout.FlexibleSpace();
            
            EditorGUILayout.LabelField($"Windows: {_graph.windowCount} | Transitions: {_graph.transitionCount}", 
                GUILayout.Width(200));
            
            GUILayout.FlexibleSpace();
            
            _zoom = EditorGUILayout.Slider(_zoom, 0.5f, 2f, GUILayout.Width(100));
            
            if (GUILayout.Button("Reset View", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                _scrollPosition = Vector2.zero;
                _zoom = 1f;
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGrid()
        {
            var rect = new Rect(0, 20, position.width, position.height - 20);
            
            // Фон
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
            
            // Сетка
            float gridSpacing = GRID_SIZE * _zoom;
            int widthDivs = Mathf.CeilToInt(rect.width / gridSpacing);
            int heightDivs = Mathf.CeilToInt(rect.height / gridSpacing);

            Handles.BeginGUI();
            Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);

            Vector3 offset = new Vector3(_scrollPosition.x % gridSpacing, _scrollPosition.y % gridSpacing, 0);

            for (int i = 0; i <= widthDivs; i++)
            {
                Handles.DrawLine(
                    new Vector3(gridSpacing * i + offset.x, rect.y, 0),
                    new Vector3(gridSpacing * i + offset.x, rect.yMax, 0));
            }

            for (int j = 0; j <= heightDivs; j++)
            {
                Handles.DrawLine(
                    new Vector3(rect.x, gridSpacing * j + offset.y + rect.y, 0),
                    new Vector3(rect.xMax, gridSpacing * j + offset.y + rect.y, 0));
            }

            Handles.EndGUI();
        }

        private void DrawConnections()
        {
            Handles.BeginGUI();

            // Локальные переходы
            foreach (var transition in _graph.transitions)
            {
                if (!_nodeRects.TryGetValue(transition.fromWindowId, out var fromRect)) continue;
                if (!_nodeRects.TryGetValue(transition.toWindowId, out var toRect)) continue;

                DrawConnection(fromRect, toRect, transition.trigger, Color.cyan);
            }

            // Глобальные переходы (рисуем от левого края)
            foreach (var transition in _graph.globalTransitions)
            {
                if (!_nodeRects.TryGetValue(transition.toWindowId, out var toRect)) continue;

                var fromPos = new Vector2(50 + _scrollPosition.x, toRect.center.y);
                var toPos = new Vector2(toRect.xMin, toRect.center.y);

                Handles.color = Color.yellow;
                Handles.DrawLine(fromPos, toPos);
                
                // Стрелка
                DrawArrow(toPos, Vector2.left);
                
                // Лейбл
                var labelPos = (fromPos + toPos) / 2;
                GUI.Label(new Rect(labelPos.x - 30, labelPos.y - 20, 60, 20), transition.trigger, _labelStyle);
            }

            Handles.EndGUI();
        }

        private void DrawConnection(Rect from, Rect to, string label, Color color)
        {
            var startPos = new Vector2(from.xMax, from.center.y);
            var endPos = new Vector2(to.xMin, to.center.y);

            // Bezier curve
            var startTangent = startPos + Vector2.right * 50;
            var endTangent = endPos + Vector2.left * 50;

            Handles.DrawBezier(startPos, endPos, startTangent, endTangent, color, null, 2f);

            // Стрелка
            DrawArrow(endPos, (endTangent - endPos).normalized);

            // Лейбл триггера
            var midPoint = GetBezierPoint(0.5f, startPos, startTangent, endTangent, endPos);
            GUI.Label(new Rect(midPoint.x - 40, midPoint.y - 20, 80, 20), label, _labelStyle);
        }

        private void DrawArrow(Vector2 point, Vector2 direction)
        {
            var arrowSize = 10f;
            var right = new Vector2(-direction.y, direction.x);

            var p1 = point + direction * arrowSize + right * arrowSize * 0.5f;
            var p2 = point + direction * arrowSize - right * arrowSize * 0.5f;

            Handles.DrawLine(point, p1);
            Handles.DrawLine(point, p2);
        }

        private Vector2 GetBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
        }

        private void DrawNodes()
        {
            foreach (var window in _graph.windows)
            {
                var rect = GetNodeRect(window);
                _nodeRects[window.id] = rect;

                // Выбор стиля
                GUIStyle style;
                if (_selectedNode == window)
                    style = _selectedNodeStyle;
                else if (window.id == _graph.startWindowId)
                    style = _startNodeStyle;
                else if (window.type == WindowType.Modal)
                    style = _modalNodeStyle;
                else
                    style = _nodeStyle;

                // Рисуем узел
                GUI.Box(rect, "", style);

                // Иконка prefab
                var iconRect = new Rect(rect.x + 5, rect.y + 5, 16, 16);
                var prefabIcon = window.prefab != null ? "✓" : "⚠";
                var prefabColor = window.prefab != null ? Color.green : Color.yellow;
                var iconStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = prefabColor } };
                GUI.Label(iconRect, prefabIcon, iconStyle);

                // Имя окна
                var nameRect = new Rect(rect.x, rect.y + 15, rect.width, 20);
                GUI.Label(nameRect, window.id, _labelStyle);

                // Тип
                var typeRect = new Rect(rect.x, rect.y + 35, rect.width, 16);
                var typeStyle = new GUIStyle(_labelStyle) { fontSize = 9, normal = { textColor = Color.gray } };
                GUI.Label(typeRect, $"[{window.type}]", typeStyle);
            }
        }

        private Rect GetNodeRect(WindowDefinition window)
        {
            var pos = window.editorPosition + _scrollPosition;
            return new Rect(pos.x, pos.y + 20, NODE_WIDTH * _zoom, NODE_HEIGHT * _zoom);
        }

        private void ProcessEvents()
        {
            Event e = Event.current;
            
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0) // Left click
                    {
                        var clickedNode = GetNodeAtPosition(e.mousePosition);
                        if (clickedNode != null)
                        {
                            _selectedNode = clickedNode;
                            _isDraggingNode = true;
                            _draggingNodeId = clickedNode.id;
                            
                            // Выбрать prefab в Project
                            if (clickedNode.prefab != null)
                                EditorGUIUtility.PingObject(clickedNode.prefab);
                        }
                        else
                        {
                            _selectedNode = null;
                        }
                        Repaint();
                    }
                    else if (e.button == 2) // Middle click - начало pan
                    {
                        _drag = e.mousePosition;
                    }
                    break;

                case EventType.MouseUp:
                    _isDraggingNode = false;
                    _draggingNodeId = null;
                    
                    if (_graph != null)
                        EditorUtility.SetDirty(_graph);
                    break;

                case EventType.MouseDrag:
                    if (e.button == 2 || (e.button == 0 && !_isDraggingNode)) // Pan
                    {
                        _scrollPosition += e.delta;
                        Repaint();
                    }
                    else if (_isDraggingNode && _draggingNodeId != null) // Drag node
                    {
                        var node = _graph.windows.FirstOrDefault(w => w.id == _draggingNodeId);
                        if (node != null)
                        {
                            node.editorPosition += e.delta / _zoom;
                            Repaint();
                        }
                    }
                    break;

                case EventType.ScrollWheel:
                    _zoom -= e.delta.y * 0.05f;
                    _zoom = Mathf.Clamp(_zoom, 0.5f, 2f);
                    Repaint();
                    e.Use();
                    break;
            }
        }

        private WindowDefinition GetNodeAtPosition(Vector2 mousePos)
        {
            foreach (var window in _graph.windows)
            {
                var rect = GetNodeRect(window);
                if (rect.Contains(mousePos))
                    return window;
            }
            return null;
        }

        private void ArrangeNodes()
        {
            if (_graph == null || _graph.windows.Count == 0) return;

            // Простой алгоритм: располагаем по слоям
            var layers = new Dictionary<int, List<WindowDefinition>>();
            var visited = new HashSet<string>();
            var depths = new Dictionary<string, int>();

            // BFS для определения глубины
            void CalculateDepth(string windowId, int depth)
            {
                if (visited.Contains(windowId)) return;
                visited.Add(windowId);
                
                if (!depths.ContainsKey(windowId) || depths[windowId] > depth)
                    depths[windowId] = depth;

                var transitions = _graph.GetTransitionsFrom(windowId);
                foreach (var t in transitions)
                {
                    CalculateDepth(t.toWindowId, depth + 1);
                }
            }

            // Начинаем со стартового окна
            var startId = _graph.startWindowId;
            if (string.IsNullOrEmpty(startId) && _graph.windows.Count > 0)
                startId = _graph.windows[0].id;

            CalculateDepth(startId, 0);

            // Добавляем недостижимые окна
            foreach (var window in _graph.windows)
            {
                if (!depths.ContainsKey(window.id))
                    depths[window.id] = 99;
            }

            // Группируем по слоям
            foreach (var window in _graph.windows)
            {
                var depth = depths.GetValueOrDefault(window.id, 0);
                if (!layers.ContainsKey(depth))
                    layers[depth] = new List<WindowDefinition>();
                layers[depth].Add(window);
            }

            // Располагаем
            float xOffset = 100;
            float yOffset = 100;
            float xSpacing = NODE_WIDTH + 80;
            float ySpacing = NODE_HEIGHT + 40;

            foreach (var layer in layers.OrderBy(l => l.Key))
            {
                float y = yOffset;
                foreach (var window in layer.Value)
                {
                    window.editorPosition = new Vector2(xOffset + layer.Key * xSpacing, y);
                    y += ySpacing;
                }
            }

            if (_graph != null)
                EditorUtility.SetDirty(_graph);
            
            Repaint();
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
    }
}
