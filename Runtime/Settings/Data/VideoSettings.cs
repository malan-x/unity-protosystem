// Packages/com.protosystem.core/Runtime/Settings/Data/VideoSettings.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Секция видео настроек с автоприменением
    /// </summary>
    public class VideoSettings : SettingsSection
    {
        public override string SectionName => "Video";
        public override string SectionComment => "Display and graphics settings";

        /// <summary>Индекс монитора (0 = основной)</summary>
        public SettingValue<int> Monitor { get; }
        
        /// <summary>Разрешение в формате "1920x1080"</summary>
        public SettingValue<string> Resolution { get; }
        
        /// <summary>Режим окна: ExclusiveFullScreen, FullScreenWindow, Windowed</summary>
        public SettingValue<string> Fullscreen { get; }
        
        /// <summary>Вертикальная синхронизация</summary>
        public SettingValue<bool> VSync { get; }
        
        /// <summary>Уровень качества (индекс в QualitySettings)</summary>
        public SettingValue<int> Quality { get; }
        
        /// <summary>Целевой FPS (-1 = без ограничения)</summary>
        public SettingValue<int> TargetFrameRate { get; }

        public VideoSettings()
        {
            // Определяем значения по умолчанию
            string defaultResolution = GetDefaultResolution();
            int defaultQuality = GetDefaultQuality();

            Monitor = new SettingValue<int>(
                "Monitor", SectionName,
                "Monitor index (0 = primary)",
                EventBus.Settings.Video.MonitorChanged,
                0
            );

            Resolution = new SettingValue<string>(
                "Resolution", SectionName,
                "Screen resolution (WIDTHxHEIGHT)",
                EventBus.Settings.Video.ResolutionChanged,
                defaultResolution
            );

            Fullscreen = new SettingValue<string>(
                "Fullscreen", SectionName,
                "Window mode: ExclusiveFullScreen, FullScreenWindow, Windowed",
                EventBus.Settings.Video.FullscreenChanged,
                "FullScreenWindow"
            );

            VSync = new SettingValue<bool>(
                "VSync", SectionName,
                "Vertical synchronization (0/1)",
                EventBus.Settings.Video.VSyncChanged,
                true
            );

            Quality = new SettingValue<int>(
                "Quality", SectionName,
                $"Quality level (0-{QualitySettings.names.Length - 1})",
                EventBus.Settings.Video.QualityChanged,
                defaultQuality
            );

            TargetFrameRate = new SettingValue<int>(
                "TargetFrameRate", SectionName,
                "Target FPS (-1 = unlimited)",
                EventBus.Settings.Video.FrameRateChanged,
                -1
            );
        }

        /// <summary>
        /// Применить видео настройки
        /// </summary>
        public override void Apply()
        {
            // Применяем монитор
            if (Monitor.IsModified)
            {
                ApplyMonitor(Monitor.Value);
            }

            // Применяем разрешение и режим окна
            if (Resolution.IsModified || Fullscreen.IsModified)
            {
                var (width, height) = ParseResolution(Resolution.Value);
                var mode = ParseFullscreenMode(Fullscreen.Value);
                Screen.SetResolution(width, height, mode);
                Debug.Log($"[VideoSettings] Resolution set to {width}x{height} {mode}");
            }

            // VSync
            if (VSync.IsModified)
            {
                QualitySettings.vSyncCount = VSync.Value ? 1 : 0;
                Debug.Log($"[VideoSettings] VSync set to {VSync.Value}");
            }

            // Quality
            if (Quality.IsModified)
            {
                QualitySettings.SetQualityLevel(Quality.Value, true);
                Debug.Log($"[VideoSettings] Quality set to {QualitySettings.names[Quality.Value]}");
            }

            // FrameRate
            if (TargetFrameRate.IsModified)
            {
                Application.targetFrameRate = TargetFrameRate.Value;
                Debug.Log($"[VideoSettings] Target frame rate set to {TargetFrameRate.Value}");
            }
        }

        #region Helper Methods

        private string GetDefaultResolution()
        {
            if (Display.displays.Length > 0)
            {
                return $"{Display.displays[0].systemWidth}x{Display.displays[0].systemHeight}";
            }
            return "1920x1080";
        }

        private int GetDefaultQuality()
        {
            // Автоопределение качества по возможностям системы
            int qualityCount = QualitySettings.names.Length;
            if (qualityCount == 0) return 0;
            
            // Выбираем средний уровень по умолчанию
            return Mathf.Clamp(qualityCount / 2, 0, qualityCount - 1);
        }

        private (int width, int height) ParseResolution(string resolution)
        {
            if (string.IsNullOrEmpty(resolution))
                return (1920, 1080);

            string[] parts = resolution.Split('x');
            if (parts.Length == 2 && 
                int.TryParse(parts[0], out int width) && 
                int.TryParse(parts[1], out int height))
            {
                return (width, height);
            }

            return (1920, 1080);
        }

        private FullScreenMode ParseFullscreenMode(string mode)
        {
            return mode switch
            {
                "ExclusiveFullScreen" => FullScreenMode.ExclusiveFullScreen,
                "FullScreenWindow" => FullScreenMode.FullScreenWindow,
                "Windowed" => FullScreenMode.Windowed,
                "MaximizedWindow" => FullScreenMode.MaximizedWindow,
                // Поддержка старых названий
                "Full Screen" => FullScreenMode.ExclusiveFullScreen,
                "Borderless full screen" => FullScreenMode.FullScreenWindow,
                _ => FullScreenMode.FullScreenWindow
            };
        }

        private void ApplyMonitor(int monitorIndex)
        {
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            if (monitorIndex >= 0 && monitorIndex < Display.displays.Length)
            {
                // Примечание: полная смена монитора требует PlayerPrefs и перезапуска
                // или использования platform-specific API
                Debug.Log($"[VideoSettings] Monitor set to {monitorIndex}. May require restart.");
            }
#endif
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Получить список доступных разрешений
        /// </summary>
        public static string[] GetAvailableResolutions()
        {
            return Screen.resolutions
                .Select(r => $"{r.width}x{r.height}")
                .Distinct()
                .OrderByDescending(r => int.Parse(r.Split('x')[0]))
                .ToArray();
        }

        /// <summary>
        /// Получить список доступных мониторов
        /// </summary>
        public static string[] GetAvailableMonitors()
        {
            var monitors = new List<string>();
            for (int i = 0; i < Display.displays.Length; i++)
            {
                monitors.Add($"Monitor {i + 1}");
            }
            return monitors.ToArray();
        }

        /// <summary>
        /// Получить список режимов окна
        /// </summary>
        public static string[] GetFullscreenModes()
        {
            return new[]
            {
                "FullScreenWindow",
                "ExclusiveFullScreen",
                "Windowed"
            };
        }

        /// <summary>
        /// Получить список уровней качества
        /// </summary>
        public static string[] GetQualityLevels()
        {
            return QualitySettings.names;
        }

        #endregion

        /// <summary>
        /// Установить значения по умолчанию из конфига
        /// </summary>
        public void SetDefaults(FullScreenMode fullscreen, bool vsync, int targetFps, int quality)
        {
            Fullscreen.SetDefaultValue(fullscreen.ToString());
            VSync.SetDefaultValue(vsync);
            TargetFrameRate.SetDefaultValue(targetFps);
            if (quality >= 0)
                Quality.SetDefaultValue(quality);
        }
    }
}
