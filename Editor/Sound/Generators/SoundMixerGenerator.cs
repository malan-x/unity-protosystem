using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Audio;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Генератор/копировщик AudioMixer из шаблона пакета
    /// </summary>
    public static class SoundMixerGenerator
    {
        private const string TEMPLATE_PATH = "Packages/com.protosystem.core/Runtime/Sound/Templates/ProtoSystemMixerTemplate.mixer";
        private const string DEFAULT_NAME = "MainAudioMixer";
        
        /// <summary>
        /// Создать AudioMixer копированием шаблона
        /// </summary>
        [MenuItem("ProtoSystem/Sound/Create Audio Mixer", priority = 100)]
        public static void CreateAudioMixer()
        {
            string savePath = EditorUtility.SaveFilePanelInProject(
                "Create Audio Mixer",
                DEFAULT_NAME,
                "mixer",
                "Choose location for the Audio Mixer",
                "Assets/Settings/Sound"
            );
            
            if (string.IsNullOrEmpty(savePath)) return;
            
            var mixer = CreateAudioMixerAt(savePath);
            if (mixer != null)
            {
                Selection.activeObject = mixer;
                EditorGUIUtility.PingObject(mixer);
            }
        }
        
        /// <summary>
        /// Создать AudioMixer по указанному пути (копирование шаблона)
        /// </summary>
        public static AudioMixer CreateAudioMixerAt(string targetPath)
        {
            // Проверить шаблон
            var template = AssetDatabase.LoadAssetAtPath<AudioMixer>(TEMPLATE_PATH);
            if (template == null)
            {
                Debug.LogError($"[SoundMixerGenerator] Template not found at: {TEMPLATE_PATH}");
                EditorUtility.DisplayDialog(
                    "Error",
                    "AudioMixer template not found in package.\nPlease reinstall ProtoSystem package.",
                    "OK"
                );
                return null;
            }
            
            // Создать директорию если нужно
            string directory = Path.GetDirectoryName(targetPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Удалить существующий если есть
            if (File.Exists(targetPath))
            {
                AssetDatabase.DeleteAsset(targetPath);
            }
            
            // Копировать шаблон
            bool success = AssetDatabase.CopyAsset(TEMPLATE_PATH, targetPath);
            
            if (!success)
            {
                Debug.LogError($"[SoundMixerGenerator] Failed to copy template to: {targetPath}");
                return null;
            }
            
            AssetDatabase.Refresh();
            
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(targetPath);
            
            if (mixer != null)
            {
                Debug.Log($"✅ AudioMixer created at: {targetPath}");
                Debug.Log("   Groups: Master, Music, SFX, Voice, Ambient, UI");
                Debug.Log("   Exposed parameters: MasterVolume, MusicVolume, SFXVolume, VoiceVolume, AmbientVolume, UIVolume");
            }
            
            return mixer;
        }
        
        /// <summary>
        /// Проверить настройку миксера
        /// </summary>
        public static bool ValidateMixer(AudioMixer mixer)
        {
            if (mixer == null) return false;
            
            string[] requiredParams = 
            {
                "MasterVolume",
                "MusicVolume", 
                "SFXVolume",
                "VoiceVolume",
                "AmbientVolume",
                "UIVolume"
            };
            
            bool valid = true;
            
            foreach (var param in requiredParams)
            {
                if (!mixer.GetFloat(param, out _))
                {
                    Debug.LogWarning($"[SoundMixerGenerator] Missing exposed parameter: {param}");
                    valid = false;
                }
            }
            
            return valid;
        }
        
        /// <summary>
        /// Получить группу миксера по имени
        /// </summary>
        public static AudioMixerGroup GetGroup(AudioMixer mixer, string groupName)
        {
            if (mixer == null) return null;
            
            var groups = mixer.FindMatchingGroups(groupName);
            return groups.Length > 0 ? groups[0] : null;
        }
        
        /// <summary>
        /// Получить группу по категории звука
        /// </summary>
        public static AudioMixerGroup GetGroupForCategory(AudioMixer mixer, SoundCategory category)
        {
            if (mixer == null) return null;
            
            string groupName = category switch
            {
                SoundCategory.Master => "Master",
                SoundCategory.Music => "Music",
                SoundCategory.SFX => "SFX",
                SoundCategory.Voice => "Voice",
                SoundCategory.Ambient => "Ambient",
                SoundCategory.UI => "UI",
                _ => "Master"
            };
            
            return GetGroup(mixer, groupName);
        }
    }
}
