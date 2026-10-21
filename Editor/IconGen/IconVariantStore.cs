using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Editor-обёртка над IconVariantLibrary: копит варианты генерации (не перезаписывая),
    /// перегенерирует по рецепту, двухэтапно чистит (сперва PNG, потом рецепты).
    /// </summary>
    public static class IconVariantStore
    {
        /// <summary>Куда создаётся библиотека, если в проекте её ещё нет.</summary>
        private const string DefaultLibraryPath = "Assets/AIStudio/Icons/IconVariantLibrary.asset";

        // ── Библиотека ──

        // Кэш: LoadAssetAtPath на каждый доступ из OnGUI был главным источником лагов.
        // Ссылка переживает вызовы кадра; после domain reload статик обнуляется сам,
        // после удаления ассета Unity-объект «умирает» (== null) — перезагружаем.
        private static IconVariantLibrary _library;

        /// <summary>
        /// Версия данных: инкрементится каждым мутирующим методом стора. Окна сравнивают
        /// со своей сохранённой и пересобирают кэши/перерисовываются только при изменении.
        /// </summary>
        public static int Version { get; private set; } = 1;

        /// <summary>Данные изменились — для retained-UI (студия) вместо поллинга Version.</summary>
        public static event System.Action Changed;

        private static void Bump()
        {
            Version++;
            Changed?.Invoke();
        }

        public static IconVariantLibrary Library
        {
            get
            {
                if (_library != null) return _library;

                // Библиотека одна на проект и лежит где угодно — ищем по типу
                var guids = AssetDatabase.FindAssets("t:" + nameof(IconVariantLibrary));
                if (guids.Length > 0)
                {
                    System.Array.Sort(guids, (a, b) => string.CompareOrdinal(
                        AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
                    if (guids.Length > 1)
                        Debug.LogWarning($"[IconStudio] В проекте {guids.Length} IconVariantLibrary — " +
                                         $"используется {AssetDatabase.GUIDToAssetPath(guids[0])}");
                    _library = AssetDatabase.LoadAssetAtPath<IconVariantLibrary>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
                }
                if (_library == null)
                {
                    _library = ScriptableObject.CreateInstance<IconVariantLibrary>();
                    EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath).Replace('\\', '/'));
                    AssetDatabase.CreateAsset(_library, DefaultLibraryPath);
                    AssetDatabase.SaveAssets();
                }
                Bump();
                return _library;
            }
        }

        /// <summary>Папка из настройки библиотеки; пусто — подпапка рядом с ассетом библиотеки.</summary>
        private static string LibraryFolder(string configured, string fallbackName)
        {
            if (!string.IsNullOrEmpty(configured)) return configured.TrimEnd('/');
            string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(Library))?.Replace('\\', '/');
            return string.IsNullOrEmpty(dir) ? "Assets/AIStudio/Icons/" + fallbackName : dir + "/" + fallbackName;
        }

        /// <summary>Папка паков (IconPack).</summary>
        public static string PacksFolder => LibraryFolder(Library.packsFolder, "IconPacks");

        /// <summary>Папка референсов стиля, вставленных из буфера.</summary>
        public static string StyleRefsFolder => LibraryFolder(Library.styleRefsFolder, "StyleRefs");

        // ── Путь под новый вариант (уникальный, не перезаписывает) ──

        public static string NewVariantId() => System.Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>Обобщённый путь: у каждого набора (иконки/превью) своя корневая папка.</summary>
        public static string VariantPngPath(string folder, string entityId, string variantId)
            => $"{folder}/{entityId}/Icon_{entityId}_{variantId}.png";

        // ── Запрос генерации для элемента набора (generic: студия работает с любым набором) ──

        /// <summary>
        /// Собрать запрос генерации для элемента набора. Каждая генерация — НОВЫЙ вариант
        /// (уникальный PNG), в OnDone вариант с рецептом попадает в историю и становится
        /// активным, спрайт назначается сущности через колбэк.
        /// </summary>
        /// <summary>
        /// Снять ИСХОДНУЮ картинку сущности в историю как вариант — при первой
        /// генерации, пока история пуста. Иначе до-студийный арт «пропадает»:
        /// новый вариант автоматически становится активным, а исходник не показан
        /// в пикере, и вернуть его кликом нельзя.
        /// </summary>
        public static void EnsureBaseSnapshot(IconItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.EntityId)) return;

            var lib = Library;
            var history = lib.GetOrCreate(item.EntityId);
            if (history.variants.Count > 0) return; // история уже есть — исходник либо снят, либо неактуален

            var current = item.GetSprite?.Invoke();
            if (current == null) return;
            string path = AssetDatabase.GetAssetPath(current);
            if (string.IsNullOrEmpty(path)) return;

            var snapshot = new IconVariant
            {
                id = NewVariantId(),
                sprite = current,
                pngAssetPath = path,
                imageDeleted = false,
                recipe = new IconRecipe
                {
                    subject = "(исходник до студии)",
                    styleName = "исходник",
                    generatedAtUtc = System.DateTime.UtcNow.ToString("o"),
                },
            };
            history.variants.Add(snapshot);
            history.activeVariantId = snapshot.id;

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
        }

        public static IconGenRequest BuildRequest(IconItem item, IconCollection col, string runId)
        {
            EnsureBaseSnapshot(item);

            string variantId = NewVariantId();
            string path = VariantPngPath(col.PngFolder, item.EntityId, variantId);
            // UI-элемент (подложка/рамка): фон не режем и до контента не обрезаем —
            // 9-slice рамка привязана к краям кадра. См. IconGenRequest.NoCrop.
            bool uiElement = item.IsUiElement;
            return new IconGenRequest
            {
                Prompt = item.GetPrompt?.Invoke(),
                AssetPath = path,
                DisplayName = item.DisplayName,
                StorageSize = col.StorageSize,
                RenderSize = col.RenderSize,
                Style = IconStyleRegistry.Default,
                // Референсам image-to-3D фон НУЖЕН (KeepBackground набора):
                // модель собирается по цельной сцене, вырезанный силуэт врёт объёмом
                CutBackground = !uiElement && !col.KeepBackground,
                NoCrop = uiElement || col.KeepBackground,
                SpriteBorder = uiElement ? item.SliceBorder : Vector4.zero,
                RunId = runId,
                UndoTarget = item.UndoTarget,
                UndoLabel = "Generate " + col.Id,
                OnDone = (sprite, result) =>
                {
                    AddVariant(item.EntityId, variantId, sprite, result);
                    item.SetSprite?.Invoke(sprite);   // активный вариант — то, что идёт в игру
                },
            };
        }

        // ── Приём результата генерации → новый вариант, активный ──

        public static void AddVariant(string entityId, string variantId, Sprite sprite, IconGenResult r)
        {
            var lib = Library;
            var history = lib.GetOrCreate(entityId);

            var variant = new IconVariant
            {
                id = variantId,
                sprite = sprite,
                pngAssetPath = r.PngAssetPath,
                imageDeleted = false,
                recipe = ToRecipe(r),
            };
            history.variants.Add(variant);
            history.activeVariantId = variantId;

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
        }

        public static IconRecipe ToRecipe(IconGenResult r) => new()
        {
            subject = r.Subject,
            positive = r.Positive,
            negative = r.Negative,
            seed = r.Seed,
            size = r.Size,
            height = r.Height,
            steps = r.Steps,
            cfg = r.Cfg,
            sampler = r.Sampler,
            scheduler = r.Scheduler,
            checkpoint = r.Checkpoint,
            lora = r.Lora,
            loraStrength = r.LoraStrength,
            styleName = r.StyleName,
            pixelGrid = r.PixelGrid,
            pixelTrim = r.PixelTrim,
            pixelColors = r.PixelColors,
            runId = r.RunId,
            generatedAtUtc = r.GeneratedAtUtc,
            styleRefPaths = r.StyleRefPaths == null
                ? new List<string>() : new List<string>(r.StyleRefPaths),
            styleRefWeight = r.StyleRefWeight,
            styleRefMode = r.StyleRefMode,
            initImagePath = r.InitImagePath,
            denoise = r.Denoise,
        };

        // ── Перегенерация по рецепту: тот же промпт/сид/модель → тот же кадр ──

        public static ComfyUIClient.IconRequest RecipeToRequest(IconRecipe rec)
        {
            // Референсы стиля: восстанавливаем абсолютные пути из asset-путей (если файлы ещё живы)
            string[] refFiles = null;
            if (rec.styleRefPaths != null && rec.styleRefPaths.Count > 0)
            {
                var list = new List<string>();
                foreach (var p in rec.styleRefPaths)
                {
                    if (string.IsNullOrEmpty(p)) continue;
                    string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                        p.Replace('/', Path.DirectorySeparatorChar)));
                    if (File.Exists(abs)) list.Add(abs);
                }
                if (list.Count > 0) refFiles = list.ToArray();
            }

            return new ComfyUIClient.IconRequest
            {
                Positive = rec.positive,
                Negative = rec.negative,
                Seed = rec.seed,
                Size = rec.size,
                Height = rec.height,
                Steps = rec.steps,
                Cfg = rec.cfg,
                Sampler = rec.sampler,
                Scheduler = rec.scheduler,
                CheckpointOverride = rec.checkpoint,
                LoraOverride = rec.lora ?? "",
                LoraStrengthOverride = rec.loraStrength,
                HasModelOverride = true,
                StyleRefFiles = refFiles,
                StyleRefWeight = rec.styleRefWeight,
                StyleRefMode = rec.styleRefMode,
                InitImageFile = string.IsNullOrEmpty(rec.initImagePath) ? null
                    : Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                        rec.initImagePath.Replace('/', Path.DirectorySeparatorChar))),
                Denoise = rec.denoise,
                PixelGrid = rec.pixelGrid,
                PixelTrim = rec.pixelTrim,
                PixelColors = rec.pixelColors,
            };
        }

        // ── Выбор активного варианта ──

        public static void SetActive(string entityId, string variantId)
        {
            var history = Library.Find(entityId);
            if (history == null) return;
            history.activeVariantId = variantId;
            EditorUtility.SetDirty(Library);
            AssetDatabase.SaveAssetIfDirty(Library);
            Bump();
        }

        /// <summary>
        /// Постобработка активного варианта (обрезка/зеркало/область) — своя у каждого варианта.
        /// НЕ вызывает Bump: иначе пересборка пикера прерывала бы перетаскивание выделения;
        /// превью обновляется локально в студии, персист — через SetDirty (сохранится с ассетом).
        /// </summary>
        public static void SetActiveVariantPost(string entityId, int mode, int axis, RectInt rect,
            Color fill, int feather, int cornerRadius)
        {
            var active = Library.Find(entityId)?.Active;
            if (active == null) return;
            active.postMode = mode; active.postAxis = axis; active.postRect = rect;
            active.postFillColor = fill;
            active.postFeather = feather; active.postCornerRadius = cornerRadius;
            EditorUtility.SetDirty(Library);
        }

        /// <summary>Прочитать постобработку активного варианта; default если варианта нет.</summary>
        public static bool GetActiveVariantPost(string entityId, out int mode, out int axis,
            out RectInt rect, out Color fill, out int feather, out int cornerRadius)
        {
            mode = 0; axis = 0; rect = default; fill = new Color(0.05f, 0.05f, 0.06f, 1f);
            feather = 6; cornerRadius = 8;
            var active = Library.Find(entityId)?.Active;
            if (active == null) return false;
            mode = active.postMode; axis = active.postAxis; rect = active.postRect;
            if (active.postFillColor.a > 0f) fill = active.postFillColor;
            feather = active.postFeather; cornerRadius = active.postCornerRadius;
            return true;
        }

        /// <summary>
        /// Запечь текущую постобработку активного варианта в НОВЫЙ вариант (для последовательных
        /// операций: залил → создал вариант → на нём обрезал → …). Новый вариант чистый (postMode=0),
        /// на нём можно делать следующий шаг. Исходный вариант не меняется.
        /// </summary>
        public static bool CreateVariantFromPost(IconItem item, int mode, int axis, RectInt rect,
            Color fill, int feather = 6, int cornerRadius = 8)
        {
            var history = Library.Find(item.EntityId);
            var active = history?.Find(history.activeVariantId);
            if (active == null || string.IsNullOrEmpty(active.pngAssetPath)) return false;
            string srcAbs = ToAbsolute(active.pngAssetPath);
            if (!File.Exists(srcAbs)) return false;

            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(srcAbs));
            var outTex = ApplyPostProcess(src, mode, axis, rect, fill, feather, cornerRadius);
            Object.DestroyImmediate(src);

            string vid = NewVariantId();
            string folder = Path.GetDirectoryName(active.pngAssetPath).Replace('\\', '/');
            string path = VariantPngPath(folder, item.EntityId, vid);
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            File.WriteAllBytes(ToAbsolute(path), outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ClipboardImageImporter.ConfigureAsSprite(path,
                new ClipboardImageImporter.ImportOptions { spriteBorder = item.SliceBorder });
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            var lib = Library;
            history.variants.Add(new IconVariant
            {
                id = vid, sprite = sprite, pngAssetPath = path, imageDeleted = false,
                recipe = active.recipe,   // происхождение наследуется
            });
            history.activeVariantId = vid;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
            item.SetSprite?.Invoke(sprite);
            Debug.Log($"[Постобр.] «{item.DisplayName}»: создан вариант из постобработки (режим {mode}).");
            return true;
        }

        /// <summary>
        /// «Применить в игру» для UI-элемента: скопировать PNG активного варианта в целевой
        /// путь (item.ApplyToPath), на который уже ссылается USS (background-image + -unity-slice),
        /// и переимпортировать с 9-slice рамкой. Замена файла обновляет все окна без правок кода.
        /// Возвращает true, если файл опубликован.
        /// </summary>
        public static bool PublishUiElement(IconItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.ApplyToPath)) return false;

            var history = Library.Find(item.EntityId);
            var active = history?.Find(history.activeVariantId);
            string src = active?.pngAssetPath;
            if (string.IsNullOrEmpty(src) || AssetDatabase.LoadAssetAtPath<Sprite>(src) == null)
            {
                Debug.LogWarning($"[UI-арт] Нет активного варианта у «{item.DisplayName}» — публиковать нечего.");
                return false;
            }

            string srcAbs = ToAbsolute(src);
            string dstAbs = ToAbsolute(item.ApplyToPath);
            EnsureFolder(Path.GetDirectoryName(item.ApplyToPath).Replace('\\', '/'));

            // Постобработка (режим/область) — СВОЯ у активного варианта, применяется ЗДЕСЬ.
            // Активный вариант не меняется, в игру уходит обработанная версия.
            var srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            srcTex.LoadImage(File.ReadAllBytes(srcAbs));
            var outTex = ApplyPostProcess(srcTex, active.postMode, active.postAxis, active.postRect,
                active.postFillColor, active.postFeather, active.postCornerRadius);
            File.WriteAllBytes(dstAbs, outTex.EncodeToPNG());
            Object.DestroyImmediate(srcTex);
            Object.DestroyImmediate(outTex);

            AssetDatabase.ImportAsset(item.ApplyToPath, ImportAssetOptions.ForceUpdate);
            ClipboardImageImporter.ConfigureAsSprite(item.ApplyToPath,
                new ClipboardImageImporter.ImportOptions { spriteBorder = item.SliceBorder });
            AssetDatabase.ImportAsset(item.ApplyToPath, ImportAssetOptions.ForceUpdate);

            // UI Toolkit берёт и картинку, и рамку из USS — прописываем background-image + slice
            // во ВСЕ классы, использующие эту панель (иначе панель видна только там, где url
            // прописан руками, а остальные элементы её не получают). Целей может быть
            // несколько: один спрайт оформляет окна в разных USS-файлах.
            {
                string url = $"url(\"project:///{item.ApplyToPath}\")";
                int touched = 0;
                foreach (var target in item.AllUssTargets())
                {
                    foreach (var cls in target.Classes)
                    {
                        if (string.IsNullOrEmpty(cls)) continue;
                        WriteUssProp(target.File, cls, "background-image", url);
                        SyncUssSlice(target.File, cls, item.SliceBorder);
                        touched++;
                    }
                }
                if (touched > 0) AssetDatabase.Refresh();
            }

            Debug.Log($"[UI-арт] «{item.DisplayName}» опубликован → {item.ApplyToPath}");
            return true;
        }

        /// <summary>
        /// Переписать -unity-slice-* в правиле «.className { … }» указанного USS под рамку border
        /// (L=x, B=y, R=z, T=w). Нужно, потому что UI Toolkit игнорирует spriteBorder ассета и
        /// берёт срез только из USS.
        /// </summary>
        public static void SyncUssSlice(string ussAssetPath, string className, Vector4 border)
        {
            if (!WriteUssProps(ussAssetPath, className, new (string, string)[]
            {
                ("-unity-slice-left",   Mathf.RoundToInt(border.x).ToString()),
                ("-unity-slice-bottom", Mathf.RoundToInt(border.y).ToString()),
                ("-unity-slice-right",  Mathf.RoundToInt(border.z).ToString()),
                ("-unity-slice-top",    Mathf.RoundToInt(border.w).ToString()),
            })) return;
            Debug.Log($"[UI-арт] -unity-slice в .{className} → L{border.x:0} B{border.y:0} R{border.z:0} T{border.w:0}");
        }

        // ── Чтение/запись свойств USS-класса (двусторонняя связь студия ↔ игра) ──

        /// <summary>Значение свойства prop в правиле «.className { … }», или null если нет.</summary>
        public static string ReadUssProp(string ussAssetPath, string className, string prop)
        {
            string abs = ToAbsolute(ussAssetPath);
            if (!File.Exists(abs)) return null;
            if (!TryGetBlock(File.ReadAllText(abs), className, out _, out int open, out int close)) return null;
            string block = File.ReadAllText(abs).Substring(open + 1, close - open - 1);
            var m = System.Text.RegularExpressions.Regex.Match(block,
                System.Text.RegularExpressions.Regex.Escape(prop) + @"\s*:\s*([^;]+);");
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        /// <summary>Задать/удалить (value==null) набор свойств в правиле класса. false — файл/класс не найден.</summary>
        public static bool WriteUssProps(string ussAssetPath, string className, (string prop, string value)[] props)
        {
            string abs = ToAbsolute(ussAssetPath);
            if (!File.Exists(abs))
            {
                Debug.LogWarning($"[UI-арт] USS не найден: {ussAssetPath}.");
                return false;
            }
            string css = File.ReadAllText(abs);
            if (!TryGetBlock(css, className, out _, out int open, out int close))
            {
                Debug.LogWarning($"[UI-арт] Класс .{className} не найден в {ussAssetPath}.");
                return false;
            }
            string block = css.Substring(open + 1, close - open - 1);
            foreach (var (prop, value) in props)
                block = value == null ? RemoveProp(block, prop) : SetOrAppendProp(block, prop, value);

            css = css.Substring(0, open + 1) + block + css.Substring(close);
            File.WriteAllText(abs, css);
            AssetDatabase.ImportAsset(ussAssetPath, ImportAssetOptions.ForceUpdate);
            return true;
        }

        public static void WriteUssProp(string ussAssetPath, string className, string prop, string value)
            => WriteUssProps(ussAssetPath, className, new[] { (prop, value) });

        // Границы блока правила «.className { … }» (первое вхождение)
        private static bool TryGetBlock(string css, string className, out int ruleIdx, out int open, out int close)
        {
            ruleIdx = open = close = -1;
            var m = System.Text.RegularExpressions.Regex.Match(css,
                @"\." + System.Text.RegularExpressions.Regex.Escape(className) + @"\s*\{");
            if (!m.Success) return false;
            ruleIdx = m.Index;
            open = css.IndexOf('{', m.Index);
            close = open >= 0 ? css.IndexOf('}', open) : -1;
            return open >= 0 && close >= 0;
        }

        private static string SetOrAppendProp(string block, string prop, string value)
        {
            var rx = new System.Text.RegularExpressions.Regex(
                @"(^|[\s;{])" + System.Text.RegularExpressions.Regex.Escape(prop) + @"\s*:\s*[^;]+;");
            string decl = $"{prop}: {value};";
            if (rx.IsMatch(block)) return rx.Replace(block, m => m.Groups[1].Value + decl, 1);
            return block.TrimEnd() + "\n    " + decl + "\n";
        }

        private static string RemoveProp(string block, string prop)
        {
            // хвост — только пробелы/таб и один перевод строки: \s* съедал бы отступ
            // СЛЕДУЮЩЕЙ строки и ломал форматирование USS
            var rx = new System.Text.RegularExpressions.Regex(
                @"[ \t]*" + System.Text.RegularExpressions.Regex.Escape(prop) + @"\s*:\s*[^;]+;[ \t]*\r?\n?");
            return rx.Replace(block, "", 1);
        }

        /// <summary>Разобрать USS-цвет (rgb/rgba 0–255 + alpha 0–1, либо #hex) в Color. false — не распознан.</summary>
        public static bool TryParseUssColor(string s, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            var rgb = System.Text.RegularExpressions.Regex.Match(s,
                @"rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*(?:,\s*([\d.]+)\s*)?\)");
            if (rgb.Success)
            {
                var ic = System.Globalization.CultureInfo.InvariantCulture;
                float r = float.Parse(rgb.Groups[1].Value, ic) / 255f;
                float g = float.Parse(rgb.Groups[2].Value, ic) / 255f;
                float b = float.Parse(rgb.Groups[3].Value, ic) / 255f;
                float a = rgb.Groups[4].Success ? float.Parse(rgb.Groups[4].Value, ic) : 1f;
                color = new Color(r, g, b, a);
                return true;
            }
            if (ColorUtility.TryParseHtmlString(s, out color)) return true;
            return false;
        }

        /// <summary>Color → строка USS rgba(0–255, alpha 0–1) в стиле проекта (инвариантная культура —
        /// иначе русская локаль пишет alpha через запятую и ломает USS).</summary>
        public static string UssColor(Color c)
            => string.Format(System.Globalization.CultureInfo.InvariantCulture,
               "rgba({0}, {1}, {2}, {3:0.###})",
               Mathf.RoundToInt(c.r * 255), Mathf.RoundToInt(c.g * 255),
               Mathf.RoundToInt(c.b * 255), c.a);

        private static string ToAbsolute(string assetPath)
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));

        /// <summary>
        /// Удалить фон у активного варианта (rembg) и сохранить НОВЫМ вариантом. То же, что для
        /// иконок, но вторым этапом: панель становится прозрачной по фону, поля вокруг обрезаются
        /// (pad=0 → впритык к краям). border → 9-slice при импорте. Исходник не трогаем.
        /// </summary>
        public static bool RemoveBackgroundVariant(IconItem item, Vector4 border)
        {
            if (!RembgProcessor.IsAvailable)
            {
                Debug.LogWarning("[Фон] rembg недоступен (Project Settings ▸ AI Icon Tools).");
                return false;
            }
            var history = Library.Find(item.EntityId);
            var active = history?.Find(history.activeVariantId);
            if (active == null || string.IsNullOrEmpty(active.pngAssetPath))
            {
                Debug.LogWarning($"[Фон] Нет активного варианта у «{item.DisplayName}».");
                return false;
            }
            string srcAbs = ToAbsolute(active.pngAssetPath);
            if (!File.Exists(srcAbs)) { Debug.LogWarning("[Фон] Файл варианта не найден."); return false; }

            byte[] png = File.ReadAllBytes(srcAbs);
            var probe = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            probe.LoadImage(png);
            int w = probe.width, h = probe.height;
            Object.DestroyImmediate(probe);

            string vid = NewVariantId();
            string folder = Path.GetDirectoryName(active.pngAssetPath).Replace('\\', '/');
            string path = VariantPngPath(folder, item.EntityId, vid);
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            // pad=0 — не добавлять поля: панель займёт весь кадр (важно для 9-slice)
            var sprite = RembgProcessor.ProcessBytes(png, path, w, h, pad: 0f, noBg: false);
            if (sprite == null) { Debug.LogWarning("[Фон] rembg не обработал вариант."); return false; }

            // переимпорт с 9-slice рамкой (ProcessBytes ставит только maxSize)
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ClipboardImageImporter.ConfigureAsSprite(path,
                new ClipboardImageImporter.ImportOptions { spriteBorder = border, maxSize = Mathf.Max(w, h) });
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            var lib = Library;
            history.variants.Add(new IconVariant
            {
                id = vid, sprite = sprite, pngAssetPath = path, imageDeleted = false,
                recipe = active.recipe,
            });
            history.activeVariantId = vid;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
            item.SetSprite?.Invoke(sprite);
            Debug.Log($"[Фон] «{item.DisplayName}»: фон удалён (новый вариант).");
            return true;
        }

        // ── Зеркальная симметрия (второй этап): AI плохо держит симметрию, а 9-slice требует
        //    одинаковых углов. Отражаем один квадрант в остальные — углы совпадут идеально. ──

        public enum MirrorMode { BothAxes, Horizontal, Vertical }

        /// <summary>
        /// Постобработка спрайта (неразрушающая, для публикации/превью): mode 0=Оригинал целиком,
        /// 1=Обрезать по области rect, 2=Зеркалить область (axis 0=обе,1=гориз,2=верт). rect —
        /// top-down px (нулевой размер = весь кадр). Вызывающий владеет результатом (DestroyImmediate).
        /// Точка расширения: сюда лягут будущие операции (яркость/контраст/инверсия…).
        /// </summary>
        public static Texture2D ApplyPostProcess(Texture2D src, int mode, int axis, RectInt rect,
            Color fillColor = default, int feather = 6, int cornerRadius = 8)
        {
            int w = src.width, h = src.height;
            if (mode == 2)
                return BuildMirrored(src, (MirrorMode)Mathf.Clamp(axis, 0, 2),
                    rect.x, rect.y, rect.width, rect.height);
            if (mode == 3)   // заливка области сплошным цветом с мягким краем (feather)
            {
                var s = src.GetPixels32();
                Color32 fill = fillColor.a > 0f ? (Color32)fillColor : new Color32(13, 13, 15, 255);
                int rx = Mathf.Clamp(rect.x, 0, w), ry = Mathf.Clamp(rect.y, 0, h);
                int rw = rect.width > 0 ? Mathf.Clamp(rect.width, 0, w - rx) : w;
                int rh = rect.height > 0 ? Mathf.Clamp(rect.height, 0, h - ry) : h;
                // Скруглённый прямоугольник с мягким краем (SDF): углы скругляются радиусом,
                // край растворяется на ширину feather — чтобы заливка села под скруглённую рамку.
                float fe = Mathf.Max(0.001f, feather);
                float rad = Mathf.Clamp(cornerRadius, 0, Mathf.Min(rw, rh) / 2f);
                float halfW = rw / 2f, halfH = rh / 2f;
                for (int y = 0; y < rh; y++)
                    for (int x = 0; x < rw; x++)
                    {
                        // расстояние до границы скруглённого прямоугольника (<0 внутри)
                        float px = Mathf.Abs(x + 0.5f - halfW) - (halfW - rad);
                        float py = Mathf.Abs(y + 0.5f - halfH) - (halfH - rad);
                        float outside = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) +
                                                   Mathf.Max(py, 0f) * Mathf.Max(py, 0f));
                        float dist = Mathf.Min(Mathf.Max(px, py), 0f) + outside - rad;
                        float t = Mathf.Clamp01(-dist / fe);   // 1 глубоко внутри → 0 на границе
                        if (t <= 0f) continue;                  // снаружи скругления — исходник цел
                        int idx = (h - 1 - (ry + y)) * w + (rx + x);   // top-down
                        s[idx] = Color32.Lerp(s[idx], fill, t);
                    }
                var filled = new Texture2D(w, h, TextureFormat.RGBA32, false);
                filled.SetPixels32(s); filled.Apply();
                return filled;
            }
            if (mode == 1)   // обрезка по области (top-down)
            {
                int rx = Mathf.Clamp(rect.x, 0, w - 1), ry = Mathf.Clamp(rect.y, 0, h - 1);
                int rw = rect.width > 0 ? Mathf.Clamp(rect.width, 1, w - rx) : w;
                int rh = rect.height > 0 ? Mathf.Clamp(rect.height, 1, h - ry) : h;
                var s = src.GetPixels32();
                var d = new Color32[rw * rh];
                for (int y = 0; y < rh; y++)
                    for (int x = 0; x < rw; x++)
                        d[(rh - 1 - y) * rw + x] = s[(h - 1 - (ry + y)) * w + (rx + x)];   // y-flip
                var cropped = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
                cropped.SetPixels32(d); cropped.Apply();
                return cropped;
            }
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);   // Оригинал
            copy.SetPixels32(src.GetPixels32()); copy.Apply();
            return copy;
        }

        /// <summary>
        /// Собрать зеркальную текстуру из прямоугольника-источника (srcX,srcY,srcW,srcH; top-down;
        /// 0-размер → половина кадра) БЕЗ сохранения — для живого превью и для MirrorActiveVariant.
        /// Вызывающий владеет результатом (DestroyImmediate после использования).
        /// </summary>
        public static Texture2D BuildMirrored(Texture2D src, MirrorMode mode,
            int srcX, int srcY, int srcW, int srcH)
        {
            int w = src.width, h = src.height;
            var s = src.GetPixels32();
            System.Func<int, int, Color32> srcTD = (tx, ty) => s[(h - 1 - ty) * w + tx]; // top-down

            int sx0 = Mathf.Clamp(srcX, 0, w - 1);
            int sy0 = Mathf.Clamp(srcY, 0, h - 1);
            int sw = srcW > 0 ? Mathf.Clamp(srcW, 1, w - sx0) : w / 2;
            int sh = srcH > 0 ? Mathf.Clamp(srcH, 1, h - sy0) : h / 2;
            int rw = mode != MirrorMode.Vertical ? 2 * sw : sw;
            int rh = mode != MirrorMode.Horizontal ? 2 * sh : sh;

            var d = new Color32[rw * rh];
            for (int ry = 0; ry < rh; ry++)
                for (int rx = 0; rx < rw; rx++)
                {
                    int qx = (mode != MirrorMode.Vertical && rx >= sw) ? 2 * sw - 1 - rx : rx;
                    int qy = (mode != MirrorMode.Horizontal && ry >= sh) ? 2 * sh - 1 - ry : ry;
                    qx = Mathf.Clamp(qx, 0, sw - 1); qy = Mathf.Clamp(qy, 0, sh - 1);
                    d[(rh - 1 - ry) * rw + rx] = srcTD(sx0 + qx, sy0 + qy);
                }
            var dst = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
            dst.SetPixels32(d); dst.Apply();
            return dst;
        }

        /// <summary>
        /// Сделать активный вариант симметричным зеркалированием и сохранить НОВЫМ вариантом
        /// (исходник не трогаем). Источник — ПРОИЗВОЛЬНЫЙ прямоугольник (srcX,srcY,srcW,srcH) в
        /// top-down координатах (0,0 = верхний-левый угол): выделяя угол панели С ОТСТУПОМ от
        /// края, отсекаем внешние поля/фон. BothAxes — отразить в 4 стороны (2×2 источника);
        /// Horizontal — лево=право; Vertical — верх=низ. border → 9-slice при импорте.
        /// srcW/srcH=0 → половина кадра от левого-верха (старое поведение).
        /// </summary>
        public static bool MirrorActiveVariant(IconItem item, MirrorMode mode, Vector4 border,
            int srcX = 0, int srcY = 0, int srcW = 0, int srcH = 0)
        {
            var history = Library.Find(item.EntityId);
            var active = history?.Find(history.activeVariantId);
            if (active == null || string.IsNullOrEmpty(active.pngAssetPath))
            {
                Debug.LogWarning($"[Зеркало] Нет активного варианта у «{item.DisplayName}».");
                return false;
            }
            string srcAbs = ToAbsolute(active.pngAssetPath);
            if (!File.Exists(srcAbs)) { Debug.LogWarning("[Зеркало] Файл варианта не найден."); return false; }

            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(srcAbs));
            var dst = BuildMirrored(src, mode, srcX, srcY, srcW, srcH);
            Object.DestroyImmediate(src);

            string vid = NewVariantId();
            string folder = Path.GetDirectoryName(active.pngAssetPath).Replace('\\', '/');
            string path = VariantPngPath(folder, item.EntityId, vid);
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            File.WriteAllBytes(ToAbsolute(path), dst.EncodeToPNG());
            Object.DestroyImmediate(dst);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ClipboardImageImporter.ConfigureAsSprite(path,
                new ClipboardImageImporter.ImportOptions { spriteBorder = border });
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            var lib = Library;
            history.variants.Add(new IconVariant
            {
                id = vid, sprite = sprite, pngAssetPath = path, imageDeleted = false,
                recipe = active.recipe,   // происхождение наследуется от исходника
            });
            history.activeVariantId = vid;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
            item.SetSprite?.Invoke(sprite);
            Debug.Log($"[Зеркало] «{item.DisplayName}»: симметричный вариант ({mode}).");
            return true;
        }

        /// <summary>
        /// Пометить НАБОР вариантов неудачным (или снять пометку) — оценивается прогон/пак
        /// целиком, а не отдельная картинка: лажает обычно профиль генерации. В подходящие
        /// IconGenProfile пишется ОДНА сводная заметка на набор (ключ — runId или имя набора).
        /// </summary>
        public static void MarkSetBad(List<(string entityId, string variantId)> set, bool bad, string setLabel)
        {
            var lib = Library;
            var runIds = new HashSet<string>();
            var checkpoints = new HashSet<string>();
            var styles = new HashSet<string>();
            int marked = 0;

            foreach (var (entityId, variantId) in set)
            {
                var variant = lib.Find(entityId)?.Find(variantId);
                if (variant == null) continue;
                variant.bad = bad;
                marked++;
                if (variant.recipe == null) continue;
                if (!string.IsNullOrEmpty(variant.recipe.runId)) runIds.Add(variant.recipe.runId);
                checkpoints.Add(variant.recipe.checkpoint);
                styles.Add(variant.recipe.styleName);
            }
            if (marked == 0) return;

            // Стабильный ключ заметки — чтобы снятие пометки убрало ровно её
            string key = runIds.Count == 1 ? FirstOf(runIds) : Sanitize(setLabel);

            foreach (var profile in IconGenProfile.All())
            {
                if (profile == null || !checkpoints.Contains(profile.checkpoint)) continue;
                if (profile.style != null && !styles.Contains(profile.style.name)) continue;

                if (bad)
                    profile.AddBadNote($"[{key}] {System.DateTime.Now:dd.MM.yyyy} «{setLabel}» — " +
                                       $"неудачный набор ({marked} иконок, прогон {string.Join(", ", runIds)})");
                else
                    profile.RemoveBadNotes(key);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
        }

        /// <summary>
        /// Удалить НАБОР вариантов целиком (PNG + рецепты) — для точно неудачного прогона.
        /// У сущностей, где удалённый вариант был активным, активным становится последний оставшийся.
        /// </summary>
        public static int DeleteVariants(List<(string entityId, string variantId)> set)
        {
            var lib = Library;
            int deleted = 0;
            foreach (var (entityId, variantId) in set)
            {
                var history = lib.Find(entityId);
                var variant = history?.Find(variantId);
                if (variant == null) continue;

                if (!string.IsNullOrEmpty(variant.pngAssetPath) &&
                    AssetDatabase.LoadAssetAtPath<Sprite>(variant.pngAssetPath) != null)
                    AssetDatabase.DeleteAsset(variant.pngAssetPath);

                history.variants.Remove(variant);
                if (history.activeVariantId == variantId)
                    history.activeVariantId = history.variants.Count > 0
                        ? history.variants[history.variants.Count - 1].id : null;
                deleted++;
            }

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
            return deleted;
        }

        private static string FirstOf(HashSet<string> set)
        {
            foreach (var s in set) return s;
            return "";
        }

        // ── Переключение «колонки» вариантов у всех сущностей разом (листание прогонов) ──

        /// <summary>Общий индекс активного варианта всех сущностей; −1, если выбор разъехался.</summary>
        public static int CommonActiveIndex()
        {
            int common = -1;
            foreach (var e in Library.entities)
            {
                if (e == null || e.variants.Count == 0) continue;
                int idx = -1;
                for (int i = 0; i < e.variants.Count; i++)
                    if (e.variants[i] != null && e.variants[i].id == e.activeVariantId) { idx = i; break; }

                if (common == -1) common = idx;
                else if (common != idx) return -1;
            }
            return common;
        }

        /// <summary>Максимальное число вариантов среди всех сущностей (сколько «колонок» рисовать).</summary>
        public static int MaxVariantCount()
        {
            int max = 0;
            foreach (var e in Library.entities)
                if (e?.variants != null && e.variants.Count > max) max = e.variants.Count;
            return max;
        }

        /// <summary>
        /// Сделать i-й вариант активным у ВСЕХ сущностей (у кого меньше — последний).
        /// Возвращает entityId → спрайт: вызывающий синхронизирует building.icon и т.п.
        /// </summary>
        public static Dictionary<string, Sprite> SetAllActiveIndex(int index)
        {
            var lib = Library;
            var result = new Dictionary<string, Sprite>();
            foreach (var e in lib.entities)
            {
                if (e == null || e.variants.Count == 0) continue;
                var variant = e.variants[Mathf.Min(index, e.variants.Count - 1)];
                if (variant == null) continue;
                e.activeVariantId = variant.id;
                if (variant.sprite != null) result[e.entityId] = variant.sprite;
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
            return result;
        }

        // ── Двухэтапная очистка ──

        /// <summary>Этап 1: удалить PNG-картинки (рецепты живы — можно перегенерить). Освобождает диск.</summary>
        public static int DeleteImages(string entityId, bool keepActive)
        {
            var history = Library.Find(entityId);
            if (history == null) return 0;

            int deleted = 0;
            foreach (var v in history.variants)
            {
                if (v == null || v.imageDeleted) continue;
                if (keepActive && v.id == history.activeVariantId) continue;
                if (!string.IsNullOrEmpty(v.pngAssetPath) &&
                    AssetDatabase.LoadAssetAtPath<Sprite>(v.pngAssetPath) != null)
                {
                    AssetDatabase.DeleteAsset(v.pngAssetPath);
                }
                v.sprite = null;
                v.imageDeleted = true;
                deleted++;
            }
            EditorUtility.SetDirty(Library);
            AssetDatabase.SaveAssetIfDirty(Library);
            Bump();
            return deleted;
        }

        /// <summary>
        /// Удалить ТОЛЬКО картинки выбранных вариантов, рецепты оставить: вариант
        /// остаётся в ленте и восстанавливается по рецепту в тот же путь. Это первый
        /// этап чистки для точечного случая «эта картинка не нужна, но рецепт жалко».
        /// Возвращает, сколько картинок удалено.
        /// </summary>
        public static int DeleteVariantImages(IReadOnlyList<(string entityId, string variantId)> set)
        {
            if (set == null || set.Count == 0) return 0;

            int deleted = 0;
            foreach (var (entityId, variantId) in set)
            {
                var v = Library.Find(entityId)?.variants?.Find(x => x != null && x.id == variantId);
                if (v == null) continue;

                if (!string.IsNullOrEmpty(v.pngAssetPath) &&
                    AssetDatabase.LoadAssetAtPath<Object>(v.pngAssetPath) != null)
                {
                    AssetDatabase.DeleteAsset(v.pngAssetPath);
                }
                v.sprite = null;
                v.imageDeleted = true;
                deleted++;
            }

            if (deleted > 0)
            {
                EditorUtility.SetDirty(Library);
                AssetDatabase.SaveAssetIfDirty(Library);
                Bump();
            }
            return deleted;
        }

        /// <summary>Этап 2: удалить рецепты (историю целиком). Необратимо — воспроизвести уже нельзя.</summary>
        public static void DeleteRecipes(string entityId)
        {
            var lib = Library;
            var history = lib.Find(entityId);
            if (history == null) return;

            // на всякий случай снести оставшиеся PNG
            foreach (var v in history.variants)
                if (v != null && !v.imageDeleted && !string.IsNullOrEmpty(v.pngAssetPath))
                    AssetDatabase.DeleteAsset(v.pngAssetPath);

            lib.entities.Remove(history);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
        }

        /// <summary>
        /// Слить истории вариантов source-сущностей в target (миграция «слоты окон →
        /// сущности»). Варианты переносятся с их старыми pngAssetPath (файлы не двигаем —
        /// рецепты и спрайты остаются валидными), активным становится актив ПЕРВОГО
        /// источника с активом. Источники удаляются из библиотеки.
        /// </summary>
        public static int MergeHistories(string[] sourceIds, string targetId)
        {
            var lib = Library;
            var target = lib.Find(targetId);
            if (target == null)
            {
                target = new EntityIconHistory { entityId = targetId };
                lib.entities.Add(target);
            }

            int moved = 0;
            foreach (var sourceId in sourceIds)
            {
                var source = lib.Find(sourceId);
                if (source == null || source == target) continue;

                foreach (var v in source.variants)
                {
                    if (v == null) continue;
                    target.variants.Add(v);
                    moved++;
                }
                if (string.IsNullOrEmpty(target.activeVariantId) &&
                    !string.IsNullOrEmpty(source.activeVariantId))
                    target.activeVariantId = source.activeVariantId;

                lib.entities.Remove(source);
            }

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
            return moved;
        }

        // ── Паки (снапшоты выбора) ──

        // Кэш: FindAssets на каждый кадр из трёх окон — дорого. Сбрасывается при
        // создании пака и при любом импорте/удалении ассетов (IconGenAssetWatcher).
        private static List<IconPack> _packsCache;

        [InitializeOnLoadMethod]
        private static void SubscribeToAssetChanges()
            => IconGenAssetWatcher.AssetsChanged += () => { _packsCache = null; Bump(); };

        public static List<IconPack> AllPacks()
        {
            if (_packsCache != null)
            {
                _packsCache.RemoveAll(p => p == null);   // ассет могли удалить руками
                return _packsCache;
            }

            _packsCache = new List<IconPack>();
            foreach (var guid in AssetDatabase.FindAssets("t:IconPack"))
                _packsCache.Add(AssetDatabase.LoadAssetAtPath<IconPack>(AssetDatabase.GUIDToAssetPath(guid)));
            return _packsCache;
        }

        /// <summary>
        /// Сохранить текущие активные варианты как именованный пак.
        /// entityIds — область пака (сущности вкладки студии); null — все сущности.
        /// </summary>
        public static IconPack SavePackFromActive(string packName, HashSet<string> entityIds = null)
        {
            EnsureFolder(PacksFolder);
            var pack = ScriptableObject.CreateInstance<IconPack>();
            pack.packName = packName;
            foreach (var e in Library.entities)
                if (e != null && !string.IsNullOrEmpty(e.activeVariantId)
                    && (entityIds == null || entityIds.Contains(e.entityId)))
                    pack.Set(e.entityId, e.activeVariantId);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{PacksFolder}/IconPack_{Sanitize(packName)}.asset");
            AssetDatabase.CreateAsset(pack, path);
            AssetDatabase.SaveAssets();
            _packsCache = null;
            Bump();
            return pack;
        }

        /// <summary>
        /// Применить пак: проставить активные варианты в библиотеке. Возвращает карту
        /// entityId → выбранный спрайт (вызывающий синхронит building.icon и т.п.).
        /// </summary>
        public static Dictionary<string, Sprite> ApplyPack(IconPack pack)
        {
            var result = new Dictionary<string, Sprite>();
            if (pack == null) return result;

            var lib = Library;
            foreach (var sel in pack.selections)
            {
                if (sel == null) continue;
                var history = lib.Find(sel.entityId);
                var variant = history?.Find(sel.variantId);
                if (history == null || variant == null) continue;
                history.activeVariantId = sel.variantId;
                result[sel.entityId] = variant.sprite;
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
            return result;
        }

        /// <summary>Совпадает ли пак с текущим выбором активных вариантов (для показа в UI).</summary>
        public static bool PackMatchesCurrent(IconPack pack)
        {
            if (pack == null || pack.selections.Count == 0) return false;
            var lib = Library;
            foreach (var sel in pack.selections)
            {
                if (sel == null) continue;
                var history = lib.Find(sel.entityId);
                if (history == null || history.activeVariantId != sel.variantId) return false;
            }
            return true;
        }

        // ── Чистка «вокруг пака»: пак — эталон, всё остальное у его сущностей — расходники ──

        /// <summary>
        /// Этап 1: удалить PNG всех вариантов сущностей пака, КРОМЕ выбранных в нём.
        /// Рецепты живы — любой вариант можно перегенерировать. Активным становится паковский.
        /// Сущности вне пака не трогаются.
        /// </summary>
        public static int DeleteImagesExceptPack(IconPack pack)
        {
            if (pack == null) return 0;
            var lib = Library;
            int deleted = 0;

            foreach (var sel in pack.selections)
            {
                if (sel == null) continue;
                var history = lib.Find(sel.entityId);
                if (history == null) continue;

                foreach (var v in history.variants)
                {
                    if (v == null || v.imageDeleted || v.id == sel.variantId) continue;
                    if (!string.IsNullOrEmpty(v.pngAssetPath) &&
                        AssetDatabase.LoadAssetAtPath<Sprite>(v.pngAssetPath) != null)
                        AssetDatabase.DeleteAsset(v.pngAssetPath);
                    v.sprite = null;
                    v.imageDeleted = true;
                    deleted++;
                }
                history.activeVariantId = sel.variantId;
            }

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
            return deleted;
        }

        /// <summary>
        /// Этап 2: удалить варианты сущностей пака ЦЕЛИКОМ (PNG + рецепты), кроме выбранных
        /// в нём. Необратимо. Сущности вне пака не трогаются.
        /// </summary>
        public static int DeleteVariantsExceptPack(IconPack pack)
        {
            if (pack == null) return 0;
            var lib = Library;
            int removed = 0;

            foreach (var sel in pack.selections)
            {
                if (sel == null) continue;
                var history = lib.Find(sel.entityId);
                if (history == null) continue;

                for (int i = history.variants.Count - 1; i >= 0; i--)
                {
                    var v = history.variants[i];
                    if (v == null || v.id == sel.variantId) continue;
                    if (!v.imageDeleted && !string.IsNullOrEmpty(v.pngAssetPath) &&
                        AssetDatabase.LoadAssetAtPath<Sprite>(v.pngAssetPath) != null)
                        AssetDatabase.DeleteAsset(v.pngAssetPath);
                    history.variants.RemoveAt(i);
                    removed++;
                }
                history.activeVariantId = sel.variantId;
            }

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            Bump();
            return removed;
        }

        // Кэш «какой пак совпадает с текущим выбором»: перебор паков×выборов на каждый
        // кадр не нужен — выбор меняется только через мутации стора (Version).
        private static int _matchCacheVersion = -1;
        private static IconPack _matchedPack;

        /// <summary>Пак, совпадающий с текущим выбором активных вариантов; null — выбор свой.</summary>
        public static IconPack PackMatchingCurrent()
        {
            if (_matchCacheVersion == Version) return _matchedPack;
            _matchCacheVersion = Version;
            _matchedPack = null;
            foreach (var p in AllPacks())
                if (PackMatchesCurrent(p)) { _matchedPack = p; break; }
            return _matchedPack;
        }

        /// <summary>
        /// Паки области: все сущности пака лежат в entityIds (паки вкладки студии).
        /// Пак иконок зданий не показывается во вкладке UI и наоборот; старые «полные»
        /// паки видны только там, где вкладка покрывает все их сущности.
        /// </summary>
        public static List<IconPack> PacksFor(HashSet<string> entityIds)
        {
            var result = new List<IconPack>();
            if (entityIds == null || entityIds.Count == 0) return result;

            foreach (var p in AllPacks())
            {
                if (p == null || p.selections == null || p.selections.Count == 0) continue;
                bool subset = true;
                foreach (var sel in p.selections)
                    if (sel == null || !entityIds.Contains(sel.entityId)) { subset = false; break; }
                if (subset) result.Add(p);
            }
            return result;
        }

        /// <summary>Пак области, совпадающий с текущим выбором; null — выбор свой.</summary>
        public static IconPack PackMatchingCurrent(HashSet<string> entityIds)
        {
            foreach (var p in PacksFor(entityIds))
                if (PackMatchesCurrent(p)) return p;
            return null;
        }

        private static string Sanitize(string s)
            => string.IsNullOrEmpty(s) ? "pack" : System.Text.RegularExpressions.Regex.Replace(s, @"[^\w\-]", "_");

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
