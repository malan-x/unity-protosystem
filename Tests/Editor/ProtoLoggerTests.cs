using NUnit.Framework;
using ProtoSystem;

namespace ProtoSystem.Tests
{
    /// <summary>
    /// Юнит-тесты для ProtoLogger
    /// </summary>
    [TestFixture]
    public class ProtoLoggerTests
    {
        private LogSettings _originalSettings;

        [SetUp]
        public void SetUp()
        {
            // Сохраняем оригинальные настройки
            _originalSettings = ProtoLogger.Settings;
            
            // Создаём чистые настройки для тестов
            ProtoLogger.Settings = new LogSettings
            {
                globalLogLevel = LogLevel.All,
                enabledCategories = LogCategory.All,
                filterMode = SystemFilterMode.All
            };
        }

        [TearDown]
        public void TearDown()
        {
            // Восстанавливаем оригинальные настройки
            ProtoLogger.Settings = _originalSettings;
        }

        #region Basic Level Tests

        [Test]
        public void ShouldLog_WithNullSettings_OnlyErrors()
        {
            ProtoLogger.Settings = null;

            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Errors));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Warnings));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Info));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Verbose));
        }

        [Test]
        public void ShouldLog_WithAllEnabled_AllLevelsPass()
        {
            ProtoLogger.Settings.globalLogLevel = LogLevel.All;
            ProtoLogger.Settings.enabledCategories = LogCategory.All;

            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Errors));
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Warnings));
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Info));
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Verbose));
        }

        [Test]
        public void ShouldLog_WithNoneLevel_NothingPasses()
        {
            ProtoLogger.Settings.globalLogLevel = LogLevel.None;

            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Errors));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Warnings));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Info));
        }

        [Test]
        public void ShouldLog_OnlyErrorsEnabled_OnlyErrorsPass()
        {
            ProtoLogger.Settings.globalLogLevel = LogLevel.Errors;

            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Errors));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Warnings));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Info));
        }

        [Test]
        public void ShouldLog_ErrorsAndWarningsEnabled()
        {
            ProtoLogger.Settings.globalLogLevel = LogLevel.Errors | LogLevel.Warnings;

            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Errors));
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Warnings));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Info));
        }

        #endregion

        #region Category Tests

        [Test]
        public void ShouldLog_WithCategoryDisabled_InfoBlocked()
        {
            ProtoLogger.Settings.enabledCategories = LogCategory.Runtime; // Only Runtime enabled

            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Initialization, LogLevel.Info));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Dependencies, LogLevel.Info));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Events, LogLevel.Info));
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Info));
        }

        [Test]
        public void ShouldLog_ErrorsBypassCategoryFilter()
        {
            ProtoLogger.Settings.enabledCategories = LogCategory.None; // All categories disabled

            // Errors should pass regardless of category
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Initialization, LogLevel.Errors));
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Dependencies, LogLevel.Errors));
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Events, LogLevel.Errors));
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Errors));
        }

        [Test]
        public void ShouldLog_WarningsRespectCategoryFilter()
        {
            ProtoLogger.Settings.enabledCategories = LogCategory.Runtime; // Only Runtime enabled

            // Warnings should respect category filter (unlike Errors)
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Initialization, LogLevel.Warnings));
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Warnings));
        }

        [Test]
        public void ShouldLog_OnlyInitializationEnabled()
        {
            ProtoLogger.Settings.enabledCategories = LogCategory.Initialization;

            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Initialization, LogLevel.Info));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Dependencies, LogLevel.Info));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Events, LogLevel.Info));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Info));
        }

        [Test]
        public void ShouldLog_MultipleCategoriesEnabled()
        {
            ProtoLogger.Settings.enabledCategories = LogCategory.Initialization | LogCategory.Events;

            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Initialization, LogLevel.Info));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Dependencies, LogLevel.Info));
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Events, LogLevel.Info));
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Info));
        }

        #endregion

        #region Per-System Override Tests

        [Test]
        public void ShouldLog_PerSystemOverride_Level()
        {
            ProtoLogger.Settings.globalLogLevel = LogLevel.All;
            ProtoLogger.Settings.SetOverride("test_system", LogLevel.Errors, LogCategory.All, false);

            // test_system should only log errors
            Assert.IsTrue(ProtoLogger.ShouldLog("test_system", LogCategory.Runtime, LogLevel.Errors));
            Assert.IsFalse(ProtoLogger.ShouldLog("test_system", LogCategory.Runtime, LogLevel.Info));

            // other_system should use global (All)
            Assert.IsTrue(ProtoLogger.ShouldLog("other_system", LogCategory.Runtime, LogLevel.Info));
        }

        [Test]
        public void ShouldLog_PerSystemOverride_Category()
        {
            ProtoLogger.Settings.enabledCategories = LogCategory.All;
            ProtoLogger.Settings.SetOverride("test_system", LogLevel.All, LogCategory.Runtime, false);

            // test_system should only log Runtime category
            Assert.IsFalse(ProtoLogger.ShouldLog("test_system", LogCategory.Initialization, LogLevel.Info));
            Assert.IsTrue(ProtoLogger.ShouldLog("test_system", LogCategory.Runtime, LogLevel.Info));

            // other_system should use global (All categories)
            Assert.IsTrue(ProtoLogger.ShouldLog("other_system", LogCategory.Initialization, LogLevel.Info));
        }

        [Test]
        public void ShouldLog_PerSystemOverride_ErrorsBypassCategory()
        {
            ProtoLogger.Settings.SetOverride("test_system", LogLevel.All, LogCategory.Runtime, false);

            // Errors should bypass category filter even with per-system override
            Assert.IsTrue(ProtoLogger.ShouldLog("test_system", LogCategory.Initialization, LogLevel.Errors));
        }

        [Test]
        public void ShouldLog_PerSystemOverride_NoneDisablesAll()
        {
            ProtoLogger.Settings.globalLogLevel = LogLevel.All;
            ProtoLogger.Settings.SetOverride("test_system", LogLevel.None, LogCategory.All, false);

            // test_system should log nothing (even errors when level is None)
            Assert.IsFalse(ProtoLogger.ShouldLog("test_system", LogCategory.Runtime, LogLevel.Errors));
            Assert.IsFalse(ProtoLogger.ShouldLog("test_system", LogCategory.Runtime, LogLevel.Info));
        }

        [Test]
        public void ShouldLog_PerSystemOverride_UseGlobalTrue()
        {
            ProtoLogger.Settings.globalLogLevel = LogLevel.Errors;
            ProtoLogger.Settings.enabledCategories = LogCategory.Runtime;
            
            // Override with useGlobal=true should use global settings
            ProtoLogger.Settings.SetOverride("test_system", LogLevel.All, LogCategory.All, true);

            // Should use global level (Errors only)
            Assert.IsTrue(ProtoLogger.ShouldLog("test_system", LogCategory.Runtime, LogLevel.Errors));
            Assert.IsFalse(ProtoLogger.ShouldLog("test_system", LogCategory.Runtime, LogLevel.Info));
        }

        #endregion

        #region System Filter Tests

        [Test]
        public void ShouldLog_WhitelistMode_OnlyAllowedSystems()
        {
            ProtoLogger.Settings.filterMode = SystemFilterMode.Whitelist;
            ProtoLogger.Settings.filteredSystems.Add("allowed_system");

            Assert.IsTrue(ProtoLogger.ShouldLog("allowed_system", LogCategory.Runtime, LogLevel.Info));
            Assert.IsFalse(ProtoLogger.ShouldLog("blocked_system", LogCategory.Runtime, LogLevel.Info));
        }

        [Test]
        public void ShouldLog_BlacklistMode_BlocksListedSystems()
        {
            ProtoLogger.Settings.filterMode = SystemFilterMode.Blacklist;
            ProtoLogger.Settings.filteredSystems.Add("blocked_system");

            Assert.IsTrue(ProtoLogger.ShouldLog("allowed_system", LogCategory.Runtime, LogLevel.Info));
            Assert.IsFalse(ProtoLogger.ShouldLog("blocked_system", LogCategory.Runtime, LogLevel.Info));
        }

        [Test]
        public void ShouldLog_WhitelistMode_ErrorsBypassFilter()
        {
            ProtoLogger.Settings.filterMode = SystemFilterMode.Whitelist;
            ProtoLogger.Settings.filteredSystems.Add("allowed_system");

            // Errors should bypass system filter
            Assert.IsTrue(ProtoLogger.ShouldLog("blocked_system", LogCategory.Runtime, LogLevel.Errors));
        }

        #endregion

        #region Combined Filter Tests

        [Test]
        public void ShouldLog_CombinedFilters_MustPassBothCategoryAndLevel()
        {
            ProtoLogger.Settings.globalLogLevel = LogLevel.Errors | LogLevel.Info; // No Warnings
            ProtoLogger.Settings.enabledCategories = LogCategory.Initialization | LogCategory.Runtime; // No Dep, Events

            // Must pass both: level AND category
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Initialization, LogLevel.Info)); // Both OK
            Assert.IsTrue(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Errors)); // Both OK
            
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Initialization, LogLevel.Warnings)); // Level blocked
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Dependencies, LogLevel.Info)); // Category blocked
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Events, LogLevel.Warnings)); // Both blocked
        }

        [Test]
        public void ShouldLog_ErrorsOnlyBypassCategory_NotLevel()
        {
            ProtoLogger.Settings.globalLogLevel = LogLevel.Info; // Errors disabled at level
            ProtoLogger.Settings.enabledCategories = LogCategory.None;

            // Errors bypass category but still need level check
            Assert.IsFalse(ProtoLogger.ShouldLog("test", LogCategory.Runtime, LogLevel.Errors));
        }

        #endregion
    }
}
