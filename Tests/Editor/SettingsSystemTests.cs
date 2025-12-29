// Packages/com.protosystem.core/Tests/Editor/SettingsSystemTests.cs
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ProtoSystem.Settings;
using UnityEngine;

// Alias для разрешения конфликта с UnityEngine.AudioSettings
using ProtoAudioSettings = ProtoSystem.Settings.AudioSettings;

namespace ProtoSystem.Tests
{
    /// <summary>
    /// Юнит-тесты для системы настроек
    /// </summary>
    [TestFixture]
    public class SettingsSystemTests
    {
        private string _testFilePath;
        
        [SetUp]
        public void SetUp()
        {
            _testFilePath = Path.Combine(Application.temporaryCachePath, "test_settings.ini");
            
            // Удаляем тестовый файл если существует
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Очищаем после тестов
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
        }

        #region SettingValue Tests

        [Test]
        public void SettingValue_DefaultValue_IsCorrect()
        {
            var setting = new SettingValue<float>("Test", "Section", "Comment", 0, 0.5f);
            
            Assert.AreEqual(0.5f, setting.Value);
            Assert.AreEqual(0.5f, setting.DefaultValue);
            Assert.AreEqual(0.5f, setting.SavedValue);
        }

        [Test]
        public void SettingValue_ChangeValue_UpdatesValue()
        {
            var setting = new SettingValue<float>("Test", "Section", "Comment", 0, 0.5f);
            
            setting.Value = 0.8f;
            
            Assert.AreEqual(0.8f, setting.Value);
        }

        [Test]
        public void SettingValue_IsModified_TrueAfterChange()
        {
            var setting = new SettingValue<float>("Test", "Section", "Comment", 0, 0.5f);
            
            Assert.IsFalse(setting.IsModified);
            
            setting.Value = 0.8f;
            
            Assert.IsTrue(setting.IsModified);
        }

        [Test]
        public void SettingValue_MarkSaved_ClearsModified()
        {
            var setting = new SettingValue<float>("Test", "Section", "Comment", 0, 0.5f);
            setting.Value = 0.8f;
            
            setting.MarkSaved();
            
            Assert.IsFalse(setting.IsModified);
            Assert.AreEqual(0.8f, setting.SavedValue);
        }

        [Test]
        public void SettingValue_Revert_RestoresSavedValue()
        {
            var setting = new SettingValue<float>("Test", "Section", "Comment", 0, 0.5f);
            setting.Value = 0.8f;
            
            setting.Revert();
            
            Assert.AreEqual(0.5f, setting.Value);
            Assert.IsFalse(setting.IsModified);
        }

        [Test]
        public void SettingValue_ResetToDefault_RestoresDefaultValue()
        {
            var setting = new SettingValue<float>("Test", "Section", "Comment", 0, 0.5f);
            setting.Value = 0.8f;
            setting.MarkSaved();
            
            setting.ResetToDefault();
            
            Assert.AreEqual(0.5f, setting.Value);
        }

        [Test]
        public void SettingValue_Serialize_Float_CorrectFormat()
        {
            var setting = new SettingValue<float>("Test", "Section", "Comment", 0, 0.5f);
            
            string serialized = setting.Serialize();
            
            Assert.AreEqual("0.5", serialized);
        }

        [Test]
        public void SettingValue_Serialize_Bool_CorrectFormat()
        {
            var settingTrue = new SettingValue<bool>("Test", "Section", "Comment", 0, true);
            var settingFalse = new SettingValue<bool>("Test2", "Section", "Comment", 0, false);
            
            Assert.AreEqual("1", settingTrue.Serialize());
            Assert.AreEqual("0", settingFalse.Serialize());
        }

        [Test]
        public void SettingValue_Deserialize_Float_ParsesCorrectly()
        {
            var setting = new SettingValue<float>("Test", "Section", "Comment", 0, 0f);
            
            setting.Deserialize("0.75");
            
            Assert.AreEqual(0.75f, setting.Value, 0.001f);
        }

