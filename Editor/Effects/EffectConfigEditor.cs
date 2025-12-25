using UnityEngine;
using UnityEditor;
using ProtoSystem.Effects;

namespace ProtoSystem.Effects.Editor
{
    /// <summary>
    /// Красивый кастомный редактор для EffectConfig с поддержкой:
    /// - Многоуровневого выбора событий через dropdown
    /// - Настроек инстанцирования (пул/объект/мир)
    /// - Визуальной валидации
    /// - Группировки параметров по секциям
    /// </summary>
    [CustomEditor(typeof(EffectConfig))]
    public class EffectConfigEditorNew : UnityEditor.Editor
    {
        private EffectConfig config;
        
        // Foldout состояния
        private bool showBasicInfo = true;
        private bool showEffectSettings = true;
        private bool showSpawnSettings = true;
        private bool showAutoTrigger = true;
        private bool showAdvanced = false;

        // Стили
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle eventPathStyle;
        private bool stylesInitialized = false;

        // Цвета секций
        private static readonly Color BasicInfoColor = new Color(0.4f, 0.7f, 1f, 0.3f);
        private static readonly Color EffectSettingsColor = new Color(0.5f, 0.9f, 0.5f, 0.3f);
        private static readonly Color SpawnSettingsColor = new Color(1f, 0.7f, 0.3f, 0.3f);
        private static readonly Color AutoTriggerColor = new Color(0.9f, 0.5f, 0.9f, 0.3f);
        private static readonly Color AdvancedColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);

        private void OnEnable()
        {
            config = (EffectConfig)target;
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(0, 0, 5, 5)
            };

            boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };

            eventPathStyle = new GUIStyle(EditorStyles.textField)
            {
                fontStyle = FontStyle.Bold
            };

            stylesInitialized = true;
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();
            serializedObject.Update();

            DrawHeader();
            
            EditorGUILayout.Space(5);

            DrawBasicInfoSection();
            DrawEffectTypeSection();
            DrawSpawnSettingsSection();
            DrawAutoTriggerSection();
            DrawAdvancedSection();
            DrawValidationSection();

            serializedObject.ApplyModifiedProperties();
        }

        private new void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            
            // Иконка типа эффекта
            var icon = GetEffectTypeIcon(config.effectType);
            EditorGUILayout.LabelField(icon, GUILayout.Width(30), GUILayout.Height(30));

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Effect Config Editor", EditorStyles.boldLabel);
            
            var statusColor = config.IsValid() ? Color.green : Color.red;
            var statusText = config.IsValid() ? "✓ Валидно" : "✗ Есть ошибки";
            var oldColor = GUI.color;
            GUI.color = statusColor;
            EditorGUILayout.LabelField(statusText, EditorStyles.miniLabel);
            GUI.color = oldColor;
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBasicInfoSection()
        {
            DrawSectionHeader("📋 Основная информация", ref showBasicInfo, BasicInfoColor);

            if (showBasicInfo)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                // ID эффекта с кнопкой переименования
                DrawEffectIdWithRename();
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("📝 Название", "Отображаемое имя для UI"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("description"), new GUIContent("📖 Описание"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("tags"), new GUIContent("🏷️ Тэги", "Для фильтрации и поиска"));

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawEffectIdWithRename()
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty("effectId"), new GUIContent("🆔 ID эффекта", "Уникальный идентификатор"));
            
            // Проверяем нужно ли показывать кнопку
            var assetPath = AssetDatabase.GetAssetPath(target);
            var currentAssetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var effectId = config.effectId;
            
            if (!string.IsNullOrEmpty(effectId) && currentAssetName != effectId)
            {
                // Проверяем существует ли ассет с таким именем
                var directory = System.IO.Path.GetDirectoryName(assetPath);
                var newPath = System.IO.Path.Combine(directory, effectId + ".asset");
                var existingAsset = AssetDatabase.LoadAssetAtPath<EffectConfig>(newPath);
                bool willOverwrite = existingAsset != null && existingAsset != target;
                
                if (willOverwrite)
                {
                    // Красная кнопка с предупреждением
                    var oldBgColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                    if (GUILayout.Button(new GUIContent("⚠️ Перезаписать!", $"Файл '{effectId}.asset' уже существует и будет перезаписан!"), GUILayout.Width(110)))
                    {
                        if (EditorUtility.DisplayDialog("Перезапись файла", 
                            $"Файл '{effectId}.asset' уже существует!\n\nВы уверены, что хотите его перезаписать?", 
                            "Перезаписать", "Отмена"))
                        {
                            RenameAsset(assetPath, effectId);
                        }
                    }
                    GUI.backgroundColor = oldBgColor;
                }
                else
                {
                    // Обычная кнопка переименования
                    if (GUILayout.Button(new GUIContent("📝 Переименовать", $"Переименовать ассет в '{effectId}.asset'"), GUILayout.Width(110)))
                    {
                        RenameAsset(assetPath, effectId);
                    }
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Предупреждение под полем если будет перезапись
            if (!string.IsNullOrEmpty(effectId) && currentAssetName != effectId)
            {
                var directory = System.IO.Path.GetDirectoryName(assetPath);
                var newPath = System.IO.Path.Combine(directory, effectId + ".asset");
                var existingAsset = AssetDatabase.LoadAssetAtPath<EffectConfig>(newPath);
                
                if (existingAsset != null && existingAsset != target)
                {
                    var oldColor = GUI.color;
                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    EditorGUILayout.HelpBox($"⚠️ Файл '{effectId}.asset' уже существует и будет перезаписан!", MessageType.Warning);
                    GUI.color = oldColor;
                }
            }
        }

        private void RenameAsset(string currentPath, string newName)
        {
            var error = AssetDatabase.RenameAsset(currentPath, newName);
            if (string.IsNullOrEmpty(error))
            {
                Debug.Log($"[EffectConfig] Ассет переименован в '{newName}'");
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogError($"[EffectConfig] Ошибка переименования: {error}");
            }
        }

        private void DrawEffectTypeSection()
        {
            DrawSectionHeader("🎭 Настройки эффекта", ref showEffectSettings, EffectSettingsColor);

            if (showEffectSettings)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("effectType"), new GUIContent("Тип эффекта"));

                EditorGUILayout.Space(5);

                switch (config.effectType)
                {
                    case EffectConfig.EffectType.VFX:
                    case EffectConfig.EffectType.Particle:
                        DrawVFXSettings();
                        break;
                    case EffectConfig.EffectType.Audio:
                        DrawAudioSettings();
                        break;
                    case EffectConfig.EffectType.UI:
                    case EffectConfig.EffectType.ScreenEffect:
                        DrawUISettings();
                        break;
                    case EffectConfig.EffectType.Combined:
                        DrawCombinedSettings();
                        break;
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawVFXSettings()
        {
            EditorGUILayout.LabelField("🎨 VFX Настройки", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("vfxPrefab"), new GUIContent("Префаб"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lifetime"), new GUIContent("⏱️ Время жизни"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("offset"), new GUIContent("📍 Смещение"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rotation"), new GUIContent("🔄 Поворот"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("scale"), new GUIContent("📐 Масштаб"));
        }

        private void DrawAudioSettings()
        {
            EditorGUILayout.LabelField("🔊 Audio Настройки", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioClip"), new GUIContent("Аудио клип"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("volume"), new GUIContent("🔉 Громкость"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pitch"), new GUIContent("🎵 Тон"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("spatial"), new GUIContent("📍 Пространственный"));
        }

        private void DrawUISettings()
        {
            EditorGUILayout.LabelField("🖼️ UI Настройки", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("uiPrefab"), new GUIContent("UI Префаб"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("uiPosition"), new GUIContent("📍 Позиция"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("uiScale"), new GUIContent("📐 Масштаб"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("uiDisplayTime"), new GUIContent("⏱️ Время показа"));
            
            EditorGUILayout.Space(10);
            
            // Анимация появления
            EditorGUILayout.LabelField("✨ Анимация появления", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("uiShowAnimation"), new GUIContent("Тип"));
            
            if (config.uiShowAnimation != UIAnimationType.None)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("uiShowDuration"), new GUIContent("⏱️ Длительность"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("uiShowEase"), new GUIContent("📈 Easing"));
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(5);
            
            // Анимация исчезновения
            EditorGUILayout.LabelField("💨 Анимация исчезновения", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("uiHideAnimation"), new GUIContent("Тип"));
            
            if (config.uiHideAnimation != UIAnimationType.None)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("uiHideDuration"), new GUIContent("⏱️ Длительность"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("uiHideEase"), new GUIContent("📈 Easing"));
                EditorGUI.indentLevel--;
            }
        }

        private void DrawCombinedSettings()
        {
            EditorGUILayout.LabelField("🎭 Комбинированный эффект", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Комбинированный эффект может содержать VFX, Audio и UI компоненты.", MessageType.Info);
            
            EditorGUILayout.Space(5);
            DrawVFXSettings();
            EditorGUILayout.Space(5);
            DrawAudioSettings();
            EditorGUILayout.Space(5);
            DrawUISettings();
        }

        private void DrawSpawnSettingsSection()
        {
            DrawSectionHeader("🎯 Режим пространства", ref showSpawnSettings, SpawnSettingsColor);

            if (showSpawnSettings)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                // Категория эффекта
                EditorGUILayout.PropertyField(serializedObject.FindProperty("category"), new GUIContent("📂 Категория", "Определяет требования к данным события"));
                
                EditorGUILayout.Space(5);
                
                // Информация о категории
                switch (config.category)
                {
                    case EffectCategory.Spatial:
                        EditorGUILayout.HelpBox("🎨 Spatial: VFX/Particle эффект — требует IEffectTarget для позиционирования.", MessageType.Info);
                        break;
                    case EffectCategory.Audio:
                        EditorGUILayout.HelpBox("🔊 Audio: Звуковой эффект — может быть пространственным (требует IEffectTarget) или глобальным.", MessageType.Info);
                        break;
                    case EffectCategory.Screen:
                        EditorGUILayout.HelpBox("📺 Screen: UI/ScreenEffect — не требует позиции, работает с любым событием.", MessageType.Info);
                        break;
                }

                EditorGUILayout.Space(10);

                // Режим пространства (только для Spatial)
                if (config.category == EffectCategory.Spatial || 
                    (config.category == EffectCategory.Audio && config.spatial))
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("spaceMode"), new GUIContent("🌐 Режим", "Все эффекты используют пул"));

                    EditorGUILayout.Space(5);
                    
                    switch (config.spaceMode)
                    {
                        case EffectSpaceMode.WorldSpace:
                            EditorGUILayout.HelpBox("🌍 WorldSpace: Эффект активируется в пуле, не привязывается к объекту.", MessageType.None);
                            break;
                        case EffectSpaceMode.LocalSpace:
                            EditorGUILayout.HelpBox("📎 LocalSpace: Эффект временно становится дочерним объектом цели, возвращается в пул после завершения.", MessageType.None);
                            
                            EditorGUILayout.Space(5);
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("attachBoneName"), new GUIContent("🦴 Кость", "Имя кости для привязки (опционально)"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("localOffset"), new GUIContent("📍 Смещение", "Локальное смещение относительно точки привязки"));
                            break;
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawAutoTriggerSection()
        {
            DrawSectionHeader("⚡ Автоматические триггеры", ref showAutoTrigger, AutoTriggerColor);

            if (showAutoTrigger)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("autoTrigger"), new GUIContent("🔄 Включить авто-триггер"));

                if (config.autoTrigger)
                {
                    EditorGUILayout.Space(10);

                    // === СОБЫТИЕ ЗАПУСКА ===
                    EditorGUILayout.LabelField("▶️ Событие запуска", EditorStyles.boldLabel);

                    DrawEventPathSelector("triggerEventPath", "Событие", ref config.triggerEventPath);

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("triggerCondition"), new GUIContent("🔍 Условие", "Дополнительное условие для фильтрации"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("passEventData"), new GUIContent("📍 Использовать данные события", "Позиция эффекта из данных события"));

                    EditorGUILayout.Space(10);

                    // === СОБЫТИЕ ОСТАНОВКИ ===
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("autoStop"), new GUIContent("⏹️ Авто-остановка"));

                    if (config.autoStop)
                    {
                        DrawEventPathSelector("stopEventPath", "Событие остановки", ref config.stopEventPath);
                    }

                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(
                        "Эффект автоматически запустится при получении указанного события.\n" +
                        "Если включена авто-остановка, эффект прекратится при получении события остановки.",
                        MessageType.Info);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawEventPathSelector(string propertyName, string label, ref string currentPath)
        {
            DrawEventPathSelector(propertyName, label, ref currentPath, config.category);
        }

        private void DrawEventPathSelector(string propertyName, string label, ref string currentPath, EffectCategory category)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PrefixLabel(new GUIContent(label));

            // Отображаем текущий путь
            var displayText = string.IsNullOrEmpty(currentPath) ? "(Не выбрано)" : $"Evt.{currentPath}";

            // Цвет валидации с учётом соответствия категории
            var oldBgColor = GUI.backgroundColor;
            if (!string.IsNullOrEmpty(currentPath) && !EventPathResolver.Exists(currentPath))
            {
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f); // Красный - не найдено
            }
            else if (string.IsNullOrEmpty(currentPath))
            {
                GUI.backgroundColor = new Color(0.9f, 0.9f, 0.7f); // Жёлтый - пусто
            }
            else if (EventPathDrawer.IsEventSuitableForCategory(currentPath, category))
            {
                GUI.backgroundColor = new Color(0.5f, 1f, 0.5f); // Ярко-зелёный - рекомендуется
            }
            else
            {
                GUI.backgroundColor = new Color(0.7f, 0.85f, 0.7f); // Бледно-зелёный - нормально
            }

            EditorGUILayout.TextField(displayText, eventPathStyle);
            GUI.backgroundColor = oldBgColor;

            // Кнопка выбора
            if (GUILayout.Button("▼", GUILayout.Width(25)))
            {
                ShowEventSelectionMenu(currentPath, category, (selected) =>
                {
                    var prop = serializedObject.FindProperty(propertyName);
                    prop.stringValue = selected;
                    serializedObject.ApplyModifiedProperties();
                    config.InvalidateEventCache();
                });
            }

            // Кнопка очистки
            if (!string.IsNullOrEmpty(currentPath))
            {
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    var prop = serializedObject.FindProperty(propertyName);
                    prop.stringValue = "";
                    serializedObject.ApplyModifiedProperties();
                    config.InvalidateEventCache();
                }
            }

            EditorGUILayout.EndHorizontal();

            // Показываем ID события и подсказку о соответствии категории
            if (!string.IsNullOrEmpty(currentPath))
            {
                EditorGUI.indentLevel++;
                var eventId = EventPathResolver.Resolve(currentPath);
                if (eventId > 0)
                {
                    var isSuitable = EventPathDrawer.IsEventSuitableForCategory(currentPath, category);
                    var suitabilityHint = isSuitable ? "✓ рекомендуется для данной категории" : "";
                    EditorGUILayout.LabelField($"Event ID: {eventId} {suitabilityHint}", EditorStyles.miniLabel);
                }
                else
                {
                    var oldColor = GUI.color;
                    GUI.color = Color.red;
                    EditorGUILayout.LabelField("⚠️ Событие не найдено!", EditorStyles.miniLabel);
                    GUI.color = oldColor;
                }
                EditorGUI.indentLevel--;
            }
        }

        private void ShowEventSelectionMenu(string currentPath, EffectCategory category, System.Action<string> onSelected)
        {
            // Используем улучшенное меню с подсветкой категорий
            var menu = EventPathDrawer.CreateEventSelectionMenuWithHighlight(currentPath, category, onSelected);
            menu.ShowAsContext();
        }

        private void DrawAdvancedSection()
        {
            DrawSectionHeader("🔧 Дополнительно", ref showAdvanced, AdvancedColor);

            if (showAdvanced)
            {
                EditorGUILayout.BeginVertical(boxStyle);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("priority"), new GUIContent("⭐ Приоритет"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("canBeInterrupted"), new GUIContent("🔄 Может быть прерван"));

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawValidationSection()
        {
            if (!config.IsValid())
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox($"⚠️ Ошибки валидации:\n{config.GetValidationErrors()}", MessageType.Error);
            }
        }

        private void DrawSectionHeader(string title, ref bool foldout, Color color)
        {
            EditorGUILayout.Space(5);

            var rect = EditorGUILayout.GetControlRect(false, 22);
            
            // Фон секции
            var bgRect = new Rect(rect.x - 15, rect.y, rect.width + 30, rect.height);
            EditorGUI.DrawRect(bgRect, color);

            // Заголовок
            foldout = EditorGUI.Foldout(rect, foldout, title, true, headerStyle);
        }

        private string GetEffectTypeIcon(EffectConfig.EffectType type)
        {
            return type switch
            {
                EffectConfig.EffectType.VFX => "🎨",
                EffectConfig.EffectType.Audio => "🔊",
                EffectConfig.EffectType.UI => "🖼️",
                EffectConfig.EffectType.ScreenEffect => "📺",
                EffectConfig.EffectType.Particle => "✨",
                EffectConfig.EffectType.Combined => "🎭",
                _ => "❓"
            };
        }
    }
}
