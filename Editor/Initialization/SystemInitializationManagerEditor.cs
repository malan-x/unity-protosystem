using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace ProtoSystem
{
    /// <summary>
    /// Кастомный редактор для SystemInitializationManager
    /// </summary>
    [CustomEditor(typeof(SystemInitializationManager))]
    public class SystemInitializationManagerEditor : Editor
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

        private void OnEnable()
        {
            systemsProperty = serializedObject.FindProperty("systems");
            SetupStyles();
            CreateSystemsList();
        }

        private void SetupStyles()
        {
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
            serializedObject.Update();
            SystemInitializationManager manager = target as SystemInitializationManager;

            // Заголовок
            EditorGUILayout.Space(10);
            GUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("⚙️ Менеджер Инициализации Систем", headerStyle);
            EditorGUILayout.Space(5);

            // Статус инициализации
            if (Application.isPlaying)
            {
                DrawRuntimeStatus(manager);
                EditorGUILayout.Space(5);
            }

            GUILayout.EndVertical();

            EditorGUILayout.Space(10);

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

            // EventBus проекта
            DrawProjectEventBusSection();

            serializedObject.ApplyModifiedProperties();
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
            EditorGUILayout.LabelField($"Общий прогресс: {(progress * 100):F1}%");

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

            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoStartInitialization"),
                new GUIContent("🚀 Автозапуск", "Автоматически запускать инициализацию при старте"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxInitializationTimeoutSeconds"),
                new GUIContent("⏱️ Таймаут (сек)", "Максимальное время инициализации одной системы"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("verboseLogging"),
                new GUIContent("📝 Подробные логи", "Выводить детальную информацию в консоль"));
        }

        private void DrawSystemsSection(SystemInitializationManager manager)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"🔧 Системы ({manager.Systems.Count})", EditorStyles.boldLabel);

            // Счетчики статусов
            int enabledCount = 0, disabledCount = 0, errorCount = 0;
            foreach (var system in manager.Systems)
            {
                if (system.enabled) enabledCount++;
                else disabledCount++;
                if (system.hasCyclicDependency) errorCount++;
            }

            GUILayout.FlexibleSpace();

            if (enabledCount > 0)
            {
                EditorGUILayout.LabelField($"✅ {enabledCount}", GUILayout.Width(40));
            }
            if (disabledCount > 0)
            {
                EditorGUILayout.LabelField($"⭕ {disabledCount}", GUILayout.Width(40));
            }
            if (errorCount > 0)
            {
                var oldColor = GUI.color;
                GUI.color = Color.red;
                EditorGUILayout.LabelField($"❌ {errorCount}", GUILayout.Width(40));
                GUI.color = oldColor;
            }

            EditorGUILayout.EndHorizontal();

            systemsList.DoLayoutList();
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
            EditorGUILayout.BeginVertical("Box", GUILayout.Width(150));
            EditorGUILayout.LabelField("⚠️ Проблемы", EditorStyles.centeredGreyMiniLabel);
            if (problemsCount > 0)
            {
                var oldColor = GUI.color;
                GUI.color = Color.red;
                EditorGUILayout.LabelField($"Циклы: {problemsCount}", EditorStyles.miniLabel);
                GUI.color = oldColor;
            }
            else
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField("Проблем нет ✅", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
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

            return height;
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = systemsProperty.GetArrayElementAtIndex(index);

            rect.y += 2;
            rect.height -= 4;

            // Получаем данные
            bool enabled = element.FindPropertyRelative("enabled").boolValue;
            string systemName = element.FindPropertyRelative("systemName").stringValue;
            bool useExisting = element.FindPropertyRelative("useExistingObject").boolValue;
            bool hasCyclicDependency = element.FindPropertyRelative("hasCyclicDependency").boolValue;

            // Цвет фона
            Color bgColor = enabled ? (hasCyclicDependency ? Color.red : Color.green) : Color.gray;
            bgColor.a = 0.1f;

            Rect bgRect = new Rect(rect.x - 2, rect.y - 1, rect.width + 4, rect.height + 2);
            EditorGUI.DrawRect(bgRect, bgColor);

            float currentY = rect.y;

            // Основная строка
            Rect mainRect = new Rect(rect.x, currentY, rect.width, 18);

            // Иконка статуса
            string statusIcon = enabled ? (hasCyclicDependency ? "❌" : "✅") : "⭕";
            Rect iconRect = new Rect(mainRect.x, mainRect.y, 25, 18);
            EditorGUI.LabelField(iconRect, statusIcon);

            // Чекбокс enabled
            Rect enabledRect = new Rect(mainRect.x + 27, mainRect.y, 18, 18);
            element.FindPropertyRelative("enabled").boolValue = EditorGUI.Toggle(enabledRect, enabled);

            // Имя системы
            Rect nameRect = new Rect(mainRect.x + 50, mainRect.y, mainRect.width - 180, 18);
            EditorGUI.LabelField(nameRect, systemName, EditorStyles.boldLabel);

            // Тип источника
            string sourceType = useExisting ? "📦 Существующий объект" : "🔨 Создать новый";
            Rect sourceRect = new Rect(mainRect.x + mainRect.width - 160, mainRect.y, 125, 18);
            EditorGUI.LabelField(sourceRect, sourceType, EditorStyles.miniLabel);

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

            // Зависимости
            var dependencies = element.FindPropertyRelative("detectedDependencies");
            if (dependencies.arraySize > 0)
            {
                Rect depsRect = new Rect(rect.x + 50, currentY, rect.width - 55, 18);
                string depsText = "🔗 Зависит от: ";
                for (int i = 0; i < dependencies.arraySize; i++)
                {
                    if (i > 0) depsText += ", ";
                    depsText += dependencies.GetArrayElementAtIndex(i).stringValue;
                }
                EditorGUI.LabelField(depsRect, depsText, EditorStyles.miniLabel);
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
            }
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
    public class SystemEditWindow : EditorWindow
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