        [Test]
        public void SettingValue_Deserialize_Bool_ParsesCorrectly()
        {
            var setting = new SettingValue<bool>("Test", "Section", "Comment", 0, false);
            
            setting.Deserialize("1");
            Assert.IsTrue(setting.Value);
            
            setting.Deserialize("0");
            Assert.IsFalse(setting.Value);
        }

        [Test]
        public void SettingValue_Deserialize_InvalidValue_UsesDefault()
        {
            var setting = new SettingValue<float>("Test", "Section", "Comment", 0, 0.5f);
            
            setting.Deserialize("invalid");
            
            Assert.AreEqual(0.5f, setting.Value);
        }

        [Test]
        public void SettingValue_FloatEquality_UsesTolerance()
        {
            var setting = new SettingValue<float>("Test", "Section", "Comment", 0, 0.5f);
            
            // Очень маленькое изменение не должно считаться изменением
            setting.Value = 0.50001f;
            
            Assert.IsFalse(setting.IsModified);
        }

        #endregion

        #region AudioSettings Tests

        [Test]
        public void AudioSettings_DefaultValues_AreCorrect()
        {
            var audio = new ProtoAudioSettings();
            
            Assert.AreEqual(1.0f, audio.MasterVolume.Value);
            Assert.AreEqual(0.8f, audio.MusicVolume.Value);
            Assert.AreEqual(1.0f, audio.SFXVolume.Value);
            Assert.AreEqual(1.0f, audio.VoiceVolume.Value);
            Assert.IsFalse(audio.Mute.Value);
        }

        [Test]
        public void AudioSettings_SetDefaults_UpdatesDefaultValues()
        {
            var audio = new ProtoAudioSettings();
            
            audio.SetDefaults(0.5f, 0.6f, 0.7f, 0.8f);
            audio.ResetToDefaults();
            
            Assert.AreEqual(0.5f, audio.MasterVolume.Value);
            Assert.AreEqual(0.6f, audio.MusicVolume.Value);
            Assert.AreEqual(0.7f, audio.SFXVolume.Value);
            Assert.AreEqual(0.8f, audio.VoiceVolume.Value);
        }

        [Test]
        public void AudioSettings_HasUnsavedChanges_DetectsChanges()
        {
            var audio = new ProtoAudioSettings();
            
            Assert.IsFalse(audio.HasUnsavedChanges());
            
            audio.MasterVolume.Value = 0.3f;
            
            Assert.IsTrue(audio.HasUnsavedChanges());
        }

        [Test]
        public void AudioSettings_Serialize_AllKeys()
        {
            var audio = new ProtoAudioSettings();
            
            var serialized = audio.Serialize();
            
            Assert.IsTrue(serialized.ContainsKey("MasterVolume"));
            Assert.IsTrue(serialized.ContainsKey("MusicVolume"));
            Assert.IsTrue(serialized.ContainsKey("SFXVolume"));
            Assert.IsTrue(serialized.ContainsKey("VoiceVolume"));
            Assert.IsTrue(serialized.ContainsKey("Mute"));
        }

        [Test]
        public void AudioSettings_Deserialize_RestoresValues()
        {
            var audio = new ProtoAudioSettings();
            var data = new Dictionary<string, string>
            {
                { "MasterVolume", "0.3" },
                { "MusicVolume", "0.4" },
                { "SFXVolume", "0.5" },
                { "VoiceVolume", "0.6" },
                { "Mute", "1" }
            };
            
            audio.Deserialize(data);
            
            Assert.AreEqual(0.3f, audio.MasterVolume.Value, 0.001f);
            Assert.AreEqual(0.4f, audio.MusicVolume.Value, 0.001f);
            Assert.AreEqual(0.5f, audio.SFXVolume.Value, 0.001f);
            Assert.AreEqual(0.6f, audio.VoiceVolume.Value, 0.001f);
            Assert.IsTrue(audio.Mute.Value);
        }

        #endregion

        #region VideoSettings Tests

