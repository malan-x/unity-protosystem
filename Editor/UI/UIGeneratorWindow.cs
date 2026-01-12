// Packages/com.protosystem.core/Editor/UI/UIGeneratorWindow.cs
using UnityEngine;
using UnityEditor;
using System.IO;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Окно для управления генерацией UI элементов и спрайтов
    /// </summary>
    public class UIGeneratorWindow : EditorWindow
    {
        private UIStyleConfiguration selectedConfig;
        private UIStylePreset selectedPreset = UIStylePreset.Modern;
        private string outputPath = "Assets/UI/Generated";
        private Vector2 scrollPosition;
        
        private bool generateSprites = true;
        private bool generatePrefabs = true;
        private bool overwriteWithoutPrompt = true; // Перезаписывать без запроса
        
        // Предпросмотр
        private Sprite previewCheckmark;
        private Sprite previewArrowDown;
        private Sprite previewButton;

        [MenuItem("ProtoSystem/UI/Tools/UI Generator", priority = 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<UIGeneratorWindow>("UI Generator");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }

        private void OnEnable()
        {
            // Загружаем или создаём конфигурацию по умолчанию
            if (selectedConfig == null)
            {
                selectedConfig = FindOrCreateDefaultConfig();
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawStyleConfiguration();
            EditorGUILayout.Space(10);
            
            DrawOutputSettings();
            EditorGUILayout.Space(10);
            
            DrawPreview();
            EditorGUILayout.Space(10);
            
            DrawGenerateButtons();
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUILayout.Label("UI Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Генератор UI элементов и спрайтов в стиле HTML.\n" +
                "1. Выберите или создайте конфигурацию стиля\n" +
                "2. Настройте параметры\n" +
                "3. Запустите генерацию спрайтов и/или префабов",
                MessageType.Info
            );
        }

        private void DrawStyleConfiguration()
        {
            GUILayout.Label("Конфигурация стиля", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            selectedConfig = (UIStyleConfiguration)EditorGUILayout.ObjectField(
                "Style Config",
                selectedConfig,
                typeof(UIStyleConfiguration),
                false
            );
            
            if (EditorGUI.EndChangeCheck() && selectedConfig != null)
            {
                selectedPreset = selectedConfig.stylePreset;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Создать новую конфигурацию", GUILayout.Height(25)))
            {
                CreateNewConfiguration();
            }
            EditorGUILayout.EndHorizontal();

            if (selectedConfig != null)
            {
                EditorGUILayout.Space(5);
                
                EditorGUI.BeginChangeCheck();
                selectedPreset = (UIStylePreset)EditorGUILayout.EnumPopup("Preset", selectedPreset);
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedConfig, "Change UI Style Preset");
                    selectedConfig.ApplyPreset(selectedPreset);
                    EditorUtility.SetDirty(selectedConfig);
                }

                DrawConfigPreview();
            }
        }

        private void DrawConfigPreview()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Предпросмотр цветов:", EditorStyles.miniBoldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUI.enabled = false;
            EditorGUILayout.ColorField("Background", selectedConfig.backgroundColor);
            EditorGUILayout.ColorField("Accent", selectedConfig.accentColor);
            EditorGUILayout.ColorField("Text", selectedConfig.textColor);
            EditorGUILayout.ColorField("Border", selectedConfig.borderColor);
            GUI.enabled = true;
            
            EditorGUILayout.EndVertical();
        }

        private void DrawOutputSettings()
        {
            GUILayout.Label("Настройки генерации", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            outputPath = EditorGUILayout.TextField("Путь сохранения", outputPath);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Обзор...", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFolderPanel("Выберите папку для генерации", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    // Конвертируем абсолютный путь в относительный от Assets
                    if (path.StartsWith(Application.dataPath))
                    {
                        outputPath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            generateSprites = EditorGUILayout.ToggleLeft("Генерировать спрайты", generateSprites);
            generatePrefabs = EditorGUILayout.ToggleLeft("Генерировать префабы окон", generatePrefabs);
            overwriteWithoutPrompt = EditorGUILayout.ToggleLeft("Перезаписывать без запроса", overwriteWithoutPrompt);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawPreview()
        {
            GUILayout.Label("Предпросмотр", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (generateSprites && selectedConfig != null)
            {
                EditorGUILayout.LabelField("После генерации здесь будут показаны примеры спрайтов:", EditorStyles.miniLabel);
                
                EditorGUILayout.BeginHorizontal();
                DrawSpritePreview("Checkmark", previewCheckmark);
                DrawSpritePreview("Arrow Down", previewArrowDown);
                DrawSpritePreview("Button", previewButton);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("Выберите конфигурацию для предпросмотра", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawSpritePreview(string label, Sprite sprite)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            
            EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel);
            
            Rect rect = GUILayoutUtility.GetRect(80, 80, GUILayout.Width(80), GUILayout.Height(80));
            
            if (sprite != null)
            {
                GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawGenerateButtons()
        {
            EditorGUILayout.Space(10);
            
            GUI.enabled = selectedConfig != null;
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Генерировать ВСЁ", GUILayout.Height(40)))
            {
                GenerateAll();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Только спрайты", GUILayout.Height(30)))
            {
                GenerateSprites();
            }
            
            if (GUILayout.Button("Только префабы", GUILayout.Height(30)))
            {
                GeneratePrefabs();
            }
            
            EditorGUILayout.EndHorizontal();
            
            GUI.enabled = true;
        }

        private void GenerateAll()
        {
            if (selectedConfig == null)
            {
                EditorUtility.DisplayDialog("Ошибка", "Выберите конфигурацию стиля!", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("UI Generator", "Генерация спрайтов и префабов...", 0f);

            try
            {
                if (generateSprites)
                {
                    EditorUtility.DisplayProgressBar("UI Generator", "Генерация спрайтов...", 0.3f);
                    UIIconGenerator.GenerateAllSprites(selectedConfig, outputPath);
                }

                if (generatePrefabs)
                {
                    EditorUtility.DisplayProgressBar("UI Generator", "Генерация префабов...", 0.7f);
                    UIWindowPrefabGenerator.OverwriteWithoutPrompt = overwriteWithoutPrompt;
                    UIWindowPrefabGenerator.GenerateWithSprites(selectedConfig, outputPath);
                }

                LoadPreviewSprites();
                
                EditorUtility.DisplayProgressBar("UI Generator", "Завершение...", 1f);
                EditorUtility.DisplayDialog("Успех", $"Генерация завершена!\nФайлы сохранены в: {outputPath}", "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Ошибка", $"Ошибка генерации: {ex.Message}", "OK");
                Debug.LogError($"[UIGeneratorWindow] Error: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void GenerateSprites()
        {
            if (selectedConfig == null)
            {
                EditorUtility.DisplayDialog("Ошибка", "Выберите конфигурацию стиля!", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("UI Generator", "Генерация спрайтов...", 0.5f);

            try
            {
                UIIconGenerator.GenerateAllSprites(selectedConfig, outputPath);
                LoadPreviewSprites();
                
                EditorUtility.DisplayDialog("Успех", $"Спрайты сгенерированы!\nПуть: {outputPath}", "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Ошибка", $"Ошибка генерации: {ex.Message}", "OK");
                Debug.LogError($"[UIGeneratorWindow] Error: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void GeneratePrefabs()
        {
            if (selectedConfig == null)
            {
                EditorUtility.DisplayDialog("Ошибка", "Выберите конфигурацию стиля!", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("UI Generator", "Генерация префабов...", 0.5f);

            try
            {
                UIWindowPrefabGenerator.OverwriteWithoutPrompt = overwriteWithoutPrompt;
                UIWindowPrefabGenerator.GenerateWithSprites(selectedConfig, outputPath);
                
                EditorUtility.DisplayDialog("Успех", $"Префабы сгенерированы!\nПуть: {outputPath}", "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Ошибка", $"Ошибка генерации: {ex.Message}", "OK");
                Debug.LogError($"[UIGeneratorWindow] Error: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void LoadPreviewSprites()
        {
            // Загружаем примеры для предпросмотра
            previewCheckmark = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_CHECKBOX, "Checkmark");
            previewArrowDown = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_ICON, "ArrowDown");
            previewButton = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_BUTTON, "ButtonBackground");
            
            Repaint();
        }

        private UIStyleConfiguration FindOrCreateDefaultConfig()
        {
            // Ищем существующую конфигурацию
            string[] guids = AssetDatabase.FindAssets("t:UIStyleConfiguration");
            
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<UIStyleConfiguration>(path);
            }

            // Создаём новую
            return UIStyleConfiguration.CreateDefault();
        }

        private void CreateNewConfiguration()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Создать UI Style Configuration",
                "UIStyleConfig",
                "asset",
                "Выберите место для сохранения конфигурации"
            );

            if (!string.IsNullOrEmpty(path))
            {
                var config = UIStyleConfiguration.CreateDefault();
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                
                selectedConfig = config;
                EditorGUIUtility.PingObject(config);
            }
        }
    }
}
