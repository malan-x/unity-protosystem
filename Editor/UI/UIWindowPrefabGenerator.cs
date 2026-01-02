// Packages/com.protosystem.core/Editor/UI/UIWindowPrefabGenerator.cs
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Генератор prefab'ов для базовых UI окон
    /// </summary>
    public static class UIWindowPrefabGenerator
    {
        private const string DEFAULT_PREFAB_PATH = "Assets/Prefabs/UI/Windows";

        [MenuItem("ProtoSystem/UI/Generate Base Windows", priority = 120)]
        public static void GenerateAllBaseWindows()
        {
            EnsureFolder(DEFAULT_PREFAB_PATH);

            int created = 0;

            if (GenerateMainMenu()) created++;
            if (GenerateGameHUD()) created++;
            if (GeneratePauseMenu()) created++;
            if (GenerateSettings()) created++;
            if (GenerateGameOver()) created++;
            if (GenerateStatistics()) created++;
            if (GenerateCredits()) created++;
            if (GenerateLoading()) created++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Пересобираем граф
            UIWindowGraphBuilder.RebuildGraph();

            Debug.Log($"[UIWindowPrefabGenerator] Created {created} prefabs. Graph rebuilt.");
        }

        #region Individual Generators

        [MenuItem("ProtoSystem/UI/Generate Window/Main Menu", priority = 121)]
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

        [MenuItem("ProtoSystem/UI/Generate Window/Game HUD", priority = 122)]
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

        [MenuItem("ProtoSystem/UI/Generate Window/Pause Menu", priority = 123)]
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

        [MenuItem("ProtoSystem/UI/Generate Window/Settings", priority = 124)]
        public static bool GenerateSettings()
        {
            var root = CreateWindowBase("Settings", new Vector2(580, 700), 1f); // Полностью непрозрачный

            // Title
            var titleGO = CreateText("Title", root.transform, "НАСТРОЙКИ", 32);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.92f);
            titleRect.anchorMax = new Vector2(1, 0.99f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            titleGO.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

            // ScrollView
            var scrollGO = CreateScrollView("ScrollView", root.transform);
            var scrollRect = scrollGO.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.03f, 0.12f);
            scrollRect.anchorMax = new Vector2(0.97f, 0.90f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            // Content inside scroll
            var contentGO = scrollGO.transform.Find("Viewport/Content").gameObject;
            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(20, 20, 15, 15);

            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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

            // Buttons
            var buttonsGO = new GameObject("Buttons");
            buttonsGO.transform.SetParent(root.transform, false);
            var buttonsRect = buttonsGO.AddComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.05f, 0.02f);
            buttonsRect.anchorMax = new Vector2(0.95f, 0.10f);
            buttonsRect.offsetMin = Vector2.zero;
            buttonsRect.offsetMax = Vector2.zero;

            var hlg = buttonsGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            var applyBtn = CreateButton("ApplyButton", buttonsGO.transform, "Применить", new Vector2(130, 42));
            var resetBtn = CreateButton("ResetButton", buttonsGO.transform, "Сброс", new Vector2(110, 42));
            var backBtn = CreateButton("BackButton", buttonsGO.transform, "Назад", new Vector2(110, 42));

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

        [MenuItem("ProtoSystem/UI/Generate Window/Credits", priority = 125)]
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

            var creditsTextGO = CreateText("CreditsText", scrollContentGO.transform, 
                "<size=24><b>Разработка</b></size>\nИмя Разработчика\n\n" +
                "<size=24><b>Дизайн</b></size>\nИмя Дизайнера\n\n" +
                "<size=24><b>Музыка</b></size>\nИмя Композитора\n\n" +
                "<size=24><b>Благодарности</b></size>\nВсем кто помогал!", 16);
            var creditsTextRect = creditsTextGO.GetComponent<RectTransform>();
            creditsTextRect.anchorMin = Vector2.zero;
            creditsTextRect.anchorMax = Vector2.one;
            creditsTextRect.offsetMin = new Vector2(20, 20);
            creditsTextRect.offsetMax = new Vector2(-20, -20);
            var creditsText = creditsTextGO.GetComponent<TMP_Text>();
            creditsText.alignment = TextAlignmentOptions.Top;
            creditsText.enableWordWrapping = true;
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
            SetField(component, "creditsText", creditsText);
            SetField(component, "scrollRect", scrollView);
            SetField(component, "contentTransform", scrollContentRect);
            SetField(component, "backButton", backBtn.GetComponent<Button>());
            SetField(component, "skipButton", skipBtn.GetComponent<Button>());

            return SavePrefab(root, "CreditsWindow");
        }

        [MenuItem("ProtoSystem/UI/Generate Window/Loading", priority = 126)]
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

        [MenuItem("ProtoSystem/UI/Generate Window/GameOver", priority = 126)]
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

        [MenuItem("ProtoSystem/UI/Generate Window/Statistics", priority = 127)]
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
                bg.color = new Color(0.15f, 0.15f, 0.15f, bgAlpha);
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
            contentRect.sizeDelta = new Vector2(0, 0);

            // Scrollbar
            var scrollbarGO = new GameObject("Scrollbar");
            scrollbarGO.transform.SetParent(scrollGO.transform, false);
            var scrollbarRect = scrollbarGO.AddComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = new Vector2(1, 1);
            scrollbarRect.pivot = new Vector2(1, 1);
            scrollbarRect.sizeDelta = new Vector2(10, 0);

            var scrollbarImage = scrollbarGO.AddComponent<Image>();
            scrollbarImage.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            var scrollbar = scrollbarGO.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            // Scrollbar Handle
            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(scrollbarGO.transform, false);
            var handleRect = handleGO.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(10, 10);

            var handleImage = handleGO.AddComponent<Image>();
            handleImage.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);

            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;

            // Setup ScrollRect references
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            return scrollGO;
        }

        private static GameObject CreateSectionLabel(string name, Transform parent, string text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            tmp.alignment = TextAlignmentOptions.Left;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 40;
            le.minHeight = 35;

            return go;
        }

        private static GameObject CreateButton(string name, Transform parent, string text, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.25f, 1f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            colors.pressedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            btn.colors = colors;

            var textGO = CreateText("Text", go.transform, text, 18);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return go;
        }

        private static GameObject CreateMenuButton(string name, Transform parent, string text)
        {
            var btn = CreateButton(name, parent, text, new Vector2(0, 50));
            btn.AddComponent<LayoutElement>().preferredHeight = 50;
            return btn;
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
            bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Fill
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(go.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.7f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var fillImg = fillGO.AddComponent<Image>();
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
        /// Слайдер для настроек с текстом значения справа
        /// </summary>
        private static GameObject CreateSettingsSlider(string name, Transform parent, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<LayoutElement>().preferredHeight = 40;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;

            // Label
            var labelGO = CreateText("Label", go.transform, label, 16);
            labelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            labelGO.AddComponent<LayoutElement>().preferredWidth = 140;

            // Slider
            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(go.transform, false);
            sliderGO.AddComponent<RectTransform>();
            sliderGO.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Slider background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(sliderGO.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.3f);
            bgRect.anchorMax = new Vector2(1, 0.7f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Fill area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.3f);
            fillAreaRect.anchorMax = new Vector2(1, 0.7f);
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

            // Value text
            var valueText = CreateText("ValueText", go.transform, "100%", 16);
            valueText.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineRight;
            valueText.AddComponent<LayoutElement>().preferredWidth = 55;

            return go;
        }

        private static GameObject CreateDropdown(string name, Transform parent, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<LayoutElement>().preferredHeight = 40;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;

            var labelGO = CreateText("Label", go.transform, label, 16);
            labelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            labelGO.AddComponent<LayoutElement>().preferredWidth = 130;

            var dropdownGO = new GameObject("Dropdown");
            dropdownGO.transform.SetParent(go.transform, false);
            var dropdownRect = dropdownGO.AddComponent<RectTransform>();
            dropdownGO.AddComponent<LayoutElement>().flexibleWidth = 1;

            var dropdownImg = dropdownGO.AddComponent<Image>();
            dropdownImg.color = new Color(0.25f, 0.25f, 0.25f, 1f);

            var dropdown = dropdownGO.AddComponent<TMP_Dropdown>();

            // Caption Label
            var ddLabelGO = CreateText("Label", dropdownGO.transform, "Option", 15);
            var ddLabelRect = ddLabelGO.GetComponent<RectTransform>();
            ddLabelRect.anchorMin = Vector2.zero;
            ddLabelRect.anchorMax = Vector2.one;
            ddLabelRect.offsetMin = new Vector2(10, 2);
            ddLabelRect.offsetMax = new Vector2(-30, -2);
            ddLabelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            dropdown.captionText = ddLabelGO.GetComponent<TMP_Text>();

            // Arrow
            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(dropdownGO.transform, false);
            var arrowRect = arrowGO.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.sizeDelta = new Vector2(20, 20);
            arrowRect.anchoredPosition = new Vector2(-15, 0);
            var arrowImg = arrowGO.AddComponent<Image>();
            arrowImg.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            // Template
            var templateGO = new GameObject("Template");
            templateGO.transform.SetParent(dropdownGO.transform, false);
            var templateRect = templateGO.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.sizeDelta = new Vector2(0, 150);
            templateGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

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
            contentRect.sizeDelta = new Vector2(0, 28);

            // Item (с Toggle - обязательно!)
            var itemGO = new GameObject("Item");
            itemGO.transform.SetParent(contentGO.transform, false);
            var itemRect = itemGO.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 28);

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
            itemBgImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            // Item Checkmark
            var checkmarkGO = new GameObject("Item Checkmark");
            checkmarkGO.transform.SetParent(itemGO.transform, false);
            var checkRect = checkmarkGO.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0, 0.5f);
            checkRect.anchorMax = new Vector2(0, 0.5f);
            checkRect.sizeDelta = new Vector2(20, 20);
            checkRect.anchoredPosition = new Vector2(12, 0);
            var checkImg = checkmarkGO.AddComponent<Image>();
            checkImg.color = new Color(0.3f, 0.7f, 1f, 1f);

            // Item Label
            var itemLabelGO = CreateText("Item Label", itemGO.transform, "Option", 14);
            var itemLabelRect = itemLabelGO.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(28, 0);
            itemLabelRect.offsetMax = new Vector2(-10, 0);
            itemLabelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;

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

        private static GameObject CreateToggle(string name, Transform parent, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<LayoutElement>().preferredHeight = 38;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;

            var labelGO = CreateText("Label", go.transform, label, 16);
            labelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            labelGO.AddComponent<LayoutElement>().preferredWidth = 160;

            var toggleGO = new GameObject("Toggle");
            toggleGO.transform.SetParent(go.transform, false);
            var toggleRect = toggleGO.AddComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(34, 34);
            toggleGO.AddComponent<LayoutElement>().preferredWidth = 34;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(toggleGO.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.25f, 0.25f, 0.25f, 1f);

            var checkGO = new GameObject("Checkmark");
            checkGO.transform.SetParent(bgGO.transform, false);
            var checkRect = checkGO.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.15f, 0.15f);
            checkRect.anchorMax = new Vector2(0.85f, 0.85f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            var checkImg = checkGO.AddComponent<Image>();
            checkImg.color = new Color(0.3f, 0.7f, 0.3f, 1f);

            var toggle = toggleGO.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = true;

            return go;
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

        private static bool SavePrefab(GameObject instance, string name)
        {
            string path = $"{DEFAULT_PREFAB_PATH}/{name}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Prefab exists",
                    $"{name} already exists. Overwrite?", "Yes", "Skip"))
                {
                    Object.DestroyImmediate(instance);
                    return false;
                }
                AssetDatabase.DeleteAsset(path);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            // Добавляем метку для автосканирования
            AssetDatabase.SetLabels(prefab, new[] { "UIWindow" });

            Debug.Log($"[UIWindowPrefabGenerator] Created: {path}");
            return true;
        }

        #endregion
    }
}