        [Test]
        public void VideoSettings_GetAvailableResolutions_ReturnsArray()
        {
            var resolutions = VideoSettings.GetAvailableResolutions();
            
            Assert.IsNotNull(resolutions);
            Assert.IsTrue(resolutions.Length > 0);
        }

        [Test]
        public void VideoSettings_GetQualityLevels_ReturnsArray()
        {
            var levels = VideoSettings.GetQualityLevels();
            
            Assert.IsNotNull(levels);
            Assert.IsTrue(levels.Length > 0);
        }

        [Test]
        public void VideoSettings_GetFullscreenModes_ReturnsCorrectModes()
        {
            var modes = VideoSettings.GetFullscreenModes();
            
            Assert.Contains("FullScreenWindow", modes);
            Assert.Contains("ExclusiveFullScreen", modes);
            Assert.Contains("Windowed", modes);
        }

        #endregion

        #region IniPersistence Tests

        [Test]
        public void IniPersistence_Exists_FalseWhenNoFile()
        {
            var persistence = new IniPersistence(_testFilePath);
            
            Assert.IsFalse(persistence.Exists());
        }

        [Test]
        public void IniPersistence_Save_CreatesFile()
        {
            var persistence = new IniPersistence(_testFilePath);
            var audio = new ProtoAudioSettings();
            
            persistence.Save(new SettingsSection[] { audio });
            
            Assert.IsTrue(File.Exists(_testFilePath));
        }

        [Test]
        public void IniPersistence_SaveAndLoad_PreservesData()
        {
            var persistence = new IniPersistence(_testFilePath);
            var audio = new ProtoAudioSettings();
            audio.MasterVolume.Value = 0.42f;
            audio.MasterVolume.MarkSaved();
            
            persistence.Save(new SettingsSection[] { audio });
            var loaded = persistence.Load();
            
            Assert.IsTrue(loaded.ContainsKey("Audio"));
            Assert.AreEqual("0.42", loaded["Audio"]["MasterVolume"]);
        }

        [Test]
        public void IniPersistence_Delete_RemovesFile()
        {
            var persistence = new IniPersistence(_testFilePath);
            var audio = new ProtoAudioSettings();
            persistence.Save(new SettingsSection[] { audio });
            
            persistence.Delete();
            
            Assert.IsFalse(File.Exists(_testFilePath));
        }

