using UnityEditor;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Настройки метрик систем (EditorPrefs, project-scoped)
    /// </summary>
    public static class SystemMetricsSettings
    {
        // === Project-Scoped Key ===
        private static string _projectKey;
        
        private static string ProjectKey
        {
            get
            {
                if (string.IsNullOrEmpty(_projectKey))
                {
                    // Стабильный ключ на основе пути проекта
                    _projectKey = Application.dataPath.GetHashCode().ToString("X8");
                }
                return _projectKey;
            }
        }
        
        private static string Key(string name) => $"ProtoSystem_Metrics_{ProjectKey}_{name}";
        
        // === Настройки ===
        
        /// <summary>
        /// Показывать ли метрики в инспекторе
        /// </summary>
        public static bool ShowMetrics
        {
            get => EditorPrefs.GetBool(Key("ShowMetrics"), true);
            set => EditorPrefs.SetBool(Key("ShowMetrics"), value);
        }
        
        /// <summary>
        /// Порог LOC для "желтого" цвета (предупреждение)
        /// </summary>
        public static int LocWarningThreshold
        {
            get => EditorPrefs.GetInt(Key("LocWarning"), 200);
            set => EditorPrefs.SetInt(Key("LocWarning"), value);
        }
        
        /// <summary>
        /// Порог LOC для "красного" цвета (проблема)
        /// </summary>
        public static int LocErrorThreshold
        {
            get => EditorPrefs.GetInt(Key("LocError"), 500);
            set => EditorPrefs.SetInt(Key("LocError"), value);
        }
        
        /// <summary>
        /// Порог размера файла (KB) для "желтого" цвета
        /// </summary>
        public static float KbWarningThreshold
        {
            get => EditorPrefs.GetFloat(Key("KbWarning"), 10f);
            set => EditorPrefs.SetFloat(Key("KbWarning"), value);
        }
        
        /// <summary>
        /// Порог размера файла (KB) для "красного" цвета
        /// </summary>
        public static float KbErrorThreshold
        {
            get => EditorPrefs.GetFloat(Key("KbError"), 30f);
            set => EditorPrefs.SetFloat(Key("KbError"), value);
        }
        
        /// <summary>
        /// Порог числа методов для "желтого" цвета
        /// </summary>
        public static int MethodsWarningThreshold
        {
            get => EditorPrefs.GetInt(Key("MethodsWarning"), 15);
            set => EditorPrefs.SetInt(Key("MethodsWarning"), value);
        }
        
        /// <summary>
        /// Порог числа методов для "красного" цвета
        /// </summary>
        public static int MethodsErrorThreshold
        {
            get => EditorPrefs.GetInt(Key("MethodsError"), 30);
            set => EditorPrefs.SetInt(Key("MethodsError"), value);
        }
        
        /// <summary>
        /// Сбросить настройки к значениям по умолчанию
        /// </summary>
        public static void ResetToDefaults()
        {
            ShowMetrics = false;
            LocWarningThreshold = 200;
            LocErrorThreshold = 500;
            KbWarningThreshold = 10f;
            KbErrorThreshold = 30f;
            MethodsWarningThreshold = 15;
            MethodsErrorThreshold = 30;
        }
    }
    
    /// <summary>
    /// Окно настроек метрик систем
    /// </summary>
    public class SystemMetricsSettingsWindow : UnityEditor.EditorWindow
    {
        [MenuItem("ProtoSystem/Diagnostics/Настройки метрик систем", false, 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<SystemMetricsSettingsWindow>("Настройки метрик");
            window.minSize = new Vector2(350, 320);
            window.Show();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("📊 Настройки метрик систем", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);
            
            // Основная настройка
            EditorGUILayout.BeginVertical("Box");
            SystemMetricsSettings.ShowMetrics = EditorGUILayout.Toggle(
                new GUIContent("✅ Показывать метрики", "Отображать метрики в инспекторе SystemInitializationManager"),
                SystemMetricsSettings.ShowMetrics);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // Пороги LOC
            EditorGUILayout.LabelField("📝 Пороги LOC (строки кода)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("Box");
            
            SystemMetricsSettings.LocWarningThreshold = EditorGUILayout.IntField(
                new GUIContent("⚠️ Предупреждение", "LOC выше этого значения — жёлтый"),
                SystemMetricsSettings.LocWarningThreshold);
            
            SystemMetricsSettings.LocErrorThreshold = EditorGUILayout.IntField(
                new GUIContent("❌ Проблема", "LOC выше этого значения — красный"),
                SystemMetricsSettings.LocErrorThreshold);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            // Пороги KB
            EditorGUILayout.LabelField("💾 Пороги размера файла (KB)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("Box");
            
            SystemMetricsSettings.KbWarningThreshold = EditorGUILayout.FloatField(
                new GUIContent("⚠️ Предупреждение", "KB выше этого значения — жёлтый"),
                SystemMetricsSettings.KbWarningThreshold);
            
            SystemMetricsSettings.KbErrorThreshold = EditorGUILayout.FloatField(
                new GUIContent("❌ Проблема", "KB выше этого значения — красный"),
                SystemMetricsSettings.KbErrorThreshold);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            // Пороги методов
            EditorGUILayout.LabelField("🔧 Пороги числа методов", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("Box");
            
            SystemMetricsSettings.MethodsWarningThreshold = EditorGUILayout.IntField(
                new GUIContent("⚠️ Предупреждение", "Методов выше этого значения — жёлтый"),
                SystemMetricsSettings.MethodsWarningThreshold);
            
            SystemMetricsSettings.MethodsErrorThreshold = EditorGUILayout.IntField(
                new GUIContent("❌ Проблема", "Методов выше этого значения — красный"),
                SystemMetricsSettings.MethodsErrorThreshold);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(15);
            
            // Кнопки
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("🔄 Сбросить к умолчаниям", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Сброс настроек", 
                    "Сбросить все настройки метрик к значениям по умолчанию?", "Да", "Отмена"))
                {
                    SystemMetricsSettings.ResetToDefaults();
                }
            }
            
            if (GUILayout.Button("🔄 Пересчитать метрики", GUILayout.Height(25)))
            {
                SystemMetricsCache.MarkDirty();
                Debug.Log("📊 Метрики будут пересчитаны при следующей отрисовке инспектора");
            }
            
            EditorGUILayout.EndHorizontal();
        }
    }
}
