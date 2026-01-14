// Packages/com.protosystem.core/Editor/Settings/SettingsConfigEditor.cs
using UnityEngine;
using UnityEditor;
using System.IO;

namespace ProtoSystem.Settings
{
    [CustomEditor(typeof(SettingsConfig))]
    public class SettingsConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (SettingsConfig)target;

            // Кнопка открытия папки с файлом (только для File режима)
            bool isFileMode = config.persistenceMode == PersistenceMode.File ||
                              (config.persistenceMode == PersistenceMode.Auto && !Application.isConsolePlatform);

            if (isFileMode && !string.IsNullOrEmpty(config.fileName))
            {
                EditorGUILayout.BeginHorizontal();
                
                string filePath = Path.Combine(Application.persistentDataPath, config.fileName);
                bool fileExists = File.Exists(filePath);
                
                GUI.enabled = true;
                if (GUILayout.Button(fileExists ? "Открыть папку с файлом" : "Открыть папку данных", GUILayout.Height(24)))
                {
                    string folderPath = Application.persistentDataPath;
                    
                    if (fileExists)
                    {
                        // Открыть папку и выделить файл
                        EditorUtility.RevealInFinder(filePath);
                    }
                    else
                    {
                        // Просто открыть папку
                        if (Directory.Exists(folderPath))
                            EditorUtility.RevealInFinder(folderPath);
                        else
                            EditorUtility.DisplayDialog("Папка не найдена", 
                                $"Папка данных ещё не создана:\n{folderPath}", "OK");
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                // Путь к файлу
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Путь:", GUILayout.Width(40));
                EditorGUILayout.SelectableLabel(filePath, EditorStyles.miniLabel, GUILayout.Height(16));
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
            }

            DrawDefaultInspector();
        }
    }
}
