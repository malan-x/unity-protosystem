// Packages/com.protosystem.core/Editor/Localization/LocalizationConfigEditor.cs
using UnityEngine;
using UnityEditor;

#if PROTO_HAS_LOCALIZATION
using UnityEngine.Localization;
using UnityEditor.Localization;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Кастомный инспектор для LocalizationConfig.
    /// Показывает статус Unity Localization и Addressables.
    /// </summary>
    [CustomEditor(typeof(LocalizationConfig))]
    public class LocalizationConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawStatusBox();
            EditorGUILayout.Space(5);
            
            base.OnInspectorGUI();
            
            EditorGUILayout.Space(10);
            DrawActionButtons();
        }
        
        private void DrawStatusBox()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🌐 Localization Status", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            #if PROTO_HAS_LOCALIZATION
            DrawStatusLine("✓ Unity Localization", "Установлен", true);
            
            // Addressables
            bool addressablesBuilt = IsAddressablesBuilt();
            DrawStatusLine(
                addressablesBuilt ? "✓ Addressables" : "✗ Addressables",
                addressablesBuilt ? "Собраны" : "Не собраны!",
                addressablesBuilt);
            if (!addressablesBuilt)
            {
                EditorGUILayout.HelpBox(
                    "Addressables не собраны. Без этого Unity Localization не работает в Play Mode.\n" +
                    "Нажмите \"Build Addressables\" ниже.",
                    MessageType.Error);
            }
            
            // Locales (через editor API — не триггерит Addressables)
            var editorLocales = LocalizationEditorSettings.GetLocales();
            int localeCount = editorLocales?.Count ?? 0;
            if (localeCount > 0)
            {
                string names = "";
                for (int i = 0; i < editorLocales.Count; i++)
                {
                    if (i > 0) names += ", ";
                    names += editorLocales[i].Identifier.Code;
                }
                DrawStatusLine($"  ✓ Locale'и ({localeCount})", names, true);
            }
            else
            {
                DrawStatusLine("  ✗ Locale'и", "Не созданы", false);
                EditorGUILayout.HelpBox(
                    "Создайте Locale'и: Window → Asset Management → Localization Tables → New Locale",
                    MessageType.Warning);
            }
            
            // Localization Settings (через AssetDatabase — не триггерит Addressables)
            var settingsGuids = AssetDatabase.FindAssets("t:LocalizationSettings");
            bool hasSettings = settingsGuids.Length > 0;
            DrawStatusLine(
                hasSettings ? "✓ Localization Settings" : "✗ Localization Settings",
                hasSettings ? "Найден" : "Не найден",
                hasSettings);
            if (!hasSettings)
            {
                EditorGUILayout.HelpBox(
                    "Edit → Project Settings → Localization → Create",
                    MessageType.Warning);
            }
            
            // Проверка: Locale'и добавлены в Addressables?
            if (localeCount > 0 && addressablesBuilt)
            {
                bool localesAddressable = AreLocalesInAddressables(editorLocales);
                if (!localesAddressable)
                {
                    EditorGUILayout.HelpBox(
                        "Locale'и найдены, но не включены в Addressables build.\n" +
                        "Нужно пересобрать: нажмите \"Build Addressables\" ниже.",
                        MessageType.Warning);
                }
            }
            
            // String Tables
            DrawStringTablesStatus();
            
            #else
            DrawStatusLine("✗ Unity Localization", "Не установлен (fallback mode)", false);
            EditorGUILayout.HelpBox(
                "Установите com.unity.localization через Package Manager для полной поддержки.",
                MessageType.Warning);
            #endif
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatusLine(string label, string value, bool ok)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.color = ok ? new Color(0.5f, 0.9f, 0.5f) : new Color(1f, 0.6f, 0.4f);
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(180));
            GUI.color = Color.white;
            EditorGUILayout.LabelField(value, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }
        
        #if PROTO_HAS_LOCALIZATION
        private void DrawStringTablesStatus()
        {
            var config = (LocalizationConfig)target;
            
            foreach (var tableName in config.preloadTables)
            {
                bool exists = false;
                try
                {
                    var collection = UnityEditor.Localization.LocalizationEditorSettings
                        .GetStringTableCollection(tableName);
                    exists = collection != null;
                }
                catch { /* ignore */ }
                
                if (exists)
                    DrawStatusLine($"  ✓ Table \"{tableName}\"", "Найдена", true);
                else
                    DrawStatusLine($"  ✗ Table \"{tableName}\"", "Не найдена", false);
            }
        }
        #endif
        
        private void DrawActionButtons()
        {
            EditorGUILayout.LabelField("Действия", EditorStyles.boldLabel);
            
            #if PROTO_HAS_LOCALIZATION
            EditorGUILayout.BeginHorizontal();
            
            if (!IsAddressablesBuilt())
            {
                GUI.color = new Color(1f, 0.8f, 0.4f);
                if (GUILayout.Button("⚡ Build Addressables", GUILayout.Height(25)))
                {
                    BuildAddressables();
                }
                GUI.color = Color.white;
            }
            
            if (GUILayout.Button("📋 Localization Tables", GUILayout.Height(25)))
            {
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Localization Tables");
            }
            
            if (GUILayout.Button("📦 Addressables Groups", GUILayout.Height(25)))
            {
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);
            
            if (GUILayout.Button("🔄 AI Translation Window", GUILayout.Height(25)))
            {
                AITranslationWindow.ShowWindow();
            }
            #else
            EditorGUILayout.HelpBox(
                "Установите com.unity.localization для доступа к действиям.",
                MessageType.Info);
            #endif
        }
        
        private static bool IsAddressablesBuilt()
        {
            string aaPath = "Library/com.unity.addressables/aa/Windows";
            if (!System.IO.Directory.Exists(aaPath)) return false;
            return System.IO.Directory.GetFiles(aaPath, "catalog*").Length > 0;
        }
        
        #if PROTO_HAS_LOCALIZATION
        private static bool AreLocalesInAddressables(System.Collections.Generic.IReadOnlyList<Locale> locales)
        {
            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (aaSettings == null) return false;
            
            foreach (var locale in locales)
            {
                string path = AssetDatabase.GetAssetPath(locale);
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (aaSettings.FindAssetEntry(guid) != null)
                    return true; // Хотя бы один locale в Addressables
            }
            return false;
        }
        
        private static void BuildAddressables()
        {
            try
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null)
                {
                    settings = AddressableAssetSettings.Create(
                        AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                        AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                        true, true);
                    AddressableAssetSettingsDefaultObject.Settings = settings;
                    AssetDatabase.SaveAssets();
                }
                
                AddressableAssetSettings.BuildPlayerContent();
                Debug.Log("[ProtoLocalization] Addressables built successfully!");
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProtoLocalization] Addressables build failed: {e.Message}");
            }
        }
        #endif
    }
}
