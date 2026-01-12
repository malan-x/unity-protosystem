// Packages/com.protosystem.core/Editor/UI/UIWindowGraphViewer.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.UI
{
    public class UIWindowGraphViewer : EditorWindow
    {
        private UIWindowGraph _graph;
        private Vector2 _scrollPosition;
        private Vector2 _inspectorScroll;
        private float _zoom = 1f;
        
        private Dictionary<string, Rect> _nodeRects = new();
        private Dictionary<string, bool> _reachableWindows = new();
        private WindowDefinition _selectedNode;
        private TransitionDefinition _selectedTransition;
        private bool _isDraggingNode;
        private string _draggingNodeId;

        // Статические кешированные текстуры - не теряются при перерисовке
        private static Texture2D _nodeTexture;
        private static Texture2D _selectedNodeTexture;
        private static Texture2D _startNodeTexture;
        private static Texture2D _modalNodeTexture;
        private static Texture2D _unreachableNodeTexture;

        private GUIStyle _nodeStyle;
        private GUIStyle _selectedNodeStyle;
        private GUIStyle _startNodeStyle;
        private GUIStyle _modalNodeStyle;
        private GUIStyle _unreachableNodeStyle;
        private GUIStyle _labelStyle;

        private const float NODE_WIDTH = 180;
        private const float NODE_HEIGHT = 70;
        private const float GRID_SIZE = 20;
        private const float INSPECTOR_WIDTH = 300;

        [MenuItem("ProtoSystem/UI/Graph/Window Graph Viewer", priority = 130)]
        public static void ShowWindow()
        {
            var window = GetWindow<UIWindowGraphViewer>();
            window.titleContent = new GUIContent("UI Graph", EditorGUIUtility.IconContent("d_SceneViewFx").image);
            window.minSize = new Vector2(800, 400);
            window.Show();
        }

        private void OnEnable()
        {
            LoadGraph();
        }

        private void LoadGraph()
        {
            _graph = Resources.Load<UIWindowGraph>(UIWindowGraph.RESOURCE_PATH);
            
            if (_graph != null)
            {
                ArrangeNodes();
                UpdateReachability();
            }
        }

        private void UpdateReachability()
        {
            _reachableWindows.Clear();
            
            if (_graph == null || _graph.windows.Count == 0) return;
            
            var startId = _graph.startWindowId;
            if (string.IsNullOrEmpty(startId) && _graph.windows.Count > 0)
                startId = _graph.windows[0].id;
            
            var visited = new HashSet<string>();
            
            void MarkReachable(string windowId)
            {
                if (visited.Contains(windowId)) return;
                visited.Add(windowId);
                _reachableWindows[windowId] = true;
                
                var transitions = _graph.GetTransitionsFrom(windowId);
                foreach (var t in transitions)
                {
                    MarkReachable(t.toWindowId);
                }
            }
            
            MarkReachable(startId);
            
            foreach (var window in _graph.windows)
            {
                if (!_reachableWindows.ContainsKey(window.id))
                {
                    _reachableWindows[window.id] = false;
                }
            }
        }

        private bool IsWindowReachable(string windowId)
        {
            return _reachableWindows.GetValueOrDefault(windowId, false);
        }

        private void InitStyles()
        {
            // Создаем текстуры только один раз
            if (_nodeTexture == null)
                _nodeTexture = MakeRoundedTexture(180, 70, 12, new Color(0.25f, 0.25f, 0.25f, 1f));
            if (_selectedNodeTexture == null)
                _selectedNodeTexture = MakeRoundedTexture(180, 70, 12, new Color(0.3f, 0.5f, 0.9f, 1f));
            if (_startNodeTexture == null)
                _startNodeTexture = MakeRoundedTexture(180, 70, 12, new Color(0.2f, 0.7f, 0.3f, 1f));
            if (_modalNodeTexture == null)
                _modalNodeTexture = MakeRoundedTexture(180, 70, 12, new Color(0.7f, 0.3f, 0.2f, 1f));
            if (_unreachableNodeTexture == null)
                _unreachableNodeTexture = MakeRoundedTexture(180, 70, 12, new Color(0.2f, 0.2f, 0.2f, 0.5f));

            _nodeStyle = new GUIStyle();
            _nodeStyle.normal.background = _nodeTexture;
            _nodeStyle.border = new RectOffset(12, 12, 12, 12);
            _nodeStyle.padding = new RectOffset(10, 10, 10, 10);
            _nodeStyle.alignment = TextAnchor.MiddleCenter;
            _nodeStyle.normal.textColor = Color.white;
            _nodeStyle.fontStyle = FontStyle.Bold;
            _nodeStyle.fontSize = 12;

            _selectedNodeStyle = new GUIStyle(_nodeStyle);
            _selectedNodeStyle.normal.background = _selectedNodeTexture;

            _startNodeStyle = new GUIStyle(_nodeStyle);
            _startNodeStyle.normal.background = _startNodeTexture;

            _modalNodeStyle = new GUIStyle(_nodeStyle);
            _modalNodeStyle.normal.background = _modalNodeTexture;

            _unreachableNodeStyle = new GUIStyle(_nodeStyle);
            _unreachableNodeStyle.normal.background = _unreachableNodeTexture;
            _unreachableNodeStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 1f);

            _labelStyle = new GUIStyle(EditorStyles.label);
            _labelStyle.alignment = TextAnchor.MiddleCenter;
            _labelStyle.normal.textColor = Color.white;
            _labelStyle.fontSize = 12;
            _labelStyle.fontStyle = FontStyle.Bold;
        }

        private void OnGUI()
        {
            // Пересоздаем стили при каждой отрисовке (но текстуры кешируются статически)
            InitStyles();
            
            if (_graph == null)
            {
                DrawNoGraphMessage();
                return;
            }

            DrawToolbar();
            
            var graphRect = new Rect(0, 20, position.width - INSPECTOR_WIDTH, position.height - 20);
            var inspectorRect = new Rect(position.width - INSPECTOR_WIDTH, 20, INSPECTOR_WIDTH, position.height - 20);
            
            GUILayout.BeginArea(graphRect);
            DrawGraph();
            GUILayout.EndArea();
            
            GUILayout.BeginArea(inspectorRect);
            DrawInspector();
            GUILayout.EndArea();
            
            ProcessEvents(graphRect);
        }

        private void DrawGraph()
        {
            DrawGrid();
            DrawConnections();
            DrawNodes();
        }

        private void DrawInspector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Информация о выбранной связи
            if (_selectedTransition != null)
            {
                _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);

                EditorGUILayout.LabelField("Transition Info", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                // Направление
                bool isGlobal = string.IsNullOrEmpty(_selectedTransition.fromWindowId);
                if (isGlobal)
                {
                    EditorGUILayout.LabelField("Type:", "Global Transition");
                    EditorGUILayout.LabelField("From:", "Any Window");
                    EditorGUILayout.LabelField("To:", _selectedTransition.toWindowId);
                }
                else
                {
                    EditorGUILayout.LabelField("Type:", "Local Transition");
                    EditorGUILayout.LabelField("From:", _selectedTransition.fromWindowId);
                    EditorGUILayout.LabelField("To:", _selectedTransition.toWindowId);
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Trigger:", _selectedTransition.trigger);
                EditorGUILayout.LabelField("Animation:", _selectedTransition.animation.ToString());

                EditorGUILayout.Space(10);

                // Проверяем двустороннюю связь
                if (!isGlobal)
                {
                    var reverseTransition = _graph.transitions.FirstOrDefault(t => 
                        t.fromWindowId == _selectedTransition.toWindowId && 
                        t.toWindowId == _selectedTransition.fromWindowId);

                    if (reverseTransition != null)
                    {
                        EditorGUILayout.HelpBox($"Bidirectional: Yes (reverse trigger: [{reverseTransition.trigger}])", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Bidirectional: No (one-way transition)", MessageType.None);
                    }

                    EditorGUILayout.Space(10);
                }

                // Префабы окон
                if (!isGlobal)
                {
                    EditorGUILayout.LabelField("Window Prefabs", EditorStyles.boldLabel);

                    // From Window
                    var fromWindow = _graph.windows.FirstOrDefault(w => w.id == _selectedTransition.fromWindowId);
                    if (fromWindow != null)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField($"From: {fromWindow.id}", EditorStyles.boldLabel);
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField(fromWindow.prefab, typeof(GameObject), false);
                        EditorGUI.EndDisabledGroup();
                        if (fromWindow.prefab != null && GUILayout.Button("Select From Prefab"))
                        {
                            Selection.activeObject = fromWindow.prefab;
                            EditorGUIUtility.PingObject(fromWindow.prefab);
                        }
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.Space(5);
                    }

                    // To Window
                    var toWindow = _graph.windows.FirstOrDefault(w => w.id == _selectedTransition.toWindowId);
                    if (toWindow != null)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField($"To: {toWindow.id}", EditorStyles.boldLabel);
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField(toWindow.prefab, typeof(GameObject), false);
                        EditorGUI.EndDisabledGroup();
                        if (toWindow.prefab != null && GUILayout.Button("Select To Prefab"))
                        {
                            Selection.activeObject = toWindow.prefab;
                            EditorGUIUtility.PingObject(toWindow.prefab);
                        }
                        EditorGUILayout.EndVertical();
                    }
                }
                else
                {
                    // Для глобальных переходов - только To Window
                    var toWindow = _graph.windows.FirstOrDefault(w => w.id == _selectedTransition.toWindowId);
                    if (toWindow != null)
                    {
                        EditorGUILayout.LabelField("Target Window", EditorStyles.boldLabel);
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField($"To: {toWindow.id}", EditorStyles.boldLabel);
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField(toWindow.prefab, typeof(GameObject), false);
                        EditorGUI.EndDisabledGroup();
                        if (toWindow.prefab != null && GUILayout.Button("Select Prefab"))
                        {
                            Selection.activeObject = toWindow.prefab;
                            EditorGUIUtility.PingObject(toWindow.prefab);
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                EditorGUILayout.Space(10);

                // Кнопка для перехода к окнам
                if (!isGlobal)
                {
                    EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Select From Window"))
                    {
                        var fromWindow = _graph.windows.FirstOrDefault(w => w.id == _selectedTransition.fromWindowId);
                        if (fromWindow != null)
                        {
                            _selectedNode = fromWindow;
                            _selectedTransition = null;
                            Repaint();
                        }
                    }
                    if (GUILayout.Button("Select To Window"))
                    {
                        var toWindow = _graph.windows.FirstOrDefault(w => w.id == _selectedTransition.toWindowId);
                        if (toWindow != null)
                        {
                            _selectedNode = toWindow;
                            _selectedTransition = null;
                            Repaint();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
            // Информация о выбранном окне
            else if (_selectedNode != null)
            {
                _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);

                EditorGUILayout.LabelField("Window Info", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("ID:", _selectedNode.id);
                EditorGUILayout.LabelField("Type:", _selectedNode.type.ToString());
                EditorGUILayout.LabelField("Layer:", _selectedNode.layer.ToString());
                EditorGUILayout.LabelField("Level:", _selectedNode.level.ToString());
                EditorGUILayout.LabelField("Reachable:", IsWindowReachable(_selectedNode.id) ? "Yes" : "No");

                EditorGUILayout.Space(10);

                // Префаб
                EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(_selectedNode.prefab, typeof(GameObject), false);
                EditorGUI.EndDisabledGroup();

                if (_selectedNode.prefab != null && GUILayout.Button("Select Prefab"))
                {
                    Selection.activeObject = _selectedNode.prefab;
                    EditorGUIUtility.PingObject(_selectedNode.prefab);
                }

                EditorGUILayout.Space(10);

                // Исходящие переходы
                var outTransitions = _graph.GetTransitionsFrom(_selectedNode.id).ToList();
                EditorGUILayout.LabelField($"Outgoing Transitions ({outTransitions.Count})", EditorStyles.boldLabel);

                foreach (var t in outTransitions)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"→ {t.toWindowId}", GUILayout.Width(150));
                    EditorGUILayout.LabelField($"[{t.trigger}]", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }

                if (outTransitions.Count == 0)
                {
                    EditorGUILayout.HelpBox("No outgoing transitions", MessageType.Info);
                }

                EditorGUILayout.Space(10);

                // Входящие переходы
                var inTransitions = _graph.transitions
                    .Where(t => t.toWindowId == _selectedNode.id)
                    .ToList();

                EditorGUILayout.LabelField($"Incoming Transitions ({inTransitions.Count})", EditorStyles.boldLabel);

                foreach (var t in inTransitions)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"← {t.fromWindowId}", GUILayout.Width(150));
                    EditorGUILayout.LabelField($"[{t.trigger}]", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }

                if (inTransitions.Count == 0)
                {
                    EditorGUILayout.HelpBox("No incoming transitions", MessageType.Info);
                }

                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.LabelField("Nothing selected", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox("Click on a node or connection to see details", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
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
            
            EditorGUILayout.LabelField("Zoom:", GUILayout.Width(40));
            _zoom = EditorGUILayout.Slider(_zoom, 0.5f, 2f, GUILayout.Width(100));
            
            if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _scrollPosition = Vector2.zero;
                _zoom = 1f;
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGrid()
        {
            var rect = new Rect(0, 0, position.width - INSPECTOR_WIDTH, position.height - 20);
            
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
            
            float gridSpacing = GRID_SIZE * _zoom;
            int widthDivs = Mathf.CeilToInt(rect.width / gridSpacing);
            int heightDivs = Mathf.CeilToInt(rect.height / gridSpacing);

            Handles.BeginGUI();
            Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);

            Vector3 offset = new Vector3(_scrollPosition.x % gridSpacing, _scrollPosition.y % gridSpacing, 0);

            for (int i = 0; i <= widthDivs; i++)
            {
                Handles.DrawLine(
                    new Vector3(gridSpacing * i + offset.x, 0, 0),
                    new Vector3(gridSpacing * i + offset.x, rect.height, 0));
            }

            for (int j = 0; j <= heightDivs; j++)
            {
                Handles.DrawLine(
                    new Vector3(0, gridSpacing * j + offset.y, 0),
                    new Vector3(rect.width, gridSpacing * j + offset.y, 0));
            }

            Handles.EndGUI();
        }

        private void DrawConnections()
        {
            Handles.BeginGUI();

            int transitionIndex = 0;

            // Локальные переходы
            foreach (var transition in _graph.transitions)
            {
                if (!_nodeRects.TryGetValue(transition.fromWindowId, out var fromRect)) continue;
                if (!_nodeRects.TryGetValue(transition.toWindowId, out var toRect)) continue;

                bool isSelected = _selectedTransition != null && 
                                _selectedTransition.fromWindowId == transition.fromWindowId && 
                                _selectedTransition.toWindowId == transition.toWindowId &&
                                _selectedTransition.trigger == transition.trigger;

                var color = isSelected ? Color.yellow : new Color(0.4f, 0.8f, 1f, 1f);
                DrawConnection(fromRect, toRect, transition.trigger, color, transitionIndex, transition);
                transitionIndex++;
            }

            // Глобальные переходы
            foreach (var transition in _graph.globalTransitions)
            {
                if (!_nodeRects.TryGetValue(transition.toWindowId, out var toRect)) continue;

                bool isSelected = _selectedTransition != null && 
                                _selectedTransition.toWindowId == transition.toWindowId &&
                                _selectedTransition.trigger == transition.trigger;

                float arrowSize = 12f * _zoom;

                var fromPos = new Vector2(50 + _scrollPosition.x, toRect.center.y);
                var toPos = new Vector2(toRect.xMin - arrowSize, toRect.center.y); // Линия заканчивается перед стрелкой

                var color = isSelected ? Color.yellow : new Color(1f, 0.9f, 0.3f, 1f);
                Handles.color = color;
                Handles.DrawLine(fromPos, toPos, 10f * _zoom);

                // Стрелка у края ноды, основание упирается в линию
                var arrowPos = new Vector2(toRect.xMin, toRect.center.y);
                DrawArrowhead(arrowPos, Vector2.left, 12f);

                var labelPos = (fromPos + toPos) / 2;
                var labelStyle = new GUIStyle(_labelStyle) { 
                    fontSize = (int)(12 * _zoom), 
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = color }
                };
                GUI.Label(new Rect(labelPos.x - 50, labelPos.y - 15, 100, 30), transition.trigger, labelStyle);
            }

            Handles.EndGUI();
        }

        private void DrawConnection(Rect from, Rect to, string label, Color color, int index, TransitionDefinition transition)
        {
            // Вычисляем offset для выхода из разных точек (mindmap style)
            float offsetY = (index % 3 - 1) * 15f * _zoom; // -15, 0, +15

            float arrowSize = 12f * _zoom; // Уменьшили размер стрелки в 2 раза

            // Без отступов от нод
            var startPos = new Vector2(from.xMax, from.center.y + offsetY);
            var endPos = new Vector2(to.xMin - arrowSize, to.center.y + offsetY); // Линия заканчивается перед стрелкой

            var startTangent = startPos + Vector2.right * 60 * _zoom;
            var endTangent = endPos + Vector2.left * 60 * _zoom;

            Handles.color = color;
            Handles.DrawBezier(startPos, endPos, startTangent, endTangent, color, null, 10f * _zoom);

            // Стрелка у края ноды, основание упирается в линию
            var arrowPos = new Vector2(to.xMin, to.center.y + offsetY);
            var direction = Vector2.left; // Стрелка смотрит влево (основание справа, вершина слева)
            DrawArrowhead(arrowPos, direction, 12f);

            // Лейбл
            var midPoint = GetBezierPoint(0.5f, startPos, startTangent, endTangent, endPos);
            var labelStyle = new GUIStyle(_labelStyle) { 
                fontSize = (int)(12 * _zoom), 
                fontStyle = FontStyle.Bold,
                normal = { textColor = color }
            };
            var labelContent = new GUIContent(label);
            var labelSize = labelStyle.CalcSize(labelContent);
            var labelRect = new Rect(midPoint.x - labelSize.x/2, midPoint.y - labelSize.y/2, labelSize.x, labelSize.y);

            // Проверяем клик по лейблу или рядом с линией
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                var mousePos = Event.current.mousePosition;

                // Кликнули по лейблу
                if (labelRect.Contains(mousePos))
                {
                    _selectedTransition = transition;
                    _selectedNode = null;
                    Event.current.Use();
                    Repaint();
                }
                // Кликнули рядом с линией (проверяем расстояние до кривой)
                else if (IsPointNearBezier(mousePos, startPos, startTangent, endTangent, endPos, 15f * _zoom))
                {
                    _selectedTransition = transition;
                    _selectedNode = null;
                    Event.current.Use();
                    Repaint();
                }
            }

            GUI.Label(labelRect, labelContent, labelStyle);
        }

        private void DrawArrowhead(Vector2 point, Vector2 direction, float baseSize)
        {
            var arrowSize = baseSize * _zoom;
            var right = new Vector2(-direction.y, direction.x);

            var p1 = point + direction * arrowSize + right * arrowSize * 0.5f;
            var p2 = point + direction * arrowSize - right * arrowSize * 0.5f;

            // Заполненная стрелка
            Handles.DrawAAConvexPolygon(point, p1, p2);
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

                bool isReachable = IsWindowReachable(window.id);

                GUIStyle style;
                // Подсвечиваем ноду только если она выбрана И не выбрана связь
                if (_selectedNode == window && _selectedTransition == null)
                    style = _selectedNodeStyle;
                else if (!isReachable)
                    style = _unreachableNodeStyle;
                else if (window.id == _graph.startWindowId)
                    style = _startNodeStyle;
                else if (window.type == WindowType.Modal)
                    style = _modalNodeStyle;
                else
                    style = _nodeStyle;

                GUI.Box(rect, "", style);

                var iconRect = new Rect(rect.x + 5 * _zoom, rect.y + 5 * _zoom, 16 * _zoom, 16 * _zoom);
                var prefabIcon = window.prefab != null ? "✓" : "⚠";
                var prefabColor = window.prefab != null ? Color.green : Color.yellow;

                if (!isReachable)
                    prefabColor = new Color(prefabColor.r * 0.5f, prefabColor.g * 0.5f, prefabColor.b * 0.5f, 1f);

                var iconStyle = new GUIStyle(EditorStyles.label) { 
                    normal = { textColor = prefabColor },
                    fontSize = (int)(14 * _zoom),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
                GUI.Label(iconRect, prefabIcon, iconStyle);

                var nameRect = new Rect(rect.x + 5 * _zoom, rect.y + rect.height * 0.3f, rect.width - 10 * _zoom, rect.height * 0.4f);
                var nameStyle = new GUIStyle(_labelStyle);
                nameStyle.fontSize = (int)(11 * _zoom);
                nameStyle.normal.textColor = isReachable ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
                nameStyle.wordWrap = false;
                nameStyle.clipping = TextClipping.Clip;
                GUI.Label(nameRect, window.id, nameStyle);

                var typeRect = new Rect(rect.x + 5 * _zoom, rect.y + rect.height * 0.7f, rect.width - 10 * _zoom, rect.height * 0.25f);
                var typeColor = isReachable ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.4f, 0.4f, 0.4f);
                var typeStyle = new GUIStyle(_labelStyle) { 
                    fontSize = (int)(9 * _zoom), 
                    normal = { textColor = typeColor },
                    fontStyle = FontStyle.Normal
                };

                var typeLabel = isReachable ? $"[{window.type}]" : $"[{window.type}] ⚠";
                GUI.Label(typeRect, typeLabel, typeStyle);
            }
        }

        private Rect GetNodeRect(WindowDefinition window)
        {
            var pos = window.editorPosition + _scrollPosition;
            return new Rect(pos.x, pos.y, NODE_WIDTH * _zoom, NODE_HEIGHT * _zoom);
        }

        private void ProcessEvents(Rect graphRect)
        {
            Event e = Event.current;

            if (!graphRect.Contains(e.mousePosition))
                return;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        var clickedNode = GetNodeAtPosition(e.mousePosition);
                        if (clickedNode != null)
                        {
                            _selectedNode = clickedNode;
                            _selectedTransition = null; // Обнуляем выбранную связь
                            _isDraggingNode = true;
                            _draggingNodeId = clickedNode.id;

                            if (clickedNode.prefab != null)
                                EditorGUIUtility.PingObject(clickedNode.prefab);

                            e.Use();
                        }
                        else
                        {
                            // Кликнули в пустоту - обнуляем всё
                            _selectedNode = null;
                            _selectedTransition = null;
                        }
                        Repaint();
                    }
                    else if (e.button == 2)
                    {
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    _isDraggingNode = false;
                    _draggingNodeId = null;

                    if (_graph != null)
                        EditorUtility.SetDirty(_graph);
                    break;

                case EventType.MouseDrag:
                    if (e.button == 2 || (e.button == 0 && !_isDraggingNode))
                    {
                        _scrollPosition += e.delta;
                        e.Use();
                        Repaint();
                    }
                    else if (_isDraggingNode && _draggingNodeId != null)
                    {
                        var node = _graph.windows.FirstOrDefault(w => w.id == _draggingNodeId);
                        if (node != null)
                        {
                            node.editorPosition += e.delta / _zoom;
                            e.Use();
                            Repaint();
                        }
                    }
                    break;

                case EventType.ScrollWheel:
                    _zoom -= e.delta.y * 0.05f;
                    _zoom = Mathf.Clamp(_zoom, 0.5f, 2f);
                    e.Use();
                    Repaint();
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

            var layers = new Dictionary<int, List<WindowDefinition>>();
            var visited = new HashSet<string>();
            var depths = new Dictionary<string, int>();

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

            var startId = _graph.startWindowId;
            if (string.IsNullOrEmpty(startId) && _graph.windows.Count > 0)
                startId = _graph.windows[0].id;

            CalculateDepth(startId, 0);

            var unreachableWindows = new List<WindowDefinition>();
            foreach (var window in _graph.windows)
            {
                if (!depths.ContainsKey(window.id))
                {
                    unreachableWindows.Add(window);
                    depths[window.id] = -1;
                }
            }

            foreach (var window in _graph.windows)
            {
                var depth = depths.GetValueOrDefault(window.id, 0);
                if (!layers.ContainsKey(depth))
                    layers[depth] = new List<WindowDefinition>();
                layers[depth].Add(window);
            }

            float xOffset = 100;
            float yOffset = 100;
            float xSpacing = NODE_WIDTH + 100;
            float ySpacing = NODE_HEIGHT + 50;

            foreach (var layer in layers.OrderBy(l => l.Key).Where(l => l.Key >= 0))
            {
                float y = yOffset;
                foreach (var window in layer.Value)
                {
                    window.editorPosition = new Vector2(xOffset + layer.Key * xSpacing, y);
                    y += ySpacing;
                }
            }

            if (unreachableWindows.Count > 0)
            {
                var maxDepth = layers.Keys.Where(k => k >= 0).DefaultIfEmpty(0).Max();
                float unreachableX = xOffset + (maxDepth + 2) * xSpacing;
                float unreachableY = yOffset;

                foreach (var window in unreachableWindows)
                {
                    window.editorPosition = new Vector2(unreachableX, unreachableY);
                    unreachableY += ySpacing;
                }
            }

            if (_graph != null)
                EditorUtility.SetDirty(_graph);

            Repaint();
        }

        private Texture2D MakeRoundedTexture(int width, int height, int radius, Color color)
        {
            var texture = new Texture2D(width, height);
            texture.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isInside = true;
                    
                    // Проверяем углы
                    if (x < radius && y < radius)
                        isInside = (x - radius) * (x - radius) + (y - radius) * (y - radius) <= radius * radius;
                    else if (x >= width - radius && y < radius)
                        isInside = (x - (width - radius)) * (x - (width - radius)) + (y - radius) * (y - radius) <= radius * radius;
                    else if (x < radius && y >= height - radius)
                        isInside = (x - radius) * (x - radius) + (y - (height - radius)) * (y - (height - radius)) <= radius * radius;
                    else if (x >= width - radius && y >= height - radius)
                        isInside = (x - (width - radius)) * (x - (width - radius)) + (y - (height - radius)) * (y - (height - radius)) <= radius * radius;
                    
                    pixels[y * width + x] = isInside ? color : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private bool IsPointNearBezier(Vector2 point, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float threshold)
        {
            // Проверяем 20 точек вдоль кривой
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                var bezierPoint = GetBezierPoint(t, p0, p1, p2, p3);
                if (Vector2.Distance(point, bezierPoint) < threshold)
                    return true;
            }
            return false;
        }
    }
}
