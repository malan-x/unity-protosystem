using UnityEditor;
using UnityEngine;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Автоматически показывает ProjectSetupWizard при первом запуске
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetupDetector
    {
        private const string SETUP_KEY = "ProtoSystem.FirstTimeSetup";
        
        static ProjectSetupDetector()
        {
            // Проверяем, был ли запущен setup ранее
            if (!EditorPrefs.HasKey(SETUP_KEY))
            {
                // Задержка, чтобы Unity полностью загрузился
                EditorApplication.delayCall += ShowSetupWizard;
            }
        }
        
        private static void ShowSetupWizard()
        {
            if (EditorUtility.DisplayDialog(
                "ProtoSystem Setup",
                "Welcome to ProtoSystem!\n\n" +
                "It looks like this is your first time using ProtoSystem in this project.\n" +
                "Would you like to run the Project Setup Wizard?",
                "Yes, Setup Now",
                "Skip"))
            {
                ProjectSetupWizard.ShowWindow();
            }
            
            // Отмечаем, что мы показали визард (даже если пользователь отказался)
            EditorPrefs.SetBool(SETUP_KEY, true);
        }
        
        [MenuItem("Tools/ProtoSystem/Reset First Time Setup", priority = 100)]
        private static void ResetFirstTimeSetup()
        {
            if (EditorUtility.DisplayDialog(
                "Reset Setup",
                "This will reset the first-time setup flag.\n" +
                "The setup wizard will appear again on next Unity restart.",
                "Reset",
                "Cancel"))
            {
                EditorPrefs.DeleteKey(SETUP_KEY);
                Debug.Log("✅ First-time setup flag has been reset.");
            }
        }
    }
}
