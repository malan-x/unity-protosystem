// Packages/com.protosystem.core/Runtime/Settings/Data/ControlsSettings.cs
namespace ProtoSystem.Settings
{
    /// <summary>
    /// Секция настроек управления
    /// </summary>
    public class ControlsSettings : SettingsSection
    {
        public override string SectionName => "Controls";
        public override string SectionComment => "Input and control settings";

        /// <summary>Чувствительность мыши/камеры (0.1 - 3.0)</summary>
        public SettingValue<float> Sensitivity { get; }
        
        /// <summary>Инвертировать ось Y</summary>
        public SettingValue<bool> InvertY { get; }
        
        /// <summary>Инвертировать ось X</summary>
        public SettingValue<bool> InvertX { get; }

        public ControlsSettings()
        {
            Sensitivity = new SettingValue<float>(
                "Sensitivity", SectionName,
                "Mouse/camera sensitivity (0.1 - 3.0)",
                EventBus.Settings.Controls.SensitivityChanged,
                1.0f
            );

            InvertY = new SettingValue<bool>(
                "InvertY", SectionName,
                "Invert Y axis (0/1)",
                EventBus.Settings.Controls.InvertYChanged,
                false
            );

            InvertX = new SettingValue<bool>(
                "InvertX", SectionName,
                "Invert X axis (0/1)",
                EventBus.Settings.Controls.InvertXChanged,
                false
            );
        }

        /// <summary>
        /// Установить значения по умолчанию из конфига
        /// </summary>
        public void SetDefaults(float sensitivity, bool invertY, bool invertX)
        {
            Sensitivity.SetDefaultValue(sensitivity);
            InvertY.SetDefaultValue(invertY);
            InvertX.SetDefaultValue(invertX);
        }
    }
}
