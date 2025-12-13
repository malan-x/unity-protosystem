using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Данные метрик для одной системы
    /// </summary>
    public struct SystemMetricsData
    {
        public bool IsValid;
        public string ScriptPath;
        public int LinesOfCode;      // LOC без комментариев
        public float FileSizeKB;
        public int MethodCount;      // Объявленные методы (DeclaredOnly)
        public string TypeName;
        
        public static SystemMetricsData Invalid => new SystemMetricsData { IsValid = false };
    }
    
    /// <summary>
    /// Кэш метрик систем с поддержкой пересчёта после перекомпиляции
    /// </summary>
    public static class SystemMetricsCache
    {
        private static Dictionary<string, SystemMetricsData> _cache = new Dictionary<string, SystemMetricsData>();
        private static bool _isDirty = true;
        
        // Кэш маппинга Type -> MonoScript path
        private static Dictionary<Type, string> _typeToScriptPath;
        private static bool _typeMapBuilt = false;
        
        /// <summary>
        /// Пометить кэш как "грязный" — пересчитать при следующем запросе
        /// </summary>
        public static void MarkDirty()
        {
            _isDirty = true;
            _cache.Clear();
            _typeToScriptPath = null;
            _typeMapBuilt = false;
        }
        
        /// <summary>
        /// Проверить, нужен ли пересчёт
        /// </summary>
        public static bool IsDirty => _isDirty;
        
        /// <summary>
        /// Сбросить флаг dirty после пересчёта
        /// </summary>
        public static void ClearDirty()
        {
            _isDirty = false;
        }
        
        /// <summary>
        /// Получить метрики для системы (из кэша или вычислить)
        /// </summary>
        public static SystemMetricsData GetMetrics(SystemEntry entry)
        {
            if (entry == null) return SystemMetricsData.Invalid;
            
            string key = GetCacheKey(entry);
            if (string.IsNullOrEmpty(key)) return SystemMetricsData.Invalid;
            
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }
            
            var metrics = ComputeMetrics(entry);
            _cache[key] = metrics;
            return metrics;
        }
        
        /// <summary>
        /// Очистить весь кэш
        /// </summary>
        public static void Clear()
        {
            _cache.Clear();
        }
        
        // === Private ===
        
        private static string GetCacheKey(SystemEntry entry)
        {
            if (entry.useExistingObject && entry.ExistingSystemObject != null)
            {
                return entry.ExistingSystemObject.GetType().FullName;
            }
            else if (!string.IsNullOrEmpty(entry.systemTypeName))
            {
                return entry.systemTypeName;
            }
            return null;
        }
        
        private static SystemMetricsData ComputeMetrics(SystemEntry entry)
        {
            Type systemType = ResolveSystemType(entry);
            if (systemType == null) return SystemMetricsData.Invalid;
            
            string scriptPath = GetScriptPath(systemType);
            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                return new SystemMetricsData
                {
                    IsValid = true,
                    TypeName = systemType.Name,
                    ScriptPath = null,
                    LinesOfCode = 0,
                    FileSizeKB = 0,
                    MethodCount = CountDeclaredMethods(systemType)
                };
            }
            
            // Размер файла
            var fileInfo = new FileInfo(scriptPath);
            float sizeKB = fileInfo.Length / 1024f;
            
            // LOC без комментариев
            string content = File.ReadAllText(scriptPath);
            int loc = CountLinesOfCode(content);
            
            // Методы
            int methodCount = CountDeclaredMethods(systemType);
            
            return new SystemMetricsData
            {
                IsValid = true,
                TypeName = systemType.Name,
                ScriptPath = scriptPath,
                LinesOfCode = loc,
                FileSizeKB = sizeKB,
                MethodCount = methodCount
            };
        }
        
        /// <summary>
        /// Определить Type системы из SystemEntry
        /// </summary>
        private static Type ResolveSystemType(SystemEntry entry)
        {
            // Используем свойство SystemType из SystemEntry — оно само учитывает selectedComponentIndex
            var systemType = entry.SystemType;
            if (systemType != null) return systemType;
            
            // Fallback: если SystemType null, пробуем найти по имени
            if (!string.IsNullOrEmpty(entry.systemTypeName))
            {
                return FindTypeByName(entry.systemTypeName);
            }
            
            return null;
        }
        
        /// <summary>
        /// Найти тип по полному имени во всех сборках
        /// </summary>
        private static Type FindTypeByName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            
            // Пробуем напрямую
            var type = Type.GetType(typeName);
            if (type != null) return type;
            
            // Ищем во всех сборках
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(typeName);
                    if (type != null) return type;
                    
                    // Пробуем только по имени класса (без namespace)
                    type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
                    if (type != null) return type;
                }
                catch
                {
                    // Игнорируем проблемные сборки
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Получить путь к скрипту по типу
        /// </summary>
        private static string GetScriptPath(Type type)
        {
            if (type == null) return null;
            
            // Сначала пробуем через MonoScript (работает для MonoBehaviour)
            if (typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                // Создаём временный инстанс? Нет, лучше искать через все скрипты
                var scripts = MonoImporter.GetAllRuntimeMonoScripts();
                foreach (var script in scripts)
                {
                    if (script != null && script.GetClass() == type)
                    {
                        return AssetDatabase.GetAssetPath(script);
                    }
                }
            }
            
            // Альтернатива: строим маппинг Type -> path один раз
            BuildTypeToScriptMap();
            if (_typeToScriptPath != null && _typeToScriptPath.TryGetValue(type, out var path))
            {
                return path;
            }
            
            return null;
        }
        
        /// <summary>
        /// Построить маппинг Type -> Script Path (однократно)
        /// </summary>
        private static void BuildTypeToScriptMap()
        {
            if (_typeMapBuilt) return;
            _typeMapBuilt = true;
            
            _typeToScriptPath = new Dictionary<Type, string>();
            
            var scripts = MonoImporter.GetAllRuntimeMonoScripts();
            foreach (var script in scripts)
            {
                if (script == null) continue;
                
                var scriptType = script.GetClass();
                if (scriptType != null && !_typeToScriptPath.ContainsKey(scriptType))
                {
                    string assetPath = AssetDatabase.GetAssetPath(script);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        // Конвертируем в абсолютный путь
                        string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
                        _typeToScriptPath[scriptType] = fullPath;
                    }
                }
            }
        }
        
        /// <summary>
        /// Подсчитать LOC без комментариев (простейший парсинг)
        /// </summary>
        private static int CountLinesOfCode(string content)
        {
            if (string.IsNullOrEmpty(content)) return 0;
            
            // 1. Убираем блочные комментарии /* ... */
            content = Regex.Replace(content, @"/\*.*?\*/", "", RegexOptions.Singleline);
            
            // 2. Убираем однострочные комментарии // ...
            content = Regex.Replace(content, @"//.*$", "", RegexOptions.Multiline);
            
            // 3. Считаем непустые строки
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int count = 0;
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    count++;
                }
            }
            
            return count;
        }
        
        /// <summary>
        /// Подсчитать объявленные методы (DeclaredOnly, без специальных)
        /// </summary>
        private static int CountDeclaredMethods(Type type)
        {
            if (type == null) return 0;
            
            try
            {
                var methods = type.GetMethods(
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                
                // Исключаем специальные методы (get_/set_/add_/remove_/op_)
                int count = 0;
                foreach (var method in methods)
                {
                    if (!method.IsSpecialName)
                    {
                        count++;
                    }
                }
                
                return count;
            }
            catch
            {
                return 0;
            }
        }
    }
    
    /// <summary>
    /// Хук для пересчёта метрик после перекомпиляции
    /// </summary>
    [InitializeOnLoad]
    public static class SystemMetricsRecompileHook
    {
        static SystemMetricsRecompileHook()
        {
            // После перезагрузки домена (после компиляции) помечаем метрики как dirty
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }
        
        private static void OnAfterAssemblyReload()
        {
            SystemMetricsCache.MarkDirty();
        }
    }
}
