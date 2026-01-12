// Packages/com.protosystem.core/Editor/UI/UIWindowPrefabGenerator.cs
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
// UIHoverEffect подключается из ProtoSystem.Runtime

namespace ProtoSystem.UI
{
    /// <summary>
    /// Генератор prefab'ов для базовых UI окон
    /// </summary>
    public static class UIWindowPrefabGenerator
    {
        private const string DEFAULT_PREFAB_PATH = "Assets/Prefabs/UI/Windows";
        private const string OUTPUT_PATH_PREF_KEY = "ProtoSystem.UI.PrefabGenerator.OutputPath";
        
        // Текущая конфигурация стиля (может быть null для старого режима)
        private static UIStyleConfiguration currentStyleConfig;
        private static string currentOutputPath;
        
        /// <summary>
        /// Флаг для перезаписи без запроса (устанавливается из UIGeneratorWindow)
        /// </summary>
        public static bool OverwriteWithoutPrompt { get; set; } = false;

        /// <summary>
        /// Генерирует окна с использованием сгенерированных спрайтов
        /// </summary>
        public static void GenerateWithSprites(UIStyleConfiguration config, string outputPath)
        {
            currentStyleConfig = config;
            currentOutputPath = outputPath;
            RememberOutputPath(outputPath);
            
            try
            {
                GenerateAllBaseWindows();
            }
            finally
            {
                currentStyleConfig = null;
                currentOutputPath = null;
            }
        }

        [MenuItem("ProtoSystem/UI/Prefabs/Generate Default UI Prefabs", priority = 100)]
        public static void GenerateAllBaseWindows()
        {
            string prefabPath = ResolveOutputPath();
            EnsureFolder(prefabPath);

            int created = 0;

            if (GenerateMainMenu()) created++;
            if (GenerateGameHUD()) created++;
            if (GeneratePauseMenu()) created++;
            if (GenerateSettings()) created++;
            if (GenerateGameOver()) created++;
            if (GenerateStatistics()) created++;
            if (GenerateCredits()) created++;
            if (GenerateLoading()) created++;

            // Dialog windows (modals) used by DialogBuilder
            if (GenerateConfirmDialog()) created++;
            if (GenerateInputDialog()) created++;
            if (GenerateChoiceDialog()) created++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Пересобираем граф
            UIWindowGraphBuilder.RebuildGraph();

            Debug.Log($"[UIWindowPrefabGenerator] Created {created} prefabs. Graph rebuilt.");
        }
        
        private static string ResolveOutputPath()
        {
            if (!string.IsNullOrEmpty(currentOutputPath))
            {
                return NormalizeAssetPath(currentOutputPath);
            }

            var remembered = EditorPrefs.GetString(OUTPUT_PATH_PREF_KEY, string.Empty);
            if (!string.IsNullOrEmpty(remembered))
            {
                remembered = NormalizeAssetPath(remembered);
                if (remembered.StartsWith("Assets/"))
                {
                    return remembered;
                }
            }

            return DEFAULT_PREFAB_PATH;
        }

        private static void RememberOutputPath(string outputPath)
        {
            outputPath = NormalizeAssetPath(outputPath);
            if (string.IsNullOrEmpty(outputPath))
            {
                return;
            }

            if (!outputPath.StartsWith("Assets/"))
            {
                return;
            }

            EditorPrefs.SetString(OUTPUT_PATH_PREF_KEY, outputPath);
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            path = path.Replace('\\', '/');
            while (path.EndsWith("/"))
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }

        #region Individual Generators

        [MenuItem("ProtoSystem/UI/Prefabs/Windows/Main Menu", priority = 110)]
        public static bool GenerateMainMenu()
        {
            var root = CreateWindowBase("MainMenu", new Vector2(500, 400));
            
            // Title
            var titleGO = CreateText("Title", root.transform, "GAME TITLE", 48);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.75f);
            titleRect.anchorMax = new Vector2(1, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleGO.GetComponent<TMP_Text>();
            titleText.fontStyle = FontStyles.Bold;

            // Buttons container
            var buttonsGO = new GameObject("Buttons");
            buttonsGO.transform.SetParent(root.transform, false);
            var buttonsRect = buttonsGO.AddComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.25f, 0.15f);
            buttonsRect.anchorMax = new Vector2(0.75f, 0.7f);
            buttonsRect.offsetMin = Vector2.zero;
            buttonsRect.offsetMax = Vector2.zero;

            var vlg = buttonsGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 15;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Buttons
            var playBtn = CreateMenuButton("PlayButton", buttonsGO.transform, "Начать игру");
            var settingsBtn = CreateMenuButton("SettingsButton", buttonsGO.transform, "Настройки");
            var creditsBtn = CreateMenuButton("CreditsButton", buttonsGO.transform, "Авторы");
            var quitBtn = CreateMenuButton("QuitButton", buttonsGO.transform, "Выход");

            // Version text
            var versionGO = CreateText("Version", root.transform, "v1.0.0", 14);
            var versionRect = versionGO.GetComponent<RectTransform>();
            versionRect.anchorMin = new Vector2(0, 0);
            versionRect.anchorMax = new Vector2(1, 0.1f);
            versionRect.offsetMin = Vector2.zero;
            versionRect.offsetMax = Vector2.zero;
            var versionText = versionGO.GetComponent<TMP_Text>();
            versionText.color = new Color(1, 1, 1, 0.5f);

            // Add component
            var component = root.AddComponent<MainMenuWindow>();
            SetField(component, "playButton", playBtn.GetComponent<Button>());
            SetField(component, "settingsButton", settingsBtn.GetComponent<Button>());
            SetField(component, "creditsButton", creditsBtn.GetComponent<Button>());
            SetField(component, "quitButton", quitBtn.GetComponent<Button>());
            SetField(component, "titleText", titleText);
            SetField(component, "versionText", versionText);

            return SavePrefab(root, "MainMenuWindow");
        }

        [MenuItem("ProtoSystem/UI/Prefabs/Windows/Game HUD", priority = 111)]
        public static bool GenerateGameHUD()
        {
            var root = CreateWindowBase("GameHUD", new Vector2(1920, 1080), true);
            root.GetComponent<Image>().color = new Color(0, 0, 0, 0); // Прозрачный фон

            // === Top Left: Health & Stamina ===
            var topLeftGO = new GameObject("TopLeft");
            topLeftGO.transform.SetParent(root.transform, false);
            var topLeftRect = topLeftGO.AddComponent<RectTransform>();
            topLeftRect.anchorMin = new Vector2(0, 1);
            topLeftRect.anchorMax = new Vector2(0, 1);
            topLeftRect.pivot = new Vector2(0, 1);
            topLeftRect.anchoredPosition = new Vector2(20, -20);
            topLeftRect.sizeDelta = new Vector2(250, 80);

            var topLeftVLG = topLeftGO.AddComponent<VerticalLayoutGroup>();
            topLeftVLG.spacing = 10;
            topLeftVLG.childControlWidth = true;
            topLeftVLG.childControlHeight = false;

            // Health bar
            var healthBar = CreateBar("HealthBar", topLeftGO.transform, new Color(0.8f, 0.2f, 0.2f), "Health");
            var healthFill = healthBar.transform.Find("Fill").GetComponent<Image>();
            var healthText = healthBar.transform.Find("Text").GetComponent<TMP_Text>();

            // Stamina bar
            var staminaBar = CreateBar("StaminaBar", topLeftGO.transform, new Color(0.2f, 0.6f, 0.8f), "Stamina");
            var staminaFill = staminaBar.transform.Find("Fill").GetComponent<Image>();
            var staminaText = staminaBar.transform.Find("Text").GetComponent<TMP_Text>();

            // === Top Right: Score & Timer ===
            var topRightGO = new GameObject("TopRight");
            topRightGO.transform.SetParent(root.transform, false);
            var topRightRect = topRightGO.AddComponent<RectTransform>();
            topRightRect.anchorMin = new Vector2(1, 1);
            topRightRect.anchorMax = new Vector2(1, 1);
            topRightRect.pivot = new Vector2(1, 1);
            topRightRect.anchoredPosition = new Vector2(-20, -20);
            topRightRect.sizeDelta = new Vector2(200, 80);

            var topRightVLG = topRightGO.AddComponent<VerticalLayoutGroup>();
            topRightVLG.spacing = 5;
            topRightVLG.childAlignment = TextAnchor.UpperRight;
            topRightVLG.childControlWidth = true;
            topRightVLG.childControlHeight = false;

            var scoreGO = CreateText("Score", topRightGO.transform, "Score: 0", 24);
            var scoreText = scoreGO.GetComponent<TMP_Text>();
            scoreText.alignment = TextAlignmentOptions.TopRight;
            scoreGO.AddComponent<LayoutElement>().preferredHeight = 30;

            var timerGO = CreateText("Timer", topRightGO.transform, "00:00", 20);
            var timerText = timerGO.GetComponent<TMP_Text>();
            timerText.alignment = TextAlignmentOptions.TopRight;
            timerGO.AddComponent<LayoutElement>().preferredHeight = 25;

            // === Bottom Center: Objective ===
            var objectiveGO = CreateText("Objective", root.transform, "", 18);
            var objectiveRect = objectiveGO.GetComponent<RectTransform>();
            objectiveRect.anchorMin = new Vector2(0.3f, 0);
            objectiveRect.anchorMax = new Vector2(0.7f, 0.1f);
            objectiveRect.offsetMin = Vector2.zero;
            objectiveRect.offsetMax = Vector2.zero;
            var objectiveText = objectiveGO.GetComponent<TMP_Text>();

            // === Center: Interaction Prompt ===
            var interactionGO = new GameObject("InteractionPrompt");
            interactionGO.transform.SetParent(root.transform, false);
            var interactionRect = interactionGO.AddComponent<RectTransform>();
            interactionRect.anchorMin = new Vector2(0.5f, 0.15f);
            interactionRect.anchorMax = new Vector2(0.5f, 0.15f);
            interactionRect.sizeDelta = new Vector2(300, 50);

            var interactionBg = interactionGO.AddComponent<Image>();
            interactionBg.color = new Color(0, 0, 0, 0.7f);

            var interactionTextGO = CreateText("Text", interactionGO.transform, "[E] Interact", 18);
            var interactionTextRect = interactionTextGO.GetComponent<RectTransform>();
            interactionTextRect.anchorMin = Vector2.zero;
            interactionTextRect.anchorMax = Vector2.one;
            interactionTextRect.offsetMin = Vector2.zero;
            interactionTextRect.offsetMax = Vector2.zero;
            var interactionText = interactionTextGO.GetComponent<TMP_Text>();

            interactionGO.SetActive(false);

            // === Top Right Corner: Pause Button ===
            var pauseBtn = CreateButton("PauseButton", root.transform, "II", new Vector2(50, 50));
            var pauseBtnRect = pauseBtn.GetComponent<RectTransform>();
            pauseBtnRect.anchorMin = new Vector2(1, 1);
            pauseBtnRect.anchorMax = new Vector2(1, 1);
            pauseBtnRect.pivot = new Vector2(1, 1);
            pauseBtnRect.anchoredPosition = new Vector2(-80, -20);

            // Add component
            var component = root.AddComponent<GameHUDWindow>();
            SetField(component, "healthFill", healthFill);
            SetField(component, "healthText", healthText);
            SetField(component, "staminaFill", staminaFill);
            SetField(component, "staminaText", staminaText);
            SetField(component, "scoreText", scoreText);
            SetField(component, "timerText", timerText);
            SetField(component, "objectiveText", objectiveText);
            SetField(component, "interactionPrompt", interactionGO);
            SetField(component, "interactionText", interactionText);
            SetField(component, "pauseButton", pauseBtn.GetComponent<Button>());

            return SavePrefab(root, "GameHUDWindow");
        }

