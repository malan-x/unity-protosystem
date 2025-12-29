// Packages/com.protosystem.core/Runtime/Settings/Data/AudioSettings.cs
using UnityEngine;

namespace ProtoSystem.Settings
{
    /// <summary>
    /// Секция аудио настроек
    /// </summary>
    public class AudioSettings : SettingsSection
    {
        public override string SectionName => "Audio";
        public override string SectionComment => "Audio volume settings (0.0 - 1.0)";

        /// <summary>Общая громкость (0-1)</summary>
        public SettingValue<float> MasterVolume { get; }
        
        /// <summary>Громкость музыки (0-1)</summary>
        public SettingValue<float> MusicVolume { get; }
        
        /// <summary>Громкость звуковых эффектов (0-1)</summary>
        public SettingValue<float> SFXVolume { get; }
        
        /// <summary>Громкость голоса (0-1)</summary>
        public SettingValue<float> VoiceVolume { get; }
        
        /// <summary>Отключить весь звук</summary>
        public SettingValue<bool> Mute { get; }

        public AudioSettings()
        {
            MasterVolume = new SettingValue<float>(
                "MasterVolume", SectionName,
                "Master volume (0.0 - 1.0)",
                EventBus.Settings.Audio.MasterChanged,
                1.0f
            );

            MusicVolume = new SettingValue<float>(
                "MusicVolume", SectionName,
                "Music volume (0.0 - 1.0)",
                EventBus.Settings.Audio.MusicChanged,
                0.8f
            );

            SFXVolume = new SettingValue<float>(
                "SFXVolume", SectionName,
                "Sound effects volume (0.0 - 1.0)",
                EventBus.Settings.Audio.SFXChanged,
                1.0f
            );

            VoiceVolume = new SettingValue<float>(
                "VoiceVolume", SectionName,
                "Voice/dialogue volume (0.0 - 1.0)",
                EventBus.Settings.Audio.VoiceChanged,
                1.0f
            );

            Mute = new SettingValue<bool>(
                "Mute", SectionName,
                "Mute all audio (0/1)",
                EventBus.Settings.Audio.MuteChanged,
                false
            );
        }

        /// <summary>
        /// Применить аудио настройки
        /// </summary>
        public override void Apply()
        {
            // Применяем Mute через AudioListener
            AudioListener.volume = Mute.Value ? 0f : MasterVolume.Value;
            
            // Остальные настройки применяются через события
            // Проект подписывается на EventBus.Settings.Audio.* и управляет AudioMixer
        }

        /// <summary>
        /// Установить значения по умолчанию из конфига
        /// </summary>
        public void SetDefaults(float master, float music, float sfx, float voice)
        {
            MasterVolume.SetDefaultValue(master);
            MusicVolume.SetDefaultValue(music);
            SFXVolume.SetDefaultValue(sfx);
            VoiceVolume.SetDefaultValue(voice);
        }
    }
}
