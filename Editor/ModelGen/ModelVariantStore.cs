using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.ModelGen.Editor
{
    /// <summary>
    /// Хранилище вариантов 3D-студии: GLB-файлы под Assets (glTFast импортирует),
    /// история с рецептами в ModelVariantLibrary. Публикация — копия GLB в папку
    /// публикации сущности + запись пути в провайдер.
    /// </summary>
    public static class ModelVariantStore
    {
        /// <summary>Куда создаётся библиотека, если в проекте её ещё нет.</summary>
        public const string DefaultLibraryPath = "Assets/AIStudio/Models/ModelVariantLibrary.asset";

        public static event Action Changed;

        private static ModelVariantLibrary _library;

        public static ModelVariantLibrary Library
        {
            get
            {
                if (_library != null) return _library;

                // Библиотека одна на проект и лежит где угодно — ищем по типу
                var guids = AssetDatabase.FindAssets("t:" + nameof(ModelVariantLibrary));
                if (guids.Length > 0)
                {
                    Array.Sort(guids, (a, b) => string.CompareOrdinal(
                        AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
                    if (guids.Length > 1)
                        Debug.LogWarning($"[ModelStudio] В проекте {guids.Length} ModelVariantLibrary — " +
                                         $"используется {AssetDatabase.GUIDToAssetPath(guids[0])}");
                    _library = AssetDatabase.LoadAssetAtPath<ModelVariantLibrary>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
                }
                if (_library == null)
                {
                    EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath).Replace('\\', '/'));
                    _library = ScriptableObject.CreateInstance<ModelVariantLibrary>();
                    AssetDatabase.CreateAsset(_library, DefaultLibraryPath);
                    AssetDatabase.SaveAssets();
                }
                return _library;
            }
        }

        /// <summary>Корень данных студии — папка ассета библиотеки вариантов.</summary>
        public static string Root
        {
            get
            {
                string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(Library))?.Replace('\\', '/');
                return string.IsNullOrEmpty(dir) ? "Assets/AIStudio/Models" : dir;
            }
        }

        /// <summary>Сгенерированные и вставленные варианты (по папке на сущность).</summary>
        public static string VariantsFolder => Root + "/Variants";

        private static string LibraryFolder(string configured, string fallbackName)
            => string.IsNullOrEmpty(configured) ? Root + "/" + fallbackName : configured.TrimEnd('/');

        /// <summary>Спрайт-листы турнтейбла и анимаций.</summary>
        public static string SheetsFolder => LibraryFolder(Library.sheetsFolder, "Sheets");

        /// <summary>Референсы, вставленные из буфера.</summary>
        public static string RefsFolder => LibraryFolder(Library.refsFolder, "Refs");

        /// <summary>Префабы — если сущность не задала свою папку.</summary>
        public static string DefaultPrefabsFolder => LibraryFolder(Library.prefabsFolder, "Prefabs");

        /// <summary>Публикация моделей — если сущность не задала свою папку.</summary>
        public static string DefaultPublishFolder => LibraryFolder(Library.publishFolder, "Published");

        /// <summary>Существующая опубликованная модель становится вариантом «original».</summary>
        public static void EnsureBaseVariant(ModelContentItem item)
        {
            string published = item.GetPublishedModelPath?.Invoke();
            if (string.IsNullOrEmpty(published) || AssetDatabase.LoadMainAssetAtPath(published) == null) return;
            var hist = Library.Ensure(item.EntityId);
            if (hist.variants.Count > 0) return;
            hist.variants.Add(new ModelVariant
            {
                id = "original",
                assetPath = published,
                createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            });
            hist.activeId = "original";
            Save();
        }

        /// <summary>Сохранить результат генерации как новый вариант (и сделать активным).</summary>
        public static ModelVariant AddVariant(ModelGenResult result, bool makeActive = true)
        {
            var req = result.Request;
            string dir = VariantsFolder + "/" + req.EntityId;
            EnsureFolder(dir);
            string variantId = $"{req.Engine.ToString().ToLower()}_{req.Seed}_{DateTime.Now:yyMMdd_HHmmss}";
            string assetPath = $"{dir}/{variantId}.glb";
            File.WriteAllBytes(assetPath, result.GlbBytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var hist = Library.Ensure(req.EntityId);
            var variant = new ModelVariant
            {
                id = variantId,
                assetPath = assetPath,
                engine = req.Engine,
                prompt = req.Prompt,
                refImagePath = ToAssetsPath(req.RefImagePath),
                seed = req.Seed,
                steps = req.Steps,
                guidance = req.Guidance,
                octreeRes = req.OctreeRes,
                maxFaces = req.MaxFaces,
                createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            hist.variants.Add(variant);
            if (makeActive || string.IsNullOrEmpty(hist.activeId)) hist.activeId = variantId;
            Save();
            return variant;
        }

        /// <summary>
        /// Взять готовый файл модели со стороны (скачан из Meshy, слеплен руками)
        /// и завести его вариантом сущности. Рецепта у такого варианта нет —
        /// сохраняем текущий промпт и референс, чтобы история осталась читаемой.
        /// </summary>
        public static ModelVariant ImportExternal(ModelContentItem item, string sourcePath,
                                                  bool makeActive = true)
        {
            if (item == null || string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                return null;

            string dir = VariantsFolder + "/" + item.EntityId;
            EnsureFolder(dir);
            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            string variantId = $"imported_{Sanitize(Path.GetFileNameWithoutExtension(sourcePath))}_" +
                               DateTime.Now.ToString("yyMMdd_HHmmss");
            string assetPath = $"{dir}/{variantId}{ext}";

            File.Copy(sourcePath, assetPath, true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            {
                AssetDatabase.DeleteAsset(assetPath);
                Debug.LogError($"[ModelStudio] Unity не смог импортировать {sourcePath}");
                return null;
            }

            var hist = Library.Ensure(item.EntityId);
            var variant = new ModelVariant
            {
                id = variantId,
                assetPath = assetPath,
                engine = ModelEngine.Imported,
                prompt = item.GetPrompt?.Invoke(),
                refImagePath = ToAssetsPath(item.GetReferenceImagePath?.Invoke()),
                createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            hist.variants.Add(variant);
            if (makeActive || string.IsNullOrEmpty(hist.activeId)) hist.activeId = variantId;
            Save();
            return variant;
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "model";
            var chars = name.Trim().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
            string s = new string(chars).Trim('_');
            if (s.Length > 24) s = s.Substring(0, 24);
            return string.IsNullOrEmpty(s) ? "model" : s;
        }

        /// <summary>Публикация: копия GLB в папку публикации + путь в провайдер.</summary>
        public static string Publish(ModelContentItem item, ModelVariant variant)
        {
            string folder = item.GetPublishFolder?.Invoke();
            if (string.IsNullOrEmpty(folder)) folder = DefaultPublishFolder;
            EnsureFolder(folder);
            // Расширение берём у варианта: вставленная со стороны модель может быть
            // не GLB (Meshy отдаёт и FBX), а имя с чужим расширением Unity не импортирует
            string ext = Path.GetExtension(variant.assetPath);
            if (string.IsNullOrEmpty(ext)) ext = ".glb";
            string dest = folder + "/" + item.EntityId + ext;

            if (variant.assetPath == dest) return dest; // публикуем уже опубликованное

            // Прежняя публикация могла быть в другом формате — иначе рядом
            // останется мёртвый файл с тем же именем и чужим расширением
            foreach (var old in new[] { ".glb", ".gltf", ".fbx", ".obj" })
                AssetDatabase.DeleteAsset(folder + "/" + item.EntityId + old);
            if (!AssetDatabase.CopyAsset(variant.assetPath, dest))
            {
                Debug.LogError($"[ModelStudio] Не удалось скопировать {variant.assetPath} → {dest}");
                return null;
            }
            item.SetPublishedModelPath?.Invoke(dest);
            var hist = Library.Ensure(item.EntityId);
            hist.activeId = variant.id;
            Save();
            return dest;
        }

        public static void DeleteVariant(string entityId, ModelVariant variant)
        {
            var hist = Library.Get(entityId);
            if (hist == null) return;
            // файлы вне папки Variants (например original) не трогаем
            if (variant.assetPath != null && variant.assetPath.StartsWith(VariantsFolder))
                AssetDatabase.DeleteAsset(variant.assetPath);
            hist.variants.Remove(variant);
            if (hist.activeId == variant.id)
                hist.activeId = hist.variants.LastOrDefault()?.id;
            Save();
        }

        private static void Save()
        {
            EditorUtility.SetDirty(Library);
            AssetDatabase.SaveAssetIfDirty(Library);
            Changed?.Invoke();
        }

        private static string ToAssetsPath(string absOrAssets)
        {
            if (string.IsNullOrEmpty(absOrAssets)) return null;
            absOrAssets = absOrAssets.Replace('\\', '/');
            int i = absOrAssets.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            if (absOrAssets.StartsWith("Assets/")) return absOrAssets;
            return i >= 0 ? absOrAssets[(i + 1)..] : absOrAssets;
        }

        public static void EnsureFolder(string assetsPath)
        {
            if (AssetDatabase.IsValidFolder(assetsPath)) return;
            string[] parts = assetsPath.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
