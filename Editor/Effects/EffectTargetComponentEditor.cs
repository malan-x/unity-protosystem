using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ProtoSystem.Effects;

namespace ProtoSystem.Effects.Editor
{
    /// <summary>
    /// Custom Editor для EffectTargetComponent.
    /// Показывает предупреждения о зацикливании и удобный выбор событий.
    /// </summary>
    [CustomEditor(typeof(EffectTargetComponent))]
    public class EffectTargetComponentEditor : UnityEditor.Editor
    {
        private EffectTargetComponent component;
        private SerializedProperty defaultAttachPointProp;
        private SerializedProperty attachPointsProp;
        private SerializedProperty defaultOffsetProp;
        private SerializedProperty defaultScaleProp;
        private SerializedProperty reactToEventsProp;
        private SerializedProperty forwardEventsProp;
        private SerializedProperty forwardAttachPointProp;

        private bool showAttachPoints = true;
        private bool showEventForwarding = true;
        private GUIStyle warningBoxStyle;
        private GUIStyle errorBoxStyle;

        private void OnEnable()
        {
            component = (EffectTargetComponent)target;
            
            defaultAttachPointProp = serializedObject.FindProperty("defaultAttachPoint");
            attachPointsProp = serializedObject.FindProperty("attachPoints");
            defaultOffsetProp = serializedObject.FindProperty("defaultOffset");
            defaultScaleProp = serializedObject.FindProperty("defaultScale");
            reactToEventsProp = serializedObject.FindProperty("reactToEvents");
            forwardEventsProp = serializedObject.FindProperty("forwardEvents");
            forwardAttachPointProp = serializedObject.FindProperty("forwardAttachPoint");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InitStyles();

            // === ЗАГОЛОВОК ===
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("🎯 Effect Target Component", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Компонент для привязки эффектов к объекту.\nПозволяет переадресовывать события без правки кода.", MessageType.Info);
            EditorGUILayout.Space(10);

            // === ТОЧКИ ПРИВЯЗКИ ===
            showAttachPoints = EditorGUILayout.BeginFoldoutHeaderGroup(showAttachPoints, "📍 Точки привязки");
            if (showAttachPoints)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(defaultAttachPointProp, new GUIContent("Основная точка"));
                EditorGUILayout.PropertyField(defaultOffsetProp, new GUIContent("Смещение"));
                EditorGUILayout.PropertyField(defaultScaleProp, new GUIContent("Масштаб"));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(attachPointsProp, new GUIContent("Дополнительные точки"), true);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(10);

            // === ПЕРЕАДРЕСАЦИЯ СОБЫТИЙ ===
            showEventForwarding = EditorGUILayout.BeginFoldoutHeaderGroup(showEventForwarding, "🔄 Переадресация событий");
            if (showEventForwarding)
            {
                EditorGUI.indentLevel++;

                // Проверка на зацикливание
                if (component.HasLoopWarning(out var loopedEvents))
                {
                    EditorGUILayout.BeginVertical(errorBoxStyle);
                    EditorGUILayout.LabelField("⚠️ ОБНАРУЖЕНО ЗАЦИКЛИВАНИЕ!", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"События: {string.Join(", ", loopedEvents)}");
                    EditorGUILayout.LabelField("Эти события будут проигнорированы при запуске.", EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }

                // События на которые реагируем
                EditorGUILayout.LabelField("📥 Входные события", EditorStyles.boldLabel);
                DrawEventArray(reactToEventsProp, "Компонент будет реагировать на эти события");

                EditorGUILayout.Space(10);

                // События которые публикуем
                EditorGUILayout.LabelField("📤 Выходные события", EditorStyles.boldLabel);
                DrawEventArray(forwardEventsProp, "Компонент опубликует эти события с данными о себе");

                EditorGUILayout.Space(5);

                // Точка привязки для переадресации
                DrawAttachPointSelector();

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // === ДОСТУПНЫЕ ТОЧКИ ===
            EditorGUILayout.Space(10);
            if (GUILayout.Button("📋 Показать доступные точки привязки"))
            {
                var points = component.GetAvailableAttachPoints();
                Debug.Log($"[EffectTargetComponent] Доступные точки на {component.name}:\n• {string.Join("\n• ", points)}");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void InitStyles()
        {
            if (warningBoxStyle == null)
            {
                warningBoxStyle = new GUIStyle(EditorStyles.helpBox);
                warningBoxStyle.normal.background = MakeTex(2, 2, new Color(1f, 0.9f, 0.5f, 0.3f));
            }

            if (errorBoxStyle == null)
            {
                errorBoxStyle = new GUIStyle(EditorStyles.helpBox);
                errorBoxStyle.normal.background = MakeTex(2, 2, new Color(1f, 0.5f, 0.5f, 0.3f));
            }
        }

        private void DrawEventArray(SerializedProperty arrayProp, string tooltip)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                var element = arrayProp.GetArrayElementAtIndex(i);
                var currentPath = element.stringValue;
                
                // Валидация события
                var isValid = !string.IsNullOrEmpty(currentPath) && EventPathResolver.Exists(currentPath);
                var isEmpty = string.IsNullOrEmpty(currentPath);
                
                // Цвет фона
                var oldBg = GUI.backgroundColor;
                if (isEmpty)
                    GUI.backgroundColor = new Color(0.9f, 0.9f, 0.7f);
                else if (!isValid)
                    GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                else
                    GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);

                // Поле с путём
                var displayText = isEmpty ? "(Не выбрано)" : $"Evt.{currentPath}";
                EditorGUILayout.TextField(displayText);
                GUI.backgroundColor = oldBg;

                // Кнопка выбора
                if (GUILayout.Button("▼", GUILayout.Width(25)))
                {
                    var index = i; // Копия для замыкания
                    ShowEventMenu(currentPath, (selected) =>
                    {
                        arrayProp.GetArrayElementAtIndex(index).stringValue = selected;
                        serializedObject.ApplyModifiedProperties();
                    });
                }

                // Кнопка удаления
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    arrayProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            // Кнопка добавления
            EditorGUILayout.Space(3);
            if (GUILayout.Button("+ Добавить событие"))
            {
                arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
                arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1).stringValue = "";
            }

            EditorGUILayout.EndVertical();
            
            EditorGUILayout.LabelField(tooltip, EditorStyles.miniLabel);
        }

        private void ShowEventMenu(string currentPath, System.Action<string> onSelected)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("(Нет события)"), string.IsNullOrEmpty(currentPath), () => onSelected(""));
            menu.AddSeparator("");

            // Используем EventPathDrawer для получения списка событий
            EventPathDrawer.InitializeCache();
            var categories = EventPathDrawer.GetCategories();

            foreach (var category in categories)
            {
                var events = EventPathDrawer.GetEventsInCategory(category);
                foreach (var evt in events)
                {
                    var isSelected = evt.Path == currentPath;
                    var menuPath = $"{category}/{evt.Name}";
                    var evtPath = evt.Path;

                    menu.AddItem(new GUIContent(menuPath), isSelected, () => onSelected(evtPath));
                }
            }

            menu.ShowAsContext();
        }

        private void DrawAttachPointSelector()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Точка привязки", "Точка привязки для данных в переадресуемых событиях"));
            
            var currentPoint = forwardAttachPointProp.stringValue;
            if (string.IsNullOrEmpty(currentPoint)) currentPoint = "default";

            if (GUILayout.Button(currentPoint, EditorStyles.popup))
            {
                var menu = new GenericMenu();
                var points = component.GetAvailableAttachPoints();
                
                foreach (var point in points)
                {
                    var p = point;
                    menu.AddItem(new GUIContent(point), point == currentPoint, () =>
                    {
                        forwardAttachPointProp.stringValue = p;
                        serializedObject.ApplyModifiedProperties();
                    });
                }
                
                menu.ShowAsContext();
            }

            EditorGUILayout.EndHorizontal();
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
