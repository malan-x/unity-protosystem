using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using PStyles = ProtoSystem.Editor.ProtoEditorStyles;

namespace ProtoSystem
{
    /// <summary>
    /// Кастомный редактор для SystemInitializationManager
    /// </summary>
    [CustomEditor(typeof(SystemInitializationManager))]
    public class SystemInitializationManagerEditor : UnityEditor.Editor
    {
        private ReorderableList systemsList;
        private SerializedProperty systemsProperty;
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private bool showDependencyGraph = false;

        // EventBus секция
        private EventBusEditorUtils.EventBusFileInfo cachedEventBusInfo;
        private string newNamespaceInput = "";
        private bool eventBusInfoCached = false;

        // ProtoSystem Components секция
        private bool showProtoSystemComponents = true;
        
        // Режим отображения списка систем
        private enum SystemsViewMode { Normal, LogSettings }
        private SystemsViewMode viewMode = SystemsViewMode.Normal;

        // Имена систем, встречающиеся в списке больше одного раза
        private readonly HashSet<string> duplicateNames = new HashSet<string>();

        // Имена выключенных систем (для подсветки зависящих от них)
        private readonly HashSet<string> disabledNames = new HashSet<string>();

        // Фильтр списка систем (по имени и типу)
        private string searchFilter = "";

        private void OnEnable()
        {
            systemsProperty = serializedObject.FindProperty("systems");
            CreateSystemsList();
        }

        private void SetupStyles()
        {
            // Проверяем, инициализированы ли стили
            if (headerStyle != null) return;
            
            try
            {
                // EditorStyles может быть null при первом вызове
                if (EditorStyles.boldLabel == null) return;
                
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
                };

                boxStyle = new GUIStyle("Box")
                {
                    padding = new RectOffset(10, 10, 10, 10),
                    margin = new RectOffset(0, 0, 5, 5)
                };
            }
            catch
            {
                // Стили ещё не готовы, попробуем в следующем кадре
            }
        }

        private void CreateSystemsList()
        {
            systemsList = new ReorderableList(serializedObject, systemsProperty, true, true, true, true)
            {
                drawHeaderCallback = DrawHeader,
                drawElementCallback = DrawElement,
                elementHeightCallback = GetElementHeight,
                onAddCallback = OnAddElement,
                onRemoveCallback = OnRemoveElement
            };
        }

        public override void OnInspectorGUI()
        {
            // Инициализируем стили при первом вызове OnInspectorGUI
            SetupStyles();
            
            serializedObject.Update();
            SystemInitializationManager manager = target as SystemInitializationManager;

            RefreshDuplicateNames(manager);

            // Заголовок
            EditorGUILayout.Space(4);
            PStyles.Header("⚙️ Менеджер Инициализации Систем",
                $"Систем: {manager.Systems.Count}, включено: {manager.Systems.Count(s => s.enabled)}");

            // Статус инициализации
            if (Application.isPlaying)
            {
                GUILayout.BeginVertical(boxStyle);
                DrawRuntimeStatus(manager);
                GUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);

            // Настройки
            GUILayout.BeginVertical(boxStyle);
            DrawSettingsSection();
            GUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Системы
            GUILayout.BeginVertical(boxStyle);
            DrawSystemsSection(manager);
            GUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Кнопки управления
            DrawControlButtonsSection(manager);

            EditorGUILayout.Space(10);

            // Статистика и граф
            DrawAnalysisSection(manager);

            EditorGUILayout.Space(10);

            // ProtoSystem компоненты
            DrawProtoSystemComponentsSection(manager);

            EditorGUILayout.Space(10);

            // EventBus проекта
            DrawProjectEventBusSection();

            serializedObject.ApplyModifiedProperties();
            
            // Обновляем настройки логирования в рантайме
            if (Application.isPlaying && manager != null)
            {
                manager.RefreshLogSettings();
            }
        }

