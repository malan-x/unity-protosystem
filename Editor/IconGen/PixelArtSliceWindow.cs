using System.Collections.Generic;
using System.IO;
using ProtoSystem.IconGen.Editor;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Пиксель-арт из буфера: облачные генераторы рисуют «псевдо-пиксель-арт», где одна
    /// клетка занимает много реальных пикселей, клетки не идеально квадратны и сетка
    /// «плывёт» по листу. Здесь: выделяем иконку рамкой, окно строит АДАПТИВНУЮ сетку —
    /// оценив средний шаг (медиана расстояний между пиками перепадов), идёт от клетки
    /// к клетке и ищет каждую следующую границу локально, так что дрейф не накапливается.
    /// Результат — один пиксель на клетку, цвет из ЦЕНТРА клетки (не усреднение).
    /// Вырез фона — отдельный финальный шаг при сохранении, в детекции не участвует.
    /// </summary>
    public class PixelArtSliceWindow : EditorWindow
    {
        private IconCollection _col;
        private Texture2D _sheet;

        // Выделение (координаты текстуры, y вверх)
        private RectInt _sel;
        private bool _hasSel;
        private bool _dragging;
        private Vector2Int _dragStart;

        // Ресайз рамки за края/углы
        private bool _resizing;
        private bool _rL, _rR, _rT, _rB;              // какие края тянем (GUI-стороны)
        private const float GrabPx = 7f;              // зона захвата, px экрана

        // Адаптивная сетка: границы клеток ВНУТРИ выделения (в px от края выделения)
        private float _cellX = 8f, _cellY = 8f;      // средний шаг — затравка для обхода
        private readonly List<float> _gridX = new();
        private readonly List<float> _gridY = new();
        private float[] _profX, _profY;              // профили перепадов текущего выделения

        private enum BgCutMode
        {
            None = 0,       // не вырезать
            Outer = 1,      // только внешний фон: заливка от краёв (внутренние точки живут)
            Everywhere = 2, // по цвету везде (старый режим)
        }

        // Результат
        private Texture2D _result;
        private Texture2D _resultCut;                 // второе превью: после выреза фона
        private bool _dirty = true;
        private BgCutMode _bgMode = BgCutMode.Outer;  // применяется ТОЛЬКО при сохранении
        private int _bgThreshold = 24;

        private int _entityIndex;
        private float _viewZoom = 1f;
        private Vector2 _scroll;

        public static void Open(IconCollection col)
        {
            var tex = ClipboardImageImporter.GrabClipboardTexture();
            if (tex == null)
            {
                EditorUtility.DisplayDialog("Пиксель-арт", "В буфере обмена нет картинки.", "Ок");
                return;
            }
            var win = CreateInstance<PixelArtSliceWindow>();
            win._col = col;
            win._sheet = tex;
            win._sheet.filterMode = FilterMode.Point;
            win.titleContent = new GUIContent("Пиксель-арт из буфера");
            win.minSize = new Vector2(760, 560);
            win._viewZoom = Mathf.Clamp(700f / tex.width, 0.1f, 2f);
            win.ShowUtility();
        }

        private void OnDisable()
        {
            if (_sheet != null) DestroyImmediate(_sheet);
            if (_result != null) DestroyImmediate(_result);
            if (_resultCut != null) DestroyImmediate(_resultCut);
        }

        private void OnGUI()
        {
            if (_sheet == null || _col == null) { Close(); return; }

            EditorGUILayout.LabelField($"«{_col.Title}» — лист {_sheet.width}×{_sheet.height}. " +
                "Выдели иконку рамкой (ЛКМ-драг по листу).", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawSheetView();
            DrawSidePanel();
            EditorGUILayout.EndHorizontal();
        }

        // ────────────────────────── лист с выделением ──────────────────────────

        private void DrawSheetView()
        {
            EditorGUILayout.BeginVertical();
            _viewZoom = EditorGUILayout.Slider("Масштаб листа", _viewZoom, 0.1f, 6f);

            _scroll = EditorGUILayout.BeginScrollView(_scroll,
                GUILayout.MaxWidth(position.width - 320), GUILayout.ExpandHeight(true));
            float w = _sheet.width * _viewZoom, h = _sheet.height * _viewZoom;
            var drawRect = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));

            var e = Event.current;
            var texPos = GuiToTex(drawRect, e.mousePosition);

            // Зоны захвата краёв рамки (курсоры + старт ресайза)
            if (_hasSel && !_dragging)
            {
                var sg = TexToGui(drawRect, _sel);
                HandleResizeZones(sg, e, texPos);
            }

            if (!_resizing && e.type == EventType.MouseDown && e.button == 0 && drawRect.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragStart = texPos;
                _sel = new RectInt(texPos.x, texPos.y, 0, 0);
                _hasSel = false;
                e.Use();
            }
            else if (_dragging && e.type == EventType.MouseDrag)
            {
                _sel = FromCorners(_dragStart, texPos);
                e.Use(); Repaint();
            }
            else if (_dragging && e.type == EventType.MouseUp)
            {
                _dragging = false;
                _sel = FromCorners(_dragStart, texPos);
                if (_sel.width >= 8 && _sel.height >= 8)
                {
                    _hasSel = true;
                    AutoDetect();
                }
                e.Use(); Repaint();
            }

            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(drawRect, new Color(0.1f, 0.1f, 0.12f));
                GUI.DrawTexture(drawRect, _sheet, ScaleMode.StretchToFill, true);

                if (_sel.width > 0 && _sel.height > 0)
                {
                    var selGui = TexToGui(drawRect, _sel);
                    DrawFrame(selGui, _hasSel ? new Color(1f, .8f, .3f) : Color.cyan, 2);
                    if (_hasSel) DrawGridOverlay(selGui);
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Ресайз жёлтой рамки за края/углы: курсоры у зон захвата, MouseDown в зоне
        /// начинает перетяжку соответствующих сторон, MouseUp пересчитывает профили и
        /// сетку (шаг клетки сохраняется — ручная подстройка не сбрасывается).
        /// </summary>
        private void HandleResizeZones(Rect sg, Event e, Vector2Int texPos)
        {
            var left   = new Rect(sg.x - GrabPx, sg.y - GrabPx, GrabPx * 2, sg.height + GrabPx * 2);
            var right  = new Rect(sg.xMax - GrabPx, sg.y - GrabPx, GrabPx * 2, sg.height + GrabPx * 2);
            var top    = new Rect(sg.x - GrabPx, sg.y - GrabPx, sg.width + GrabPx * 2, GrabPx * 2);
            var bottom = new Rect(sg.x - GrabPx, sg.yMax - GrabPx, sg.width + GrabPx * 2, GrabPx * 2);

            bool onL = left.Contains(e.mousePosition), onR = right.Contains(e.mousePosition);
            bool onT = top.Contains(e.mousePosition), onB = bottom.Contains(e.mousePosition);

            // Курсоры (углы — диагональные)
            if (onL && onT) EditorGUIUtility.AddCursorRect(new Rect(e.mousePosition.x - 8, e.mousePosition.y - 8, 16, 16), MouseCursor.ResizeUpLeft);
            else if (onR && onB) EditorGUIUtility.AddCursorRect(new Rect(e.mousePosition.x - 8, e.mousePosition.y - 8, 16, 16), MouseCursor.ResizeUpLeft);
            else if (onR && onT) EditorGUIUtility.AddCursorRect(new Rect(e.mousePosition.x - 8, e.mousePosition.y - 8, 16, 16), MouseCursor.ResizeUpRight);
            else if (onL && onB) EditorGUIUtility.AddCursorRect(new Rect(e.mousePosition.x - 8, e.mousePosition.y - 8, 16, 16), MouseCursor.ResizeUpRight);
            else if (onL || onR) EditorGUIUtility.AddCursorRect(onL ? left : right, MouseCursor.ResizeHorizontal);
            else if (onT || onB) EditorGUIUtility.AddCursorRect(onT ? top : bottom, MouseCursor.ResizeVertical);

            if (e.type == EventType.MouseDown && e.button == 0 && (onL || onR || onT || onB))
            {
                _resizing = true;
                _rL = onL; _rR = onR; _rT = onT; _rB = onB;
                e.Use();
            }
            else if (_resizing && e.type == EventType.MouseDrag)
            {
                int xMin = _sel.xMin, xMax = _sel.xMax, yMin = _sel.yMin, yMax = _sel.yMax;
                if (_rL) xMin = Mathf.Min(texPos.x, xMax - 8);
                if (_rR) xMax = Mathf.Max(texPos.x + 1, xMin + 8);
                // GUI-верх рамки = верх текстуры (yMax), GUI-низ = yMin
                if (_rT) yMax = Mathf.Max(texPos.y + 1, yMin + 8);
                if (_rB) yMin = Mathf.Min(texPos.y, yMax - 8);
                _sel = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
                e.Use(); Repaint();
            }
            else if (_resizing && e.type == EventType.MouseUp)
            {
                _resizing = false;
                _rL = _rR = _rT = _rB = false;
                ComputeProfiles();
                BuildGrids();
                _dirty = true;
                e.Use(); Repaint();
            }
        }

        /// <summary>Рисуем реальные (неравномерные) границы сетки.</summary>
        private void DrawGridOverlay(Rect selGui)
        {
            if (_cellX * _viewZoom < 3f || _cellY * _viewZoom < 3f) return;
            var c = new Color(1f, .8f, .3f, 0.35f);
            foreach (var x in _gridX)
                EditorGUI.DrawRect(new Rect(selGui.x + x * _viewZoom, selGui.y, 1, selGui.height), c);
            foreach (var y in _gridY)
                EditorGUI.DrawRect(new Rect(selGui.x, selGui.yMax - y * _viewZoom, selGui.width, 1), c);
        }

        // ────────────────────────── правая панель ──────────────────────────

        private void DrawSidePanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            if (!_hasSel)
            {
                EditorGUILayout.HelpBox("Выдели иконку на листе — шаг клетки определится сам, " +
                    "потом подстрой при необходимости.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField($"Выделение: {_sel.width}×{_sel.height}px", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _cellX = CellField("Клетка X, px", _cellX);
            _cellY = CellField("Клетка Y, px", _cellY);
            bool cellChanged = EditorGUI.EndChangeCheck();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Автоопределить")) { AutoDetect(); _dirty = true; }
            if (GUILayout.Button(new GUIContent("Перестроить сетку",
                "Обход клеток заново с текущим шагом-затравкой")))
            { BuildGrids(); _dirty = true; }
            EditorGUILayout.EndHorizontal();

            if (cellChanged) { BuildGrids(); _dirty = true; }

            int cols = Mathf.Max(0, _gridX.Count - 1), rows = Mathf.Max(0, _gridY.Count - 1);
            EditorGUILayout.LabelField("Результат", $"{cols} × {rows} px", EditorStyles.boldLabel);

            // Превью — БЕЗ выреза фона: фон убирается только при сохранении
            if (_dirty) { RebuildResult(); _dirty = false; }
            if (_result != null)
            {
                float scale = Mathf.Max(1f, Mathf.Floor(280f / Mathf.Max(_result.width, _result.height)));
                var pr = GUILayoutUtility.GetRect(_result.width * scale, _result.height * scale,
                    GUILayout.Width(_result.width * scale));
                EditorGUI.DrawTextureTransparent(pr, _result, ScaleMode.ScaleToFit);
                EditorGUILayout.LabelField($"превью ×{scale:0} (фон не вырезан)", EditorStyles.miniLabel);
            }

            GUILayout.Space(6);
            EditorGUILayout.LabelField("Финальный шаг (при сохранении)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _bgMode = (BgCutMode)EditorGUILayout.Popup("Вырез фона", (int)_bgMode, new[]
            {
                "Не вырезать",
                "Только снаружи (заливка от краёв)",
                "По цвету везде",
            });
            if (_bgMode != BgCutMode.None)
                _bgThreshold = EditorGUILayout.IntSlider("Порог фона", _bgThreshold, 4, 96);
            if (EditorGUI.EndChangeCheck()) RebuildCutPreview();

            if (_resultCut != null)
            {
                float scale2 = Mathf.Max(1f, Mathf.Floor(280f / Mathf.Max(_resultCut.width, _resultCut.height)));
                var pr2 = GUILayoutUtility.GetRect(_resultCut.width * scale2, _resultCut.height * scale2,
                    GUILayout.Width(_resultCut.width * scale2));
                EditorGUI.DrawTextureTransparent(pr2, _resultCut, ScaleMode.ScaleToFit);
                EditorGUILayout.LabelField($"превью ×{scale2:0} (фон вырезан)", EditorStyles.miniLabel);
            }

            // Сущность-получатель
            var items = _col.GetItems?.Invoke();
            if (items != null && items.Count > 0)
            {
                var names = new string[items.Count];
                for (int i = 0; i < items.Count; i++) names[i] = items[i].DisplayName;
                _entityIndex = EditorGUILayout.Popup("Сущность", Mathf.Clamp(_entityIndex, 0, items.Count - 1), names);

                using (new EditorGUI.DisabledScope(_result == null || cols == 0 || rows == 0))
                {
                    if (GUILayout.Button($"Создать вариант {cols}×{rows}", GUILayout.Height(28)))
                        CreateVariant(items[_entityIndex]);
                }
            }
            else EditorGUILayout.HelpBox("В наборе нет сущностей.", MessageType.Warning);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private static float CellField(string label, float v)
        {
            EditorGUILayout.BeginHorizontal();
            v = EditorGUILayout.FloatField(label, v);
            if (GUILayout.Button("−", GUILayout.Width(24))) v -= 0.1f;
            if (GUILayout.Button("+", GUILayout.Width(24))) v += 0.1f;
            EditorGUILayout.EndHorizontal();
            return Mathf.Clamp(v, 2f, 256f);
        }

        // ────────────────────────── семплинг ──────────────────────────

        /// <summary>Честный пиксель-арт: цвет — из центра каждой клетки адаптивной сетки.</summary>
        private void RebuildResult()
        {
            if (_result != null) DestroyImmediate(_result);
            _result = null;
            int cols = _gridX.Count - 1, rows = _gridY.Count - 1;
            if (cols < 1 || rows < 1) return;

            _result = new Texture2D(cols, rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color[cols * rows];
            for (int gy = 0; gy < rows; gy++)
                for (int gx = 0; gx < cols; gx++)
                {
                    float cx = _sel.x + (_gridX[gx] + _gridX[gx + 1]) * 0.5f;
                    float cy = _sel.y + (_gridY[gy] + _gridY[gy + 1]) * 0.5f;
                    px[gy * cols + gx] = _sheet.GetPixel(
                        Mathf.Clamp((int)cx, _sel.x, _sel.xMax - 1),
                        Mathf.Clamp((int)cy, _sel.y, _sel.yMax - 1));
                }
            _result.SetPixels(px);
            _result.Apply();
            RebuildCutPreview();
        }

        /// <summary>Второе превью: тот же результат после выреза фона.</summary>
        private void RebuildCutPreview()
        {
            if (_resultCut != null) DestroyImmediate(_resultCut);
            _resultCut = null;
            if (_bgMode == BgCutMode.None || _result == null || _result.width < 2 || _result.height < 2) return;

            var px = _result.GetPixels();
            ApplyBgCut(px, _result.width, _result.height);
            _resultCut = new Texture2D(_result.width, _result.height, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point };
            _resultCut.SetPixels(px);
            _resultCut.Apply();
        }

        /// <summary>
        /// Фон в прозрачность. Цвет фона — медиана четырёх углов. Everywhere — по цвету
        /// по всей картинке; Outer — только пиксели, ДОСТИЖИМЫЕ от края по фоновому цвету
        /// (заливка): похожие точки внутри иконки остаются.
        /// </summary>
        private void ApplyBgCut(Color[] px, int cols, int rows)
        {
            var corners = new[] { px[0], px[cols - 1], px[(rows - 1) * cols], px[rows * cols - 1] };
            var bg = MedianColor(corners);
            float thr = _bgThreshold / 255f * 3f;

            bool IsBg(int i)
            {
                var c = px[i];
                return Mathf.Abs(c.r - bg.r) + Mathf.Abs(c.g - bg.g) + Mathf.Abs(c.b - bg.b) < thr;
            }

            if (_bgMode == BgCutMode.Everywhere)
            {
                for (int i = 0; i < px.Length; i++)
                    if (IsBg(i)) px[i] = Color.clear;
                return;
            }

            // Outer: BFS от всех фоновых пикселей на границе кадра
            var visited = new bool[px.Length];
            var queue = new Queue<int>();
            void Seed(int i) { if (!visited[i] && IsBg(i)) { visited[i] = true; queue.Enqueue(i); } }
            for (int x = 0; x < cols; x++) { Seed(x); Seed((rows - 1) * cols + x); }
            for (int y = 0; y < rows; y++) { Seed(y * cols); Seed(y * cols + cols - 1); }

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % cols, y = i / cols;
                if (x > 0) Seed(i - 1);
                if (x < cols - 1) Seed(i + 1);
                if (y > 0) Seed(i - cols);
                if (y < rows - 1) Seed(i + cols);
            }

            for (int i = 0; i < px.Length; i++)
                if (visited[i]) px[i] = Color.clear;
        }

        // ────────────────────────── автоопределение и адаптивная сетка ──────────────────────────

        /// <summary>
        /// 1) профили перепадов по X/Y; 2) средний шаг — МЕДИАНА расстояний между пиками
        /// (устойчива на больших областях, где периодограмму размывают дрейф и гармоники);
        /// 3) адаптивный обход BuildGrids.
        /// </summary>
        private void AutoDetect()
        {
            if (!ComputeProfiles()) return;
            _cellX = EstimateStep(_profX, _cellX);
            _cellY = EstimateStep(_profY, _cellY);
            BuildGrids();
            _dirty = true;
        }

        /// <summary>Профили перепадов текущего выделения (общие для детекции и ресайза рамки).</summary>
        private bool ComputeProfiles()
        {
            int w = _sel.width, h = _sel.height;
            if (w < 8 || h < 8) return false;

            var pix = _sheet.GetPixels(_sel.x, _sel.y, w, h);
            float[] lum = new float[w * h];
            for (int i = 0; i < pix.Length; i++)
                lum[i] = (pix[i].r * 0.3f + pix[i].g * 0.59f + pix[i].b * 0.11f) * pix[i].a;

            _profX = new float[w];
            for (int x = 1; x < w; x++)
            {
                float s = 0;
                for (int y = 0; y < h; y++) s += Mathf.Abs(lum[y * w + x] - lum[y * w + x - 1]);
                _profX[x] = s;
            }
            _profY = new float[h];
            for (int y = 1; y < h; y++)
            {
                float s = 0;
                for (int x = 0; x < w; x++) s += Mathf.Abs(lum[y * w + x] - lum[(y - 1) * w + x]);
                _profY[y] = s;
            }
            return true;
        }

        /// <summary>Средний шаг клетки: медиана расстояний между соседними пиками профиля.</summary>
        private static float EstimateStep(float[] prof, float fallback)
        {
            var peaks = FindPeaks(prof);
            if (peaks.Count < 3) return fallback;

            var deltas = new List<float>();
            for (int i = 1; i < peaks.Count; i++)
                deltas.Add(peaks[i] - peaks[i - 1]);
            deltas.Sort();
            float median = deltas[deltas.Count / 2];

            // сетка может «мигать» через пик (пропуски) — половинки/двойники приводим к медиане
            var refined = new List<float>();
            foreach (var d in deltas)
            {
                if (d > median * 1.6f && d < median * 2.4f) refined.Add(d * 0.5f);
                else if (d > median * 0.6f && d < median * 1.4f) refined.Add(d);
            }
            if (refined.Count == 0) return Mathf.Max(2f, median);
            refined.Sort();
            return Mathf.Max(2f, refined[refined.Count / 2]);
        }

        /// <summary>Локальные максимумы профиля с порогом (шумовые не берём).</summary>
        private static List<int> FindPeaks(float[] prof)
        {
            float max = 0, mean = 0;
            for (int i = 1; i < prof.Length; i++) { max = Mathf.Max(max, prof[i]); mean += prof[i]; }
            mean /= Mathf.Max(1, prof.Length - 1);
            float thr = Mathf.Max(mean * 1.2f, max * 0.15f);

            var peaks = new List<int>();
            for (int i = 2; i < prof.Length - 1; i++)
                if (prof[i] >= thr && prof[i] >= prof[i - 1] && prof[i] >= prof[i + 1])
                {
                    // плато из равных значений не плодит пики
                    if (peaks.Count > 0 && i - peaks[peaks.Count - 1] <= 1) continue;
                    peaks.Add(i);
                }
            return peaks;
        }

        /// <summary>
        /// Адаптивный обход: от якоря (сильнейший пик) шагаем в обе стороны; каждую следующую
        /// границу ищем ЛОКАЛЬНО (пик в окне ±35% шага вокруг ожидаемой позиции), нет пика —
        /// продолжаем ожидаемым шагом. Дрейф сетки и неквадратность так не накапливаются.
        /// </summary>
        private void BuildGrids()
        {
            if (_profX == null || _profY == null) return;
            BuildAxis(_profX, _cellX, _sel.width, _gridX);
            BuildAxis(_profY, _cellY, _sel.height, _gridY);
        }

        private static void BuildAxis(float[] prof, float step, int length, List<float> grid)
        {
            grid.Clear();
            if (step < 2f || length < step) return;

            // Якорь — сильнейший пик; нет пиков — от нуля
            var peaks = FindPeaks(prof);
            float anchor = 0f;
            if (peaks.Count > 0)
            {
                int best = peaks[0];
                foreach (var p in peaks) if (prof[p] > prof[best]) best = p;
                anchor = best;
            }

            float win = step * 0.35f;

            // вправо от якоря
            var right = new List<float>();
            float pos = anchor;
            while (pos <= length - step * 0.5f)
            {
                float next = SnapToPeak(prof, pos + step, win);
                right.Add(next);
                pos = next;
            }
            // влево от якоря
            var left = new List<float>();
            pos = anchor;
            while (pos >= step * 0.5f)
            {
                float prev = SnapToPeak(prof, pos - step, win);
                left.Add(prev);
                pos = prev;
            }
            left.Reverse();

            // края выделения замыкают крайние (возможно неполные) клетки
            if (left.Count == 0 || left[0] > step * 0.4f) grid.Add(0f);
            grid.AddRange(left);
            grid.Add(anchor);
            grid.AddRange(right);
            if (length - grid[grid.Count - 1] > step * 0.4f) grid.Add(length);

            // строго возрастающая последовательность без слипшихся границ
            grid.Sort();
            for (int i = grid.Count - 1; i > 0; i--)
                if (grid[i] - grid[i - 1] < 1f) grid.RemoveAt(i);
        }

        /// <summary>Пик профиля в окне ±win вокруг expected; слабый или нет — сама expected.</summary>
        private static float SnapToPeak(float[] prof, float expected, float win)
        {
            int lo = Mathf.Max(1, Mathf.FloorToInt(expected - win));
            int hi = Mathf.Min(prof.Length - 1, Mathf.CeilToInt(expected + win));
            if (lo >= hi) return expected;

            int best = -1;
            for (int i = lo; i <= hi; i++)
                if (best < 0 || prof[i] > prof[best]) best = i;

            // порог: пик должен заметно выделяться на фоне окна, иначе это ровная заливка
            float mean = 0; int n = 0;
            for (int i = lo; i <= hi; i++) { mean += prof[i]; n++; }
            mean /= Mathf.Max(1, n);
            return prof[best] > mean * 1.3f ? best : expected;
        }

        // ────────────────────────── сохранение варианта ──────────────────────────

        private void CreateVariant(IconItem item)
        {
            // Вырез фона — финальный шаг; ровно то, что показывает второе превью
            var final = new Texture2D(_result.width, _result.height, TextureFormat.RGBA32, false);
            var px = _result.GetPixels();
            if (_bgMode != BgCutMode.None && _result.width > 1 && _result.height > 1)
                ApplyBgCut(px, _result.width, _result.height);
            final.SetPixels(px);
            final.Apply();

            string variantId = IconVariantStore.NewVariantId();
            string path = IconVariantStore.VariantPngPath(_col.PngFolder, item.EntityId, variantId);

            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                path.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            File.WriteAllBytes(abs, final.EncodeToPNG());
            DestroyImmediate(final);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ClipboardImageImporter.ConfigureAsSprite(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter imp)
            {
                imp.filterMode = FilterMode.Point;       // пиксель-арт — без мыла
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.maxTextureSize = 256;
                imp.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) { Debug.LogError("[Пиксель-арт] Спрайт не импортировался: " + path); return; }

            var result = new IconGenResult
            {
                Subject = item.GetPrompt?.Invoke() ?? "",
                StyleName = "(пиксель-арт из буфера)",
                PixelGrid = _result.width,
                PngAssetPath = path,
                GeneratedAtUtc = System.DateTime.UtcNow.ToString("o"),
            };
            if (item.UndoTarget != null) Undo.RecordObject(item.UndoTarget, "Pixel art paste");
            IconVariantStore.AddVariant(item.EntityId, variantId, sprite, result);
            item.SetSprite?.Invoke(sprite);
            ShowNotification(new GUIContent($"{item.DisplayName}: вариант {_result.width}×{_result.height}"));
        }

        private static Color MedianColor(Color[] cs)
        {
            float Med(System.Func<Color, float> f)
            {
                var v = new List<float>(); foreach (var c in cs) v.Add(f(c));
                v.Sort(); return (v[1] + v[2]) * 0.5f;
            }
            return new Color(Med(c => c.r), Med(c => c.g), Med(c => c.b), 1f);
        }

        // ────────────────────────── координаты ──────────────────────────

        private Vector2Int GuiToTex(Rect drawRect, Vector2 gui)
        {
            int tx = Mathf.Clamp(Mathf.FloorToInt((gui.x - drawRect.x) / _viewZoom), 0, _sheet.width - 1);
            int ty = Mathf.Clamp(_sheet.height - 1 - Mathf.FloorToInt((gui.y - drawRect.y) / _viewZoom), 0, _sheet.height - 1);
            return new Vector2Int(tx, ty);
        }

        private Rect TexToGui(Rect drawRect, RectInt sel)
        {
            float x = drawRect.x + sel.x * _viewZoom;
            float y = drawRect.y + (_sheet.height - sel.yMax) * _viewZoom;
            return new Rect(x, y, sel.width * _viewZoom, sel.height * _viewZoom);
        }

        private static RectInt FromCorners(Vector2Int a, Vector2Int b)
        {
            int xMin = Mathf.Min(a.x, b.x), yMin = Mathf.Min(a.y, b.y);
            return new RectInt(xMin, yMin, Mathf.Abs(a.x - b.x) + 1, Mathf.Abs(a.y - b.y) + 1);
        }

        private static void DrawFrame(Rect r, Color c, int wpx)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, wpx), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - wpx, r.width, wpx), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, wpx, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - wpx, r.y, wpx, r.height), c);
        }
    }
}
