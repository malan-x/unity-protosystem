using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Property drawer для SoundId атрибута — показывает dropdown со списком звуков
    /// </summary>
    [CustomPropertyDrawer(typeof(SoundIdAttribute))]
    public class SoundIdDrawer : PropertyDrawer
    {
        private static SoundLibrary _cachedLibrary;
        private static string[] _cachedIds;
        private static string[] _cachedDisplayNames;
        private static double _lastCacheTime;
        private const double CACHE_DURATION = 5.0; // Обновлять кэш каждые 5 секунд
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            
            var attr = (SoundIdAttribute)attribute;
            
            // Получить список звуков
            var (ids, displayNames) = GetSoundIds(attr.FilterCategory);
            
            if (ids.Length == 0)
            {
                // Нет звуков — показать обычное поле
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            
            // Найти текущий индекс
            int currentIndex = System.Array.IndexOf(ids, property.stringValue);
            if (currentIndex < 0) currentIndex = 0;
            
            // Разделить позицию для dropdown и кнопки preview
            float previewWidth = attr.ShowPreview ? 24 : 0;
            Rect dropdownRect = new Rect(position.x, position.y, position.width - previewWidth - 2, position.height);
            Rect previewRect = new Rect(position.xMax - previewWidth, position.y, previewWidth, position.height);
            
            // Dropdown
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(dropdownRect, label.text, currentIndex, displayNames);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = ids[newIndex];
            }
            
            // Preview button
            if (attr.ShowPreview && Application.isPlaying)
            {
                if (GUI.Button(previewRect, "▶"))
                {
                    SoundManagerSystem.Play(property.stringValue);
                }
            }
        }
        
        private (string[] ids, string[] displayNames) GetSoundIds(SoundCategory? filterCategory)
        {
            // Проверить кэш
            if (_cachedIds != null && EditorApplication.timeSinceStartup - _lastCacheTime < CACHE_DURATION)
            {
                return (_cachedIds, _cachedDisplayNames);
            }
            
            // Найти SoundLibrary
            var library = FindSoundLibrary();
            if (library == null)
            {
                return (new[] { "(None)" }, new[] { "(No SoundLibrary found)" });
            }
            
            // Получить все ID
            library.Initialize();
            
            var ids = new List<string> { "" }; // Пустой элемент первым
            var displayNames = new List<string> { "(None)" };
            
            foreach (var id in library.GetAllIds())
            {
                var entry = library.Get(id);
                if (entry == null) continue;
                
                // Фильтр по категории
                if (filterCategory.HasValue && entry.category != filterCategory.Value)
                    continue;
                
                ids.Add(id);
                displayNames.Add($"{entry.category}/{id}");
            }
            
            // Кэшировать
            _cachedIds = ids.ToArray();
            _cachedDisplayNames = displayNames.ToArray();
            _lastCacheTime = EditorApplication.timeSinceStartup;
            
            return (_cachedIds, _cachedDisplayNames);
        }
        
        private SoundLibrary FindSoundLibrary()
        {
            if (_cachedLibrary != null) return _cachedLibrary;
            
            // Поиск в Resources
            _cachedLibrary = Resources.Load<SoundLibrary>("SoundLibrary");
            if (_cachedLibrary != null) return _cachedLibrary;
            
            // Поиск через SoundManagerConfig
            var configs = AssetDatabase.FindAssets("t:SoundManagerConfig");
            foreach (var guid in configs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<SoundManagerConfig>(path);
                if (config?.soundLibrary != null)
                {
                    _cachedLibrary = config.soundLibrary;
                    return _cachedLibrary;
                }
            }
            
            // Поиск напрямую
            var libraries = AssetDatabase.FindAssets("t:SoundLibrary");
            if (libraries.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(libraries[0]);
                _cachedLibrary = AssetDatabase.LoadAssetAtPath<SoundLibrary>(path);
            }
            
            return _cachedLibrary;
        }
        
        /// <summary>
        /// Сбросить кэш (вызывать при изменении библиотеки)
        /// </summary>
        public static void InvalidateCache()
        {
            _cachedLibrary = null;
            _cachedIds = null;
            _cachedDisplayNames = null;
        }
    }
}