        [MenuItem("ProtoSystem/UI/Prefabs/Windows/Pause Menu", priority = 112)]
        public static bool GeneratePauseMenu()
        {
            var root = CreateWindowBase("PauseMenu", new Vector2(400, 350));

            // Title
            var titleGO = CreateText("Title", root.transform, "ПАУЗА", 36);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.8f);
            titleRect.anchorMax = new Vector2(1, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleGO.GetComponent<TMP_Text>();
            titleText.fontStyle = FontStyles.Bold;

            // Buttons container
            var buttonsGO = new GameObject("Buttons");
            buttonsGO.transform.SetParent(root.transform, false);
            var buttonsRect = buttonsGO.AddComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.2f, 0.1f);
            buttonsRect.anchorMax = new Vector2(0.8f, 0.75f);
            buttonsRect.offsetMin = Vector2.zero;
            buttonsRect.offsetMax = Vector2.zero;

            var vlg = buttonsGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var resumeBtn = CreateMenuButton("ResumeButton", buttonsGO.transform, "Продолжить");
            var settingsBtn = CreateMenuButton("SettingsButton", buttonsGO.transform, "Настройки");
            var mainMenuBtn = CreateMenuButton("MainMenuButton", buttonsGO.transform, "В главное меню");
            var quitBtn = CreateMenuButton("QuitButton", buttonsGO.transform, "Выход");

            var component = root.AddComponent<PauseMenuWindow>();
            SetField(component, "resumeButton", resumeBtn.GetComponent<Button>());
            SetField(component, "settingsButton", settingsBtn.GetComponent<Button>());
            SetField(component, "mainMenuButton", mainMenuBtn.GetComponent<Button>());
            SetField(component, "quitButton", quitBtn.GetComponent<Button>());
            SetField(component, "titleText", titleText);

