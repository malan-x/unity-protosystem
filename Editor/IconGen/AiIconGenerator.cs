using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Неблокирующая очередь генерации иконок через локальный ComfyUI. Сама генерация идёт в Task,
    /// а импорт PNG и назначение спрайта — на главном потоке в EditorApplication.update
    /// (AssetDatabase доступен только там). Генерация небыстрая, поэтому морозить редактор нельзя.
    ///
    /// Не привязан к сущности: что генерировать и куда положить результат — в IconGenRequest.
    /// Замена прежнего ComfyIconGenerator&lt;BaseBuildingConfig&gt;.
    /// </summary>
    public static class AiIconGenerator
    {
        /// <summary>Псевдо-чекпоинты в рецептах вариантов PixelLab (UI Creator и pixflux).</summary>
        public const string PixelLabCheckpointId = "pixellab-ui";
        public const string PixelLabImageCheckpointId = "pixellab-img";

        /// <summary>Рецепт сгенерирован любым движком PixelLab (ComfyUI не нужен).</summary>
        public static bool IsPixelLabCheckpoint(string checkpoint) =>
            checkpoint == PixelLabCheckpointId || checkpoint == PixelLabImageCheckpointId;

        private static readonly List<IconGenRequest> Queue = new();   // список: выбор следующего — по модели
        private static System.Threading.Tasks.Task<byte[]> _current;
        private static IconGenRequest _currentReq;
        private static ComfyUIClient.IconRequest _currentComposed;  // что реально ушло — для рецепта
        private static string _currentStyleName;
        private static string[] _currentStyleRefPaths;              // asset-пути референсов (для рецепта)
        private static string _currentInitPath;                     // asset-путь исходника img2img (для рецепта)
        private static string _lastCheckpoint;                      // чем генерили только что (для группировки)
        private static int _total;
        private static int _done;
        private static bool _cancelled;
        private static int _progressId = -1;                        // фоновый прогресс (Progress API)

        public static bool IsRunning => _current != null || Queue.Count > 0;

        /// <summary>Сколько запросов ещё в работе (включая текущий) — для счётчика очереди в UI.</summary>
        public static int Pending => Queue.Count + (_current != null ? 1 : 0);

        /// <summary>Очередь изменилась (добавили/завершили/отменили) — retained-UI обновляет счётчики.</summary>
        public static event System.Action QueueChanged;

        /// <summary>Прогресс пачки для UI: сделано/всего и метка текущего элемента.</summary>
        public static int Done => _done;
        public static int Total => _total;
        public static string CurrentLabel =>
            _current == null ? ""
            : string.IsNullOrEmpty(_currentReq.DisplayName) ? "иконка" : _currentReq.DisplayName;

        /// <summary>Поставить в очередь несколько запросов (пустые промпты пропускаются).</summary>
        public static void Enqueue(IEnumerable<IconGenRequest> requests)
        {
            foreach (var r in requests)
            {
                if (string.IsNullOrWhiteSpace(r.Prompt) || string.IsNullOrEmpty(r.AssetPath)) continue;
                Queue.Add(r);
            }

            _total = _done + Queue.Count + (_current != null ? 1 : 0);
            _cancelled = false;

            // Фоновый прогресс в статус-баре: модальный DisplayCancelableProgressBar
            // блокировал редактор на всё время генерации.
            if (_progressId < 0)
            {
                _progressId = Progress.Start("Генерация иконок (ComfyUI)");
                Progress.RegisterCancelCallback(_progressId, () => { _cancelled = true; return true; });
            }

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            QueueChanged?.Invoke();
        }

        /// <summary>
        /// Следующий запрос: предпочитаем тот же чекпоинт, что у только что завершённого —
        /// своп моделей Flux↔Juggernaut стоит 20–40 c VRAM-перезагрузки. FIFO внутри модели.
        /// </summary>
        private static IconGenRequest DequeueNext()
        {
            int pick = 0;
            if (!string.IsNullOrEmpty(_lastCheckpoint))
            {
                for (int i = 0; i < Queue.Count; i++)
                {
                    string ckpt = !string.IsNullOrEmpty(Queue[i].CheckpointOverride)
                        ? Queue[i].CheckpointOverride
                        : Queue[i].DirectRequest?.EffCheckpoint ?? AiToolsSettings.Checkpoint;
                    if (ckpt == _lastCheckpoint) { pick = i; break; }
                }
            }
            var req = Queue[pick];
            Queue.RemoveAt(pick);
            return req;
        }

        public static void Enqueue(IconGenRequest request) => Enqueue(new[] { request });

        public static void CancelAll() => _cancelled = true;

        /// <summary>Ожидающие запросы (без текущего) — для окна очереди. Не мутировать.</summary>
        public static IReadOnlyList<IconGenRequest> PendingRequests => Queue;

        /// <summary>
        /// Отменить один ОЖИДАЮЩИЙ запрос по индексу в PendingRequests. Текущий рендер
        /// не прерывается (ComfyUI уже занят) — его снимает только CancelAll.
        /// </summary>
        public static bool CancelAt(int index)
        {
            if (index < 0 || index >= Queue.Count) return false;
            Queue.RemoveAt(index);
            _total = Mathf.Max(_done, _total - 1);
            QueueChanged?.Invoke();
            return true;
        }

        private static void Tick()
        {
            if (_current == null)
            {
                if (_cancelled || Queue.Count == 0) { Finish(); return; }

                _currentReq = DequeueNext();

                if (_currentReq.DirectRequest.HasValue)
                {
                    // Перегенерация по готовому рецепту — гоним как есть (та же модель/промпт/сид)
                    _currentComposed = _currentReq.DirectRequest.Value;
                    _currentStyleName = "(рецепт)";
                    _currentStyleRefPaths = null;
                    _currentInitPath = "";
                }
                else
                {
                    var style = _currentReq.ResolveStyle();
                    if (style == null)
                    {
                        Debug.LogError("[AiIcon] Нет стиль-пресета (IconStylePreset). Создайте хотя бы один " +
                                       "и выберите дефолтным в Project Settings ▸ AI Icon Tools.");
                        Finish();
                        return;
                    }
                    _currentComposed = style.Compose(_currentReq.Prompt, _currentReq.ResolveSeed());
                    _currentStyleName = style.name;

                    // Прямоугольный рендер набора (превью 1024×640); 0 — квадрат из пресета
                    if (_currentReq.RenderSize.x > 0)
                    {
                        _currentComposed.Size = _currentReq.RenderSize.x;
                        _currentComposed.Height = _currentReq.RenderSize.y;
                    }

                    // Референсы стиля (IPAdapter): глобальная настройка, применяется ко всем
                    // генерациям через стиль (перегенерация по рецепту идёт своим путём выше)
                    var refAssets = new List<string>();
                    var refFiles = new List<string>();
                    foreach (var assetPath in AiToolsSettings.StyleRefAssetPaths)
                    {
                        if (string.IsNullOrEmpty(assetPath)) continue;
                        string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                            assetPath.Replace('/', Path.DirectorySeparatorChar)));
                        if (!File.Exists(abs)) continue;
                        refAssets.Add(assetPath);
                        refFiles.Add(abs);
                    }
                    _currentStyleRefPaths = refAssets.ToArray();
                    if (refFiles.Count > 0)
                    {
                        _currentComposed.StyleRefFiles = refFiles.ToArray();
                        _currentComposed.StyleRefWeight = AiToolsSettings.StyleRefWeight;
                        _currentComposed.StyleRefMode = AiToolsSettings.StyleRefMode;
                    }

                    // img2img-стилизация: исходник из запроса (композиция его, стиль — промпта)
                    _currentInitPath = "";
                    if (!string.IsNullOrEmpty(_currentReq.InitImageAssetPath))
                    {
                        string initAbs = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                            _currentReq.InitImageAssetPath.Replace('/', Path.DirectorySeparatorChar)));
                        if (File.Exists(initAbs))
                        {
                            _currentComposed.InitImageFile = initAbs;
                            _currentComposed.Denoise = _currentReq.Denoise;
                            _currentInitPath = _currentReq.InitImageAssetPath;
                        }
                    }

                    // Оверрайд модели из запроса (стилизация другим профилем и т.п.) —
                    // до Flux-страховки, чтобы она видела эффективный чекпоинт
                    if (!string.IsNullOrEmpty(_currentReq.CheckpointOverride))
                    {
                        _currentComposed.HasModelOverride = true;
                        _currentComposed.CheckpointOverride = _currentReq.CheckpointOverride;
                        _currentComposed.LoraOverride = _currentReq.LoraOverride ?? "";
                        _currentComposed.LoraStrengthOverride = _currentReq.LoraStrengthOverride;
                    }

                    // Страховка от несовместимой пары «модель + параметры стиля».
                    // Правим ДО отправки, чтобы рецепт варианта записал реальные параметры.
                    // DirectRequest (перегенерация по рецепту) не трогаем — воспроизводим точь-в-точь.
                    if (ModelCompat.NeedsAdjustment(_currentComposed))
                    {
                        _currentComposed = ModelCompat.Adjust(_currentComposed);
                        Debug.Log($"[AiIcon] {_currentComposed.EffCheckpoint}: параметры стиля " +
                                  "несовместимы с Flux — подогнано (≤4 шага, cfg 1, euler/simple, LoRA откл).");
                    }

                    // Движки PixelLab: размер из стиля, псевдо-чекпоинт в рецепт —
                    // по нему перегенерация вариантов узнаёт движок
                    if (style.engine == IconGenEngine.PixelLabUI ||
                        style.engine == IconGenEngine.PixelLabImage)
                    {
                        _currentComposed.Size = style.pixelLabSize.x;
                        _currentComposed.Height = style.pixelLabSize.y;
                        _currentComposed.HasModelOverride = true;
                        _currentComposed.CheckpointOverride = style.engine == IconGenEngine.PixelLabUI
                            ? PixelLabCheckpointId
                            : PixelLabImageCheckpointId;
                        _currentComposed.LoraOverride = "";
                    }
                }

                _lastCheckpoint = _currentComposed.EffCheckpoint;
                _current = StartGeneration(_currentReq, _currentComposed);

                // Прогресс — в статус-баре Unity (не модальный): редактор кликабелен
                if (_progressId >= 0)
                    Progress.Report(_progressId, _total > 0 ? _done / (float)_total : 0f,
                        $"{_done + 1}/{_total}: " +
                        (string.IsNullOrEmpty(_currentReq.DisplayName) ? "иконка" : _currentReq.DisplayName));
            }

            string label = string.IsNullOrEmpty(_currentReq.DisplayName) ? "иконка" : _currentReq.DisplayName;

            if (!_current.IsCompleted) return;

            var finished = _current;
            var req2 = _currentReq;
            _current = null;

            if (finished.IsFaulted)
            {
                var ex = finished.Exception?.GetBaseException();
                Debug.LogError($"[AiIcon] «{label}»: {ex?.Message}");
            }
            else
            {
                if (SaveResult(req2, finished.Result, label)) _done++;
            }
            QueueChanged?.Invoke();

            if (_cancelled) Finish();
        }

        /// <summary>
        /// Диспетчер движков: по псевдо-чекпоинту рецепта выбираем ComfyUI или
        /// PixelLab UI. Палитра и элементы-каркасы PixelLab живут в стиле; при
        /// перегенерации по рецепту (DirectRequest) стиль недоступен — уходит
        /// только описание+сид, этого достаточно для воспроизведения.
        /// </summary>
        private static System.Threading.Tasks.Task<byte[]> StartGeneration(
            IconGenRequest req, ComfyUIClient.IconRequest composed)
        {
            if (!IsPixelLabCheckpoint(composed.EffCheckpoint))
                return ComfyUIClient.GenerateAsync(composed);

            var style = req.DirectRequest.HasValue ? null : req.ResolveStyle();
            var plRequest = new PixelLabUiClient.Request
            {
                Description = composed.Positive,
                Width = composed.Size,
                Height = composed.EffHeight,
                Palette = style != null ? style.pixelLabPalette : "",
                Elements = style != null ? style.pixelLabElements : null,
                Seed = composed.Seed,
            };

            return composed.EffCheckpoint == PixelLabCheckpointId
                ? PixelLabUiClient.GenerateAsync(plRequest)       // UI Creator: цельная панель
                : PixelLabUiClient.GenerateImageAsync(plRequest); // pixflux: отдельная иконка
        }

        private static bool SaveResult(IconGenRequest req, byte[] png, string label)
        {
            if (png == null || png.Length == 0)
            {
                Debug.LogError($"[AiIcon] «{label}»: пустой ответ.");
                return false;
            }

            EnsureFolder(Path.GetDirectoryName(req.AssetPath).Replace('\\', '/'));
            var size = req.ResolveSize();

            // ComfyUI отдаёт иконку НА ФОНЕ (LayerDiffuse отключён — он корёжил формы).
            // Прозрачность и ужатие делает rembg: тот же путь, что для облачных иконок.
            Sprite sprite;
            // PixelLab отдаёт уже прозрачный PNG (no_background) — rembg не нужен,
            // а его маска размягчила бы пиксельные края
            bool alreadyTransparent = IsPixelLabCheckpoint(_currentComposed.EffCheckpoint);
            // UI-элемент (NoCrop): фон не режем — 9-slice рамка привязана к краям кадра,
            // а rembg/trim сдвинули бы её. Сохраняем кадр целиком и ставим spriteBorder.
            if (req.CutBackground && !req.NoCrop && !alreadyTransparent && RembgProcessor.IsAvailable)
            {
                sprite = RembgProcessor.ProcessBytes(png, req.AssetPath, size.x, size.y);
                if (sprite == null)
                {
                    Debug.LogError($"[AiIcon] «{label}»: rembg не обработал результат.");
                    return false;
                }
            }
            else
            {
                // Без rembg (UI-элемент или rembg недоступен): фон не режем, но РЕСАЙЗИМ до
                // размера хранения (StorageSize). Иначе PNG остаётся 1024², а 9-slice рамка
                // (напр. 24 px) считается от 1024 → срез у самого края, весь центр тайлится.
                // rembg-ветка делает этот даунскейл сама; здесь — вручную.
                string absolute = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", req.AssetPath.Replace('/', Path.DirectorySeparatorChar)));
                byte[] toWrite = ResizePng(png, size.x, size.y);
                File.WriteAllBytes(absolute, toWrite ?? png);
                AssetDatabase.ImportAsset(req.AssetPath, ImportAssetOptions.ForceUpdate);
                ClipboardImageImporter.ConfigureAsSprite(req.AssetPath,
                    new ClipboardImageImporter.ImportOptions
                    {
                        maxSize = Mathf.NextPowerOfTwo(Mathf.Max(size.x, size.y)),
                        spriteBorder = req.SpriteBorder,
                    });
                AssetDatabase.ImportAsset(req.AssetPath, ImportAssetOptions.ForceUpdate);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(req.AssetPath);
                if (req.CutBackground && !req.NoCrop && !alreadyTransparent)
                    Debug.LogWarning($"[AiIcon] rembg недоступен — фон у «{label}» не вырезан.");
            }

            if (sprite == null)
            {
                Debug.LogError($"[AiIcon] «{label}»: PNG сохранён, но спрайт не загрузился ({req.AssetPath}).");
                return false;
            }

            var c = _currentComposed;

            // Честная пиксельная сетка: модель рисует «примерно пиксели», квантование
            // делает их точными (N px по ширине, жёсткая альфа). После rembg и ужатия.
            // UI-элементы (NoCrop) пропускают: trim сдвинул бы 9-slice рамку.
            if (c.PixelGrid > 0 && !req.NoCrop)
                PixelSnap(req.AssetPath, c.PixelGrid, c.PixelTrim, c.PixelColors);
            var result = new IconGenResult
            {
                Subject = req.Prompt,
                Positive = c.Positive,
                Negative = c.Negative,
                Seed = c.Seed,
                Size = c.Size,
                Height = c.Height,
                Steps = c.Steps,
                Cfg = c.Cfg,
                Sampler = c.Sampler,
                Scheduler = c.Scheduler,
                Checkpoint = c.EffCheckpoint,
                Lora = c.EffLora,
                LoraStrength = c.EffLoraStrength,
                StyleName = _currentStyleName,
                PixelGrid = c.PixelGrid,
                PixelTrim = c.PixelTrim,
                PixelColors = c.PixelColors,
                RunId = req.RunId,
                GeneratedAtUtc = System.DateTime.UtcNow.ToString("o"),
                PngAssetPath = req.AssetPath,
                // Референсы пишем, только если они реально дошли до графа (Flux-страховка их снимает)
                StyleRefPaths = c.HasStyleRef ? _currentStyleRefPaths : null,
                StyleRefWeight = c.HasStyleRef ? c.StyleRefWeight : 0f,
                StyleRefMode = c.HasStyleRef ? c.StyleRefMode : "",
                InitImagePath = string.IsNullOrEmpty(c.InitImageFile) ? "" : _currentInitPath,
                Denoise = string.IsNullOrEmpty(c.InitImageFile) ? 0f : c.Denoise,
            };

            if (req.UndoTarget != null)
                Undo.RecordObject(req.UndoTarget, string.IsNullOrEmpty(req.UndoLabel) ? "Generate icon" : req.UndoLabel);
            req.OnDone?.Invoke(sprite, result);
            if (req.UndoTarget != null)
            {
                EditorUtility.SetDirty(req.UndoTarget);
                AssetDatabase.SaveAssetIfDirty(req.UndoTarget);   // точечно: полный SaveAssets морозит редактор
            }

            Debug.Log($"[AiIcon] Иконка готова: {label} → {req.AssetPath}");
            return true;
        }

        /// <summary>
        /// Ужать PNG до w×h (bilinear через GPU-Blit). Для UI-элементов, идущих мимо rembg:
        /// рендер 1024² надо привести к размеру хранения, иначе 9-slice рамка в пикселях
        /// оказывается ничтожной долей кадра. Возвращает null при ошибке (сохранится оригинал).
        /// </summary>
        private static byte[] ResizePng(byte[] png, int w, int h)
        {
            if (png == null || w <= 0 || h <= 0) return null;
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(png)) { Object.DestroyImmediate(src); return null; }
            if (src.width == w && src.height == h) { Object.DestroyImmediate(src); return png; }

            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            dst.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var bytes = dst.EncodeToPNG();
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);
            return bytes;
        }

        /// <summary>
        /// Пиксель-постпроцесс PNG: (1) trim прозрачных полей — объект на весь кадр;
        /// (2) квантование в сетку grid px (box-усреднение по ячейке, жёсткая альфа);
        /// (3) ограничение палитры colors цветов (median-cut); (4) nearest-разворот
        /// обратно в исходный размер файла. Даёт честный пиксель-арт даже когда модель
        /// нарисовала «примерно пиксели», а мелкий размер не разваливается.
        /// </summary>
        private static void PixelSnap(string assetPath, int grid, bool trim, int colors)
        {
            string abs = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", assetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(abs)) return;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(abs))) { Object.DestroyImmediate(tex); return; }

            int fullW = tex.width, fullH = tex.height;
            var full = tex.GetPixels32();

            // (1) Trim: bbox непрозрачных пикселей. Объект занимает весь кадр → при мелкой
            // сетке на него приходится больше ячеек. Небольшой отступ, чтобы не срезать край.
            int rx = 0, ry = 0, rw = fullW, rh = fullH;
            if (trim && !TrimBounds(full, fullW, fullH, out rx, out ry, out rw, out rh))
            { rx = 0; ry = 0; rw = fullW; rh = fullH; }   // пусто — не трогаем

            int gw = Mathf.Clamp(grid, 4, rw);
            int gh = Mathf.Max(1, Mathf.RoundToInt(gw * (rh / (float)rw)));

            // (2) Ячейка сетки над обрезанной областью: средний цвет с весом альфы + порог
            var cells = new Color32[gw * gh];
            for (int cy = 0; cy < gh; cy++)
            for (int cx = 0; cx < gw; cx++)
            {
                int x0 = rx + cx * rw / gw, x1 = rx + Mathf.Max(cx * rw / gw + 1, (cx + 1) * rw / gw);
                int y0 = ry + cy * rh / gh, y1 = ry + Mathf.Max(cy * rh / gh + 1, (cy + 1) * rh / gh);
                float r = 0, g = 0, b = 0, aSum = 0; int count = 0;
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    var p = full[y * fullW + x];
                    r += p.r * p.a; g += p.g * p.a; b += p.b * p.a;
                    aSum += p.a; count++;
                }
                bool opaque = count > 0 && aSum / count >= 128f;
                cells[cy * gw + cx] = !opaque || aSum <= 0f
                    ? new Color32(0, 0, 0, 0)
                    : new Color32((byte)(r / aSum), (byte)(g / aSum), (byte)(b / aSum), 255);
            }

            // (3) Палитра: свести непрозрачные цвета к N (median-cut)
            if (colors > 0) QuantizePalette(cells, colors);

            // (4) Nearest-разворот обратно в полный размер файла (квадрат из ячеек)
            var outPix = new Color32[fullW * fullH];
            for (int y = 0; y < fullH; y++)
            {
                int cy = Mathf.Min(gh - 1, y * gh / fullH);
                for (int x = 0; x < fullW; x++)
                    outPix[y * fullW + x] = cells[cy * gw + Mathf.Min(gw - 1, x * gw / fullW)];
            }

            tex.SetPixels32(outPix);
            tex.Apply();
            File.WriteAllBytes(abs, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>BBox непрозрачных пикселей (порог альфы 16). false — кадр пуст.</summary>
        private static bool TrimBounds(Color32[] px, int w, int h,
                                       out int x0, out int y0, out int rw, out int rh)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 16)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }

            if (maxX < 0) { x0 = 0; y0 = 0; rw = w; rh = h; return false; }

            // Отступ 1 пиксель, чтобы контур объекта не лип к самому краю
            minX = Mathf.Max(0, minX - 1); minY = Mathf.Max(0, minY - 1);
            maxX = Mathf.Min(w - 1, maxX + 1); maxY = Mathf.Min(h - 1, maxY + 1);
            x0 = minX; y0 = minY; rw = maxX - minX + 1; rh = maxY - minY + 1;
            return true;
        }

        /// <summary>
        /// Свести непрозрачные цвета ячеек к n через median-cut: рекурсивно делим набор
        /// цветов по каналу с наибольшим разбросом, каждый бокс → его средний цвет.
        /// </summary>
        private static void QuantizePalette(Color32[] cells, int n)
        {
            var idx = new List<int>();
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].a > 0) idx.Add(i);
            if (idx.Count <= n) return;

            var boxes = new List<List<int>> { idx };
            while (boxes.Count < n)
            {
                // Бокс с наибольшим разбросом канала
                int bi = -1, bestSpread = -1, bestCh = 0;
                for (int i = 0; i < boxes.Count; i++)
                {
                    if (boxes[i].Count < 2) continue;
                    ChannelSpread(cells, boxes[i], out int ch, out int spread);
                    if (spread > bestSpread) { bestSpread = spread; bi = i; bestCh = ch; }
                }
                if (bi < 0) break;

                var box = boxes[bi];
                box.Sort((a, b) => Channel(cells[a], bestCh).CompareTo(Channel(cells[b], bestCh)));
                int mid = box.Count / 2;
                boxes[bi] = box.GetRange(0, mid);
                boxes.Add(box.GetRange(mid, box.Count - mid));
            }

            // Каждый бокс → средний цвет всем его ячейкам
            foreach (var box in boxes)
            {
                long r = 0, g = 0, b = 0;
                foreach (int i in box) { r += cells[i].r; g += cells[i].g; b += cells[i].b; }
                int cnt = Mathf.Max(1, box.Count);
                var avg = new Color32((byte)(r / cnt), (byte)(g / cnt), (byte)(b / cnt), 255);
                foreach (int i in box) cells[i] = avg;
            }
        }

        private static byte Channel(Color32 c, int ch) => ch == 0 ? c.r : ch == 1 ? c.g : c.b;

        private static void ChannelSpread(Color32[] cells, List<int> box, out int ch, out int spread)
        {
            int rMin = 255, rMax = 0, gMin = 255, gMax = 0, bMin = 255, bMax = 0;
            foreach (int i in box)
            {
                var c = cells[i];
                if (c.r < rMin) rMin = c.r; if (c.r > rMax) rMax = c.r;
                if (c.g < gMin) gMin = c.g; if (c.g > gMax) gMax = c.g;
                if (c.b < bMin) bMin = c.b; if (c.b > bMax) bMax = c.b;
            }
            int dr = rMax - rMin, dg = gMax - gMin, db = bMax - bMin;
            if (dr >= dg && dr >= db) { ch = 0; spread = dr; }
            else if (dg >= db) { ch = 1; spread = dg; }
            else { ch = 2; spread = db; }
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            if (_progressId >= 0)
            {
                Progress.Remove(_progressId);
                _progressId = -1;
            }
            Queue.Clear();
            _current = null;

            if (_done > 0) Debug.Log($"[AiIcon] Готово иконок: {_done}.");
            _done = 0;
            _total = 0;
            _cancelled = false;
            QueueChanged?.Invoke();
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
