// Packages/com.protosystem.core/Editor/UI/UIStyleConfigurationEditor.cs
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Кастомный редактор для UIStyleConfiguration с удобным отображением всех настроек
    /// </summary>
    [CustomEditor(typeof(UIStyleConfiguration))]
    public class UIStyleConfigurationEditor : UnityEditor.Editor
    {
        private bool showColors = true;
        private bool showSizes = true;
        private bool showBorder = true;
        private bool showElements = true;
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var config = (UIStyleConfiguration)target;

            // ========== Пресет ==========
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stylePreset"), new GUIContent("🎯 Пресет стиля"));
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Modern", GUILayout.Height(22)))
            {
                Undo.RecordObject(config, "Apply Modern Preset");
                config.ApplyPreset(UIStylePreset.Modern);
                EditorUtility.SetDirty(config);
            }
            if (GUILayout.Button("Minimal", GUILayout.Height(22)))
            {
                Undo.RecordObject(config, "Apply Minimal Preset");
                config.ApplyPreset(UIStylePreset.Minimal);
                EditorUtility.SetDirty(config);
            }
            if (GUILayout.Button("Material", GUILayout.Height(22)))
            {
                Undo.RecordObject(config, "Apply Material Preset");
                config.ApplyPreset(UIStylePreset.Material);
                EditorUtility.SetDirty(config);
            }
            if (GUILayout.Button("Classic", GUILayout.Height(22)))
            {
                Undo.RecordObject(config, "Apply Classic Preset");
                config.ApplyPreset(UIStylePreset.Classic);
                EditorUtility.SetDirty(config);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(12);

            // ========== Цвета ==========
            showColors = EditorGUILayout.Foldout(showColors, "🎨 Основные цвета", true, EditorStyles.foldoutHeader);
            if (showColors)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("backgroundColor"), new GUIContent("Фон окна"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("accentColor"), new GUIContent("Акцент (primary)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("textColor"), new GUIContent("Текст"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("secondaryTextColor"), new GUIContent("Текст вторичный"));
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(8);

            // ========== Рамка ==========
            showBorder = EditorGUILayout.Foldout(showBorder, "📐 Рамка и закругления", true, EditorStyles.foldoutHeader);
            if (showBorder)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("borderColor"), new GUIContent("Цвет рамки"));
                
                EditorGUILayout.Space(4);
                
                // borderWidth с float слайдером (кратно 0.25)
                var borderWidthProp = serializedObject.FindProperty("borderWidth");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Толщина рамки", GUILayout.Width(120));
                
                // Слайдер 0-4 с шагом 0.25
                float newValue = EditorGUILayout.Slider(borderWidthProp.floatValue, 0f, 4f);
                // Округляем до 0.25
                newValue = Mathf.Round(newValue * 4f) / 4f;
                borderWidthProp.floatValue = newValue;
                
                EditorGUILayout.LabelField($"{newValue:F2}px", GUILayout.Width(45));
                EditorGUILayout.EndHorizontal();
                
                // Визуальная подсказка
                if (borderWidthProp.floatValue < 0.01f)
                {
                    EditorGUILayout.HelpBox("Рамка отключена (borderWidth = 0)", MessageType.Info);
                }
                else if (borderWidthProp.floatValue <= 0.5f)
                {
                    EditorGUILayout.HelpBox("Тонкая рамка (как в HTML)", MessageType.None);
                }
                
                EditorGUILayout.Space(4);
                
                // Радиусы
                EditorGUILayout.PropertyField(serializedObject.FindProperty("windowBorderRadius"), new GUIContent("Радиус окна"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("buttonBorderRadius"), new GUIContent("Радиус кнопок"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("inputBorderRadius"), new GUIContent("Радиус полей"));
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(8);

            // ========== Размеры ==========
            showSizes = EditorGUILayout.Foldout(showSizes, "📏 Размеры", true, EditorStyles.foldoutHeader);
            if (showSizes)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("elementHeight"), new GUIContent("Высота элементов"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fontSize"), new GUIContent("Размер шрифта"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("headerFontSize"), new GUIContent("Размер заголовка"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("spacing"), new GUIContent("Отступ между"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("padding"), new GUIContent("Внутренний отступ"));
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(8);

            // ========== Элементы ==========
            showElements = EditorGUILayout.Foldout(showElements, "🔧 Элементы управления", true, EditorStyles.foldoutHeader);
            if (showElements)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.LabelField("Фоны элементов", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("elementBackgroundColor"), new GUIContent("Фон"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("elementHoverColor"), new GUIContent("Hover"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("elementActiveColor"), new GUIContent("Active"));
                
                EditorGUILayout.Space(4);
                
                EditorGUILayout.LabelField("Слайдер", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sliderHandleSize"), new GUIContent("Размер ручки"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sliderTrackHeight"), new GUIContent("Высота трека"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sliderHandleColor"), new GUIContent("Цвет ручки"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sliderTrackBackgroundColor"), new GUIContent("Фон трека"));
                
                EditorGUILayout.Space(4);
                
                EditorGUILayout.LabelField("Checkbox", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("toggleStyle"), new GUIContent("Стиль"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("checkboxSize"), new GUIContent("Размер"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("checkboxBorderWidth"), new GUIContent("Толщина обводки"));
                
                EditorGUILayout.Space(4);
                
                EditorGUILayout.LabelField("Иконки", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("iconSize"), new GUIContent("Размер"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("iconStrokeWidth"), new GUIContent("Толщина линий"));
                
                EditorGUILayout.Space(4);
                
                EditorGUILayout.LabelField("Эффекты", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("useGradients"), new GUIContent("Градиенты"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("useShadows"), new GUIContent("Тени"));
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(12);

            // ========== Превью ==========
            EditorGUILayout.LabelField("👁 Превью цветов", EditorStyles.boldLabel);
            
            // Цветовой превью
            Rect previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(60));
            previewRect = EditorGUI.IndentedRect(previewRect);
            
            // Фон
            Rect bgRect = new Rect(previewRect.x, previewRect.y, previewRect.width, previewRect.height);
            EditorGUI.DrawRect(bgRect, config.backgroundColor);
            
            // Рамка (минимум 1px для видимости в превью)
            float previewBorderWidth = Mathf.Max(config.borderWidth, 1f);
            if (config.borderWidth > 0.01f)
            {
                // Верх
                EditorGUI.DrawRect(new Rect(bgRect.x, bgRect.y, bgRect.width, previewBorderWidth), config.borderColor);
                // Низ
                EditorGUI.DrawRect(new Rect(bgRect.x, bgRect.yMax - previewBorderWidth, bgRect.width, previewBorderWidth), config.borderColor);
                // Лево
                EditorGUI.DrawRect(new Rect(bgRect.x, bgRect.y, previewBorderWidth, bgRect.height), config.borderColor);
                // Право
                EditorGUI.DrawRect(new Rect(bgRect.xMax - previewBorderWidth, bgRect.y, previewBorderWidth, bgRect.height), config.borderColor);
            }
            
            // Accent кнопка
            Rect btnRect = new Rect(bgRect.x + 10, bgRect.y + 15, 80, 24);
            EditorGUI.DrawRect(btnRect, config.accentColor);
            
            // Текст на кнопке
            GUI.Label(btnRect, "Accent", new GUIStyle(EditorStyles.label) { 
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = config.textColor }
            });
            
            // Element background
            Rect elemRect = new Rect(bgRect.x + 100, bgRect.y + 15, 80, 24);
            EditorGUI.DrawRect(elemRect, config.elementBackgroundColor);
            
            // Element hover
            Rect hoverRect = new Rect(bgRect.x + 190, bgRect.y + 15, 80, 24);
            EditorGUI.DrawRect(hoverRect, config.elementHoverColor);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
