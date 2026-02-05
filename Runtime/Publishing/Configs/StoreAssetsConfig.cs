// Packages/com.protosystem.core/Runtime/Publishing/Configs/StoreAssetsConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Publishing
{
    /// <summary>
    /// Определение требуемого изображения для магазина
    /// </summary>
    [Serializable]
    public class StoreAssetDefinition
    {
        public string id;
        public string displayName;
        public int width;
        public int height;
        public bool required;
        public bool allowTransparency;
        public string description;
        public string steamworksUrl;  // Относительный путь в Steamworks
    }

    /// <summary>
    /// Запись об изображении пользователя
    /// </summary>
    [Serializable]
    public class StoreAssetEntry
    {
        public string assetId;
        public string sourcePath;      // Путь к исходному изображению
        public string outputPath;      // Путь к подготовленному изображению
        public string prompt;          // AI промпт для генерации
        public string notes;           // Заметки пользователя
        public bool isReady;           // Подготовлено и готово к загрузке
        public DateTime lastModified;
    }

    /// <summary>
    /// Локализованный текст для одного языка
    /// </summary>
    [Serializable]
    public class StoreLocalizedText
    {
        public string languageCode;        // english, russian, french, etc.
        public string shortDescription;    // Краткое описание (200-300 символов)
        [TextArea(10, 30)]
        public string about;               // Полное описание (с BBCode)
        
        // Системные требования
        public string sysreqsMinOS;
        public string sysreqsMinProcessor;
        public string sysreqsMinGraphics;
        public string sysreqsMinMemory;
        public string sysreqsMinStorage;
        
        public string sysreqsRecOS;
        public string sysreqsRecProcessor;
        public string sysreqsRecGraphics;
        public string sysreqsRecMemory;
        public string sysreqsRecStorage;
    }

    /// <summary>
    /// Конфигурация Store Assets для публикации
    /// </summary>
    [CreateAssetMenu(fileName = "StoreAssetsConfig", menuName = "ProtoSystem/Publishing/Store Assets Config")]
    public class StoreAssetsConfig : ScriptableObject
    {
        [Header("Output")]
        [Tooltip("Папка для готовых изображений (относительно Assets)")]
        public string outputFolder = "Publishing/StoreAssets";

        [Header("Steam Assets")]
        public List<StoreAssetEntry> steamAssets = new List<StoreAssetEntry>();

        [Header("Steam Screenshots")]
        public List<string> steamScreenshots = new List<string>();  // Пути к скриншотам

        [Header("Steam Localized Texts")]
        public List<StoreLocalizedText> steamTexts = new List<StoreLocalizedText>();

        [Header("Itch.io Assets")]
        public List<StoreAssetEntry> itchAssets = new List<StoreAssetEntry>();

        [Header("Epic Assets")]
        public List<StoreAssetEntry> epicAssets = new List<StoreAssetEntry>();

        [Header("GOG Assets")]
        public List<StoreAssetEntry> gogAssets = new List<StoreAssetEntry>();

        /// <summary>
        /// Все поддерживаемые языки Steam
        /// </summary>
        public static readonly string[] SteamLanguages = new[]
        {
            "english", "french", "italian", "german", "spanish",
            "schinese", "tchinese", "russian", "japanese", "portuguese",
            "brazilian", "polish", "danish", "dutch", "finnish",
            "norwegian", "swedish", "hungarian", "czech", "romanian",
            "turkish", "arabic", "bulgarian", "greek", "korean",
            "thai", "ukrainian", "vietnamese"
        };

        /// <summary>
        /// Отображаемые имена языков
        /// </summary>
        public static readonly Dictionary<string, string> LanguageDisplayNames = new Dictionary<string, string>
        {
            { "english", "English" },
            { "french", "French (Français)" },
            { "italian", "Italian (Italiano)" },
            { "german", "German (Deutsch)" },
            { "spanish", "Spanish - Spain (Español)" },
            { "schinese", "Chinese - Simplified (简体中文)" },
            { "tchinese", "Chinese - Traditional (繁體中文)" },
            { "russian", "Russian (Русский)" },
            { "japanese", "Japanese (日本語)" },
            { "portuguese", "Portuguese - Portugal" },
            { "brazilian", "Portuguese - Brazil (Português-Brasil)" },
            { "polish", "Polish (Polski)" },
            { "danish", "Danish (Dansk)" },
            { "dutch", "Dutch (Nederlands)" },
            { "finnish", "Finnish (Suomi)" },
            { "norwegian", "Norwegian (Norsk)" },
            { "swedish", "Swedish (Svenska)" },
            { "hungarian", "Hungarian (Magyar)" },
            { "czech", "Czech (Čeština)" },
            { "romanian", "Romanian (Română)" },
            { "turkish", "Turkish (Türkçe)" },
            { "arabic", "Arabic (العربية)" },
            { "bulgarian", "Bulgarian (Български)" },
            { "greek", "Greek (Ελληνικά)" },
            { "korean", "Korean (한국어)" },
            { "thai", "Thai (ไทย)" },
            { "ukrainian", "Ukrainian (Українська)" },
            { "vietnamese", "Vietnamese (Tiếng Việt)" }
        };

        /// <summary>
        /// Определения Steam изображений
        /// </summary>
        public static readonly StoreAssetDefinition[] SteamAssetDefinitions = new[]
        {
            // Изображения для магазина (Store Capsules)
            new StoreAssetDefinition
            {
                id = "header_capsule",
                displayName = "Верхнее изображение",
                width = 920, height = 430,
                required = true,
                description = "Верх страницы магазина, рекомендации, библиотека (сетка), Big Picture.",
                steamworksUrl = "graphicalassets"
            },
            new StoreAssetDefinition
            {
                id = "small_capsule",
                displayName = "Малое изображение",
                width = 462, height = 174,
                required = true,
                description = "Списки Steam: поиск, лидеры продаж, новинки. Логотип должен заполнять почти всё изображение.",
                steamworksUrl = "graphicalassets"
            },
            new StoreAssetDefinition
            {
                id = "main_capsule",
                displayName = "Основное изображение",
                width = 1232, height = 706,
                required = true,
                description = "Карусель избранных на главной странице. Без цитат, рейтингов и наград.",
                steamworksUrl = "graphicalassets"
            },
            new StoreAssetDefinition
            {
                id = "vertical_capsule",
                displayName = "Вертикальное изображение",
                width = 748, height = 896,
                required = true,
                description = "Сезонные распродажи, страницы распродаж. Без цитат, рейтингов и наград.",
                steamworksUrl = "graphicalassets"
            },
            new StoreAssetDefinition
            {
                id = "page_background",
                displayName = "Фон страницы",
                width = 1438, height = 810,
                required = false,
                description = "Фон страницы магазина. Авто-синий фильтр и размытие по краям.",
                steamworksUrl = "graphicalassets"
            },

            // Иконка сообщества (clientimages)
            new StoreAssetDefinition
            {
                id = "community_icon",
                displayName = "Иконка сообщества",
                width = 184, height = 184,
                required = true,
                description = "JPG 184x184. Уведомления, сообщество, библиотека.",
                steamworksUrl = "clientimages"
            },
        };

        /// <summary>
        /// Определения itch.io изображений
        /// </summary>
        public static readonly StoreAssetDefinition[] ItchAssetDefinitions = new[]
        {
            new StoreAssetDefinition
            {
                id = "cover",
                displayName = "Cover Image",
                width = 630, height = 500,
                required = true,
                description = "Главное изображение на странице игры."
            },
            new StoreAssetDefinition
            {
                id = "banner",
                displayName = "Banner",
                width = 960, height = 540,
                required = false,
                description = "Баннер для featured секций."
            },
        };

        /// <summary>
        /// Получить запись по ID или создать новую
        /// </summary>
        public StoreAssetEntry GetOrCreateEntry(string assetId, List<StoreAssetEntry> entries)
        {
            var entry = entries.Find(e => e.assetId == assetId);
            if (entry == null)
            {
                entry = new StoreAssetEntry { assetId = assetId };
                entries.Add(entry);
            }
            return entry;
        }

        /// <summary>
        /// Получить или создать локализованный текст для языка
        /// </summary>
        public StoreLocalizedText GetOrCreateLocalizedText(string languageCode)
        {
            var text = steamTexts.Find(t => t.languageCode == languageCode);
            if (text == null)
            {
                text = new StoreLocalizedText { languageCode = languageCode };
                
                // Копируем системные требования из английского
                var english = steamTexts.Find(t => t.languageCode == "english");
                if (english != null)
                {
                    text.sysreqsMinOS = english.sysreqsMinOS;
                    text.sysreqsMinProcessor = english.sysreqsMinProcessor;
                    text.sysreqsMinGraphics = english.sysreqsMinGraphics;
                    text.sysreqsMinMemory = english.sysreqsMinMemory;
                    text.sysreqsMinStorage = english.sysreqsMinStorage;
                    text.sysreqsRecOS = english.sysreqsRecOS;
                    text.sysreqsRecProcessor = english.sysreqsRecProcessor;
                    text.sysreqsRecGraphics = english.sysreqsRecGraphics;
                    text.sysreqsRecMemory = english.sysreqsRecMemory;
                    text.sysreqsRecStorage = english.sysreqsRecStorage;
                }
                
                steamTexts.Add(text);
            }
            return text;
        }

        /// <summary>
        /// Проверить заполнен ли язык
        /// </summary>
        public bool IsLanguageFilled(string languageCode)
        {
            var text = steamTexts.Find(t => t.languageCode == languageCode);
            if (text == null) return false;
            return !string.IsNullOrEmpty(text.shortDescription) && !string.IsNullOrEmpty(text.about);
        }

        /// <summary>
        /// Получить полный путь к output папке
        /// </summary>
        public string GetOutputPath()
        {
            return System.IO.Path.Combine(Application.dataPath, outputFolder);
        }

        /// <summary>
        /// Создать конфиг по умолчанию
        /// </summary>
        public static StoreAssetsConfig CreateDefault()
        {
            var config = CreateInstance<StoreAssetsConfig>();
            return config;
        }

        /// <summary>
        /// Создать конфиг с указанным namespace
        /// </summary>
        public static StoreAssetsConfig CreateDefault(string projectNamespace)
        {
            var config = CreateInstance<StoreAssetsConfig>();
            
            if (!string.IsNullOrEmpty(projectNamespace))
            {
                config.outputFolder = $"{projectNamespace}/Publishing/StoreAssets";
            }
            
            return config;
        }
    }
}