            return SavePrefab(root, "PauseMenuWindow");
        }

        [MenuItem("ProtoSystem/UI/Prefabs/Dialogs/Confirm Dialog", priority = 120)]
        public static bool GenerateConfirmDialog()
        {
            var root = CreateDialogBase("ConfirmDialog", new Vector2(400, 200));
            var content = root.transform.Find("Content");
            var header = root.transform.Find("Header");

            var titleText = header.Find("Title").GetComponent<TMP_Text>();

            var messageGO = CreateText("Message", content, "Вы уверены?", 18);
            var messageRect = messageGO.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0, 0.4f);
            messageRect.anchorMax = new Vector2(1, 0.8f);
            messageRect.offsetMin = new Vector2(20, 0);
            messageRect.offsetMax = new Vector2(-20, 0);
            var messageText = messageGO.GetComponent<TMP_Text>();

            var buttonsContainer = CreateButtonsContainer(content);
            var yesButtonGO = CreateButton("YesButton", buttonsContainer.transform, "Да", new Vector2(120, 40));
            var noButtonGO = CreateButton("NoButton", buttonsContainer.transform, "Нет", new Vector2(120, 40));

            var yesButton = yesButtonGO.GetComponent<Button>();
            var noButton = noButtonGO.GetComponent<Button>();
            var yesButtonText = yesButtonGO.transform.Find("Text").GetComponent<TMP_Text>();
            var noButtonText = noButtonGO.transform.Find("Text").GetComponent<TMP_Text>();

            var dialogComponent = root.AddComponent<ConfirmDialogWindow>();
            SetField(dialogComponent, "titleText", titleText);
            SetField(dialogComponent, "messageText", messageText);
            SetField(dialogComponent, "yesButton", yesButton);
            SetField(dialogComponent, "noButton", noButton);
            SetField(dialogComponent, "yesButtonText", yesButtonText);
            SetField(dialogComponent, "noButtonText", noButtonText);

            return SavePrefab(root, "ConfirmDialog");
        }

        [MenuItem("ProtoSystem/UI/Prefabs/Dialogs/Input Dialog", priority = 121)]
        public static bool GenerateInputDialog()
        {
            var root = CreateDialogBase("InputDialog", new Vector2(400, 250));
            var content = root.transform.Find("Content");
            var header = root.transform.Find("Header");

            var titleText = header.Find("Title").GetComponent<TMP_Text>();

            var messageGO = CreateText("Message", content, "Введите значение:", 18);
            var messageRect = messageGO.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0, 0.65f);
            messageRect.anchorMax = new Vector2(1, 0.85f);
            messageRect.offsetMin = new Vector2(20, 0);
            messageRect.offsetMax = new Vector2(-20, 0);
            var messageText = messageGO.GetComponent<TMP_Text>();

            var inputGO = CreateTMPInputField("InputField", content);
            var inputRect = inputGO.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0, 0.4f);
            inputRect.anchorMax = new Vector2(1, 0.6f);
            inputRect.offsetMin = new Vector2(20, 0);
            inputRect.offsetMax = new Vector2(-20, 0);
            var inputField = inputGO.GetComponent<TMP_InputField>();

            var errorGO = CreateText("ErrorText", content, "", 14);
            var errorRect = errorGO.GetComponent<RectTransform>();
            errorRect.anchorMin = new Vector2(0, 0.25f);
            errorRect.anchorMax = new Vector2(1, 0.38f);
            errorRect.offsetMin = new Vector2(20, 0);
            errorRect.offsetMax = new Vector2(-20, 0);
            var errorText = errorGO.GetComponent<TMP_Text>();
            errorText.color = new Color(1f, 0.4f, 0.4f);
            errorText.fontSize = 14;

            var buttonsContainer = CreateButtonsContainer(content);
            var submitButtonGO = CreateButton("SubmitButton", buttonsContainer.transform, "OK", new Vector2(120, 40));
            var cancelButtonGO = CreateButton("CancelButton", buttonsContainer.transform, "Отмена", new Vector2(120, 40));

            var submitButton = submitButtonGO.GetComponent<Button>();
            var cancelButton = cancelButtonGO.GetComponent<Button>();
            var submitButtonText = submitButtonGO.transform.Find("Text").GetComponent<TMP_Text>();
            var cancelButtonText = cancelButtonGO.transform.Find("Text").GetComponent<TMP_Text>();

            var dialogComponent = root.AddComponent<InputDialogWindow>();
            SetField(dialogComponent, "titleText", titleText);
            SetField(dialogComponent, "messageText", messageText);
            SetField(dialogComponent, "inputField", inputField);
            SetField(dialogComponent, "errorText", errorText);
            SetField(dialogComponent, "submitButton", submitButton);
            SetField(dialogComponent, "cancelButton", cancelButton);
            SetField(dialogComponent, "submitButtonText", submitButtonText);
            SetField(dialogComponent, "cancelButtonText", cancelButtonText);

            return SavePrefab(root, "InputDialog");
        }

        [MenuItem("ProtoSystem/UI/Prefabs/Dialogs/Choice Dialog", priority = 122)]
        public static bool GenerateChoiceDialog()
        {
            var root = CreateDialogBase("ChoiceDialog", new Vector2(400, 300));
            var content = root.transform.Find("Content");
            var header = root.transform.Find("Header");

            var titleText = header.Find("Title").GetComponent<TMP_Text>();

            var messageGO = CreateText("Message", content, "Выберите вариант:", 18);
            var messageRect = messageGO.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0, 0.8f);
            messageRect.anchorMax = new Vector2(1, 0.95f);
            messageRect.offsetMin = new Vector2(20, 0);
            messageRect.offsetMax = new Vector2(-20, 0);
            var messageText = messageGO.GetComponent<TMP_Text>();

            var choicesGO = new GameObject("ChoicesContainer");
            choicesGO.transform.SetParent(content, false);
            var choicesRect = choicesGO.AddComponent<RectTransform>();
            choicesRect.anchorMin = new Vector2(0, 0.2f);
            choicesRect.anchorMax = new Vector2(1, 0.75f);
            choicesRect.offsetMin = new Vector2(20, 0);
            choicesRect.offsetMax = new Vector2(-20, 0);

            var vlg = choicesGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var choiceTemplate = CreateButton("ChoiceButtonTemplate", choicesGO.transform, "Вариант", new Vector2(0, 40));
            var choiceLE = choiceTemplate.AddComponent<LayoutElement>();
            choiceLE.preferredHeight = 40;
            choiceTemplate.SetActive(false);
            var choiceButton = choiceTemplate.GetComponent<Button>();

            var cancelContainer = CreateButtonsContainer(content);
            var cancelRect = cancelContainer.GetComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0, 0);
            cancelRect.anchorMax = new Vector2(1, 0.18f);

            var cancelButtonGO = CreateButton("CancelButton", cancelContainer.transform, "Отмена", new Vector2(120, 40));
            var cancelButton = cancelButtonGO.GetComponent<Button>();
            var cancelButtonText = cancelButtonGO.transform.Find("Text").GetComponent<TMP_Text>();

            var dialogComponent = root.AddComponent<ChoiceDialogWindow>();
            SetField(dialogComponent, "titleText", titleText);
            SetField(dialogComponent, "messageText", messageText);
            SetField(dialogComponent, "choicesContainer", choicesGO.transform);
            SetField(dialogComponent, "choiceButtonTemplate", choiceButton);
            SetField(dialogComponent, "cancelButton", cancelButton);
            SetField(dialogComponent, "cancelButtonText", cancelButtonText);

            return SavePrefab(root, "ChoiceDialog");
        }

        [MenuItem("ProtoSystem/UI/Prefabs/Windows/Settings", priority = 113)]
        public static bool GenerateSettings()
                {
                    var root = CreateWindowBase("Settings", new Vector2(500, 720), 1f);

                    // НЕПРОЗРАЧНЫЙ фон - rgba(25, 25, 35, 1.0)
                    var rootImg = root.GetComponent<Image>();
                    if (rootImg != null)
                    {
                        // Спрайт белый - задаём цвет через Image.color
                        rootImg.color = new Color(0.098f, 0.098f, 0.137f, 1f); // Полностью непрозрачный!
                        Debug.Log($"[UIWindowPrefabGenerator] Settings window background: color={rootImg.color}, alpha=1.0 (OPAQUE)");
                    }

                    // Title - из HTML: padding 24px 30px 20px, border-bottom, font-size 28px
                    var titleGO = CreateText("Title", root.transform, "НАСТРОЙКИ", 28);
                    var titleRect = titleGO.GetComponent<RectTransform>();
                    titleRect.anchorMin = new Vector2(0, 0.91f);
                    titleRect.anchorMax = new Vector2(1, 0.98f);
                    titleRect.offsetMin = new Vector2(30, 0);
                    titleRect.offsetMax = new Vector2(-30, -20);
                    var titleTmp = titleGO.GetComponent<TMP_Text>();
                    titleTmp.fontStyle = FontStyles.Bold;
                    titleTmp.color = Color.white;
                    titleTmp.characterSpacing = 2f; // letter-spacing из HTML

                    // Border-bottom для заголовка
                    var titleBorderGO = new GameObject("TitleBorder");
                    titleBorderGO.transform.SetParent(root.transform, false);
                    var borderRect = titleBorderGO.AddComponent<RectTransform>();
                    borderRect.anchorMin = new Vector2(0, 0.91f);
                    borderRect.anchorMax = new Vector2(1, 0.91f);
                    borderRect.sizeDelta = new Vector2(0, 1);
                    var borderImg = titleBorderGO.AddComponent<Image>();
                    borderImg.color = new Color(1f, 1f, 1f, 0.1f); // border из HTML

                    // ScrollView - padding 20px 30px из HTML
                    var scrollGO = CreateScrollView("ScrollView", root.transform);
                    var scrollRect = scrollGO.GetComponent<RectTransform>();
                    scrollRect.anchorMin = new Vector2(0, 0.14f);
                    scrollRect.anchorMax = new Vector2(1, 0.90f);
                    scrollRect.offsetMin = new Vector2(30, 0);
                    scrollRect.offsetMax = new Vector2(-30, 0);

                    // Content inside scroll - настройки из HTML
                    var contentGO = scrollGO.transform.Find("Viewport/Content").gameObject;

                    var csf = contentGO.AddComponent<ContentSizeFitter>();
                    csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
                    int spacing = currentStyleConfig != null ? currentStyleConfig.elementSpacing : 6;
                    vlg.spacing = spacing;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = true; // ВАЖНО: контролируем высоту детей через LayoutElement!
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false; // НЕ растягиваем детей
                    vlg.childScaleWidth = false;
                    vlg.childScaleHeight = false;
                    vlg.padding = new RectOffset(0, 0, 10, 10);
                    Debug.Log($"[UIWindowPrefabGenerator] Content VLG: spacing={spacing}, childControlHeight=TRUE");

                    // Audio section
                    CreateSectionLabel("AudioLabel", contentGO.transform, "Звук");
                    var masterSlider = CreateSettingsSlider("MasterVolume", contentGO.transform, "Громкость");
                    var musicSlider = CreateSettingsSlider("MusicVolume", contentGO.transform, "Музыка");
                    var sfxSlider = CreateSettingsSlider("SfxVolume", contentGO.transform, "Эффекты");

                    // Graphics section
                    CreateSectionLabel("GraphicsLabel", contentGO.transform, "Графика");
                    var qualityDropdown = CreateDropdown("QualityDropdown", contentGO.transform, "Качество");
                    var resolutionDropdown = CreateDropdown("ResolutionDropdown", contentGO.transform, "Разрешение");
                    var fullscreenToggle = CreateToggle("FullscreenToggle", contentGO.transform, "Полный экран");
                    var vsyncToggle = CreateToggle("VsyncToggle", contentGO.transform, "V-Sync");

                    // Gameplay section
                    CreateSectionLabel("GameplayLabel", contentGO.transform, "Управление");
                    var sensitivitySlider = CreateSettingsSlider("SensitivitySlider", contentGO.transform, "Чувствительность");
                    var invertYToggle = CreateToggle("InvertYToggle", contentGO.transform, "Инверсия Y");

                    // Buttons - padding: 20px 30px 24px, border-top из HTML
                    var footerBorderGO = new GameObject("FooterBorder");
                    footerBorderGO.transform.SetParent(root.transform, false);
                    var footerBorderRect = footerBorderGO.AddComponent<RectTransform>();
                    footerBorderRect.anchorMin = new Vector2(0, 0.14f);
                    footerBorderRect.anchorMax = new Vector2(1, 0.14f);
                    footerBorderRect.sizeDelta = new Vector2(0, 1);
                    var footerBorderImg = footerBorderGO.AddComponent<Image>();
                    footerBorderImg.color = new Color(1f, 1f, 1f, 0.1f);

                    var buttonsGO = new GameObject("Buttons");
                    buttonsGO.transform.SetParent(root.transform, false);
                    var buttonsRect = buttonsGO.AddComponent<RectTransform>();
                    buttonsRect.anchorMin = new Vector2(0, 0.02f);
                    buttonsRect.anchorMax = new Vector2(1, 0.13f);
                    buttonsRect.offsetMin = new Vector2(30, 20);
                    buttonsRect.offsetMax = new Vector2(-30, -20);

                    var hlg = buttonsGO.AddComponent<HorizontalLayoutGroup>();
                    hlg.spacing = 12;
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;

                    var applyBtn = CreateStyledButton("ApplyButton", buttonsGO.transform, "Применить", new Vector2(120, 40), true);
                    var resetBtn = CreateStyledButton("ResetButton", buttonsGO.transform, "Сброс", new Vector2(100, 40), false);
                    var backBtn = CreateStyledButton("BackButton", buttonsGO.transform, "Назад", new Vector2(100, 40), false);

                    var component = root.AddComponent<SettingsWindow>();
                    // Audio
                    SetField(component, "masterVolumeSlider", masterSlider.GetComponentInChildren<Slider>());
                    SetField(component, "musicVolumeSlider", musicSlider.GetComponentInChildren<Slider>());
                    SetField(component, "sfxVolumeSlider", sfxSlider.GetComponentInChildren<Slider>());
                    SetField(component, "masterVolumeText", masterSlider.transform.Find("ValueText")?.GetComponent<TMP_Text>());
                    SetField(component, "musicVolumeText", musicSlider.transform.Find("ValueText")?.GetComponent<TMP_Text>());
                    SetField(component, "sfxVolumeText", sfxSlider.transform.Find("ValueText")?.GetComponent<TMP_Text>());
                    // Graphics
                    SetField(component, "qualityDropdown", qualityDropdown.GetComponentInChildren<TMP_Dropdown>());
                    SetField(component, "resolutionDropdown", resolutionDropdown.GetComponentInChildren<TMP_Dropdown>());
                    SetField(component, "fullscreenToggle", fullscreenToggle.GetComponentInChildren<Toggle>());
                    SetField(component, "vsyncToggle", vsyncToggle.GetComponentInChildren<Toggle>());
                    // Gameplay
                    SetField(component, "sensitivitySlider", sensitivitySlider.GetComponentInChildren<Slider>());
                    SetField(component, "sensitivityText", sensitivitySlider.transform.Find("ValueText")?.GetComponent<TMP_Text>());
                    SetField(component, "invertYToggle", invertYToggle.GetComponentInChildren<Toggle>());
                    // Buttons
                    SetField(component, "applyButton", applyBtn.GetComponent<Button>());
                    SetField(component, "resetButton", resetBtn.GetComponent<Button>());
                    SetField(component, "backButton", backBtn.GetComponent<Button>());

                    return SavePrefab(root, "Settings");
                }

        [MenuItem("ProtoSystem/UI/Prefabs/Windows/Credits", priority = 114)]
        public static bool GenerateCredits()
        {
            var root = CreateWindowBase("Credits", new Vector2(450, 500));

            // Title
            var titleGO = CreateText("Title", root.transform, "АВТОРЫ", 32);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.9f);
            titleRect.anchorMax = new Vector2(1, 0.98f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            titleGO.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

            // Scroll View
            var scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(root.transform, false);
            var scrollRect = scrollGO.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.05f, 0.12f);
            scrollRect.anchorMax = new Vector2(0.95f, 0.88f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            var scrollView = scrollGO.AddComponent<ScrollRect>();
            scrollGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.3f);
            scrollGO.AddComponent<Mask>().showMaskGraphic = true;

            // Content
            var scrollContentGO = new GameObject("Content");
            scrollContentGO.transform.SetParent(scrollGO.transform, false);
            var scrollContentRect = scrollContentGO.AddComponent<RectTransform>();
            scrollContentRect.anchorMin = new Vector2(0, 1);
            scrollContentRect.anchorMax = new Vector2(1, 1);
            scrollContentRect.pivot = new Vector2(0.5f, 1);
            scrollContentRect.sizeDelta = new Vector2(0, 600);

            var csf = scrollContentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Ищем CreditsData в проекте
            CreditsData creditsData = null;
            var guids = AssetDatabase.FindAssets("t:CreditsData");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                creditsData = AssetDatabase.LoadAssetAtPath<CreditsData>(path);
                Debug.Log($"[UIWindowPrefabGenerator] Found CreditsData: {path}");
            }

            // Текст по умолчанию или из CreditsData
            string defaultText = creditsData != null 
                ? creditsData.GenerateCreditsText()
                : "<size=24><b>Разработка</b></size>\nИмя Разработчика\n\n" +
                "<size=24><b>Дизайн</b></size>\nИмя Дизайнера\n\n" +
                "<size=24><b>Музыка</b></size>\nИмя Композитора\n\n" +
                "<size=24><b>Благодарности</b></size>\nВсем кто помогал!";

            var creditsTextGO = CreateText("CreditsText", scrollContentGO.transform, defaultText, 16);
            var creditsTextRect = creditsTextGO.GetComponent<RectTransform>();
            creditsTextRect.anchorMin = Vector2.zero;
            creditsTextRect.anchorMax = Vector2.one;
            creditsTextRect.offsetMin = new Vector2(20, 20);
            creditsTextRect.offsetMax = new Vector2(-20, -20);
            var creditsText = creditsTextGO.GetComponent<TMP_Text>();
            creditsText.alignment = TextAlignmentOptions.Top;
            creditsText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            var textLE = creditsTextGO.AddComponent<LayoutElement>();
            textLE.preferredWidth = 380;

            scrollView.content = scrollContentRect;
            scrollView.vertical = true;
            scrollView.horizontal = false;

            // Buttons
            var buttonsGO = new GameObject("Buttons");
            buttonsGO.transform.SetParent(root.transform, false);
            var buttonsRect = buttonsGO.AddComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.2f, 0.02f);
            buttonsRect.anchorMax = new Vector2(0.8f, 0.1f);
            buttonsRect.offsetMin = Vector2.zero;
            buttonsRect.offsetMax = Vector2.zero;

            var hlg = buttonsGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;

            var skipBtn = CreateButton("SkipButton", buttonsGO.transform, "Пропустить", new Vector2(110, 35));
            var backBtn = CreateButton("BackButton", buttonsGO.transform, "Назад", new Vector2(90, 35));

            var component = root.AddComponent<CreditsWindow>();
            SetField(component, "creditsData", creditsData); // Привязываем CreditsData
            SetField(component, "creditsText", creditsText);
            SetField(component, "scrollRect", scrollView);
            SetField(component, "contentTransform", scrollContentRect);
            SetField(component, "backButton", backBtn.GetComponent<Button>());
            SetField(component, "skipButton", skipBtn.GetComponent<Button>());

            return SavePrefab(root, "CreditsWindow");
        }

        [MenuItem("ProtoSystem/UI/Prefabs/Windows/Loading", priority = 115)]
        public static bool GenerateLoading()
        {
            var root = CreateWindowBase("Loading", new Vector2(1920, 1080), true);

            // Background
            var bg = root.GetComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 1f);

            // Center container
            var centerGO = new GameObject("Center");
            centerGO.transform.SetParent(root.transform, false);
            var centerRect = centerGO.AddComponent<RectTransform>();
            centerRect.anchorMin = new Vector2(0.3f, 0.35f);
            centerRect.anchorMax = new Vector2(0.7f, 0.65f);
            centerRect.offsetMin = Vector2.zero;
            centerRect.offsetMax = Vector2.zero;

            var vlg = centerGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Status text
            var statusGO = CreateText("Status", centerGO.transform, "Загрузка...", 24);
            var statusText = statusGO.GetComponent<TMP_Text>();
            statusGO.AddComponent<LayoutElement>().preferredHeight = 35;

            // Progress bar
            var progressBar = CreateBar("ProgressBar", centerGO.transform, new Color(0.3f, 0.7f, 1f), "");
            var progressFill = progressBar.transform.Find("Fill").GetComponent<Image>();
            var progressText = progressBar.transform.Find("Text").GetComponent<TMP_Text>();
            progressText.text = "0%";

            // Tips
            var tipsGO = CreateText("Tips", root.transform, "Совет: Нажмите ESC для паузы", 16);
            var tipsRect = tipsGO.GetComponent<RectTransform>();
            tipsRect.anchorMin = new Vector2(0.2f, 0.1f);
            tipsRect.anchorMax = new Vector2(0.8f, 0.2f);
            tipsRect.offsetMin = Vector2.zero;
            tipsRect.offsetMax = Vector2.zero;
            var tipsText = tipsGO.GetComponent<TMP_Text>();
            tipsText.color = new Color(1, 1, 1, 0.6f);
            tipsText.fontStyle = FontStyles.Italic;

            var component = root.AddComponent<LoadingWindow>();
            SetField(component, "progressFill", progressFill);
            SetField(component, "progressText", progressText);
            SetField(component, "statusText", statusText);
            SetField(component, "tipsText", tipsText);

            return SavePrefab(root, "LoadingWindow");
        }

        [MenuItem("ProtoSystem/UI/Prefabs/Windows/GameOver", priority = 116)]
        public static bool GenerateGameOver()
        {
            var root = CreateWindowBase("GameOver", new Vector2(450, 320), 0.92f);

            // Icon placeholder (top center)
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(root.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.7f);
            iconRect.anchorMax = new Vector2(0.5f, 0.7f);
            iconRect.sizeDelta = new Vector2(80, 80);
            var iconImage = iconGO.AddComponent<Image>();
            iconImage.color = new Color(0.2f, 0.8f, 0.3f);

            // Title
            var titleGO = CreateText("Title", root.transform, "ПОБЕДА", 36);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.45f);
            titleRect.anchorMax = new Vector2(1, 0.65f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleGO.GetComponent<TMP_Text>();
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(0.2f, 0.8f, 0.3f);

            // Message
            var messageGO = CreateText("Message", root.transform, "Поздравляем! Вы победили!", 18);
            var messageRect = messageGO.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0.1f, 0.3f);
            messageRect.anchorMax = new Vector2(0.9f, 0.45f);
            messageRect.offsetMin = Vector2.zero;
            messageRect.offsetMax = Vector2.zero;
            var messageText = messageGO.GetComponent<TMP_Text>();
            messageText.color = new Color(0.8f, 0.8f, 0.8f);

            // Buttons
            var buttonsGO = new GameObject("Buttons");
            buttonsGO.transform.SetParent(root.transform, false);
            var buttonsRect = buttonsGO.AddComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.05f, 0.05f);
            buttonsRect.anchorMax = new Vector2(0.95f, 0.25f);
            buttonsRect.offsetMin = Vector2.zero;
            buttonsRect.offsetMax = Vector2.zero;

            var hlg = buttonsGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 15;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            var restartBtn = CreateButton("RestartButton", buttonsGO.transform, "Заново", new Vector2(110, 40));
            var menuBtn = CreateButton("MainMenuButton", buttonsGO.transform, "Меню", new Vector2(100, 40));
            var quitBtn = CreateButton("QuitButton", buttonsGO.transform, "Выход", new Vector2(100, 40));

            var component = root.AddComponent<GameOverWindow>();
            SetField(component, "titleText", titleText);
            SetField(component, "messageText", messageText);
            SetField(component, "iconImage", iconImage);
            SetField(component, "restartButton", restartBtn.GetComponent<Button>());
            SetField(component, "mainMenuButton", menuBtn.GetComponent<Button>());
            SetField(component, "quitButton", quitBtn.GetComponent<Button>());

            return SavePrefab(root, "GameOver");
        }

        [MenuItem("ProtoSystem/UI/Prefabs/Windows/Statistics", priority = 117)]
        public static bool GenerateStatistics()
        {
            var root = CreateWindowBase("Statistics", new Vector2(420, 450), 0.92f);

            // Title
            var titleGO = CreateText("Title", root.transform, "СТАТИСТИКА", 28);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.88f);
            titleRect.anchorMax = new Vector2(1, 0.97f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleGO.GetComponent<TMP_Text>();
            titleText.fontStyle = FontStyles.Bold;

            // Stats container with scroll
            var scrollGO = CreateScrollView("StatsScroll", root.transform);
            var scrollRect = scrollGO.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.05f, 0.15f);
            scrollRect.anchorMax = new Vector2(0.95f, 0.85f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            var contentGO = scrollGO.transform.Find("Viewport/Content").gameObject;
            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.padding = new RectOffset(10, 10, 10, 10);

            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Sample stat rows
            CreateStatRow("Время игры", "12:34", contentGO.transform);
            CreateStatRow("Убито врагов", "25", contentGO.transform);
            CreateStatRow("Урон нанесён", "15,420", contentGO.transform);
            CreateStatRow("Урон получен", "3,200", contentGO.transform);
            CreateStatRow("Точность", "78%", contentGO.transform);

            // Buttons
            var buttonsGO = new GameObject("Buttons");
            buttonsGO.transform.SetParent(root.transform, false);
            var buttonsRect = buttonsGO.AddComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.1f, 0.03f);
            buttonsRect.anchorMax = new Vector2(0.9f, 0.12f);
            buttonsRect.offsetMin = Vector2.zero;
            buttonsRect.offsetMax = Vector2.zero;

            var hlg2 = buttonsGO.AddComponent<HorizontalLayoutGroup>();
            hlg2.spacing = 20;
            hlg2.childAlignment = TextAnchor.MiddleCenter;
            hlg2.childControlWidth = false;
            hlg2.childControlHeight = false;

            var continueBtn = CreateButton("ContinueButton", buttonsGO.transform, "Продолжить", new Vector2(130, 38));
            var backBtn = CreateButton("BackButton", buttonsGO.transform, "Назад", new Vector2(100, 38));

            var component = root.AddComponent<StatisticsWindow>();
            SetField(component, "titleText", titleText);
            SetField(component, "statsContainer", contentGO.transform);
            SetField(component, "continueButton", continueBtn.GetComponent<Button>());
            SetField(component, "backButton", backBtn.GetComponent<Button>());

            return SavePrefab(root, "Statistics");
        }

        private static void CreateStatRow(string label, string value, Transform parent)
        {
            var row = new GameObject("StatRow_" + label.Replace(" ", ""));
            row.transform.SetParent(parent, false);
            row.AddComponent<RectTransform>();
            row.AddComponent<LayoutElement>().preferredHeight = 28;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.spacing = 10;

            var labelGO = CreateText("Label", row.transform, label, 15);
            labelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            labelGO.AddComponent<LayoutElement>().flexibleWidth = 1;

            var valueGO = CreateText("Value", row.transform, value, 15);
            valueGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineRight;
            valueGO.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            valueGO.AddComponent<LayoutElement>().preferredWidth = 80;
        }

        #endregion

        #region Helper Methods

        private static GameObject CreateWindowBase(string name, Vector2 size, bool withBackground = true)
        {
            return CreateWindowBase(name, size, 0.95f, withBackground);
        }

        private static GameObject CreateWindowBase(string name, Vector2 size, float bgAlpha, bool withBackground = true)
        {
            var root = new GameObject(name);
            var rect = root.AddComponent<RectTransform>();
            rect.sizeDelta = size;

            root.AddComponent<CanvasGroup>();

            if (withBackground)
            {
                var bg = root.AddComponent<Image>();
                
                // Пытаемся загрузить спрайт фона окна с округлыми углами
                Sprite windowBgSprite = null;
                if (currentStyleConfig != null)
                {
                    windowBgSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_BACKGROUND, "WindowBackground");
                }
                
                if (windowBgSprite != null)
                {
                    bg.sprite = windowBgSprite;
                    bg.type = Image.Type.Sliced;
                    // Для шейдера UI/TwoColor используем белый цвет в Image.color
                    // Реальные цвета задаются через UITwoColorImage
                    bg.color = Color.white;
                    Debug.Log($"[UIWindowPrefabGenerator] CreateWindowBase: {name} - Image.color SET TO WHITE: {bg.color}");
                    
                    // Применяем двухцветный эффект
                    Color fillColor = currentStyleConfig.backgroundColor;
                    fillColor.a = bgAlpha;
                    ApplyTwoColorEffect(root, fillColor, currentStyleConfig.borderColor);
                    
                    // ПРОВЕРКА: убеждаемся что color всё ещё белый после ApplyTwoColorEffect
                    Debug.Log($"[UIWindowPrefabGenerator] CreateWindowBase: {name} - Image.color AFTER effect: {bg.color}");
                    Debug.Log($"[UIWindowPrefabGenerator] CreateWindowBase: {name}, fill={fillColor}, border={currentStyleConfig.borderColor}");
                }
                else
                {
                    bg.color = new Color(0.15f, 0.15f, 0.15f, bgAlpha);
                }
            }

            return root;
        }

        private static GameObject CreateText(string name, Transform parent, string text, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return go;
        }

        private static GameObject CreateScrollView(string name, Transform parent)
        {
            // ScrollView container
            var scrollGO = new GameObject(name);
            scrollGO.transform.SetParent(parent, false);
            var scrollRectTransform = scrollGO.AddComponent<RectTransform>();

            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.vertical = true;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRect = viewportGO.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.pivot = new Vector2(0, 1);

            var viewportImage = viewportGO.AddComponent<Image>();
            viewportImage.color = new Color(0, 0, 0, 0.01f); // Почти прозрачный для Mask
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 300); // Начальная высота, CSF переопределит

            // Scrollbar - очень тонкий как в HTML
            var scrollbarGO = new GameObject("Scrollbar");
            scrollbarGO.transform.SetParent(scrollGO.transform, false);
            var scrollbarRect = scrollbarGO.AddComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = new Vector2(1, 1);
            scrollbarRect.pivot = new Vector2(1, 0.5f);
            scrollbarRect.sizeDelta = new Vector2(4, 0); // Очень тонкий scrollbar (4px)
            scrollbarRect.anchoredPosition = new Vector2(2, 0); // Отступ от правого края

            var scrollbarImage = scrollbarGO.AddComponent<Image>();
            scrollbarImage.color = new Color(0.1f, 0.1f, 0.1f, 0.2f); // Почти невидимый фон
            var scrollbar = scrollbarGO.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            // Scrollbar Handle - тонкий
            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(scrollbarGO.transform, false);
            var handleRect = handleGO.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(4, 20); // Тонкий handle

            var handleImage = handleGO.AddComponent<Image>();
            handleImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Полупрозрачный
            
            Debug.Log("[UIWindowPrefabGenerator] Scrollbar: width=4px, ultra-thin style");

            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;

            // Setup ScrollRect references
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            return scrollGO;
        }

        /// <summary>
                /// Создаёт заголовок секции с линией подчёркивания
                /// </summary>
                private static GameObject CreateSectionLabel(string name, Transform parent, string text)
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    var rect = go.AddComponent<RectTransform>();

                    // LayoutElement с ФИКСИРОВАННОЙ высотой - не растягивается
                    var le = go.AddComponent<LayoutElement>();
                    le.preferredHeight = 28; // Уменьшено с 36
                    le.minHeight = 28;
                    le.flexibleHeight = 0;
                    le.flexibleWidth = 1; // Растягивается по ширине

                    Debug.Log($"[UIWindowPrefabGenerator] CreateSectionLabel: {text}, height=28px FIXED");

                    // Текст секции - в верхней части контейнера
                    var textGO = new GameObject("Text");
                    textGO.transform.SetParent(go.transform, false);
                    var textRect = textGO.AddComponent<RectTransform>();
                    textRect.anchorMin = new Vector2(0, 0.25f);
                    textRect.anchorMax = new Vector2(1, 1);
                    textRect.offsetMin = Vector2.zero;
                    textRect.offsetMax = Vector2.zero;

                    var tmp = textGO.AddComponent<TextMeshProUGUI>();
                    tmp.text = text.ToUpper();
                    tmp.fontSize = 13;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color = new Color(0.545f, 0.616f, 0.765f, 1f); // #8B9DC3
                    tmp.alignment = TextAlignmentOptions.BottomLeft;
                    tmp.characterSpacing = 1.5f;

                    // Линия подчёркивания - АБСОЛЮТНО ФИКСИРОВАННАЯ высота 1px
                    var lineGO = new GameObject("Line");
                    lineGO.transform.SetParent(go.transform, false);
                    var lineRect = lineGO.AddComponent<RectTransform>();
                    // Привязка к низу родителя
                    lineRect.anchorMin = new Vector2(0, 0);
                    lineRect.anchorMax = new Vector2(1, 0);
                    lineRect.pivot = new Vector2(0.5f, 0);
                    lineRect.sizeDelta = new Vector2(0, 1); // СТРОГО 1 пиксель высоты
                    lineRect.anchoredPosition = new Vector2(0, 2);

                    // Добавляем LayoutElement к линии чтобы она НЕ растягивалась
                    var lineLE = lineGO.AddComponent<LayoutElement>();
                    lineLE.ignoreLayout = true; // ИГНОРИРУЕМ Layout!

                    var lineImg = lineGO.AddComponent<Image>();
                    lineImg.color = new Color(0.545f, 0.616f, 0.765f, 0.2f); // rgba(139, 157, 195, 0.2)

                    Debug.Log($"[UIWindowPrefabGenerator] Section '{text}' line: height=1px, ignoreLayout=true");

                    return go;
                }

        private static GameObject CreateButton(string name, Transform parent, string text, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();

            // Пытаемся загрузить спрайт кнопки
            Sprite buttonSprite = null;
            if (currentStyleConfig != null)
            {
                buttonSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_BUTTON, "ButtonBackground");
            }

            Color fillColor = new Color(0.22f, 0.22f, 0.24f, 1f);
            Color borderColor = new Color(0.4f, 0.4f, 0.5f, 1f);

            if (buttonSprite != null)
            {
                img.sprite = buttonSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                ApplyTwoColorEffect(go, fillColor, borderColor);
            }
            else
            {
                img.color = fillColor;
            }

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            // UIHoverEffect
            var hoverEffect = go.AddComponent<UIHoverEffect>();
            hoverEffect.hoverBrightness = 1.3f;
            hoverEffect.pressedBrightness = 0.85f;
            hoverEffect.transitionDuration = 0.15f;

            // Текст кнопки
            var textGO = CreateText("Text", go.transform, text, 18);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return go;
        }

        /// <summary>
        /// Стилизованная кнопка - primary (синяя) или secondary (тёмная)
        /// </summary>
                private static GameObject CreateStyledButton(string name, Transform parent, string text, Vector2 size, bool isPrimary)
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    var rect = go.AddComponent<RectTransform>();
                    rect.sizeDelta = size;

                    var img = go.AddComponent<Image>();

                    // Пытаемся загрузить спрайт кнопки
                    Sprite buttonSprite = null;
                    if (currentStyleConfig != null)
                    {
                        string spriteName = isPrimary ? "ButtonBackground" : "ButtonBackgroundSecondary";
                        buttonSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_BUTTON, spriteName);
                        Debug.Log($"[UIWindowPrefabGenerator] CreateStyledButton: {name}, isPrimary={isPrimary}, sprite={buttonSprite != null}");
                    }

                    // Устанавливаем базовый цвет
                    Color fillColor;
                    Color borderColor;
                    if (isPrimary)
                    {
                        fillColor = new Color(0.29f, 0.62f, 1f, 1f); // Синий #4A9EFF
                        borderColor = new Color(0.4f, 0.7f, 1f, 1f); // Светло-синий для рамки
                    }
                    else
                    {
                        fillColor = new Color(1f, 1f, 1f, 0.1f); // Полупрозрачный тёмный
                        borderColor = new Color(1f, 1f, 1f, 0.3f); // Светлая рамка
                    }

                    if (buttonSprite != null)
                    {
                        img.sprite = buttonSprite;
                        img.type = Image.Type.Sliced;
                        img.color = Color.white; // Белый для шейдера
                        
                        // Применяем двухцветный эффект
                        ApplyTwoColorEffect(go, fillColor, borderColor);
                    }
                    else
                    {
                        img.color = fillColor;
                    }

                    // Button компонент - БЕЗ ColorTint, hover управляется через UIHoverEffect
                    var btn = go.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None; // Отключаем встроенный transition
                    btn.targetGraphic = img;

                    Debug.Log($"[UIWindowPrefabGenerator] Button {name} transition set to None");

                    // UIHoverEffect - он будет управлять цветом
                    var hoverEffect = go.AddComponent<UIHoverEffect>();
                    hoverEffect.hoverBrightness = isPrimary ? 1.12f : 1.5f; // Secondary нужен более заметный hover из-за низкой альфы
                    hoverEffect.pressedBrightness = 0.85f;
                    hoverEffect.transitionDuration = 0.15f;

                    // Для secondary кнопок (полупрозрачных) увеличиваем альфу при hover
                    if (!isPrimary)
                    {
                        hoverEffect.boostAlphaOnHover = true;
                        hoverEffect.hoverAlpha = 0.2f;
                    }

                    var textGO = CreateText("Text", go.transform, text, 14);
                    var textRect = textGO.GetComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = Vector2.zero;
                    textRect.offsetMax = Vector2.zero;
                    textGO.GetComponent<TMP_Text>().color = Color.white;

                    return go;
                }

        /// <summary>
        /// Применяет двухцветный эффект к Image компоненту
        /// Позволяет задать отдельно цвет заливки и рамки через шейдер UI/TwoColor
        /// ВАЖНО: Image.color должен быть белым для корректной работы шейдера!
        /// </summary>
        private static void ApplyTwoColorEffect(GameObject go, Color fillColor, Color borderColor)
        {
            // ГАРАНТИРУЕМ что Image.color белый
            var image = go.GetComponent<Image>();
            if (image != null)
            {
                image.color = Color.white;
                Debug.Log($"[UIWindowPrefabGenerator] ApplyTwoColorEffect: FORCED Image.color to WHITE on {go.name}");
            }
            
            var twoColorImage = go.AddComponent<UITwoColorImage>();
            twoColorImage.FillColor = fillColor;
            twoColorImage.BorderColor = borderColor;
            Debug.Log($"[UIWindowPrefabGenerator] ApplyTwoColorEffect on {go.name}: fill={fillColor}, border={borderColor}");
        }

        private static GameObject CreateMenuButton(string name, Transform parent, string text)
        {
            var btn = CreateButton(name, parent, text, new Vector2(0, 50));
            btn.AddComponent<LayoutElement>().preferredHeight = 50;
            return btn;
        }

        /// <summary>
        /// Создаёт круглую кнопку закрытия (X) для правого верхнего угла окна
        /// </summary>
        private static GameObject CreateCloseButton(string name, Transform parent, int size = 32)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            
            // Позиционируем в правом верхнем углу
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(-10, -10);

            var img = go.AddComponent<Image>();
            
            // Пытаемся загрузить спрайт кнопки закрытия
            Sprite closeButtonSprite = null;
            if (currentStyleConfig != null)
            {
                closeButtonSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_BUTTON, "CloseButton");
            }

            Color fillColor = new Color(1f, 1f, 1f, 0.1f);
            Color borderColor = new Color(1f, 1f, 1f, 0.3f);

            if (closeButtonSprite != null)
            {
                img.sprite = closeButtonSprite;
                img.color = Color.white;
                ApplyTwoColorEffect(go, fillColor, borderColor);
            }
            else
            {
                // Fallback - простой полупрозрачный круг
                img.color = fillColor;
            }

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            // UIHoverEffect
            var hoverEffect = go.AddComponent<UIHoverEffect>();
            hoverEffect.hoverBrightness = 1.5f;
            hoverEffect.pressedBrightness = 0.8f;
            hoverEffect.transitionDuration = 0.12f;
            hoverEffect.boostAlphaOnHover = true;
            hoverEffect.hoverAlpha = 0.3f;

            Debug.Log($"[UIWindowPrefabGenerator] CloseButton created: size={size}");

            return go;
        }

        private static GameObject CreateBar(string name, Transform parent, Color fillColor, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 30);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 30;

            // Background
            var bg = go.AddComponent<Image>();

            Sprite inputBgSprite = null;
            if (currentStyleConfig != null)
            {
                inputBgSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_BACKGROUND, "InputBackground");
            }

            if (inputBgSprite != null)
            {
                bg.sprite = inputBgSprite;
                bg.type = Image.Type.Sliced;
                bg.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            }
            else
            {
                bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            }

            // Fill
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(go.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.7f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var fillImg = fillGO.AddComponent<Image>();

            if (inputBgSprite != null)
            {
                fillImg.sprite = inputBgSprite;
                fillImg.type = Image.Type.Sliced;
            }
            fillImg.color = fillColor;

            // Text
            var textGO = CreateText("Text", go.transform, label, 14);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(5, 0);
            textRect.offsetMax = new Vector2(-5, 0);
            textGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;

            return go;
        }

        private static GameObject CreateSlider(string name, Transform parent, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<LayoutElement>().preferredHeight = 35;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            // Label
            var labelGO = CreateText("Label", go.transform, label, 16);
            labelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            labelGO.AddComponent<LayoutElement>().preferredWidth = 100;

            // Slider
            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(go.transform, false);
            sliderGO.AddComponent<RectTransform>();
            sliderGO.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Slider background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(sliderGO.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.4f);
            bgRect.anchorMax = new Vector2(1, 0.6f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Fill area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.4f);
            fillAreaRect.anchorMax = new Vector2(1, 0.6f);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillGO.AddComponent<Image>().color = new Color(0.3f, 0.6f, 1f, 1f);

            // Handle
            var handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(sliderGO.transform, false);
            var handleAreaRect = handleAreaGO.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = Vector2.zero;
            handleAreaRect.offsetMax = Vector2.zero;

            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleRect = handleGO.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 20);
            handleGO.AddComponent<Image>().color = Color.white;

            var slider = sliderGO.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 1;

            return go;
        }

        /// <summary>
        /// Слайдер для настроек - синий стиль
        /// </summary>
        /// <summary>
                /// Слайдер для настроек - синий стиль
                /// </summary>
                /// <summary>
                        /// Слайдер для настроек - синий стиль
                        /// Спрайты БЕЛЫЕ, цвет задаётся через Image.color
                        /// </summary>
                        private static GameObject CreateSettingsSlider(string name, Transform parent, string label)
                        {
                            var go = new GameObject(name);
                            go.transform.SetParent(parent, false);
                            go.AddComponent<RectTransform>();
                            var le = go.AddComponent<LayoutElement>();
                            le.preferredHeight = 24; // Компактная высота
                            le.minHeight = 24;
                            le.flexibleHeight = 0;

                            var hlg = go.AddComponent<HorizontalLayoutGroup>();
                            hlg.spacing = 12;
                            hlg.childControlWidth = true;
                            hlg.childControlHeight = true;
                            hlg.childForceExpandWidth = false;
                            hlg.childForceExpandHeight = false;
                            hlg.childAlignment = TextAnchor.MiddleLeft;

                            // Label
                            var labelGO = CreateText("Label", go.transform, label, 14);
                            labelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
                            labelGO.GetComponent<TMP_Text>().color = new Color(0.88f, 0.88f, 0.88f, 1f);
                            var labelLE = labelGO.AddComponent<LayoutElement>();
                            labelLE.preferredWidth = 130;
                            labelLE.preferredHeight = 24;

                            // Slider container - НЕ использует childControlHeight для предотвращения растяжения
                            var sliderGO = new GameObject("Slider");
                            sliderGO.transform.SetParent(go.transform, false);
                            var sliderRect = sliderGO.AddComponent<RectTransform>();
                            var sliderLE = sliderGO.AddComponent<LayoutElement>();
                            sliderLE.flexibleWidth = 1;
                            sliderLE.minWidth = 150;
                            sliderLE.preferredHeight = 24; // Фиксированная высота

                            // Пытаемся загрузить спрайты
                            Sprite trackSprite = null;
                            Sprite fillSprite = null;
                            Sprite handleSprite = null;

                            if (currentStyleConfig != null)
                            {
                                trackSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_SLIDER, "SliderTrack");
                                fillSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_SLIDER, "SliderFill");
                                handleSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_SLIDER, "SliderHandle");
                            }

                            int handleSize = currentStyleConfig != null ? currentStyleConfig.sliderHandleSize : 18;
                            Debug.Log($"[UIWindowPrefabGenerator] CreateSettingsSlider: {name}, handleSize={handleSize}");

                            // Background track - занимает центральную часть по вертикали
                            var bgGO = new GameObject("Background");
                            bgGO.transform.SetParent(sliderGO.transform, false);
                            var bgRect = bgGO.AddComponent<RectTransform>();
                            int trackHeight = currentStyleConfig != null ? currentStyleConfig.sliderTrackHeight : 6;
                            
                            bgRect.anchorMin = new Vector2(0, 0.5f);
                            bgRect.anchorMax = new Vector2(1, 0.5f);
                            bgRect.pivot = new Vector2(0.5f, 0.5f);
                            bgRect.sizeDelta = new Vector2(0, trackHeight);
                            bgRect.anchoredPosition = Vector2.zero;
                            var bgImage = bgGO.AddComponent<Image>();
                            Debug.Log($"[UIWindowPrefabGenerator] Slider track height: {trackHeight}px");

                            if (trackSprite != null)
                            {
                                bgImage.sprite = trackSprite;
                                bgImage.type = Image.Type.Sliced;
                                // Спрайт белый - задаём цвет трека через Image.color
                                bgImage.color = new Color(1f, 1f, 1f, 0.1f); // rgba(255,255,255,0.1)
                            }
                            else
                            {
                                bgImage.color = new Color(1f, 1f, 1f, 0.1f);
                            }

                            // Fill area
                            var fillAreaGO = new GameObject("Fill Area");
                            fillAreaGO.transform.SetParent(sliderGO.transform, false);
                            var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
                            fillAreaRect.anchorMin = new Vector2(0, 0.5f);
                            fillAreaRect.anchorMax = new Vector2(1, 0.5f);
                            fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
                            fillAreaRect.sizeDelta = new Vector2(-handleSize, trackHeight);
                            fillAreaRect.anchoredPosition = new Vector2(-handleSize / 2f, 0);

                            var fillGO = new GameObject("Fill");
                            fillGO.transform.SetParent(fillAreaGO.transform, false);
                            var fillRect = fillGO.AddComponent<RectTransform>();
                            fillRect.anchorMin = Vector2.zero;
                            fillRect.anchorMax = Vector2.one;
                            fillRect.offsetMin = Vector2.zero;
                            fillRect.offsetMax = Vector2.zero;
                            var fillImage = fillGO.AddComponent<Image>();

                            if (fillSprite != null)
                            {
                                fillImage.sprite = fillSprite;
                                fillImage.type = Image.Type.Sliced;
                                // Спрайт белый - задаём синий цвет через Image.color
                                fillImage.color = new Color(0.29f, 0.62f, 1f, 1f); // #4A9EFF
                            }
                            else
                            {
                                fillImage.color = new Color(0.29f, 0.62f, 1f, 1f);
                            }

                            // Handle area - КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: не растягиваем по вертикали
                            var handleAreaGO = new GameObject("Handle Slide Area");
                            handleAreaGO.transform.SetParent(sliderGO.transform, false);
                            var handleAreaRect = handleAreaGO.AddComponent<RectTransform>();
                            handleAreaRect.anchorMin = new Vector2(0, 0);
                            handleAreaRect.anchorMax = new Vector2(1, 1);
                            handleAreaRect.offsetMin = new Vector2(handleSize / 2f, 0);
                            handleAreaRect.offsetMax = new Vector2(-handleSize / 2f, 0);

                            var handleGO = new GameObject("Handle");
                            handleGO.transform.SetParent(handleAreaGO.transform, false);
                            var handleRect = handleGO.AddComponent<RectTransform>();

                            // КРИТИЧНО: Handle должен быть квадратным и не растягиваться!
                            handleRect.anchorMin = new Vector2(0, 0.5f);
                            handleRect.anchorMax = new Vector2(0, 0.5f);
                            handleRect.pivot = new Vector2(0.5f, 0.5f);
                            handleRect.sizeDelta = new Vector2(handleSize, handleSize); // КВАДРАТ!
                            handleRect.anchoredPosition = Vector2.zero;

                            Debug.Log($"[UIWindowPrefabGenerator] Handle RectTransform: sizeDelta={handleRect.sizeDelta}, anchor=({handleRect.anchorMin}, {handleRect.anchorMax})");

                            var handleImage = handleGO.AddComponent<Image>();
                            handleImage.preserveAspect = true; // Сохраняем пропорции

                            if (handleSprite != null)
                            {
                                handleImage.sprite = handleSprite;
                                // Спрайт белый - handle белый
                                handleImage.color = Color.white;
                            }
                            else
                            {
                                handleImage.color = Color.white;
                            }

                            var slider = sliderGO.AddComponent<Slider>();
                            slider.fillRect = fillRect;
                            slider.handleRect = handleRect;
                            slider.targetGraphic = handleImage;
                            slider.minValue = 0;
                            slider.maxValue = 1;
                            slider.value = 0.8f;
                            slider.direction = Slider.Direction.LeftToRight;

                            // UIHoverEffect для ручки слайдера
                            var handleHover = handleGO.AddComponent<UIHoverEffect>();
                            handleHover.hoverBrightness = 1.2f;
                            handleHover.pressedBrightness = 0.85f;
                            handleHover.transitionDuration = 0.12f;
                            handleHover.scaleOnHover = true;
                            handleHover.hoverScale = 1.15f;

                            // Value text
                            var valueText = CreateText("ValueText", go.transform, "80%", 14);
                            valueText.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineRight;
                            valueText.GetComponent<TMP_Text>().color = Color.white;
                            var valueLE = valueText.AddComponent<LayoutElement>();
                            valueLE.preferredWidth = 50;
                            valueLE.preferredHeight = 30;

                            return go;
                        }

        /// <summary>
                /// Dropdown для настроек с закруглёнными углами и стрелкой
                /// Спрайты БЕЛЫЕ - цвет через Image.color
                /// </summary>
                private static GameObject CreateDropdown(string name, Transform parent, string label)
                {
                    // Получаем параметры из конфига
                    int rowHeight = currentStyleConfig != null ? currentStyleConfig.dropdownRowHeight : 32;
                    
                    var go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    go.AddComponent<RectTransform>();
                    var le = go.AddComponent<LayoutElement>();
                    le.preferredHeight = rowHeight;
                    le.minHeight = rowHeight;
                    le.flexibleHeight = 0;

                    var hlg = go.AddComponent<HorizontalLayoutGroup>();
                    hlg.spacing = 12;
                    hlg.childControlWidth = true;
                    hlg.childControlHeight = true; // Контролируем высоту через LayoutElement
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false; // НЕ растягиваем
                    hlg.childAlignment = TextAnchor.MiddleLeft;
                    Debug.Log($"[UIWindowPrefabGenerator] Dropdown: rowHeight={rowHeight}");

                    // Label
                    var labelGO = CreateText("Label", go.transform, label, 14);
                    labelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
                    labelGO.GetComponent<TMP_Text>().color = new Color(0.88f, 0.88f, 0.88f, 1f);
                    var labelLE = labelGO.AddComponent<LayoutElement>();
                    labelLE.preferredWidth = 130;
                    labelLE.preferredHeight = rowHeight;

                    var dropdownGO = new GameObject("Dropdown");
                    dropdownGO.transform.SetParent(go.transform, false);
                    var dropdownRect = dropdownGO.AddComponent<RectTransform>();
                    var dropdownLE = dropdownGO.AddComponent<LayoutElement>();
                    dropdownLE.flexibleWidth = 1;
                    dropdownLE.minWidth = 180;
                    dropdownLE.preferredHeight = rowHeight;
                    dropdownLE.minHeight = rowHeight;

                    var dropdownImg = dropdownGO.AddComponent<Image>();

                    // Загружаем спрайты - DropdownBackground для dropdown
                    Sprite dropdownBgSprite = null;
                    Sprite arrowSprite = null;
                    if (currentStyleConfig != null)
                    {
                        // Сначала пробуем специальный спрайт для dropdown
                        dropdownBgSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_DROPDOWN, "DropdownBackground");
                        // Если нет - fallback на InputBackground
                        if (dropdownBgSprite == null)
                        {
                            dropdownBgSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_BACKGROUND, "InputBackground");
                        }
                        arrowSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_ICON, "ArrowDown");
                    }

                    Debug.Log($"[UIWindowPrefabGenerator] CreateDropdown: {name}");
                    Debug.Log($"[UIWindowPrefabGenerator]   dropdownBgSprite={dropdownBgSprite != null}, arrowSprite={arrowSprite != null}");

                    if (dropdownBgSprite != null)
                    {
                        dropdownImg.sprite = dropdownBgSprite;
                        dropdownImg.type = Image.Type.Sliced;
                        // Белый для шейдера UI/TwoColor
                        dropdownImg.color = Color.white;
                        
                        // Применяем двухцветный эффект с видимой рамкой
                        Color fillColor = new Color(0.2f, 0.2f, 0.25f, 1f);
                        Color borderColor = new Color(0.4f, 0.4f, 0.5f, 1f); // Светло-серая рамка
                        ApplyTwoColorEffect(dropdownGO, fillColor, borderColor);
                    }
                    else
                    {
                        // Fallback без спрайта
                        dropdownImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
                    }
                    Debug.Log($"[UIWindowPrefabGenerator] Dropdown background with two-color effect");

                    // UIHoverEffect для dropdown
                    var dropdownHover = dropdownGO.AddComponent<UIHoverEffect>();
                    dropdownHover.hoverBrightness = 1.3f;
                    dropdownHover.pressedBrightness = 0.9f;
                    dropdownHover.transitionDuration = 0.15f;
                    dropdownHover.boostAlphaOnHover = true;
                    dropdownHover.hoverAlpha = 0.7f;

                    var dropdown = dropdownGO.AddComponent<TMP_Dropdown>();

                    // Caption Label
                    var ddLabelGO = CreateText("Label", dropdownGO.transform, "Option", 14);
                    var ddLabelRect = ddLabelGO.GetComponent<RectTransform>();
                    ddLabelRect.anchorMin = Vector2.zero;
                    ddLabelRect.anchorMax = Vector2.one;
                    ddLabelRect.offsetMin = new Vector2(12, 2);
                    ddLabelRect.offsetMax = new Vector2(-30, -2);
                    ddLabelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
                    ddLabelGO.GetComponent<TMP_Text>().color = Color.white;
                    dropdown.captionText = ddLabelGO.GetComponent<TMP_Text>();

                    // Arrow (треугольник) - спрайт белый, цвет через Image.color
                    var arrowGO = new GameObject("Arrow");
                    arrowGO.transform.SetParent(dropdownGO.transform, false);
                    var arrowRect = arrowGO.AddComponent<RectTransform>();
                    arrowRect.anchorMin = new Vector2(1, 0.5f);
                    arrowRect.anchorMax = new Vector2(1, 0.5f);
                    arrowRect.sizeDelta = new Vector2(12, 12);
                    arrowRect.anchoredPosition = new Vector2(-14, 0);
                    var arrowImg = arrowGO.AddComponent<Image>();
                    arrowImg.preserveAspect = true;

                    if (arrowSprite != null)
                    {
                        arrowImg.sprite = arrowSprite;
                        arrowImg.color = new Color(0.8f, 0.8f, 0.8f, 1f); // Светло-серый
                        Debug.Log("[UIWindowPrefabGenerator] Arrow sprite applied to dropdown");
                    }
                    else
                    {
                        // Fallback - просто серый квадрат (будет видно что спрайт не загрузился)
                        arrowImg.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                        Debug.LogWarning("[UIWindowPrefabGenerator] Arrow sprite NOT FOUND!");
                    }

                    // Template (выпадающий список)
                    int listHeight = currentStyleConfig != null ? currentStyleConfig.dropdownListHeight : 120;
                    int itemHeight = currentStyleConfig != null ? Mathf.Max(24, currentStyleConfig.dropdownRowHeight - 4) : 28;
                    
                    var templateGO = new GameObject("Template");
                    templateGO.transform.SetParent(dropdownGO.transform, false);
                    var templateRect = templateGO.AddComponent<RectTransform>();
                    templateRect.anchorMin = new Vector2(0, 0);
                    templateRect.anchorMax = new Vector2(1, 0);
                    templateRect.pivot = new Vector2(0.5f, 1);
                    templateRect.sizeDelta = new Vector2(0, listHeight);
                    var templateImg = templateGO.AddComponent<Image>();
                    templateImg.color = new Color(0.12f, 0.12f, 0.16f, 0.98f);
                    Debug.Log($"[UIWindowPrefabGenerator] Dropdown template: listHeight={listHeight}, itemHeight={itemHeight}");

                    // Viewport
                    var viewportGO = new GameObject("Viewport");
                    viewportGO.transform.SetParent(templateGO.transform, false);
                    var viewportRect = viewportGO.AddComponent<RectTransform>();
                    viewportRect.anchorMin = Vector2.zero;
                    viewportRect.anchorMax = Vector2.one;
                    viewportRect.offsetMin = Vector2.zero;
                    viewportRect.offsetMax = Vector2.zero;
                    viewportGO.AddComponent<Image>().color = new Color(1, 1, 1, 0.01f);
                    viewportGO.AddComponent<Mask>().showMaskGraphic = false;

                    // Content
                    var contentGO = new GameObject("Content");
                    contentGO.transform.SetParent(viewportGO.transform, false);
                    var contentRect = contentGO.AddComponent<RectTransform>();
                    contentRect.anchorMin = new Vector2(0, 1);
                    contentRect.anchorMax = new Vector2(1, 1);
                    contentRect.pivot = new Vector2(0.5f, 1);
                    contentRect.sizeDelta = new Vector2(0, itemHeight);

                    // Item
                    var itemGO = new GameObject("Item");
                    itemGO.transform.SetParent(contentGO.transform, false);
                    var itemRect = itemGO.AddComponent<RectTransform>();
                    itemRect.anchorMin = new Vector2(0, 0.5f);
                    itemRect.anchorMax = new Vector2(1, 0.5f);
                    itemRect.sizeDelta = new Vector2(0, itemHeight);

                    var itemToggle = itemGO.AddComponent<Toggle>();
                    itemToggle.isOn = true;

                    // Item Background
                    var itemBgGO = new GameObject("Item Background");
                    itemBgGO.transform.SetParent(itemGO.transform, false);
                    var itemBgRect = itemBgGO.AddComponent<RectTransform>();
                    itemBgRect.anchorMin = Vector2.zero;
                    itemBgRect.anchorMax = Vector2.one;
                    itemBgRect.offsetMin = Vector2.zero;
                    itemBgRect.offsetMax = Vector2.zero;
                    var itemBgImg = itemBgGO.AddComponent<Image>();
                    itemBgImg.color = new Color(1f, 1f, 1f, 0.05f);

                    // Item Checkmark
                    var checkmarkGO = new GameObject("Item Checkmark");
                    checkmarkGO.transform.SetParent(itemGO.transform, false);
                    var checkRect = checkmarkGO.AddComponent<RectTransform>();
                    checkRect.anchorMin = new Vector2(0, 0.5f);
                    checkRect.anchorMax = new Vector2(0, 0.5f);
                    checkRect.sizeDelta = new Vector2(16, 16);
                    checkRect.anchoredPosition = new Vector2(12, 0);
                    var checkImg = checkmarkGO.AddComponent<Image>();
                    checkImg.color = new Color(0.29f, 0.62f, 1f, 1f);

                    // Item Label
                    var itemLabelGO = CreateText("Item Label", itemGO.transform, "Option", 14);
                    var itemLabelRect = itemLabelGO.GetComponent<RectTransform>();
                    itemLabelRect.anchorMin = Vector2.zero;
                    itemLabelRect.anchorMax = Vector2.one;
                    itemLabelRect.offsetMin = new Vector2(32, 0);
                    itemLabelRect.offsetMax = new Vector2(-10, 0);
                    itemLabelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
                    itemLabelGO.GetComponent<TMP_Text>().color = Color.white;

                    // Setup Toggle
                    itemToggle.targetGraphic = itemBgImg;
                    itemToggle.graphic = checkImg;

                    // Setup ScrollRect
                    var scrollRect = templateGO.AddComponent<ScrollRect>();
                    scrollRect.content = contentRect;
                    scrollRect.viewport = viewportRect;
                    scrollRect.horizontal = false;
                    scrollRect.vertical = true;
                    scrollRect.movementType = ScrollRect.MovementType.Clamped;

                    // Setup Dropdown references
                    dropdown.template = templateRect;
                    dropdown.itemText = itemLabelGO.GetComponent<TMP_Text>();

                    templateGO.SetActive(false);

                    return go;
                }

        /// <summary>
        /// Checkbox в стиле HTML
        /// </summary>
        /// <summary>
                /// Toggle Switch в стиле HTML (как в iOS/Android)
                /// </summary>
                /// <summary>
                        /// Создаёт Toggle - Checkbox или Switch в зависимости от настроек
                        /// </summary>
                        private static GameObject CreateToggle(string name, Transform parent, string label)
                        {
                            var go = new GameObject(name);
                            go.transform.SetParent(parent, false);
                            go.AddComponent<RectTransform>();
                            var le = go.AddComponent<LayoutElement>();
                            // Высота toggle row чуть больше чем checkbox size
                            int toggleRowHeight = currentStyleConfig != null ? Mathf.Max(24, currentStyleConfig.checkboxSize + 4) : 28;
                            le.preferredHeight = toggleRowHeight;
                            le.minHeight = toggleRowHeight;
                            le.flexibleHeight = 0;

                            int textGap = currentStyleConfig != null ? currentStyleConfig.checkboxTextGap : 8;
                            var hlg = go.AddComponent<HorizontalLayoutGroup>();
                            hlg.spacing = textGap;
                            hlg.childControlWidth = true;
                            hlg.childControlHeight = true;
                            hlg.childForceExpandWidth = false;
                            hlg.childForceExpandHeight = false;
                            hlg.childAlignment = TextAnchor.MiddleLeft;

                            // Определяем стиль toggle
                            ToggleStyle style = currentStyleConfig != null ? currentStyleConfig.toggleStyle : ToggleStyle.Checkbox;
                            Debug.Log($"[UIWindowPrefabGenerator] CreateToggle: {name}, style={style}, label='{label}'");

                            // Label слева, checkbox/switch справа (как в HTML mockup)
                            if (style == ToggleStyle.Checkbox)
                            {
                                return CreateCheckboxToggle(go, name, label);
                            }
                            else
                            {
                                return CreateSwitchToggle(go, name, label);
                            }
                        }

                        /// <summary>
                        /// Создаёт классический Checkbox с галочкой
                        /// Label СЛЕВА, Checkbox СПРАВА рядом с текстом
                        /// </summary>
                        private static GameObject CreateCheckboxToggle(GameObject container, string name, string label)
                        {
                            int checkboxSize = currentStyleConfig != null ? currentStyleConfig.checkboxSize : 20;
                            int textGap = currentStyleConfig != null ? currentStyleConfig.checkboxTextGap : 8;

                            // Label - СНАЧАЛА (будет слева)
                            var labelGO = CreateText("Label", container.transform, label, 14);
                            labelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
                            labelGO.GetComponent<TMP_Text>().color = new Color(0.88f, 0.88f, 0.88f, 1f);
                            var labelLE = labelGO.AddComponent<LayoutElement>();
                            // Используем preferredWidth вместо flexibleWidth чтобы checkbox был рядом
                            labelLE.preferredWidth = 130; // Фиксированная ширина как у dropdown label
                            labelLE.preferredHeight = 24;
                            
                            // Checkbox container - ПОТОМ (будет справа, рядом с текстом)
                            var toggleGO = new GameObject("Toggle");
                            toggleGO.transform.SetParent(container.transform, false);
                            var toggleRect = toggleGO.AddComponent<RectTransform>();
                            var toggleLE = toggleGO.AddComponent<LayoutElement>();
                            toggleLE.preferredWidth = checkboxSize;
                            toggleLE.preferredHeight = checkboxSize;
                            toggleLE.minWidth = checkboxSize;
                            toggleLE.minHeight = checkboxSize;
                            
                            Debug.Log($"[UIWindowPrefabGenerator] Checkbox: size={checkboxSize}, labelWidth=130, gap={textGap}");

                            // Background (квадрат с закруглёнными углами)
                            var bgGO = new GameObject("Background");
                            bgGO.transform.SetParent(toggleGO.transform, false);
                            var bgRect = bgGO.AddComponent<RectTransform>();
                            bgRect.anchorMin = Vector2.zero;
                            bgRect.anchorMax = Vector2.one;
                            bgRect.offsetMin = Vector2.zero;
                            bgRect.offsetMax = Vector2.zero;
                            var bgImg = bgGO.AddComponent<Image>();

                            // Спрайт с закруглением
                            Sprite checkboxBgSprite = null;
                            if (currentStyleConfig != null)
                            {
                                checkboxBgSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_CHECKBOX, "CheckboxBackground");
                                Debug.Log($"[UIWindowPrefabGenerator] Checkbox sprite lookup: found={checkboxBgSprite != null}");
                            }

                            if (checkboxBgSprite != null)
                            {
                                bgImg.sprite = checkboxBgSprite;
                                bgImg.type = Image.Type.Sliced;
                                // БЕЛЫЙ для шейдера UITwoColor
                                bgImg.color = Color.white;
                                
                                // Применяем двухцветный эффект
                                Color fillColor = new Color(0.25f, 0.25f, 0.3f, 1f);
                                Color borderColor = new Color(0.4f, 0.4f, 0.5f, 1f);
                                ApplyTwoColorEffect(bgGO, fillColor, borderColor);
                            }
                            else
                            {
                                bgImg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
                            }
                            Debug.Log($"[UIWindowPrefabGenerator] Checkbox with UITwoColorImage");

                            // Checkmark (галочка) - показывается когда isOn = true
                            var checkGO = new GameObject("Checkmark");
                            checkGO.transform.SetParent(bgGO.transform, false);
                            var checkRect = checkGO.AddComponent<RectTransform>();
                            checkRect.anchorMin = new Vector2(0.15f, 0.15f);
                            checkRect.anchorMax = new Vector2(0.85f, 0.85f);
                            checkRect.offsetMin = Vector2.zero;
                            checkRect.offsetMax = Vector2.zero;
                            var checkImg = checkGO.AddComponent<Image>();

                            // Спрайт галочки - БЕЛЫЙ, цвет через Image.color
                            Sprite checkmarkSprite = null;
                            if (currentStyleConfig != null)
                            {
                                checkmarkSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_CHECKBOX, "Checkmark");
                                Debug.Log($"[UIWindowPrefabGenerator] Checkmark sprite lookup: found={checkmarkSprite != null}");
                            }

                            if (checkmarkSprite != null)
                            {
                                checkImg.sprite = checkmarkSprite;
                            }
                            // Синий цвет галочки
                            checkImg.color = new Color(0.29f, 0.62f, 1f, 1f); // #4A9EFF

                            var toggle = toggleGO.AddComponent<Toggle>();
                            toggle.targetGraphic = bgImg;
                            toggle.graphic = checkImg;
                            toggle.isOn = false;

                            // UIHoverEffect для checkbox
                            var hoverEffect = bgGO.AddComponent<UIHoverEffect>();
                            hoverEffect.hoverBrightness = 1.3f;
                            hoverEffect.pressedBrightness = 0.9f;
                            hoverEffect.transitionDuration = 0.15f;
                            hoverEffect.boostAlphaOnHover = true;
                            hoverEffect.hoverAlpha = 0.25f;

                            Debug.Log($"[UIWindowPrefabGenerator] Checkbox created: size={checkboxSize}x{checkboxSize}");

                            return container;
                        }

                        /// <summary>
                        /// Создаёт Switch в стиле iOS/Android
                        /// Label СЛЕВА, switch СПРАВА
                        /// </summary>
                        private static GameObject CreateSwitchToggle(GameObject container, string name, string label)
                        {
                            // Label - СНАЧАЛА (будет слева)
                            var labelGO = CreateText("Label", container.transform, label, 14);
                            labelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
                            labelGO.GetComponent<TMP_Text>().color = new Color(0.88f, 0.88f, 0.88f, 1f);
                            var labelLE = labelGO.AddComponent<LayoutElement>();
                            labelLE.flexibleWidth = 1;
                            labelLE.preferredHeight = 30;
                            
                            // Toggle Switch container (48x26) - ПОТОМ (будет справа)
                            var toggleGO = new GameObject("Toggle");
                            toggleGO.transform.SetParent(container.transform, false);
                            var toggleRect = toggleGO.AddComponent<RectTransform>();
                            var toggleLE = toggleGO.AddComponent<LayoutElement>();
                            toggleLE.preferredWidth = 48;
                            toggleLE.preferredHeight = 26;
                            toggleLE.minWidth = 48;
                            toggleLE.minHeight = 26;

                            // Background (pill shape / капсула)
                            var bgGO = new GameObject("Background");
                            bgGO.transform.SetParent(toggleGO.transform, false);
                            var bgRect = bgGO.AddComponent<RectTransform>();
                            bgRect.anchorMin = Vector2.zero;
                            bgRect.anchorMax = Vector2.one;
                            bgRect.offsetMin = Vector2.zero;
                            bgRect.offsetMax = Vector2.zero;
                            var bgImg = bgGO.AddComponent<Image>();
                            // ВИДИМЫЙ тёмный фон для Switch - спрайт белый
                            bgImg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
                            Debug.Log($"[UIWindowPrefabGenerator] Switch '{label}': background color={bgImg.color} (OPAQUE DARK)");

                            Sprite toggleBgSprite = null;
                            if (currentStyleConfig != null)
                            {
                                toggleBgSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_BACKGROUND, "InputBackground");
                            }

                            if (toggleBgSprite != null)
                            {
                                bgImg.sprite = toggleBgSprite;
                                bgImg.type = Image.Type.Sliced;
                            }

                            // Handle (круглая ручка)
                            var handleGO = new GameObject("Handle");
                            handleGO.transform.SetParent(bgGO.transform, false);
                            var handleRect = handleGO.AddComponent<RectTransform>();
                            handleRect.anchorMin = new Vector2(0, 0.5f);
                            handleRect.anchorMax = new Vector2(0, 0.5f);
                            handleRect.pivot = new Vector2(0, 0.5f);
                            handleRect.sizeDelta = new Vector2(20, 20);
                            handleRect.anchoredPosition = new Vector2(3, 0);

                            var handleImg = handleGO.AddComponent<Image>();
                            handleImg.color = Color.white;

                            Sprite handleSprite = null;
                            if (currentStyleConfig != null)
                            {
                                handleSprite = UIIconGenerator.FindSpriteByLabel(UIIconGenerator.LABEL_UI_SLIDER, "SliderHandle");
                            }

                            if (handleSprite != null)
                            {
                                handleImg.sprite = handleSprite;
                            }

                            // Checkmark overlay (синий фон когда ON)
                            var checkGO = new GameObject("Checkmark");
                            checkGO.transform.SetParent(bgGO.transform, false);
                            var checkRect = checkGO.AddComponent<RectTransform>();
                            checkRect.anchorMin = Vector2.zero;
                            checkRect.anchorMax = Vector2.one;
                            checkRect.offsetMin = Vector2.zero;
                            checkRect.offsetMax = Vector2.zero;
                            var checkImg = checkGO.AddComponent<Image>();
                            checkImg.color = new Color(0.29f, 0.62f, 1f, 1f);

                            if (toggleBgSprite != null)
                            {
                                checkImg.sprite = toggleBgSprite;
                                checkImg.type = Image.Type.Sliced;
                            }

                            // Перемещаем Handle поверх Checkmark
                            handleGO.transform.SetAsLastSibling();

                            var toggle = toggleGO.AddComponent<Toggle>();
                            toggle.targetGraphic = bgImg;
                            toggle.graphic = checkImg;
                            toggle.isOn = false;

                            // Аниматор для перемещения handle
                            var toggleAnim = toggleGO.AddComponent<ToggleSwitchAnimator>();
                            toggleAnim.handleTransform = handleRect;
                            toggleAnim.offPositionX = 3f;
                            toggleAnim.onPositionX = 25f;

                            // UIHoverEffect
                            var toggleHover = bgGO.AddComponent<UIHoverEffect>();
                            toggleHover.hoverBrightness = 1.3f;
                            toggleHover.pressedBrightness = 0.9f;
                            toggleHover.transitionDuration = 0.15f;
                            toggleHover.boostAlphaOnHover = true;
                            toggleHover.hoverAlpha = 0.25f;

                            Debug.Log($"[UIWindowPrefabGenerator] Switch toggle created: size=48x26");

                            return container;
                        }

        private static void SetField(Component component, string fieldName, object value)
        {
            var field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(component, value);
                EditorUtility.SetDirty(component);
            }
            else
            {
                Debug.LogWarning($"Field '{fieldName}' not found in {component.GetType().Name}");
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            var current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static GameObject CreateDialogBase(string name, Vector2 size)
        {
            var root = new GameObject(name);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = size;

            root.AddComponent<CanvasGroup>();

            // Background
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Header
            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(root.transform, false);
            var headerRect = headerGO.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 0.85f);
            headerRect.anchorMax = Vector2.one;
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;

            var headerBg = headerGO.AddComponent<Image>();
            headerBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var titleGO = CreateText("Title", headerGO.transform, "Dialog Title", 20);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(15, 0);
            titleRect.offsetMax = new Vector2(-15, 0);
            titleGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(root.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = new Vector2(1, 0.85f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            return root;
        }

        private static GameObject CreateButtonsContainer(Transform parent)
        {
            var go = new GameObject("Buttons");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0.3f);
            rect.offsetMin = new Vector2(20, 10);
            rect.offsetMax = new Vector2(-20, -5);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            return go;
        }

        private static GameObject CreateTMPInputField(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 1f);

            // Text Area
            var textAreaGO = new GameObject("Text Area");
            textAreaGO.transform.SetParent(go.transform, false);
            var textAreaRect = textAreaGO.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);
            textAreaGO.AddComponent<RectMask2D>();

            // Placeholder
            var placeholderGO = CreateText("Placeholder", textAreaGO.transform, "Введите текст...", 16);
            var placeholderRect = placeholderGO.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            var placeholderTMP = placeholderGO.GetComponent<TMP_Text>();
            placeholderTMP.alignment = TextAlignmentOptions.MidlineLeft;
            placeholderTMP.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            placeholderTMP.fontStyle = FontStyles.Italic;

            // Text
            var textGO = CreateText("Text", textAreaGO.transform, "", 16);
            var textRectT = textGO.GetComponent<RectTransform>();
            textRectT.anchorMin = Vector2.zero;
            textRectT.anchorMax = Vector2.one;
            textRectT.offsetMin = Vector2.zero;
            textRectT.offsetMax = Vector2.zero;
            textGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;

            var inputField = go.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = textGO.GetComponent<TMP_Text>();
            inputField.placeholder = placeholderTMP;

            return go;
        }

        private static bool SavePrefab(GameObject instance, string name)
        {
            string basePath = ResolveOutputPath();
            EnsureFolder(basePath);
            string path = $"{basePath}/{name}.prefab";
            path = path.Replace("\\", "/");

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                // Если флаг перезаписи включён — не спрашиваем
                if (!OverwriteWithoutPrompt)
                {
                    if (!EditorUtility.DisplayDialog("Prefab exists",
                        $"{name} already exists. Overwrite?", "Yes", "Skip"))
                    {
                        Object.DestroyImmediate(instance);
                        // Ensure label exists so UISystemConfig auto-scan can still find the prefab.
                        EnsureUIWindowLabel(existing);
                        Debug.Log($"[UIWindowPrefabGenerator] Using existing (kept): {path}");
                        return true;
                    }
                }
                AssetDatabase.DeleteAsset(path);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            // Добавляем метку для автосканирования
            EnsureUIWindowLabel(prefab);

            Debug.Log($"[UIWindowPrefabGenerator] Created: {path}");
            return true;
        }

        private static void EnsureUIWindowLabel(Object asset)
        {
            if (asset == null) return;

            var labels = new HashSet<string>(AssetDatabase.GetLabels(asset));
            labels.Add("UIWindow");
            AssetDatabase.SetLabels(asset, new List<string>(labels).ToArray());
        }

        #endregion
    }
}
