using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProtoSystem.IconGen.Editor;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Обобщённый атлас-пайплайн для ЛЮБОГО набора студии (ресурсы, специалисты, эффекты,
    /// здания…): промпт спрайт-листа → внешний генератор → картинка в буфере → нарезка по
    /// сетке и раскладка по сущностям (опционально rembg-вырез фона). Нарезки попадают в
    /// систему вариантов (лента пикера), становятся активными.
    ///
    /// Работает через абстракцию IconCollection: GetItems/SetSprite/StorageSize/PngFolder —
    /// поэтому не привязан к зданиям (в отличие от BuildingAtlasTools, который остаётся для
    /// дефолтных промптов зданий).
    /// </summary>
    public static class AtlasTools
    {
        /// <summary>Как убирать фон у нарезанной ячейки.</summary>
        public enum BgMode
        {
            /// <summary>Не трогать — фон уже прозрачный или нужен.</summary>
            None,
            /// <summary>Заливка от краёв: убрать только фон, связный с границей кадра.
            /// Внутренние детали силуэта НЕ трогаются (в отличие от rembg).</summary>
            EdgeFlood,
            /// <summary>rembg (ИИ-сегментация) — для сложного/неоднородного фона.</summary>
            Rembg,
        }

        /// <summary>Доступные модели rembg (для дропдауна в окне нарезки).</summary>
        public static readonly string[] RembgModels =
        {
            "isnet-general-use", "birefnet-general", "u2net", "silueta", "isnet-anime",
        };

        /// <summary>
        /// Подобрать число колонок под плотную сетку: минимум пустых ячеек в последнем
        /// ряду, при равенстве — ближе к квадрату. 4→2×2, 6→2×3, 16→4×4 (а не жёсткие
        /// 4 колонки с пустотами, на которых генератор сбивается).
        /// </summary>
        private static int PickColumns(int count)
        {
            int best = count, bestWaste = int.MaxValue, bestSquare = int.MaxValue;
            for (int cols = 1; cols <= count; cols++)
            {
                int rows = Mathf.CeilToInt(count / (float)cols);
                int waste = cols * rows - count;
                int square = Mathf.Abs(cols - rows);
                if (waste < bestWaste || (waste == bestWaste && square < bestSquare))
                {
                    bestWaste = waste; bestSquare = square; best = cols;
                }
            }
            return best;
        }

        private const string TransparencyPrompt =
            "Fully transparent background (PNG with alpha): each subject cut out and isolated, " +
            "no backdrop, no ground, no shadow, no checkerboard drawn as pixels.";

        // ── Промпт атласа → буфер ──

        public static void CopyAtlasPrompt(IconCollection col)
        {
            if (col == null) return;
            var items = col.GetItems?.Invoke();
            if (items == null || items.Count == 0) return;

            EditorGUIUtility.systemCopyBuffer = BuildAtlasPrompt(col, items);
            Debug.Log($"[Атлас] Промпт «{col.Title}» ({items.Count} шт.) скопирован в буфер.");
        }

        private static string BuildAtlasPrompt(IconCollection col, List<IconItem> items)
        {
            int columns = PickColumns(items.Count);
            int rows = Mathf.CeilToInt(items.Count / (float)columns);

            var cell = col.StorageSize;
            int scale = Mathf.Max(1, 512 / Mathf.Max(1, Mathf.Max(cell.x, cell.y)));   // ячейка ~512px
            int cw = cell.x * scale, ch = cell.y * scale;
            int sheetW = columns * cw, sheetH = rows * ch;
            string cellShape = cell.x == cell.y ? "a square" : $"a {cell.x}:{cell.y} rectangle";

            var sb = new StringBuilder();
            sb.AppendLine($"Sprite sheet: «{col.Title}» — {items.Count} game icons.");
            sb.AppendLine();
            sb.AppendLine($"Layout: strict grid {columns} columns × {rows} rows, {items.Count} cells. " +
                          $"Canvas exactly {sheetW}×{sheetH} px. Every cell {cw}×{ch} px ({cellShape}), " +
                          "all identical size. The grid fills the whole canvas — outer cells touch the edges. " +
                          "No outer margin, no dividing lines, frames, gutters, captions or numbers.");
            sb.AppendLine();
            sb.AppendLine("Each subject centred in its own cell, fully contained, identical style, " +
                          "scale and lighting across all cells — they are one set.");
            sb.AppendLine();
            sb.AppendLine($"Important: in game each cell is shown at only {cell.x}×{cell.y} px — keep shapes " +
                          "bold and readable at that size, fine detail is wasted.");
            sb.AppendLine();
            sb.AppendLine("Cells, left to right, top to bottom:");
            for (int i = 0; i < items.Count; i++)
            {
                string subject = items[i].GetPrompt?.Invoke();
                if (string.IsNullOrWhiteSpace(subject)) subject = "(промпт не задан)";
                sb.AppendLine($"{i + 1}. {items[i].DisplayName} — {subject}");
            }
            sb.AppendLine();
            sb.AppendLine($"Background: {TransparencyPrompt}");
            return sb.ToString();
        }

        // ── Нарезка атласа из буфера → варианты сущностей ──

        /// <summary>Взять картинку из буфера и открыть окно настроек нарезки.</summary>
        public static void OpenSliceWindow(IconCollection col)
        {
            if (col == null) return;
            var items = col.GetItems?.Invoke();
            if (items == null || items.Count == 0) return;

            var atlas = ClipboardImageImporter.GrabClipboardTexture();
            if (atlas == null)
            {
                EditorUtility.DisplayDialog("Атлас", "В буфере обмена нет картинки.", "Ок");
                return;
            }
            AtlasSliceWindow.Open(col, atlas);
        }

        /// <summary>Сколько иконок находит умная нарезка (для превью в окне). -1 — не удалось.</summary>
        public static int CountSmart(Texture2D atlas)
        {
            var r = SmartSlice(atlas, out int found);
            return r == null ? -1 : found;
        }

        /// <summary>
        /// Выполнить нарезку. smart — по границам объектов (если нашла ровно items.Count),
        /// иначе сетка columns колонок. bgMode — как убирать фон; edgeThreshold — порог для
        /// EdgeFlood; rembgModel — модель для Rembg (пусто = из настроек).
        /// </summary>
        public static void Slice(IconCollection col, Texture2D atlas, bool smart, int columns,
                                 BgMode bgMode, int edgeThreshold, string rembgModel)
        {
            var items = col.GetItems?.Invoke();
            if (items == null || items.Count == 0) return;

            var cell = col.StorageSize;
            float wantAspect = cell.y > 0 ? cell.x / (float)cell.y : 1f;

            List<RectInt> regions = null;
            bool useSmart = false;
            if (smart)
            {
                regions = SmartSlice(atlas, out int found);
                useSmart = regions != null && found == items.Count;
            }
            if (!useSmart)
                regions = GridRegions(atlas, items.Count, columns, wantAspect);

            // rembg берёт модель из настроек — временно подменяем на выбранную
            string savedModel = AiToolsSettings.RembgModel;
            if (bgMode == BgMode.Rembg && !string.IsNullOrEmpty(rembgModel))
                AiToolsSettings.RembgModel = rembgModel;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (bgMode != BgMode.None)
                    EditorUtility.DisplayProgressBar("Нарезка",
                        $"{i + 1}/{items.Count}: {item.DisplayName}", i / (float)items.Count);

                var region = regions[i];   // умная — bbox объекта; сетка — уже под аспект
                string variantId = IconVariantStore.NewVariantId();
                string path = IconVariantStore.VariantPngPath(col.PngFolder, item.EntityId, variantId);

                var sprite = ClipboardImageImporter.SaveRegionAsSprite(atlas, region, path);
                if (sprite == null) continue;

                switch (bgMode)
                {
                    case BgMode.EdgeFlood:
                        sprite = EdgeFloodCutout(path, cell, edgeThreshold) ?? sprite;
                        break;
                    case BgMode.Rembg:
                        sprite = RembgProcessor.ProcessAssetInPlace(path, cell.x, cell.y) ?? sprite;
                        break;
                }

                var result = new IconGenResult
                {
                    Subject = item.GetPrompt?.Invoke() ?? "",
                    StyleName = "(атлас из буфера)",
                    PngAssetPath = path,
                    GeneratedAtUtc = System.DateTime.UtcNow.ToString("o"),
                };
                if (item.UndoTarget != null) Undo.RecordObject(item.UndoTarget, "Paste atlas");
                IconVariantStore.AddVariant(item.EntityId, variantId, sprite, result);
                item.SetSprite?.Invoke(sprite);
            }

            AiToolsSettings.RembgModel = savedModel;
            EditorUtility.ClearProgressBar();
            Debug.Log($"[Атлас] Нарезан «{col.Title}»: {items.Count} сущностей " +
                      $"({(useSmart ? "по границам" : $"сетка {columns} кол.")}, фон: {bgMode}).");
        }

        /// <summary>
        /// Убрать фон заливкой от краёв: помечаем пиксели, достижимые от границы кадра по
        /// цвету фона (в пределах threshold), делаем их прозрачными. Внутренние области силуэта
        /// не трогаются — даже если их цвет похож на фон, они не связаны с краем. Затем вписать
        /// в storage (rembg --no-bg делает fill_holes+fit; иначе импортёр по maxSide).
        /// </summary>
        private static Sprite EdgeFloodCutout(string assetPath, Vector2Int storage, int threshold)
        {
            string abs = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                Application.dataPath, "..", assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            if (!System.IO.File.Exists(abs)) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(abs))) { Object.DestroyImmediate(tex); return null; }

            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            var bg = px[0];   // угол кадра

            var mark = new bool[w * h];
            var st = new Stack<int>();
            void Seed(int i) { if (!mark[i] && SimilarT(px[i], bg, threshold)) { mark[i] = true; st.Push(i); } }
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            while (st.Count > 0)
            {
                int idx = st.Pop(); int cx = idx % w, cy = idx / w;
                void Try(int nx, int ny)
                {
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) return;
                    int ni = ny * w + nx;
                    if (!mark[ni] && SimilarT(px[ni], bg, threshold)) { mark[ni] = true; st.Push(ni); }
                }
                Try(cx - 1, cy); Try(cx + 1, cy); Try(cx, cy - 1); Try(cx, cy + 1);
            }
            for (int i = 0; i < px.Length; i++) if (mark[i]) px[i] = new Color32(px[i].r, px[i].g, px[i].b, 0);

            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(abs, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            // Вписать в storage: rembg --no-bg (fill_holes + autocrop + fit) без ИИ-выреза
            if (RembgProcessor.IsAvailable)
                return RembgProcessor.ProcessAssetInPlace(assetPath, storage.x, storage.y, noBg: true);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ClipboardImageImporter.ConfigureAsSprite(assetPath,
                new ClipboardImageImporter.ImportOptions { maxSize = Mathf.Max(storage.x, storage.y) });
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static bool SimilarT(Color32 a, Color32 b, int t)
        {
            if (a.a < 12 && b.a < 12) return true;
            if (Mathf.Abs(a.a - b.a) > t) return false;
            return Mathf.Abs(a.r - b.r) <= t && Mathf.Abs(a.g - b.g) <= t && Mathf.Abs(a.b - b.b) <= t;
        }

        /// <summary>Регионы фиксированной сетки columns колонок, подрезанные под аспект.</summary>
        private static List<RectInt> GridRegions(Texture2D atlas, int count, int columns, float wantAspect)
        {
            columns = Mathf.Clamp(columns, 1, count);
            int rows = Mathf.CeilToInt(count / (float)columns);
            var sheet = TrimUniformBorder(atlas);
            int cellW = sheet.width / columns, cellH = sheet.height / rows;
            var regions = new List<RectInt>();
            for (int i = 0; i < count; i++)
            {
                int c = i % columns, r = i / columns;
                int y = sheet.yMax - (r + 1) * cellH;   // ячейки нумеруются сверху
                regions.Add(FitAspect(new RectInt(sheet.x + c * cellW, y, cellW, cellH), wantAspect));
            }
            return regions;
        }

        /// <summary>Плотное число колонок по количеству (для дефолта в окне).</summary>
        public static int DefaultColumns(int count) => PickColumns(count);

        // ── Умная нарезка: границы иконок по «островам» непрозрачности ──

        /// <summary>
        /// Найти bbox каждой иконки как связную область непрозрачности (connected-components):
        /// на прозрачном фоне каждая иконка — свой «остров». Точнее фиксированной сетки, когда
        /// генератор разложил иконки неровно. Возвращает регионы в порядке чтения (сверху вниз,
        /// слева направо) в координатах текстуры (y снизу) — или null, если детекция не удалась.
        /// </summary>
        private static List<RectInt> SmartSlice(Texture2D atlas, out int found)
        {
            found = 0;
            int W = atlas.width, H = atlas.height;
            var px = atlas.GetPixels32();   // row 0 = низ

            // Маска объекта. Есть прозрачность → по альфе; иначе фон = цвет углов.
            bool hasAlpha = false;
            for (int i = 0; i < px.Length && !hasAlpha; i++) if (px[i].a < 250) hasAlpha = true;
            Color32 bg = px[0];

            bool Obj(int x, int yTop)
            {
                var p = px[(H - 1 - yTop) * W + x];   // top-down → bottom-up индекс
                return hasAlpha ? p.a > 40 : !Similar(p, bg);
            }

            // Уменьшенная маска для быстрого CCL (границы огрубляются, но их хватает)
            int md = Mathf.Max(W, H);
            int ds = Mathf.Max(1, Mathf.CeilToInt(md / 400f));
            int mw = W / ds, mh = H / ds;
            if (mw < 2 || mh < 2) { return null; }
            var mask = new bool[mw * mh];
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                    mask[y * mw + x] = Obj(Mathf.Min(W - 1, x * ds + ds / 2),
                                           Mathf.Min(H - 1, y * ds + ds / 2));

            // CCL (BFS 4-связность) → bbox островов в координатах маски (top-down)
            var seen = new bool[mw * mh];
            var boxes = new List<RectInt>();
            var stack = new Stack<int>();
            for (int s = 0; s < mask.Length; s++)
            {
                if (!mask[s] || seen[s]) continue;
                int minx = mw, miny = mh, maxx = 0, maxy = 0, area = 0;
                stack.Push(s); seen[s] = true;
                while (stack.Count > 0)
                {
                    int idx = stack.Pop();
                    int cx = idx % mw, cy = idx / mw;
                    area++;
                    if (cx < minx) minx = cx; if (cx > maxx) maxx = cx;
                    if (cy < miny) miny = cy; if (cy > maxy) maxy = cy;
                    void Try(int nx, int ny)
                    {
                        if (nx < 0 || ny < 0 || nx >= mw || ny >= mh) return;
                        int ni = ny * mw + nx;
                        if (mask[ni] && !seen[ni]) { seen[ni] = true; stack.Push(ni); }
                    }
                    Try(cx - 1, cy); Try(cx + 1, cy); Try(cx, cy - 1); Try(cx, cy + 1);
                }
                boxes.Add(new RectInt(minx, miny, maxx - minx + 1, maxy - miny + 1));
            }
            if (boxes.Count == 0) return null;

            // Слить близкие/пересекающиеся острова (части одной иконки: антенна+корпус)
            int gap = Mathf.Max(2, Mathf.RoundToInt(Mathf.Max(mw, mh) * 0.02f));
            MergeClose(boxes, gap);

            // Отбросить шум: острова заметно мельче медианы площади
            boxes.Sort((a, b) => (a.width * a.height).CompareTo(b.width * b.height));
            int medArea = boxes[boxes.Count / 2].width * boxes[boxes.Count / 2].height;
            boxes.RemoveAll(b => b.width * b.height < medArea * 0.15f);
            if (boxes.Count == 0) return null;

            // Порядок чтения: ряды по Y (толеранс = медианная высота), внутри ряда по X
            var heights = new List<int>(); foreach (var b in boxes) heights.Add(b.height);
            heights.Sort();
            int medH = heights[heights.Count / 2];
            boxes.Sort((a, b) => (a.y + a.height / 2).CompareTo(b.y + b.height / 2));
            var ordered = new List<RectInt>();
            int r0 = 0;
            while (r0 < boxes.Count)
            {
                int rowY = boxes[r0].y + boxes[r0].height / 2;
                var rowItems = new List<RectInt>();
                int r1 = r0;
                while (r1 < boxes.Count && (boxes[r1].y + boxes[r1].height / 2) - rowY <= medH * 0.6f)
                    rowItems.Add(boxes[r1++]);
                rowItems.Sort((a, b) => a.x.CompareTo(b.x));
                ordered.AddRange(rowItems);
                r0 = r1;
            }

            // Масштабировать в полный размер + перевести в координаты текстуры (y снизу)
            var result = new List<RectInt>();
            foreach (var b in ordered)
            {
                int x = b.x * ds, yTop = b.y * ds, w = b.width * ds, hgt = b.height * ds;
                x = Mathf.Clamp(x, 0, W - 1);
                w = Mathf.Clamp(w, 1, W - x);
                yTop = Mathf.Clamp(yTop, 0, H - 1);
                hgt = Mathf.Clamp(hgt, 1, H - yTop);
                int texY = H - (yTop + hgt);   // top-down → bottom-up
                result.Add(new RectInt(x, Mathf.Max(0, texY), w, hgt));
            }

            found = result.Count;
            return result;
        }

        /// <summary>Слить bbox'ы, чьи расширенные на gap рамки пересекаются (итеративно).</summary>
        private static void MergeClose(List<RectInt> boxes, int gap)
        {
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < boxes.Count && !merged; i++)
                for (int j = i + 1; j < boxes.Count && !merged; j++)
                {
                    var a = boxes[i]; var b = boxes[j];
                    bool overlap = a.xMin - gap < b.xMax && b.xMin - gap < a.xMax &&
                                   a.yMin - gap < b.yMax && b.yMin - gap < a.yMax;
                    if (!overlap) continue;
                    int x0 = Mathf.Min(a.xMin, b.xMin), y0 = Mathf.Min(a.yMin, b.yMin);
                    int x1 = Mathf.Max(a.xMax, b.xMax), y1 = Mathf.Max(a.yMax, b.yMax);
                    boxes[i] = new RectInt(x0, y0, x1 - x0, y1 - y0);
                    boxes.RemoveAt(j);
                    merged = true;
                }
            }
        }

        // ── Помощники нарезки (перенос из BuildingAtlasTools) ──

        private static RectInt TrimUniformBorder(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            int w = texture.width, h = texture.height;
            var bg = pixels[0];
            int left = 0, right = w - 1, bottom = 0, top = h - 1;

            while (left < right && ColumnIsBg(pixels, w, h, left, bg)) left++;
            while (right > left && ColumnIsBg(pixels, w, h, right, bg)) right--;
            while (bottom < top && RowIsBg(pixels, w, bottom, bg)) bottom++;
            while (top > bottom && RowIsBg(pixels, w, top, bg)) top--;

            var rect = new RectInt(left, bottom, right - left + 1, top - bottom + 1);
            if (rect.width < w / 2 || rect.height < h / 2) return new RectInt(0, 0, w, h);
            return rect;
        }

        private static bool ColumnIsBg(Color32[] px, int w, int h, int x, Color32 bg)
        {
            for (int y = 0; y < h; y++) if (!Similar(px[y * w + x], bg)) return false;
            return true;
        }

        private static bool RowIsBg(Color32[] px, int w, int y, Color32 bg)
        {
            int off = y * w;
            for (int x = 0; x < w; x++) if (!Similar(px[off + x], bg)) return false;
            return true;
        }

        private static bool Similar(Color32 a, Color32 b)
        {
            if (a.a < 12 && b.a < 12) return true;
            if (Mathf.Abs(a.a - b.a) > 12) return false;
            return Mathf.Abs(a.r - b.r) <= 12 && Mathf.Abs(a.g - b.g) <= 12 && Mathf.Abs(a.b - b.b) <= 12;
        }

        private static RectInt FitAspect(RectInt cell, float wantAspect)
        {
            float got = cell.height > 0 ? cell.width / (float)cell.height : 1f;
            if (Mathf.Abs(got - wantAspect) < 0.02f) return cell;
            if (got > wantAspect)
            {
                int width = Mathf.RoundToInt(cell.height * wantAspect);
                return new RectInt(cell.x + (cell.width - width) / 2, cell.y, width, cell.height);
            }
            int height = Mathf.RoundToInt(cell.width / wantAspect);
            return new RectInt(cell.x, cell.y + (cell.height - height) / 2, cell.width, height);
        }
    }
}
