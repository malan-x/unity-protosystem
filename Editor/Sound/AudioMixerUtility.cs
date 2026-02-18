// Packages/com.protosystem.core/Editor/Sound/AudioMixerUtility.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using UnityEditor;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Информация об exposed параметре AudioMixer
    /// </summary>
    [Serializable]
    public class ExposedAudioParameter
    {
        public string name;           // Имя параметра в миксере (MasterVolume)
        public string displayName;    // Отображаемое имя (Громкость)
        public bool enabled = true;   // Включён для генерации
        public float defaultValue = 0.8f;
        
        public ExposedAudioParameter() { }
        
        public ExposedAudioParameter(string name, string displayName = null)
        {
            this.name = name;
            this.displayName = displayName ?? GetDefaultDisplayName(name);
        }
        
        /// <summary>
        /// Получить отображаемое имя по умолчанию
        /// </summary>
        public static string GetDefaultDisplayName(string paramName)
        {
            return paramName switch
            {
                "MasterVolume" => UIKeys.Settings.Fallback.MasterVolume,
                "MusicVolume" => UIKeys.Settings.Fallback.MusicVolume,
                "SFXVolume" => UIKeys.Settings.Fallback.SfxVolume,
                "VoiceVolume" => UIKeys.Settings.Fallback.VoiceVolume,
                "AmbientVolume" => UIKeys.Settings.Fallback.AmbientVolume,
                "UIVolume" => UIKeys.Settings.Fallback.UIVolume,
                _ => paramName.Replace("Volume", "").Trim()
            };
        }
    }
    
    /// <summary>
    /// Утилита для работы с AudioMixer в Editor
    /// </summary>
    public static class AudioMixerUtility
    {
        /// <summary>
        /// Получить список exposed параметров из AudioMixer
        /// </summary>
        public static List<ExposedAudioParameter> GetExposedParameters(AudioMixer mixer)
        {
            var result = new List<ExposedAudioParameter>();
            
            if (mixer == null) return result;
            
            // Получаем через рефлексию (Unity не предоставляет публичный API)
            var exposedParams = GetExposedParametersInternal(mixer);
            
            foreach (var paramName in exposedParams)
            {
                // Фильтруем только параметры громкости
                if (paramName.EndsWith("Volume", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new ExposedAudioParameter(paramName));
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Получить все exposed параметры (включая не-Volume)
        /// </summary>
        public static List<string> GetAllExposedParameters(AudioMixer mixer)
        {
            if (mixer == null) return new List<string>();
            return GetExposedParametersInternal(mixer);
        }
        
        /// <summary>
        /// Внутренний метод получения exposed параметров через рефлексию
        /// </summary>
        private static List<string> GetExposedParametersInternal(AudioMixer mixer)
        {
            var result = new List<string>();
            
            try
            {
                // Способ 1: Через SerializedObject
                var serializedMixer = new SerializedObject(mixer);
                var exposedParams = serializedMixer.FindProperty("m_ExposedParameters");
                
                if (exposedParams != null && exposedParams.isArray)
                {
                    for (int i = 0; i < exposedParams.arraySize; i++)
                    {
                        var param = exposedParams.GetArrayElementAtIndex(i);
                        var nameProp = param.FindPropertyRelative("name");
                        if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
                        {
                            result.Add(nameProp.stringValue);
                        }
                    }
                }
                
                // Если SerializedObject не дал результатов, пробуем известные имена
                if (result.Count == 0)
                {
                    result = TryKnownParameters(mixer);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioMixerUtility] Failed to get exposed parameters: {e.Message}");
                result = TryKnownParameters(mixer);
            }
            
            return result;
        }
        
        /// <summary>
        /// Проверить известные имена параметров
        /// </summary>
        private static List<string> TryKnownParameters(AudioMixer mixer)
        {
            var result = new List<string>();
            
            string[] knownParams = 
            {
                "MasterVolume",
                "MusicVolume",
                "SFXVolume",
                "VoiceVolume",
                "AmbientVolume",
                "UIVolume"
            };
            
            foreach (var param in knownParams)
            {
                if (mixer.GetFloat(param, out _))
                {
                    result.Add(param);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Проверить существует ли параметр
        /// </summary>
        public static bool HasParameter(AudioMixer mixer, string paramName)
        {
            if (mixer == null) return false;
            return mixer.GetFloat(paramName, out _);
        }
        
        /// <summary>
        /// Конвертировать линейную громкость (0-1) в децибелы для AudioMixer
        /// </summary>
        public static float LinearToDecibel(float linear)
        {
            if (linear <= 0.0001f) return -80f;
            return Mathf.Log10(linear) * 20f;
        }
        
        /// <summary>
        /// Конвертировать децибелы в линейную громкость (0-1)
        /// </summary>
        public static float DecibelToLinear(float decibel)
        {
            if (decibel <= -80f) return 0f;
            return Mathf.Pow(10f, decibel / 20f);
        }
    }
}
