// Packages/com.protosystem.core/Editor/Settings/SettingsConfigEditor.cs
using UnityEngine;
using UnityEditor;
using System.IO;

namespace ProtoSystem.Settings
{
    [CustomEditor(typeof(SettingsConfig))]
    public class SettingsConfigEditor : UnityEditor.Editor
    {
        private static readonly Color FileExistsColor = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color FileNotExistsColor = new Color(0.8f, 0.4f, 0.2f);
        
        public override void OnInspectorGUI()
        {
            var config = (SettingsConfig)target;

            DrawPersistenceInfo(config);
            
            EditorGUILayout.Space(10);
            
            DrawDefaultInspector();
        }

        private void DrawPersistenceInfo(SettingsConfig config)
        {
            // Определяем фактический режим
            PersistenceMode actualMode = GetActualMode(config.persistenceMode);
            bool isFileMode = actualMode == PersistenceMode.File;
            
            // Заголовок секции
            EditorGUILayout.LabelField("Хранилище данных", EditorStyles.boldLabel);
            
            // Показываем режим
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Режим:", GUILayout.Width(60));
            
            string modeText = config.persistenceMode == PersistenceMode.Auto 
                ? $"Auto → {actualMode}" 
                : actualMode.ToString();
            EditorGUILayout.LabelField(modeText, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            if (isFileMode)
            {
                DrawFileInfo(config);
            }
            else
            {
                DrawPlayerPrefsInfo();
            }
        }

        private void DrawFileInfo(SettingsConfig config)
        {
            string filePath = Path.Combine(Application.persistentDataPath, config.fileName);
            bool fileExists = File.Exists(filePath);
            
            // Статус файла
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Статус:", GUILayout.Width(60));
            
            var prevColor = GUI.color;
            GUI.color = fileExists ? FileExistsColor : FileNotExistsColor;
            EditorGUILayout.LabelField(fileExists ? "✓ Файл существует" : "✗ Файл не создан", EditorStyles.boldLabel);
            GUI.color = prevColor;
            EditorGUILayout.EndHorizontal();
            
            // Путь к файлу
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Путь:", GUILayout.Width(60));
            EditorGUILayout.SelectableLabel(filePath, EditorStyles.miniLabel, GUILayout.Height(16));
            EditorGUILayout.EndHorizontal();
            
            if (fileExists)
            {
                var fileInfo = new FileInfo(filePath);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Размер:", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{fileInfo.Length} байт, изменён: {fileInfo.LastWriteTime:g}");
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.Space(5);
            
            // Кнопки
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button(fileExists ? "📂 Открыть в проводнике" : "📂 Открыть папку", GUILayout.Height(24)))
            {
                if (fileExists)
                {
                    EditorUtility.RevealInFinder(filePath);
                }
                else
                {
                    string folderPath = Application.persistentDataPath;
                    if (Directory.Exists(folderPath))
                        EditorUtility.RevealInFinder(folderPath);
                    else
                        EditorUtility.DisplayDialog("Папка не найдена", 
                            $"Папка данных ещё не создана:\n{folderPath}\n\nОна создастся при первом сохранении.", "OK");
                }
            }
            
            // Кнопка удаления
            GUI.enabled = fileExists;
            if (GUILayout.Button("🗑 Удалить файл", GUILayout.Height(24), GUILayout.Width(120)))
            {
                if (EditorUtility.DisplayDialog("Удалить настройки?", 
                    $"Удалить файл настроек?\n{filePath}\n\nЭто действие нельзя отменить.", "Удалить", "Отмена"))
                {
                    try
                    {
                        File.Delete(filePath);
                        Debug.Log($"[SettingsConfig] Deleted: {filePath}");
                    }
                    catch (System.Exception ex)
                    {
                        EditorUtility.DisplayDialog("Ошибка", $"Не удалось удалить файл:\n{ex.Message}", "OK");
                    }
                }
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            // Runtime кнопки
            if (Application.isPlaying && SettingsSystem.Instance != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("💾 Сохранить сейчас", GUILayout.Height(24)))
                {
                    SettingsSystem.Instance.Save();
                    Debug.Log("[SettingsConfig] Settings saved via Editor button");
                }
                
                if (GUILayout.Button("🔄 Перезагрузить", GUILayout.Height(24)))
                {
                    SettingsSystem.Instance.Load();
                    Debug.Log("[SettingsConfig] Settings reloaded via Editor button");
                }
                
                EditorGUILayout.EndHorizontal();
            }
            else if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Файл настроек создаётся при первом вызове Save() в игре.\n" +
                    "Запустите игру и измените настройки, или вызовите SettingsSystem.Instance.Save().", 
                    MessageType.Info);
            }
        }

        private void DrawPlayerPrefsInfo()
        {
            bool dataExists = PlayerPrefs.HasKey("ProtoSettings_Sections");
            
            // Статус
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Статус:", GUILayout.Width(60));
            
            var prevColor = GUI.color;
            GUI.color = dataExists ? FileExistsColor : FileNotExistsColor;
            EditorGUILayout.LabelField(dataExists ? "✓ Данные сохранены" : "✗ Данные отсутствуют", EditorStyles.boldLabel);
            GUI.color = prevColor;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Кнопка удаления
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = dataExists;
            if (GUILayout.Button("🗑 Удалить из PlayerPrefs", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("Удалить настройки?", 
                    "Удалить все настройки из PlayerPrefs?\n\nЭто действие нельзя отменить.", "Удалить", "Отмена"))
                {
                    DeletePlayerPrefsSettings();
                    Debug.Log("[SettingsConfig] Deleted settings from PlayerPrefs");
                }
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            // Runtime кнопки
            if (Application.isPlaying && SettingsSystem.Instance != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("💾 Сохранить сейчас", GUILayout.Height(24)))
                {
                    SettingsSystem.Instance.Save();
                    Debug.Log("[SettingsConfig] Settings saved to PlayerPrefs via Editor button");
                }
                
                if (GUILayout.Button("🔄 Перезагрузить", GUILayout.Height(24)))
                {
                    SettingsSystem.Instance.Load();
                    Debug.Log("[SettingsConfig] Settings reloaded via Editor button");
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private PersistenceMode GetActualMode(PersistenceMode mode)
        {
            if (mode != PersistenceMode.Auto)
                return mode;
            
#if UNITY_WEBGL
            return PersistenceMode.PlayerPrefs;
#elif UNITY_IOS || UNITY_ANDROID
            return PersistenceMode.PlayerPrefs;
#else
            return PersistenceMode.File;
#endif
        }
        
        private void DeletePlayerPrefsSettings()
        {
            const string PREFIX = "ProtoSettings_";
            
            // Получаем список секций
            string sectionsJson = PlayerPrefs.GetString(PREFIX + "Sections", "");
            if (!string.IsNullOrEmpty(sectionsJson))
            {
                // Простой парсинг JSON массива
                var sections = ParseStringArray(sectionsJson);
                
                foreach (string sectionName in sections)
                {
                    string keysJson = PlayerPrefs.GetString($"{PREFIX}{sectionName}_Keys", "");
                    if (!string.IsNullOrEmpty(keysJson))
                    {
                        var keys = ParseStringArray(keysJson);
                        foreach (string key in keys)
                        {
                            PlayerPrefs.DeleteKey($"{PREFIX}{sectionName}_{key}");
                        }
                    }
                    PlayerPrefs.DeleteKey($"{PREFIX}{sectionName}_Keys");
                }
            }
            
            PlayerPrefs.DeleteKey(PREFIX + "Sections");
            PlayerPrefs.DeleteKey(PREFIX + "Version");
            PlayerPrefs.Save();
        }
        
        private string[] ParseStringArray(string json)
        {
            // Простой парсинг {"items":["a","b","c"]}
            try
            {
                int start = json.IndexOf('[');
                int end = json.LastIndexOf(']');
                if (start < 0 || end < 0) return new string[0];
                
                string content = json.Substring(start + 1, end - start - 1);
                if (string.IsNullOrWhiteSpace(content)) return new string[0];
                
                var items = new System.Collections.Generic.List<string>();
                foreach (var item in content.Split(','))
                {
                    string trimmed = item.Trim().Trim('"');
                    if (!string.IsNullOrEmpty(trimmed))
                        items.Add(trimmed);
                }
                return items.ToArray();
            }
            catch
            {
                return new string[0];
            }
        }
    }
}
