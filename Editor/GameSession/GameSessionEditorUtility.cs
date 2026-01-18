// Packages/com.protosystem.core/Editor/GameSession/GameSessionEditorUtility.cs
using UnityEngine;
using UnityEditor;
using System.IO;

namespace ProtoSystem
{
    /// <summary>
    /// Editor утилиты для GameSessionSystem
    /// </summary>
    public static class GameSessionEditorUtility
    {
        private const string ConfigPath = "Assets/Resources/GameSessionConfig.asset";
        
        [MenuItem("ProtoSystem/Game Session/Create Config", false, 200)]
        public static void CreateConfig()
        {
            // Проверяем существование
            var existing = AssetDatabase.LoadAssetAtPath<GameSessionConfig>(ConfigPath);
            if (existing != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"[GameSession] Config already exists: {ConfigPath}");
                return;
            }
            
            // Создаём директорию Resources если нужно
            string directory = Path.GetDirectoryName(ConfigPath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }
            
            // Создаём конфиг
            var config = ScriptableObject.CreateInstance<GameSessionConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            
            Debug.Log($"[GameSession] Created config: {ConfigPath}");
        }
        
        [MenuItem("ProtoSystem/Game Session/Create Config", true)]
        public static bool CreateConfigValidate()
        {
            return !AssetDatabase.LoadAssetAtPath<GameSessionConfig>(ConfigPath);
        }
        
        [MenuItem("ProtoSystem/Game Session/Select Config", false, 201)]
        public static void SelectConfig()
        {
            var config = Resources.Load<GameSessionConfig>("GameSessionConfig");
            if (config != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
            else
            {
                Debug.LogWarning("[GameSession] Config not found. Create it via ProtoSystem/Game Session/Create Config");
            }
        }
        
        [MenuItem("ProtoSystem/Game Session/Documentation", false, 300)]
        public static void OpenDocumentation()
        {
            string docPath = "Packages/com.protosystem.core/Documentation~/GameSession.md";
            var doc = AssetDatabase.LoadAssetAtPath<TextAsset>(docPath);
            if (doc != null)
            {
                AssetDatabase.OpenAsset(doc);
            }
            else
            {
                Debug.Log("[GameSession] Documentation: See README.md for GameSession usage");
            }
        }
    }
}
