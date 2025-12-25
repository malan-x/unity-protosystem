using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProtoSystem.Effects;

namespace ProtoSystem.Effects.Editor
{
    /// <summary>
    /// Кастомный редактор для EffectContainer с поддержкой поиска и фильтрации по тегам
    /// </summary>
    [CustomEditor(typeof(EffectContainer))]
    public class EffectContainerEditor : UnityEditor.Editor
    {
        private EffectContainer container;
        private string searchText = "";
        private string selectedTag = "";
        private bool showTagFilter = false;
        private Vector2 scrollPosition;

        private static string FormatEventPath(string eventPath)
        {
            if (string.IsNullOrWhiteSpace(eventPath)) return "(не задан)";
            return eventPath.Replace('.', '/');
        }

        private static void MarkEffectDirty(EffectConfig effect, string undoLabel)
        {
            Undo.RecordObject(effect, undoLabel);
            EditorUtility.SetDirty(effect);
        }

        private void OnEnable()
        {
            container = (EffectContainer)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawCustomHeader();
            DrawContainerInfo();
            DrawSearchAndFilter();
            DrawAddEffectButtons();
            DrawEffectsList();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCustomHeader()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("🎭 Effect Container Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();
        }

        private void DrawContainerInfo()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("containerName"), new GUIContent("📦 Название"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"), new GUIContent("📝 Описание"));
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawSearchAndFilter()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Поиск по имени/ID
            EditorGUILayout.LabelField("🔍 Поиск эффектов", EditorStyles.miniBoldLabel);
            searchText = EditorGUILayout.TextField("Поиск", searchText);

            // Фильтр по тегам
            showTagFilter = EditorGUILayout.Foldout(showTagFilter, "🏷️ Фильтр по тегам");
            if (showTagFilter)
            {
                DrawTagFilter();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawAddEffectButtons()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("➕ Управление эффектами", EditorStyles.boldLabel);

            // === ДОБАВИТЬ СУЩЕСТВУЮЩИЙ ===
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📂 Существующий эффект:", GUILayout.Width(140));
            if (GUILayout.Button("▼ Выбрать из проекта"))
            {
                ShowExistingEffectsMenu();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // === СОЗДАТЬ НОВЫЙ ===
            EditorGUILayout.LabelField("🆕 Создать новый эффект:", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🎨 VFX", EditorStyles.miniButtonLeft))
            {
                CreateNewEffectInFolder(EffectConfig.EffectType.VFX);
            }

            if (GUILayout.Button("🔊 Audio", EditorStyles.miniButtonMid))
            {
                CreateNewEffectInFolder(EffectConfig.EffectType.Audio);
            }

            if (GUILayout.Button("🖼️ UI", EditorStyles.miniButtonMid))
            {
                CreateNewEffectInFolder(EffectConfig.EffectType.UI);
            }

            if (GUILayout.Button("📺 Screen", EditorStyles.miniButtonMid))
            {
                CreateNewEffectInFolder(EffectConfig.EffectType.ScreenEffect);
            }

            if (GUILayout.Button("🎭 Combined", EditorStyles.miniButtonRight))
            {
                CreateNewEffectInFolder(EffectConfig.EffectType.Combined);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Показывает меню со списком эффектов, которых ещё нет в контейнере
        /// </summary>
        private void ShowExistingEffectsMenu()
        {
            var menu = new GenericMenu();

            // Получаем все EffectConfig в проекте
            var allEffectGuids = AssetDatabase.FindAssets("t:EffectConfig");
            var existingIds = new HashSet<string>(container.Effects.Where(e => e != null).Select(e => AssetDatabase.GetAssetPath(e)));

            var availableEffects = new List<EffectConfig>();
            foreach (var guid in allEffectGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!existingIds.Contains(path))
                {
                    var effect = AssetDatabase.LoadAssetAtPath<EffectConfig>(path);
                    if (effect != null)
                    {
                        availableEffects.Add(effect);
                    }
                }
            }

            if (availableEffects.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("Все эффекты уже добавлены"));
            }
            else
            {
                // Группируем по категории
                var grouped = availableEffects
                    .GroupBy(e => e.category)
                    .OrderBy(g => g.Key.ToString());

                foreach (var group in grouped)
                {
                    var categoryIcon = GetCategoryIcon(group.Key);
                    foreach (var effect in group.OrderBy(e => e.effectId))
                    {
                        var effectRef = effect; // Локальная копия для замыкания
                        var displayName = string.IsNullOrEmpty(effect.displayName) ? effect.effectId : effect.displayName;
                        menu.AddItem(
                            new GUIContent($"{categoryIcon} {group.Key}/{displayName} ({effect.effectId})"),
                            false,
                            () => AddExistingEffect(effectRef)
                        );
                    }
                }

                menu.AddSeparator("");
                menu.AddItem(new GUIContent($"Добавить все ({availableEffects.Count})"), false, () => AddAllAvailableEffects(availableEffects));
            }

            menu.ShowAsContext();
        }

        private void AddExistingEffect(EffectConfig effect)
        {
            container.AddEffect(effect);
            EditorUtility.SetDirty(container);
            Debug.Log($"[EffectContainer] Добавлен эффект: {effect.effectId}");
        }

        private void AddAllAvailableEffects(List<EffectConfig> effects)
        {
            foreach (var effect in effects)
            {
                container.AddEffect(effect);
            }
            EditorUtility.SetDirty(container);
            Debug.Log($"[EffectContainer] Добавлено {effects.Count} эффектов");
        }

        private string GetCategoryIcon(EffectCategory category)
        {
            return category switch
            {
                EffectCategory.Spatial => "🎨",
                EffectCategory.Audio => "🔊",
                EffectCategory.Screen => "📺",
                _ => "❓"
            };
        }

        /// <summary>
        /// Создаёт новый эффект в стандартной папке Assets/Settings/Effects/
        /// </summary>
        private void CreateNewEffectInFolder(EffectConfig.EffectType effectType)
        {
            // Стандартная папка для эффектов
            const string effectsFolder = "Assets/Settings/Effects";
            
            // Создаём папку если не существует
            if (!AssetDatabase.IsValidFolder(effectsFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                {
                    AssetDatabase.CreateFolder("Assets", "Settings");
                }
                AssetDatabase.CreateFolder("Assets/Settings", "Effects");
            }

            // Определяем подпапку по типу
            string subFolder = effectType switch
            {
                EffectConfig.EffectType.VFX => "VFX",
                EffectConfig.EffectType.Particle => "VFX",
                EffectConfig.EffectType.Audio => "Audio",
                EffectConfig.EffectType.UI => "UI",
                EffectConfig.EffectType.ScreenEffect => "Screen",
                EffectConfig.EffectType.Combined => "Combined",
                _ => "Other"
            };

            string fullFolder = $"{effectsFolder}/{subFolder}";
            if (!AssetDatabase.IsValidFolder(fullFolder))
            {
                AssetDatabase.CreateFolder(effectsFolder, subFolder);
            }

            // Диалог для имени эффекта
            var effectName = ShowInputDialog("Создание эффекта", "Введите ID эффекта:", $"new_{effectType.ToString().ToLower()}");
            if (string.IsNullOrEmpty(effectName)) return;

            // Проверяем уникальность
            string assetPath = $"{fullFolder}/{effectName}.asset";
            if (AssetDatabase.LoadAssetAtPath<EffectConfig>(assetPath) != null)
            {
                EditorUtility.DisplayDialog("Ошибка", $"Эффект с ID '{effectName}' уже существует!", "OK");
                return;
            }

            // Создаём эффект
            var effect = ScriptableObject.CreateInstance<EffectConfig>();
            effect.effectId = effectName;
            effect.effectType = effectType;
            effect.displayName = effectName;
            effect.category = effect.GetAutoCategory();

            AssetDatabase.CreateAsset(effect, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Добавляем в контейнер
            container.AddEffect(effect);
            EditorUtility.SetDirty(container);

            // Выбираем созданный эффект
            Selection.activeObject = effect;
            EditorGUIUtility.PingObject(effect);

            Debug.Log($"[EffectContainer] Создан эффект: {assetPath}");
        }

        private string ShowInputDialog(string title, string message, string defaultValue)
        {
            return EditorInputDialog.Show(title, message, defaultValue);
        }

        private void DrawTagFilter()
        {
            var allTags = container.GetAllTags();
            if (allTags.Count == 0)
            {
                EditorGUILayout.HelpBox("Нет тегов в контейнере", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Выберите тег для фильтрации:");

            // Кнопка "Все теги"
            if (GUILayout.Button("Все теги", selectedTag == "" ? EditorStyles.miniButton : EditorStyles.miniButtonMid))
            {
                selectedTag = "";
            }

            // Кнопки для каждого тега
            foreach (var tag in allTags.OrderBy(t => t))
            {
                if (GUILayout.Button(tag, selectedTag == tag ? EditorStyles.miniButton : EditorStyles.miniButtonMid))
                {
                    selectedTag = tag;
                }
            }

            EditorGUILayout.Space();
        }

        private void DrawEffectsList()
        {
            var filteredEffects = GetFilteredEffects();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"📋 Эффекты ({filteredEffects.Count})", EditorStyles.boldLabel);

            if (filteredEffects.Count == 0)
            {
                if (!string.IsNullOrEmpty(searchText) || !string.IsNullOrEmpty(selectedTag))
                {
                    EditorGUILayout.HelpBox("Эффекты не найдены по заданным критериям", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("Добавьте эффекты в контейнер", MessageType.Info);
                }
            }
            else
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

                foreach (var effect in filteredEffects)
                {
                    DrawEffectItem(effect);
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawEffectItem(EffectConfig effect)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Заголовок (ID + опционально displayName) + тип
            EditorGUILayout.BeginHorizontal();
            var hasDisplayName = !string.IsNullOrWhiteSpace(effect.displayName) && effect.displayName != effect.effectId;
            EditorGUILayout.LabelField(hasDisplayName ? $"🎯 {effect.effectId}  —  {effect.displayName}" : $"🎯 {effect.effectId}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(effect.effectType.ToString(), EditorStyles.miniLabel, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            // Короткое описание
            if (!string.IsNullOrWhiteSpace(effect.description))
            {
                EditorGUILayout.LabelField(effect.description, EditorStyles.wordWrappedMiniLabel);
            }

            // Теги (одной строкой)
            if (effect.tags != null && effect.tags.Length > 0)
            {
                EditorGUILayout.LabelField($"🏷️ {string.Join(", ", effect.tags)}", EditorStyles.miniLabel);
            }

            // Auto-trigger (компактно: Класс/Подкласс)
            if (effect.HasAutoTrigger())
            {
                var startText = FormatEventPath(effect.triggerEventPath);
                var stopText = effect.HasAutoStop() ? FormatEventPath(effect.stopEventPath) : null;
                var line = stopText == null ? $"⚡ {startText}" : $"⚡ {startText}  →  ⏹ {stopText}";
                if (effect.passEventData) line += "  (из данных события)";
                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);

                if (!string.IsNullOrWhiteSpace(effect.triggerCondition))
                {
                    EditorGUILayout.LabelField($"🔍 {effect.triggerCondition}", EditorStyles.miniLabel);
                }
            }

            // Приоритет и настройки прерывания
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"⭐ Приоритет: {effect.priority}", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField($"🔄 Прерываемый: {(effect.canBeInterrupted ? "Да" : "Нет")}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // Специфичные настройки по типу эффекта
            DrawEffectTypeSpecificInfo(effect);

            // Кнопки действий
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Выбрать", EditorStyles.miniButtonLeft))
            {
                Selection.activeObject = effect;
            }

            if (GUILayout.Button("Показать", EditorStyles.miniButtonMid))
            {
                EditorGUIUtility.PingObject(effect);
            }

            if (GUILayout.Button("Удалить", EditorStyles.miniButtonRight))
            {
                if (EditorUtility.DisplayDialog("Удалить эффект",
                    $"Удалить эффект '{effect.effectId}' из контейнера?",
                    "Да", "Отмена"))
                {
                    container.RemoveEffect(effect);
                    EditorUtility.SetDirty(container);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawEffectTypeSpecificInfo(EffectConfig effect)
        {
            switch (effect.effectType)
            {
                case EffectConfig.EffectType.VFX:
                    EditorGUI.BeginChangeCheck();
                    var newVfxPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("🎨 Префаб"), effect.vfxPrefab, typeof(GameObject), false);
                    var newLifetime = EditorGUILayout.FloatField(new GUIContent("⏱️ Время жизни (0 = беск.)"), effect.lifetime);
                    if (EditorGUI.EndChangeCheck())
                    {
                        MarkEffectDirty(effect, "Edit VFX Effect");
                        effect.vfxPrefab = newVfxPrefab;
                        effect.lifetime = newLifetime;
                    }

                    if (effect.vfxPrefab == null)
                    {
                        EditorGUILayout.HelpBox("⚠️ Отсутствует VFX префаб!", MessageType.Warning);
                    }
                    break;

                case EffectConfig.EffectType.Audio:
                    EditorGUI.BeginChangeCheck();
                    var newClip = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("🔊 Клип"), effect.audioClip, typeof(AudioClip), false);
                    var newVolume = EditorGUILayout.Slider(new GUIContent("🔉 Громкость"), effect.volume, 0f, 1f);
                    var newPitch = EditorGUILayout.FloatField(new GUIContent("🎵 Тон"), effect.pitch);
                    var newSpatial = EditorGUILayout.Toggle(new GUIContent("🌐 Пространственный"), effect.spatial);
                    if (EditorGUI.EndChangeCheck())
                    {
                        MarkEffectDirty(effect, "Edit Audio Effect");
                        effect.audioClip = newClip;
                        effect.volume = newVolume;
                        effect.pitch = newPitch;
                        effect.spatial = newSpatial;
                    }

                    if (effect.audioClip == null)
                    {
                        EditorGUILayout.HelpBox("⚠️ Отсутствует Audio клип!", MessageType.Warning);
                    }
                    break;

                case EffectConfig.EffectType.UI:
                    EditorGUI.BeginChangeCheck();
                    var newUiPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("🖼️ Префаб"), effect.uiPrefab, typeof(GameObject), false);
                    var newUiTime = EditorGUILayout.FloatField(new GUIContent("⏱️ Время отображения"), effect.uiDisplayTime);
                    if (EditorGUI.EndChangeCheck())
                    {
                        MarkEffectDirty(effect, "Edit UI Effect");
                        effect.uiPrefab = newUiPrefab;
                        effect.uiDisplayTime = newUiTime;
                    }

                    if (effect.uiPrefab == null)
                    {
                        EditorGUILayout.HelpBox("⚠️ Отсутствует UI префаб!", MessageType.Warning);
                    }
                    break;

                case EffectConfig.EffectType.Combined:
                    EditorGUILayout.LabelField("🎭 Комбинированный эффект (несколько типов)", EditorStyles.miniLabel);
                    break;
            }
        }

        private List<EffectConfig> GetFilteredEffects()
        {
            var effects = container.Effects.ToList();

            // Фильтр по поисковому тексту
            if (!string.IsNullOrEmpty(searchText))
            {
                effects = effects.Where(e =>
                    e.effectId.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(e.displayName) && e.displayName.Contains(searchText, System.StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            // Фильтр по тегу
            if (!string.IsNullOrEmpty(selectedTag))
            {
                effects = effects.Where(e => e.HasTag(selectedTag)).ToList();
            }

            return effects;
        }

        [MenuItem("CONTEXT/EffectContainer/Find Effects by Tag")]
        private static void FindEffectsByTagMenuItem()
        {
            var container = Selection.activeObject as EffectContainer;
            if (container == null) return;

            // Открыть окно поиска по тегам
            EffectTagSearchWindow.Show(container);
        }

        [MenuItem("CONTEXT/EffectContainer/Validate Container")]
        private static void ValidateContainerMenuItem()
        {
            var container = Selection.activeObject as EffectContainer;
            if (container == null) return;

            var isValid = container.IsValid();
            if (isValid)
            {
                EditorUtility.DisplayDialog("Валидация", "Контейнер валиден!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Валидация", "Найдены проблемы в контейнере. Проверьте консоль.", "OK");
            }
        }

        private void CreateNewEffect(EffectConfig.EffectType effectType)
        {
            // Создаем новый EffectConfig
            var effect = ScriptableObject.CreateInstance<EffectConfig>();
            effect.effectType = effectType;
            effect.effectId = $"new_{effectType.ToString().ToLower()}_{container.Count + 1}";
            effect.displayName = $"Новый {effectType} эффект";
            effect.description = $"Описание {effectType.ToString().ToLower()} эффекта";
            effect.tags = new[] { effectType.ToString().ToLower() };

            // Сохраняем как asset
            string path = EditorUtility.SaveFilePanelInProject(
                $"Создать {effectType} эффект",
                effect.effectId,
                "asset",
                $"Создать новый {effectType} эффект");

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(effect, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Добавляем в контейнер
                container.AddEffect(effect);
                EditorUtility.SetDirty(container);

                // Выбираем созданный эффект
                Selection.activeObject = effect;
            }
            else
            {
                // Если отменили сохранение, уничтожаем объект
                Object.DestroyImmediate(effect);
            }
        }
    }

    /// <summary>
    /// Окно поиска эффектов по тегам
    /// </summary>
    public class EffectTagSearchWindow : EditorWindow
    {
        private EffectContainer container;
        private string searchTag = "";
        private List<EffectConfig> foundEffects = new();

        public static void Show(EffectContainer container)
        {
            var window = GetWindow<EffectTagSearchWindow>("Поиск по тегам");
            window.container = container;
            window.minSize = new Vector2(400, 300);
        }

        private void OnGUI()
        {
            if (container == null)
            {
                EditorGUILayout.HelpBox("Контейнер не найден", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("🔍 Поиск эффектов по тегу", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Поле ввода тега
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Тег:", GUILayout.Width(50));
            searchTag = EditorGUILayout.TextField(searchTag);
            if (GUILayout.Button("Найти", GUILayout.Width(60)))
            {
                SearchEffects();
            }
            EditorGUILayout.EndHorizontal();

            // Кнопки быстрых тегов
            var allTags = container.GetAllTags();
            if (allTags.Count > 0)
            {
                EditorGUILayout.LabelField("Быстрые теги:");
                EditorGUILayout.BeginHorizontal();
                foreach (var tag in allTags.OrderBy(t => t))
                {
                    if (GUILayout.Button(tag, EditorStyles.miniButton))
                    {
                        searchTag = tag;
                        SearchEffects();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Найдено эффектов: {foundEffects.Count}", EditorStyles.boldLabel);

            // Список найденных эффектов
            if (foundEffects.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (var effect in foundEffects)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"🎯 {effect.effectId}");
                    if (GUILayout.Button("Выбрать", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        Selection.activeObject = effect;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void SearchEffects()
        {
            if (string.IsNullOrEmpty(searchTag))
            {
                foundEffects.Clear();
                return;
            }

            foundEffects = container.FindEffectsByTag(searchTag);
        }
    }

    /// <summary>
    /// Простой диалог для ввода текста
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private string inputValue = "";
        private string message = "";
        private bool confirmed = false;
        private bool closed = false;

        private static string result;

        public static string Show(string title, string message, string defaultValue = "")
        {
            result = null;
            
            var window = CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window.message = message;
            window.inputValue = defaultValue;
            window.minSize = new Vector2(300, 100);
            window.maxSize = new Vector2(500, 100);
            window.ShowModalUtility();

            return result;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(message);
            
            GUI.SetNextControlName("InputField");
            inputValue = EditorGUILayout.TextField(inputValue);
            
            // Фокус на поле ввода
            if (Event.current.type == EventType.Repaint && !closed)
            {
                EditorGUI.FocusTextInControl("InputField");
            }

            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("OK", GUILayout.Width(80)) || 
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            {
                result = inputValue;
                closed = true;
                Close();
            }

            if (GUILayout.Button("Отмена", GUILayout.Width(80)) ||
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape))
            {
                result = null;
                closed = true;
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
