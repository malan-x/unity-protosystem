// Packages/com.protosystem.core/Tests/Editor/LocalizationLanguageTests.cs
using System.Collections.Generic;
using NUnit.Framework;

namespace ProtoSystem.Tests
{
    /// <summary>
    /// Выбор языка на первом запуске. Правило проверяется тестом, а не игрой:
    /// Application.systemLanguage в рантайме не подменить, а ошибка тут видна
    /// только тем игрокам, чей язык мы как раз и переводили.
    /// </summary>
    [TestFixture]
    public class LocalizationLanguageTests
    {
        // Как в Last Convoy: региональная локаль соседствует с простыми кодами
        private static readonly List<string> Available =
            new() { "ru", "en", "de", "es", "fr", "pt-BR" };

        private const string Fallback = "en";

        [Test]
        public void SavedChoice_WinsOverSystemLanguage()
        {
            Assert.AreEqual("de",
                LocalizationSystem.ResolveLanguage("de", "ru", Available, Fallback));
        }

        [Test]
        public void SystemLanguage_UsedWhenNothingSaved()
        {
            Assert.AreEqual("fr",
                LocalizationSystem.ResolveLanguage("", "fr", Available, Fallback));
        }

        /// <summary>
        /// Unity отдаёт «pt» для ЛЮБОГО португальского, включая бразильский.
        /// Пока совпадение искалось только точное, игрок с pt-BR получал
        /// английский, хотя перевод для него лежал в проекте.
        /// </summary>
        [Test]
        public void PortugueseSystem_FindsRegionalLocale()
        {
            Assert.AreEqual("pt-BR",
                LocalizationSystem.ResolveLanguage("", "pt", Available, Fallback));
        }

        /// <summary>И в обратную сторону: региональный код системы → простая локаль.</summary>
        [Test]
        public void RegionalSystemCode_FindsPlainLocale()
        {
            Assert.AreEqual("en",
                LocalizationSystem.ResolveLanguage("", "en-GB", Available, Fallback));
            Assert.AreEqual("es",
                LocalizationSystem.ResolveLanguage("", "es-MX", Available, Fallback));
        }

        [Test]
        public void UnsupportedLanguage_FallsBackToDefault()
        {
            Assert.AreEqual(Fallback,
                LocalizationSystem.ResolveLanguage("", "ja", Available, Fallback));
        }

        /// <summary>
        /// Сохранённый язык мог пропасть из сборки (локаль убрали) — тогда выбор
        /// не «залипает» пустым экраном, а уходит к системе и дальше к дефолту.
        /// </summary>
        [Test]
        public void SavedLanguageNoLongerAvailable_FallsThrough()
        {
            Assert.AreEqual("de",
                LocalizationSystem.ResolveLanguage("it", "de", Available, Fallback));
            Assert.AreEqual(Fallback,
                LocalizationSystem.ResolveLanguage("it", "ja", Available, Fallback));
        }

        [Test]
        public void AutoDetectOff_SystemIgnored()
        {
            // config.autoDetectSystemLanguage = false отдаёт system == null
            Assert.AreEqual(Fallback,
                LocalizationSystem.ResolveLanguage("", null, Available, Fallback));
        }

        [Test]
        public void CaseInsensitive()
        {
            Assert.AreEqual("pt-BR",
                LocalizationSystem.ResolveLanguage("PT-br", null, Available, Fallback));
        }
    }
}