        [Test]
        public void IniPersistence_Load_IgnoresComments()
        {
            // Создаём INI файл с комментариями
            File.WriteAllText(_testFilePath, @"
; This is a comment
[Audio]
; Another comment
MasterVolume=0.5
");
            var persistence = new IniPersistence(_testFilePath);
            
            var loaded = persistence.Load();
            
            Assert.IsTrue(loaded.ContainsKey("Audio"));
            Assert.AreEqual("0.5", loaded["Audio"]["MasterVolume"]);
            Assert.IsFalse(loaded["Audio"].ContainsKey("; Another comment"));
        }

        [Test]
        public void IniPersistence_Load_HandlesEmptyLines()
        {
            File.WriteAllText(_testFilePath, @"
[Audio]

MasterVolume=0.5

[Video]
Resolution=1920x1080
");
            var persistence = new IniPersistence(_testFilePath);
            
            var loaded = persistence.Load();
            
            Assert.IsTrue(loaded.ContainsKey("Audio"));
            Assert.IsTrue(loaded.ContainsKey("Video"));
        }

        #endregion

        #region PlayerPrefsPersistence Tests

        [Test]
        public void PlayerPrefsPersistence_SaveAndLoad_PreservesData()
        {
            var persistence = new PlayerPrefsPersistence();
            var audio = new ProtoAudioSettings();
            audio.MasterVolume.Value = 0.33f;
            audio.MasterVolume.MarkSaved();
            
            persistence.Save(new SettingsSection[] { audio });
            var loaded = persistence.Load();
            
            Assert.IsTrue(loaded.ContainsKey("Audio"));
            Assert.AreEqual("0.33", loaded["Audio"]["MasterVolume"]);
            
            // Очистка
            persistence.Delete();
        }

        #endregion

        #region SettingsMigrator Tests

        [Test]
        public void SettingsMigrator_NoMigrationNeeded_ReturnsOriginalData()
        {
            var migrator = new SettingsMigrator();
            var data = new Dictionary<string, Dictionary<string, string>>
            {
                { "Audio", new Dictionary<string, string> { { "MasterVolume", "0.5" } } }
            };
            
            var result = migrator.Migrate(data, SettingsMigrator.CURRENT_VERSION);
            
            Assert.AreEqual(data, result);
        }

        [Test]
        public void SettingsMigrator_ExtractVersion_ReturnsZeroForMissingMeta()
        {
            var migrator = new SettingsMigrator();
            var data = new Dictionary<string, Dictionary<string, string>>();
            
            int version = migrator.ExtractVersion(data);
            
            Assert.AreEqual(0, version);
        }

        [Test]
        public void SettingsMigrator_ExtractVersion_ReturnsCorrectVersion()
        {
            var migrator = new SettingsMigrator();
            var data = new Dictionary<string, Dictionary<string, string>>
            {
                { "Meta", new Dictionary<string, string> { { "Version", "5" } } }
            };
            
            int version = migrator.ExtractVersion(data);
            
            Assert.AreEqual(5, version);
        }

        [Test]
        public void SettingsMigrator_RegisterMigration_ExecutesMigration()
        {
            var migrator = new SettingsMigrator();
            bool migrationCalled = false;
            
            // Регистрируем миграцию для версии CURRENT_VERSION
            // (в реальности это было бы для старой версии)
            migrator.RegisterMigration(1, data => {
                migrationCalled = true;
                return data;
            });
            
            var testData = new Dictionary<string, Dictionary<string, string>>();
            migrator.Migrate(testData, 0);
            
            Assert.IsTrue(migrationCalled);
        }

        #endregion

        #region DynamicSettingsSection Tests

        [Test]
        public void DynamicSettingsSection_AddSettings_AddsCorrectly()
        {
            var section = new DynamicSettingsSection("Custom", "Custom settings");
            
            section.AddString("Key1", "Comment1", 0, "default");
            section.AddInt("Key2", "Comment2", 0, 42);
            section.AddFloat("Key3", "Comment3", 0, 0.5f);
            section.AddBool("Key4", "Comment4", 0, true);
            
            var settings = section.GetAllSettings();
            Assert.AreEqual(4, System.Linq.Enumerable.Count(settings));
        }

        [Test]
        public void DynamicSettingsSection_Serialize_IncludesAllSettings()
        {
            var section = new DynamicSettingsSection("Custom", "Custom settings");
            section.AddString("StringKey", "", 0, "value");
            section.AddInt("IntKey", "", 0, 10);
            
            var serialized = section.Serialize();
            
            Assert.AreEqual("value", serialized["StringKey"]);
            Assert.AreEqual("10", serialized["IntKey"]);
        }

        #endregion

        #region Integration Tests

        [Test]
        public void Integration_FullCycle_SaveLoadRevert()
        {
            var persistence = new IniPersistence(_testFilePath);
            
            // Создаём секции
            var audio = new ProtoAudioSettings();
            var video = new VideoSettings();
            
            // Изменяем значения
            audio.MasterVolume.Value = 0.7f;
            video.VSync.Value = false;
            
            // Сохраняем
            audio.MarkAllSaved();
            video.MarkAllSaved();
            persistence.Save(new SettingsSection[] { audio, video });
            
            // Создаём новые секции и загружаем
            var audio2 = new ProtoAudioSettings();
            var video2 = new VideoSettings();
            
            var data = persistence.Load();
            audio2.Deserialize(data["Audio"]);
            video2.Deserialize(data["Video"]);
            
            // Проверяем
            Assert.AreEqual(0.7f, audio2.MasterVolume.Value, 0.001f);
            Assert.IsFalse(video2.VSync.Value);
        }

        #endregion
    }
}
