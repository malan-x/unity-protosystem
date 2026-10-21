using System;
using System.Collections.Generic;
using ProtoSystem.Sound;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// История вариантов звука: обёртка над AudioVariantLibrary.asset — добавление вариантов
    /// с рецептами, выбор активного (публикация в провайдера), пометки, двухэтапная чистка.
    /// Зеркало IconVariantStore (без постобработки).
    /// </summary>
    public static class AudioVariantStore
    {
        /// <summary>Куда создаётся библиотека, если в проекте её ещё нет.</summary>
        public const string DefaultLibraryPath = "Assets/AIStudio/Audio/AudioVariantLibrary.asset";

        /// <summary>Растёт при каждой мутации — окна перерисовываются по Changed.</summary>
        public static int Version { get; private set; }

        public static event Action Changed;

        private static void Bump()
        {
            Version++;
            Changed?.Invoke();
        }

        private static AudioVariantLibrary _library;

        public static AudioVariantLibrary Library
        {
            get
            {
                if (_library != null) return _library;
                // Библиотека одна на проект и лежит где угодно — ищем по типу
                _library = FindFirst<AudioVariantLibrary>();
                if (_library == null)
                {
                    _library = ScriptableObject.CreateInstance<AudioVariantLibrary>();
                    EnsureFolder(System.IO.Path.GetDirectoryName(DefaultLibraryPath)?.Replace('\\', '/'));
                    AssetDatabase.CreateAsset(_library, DefaultLibraryPath);
                }
                return _library;
            }
        }

        private static SoundLibrary _soundLibrary;

        /// <summary>
        /// SoundLibrary проекта — куда публикуются клипы и откуда студия узнаёт «клип → id».
        /// Первая по пути; null — библиотеки в проекте нет.
        /// </summary>
        public static SoundLibrary SoundLibrary
        {
            get
            {
                if (_soundLibrary == null) _soundLibrary = FindFirst<SoundLibrary>();
                return _soundLibrary;
            }
        }

        private static T FindFirst<T>() where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0) return null;
            Array.Sort(guids, (a, b) => string.CompareOrdinal(
                AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
            if (guids.Length > 1)
                Debug.LogWarning($"[AudioStudio] В проекте {guids.Length} ассетов {typeof(T).Name} — " +
                                 $"используется {AssetDatabase.GUIDToAssetPath(guids[0])}");
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public static string NewVariantId() => Guid.NewGuid().ToString("N").Substring(0, 8);

        public static string VariantWavPath(string folder, string entityId, string variantId)
            => $"{folder}/{entityId}/Snd_{entityId}_{variantId}.wav";

        /// <summary>
        /// Собрать запрос генерации для сущности набора: путь варианта, длительность,
        /// луп, чекпоинт профиля; OnDone кладёт вариант в историю и делает его активным
        /// (write-through клипа в провайдера).
        /// </summary>
        public static AudioGenRequest BuildRequest(AudioItem item, AudioCollection col, string runId)
        {
            // Существующий игровой клип (не из студии) — в историю как «базовый вариант»,
            // иначе после первой генерации оригинал не с чем сравнить и не вернуть.
            EnsureBaseVariant(item);

            string variantId = NewVariantId();
            string path = VariantWavPath(col.WavFolder, item.EntityId, variantId);
            var profile = col.SourceAsset != null ? col.SourceAsset.profile : null;

            float seconds = item.Seconds > 0f ? item.Seconds : col.Seconds;

            return new AudioGenRequest
            {
                Prompt = item.GetPrompt?.Invoke() ?? "",
                AssetPath = path,
                DisplayName = item.DisplayName,
                Seconds = seconds,
                Loop = item.Loop,
                Style = profile != null ? profile.style : null,
                CheckpointOverride = profile != null ? profile.checkpoint : null,
                RunId = runId,
                UndoTarget = item.UndoTarget,
                UndoLabel = $"AI звук {item.DisplayName}",
                OnDone = (clip, result) =>
                {
                    // Публикуем автоматически ТОЛЬКО если у звука ещё нет клипа в игре —
                    // существующий (оригинал или выбранный вариант) генерация не трогает,
                    // публикация — явный двойной клик в пикере.
                    bool hadClip = item.GetClip?.Invoke() != null;
                    AddVariant(item.EntityId, variantId, clip, result, makeActive: !hadClip);
                    if (!hadClip) item.SetClip?.Invoke(clip);
                },
            };
        }

        /// <summary>
        /// Занести текущий игровой клип сущности в историю как «базовый вариант» (id
        /// "original", без рецепта). Клипы из папки вариантов студии не считаются базой.
        /// </summary>
        public static void EnsureBaseVariant(AudioItem item)
        {
            var clip = item.GetClip?.Invoke();
            if (clip == null) return;

            string path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path) || path.Contains("/Variants/")) return;

            var history = Library.GetOrCreate(item.EntityId);
            if (history.Find("original") != null) return;

            history.variants.Insert(0, new AudioVariant
            {
                id = "original",
                recipe = null,          // не сгенерирован — перегенерация недоступна
                clip = clip,
                wavAssetPath = path,
            });
            if (string.IsNullOrEmpty(history.activeVariantId))
                history.activeVariantId = "original";

            EditorUtility.SetDirty(Library);
            Bump();
        }

        public static void AddVariant(string entityId, string variantId, AudioClip clip,
                                      AudioGenResult result, bool makeActive = true)
        {
            var history = Library.GetOrCreate(entityId);
            history.variants.Add(new AudioVariant
            {
                id = variantId,
                recipe = ToRecipe(result),
                clip = clip,
                wavAssetPath = result.WavAssetPath,
            });
            if (makeActive) history.activeVariantId = variantId;

            EditorUtility.SetDirty(Library);
            AssetDatabase.SaveAssetIfDirty(Library);
            Bump();
        }

        /// <summary>
        /// Вариант из мини-редактора обрезки: без рецепта (как «original») —
        /// перегенерация недоступна, источник правды — сам WAV.
        /// </summary>
        public static void AddTrimmedVariant(string entityId, string variantId, AudioClip clip,
                                             string wavAssetPath)
        {
            var history = Library.GetOrCreate(entityId);
            history.variants.Add(new AudioVariant
            {
                id = variantId,
                recipe = null,
                clip = clip,
                wavAssetPath = wavAssetPath,
            });

            EditorUtility.SetDirty(Library);
            AssetDatabase.SaveAssetIfDirty(Library);
            Bump();
        }

        public static AudioRecipe ToRecipe(in AudioGenResult r) => new()
        {
            subject = r.Subject,
            positive = r.Positive,
            negative = r.Negative,
            lyrics = r.Lyrics,
            lyricsStrength = r.LyricsStrength,
            engine = r.Engine,
            seconds = r.Seconds,
            seed = r.Seed,
            steps = r.Steps,
            cfg = r.Cfg,
            sampler = r.Sampler,
            scheduler = r.Scheduler,
            checkpoint = r.Checkpoint,
            styleName = r.StyleName,
            trimSilence = r.TrimSilence,
            runId = r.RunId,
            generatedAtUtc = r.GeneratedAtUtc,
            voiceId = r.VoiceId,
            ttsModelId = r.TtsModelId,
            ttsStability = r.TtsStability,
            ttsSimilarity = r.TtsSimilarity,
            ttsLanguage = r.TtsLanguage,
            postFilter = r.PostFilter,
            targetLufs = r.TargetLufs,
        };

        /// <summary>Запрос ComfyUI из рецепта — перегенерация точь-в-точь (минуя стиль).</summary>
        public static ComfyAudioClient.AudioRequest RecipeToRequest(AudioRecipe r) => new()
        {
            Engine = (AudioEngine)r.engine,
            Positive = r.positive,
            Negative = r.negative,
            Lyrics = r.lyrics,
            LyricsStrength = r.lyricsStrength,
            Seconds = r.seconds,
            Seed = r.seed,
            Steps = r.steps,
            Cfg = r.cfg,
            Sampler = r.sampler,
            Scheduler = r.scheduler,
            CheckpointOverride = r.checkpoint,
            HasModelOverride = !string.IsNullOrEmpty(r.checkpoint),
            VoiceId = r.voiceId,
            TtsModelId = r.ttsModelId,
            TtsStability = r.ttsStability,
            TtsSimilarity = r.ttsSimilarity,
            TtsLanguage = r.ttsLanguage,
            PostFilter = r.postFilter,
            TargetLufs = r.targetLufs,
        };

        /// <summary>Сделать вариант активным + write-through клипа в провайдера.</summary>
        public static void SetActive(string entityId, string variantId, AudioItem item)
        {
            var history = Library.Find(entityId);
            var variant = history?.Find(variantId);
            if (variant == null) return;

            history.activeVariantId = variantId;
            if (variant.clip != null && item != null)
            {
                if (item.UndoTarget != null) Undo.RecordObject(item.UndoTarget, "Активный вариант звука");
                item.SetClip?.Invoke(variant.clip);
                if (item.UndoTarget != null)
                {
                    EditorUtility.SetDirty(item.UndoTarget);
                    AssetDatabase.SaveAssetIfDirty(item.UndoTarget);
                }
            }

            EditorUtility.SetDirty(Library);
            AssetDatabase.SaveAssetIfDirty(Library);
            Bump();
        }

        public static void MarkBad(string entityId, string variantId, bool bad)
        {
            var v = Library.Find(entityId)?.Find(variantId);
            if (v == null) return;
            v.bad = bad;
            EditorUtility.SetDirty(Library);
            Bump();
        }

        /// <summary>
        /// Файл варианта принадлежит студии (папка Variants)? Только такие можно удалять
        /// с диска: «базовый вариант» ссылается на оригинальный игровой ассет.
        /// </summary>
        private static bool IsStudioFile(AudioVariant v)
            => !string.IsNullOrEmpty(v.wavAssetPath) && v.wavAssetPath.Contains("/Variants/");

        /// <summary>Удалить вариант целиком (WAV + рецепт). Оригинальный файл игры не трогается.</summary>
        public static void DeleteVariant(string entityId, string variantId)
        {
            var history = Library.Find(entityId);
            var v = history?.Find(variantId);
            if (v == null) return;

            if (IsStudioFile(v) && !v.fileDeleted)
                AssetDatabase.DeleteAsset(v.wavAssetPath);

            history.variants.Remove(v);
            if (history.activeVariantId == variantId) history.activeVariantId = "";

            EditorUtility.SetDirty(Library);
            AssetDatabase.SaveAssetIfDirty(Library);
            Bump();
        }

        /// <summary>Групповое удаление вариантов (WAV студии + рецепты) — один Bump на пачку.</summary>
        public static int DeleteVariants(string entityId, List<string> variantIds)
        {
            var history = Library.Find(entityId);
            if (history == null) return 0;

            int removed = 0;
            foreach (var id in variantIds)
            {
                var v = history.Find(id);
                if (v == null) continue;

                if (IsStudioFile(v) && !v.fileDeleted)
                    AssetDatabase.DeleteAsset(v.wavAssetPath);

                history.variants.Remove(v);
                if (history.activeVariantId == id) history.activeVariantId = "";
                removed++;
            }

            if (removed > 0)
            {
                EditorUtility.SetDirty(Library);
                AssetDatabase.SaveAssetIfDirty(Library);
                Bump();
            }
            return removed;
        }

        /// <summary>
        /// Этап 1 чистки: удалить WAV-файлы неактивных вариантов сущности — рецепты живут,
        /// любой вариант восстановим перегенерацией. Этап 2 — DeleteRecipes.
        /// </summary>
        public static int DeleteFiles(string entityId, bool keepActive = true)
        {
            var history = Library.Find(entityId);
            if (history == null) return 0;

            int deleted = 0;
            foreach (var v in history.variants)
            {
                if (keepActive && v.id == history.activeVariantId) continue;
                if (v.fileDeleted || !IsStudioFile(v)) continue;

                if (AssetDatabase.DeleteAsset(v.wavAssetPath))
                {
                    v.clip = null;
                    v.fileDeleted = true;
                    deleted++;
                }
            }

            if (deleted > 0)
            {
                EditorUtility.SetDirty(Library);
                AssetDatabase.SaveAssetIfDirty(Library);
                Bump();
            }
            return deleted;
        }

        /// <summary>Этап 2 чистки: выкинуть записи вариантов без файлов (рецепты пропадают).</summary>
        public static int DeleteRecipes(string entityId)
        {
            var history = Library.Find(entityId);
            if (history == null) return 0;

            int removed = history.variants.RemoveAll(v => v.fileDeleted || v.clip == null);
            if (removed > 0)
            {
                EditorUtility.SetDirty(Library);
                AssetDatabase.SaveAssetIfDirty(Library);
                Bump();
            }
            return removed;
        }

        /// <summary>Опубликовать выбранные варианты в рандом-пул сущности (SoundEntry.clipVariants).</summary>
        public static void PublishClipVariants(AudioItem item, List<string> variantIds)
        {
            if (item?.SetClipVariants == null) return;
            var history = Library.Find(item.EntityId);
            if (history == null) return;

            var clips = new List<AudioClip>();
            foreach (var id in variantIds)
            {
                var v = history.Find(id);
                if (v?.clip != null) clips.Add(v.clip);
            }

            if (item.UndoTarget != null) Undo.RecordObject(item.UndoTarget, "Рандом-пул звука");
            item.SetClipVariants(clips.ToArray());
            if (item.UndoTarget != null)
            {
                EditorUtility.SetDirty(item.UndoTarget);
                AssetDatabase.SaveAssetIfDirty(item.UndoTarget);
            }
            Bump();
        }

        // ── Паки звуков (снапшоты активных вариантов) ──

        /// <summary>Паки лежат в папке Packs рядом с библиотекой вариантов.</summary>
        public static string PacksFolder
        {
            get
            {
                string dir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(Library))?.Replace('\\', '/');
                return (string.IsNullOrEmpty(dir) ? "Assets/AIStudio/Audio" : dir) + "/Packs";
            }
        }

        private static List<AudioPack> _packsCache;

        [InitializeOnLoadMethod]
        private static void HookPackCacheInvalidation()
            => ProtoSystem.AudioGen.Editor.AudioGenAssetWatcher.AssetsChanged += () => _packsCache = null;

        public static IReadOnlyList<AudioPack> AllPacks()
        {
            if (_packsCache != null)
            {
                _packsCache.RemoveAll(p => p == null);
                return _packsCache;
            }
            _packsCache = new List<AudioPack>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioPack"))
            {
                var p = AssetDatabase.LoadAssetAtPath<AudioPack>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null) _packsCache.Add(p);
            }
            return _packsCache;
        }

        /// <summary>Снапшот ТЕКУЩИХ активных вариантов всех сущностей в новый пак-ассет.</summary>
        public static AudioPack SavePackFromActive(string packName)
        {
            EnsureFolder(PacksFolder);
            var pack = ScriptableObject.CreateInstance<AudioPack>();
            pack.packName = packName;

            foreach (var history in Library.entities)
            {
                if (history == null || string.IsNullOrEmpty(history.activeVariantId)) continue;
                pack.selections.Add(new AudioPack.Selection
                {
                    entityId = history.entityId,
                    variantId = history.activeVariantId,
                });
            }

            string file = System.Text.RegularExpressions.Regex.Replace(packName, @"[^\w\-]+", "_");
            AssetDatabase.CreateAsset(pack,
                AssetDatabase.GenerateUniqueAssetPath($"{PacksFolder}/AudioPack_{file}.asset"));
            AssetDatabase.SaveAssets();
            _packsCache = null;
            return pack;
        }

        /// <summary>Пак совпадает с текущим выбором (для галочки в меню)?</summary>
        public static bool PackMatchesCurrent(AudioPack pack)
        {
            if (pack == null || pack.selections.Count == 0) return false;
            foreach (var s in pack.selections)
            {
                var history = Library.Find(s.entityId);
                if (history == null || history.activeVariantId != s.variantId) return false;
            }
            return true;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