        private void DrawRuntimeStatus(SystemInitializationManager manager)
        {
            EditorGUILayout.LabelField("🚀 Состояние инициализации", EditorStyles.boldLabel);

            // Статус основной инициализации
            string statusText = manager.IsInitialized ? "✅ Инициализирован" : "⏳ Не инициализирован";
            Color statusColor = manager.IsInitialized ? Color.green : Color.yellow;

            var oldColor = GUI.color;
            GUI.color = statusColor;
            EditorGUILayout.LabelField($"Основные системы: {statusText}");
            GUI.color = oldColor;

            // Статус post-зависимостей
            if (manager.IsInitialized)
            {
                string postStatusText = manager.IsPostDependenciesInitialized ? "✅ Post-зависимости готовы" : "⏳ Post-зависимости не готовы";
                Color postStatusColor = manager.IsPostDependenciesInitialized ? Color.green : Color.yellow;

                GUI.color = postStatusColor;
                EditorGUILayout.LabelField($"Post-зависимости: {postStatusText}");
                GUI.color = oldColor;
            }

            // Прогресс
            float progress = serializedObject.FindProperty("overallProgress").floatValue;
            var progressRect = EditorGUILayout.GetControlRect(false, 16);
            EditorGUI.ProgressBar(progressRect, progress, $"Общий прогресс: {(progress * 100):F0}%");

            // Текущая система
            string currentSystem = serializedObject.FindProperty("currentSystemName").stringValue;
            if (!string.IsNullOrEmpty(currentSystem))
            {
                EditorGUILayout.LabelField($"Текущая система: {currentSystem}");
            }

            // Кнопки ручного запуска
            EditorGUILayout.BeginHorizontal();

            if (!manager.IsInitialized)
            {
                if (GUILayout.Button("🚀 Запустить инициализацию"))
                {
                    manager.StartManualInitialization();
                }
            }
            else if (!manager.IsPostDependenciesInitialized)
            {
                if (GUILayout.Button("🔗 Запустить Post-зависимости"))
                {
                    manager.StartPostDependenciesInitialization();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSettingsSection()
        {
            EditorGUILayout.LabelField("⚙️ Настройки", EditorStyles.boldLabel);

            // Поля рисуем вручную (без PropertyField), чтобы не дублировался
            // [Header("Настройки инициализации")] из рантайм-класса.
            var autoStart = serializedObject.FindProperty("autoStartInitialization");
            autoStart.boolValue = EditorGUILayout.Toggle(
                new GUIContent("🚀 Автозапуск", "Автоматически запускать инициализацию при старте"),
                autoStart.boolValue);

            var timeout = serializedObject.FindProperty("maxInitializationTimeoutSeconds");
            timeout.floatValue = EditorGUILayout.FloatField(
                new GUIContent("⏱️ Таймаут (сек)", "Максимальное время инициализации одной системы"),
                timeout.floatValue);
        }
        
        /// <summary>
        /// Секция внутренних компонентов ProtoSystem (псевдосистемы)
        /// </summary>
        private void DrawInternalComponentsSection()
        {
            EditorGUILayout.LabelField("🔩 Внутренние компоненты", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            // EventPathResolver
            DrawPseudoSystemRow(
                "EventPathResolver",
                "🔀",
                "Резолвер путей событий",
                serializedObject.FindProperty("logEventPathResolver"),
                serializedObject.FindProperty("eventPathResolverLogLevel"),
                serializedObject.FindProperty("eventPathResolverLogCategories"));
            
            EditorGUILayout.Space(2);
            
            // SystemInit
            DrawPseudoSystemRow(
                "SystemInit",
                "🚀",
                "Менеджер инициализации",
                serializedObject.FindProperty("logSystemInit"),
                serializedObject.FindProperty("systemInitLogLevel"),
                serializedObject.FindProperty("systemInitLogCategories"));
            
            EditorGUILayout.Space(5);
            
            // Прочие системы (глобальные настройки для незарегистрированных systemId)
            DrawGlobalLogSettingsRow();
        }
        
        /// <summary>
        /// Отрисовка строки псевдосистемы
        /// </summary>
        private void DrawPseudoSystemRow(string name, string icon, string description,
            SerializedProperty logEnabled, SerializedProperty logLevel, SerializedProperty logCategories)
        {
            // Фон — фиолетовый оттенок для псевдосистем
            Rect rowRect = EditorGUILayout.GetControlRect(false, 40);
            Color bgColor = logEnabled.boolValue 
                ? new Color(0.45f, 0.30f, 0.55f, 0.25f)   // Фиолетовый
                : new Color(0.3f, 0.3f, 0.3f, 0.15f);
            EditorGUI.DrawRect(new Rect(rowRect.x - 2, rowRect.y - 1, rowRect.width + 4, rowRect.height + 2), bgColor);
            
            float currentY = rowRect.y + 2;
            
            // Первая строка: чекбокс + иконка + название + описание
            Rect enableRect = new Rect(rowRect.x, currentY, 18, 18);
            logEnabled.boolValue = EditorGUI.Toggle(enableRect, logEnabled.boolValue);
            
            Rect iconRect = new Rect(rowRect.x + 22, currentY, 20, 18);
            EditorGUI.LabelField(iconRect, icon);
            
            Rect nameRect = new Rect(rowRect.x + 44, currentY, 120, 18);
            EditorGUI.LabelField(nameRect, name, EditorStyles.boldLabel);
            
            Rect descRect = new Rect(rowRect.x + 170, currentY, rowRect.width - 175, 18);
            EditorGUI.LabelField(descRect, description, EditorStyles.miniLabel);
            
            currentY += 20;
            
            // Вторая строка: уровень + категории (только если включено)
            if (logEnabled.boolValue)
            {
                float levelX = rowRect.x + 22;
                
                // Уровни
                var levels = new (LogLevel level, string label, Color color, float width)[]
                {
                    (LogLevel.Errors, "Err", new Color(0.96f, 0.31f, 0.31f), 36),
                    (LogLevel.Warnings, "Warn", new Color(1f, 0.76f, 0.03f), 44),
                    (LogLevel.Info, "Info", new Color(0.5f, 0.8f, 0.5f), 36),
                    (LogLevel.Verbose, "Vrb", new Color(0.5f, 0.5f, 0.5f), 32),
                };
                
                var currentLevels = (LogLevel)logLevel.intValue;
                foreach (var lvl in levels)
                {
                    Rect btnRect = new Rect(levelX, currentY, lvl.width, 16);
                    bool isEnabled = (currentLevels & lvl.level) != 0;
                    
                    var oldBg = GUI.backgroundColor;
                    if (isEnabled) GUI.backgroundColor = lvl.color;
                    
                    if (GUI.Button(btnRect, lvl.label, EditorStyles.miniButton))
                    {
                        if (isEnabled)
                            logLevel.intValue = (int)(currentLevels & ~lvl.level);
                        else
                            logLevel.intValue = (int)(currentLevels | lvl.level);
                    }
                    
                    GUI.backgroundColor = oldBg;
                    levelX += lvl.width + 2;
                }
                
                levelX += 12;
                
                // Категории
                var categories = new (LogCategory cat, string label, Color color, float width)[]
                {
                    (LogCategory.Initialization, "Init", new Color(0.30f, 0.69f, 0.31f), 34),
                    (LogCategory.Dependencies, "Dep", new Color(1f, 0.60f, 0f), 34),
                    (LogCategory.Events, "Event", new Color(0.13f, 0.59f, 0.95f), 42),
                    (LogCategory.Runtime, "Run", new Color(0.61f, 0.15f, 0.69f), 34)
                };
                
                var currentCategories = (LogCategory)logCategories.intValue;
                foreach (var cat in categories)
                {
                    Rect catRect = new Rect(levelX, currentY, cat.width, 16);
                    bool isEnabled = (currentCategories & cat.cat) != 0;
                    
                    var oldBg = GUI.backgroundColor;
                    if (isEnabled) GUI.backgroundColor = cat.color;
                    
                    if (GUI.Button(catRect, cat.label, EditorStyles.miniButton))
                    {
                        if (isEnabled)
                            logCategories.intValue = (int)(currentCategories & ~cat.cat);
                        else
                            logCategories.intValue = (int)(currentCategories | cat.cat);
                    }
                    
                    GUI.backgroundColor = oldBg;
                    levelX += cat.width + 2;
                }
            }
            else
            {
                Rect hintRect = new Rect(rowRect.x + 22, currentY, rowRect.width - 22, 16);
                EditorGUI.LabelField(hintRect, "Логирование выключено", EditorStyles.centeredGreyMiniLabel);
            }
        }

        /// <summary>
        /// Отрисовка строки глобальных настроек логирования (для незарегистрированных систем)
        /// </summary>
        private void DrawGlobalLogSettingsRow()
        {
            var logSettingsProp = serializedObject.FindProperty("logSettings");
            var globalLogLevel = logSettingsProp.FindPropertyRelative("globalLogLevel");
            var enabledCategories = logSettingsProp.FindPropertyRelative("enabledCategories");
            
            // Фон — серый оттенок для глобальных настроек
            Rect rowRect = EditorGUILayout.GetControlRect(false, 40);
            Color bgColor = new Color(0.35f, 0.35f, 0.40f, 0.25f);
            EditorGUI.DrawRect(new Rect(rowRect.x - 2, rowRect.y - 1, rowRect.width + 4, rowRect.height + 2), bgColor);
            
            float currentY = rowRect.y + 2;
            
            // Первая строка: иконка + название + описание
            Rect iconRect = new Rect(rowRect.x + 4, currentY, 20, 18);
            EditorGUI.LabelField(iconRect, "🌐");
            
            Rect nameRect = new Rect(rowRect.x + 26, currentY, 140, 18);
            EditorGUI.LabelField(nameRect, "Прочие системы", EditorStyles.boldLabel);
            
            Rect descRect = new Rect(rowRect.x + 170, currentY, rowRect.width - 175, 18);
            EditorGUI.LabelField(descRect, "Настройки для незарегистрированных systemId", EditorStyles.miniLabel);
            
            currentY += 20;
            
            // Вторая строка: уровень + категории
            float levelX = rowRect.x + 22;
            
            // Уровни
            var levels = new (LogLevel level, string label, Color color, float width)[]
            {
                (LogLevel.Errors, "Err", new Color(0.96f, 0.31f, 0.31f), 36),
                (LogLevel.Warnings, "Warn", new Color(1f, 0.76f, 0.03f), 44),
                (LogLevel.Info, "Info", new Color(0.5f, 0.8f, 0.5f), 36),
                (LogLevel.Verbose, "Vrb", new Color(0.5f, 0.5f, 0.5f), 32),
            };
            
            var currentLevels = (LogLevel)globalLogLevel.intValue;
            foreach (var lvl in levels)
            {
                Rect btnRect = new Rect(levelX, currentY, lvl.width, 16);
                bool isEnabled = (currentLevels & lvl.level) != 0;
                
                var oldBg = GUI.backgroundColor;
                if (isEnabled) GUI.backgroundColor = lvl.color;
                
                if (GUI.Button(btnRect, lvl.label, EditorStyles.miniButton))
                {
                    if (isEnabled)
                        globalLogLevel.intValue = (int)(currentLevels & ~lvl.level);
                    else
                        globalLogLevel.intValue = (int)(currentLevels | lvl.level);
                }
                
                GUI.backgroundColor = oldBg;
                levelX += lvl.width + 2;
            }
            
            levelX += 12;
            
            // Категории
            var categories = new (LogCategory cat, string label, Color color, float width)[]
            {
                (LogCategory.Initialization, "Init", new Color(0.30f, 0.69f, 0.31f), 34),
                (LogCategory.Dependencies, "Dep", new Color(1f, 0.60f, 0f), 34),
                (LogCategory.Events, "Event", new Color(0.13f, 0.59f, 0.95f), 42),
                (LogCategory.Runtime, "Run", new Color(0.61f, 0.15f, 0.69f), 34)
            };
            
            var currentCategories = (LogCategory)enabledCategories.intValue;
            foreach (var cat in categories)
            {
                Rect catRect = new Rect(levelX, currentY, cat.width, 16);
                bool isEnabled = (currentCategories & cat.cat) != 0;
                
                var oldBg = GUI.backgroundColor;
                if (isEnabled) GUI.backgroundColor = cat.color;
                
                if (GUI.Button(catRect, cat.label, EditorStyles.miniButton))
                {
                    if (isEnabled)
                        enabledCategories.intValue = (int)(currentCategories & ~cat.cat);
                    else
                        enabledCategories.intValue = (int)(currentCategories | cat.cat);
                }
                
                GUI.backgroundColor = oldBg;
                levelX += cat.width + 2;
            }
        }

        /// <summary>
        /// Пересчитывает список дублированных имён систем.
        /// </summary>
        private void RefreshDuplicateNames(SystemInitializationManager manager)
        {
            duplicateNames.Clear();
            disabledNames.Clear();
            var seen = new HashSet<string>();
            foreach (var s in manager.Systems)
            {
                if (string.IsNullOrEmpty(s.systemName)) continue;
                if (!seen.Add(s.systemName))
                    duplicateNames.Add(s.systemName);
                if (!s.enabled)
                    disabledNames.Add(s.systemName);
            }
        }

        /// <summary>
        /// Удаляет дубли систем из списка, оставляя первую запись с каждым именем.
        /// </summary>
        private void RemoveDuplicateSystems()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < systemsProperty.arraySize; i++)
            {
                string name = systemsProperty.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("systemName").stringValue;
                if (string.IsNullOrEmpty(name)) continue;
                if (!seen.Add(name))
                {
                    systemsProperty.DeleteArrayElementAtIndex(i);
                    i--;
                }
            }
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void DrawSystemsSection(SystemInitializationManager manager)
        {
            // Дубли систем — предупреждение и кнопка очистки
            if (duplicateNames.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"⚠ Дублированные имена систем: {string.Join(", ", duplicateNames)}.\n" +
                    "Учитывается только первая запись с каждым именем — остальные игнорируются.",
                    MessageType.Warning);
                if (GUILayout.Button("🧹 Удалить дубли (оставить первые)"))
                {
                    RemoveDuplicateSystems();
                    return; // список изменился — перерисуем в следующем кадре
                }
                EditorGUILayout.Space(5);
            }

            // Заголовок с переключателем режимов
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"🔧 Системы ({manager.Systems.Count})", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Toggle(viewMode == SystemsViewMode.Normal, "📋 Список", "ToolbarButton", GUILayout.Width(72)))
            {
                viewMode = SystemsViewMode.Normal;
            }
            if (GUILayout.Toggle(viewMode == SystemsViewMode.LogSettings, "📝 Логи", "ToolbarButton", GUILayout.Width(58)))
            {
                viewMode = SystemsViewMode.LogSettings;
            }

            EditorGUILayout.EndHorizontal();

            // Фильтр по имени/типу
            EditorGUILayout.BeginHorizontal();
            searchFilter = EditorGUILayout.TextField(
                new GUIContent("🔍 Фильтр", "Поиск по имени и типу системы"), searchFilter);
            if (!string.IsNullOrEmpty(searchFilter) &&
                GUILayout.Button("✕", GUILayout.Width(22)))
            {
                searchFilter = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            // В режиме логов — показываем tri-state кнопки для массового управления
            if (viewMode == SystemsViewMode.LogSettings)
            {
                DrawLogSettingsToolbar(manager);
                
                EditorGUILayout.Space(5);
                
                // Внутренние компоненты (псевдосистемы)
                DrawInternalComponentsSection();
                
                EditorGUILayout.Space(5);
            }
            else
            {
                // Строка метрик (только в обычном режиме)
                EditorGUILayout.BeginHorizontal();
                
                bool showMetrics = SystemMetricsSettings.ShowMetrics;
                bool newShowMetrics = EditorGUILayout.Toggle("📊 Метрики", showMetrics, GUILayout.Width(100));
                if (newShowMetrics != showMetrics)
                {
                    SystemMetricsSettings.ShowMetrics = newShowMetrics;
                }
                
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("⚙️ Пороги", "Настройки порогов метрик (LOC/KB/методы)"),
                        GUILayout.Width(80)))
                {
                    SystemMetricsSettingsWindow.ShowWindow();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (string.IsNullOrEmpty(searchFilter))
            {
                systemsList.DoLayoutList();
            }
            else
            {
                DrawFilteredSystemsList();
            }
        }

        /// <summary>
        /// Плоский список систем под активным фильтром.
        /// Перетаскивание в этом режиме недоступно — только просмотр/редактирование.
        /// </summary>
        private void DrawFilteredSystemsList()
        {
            string filter = searchFilter.Trim().ToLowerInvariant();
            int shown = 0;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < systemsProperty.arraySize; i++)
            {
                var element = systemsProperty.GetArrayElementAtIndex(i);
                string name = element.FindPropertyRelative("systemName").stringValue ?? "";

                var existingObj = element.FindPropertyRelative("existingSystemObject").objectReferenceValue;
                string typeName = existingObj != null
                    ? existingObj.GetType().Name
                    : element.FindPropertyRelative("systemTypeName").stringValue ?? "";

                if (!name.ToLowerInvariant().Contains(filter) &&
                    !typeName.ToLowerInvariant().Contains(filter))
                    continue;

                var rect = EditorGUILayout.GetControlRect(false, GetElementHeight(i));
                DrawElement(rect, i, false, false);
                shown++;
            }

            if (shown == 0)
                EditorGUILayout.LabelField($"Ничего не найдено по «{searchFilter}»",
                    EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField(
                $"Показано {shown} из {systemsProperty.arraySize} · перетаскивание недоступно при фильтре",
                EditorStyles.centeredGreyMiniLabel);
        }
        
        /// <summary>
        /// Toolbar с tri-state кнопками для массового управления логами
        /// </summary>
        private void DrawLogSettingsToolbar(SystemInitializationManager manager)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Tri-state для включения логов
            DrawTriStateButton(manager, "Логи", "Логирование", 
                e => e.logEnabled, 
                (e, v) => e.logEnabled = v, 55);
            
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Уровень:", GUILayout.Width(50));
            
            // Tri-state для уровней
            DrawTriStateLevelButton(manager, "Err", LogLevel.Errors, new Color(0.96f, 0.31f, 0.31f), 42);
            DrawTriStateLevelButton(manager, "Warn", LogLevel.Warnings, new Color(1f, 0.76f, 0.03f), 50);
            DrawTriStateLevelButton(manager, "Info", LogLevel.Info, new Color(0.5f, 0.8f, 0.5f), 42);
            DrawTriStateLevelButton(manager, "Vrb", LogLevel.Verbose, new Color(0.5f, 0.5f, 0.5f), 36);
            
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Категории:", GUILayout.Width(65));
            
            // Tri-state для категорий
            DrawTriStateCategoryButton(manager, "Init", LogCategory.Initialization, new Color(0.30f, 0.69f, 0.31f), 42);
            DrawTriStateCategoryButton(manager, "Dep", LogCategory.Dependencies, new Color(1f, 0.60f, 0f), 42);
            DrawTriStateCategoryButton(manager, "Event", LogCategory.Events, new Color(0.13f, 0.59f, 0.95f), 50);
            DrawTriStateCategoryButton(manager, "Run", LogCategory.Runtime, new Color(0.61f, 0.15f, 0.69f), 42);
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// Tri-state кнопка для bool поля
        /// </summary>
        private void DrawTriStateButton(SystemInitializationManager manager, string label, string tooltip,
            System.Func<SystemEntry, bool> getter, System.Action<SystemEntry, bool> setter, float width)
        {
            int enabledCount = manager.Systems.Count(s => getter(s));
            int totalCount = manager.Systems.Count;
            
            // Определяем состояние: все вкл, все выкл, частично
            string stateIcon;
            Color bgColor;
            if (enabledCount == totalCount)
            {
                stateIcon = "✓";
                bgColor = new Color(0.3f, 0.7f, 0.3f);
            }
            else if (enabledCount == 0)
            {
                stateIcon = "✗";
                bgColor = Color.gray;
            }
            else
            {
                stateIcon = "◐";
                bgColor = new Color(0.7f, 0.7f, 0.3f);
            }
            
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;
            
            if (GUILayout.Button(new GUIContent($"{stateIcon} {label}", $"{tooltip}: {enabledCount}/{totalCount}"), 
                GUILayout.Width(width)))
            {
                // При клике переключаем все
                bool newValue = enabledCount < totalCount;
                foreach (var entry in manager.Systems)
                {
                    setter(entry, newValue);
                }
                EditorUtility.SetDirty(manager);
            }
            
            GUI.backgroundColor = oldBg;
        }
        
        /// <summary>
        /// Tri-state кнопка для уровня логов (флаговая)
        /// </summary>
        private void DrawTriStateLevelButton(SystemInitializationManager manager, string label, LogLevel level, Color color, float width)
        {
            int enabledCount = manager.Systems.Count(s => (s.logLevel & level) != 0);
            int totalCount = manager.Systems.Count;
            
            string stateIcon = enabledCount == totalCount ? "✓" : (enabledCount > 0 ? "◐" : "○");
            
            var oldBg = GUI.backgroundColor;
            if (enabledCount == totalCount)
                GUI.backgroundColor = color;
            else if (enabledCount > 0)
                GUI.backgroundColor = color * 0.5f;
            
            if (GUILayout.Button(new GUIContent($"{stateIcon} {label}", $"{enabledCount}/{totalCount} систем"), 
                GUILayout.Width(width)))
            {
                // Переключаем: если не все включены — включаем всем, иначе выключаем всем
                bool enable = enabledCount < totalCount;
                foreach (var entry in manager.Systems)
                {
                    if (enable)
                        entry.logLevel |= level;
                    else
                        entry.logLevel &= ~level;
                }
                EditorUtility.SetDirty(manager);
            }
            
            GUI.backgroundColor = oldBg;
        }
        
        /// <summary>
        /// Tri-state кнопка для категории логов
        /// </summary>
        private void DrawTriStateCategoryButton(SystemInitializationManager manager, string label, LogCategory category, Color color, float width)
        {
            int enabledCount = manager.Systems.Count(s => (s.logCategories & category) != 0);
            int totalCount = manager.Systems.Count;
            
            string stateIcon = enabledCount == totalCount ? "✓" : (enabledCount > 0 ? "◐" : "○");
            
            var oldBg = GUI.backgroundColor;
            if (enabledCount == totalCount)
                GUI.backgroundColor = color;
            else if (enabledCount > 0)
                GUI.backgroundColor = color * 0.5f;
            
            if (GUILayout.Button(new GUIContent($"{stateIcon} {label}", $"{enabledCount}/{totalCount} систем"), 
                GUILayout.Width(width)))
            {
                // Переключаем: если не все включены — включаем всем, иначе выключаем всем
                bool enable = enabledCount < totalCount;
                foreach (var entry in manager.Systems)
                {
                    if (enable)
                        entry.logCategories |= category;
                    else
                        entry.logCategories &= ~category;
                }
                EditorUtility.SetDirty(manager);
            }
            
            GUI.backgroundColor = oldBg;
        }

        private void DrawControlButtonsSection(SystemInitializationManager manager)
        {
            GUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("🔍 Анализ и управление", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // Кнопка анализа зависимостей
            if (GUILayout.Button("🔍 Анализировать зависимости", GUILayout.Height(30)))
            {
                manager.AnalyzeDependencies();
                EditorUtility.SetDirty(manager);
            }

            // Кнопка валидации
            if (GUILayout.Button("✅ Валидировать", GUILayout.Height(30)))
            {
                if (manager.Validate(out List<string> errors))
                {
                    EditorUtility.DisplayDialog("Валидация", "✅ Настройки прошли валидацию успешно!", "OK");
                }
                else
                {
                    string errorMessage = "❌ Найдены ошибки:\n\n" + string.Join("\n", errors);
                    EditorUtility.DisplayDialog("Ошибки валидации", errorMessage, "OK");
                }
            }

            EditorGUILayout.EndHorizontal();

            // Вторая строка с кнопкой добавления недостающих систем
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("➕ Добавить недостающие системы", GUILayout.Height(30)))
            {
                AddMissingSystems(manager);
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        /// <summary>
        /// Находит и добавляет недостающие системы из сцены
        /// </summary>
        private void AddMissingSystems(SystemInitializationManager manager)
        {
            // Находим все объекты с IInitializableSystem в сцене
            var allSystemsInScene = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(mb => mb is IInitializableSystem)
                .Cast<IInitializableSystem>()
                .ToList();

            // Получаем уже добавленные системы
            var existingSystems = new HashSet<MonoBehaviour>();
            foreach (var entry in manager.Systems)
            {
                if (entry.useExistingObject && entry.ExistingSystemObject != null)
                {
                    existingSystems.Add(entry.ExistingSystemObject as MonoBehaviour);
                }
            }

            // Находим недостающие
            var missingSystems = allSystemsInScene
                .Where(s => !existingSystems.Contains(s as MonoBehaviour))
                .ToList();

            if (missingSystems.Count == 0)
            {
                EditorUtility.DisplayDialog("Поиск систем",
                    "Все системы из сцены уже добавлены в список.", "OK");
                return;
            }

            // Показываем диалог подтверждения
            string message = $"Найдено {missingSystems.Count} недостающих систем:\n\n";
            int showCount = Mathf.Min(missingSystems.Count, 10);
            for (int i = 0; i < showCount; i++)
            {
                var system = missingSystems[i] as MonoBehaviour;
                message += $"- {system.name} ({system.GetType().Name})\n";
            }
            if (missingSystems.Count > 10)
            {
                message += $"... и еще {missingSystems.Count - 10}\n";
            }
            message += "\nДобавить их в список?";

            if (EditorUtility.DisplayDialog("Добавить недостающие системы", message, "Добавить", "Отмена"))
            {
                // Добавляем системы
                foreach (var system in missingSystems)
                {
                    var monoBehaviour = system as MonoBehaviour;

                    int index = systemsProperty.arraySize;
                    systemsProperty.arraySize++;

                    var element = systemsProperty.GetArrayElementAtIndex(index);
                    element.FindPropertyRelative("systemName").stringValue = monoBehaviour.GetType().Name;
                    element.FindPropertyRelative("enabled").boolValue = true;
                    element.FindPropertyRelative("useExistingObject").boolValue = true;
                    element.FindPropertyRelative("existingSystemObject").objectReferenceValue = monoBehaviour;
                    element.FindPropertyRelative("verboseLogging").boolValue = true;

                    // Очищаем данные анализа
                    var dependencies = element.FindPropertyRelative("detectedDependencies");
                    dependencies.arraySize = 0;
                    element.FindPropertyRelative("hasCyclicDependency").boolValue = false;
                    element.FindPropertyRelative("cyclicDependencyInfo").stringValue = "";
                }

                serializedObject.ApplyModifiedProperties();
                manager.AnalyzeDependencies();
                EditorUtility.SetDirty(manager);
                
                // Обновляем настройки логирования в рантайме
                if (Application.isPlaying)
                {
                    manager.RefreshLogSettings();
                }

                Debug.Log($"✅ Добавлено {missingSystems.Count} систем из сцены");
            }
        }

        private void DrawAnalysisSection(SystemInitializationManager manager)
        {
            GUILayout.BeginVertical(boxStyle);

            // Заголовок с кнопкой-переключателем
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📊 Анализ системы", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            string toggleText = showDependencyGraph ? "🔽 Скрыть граф" : "🔼 Показать граф";
            if (GUILayout.Button(toggleText, GUILayout.Width(120)))
            {
                showDependencyGraph = !showDependencyGraph;
            }
            EditorGUILayout.EndHorizontal();

            // Краткая статистика
            DrawQuickStats(manager);

            // Граф зависимостей
            if (showDependencyGraph)
            {
                EditorGUILayout.Space(5);
                DrawDependencyGraph(manager);
            }

            GUILayout.EndVertical();
        }

        private void DrawQuickStats(SystemInitializationManager manager)
        {
            EditorGUILayout.BeginHorizontal();

            // Общая статистика
            EditorGUILayout.BeginVertical("Box", GUILayout.Width(150));
            EditorGUILayout.LabelField("📈 Статистика", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"Всего систем: {manager.Systems.Count}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Включено: {manager.Systems.Count(s => s.enabled)}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            // Проблемы
            var problemsCount = manager.Systems.Count(s => s.hasCyclicDependency);
            var depsOnDisabledCount = manager.Systems.Count(s =>
                s.enabled && s.detectedDependencies.Any(d => disabledNames.Contains(d)));

            EditorGUILayout.BeginVertical("Box", GUILayout.Width(150));
            EditorGUILayout.LabelField("⚠️ Проблемы", EditorStyles.centeredGreyMiniLabel);

            var oldProblemColor = GUI.color;
            if (problemsCount > 0)
            {
                GUI.color = Color.red;
                EditorGUILayout.LabelField($"Циклы: {problemsCount}", EditorStyles.miniLabel);
            }
            if (depsOnDisabledCount > 0)
            {
                GUI.color = new Color(1f, 0.55f, 0.7f);
                EditorGUILayout.LabelField($"⛔ Зависят от выключенных: {depsOnDisabledCount}", EditorStyles.miniLabel);
            }
            if (problemsCount == 0 && depsOnDisabledCount == 0)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField("Проблем нет ✅", EditorStyles.miniLabel);
            }
            GUI.color = oldProblemColor;
            EditorGUILayout.EndVertical();

            // Порядок инициализации
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("🔄 Порядок инициализации", EditorStyles.centeredGreyMiniLabel);
            var orderedSystems = manager.GetSystemsInInitializationOrder();

            if (orderedSystems.Count > 0)
            {
                for (int i = 0; i < Mathf.Min(3, orderedSystems.Count); i++)
                {
                    var system = orderedSystems[i];
                    string statusIcon = system.enabled ? "✅" : "⭕";
                    if (system.hasCyclicDependency) statusIcon = "❌";

                    EditorGUILayout.LabelField($"{i + 1}. {statusIcon} {system.systemName}", EditorStyles.miniLabel);
                }

                if (orderedSystems.Count > 3)
                {
                    EditorGUILayout.LabelField($"... и еще {orderedSystems.Count - 3}", EditorStyles.centeredGreyMiniLabel);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Нет систем", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDependencyGraph(SystemInitializationManager manager)
        {
            EditorGUILayout.LabelField("🕸️ Граф зависимостей", EditorStyles.boldLabel);

            string dependencyGraph = serializedObject.FindProperty("dependencyGraph").stringValue;
            if (!string.IsNullOrEmpty(dependencyGraph))
            {
                var style = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,
                    fontSize = 10,
                    padding = new RectOffset(10, 10, 10, 10)
                };

                EditorGUILayout.TextArea(dependencyGraph, style, GUILayout.Height(150));
            }
            else
            {
                EditorGUILayout.HelpBox("🔍 Нажмите 'Анализировать зависимости' для построения графа", MessageType.Info);
            }
        }

        #region ProtoSystem Components Section

        private void DrawProtoSystemComponentsSection(SystemInitializationManager manager)
        {
            GUILayout.BeginVertical(boxStyle);
            
            // Заголовок с кнопкой раскрытия
            EditorGUILayout.BeginHorizontal();
            
            string foldoutIcon = showProtoSystemComponents ? "🔽" : "🔼";
            if (GUILayout.Button($"{foldoutIcon} 📦 Компоненты ProtoSystem", EditorStyles.boldLabel))
            {
                showProtoSystemComponents = !showProtoSystemComponents;
            }
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("🔄", GUILayout.Width(25)))
            {
                ProtoSystemComponentsUtility.InvalidateCache();
            }
            
            EditorGUILayout.EndHorizontal();

            if (showProtoSystemComponents)
            {
                EditorGUILayout.Space(5);
                
                var components = ProtoSystemComponentsUtility.GetAllComponents(manager);
                
                if (components.Count == 0)
                {
                    EditorGUILayout.HelpBox("Компоненты ProtoSystem не найдены", MessageType.Info);
                }
                else
                {
                    // Группируем по категориям
                    var categories = components.GroupBy(c => c.Category).OrderBy(g => g.Key);
                    
                    foreach (var category in categories)
                    {
                        EditorGUILayout.LabelField($"📁 {category.Key}", EditorStyles.miniLabel);
                        
                        EditorGUILayout.BeginVertical("Box");
                        
                        foreach (var component in category)
                        {
                            DrawComponentRow(manager, component);
                        }
                        
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(3);
                    }
                }
                
                // Общая статистика
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                
                int inScene = components.Count(c => c.ExistsInScene);
                int inManager = components.Count(c => c.ExistsInManager);
                
                EditorGUILayout.LabelField($"В сцене: {inScene}/{components.Count}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"В менеджере: {inManager}/{components.Count}", EditorStyles.miniLabel);
                
                GUILayout.FlexibleSpace();
                
                // Кнопка добавить все
                EditorGUI.BeginDisabledGroup(inManager == components.Count);
                if (GUILayout.Button("➕ Добавить все", GUILayout.Width(110)))
                {
                    foreach (var component in components)
                    {
                        if (!component.ExistsInManager)
                        {
                            if (!component.ExistsInScene)
                            {
                                ProtoSystemComponentsUtility.CreateAndAddToManager(manager, component);
                            }
                            else
                            {
                                ProtoSystemComponentsUtility.AddToManager(manager, component);
                            }
                        }
                    }
                }
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void DrawComponentRow(SystemInitializationManager manager, ProtoSystemComponentInfo component)
        {
            // GUILayout-верстка с ExpandWidth на описании может "выталкивать" кнопки вправо/на следующую строку.
            // Делаем предсказуемую сетку: фиксированные колонки + зарезервированная область под кнопки.
            var rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

            const float nameWidth = 160f;
            const float statusWidth = 25f;
            const float buttonSmall = 25f;
            const float buttonWide = 40f;
            const float spacing = 4f;

            bool showCreateInScene = !component.ExistsInScene;
            bool showAddToManager = !component.ExistsInManager && component.ExistsInScene;
            bool showCreateAndAdd = !component.ExistsInManager && !component.ExistsInScene;
            bool showSelectInScene = component.ExistsInManager && component.SceneInstance != null;

            float buttonsWidth = 0f;
            int buttonCount = 0;
            if (showCreateInScene) { buttonsWidth += buttonSmall; buttonCount++; }
            if (showAddToManager) { buttonsWidth += buttonSmall; buttonCount++; }
            if (showCreateAndAdd) { buttonsWidth += buttonWide; buttonCount++; }
            if (showSelectInScene) { buttonsWidth += buttonSmall; buttonCount++; }
            if (buttonCount > 1) buttonsWidth += spacing * (buttonCount - 1);

            var nameRect = new Rect(rowRect.x, rowRect.y, nameWidth, rowRect.height);
            var statusRect = new Rect(nameRect.xMax + spacing, rowRect.y, statusWidth, rowRect.height);
            var buttonsRect = new Rect(rowRect.xMax - buttonsWidth, rowRect.y, buttonsWidth, rowRect.height);
            var descRect = new Rect(statusRect.xMax + spacing, rowRect.y, buttonsRect.xMin - (statusRect.xMax + spacing * 2), rowRect.height);
            if (descRect.width < 0) descRect.width = 0;

            // Статус
            string statusIcon;
            Color statusColor;
            if (component.ExistsInManager)
            {
                statusIcon = "✅";
                statusColor = Color.green;
            }
            else if (component.ExistsInScene)
            {
                statusIcon = "🔶";
                statusColor = Color.yellow;
            }
            else
            {
                statusIcon = "⭕";
                statusColor = Color.gray;
            }

            var oldColor = GUI.color;
            GUI.color = statusColor;
            EditorGUI.LabelField(nameRect, $"{component.Icon} {component.DisplayName}");
            GUI.color = oldColor;

            EditorGUI.LabelField(statusRect, statusIcon, EditorStyles.label);

            var descContent = new GUIContent(TruncateString(component.Description, 35), component.Description);
            EditorGUI.LabelField(descRect, descContent, EditorStyles.miniLabel);

            // Кнопки справа (в фиксированной области)
            float bx = buttonsRect.x;
            if (showCreateInScene)
            {
                if (GUI.Button(new Rect(bx, rowRect.y, buttonSmall, rowRect.height), new GUIContent("🔨", "Создать в сцене")))
                {
                    ProtoSystemComponentsUtility.CreateComponentInScene(component.Type, manager.transform);
                }
                bx += buttonSmall + spacing;
            }

            if (showAddToManager)
            {
                if (GUI.Button(new Rect(bx, rowRect.y, buttonSmall, rowRect.height), new GUIContent("➕", "Добавить в менеджер")))
                {
                    ProtoSystemComponentsUtility.AddToManager(manager, component);
                }
                bx += buttonSmall + spacing;
            }
            else if (showCreateAndAdd)
            {
                if (GUI.Button(new Rect(bx, rowRect.y, buttonWide, rowRect.height), new GUIContent("➕🔨", "Создать и добавить")))
                {
                    ProtoSystemComponentsUtility.CreateAndAddToManager(manager, component);
                }
                bx += buttonWide + spacing;
            }
            else if (showSelectInScene)
            {
                if (GUI.Button(new Rect(bx, rowRect.y, buttonSmall, rowRect.height), new GUIContent("🎯", "Выбрать в сцене")))
                {
                    Selection.activeGameObject = component.SceneInstance.gameObject;
                }
                bx += buttonSmall + spacing;
            }
        }

        private string TruncateString(string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str) || str.Length <= maxLength)
                return str;
            return str.Substring(0, maxLength - 3) + "...";
        }

        #endregion

        #region EventBus Section

        private void DrawProjectEventBusSection()
        {
            GUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("📡 EventBus проекта", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Кэшируем информацию при первом вызове или по запросу
            if (!eventBusInfoCached || cachedEventBusInfo == null)
            {
                cachedEventBusInfo = EventBusEditorUtils.GetProjectEventBusInfo();
                eventBusInfoCached = true;
            }

            if (cachedEventBusInfo.Exists)
            {
                DrawExistingEventBusInfo();
            }
            else
            {
                DrawCreateEventBusUI();
            }

            EditorGUILayout.Space(5);

            // Кнопка обновления
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🔄 Обновить", GUILayout.Width(100)))
            {
                eventBusInfoCached = false;
                cachedEventBusInfo = null;
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawExistingEventBusInfo()
        {
            // Информация о найденном файле
            EditorGUILayout.BeginVertical("Box");

            EditorGUILayout.LabelField($"✅ Файл найден", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Namespace: {cachedEventBusInfo.Namespace}", EditorStyles.miniLabel);

            // Путь к файлу (относительный)
            string relativePath = cachedEventBusInfo.FilePath;
            if (relativePath.Contains(Application.dataPath))
            {
                relativePath = "Assets" + relativePath.Substring(Application.dataPath.Length);
            }
            EditorGUILayout.LabelField($"Путь: {relativePath}", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // Статистика
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical("Box", GUILayout.Width(120));
            EditorGUILayout.LabelField("📊 Событий", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{cachedEventBusInfo.EventCount}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("Box", GUILayout.Width(120));
            EditorGUILayout.LabelField("📁 Категорий", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{cachedEventBusInfo.CategoryCount}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Список категорий
            if (cachedEventBusInfo.Categories.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("📁 Категории:", EditorStyles.miniLabel);

                foreach (var category in cachedEventBusInfo.Categories.Take(5))
                {
                    EditorGUILayout.LabelField($"  • {category.Name} ({category.EventCount} событий)", EditorStyles.miniLabel);
                }

                if (cachedEventBusInfo.Categories.Count > 5)
                {
                    EditorGUILayout.LabelField($"  ... и ещё {cachedEventBusInfo.Categories.Count - 5}", EditorStyles.centeredGreyMiniLabel);
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Кнопки действий
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("📝 Открыть файл", GUILayout.Height(25)))
            {
                EventBusEditorUtils.OpenEventBusFile(cachedEventBusInfo.FilePath);
            }

            if (GUILayout.Button("📤 Экспорт для MCP", GUILayout.Height(25)))
            {
                string exportPath = EventBusEditorUtils.ExportEventsForMCP(cachedEventBusInfo);
                if (!string.IsNullOrEmpty(exportPath))
                {
                    EditorUtility.DisplayDialog("Экспорт EventBus",
                        $"✅ Данные EventBus экспортированы:\n{exportPath}", "OK");
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCreateEventBusUI()
        {
            EditorGUILayout.HelpBox("EventBus файл проекта не найден.\nСоздайте новый файл, указав namespace проекта.", MessageType.Info);

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Namespace проекта:", GUILayout.Width(130));
            newNamespaceInput = EditorGUILayout.TextField(newNamespaceInput);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Превью пути
            if (!string.IsNullOrEmpty(newNamespaceInput))
            {
                string previewPath = $"Assets/{newNamespaceInput}/Scripts/Events/EventBus.{newNamespaceInput}.cs";
                EditorGUILayout.LabelField($"Будет создан: {previewPath}", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(newNamespaceInput));
            if (GUILayout.Button("✨ Создать EventBus файл", GUILayout.Height(30)))
            {
                string createdPath = EventBusEditorUtils.CreateEventBusFile(newNamespaceInput);
                if (!string.IsNullOrEmpty(createdPath))
                {
                    // Обновляем кэш
                    eventBusInfoCached = false;
                    cachedEventBusInfo = null;

                    // Открываем созданный файл
                    EventBusEditorUtils.OpenEventBusFile(createdPath);
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        #endregion

        #region ReorderableList Methods

        private void DrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "🔧 Системы для инициализации", EditorStyles.boldLabel);
        }

        private float GetElementHeight(int index)
        {
            // В режиме логов — компактная высота
            if (viewMode == SystemsViewMode.LogSettings)
            {
                return 44f; // Две строки: название + настройки
            }
            
            var element = systemsProperty.GetArrayElementAtIndex(index);

            // Базовая высота
            float height = 46f;

            // Добавляем место для зависимостей
            var dependencies = element.FindPropertyRelative("detectedDependencies");
            if (dependencies.arraySize > 0)
            {
                height += 20f;
            }

            // Добавляем место для предупреждений о циклах
            bool hasCyclicDependency = element.FindPropertyRelative("hasCyclicDependency").boolValue;
            if (hasCyclicDependency)
            {
                height += 40f;
            }
            
            // Добавляем место для метрик
            if (SystemMetricsSettings.ShowMetrics)
            {
                height += 22f; // Строка метрик
            }

            return height;
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = systemsProperty.GetArrayElementAtIndex(index);
            
            // В режиме логов — упрощённый вид
            if (viewMode == SystemsViewMode.LogSettings)
            {
                DrawElementLogMode(rect, element, index);
                return;
            }
            
            // Обычный режим
            DrawElementNormalMode(rect, element, index);
        }
        
        /// <summary>
        /// Отрисовка элемента в режиме настройки логов
        /// </summary>
        private void DrawElementLogMode(Rect rect, SerializedProperty element, int index)
        {
            rect.y += 2;
            rect.height -= 4;
            
            string systemName = element.FindPropertyRelative("systemName").stringValue;
            var logEnabled = element.FindPropertyRelative("logEnabled");
            var logLevel = element.FindPropertyRelative("logLevel");
            var logCategories = element.FindPropertyRelative("logCategories");
            var logColor = element.FindPropertyRelative("logColor");
            
            // Устанавливаем дефолтный цвет если белый (первый раз)
            if (logColor.colorValue == Color.white)
            {
                logColor.colorValue = GetDefaultSystemColor(index);
            }
            
            // Определяем, это ProtoSystem или кастомная система
            bool isProtoSystem = IsProtoSystemType(element);
            
            // Фон в зависимости от состояния логирования и типа системы
            Color bgColor;
            if (logEnabled.boolValue)
            {
                bgColor = isProtoSystem 
                    ? new Color(0.25f, 0.4f, 0.55f, 0.2f)   // Синеватый для ProtoSystem
                    : new Color(0.3f, 0.5f, 0.3f, 0.15f);   // Зеленоватый для кастомных
            }
            else
            {
                bgColor = new Color(0.3f, 0.3f, 0.3f, 0.1f);
            }
            EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 1, rect.width + 4, rect.height + 2), bgColor);
            
            float currentY = rect.y;
            
            // Первая строка: чекбокс + название + цвет
            Rect row1 = new Rect(rect.x, currentY, rect.width, 18);
            
            // Чекбокс логирования
            Rect enableRect = new Rect(row1.x, row1.y, 18, 18);
            logEnabled.boolValue = EditorGUI.Toggle(enableRect, logEnabled.boolValue);
            
            // Иконка типа системы
            string typeIcon = isProtoSystem ? "📦" : "🎮";
            Rect typeIconRect = new Rect(row1.x + 20, row1.y, 18, 18);
            EditorGUI.LabelField(typeIconRect, typeIcon);
            
            // Название системы
            Rect nameRect = new Rect(row1.x + 40, row1.y, row1.width - 100, 18);
            EditorGUI.LabelField(nameRect, systemName, logEnabled.boolValue ? EditorStyles.boldLabel : EditorStyles.label);
            
            // Цвет логов
            Rect colorRect = new Rect(row1.x + row1.width - 55, row1.y, 50, 16);
            logColor.colorValue = EditorGUI.ColorField(colorRect, GUIContent.none, logColor.colorValue, false, false, false);
            
            currentY += 20;
            
            // Вторая строка: уровень + категории (только если логирование включено)
            if (logEnabled.boolValue)
            {
                Rect row2 = new Rect(rect.x + 22, currentY, rect.width - 22, 18);
                
                // Уровень логирования (флаги)
                float levelX = row2.x;
                var levels = new (LogLevel level, string label, Color color, float width)[]
                {
                    (LogLevel.Errors, "Err", new Color(0.96f, 0.31f, 0.31f), 36),
                    (LogLevel.Warnings, "Warn", new Color(1f, 0.76f, 0.03f), 44),
                    (LogLevel.Info, "Info", new Color(0.5f, 0.8f, 0.5f), 36),
                    (LogLevel.Verbose, "Vrb", new Color(0.5f, 0.5f, 0.5f), 32),
                };
                
                var currentLevels = (LogLevel)logLevel.intValue;
                foreach (var lvl in levels)
                {
                    Rect btnRect = new Rect(levelX, row2.y, lvl.width, 16);
                    bool isEnabled = (currentLevels & lvl.level) != 0;
                    
                    var oldBg = GUI.backgroundColor;
                    if (isEnabled) GUI.backgroundColor = lvl.color;
                    
                    if (GUI.Button(btnRect, lvl.label, EditorStyles.miniButton))
                    {
                        // Переключаем флаг
                        if (isEnabled)
                            logLevel.intValue = (int)(currentLevels & ~lvl.level);
                        else
                            logLevel.intValue = (int)(currentLevels | lvl.level);
                    }
                    
                    GUI.backgroundColor = oldBg;
                    levelX += lvl.width + 2;
                }
                
                // Разделитель
                levelX += 12;
                
                // Категории
                var categories = new (LogCategory cat, string label, Color color, float width)[]
                {
                    (LogCategory.Initialization, "Init", new Color(0.30f, 0.69f, 0.31f), 34),
                    (LogCategory.Dependencies, "Dep", new Color(1f, 0.60f, 0f), 34),
                    (LogCategory.Events, "Event", new Color(0.13f, 0.59f, 0.95f), 42),
                    (LogCategory.Runtime, "Run", new Color(0.61f, 0.15f, 0.69f), 34)
                };
                
                var currentCategories = (LogCategory)logCategories.intValue;
                foreach (var cat in categories)
                {
                    Rect catRect = new Rect(levelX, row2.y, cat.width, 16);
                    bool isEnabled = (currentCategories & cat.cat) != 0;
                    
                    var oldBg = GUI.backgroundColor;
                    if (isEnabled) GUI.backgroundColor = cat.color;
                    
                    if (GUI.Button(catRect, cat.label, EditorStyles.miniButton))
                    {
                        if (isEnabled)
                            logCategories.intValue = (int)(currentCategories & ~cat.cat);
                        else
                            logCategories.intValue = (int)(currentCategories | cat.cat);
                    }
                    
                    GUI.backgroundColor = oldBg;
                    levelX += cat.width + 2;
                }
            }
            else
            {
                // Показываем подсказку что логирование выключено
                Rect hintRect = new Rect(rect.x + 22, currentY, rect.width - 22, 18);
                EditorGUI.LabelField(hintRect, "Логирование выключено", EditorStyles.centeredGreyMiniLabel);
            }
        }
        
        /// <summary>
        /// Проверяет, является ли система из пакета ProtoSystem
        /// </summary>
        private bool IsProtoSystemType(SerializedProperty element)
        {
            var existingObj = element.FindPropertyRelative("existingSystemObject").objectReferenceValue;
            if (existingObj != null)
            {
                string ns = existingObj.GetType().Namespace;
                return ns != null && ns.StartsWith("ProtoSystem");
            }
            
            string typeName = element.FindPropertyRelative("systemTypeName").stringValue;
            if (!string.IsNullOrEmpty(typeName))
            {
                return typeName.Contains("ProtoSystem");
            }
            
            return false;
        }
        
        /// <summary>
        /// Возвращает дефолтный цвет для системы по индексу
        /// </summary>
        private Color GetDefaultSystemColor(int index)
        {
            // Набор различимых цветов
            Color[] defaultColors = new Color[]
            {
                new Color(0.35f, 0.70f, 0.90f), // Голубой
                new Color(0.90f, 0.60f, 0.30f), // Оранжевый
                new Color(0.60f, 0.80f, 0.40f), // Салатовый
                new Color(0.85f, 0.45f, 0.55f), // Розовый
                new Color(0.70f, 0.55f, 0.85f), // Фиолетовый
                new Color(0.95f, 0.75f, 0.30f), // Жёлтый
                new Color(0.45f, 0.80f, 0.75f), // Бирюзовый
                new Color(0.85f, 0.55f, 0.40f), // Коралловый
                new Color(0.55f, 0.70f, 0.55f), // Зелёный приглушённый
                new Color(0.75f, 0.65f, 0.55f), // Бежевый
                new Color(0.60f, 0.60f, 0.85f), // Сиреневый
                new Color(0.80f, 0.70f, 0.50f), // Песочный
            };
            
            return defaultColors[index % defaultColors.Length];
        }
        
        /// <summary>
        /// Отрисовка элемента в обычном режиме
        /// </summary>
        private void DrawElementNormalMode(Rect rect, SerializedProperty element, int index)
        {
            rect.y += 2;
            rect.height -= 4;

            // Получаем данные
            bool enabled = element.FindPropertyRelative("enabled").boolValue;
            string systemName = element.FindPropertyRelative("systemName").stringValue;
            bool useExisting = element.FindPropertyRelative("useExistingObject").boolValue;
            bool hasCyclicDependency = element.FindPropertyRelative("hasCyclicDependency").boolValue;
            bool isDuplicate = duplicateNames.Contains(systemName);

            // Зависимости на выключенные системы → розовая подсветка
            var dependencies = element.FindPropertyRelative("detectedDependencies");
            bool depsOnDisabled = false;
            if (enabled)
            {
                for (int d = 0; d < dependencies.arraySize; d++)
                {
                    if (disabledNames.Contains(dependencies.GetArrayElementAtIndex(d).stringValue))
                    {
                        depsOnDisabled = true;
                        break;
                    }
                }
            }

            // Цвет фона (приоритет: цикл > дубль > зависимость на выключенную > обычный)
            Color bgColor = enabled ? (hasCyclicDependency ? Color.red : Color.green) : Color.gray;
            if (depsOnDisabled) bgColor = new Color(1f, 0.35f, 0.6f);
            if (isDuplicate) bgColor = new Color(1f, 0.6f, 0.1f);
            bgColor.a = (isDuplicate || depsOnDisabled) ? 0.18f : 0.1f;

            Rect bgRect = new Rect(rect.x - 2, rect.y - 1, rect.width + 4, rect.height + 2);
            EditorGUI.DrawRect(bgRect, bgColor);

            // Double-click → ping component
            if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2 && bgRect.Contains(Event.current.mousePosition))
            {
                var obj = element.FindPropertyRelative("existingSystemObject").objectReferenceValue;
                if (obj != null)
                {
                    EditorGUIUtility.PingObject(obj);
                    Selection.activeObject = obj;
                    Event.current.Use();
                }
            }

            float currentY = rect.y;

            // Основная строка
            Rect mainRect = new Rect(rect.x, currentY, rect.width, 18);

            // Иконка статуса
            string statusIcon = enabled ? (hasCyclicDependency ? "❌" : "✅") : "⭕";
            if (depsOnDisabled) statusIcon = "⛔";
            if (isDuplicate) statusIcon = "⚠️";
            Rect iconRect = new Rect(mainRect.x, mainRect.y, 25, 18);
            EditorGUI.LabelField(iconRect, statusIcon);

            // Чекбокс enabled
            Rect enabledRect = new Rect(mainRect.x + 27, mainRect.y, 18, 18);
            element.FindPropertyRelative("enabled").boolValue = EditorGUI.Toggle(enabledRect, enabled);

            // Имя системы
            Rect nameRect = new Rect(mainRect.x + 50, mainRect.y, mainRect.width - 145, 18);
            EditorGUI.LabelField(nameRect, systemName, EditorStyles.boldLabel);

            // Тип источника — коротко, полное описание в tooltip
            var sourceContent = useExisting
                ? new GUIContent("📦 Объект", "Используется существующий объект в сцене")
                : new GUIContent("🔨 Создать", "Объект будет создан при инициализации");
            Rect sourceRect = new Rect(mainRect.x + mainRect.width - 90, mainRect.y, 85, 18);
            EditorGUI.LabelField(sourceRect, sourceContent, EditorStyles.miniLabel);

            currentY += 20;

            // Вторая строка - тип класса и кнопка настроек
            Rect secondRowRect = new Rect(rect.x + 50, currentY, rect.width - 90, 18);

            var existingObj = element.FindPropertyRelative("existingSystemObject").objectReferenceValue;
            string typeName = existingObj != null ? existingObj.GetType().Name : element.FindPropertyRelative("systemTypeName").stringValue;

            if (string.IsNullOrEmpty(typeName))
            {
                typeName = "Тип не указан";
            }

            EditorGUI.LabelField(secondRowRect, $"Тип: {typeName}", EditorStyles.miniLabel);

            // Кнопка настроек
            Rect settingsRect = new Rect(rect.x + rect.width - 35, currentY, 30, 18);
            if (GUI.Button(settingsRect, "⚙️"))
            {
                ShowSystemEditWindow(element, index);
            }

            currentY += 22;

            // Зависимости (выключенные помечаются ⛔ и красятся розовым)
            if (dependencies.arraySize > 0)
            {
                Rect depsRect = new Rect(rect.x + 50, currentY, rect.width - 55, 18);
                string depsText = "🔗 Зависит от: ";
                for (int i = 0; i < dependencies.arraySize; i++)
                {
                    if (i > 0) depsText += ", ";
                    string depName = dependencies.GetArrayElementAtIndex(i).stringValue;
                    depsText += disabledNames.Contains(depName) ? $"⛔{depName}" : depName;
                }

                var oldDepColor = GUI.color;
                if (depsOnDisabled) GUI.color = new Color(1f, 0.55f, 0.7f);
                EditorGUI.LabelField(depsRect, new GUIContent(depsText, depsOnDisabled
                        ? "⛔ — система выключена, зависимость не будет заинжектена"
                        : null), EditorStyles.miniLabel);
                GUI.color = oldDepColor;
                currentY += 20;
            }

            // Предупреждение о цикле
            if (hasCyclicDependency)
            {
                string cyclicInfo = element.FindPropertyRelative("cyclicDependencyInfo").stringValue;
                Rect warningRect = new Rect(rect.x + 25, currentY, rect.width - 30, 36);

                var oldColor = GUI.color;
                GUI.color = new Color(1f, 0.3f, 0.3f);
                EditorGUI.HelpBox(warningRect, $"Циклическая зависимость: {cyclicInfo}", MessageType.Error);
                GUI.color = oldColor;
                currentY += 40;
            }
            
            // Метрики системы
            if (SystemMetricsSettings.ShowMetrics)
            {
                DrawSystemMetrics(rect, currentY, index);
            }
        }
        
        /// <summary>
        /// Отрисовка метрик для системы
        /// </summary>
        private void DrawSystemMetrics(Rect rect, float y, int index)
        {
            SystemInitializationManager manager = target as SystemInitializationManager;
            if (manager == null || index >= manager.Systems.Count) return;
            
            var entry = manager.Systems[index];
            var metrics = SystemMetricsCache.GetMetrics(entry);
            
            if (!metrics.IsValid)
            {
                Rect invalidRect = new Rect(rect.x + 50, y, rect.width - 55, 18);
                EditorGUI.LabelField(invalidRect, "📊 Метрики недоступны", EditorStyles.miniLabel);
                return;
            }
            
            // Строка метрик с прогресс-барами
            float startX = rect.x + 50;
            float itemWidth = (rect.width - 60) / 3f;
            
            // LOC
            DrawMetricWithBar(
                new Rect(startX, y, itemWidth - 5, 18),
                $"📝 {metrics.LinesOfCode} LOC",
                metrics.LinesOfCode,
                SystemMetricsSettings.LocWarningThreshold,
                SystemMetricsSettings.LocErrorThreshold);
            
            // KB
            DrawMetricWithBar(
                new Rect(startX + itemWidth, y, itemWidth - 5, 18),
                $"💾 {metrics.FileSizeKB:F1} KB",
                metrics.FileSizeKB,
                SystemMetricsSettings.KbWarningThreshold,
                SystemMetricsSettings.KbErrorThreshold);
            
            // Methods
            DrawMetricWithBar(
                new Rect(startX + itemWidth * 2, y, itemWidth - 5, 18),
                $"🔧 {metrics.MethodCount} методов",
                metrics.MethodCount,
                SystemMetricsSettings.MethodsWarningThreshold,
                SystemMetricsSettings.MethodsErrorThreshold);
        }
        
        /// <summary>
        /// Отрисовка метрики с цветовым индикатором
        /// </summary>
        private void DrawMetricWithBar(Rect rect, string label, float value, float warningThreshold, float errorThreshold)
        {
            // Определяем цвет
            Color barColor;
            if (value >= errorThreshold)
            {
                barColor = new Color(1f, 0.3f, 0.3f, 0.5f); // Красный
            }
            else if (value >= warningThreshold)
            {
                barColor = new Color(1f, 0.8f, 0.2f, 0.5f); // Жёлтый
            }
            else
            {
                barColor = new Color(0.3f, 0.8f, 0.3f, 0.3f); // Зелёный
            }
            
            // Рисуем фон-индикатор
            float progress = Mathf.Clamp01(value / errorThreshold);
            Rect barRect = new Rect(rect.x, rect.y + 14, rect.width * progress, 3);
            EditorGUI.DrawRect(barRect, barColor);
            
            // Рисуем текст
            var style = new GUIStyle(EditorStyles.miniLabel);
            if (value >= errorThreshold)
            {
                style.normal.textColor = new Color(1f, 0.4f, 0.4f);
            }
            else if (value >= warningThreshold)
            {
                style.normal.textColor = new Color(1f, 0.85f, 0.3f);
            }
            
            EditorGUI.LabelField(rect, label, style);
        }

        private void ShowSystemEditWindow(SerializedProperty element, int index)
        {
            SystemEditWindow.ShowWindow(element, serializedObject);
        }

        private void OnAddElement(ReorderableList list)
        {
            int index = systemsProperty.arraySize;
            systemsProperty.arraySize++;

            var element = systemsProperty.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("systemName").stringValue = $"NewSystem_{index}";
            element.FindPropertyRelative("enabled").boolValue = true;
            element.FindPropertyRelative("useExistingObject").boolValue = false;
            element.FindPropertyRelative("existingSystemObject").objectReferenceValue = null;
            element.FindPropertyRelative("systemTypeName").stringValue = "";
            element.FindPropertyRelative("verboseLogging").boolValue = true;

            var dependencies = element.FindPropertyRelative("detectedDependencies");
            dependencies.arraySize = 0;
            element.FindPropertyRelative("hasCyclicDependency").boolValue = false;
            element.FindPropertyRelative("cyclicDependencyInfo").stringValue = "";
        }

        private void OnRemoveElement(ReorderableList list)
        {
            if (list.index >= 0 && list.index < systemsProperty.arraySize)
            {
                systemsProperty.DeleteArrayElementAtIndex(list.index);
            }
        }

        #endregion
    }

    /// <summary>
    /// Окно редактирования системы
    /// </summary>
    public class SystemEditWindow : UnityEditor.EditorWindow
    {
        private SerializedProperty systemProperty;
        private SerializedObject parentObject;

        public static void ShowWindow(SerializedProperty property, SerializedObject parent)
        {
            var window = GetWindow<SystemEditWindow>("Редактирование системы");
            window.systemProperty = property;
            window.parentObject = parent;
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            if (systemProperty == null || parentObject == null)
            {
                EditorGUILayout.HelpBox("⚠️ Система не выбрана", MessageType.Warning);
                return;
            }

            parentObject.Update();

            EditorGUILayout.LabelField("⚙️ Настройки системы", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // Основные настройки
            EditorGUILayout.PropertyField(systemProperty.FindPropertyRelative("systemName"),
                new GUIContent("🏷️ Имя системы"));
            EditorGUILayout.PropertyField(systemProperty.FindPropertyRelative("enabled"),
                new GUIContent("✅ Включена"));
            EditorGUILayout.PropertyField(systemProperty.FindPropertyRelative("verboseLogging"),
                new GUIContent("📝 Подробные логи"));

            EditorGUILayout.Space(10);

            // Источник системы
            EditorGUILayout.LabelField("🔧 Источник системы", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(systemProperty.FindPropertyRelative("useExistingObject"),
                new GUIContent("📦 Использовать существующий объект"));

            bool useExisting = systemProperty.FindPropertyRelative("useExistingObject").boolValue;
            if (useExisting)
            {
                EditorGUILayout.PropertyField(systemProperty.FindPropertyRelative("existingSystemObject"),
                    new GUIContent("🎯 Объект в сцене"));
            }
            else
            {
                EditorGUILayout.PropertyField(systemProperty.FindPropertyRelative("systemTypeName"),
                    new GUIContent("📋 Полное имя типа"));
            }

            EditorGUILayout.Space(10);

            // Зависимости (только для чтения)
            EditorGUILayout.LabelField("🔗 Обнаруженные зависимости", EditorStyles.boldLabel);
            var dependencies = systemProperty.FindPropertyRelative("detectedDependencies");
            if (dependencies.arraySize > 0)
            {
                for (int i = 0; i < dependencies.arraySize; i++)
                {
                    EditorGUILayout.LabelField($"  • {dependencies.GetArrayElementAtIndex(i).stringValue}");
                }
            }
            else
            {
                EditorGUILayout.LabelField("  🆓 Нет зависимостей", EditorStyles.miniLabel);
            }

            // Предупреждение о цикле
            bool hasCyclic = systemProperty.FindPropertyRelative("hasCyclicDependency").boolValue;
            if (hasCyclic)
            {
                EditorGUILayout.Space(10);
                string cyclicInfo = systemProperty.FindPropertyRelative("cyclicDependencyInfo").stringValue;
                EditorGUILayout.HelpBox($"⚠️ ЦИКЛИЧЕСКАЯ ЗАВИСИМОСТЬ: {cyclicInfo}", MessageType.Error);
            }

            parentObject.ApplyModifiedProperties();
        }
    }
}
