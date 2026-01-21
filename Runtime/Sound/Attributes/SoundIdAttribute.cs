using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Атрибут для отображения dropdown со списком звуков из SoundLibrary
    /// </summary>
    public class SoundIdAttribute : PropertyAttribute
    {
        /// <summary>
        /// Фильтр по категории (null = все категории)
        /// </summary>
        public SoundCategory? FilterCategory { get; }
        
        /// <summary>
        /// Показывать кнопку предпрослушивания
        /// </summary>
        public bool ShowPreview { get; }
        
        /// <summary>
        /// Атрибут без фильтра (все звуки)
        /// </summary>
        public SoundIdAttribute()
        {
            FilterCategory = null;
            ShowPreview = true;
        }
        
        /// <summary>
        /// Атрибут с фильтром по категории
        /// </summary>
        /// <param name="category">Категория звуков для отображения</param>
        public SoundIdAttribute(SoundCategory category)
        {
            FilterCategory = category;
            ShowPreview = true;
        }
        
        /// <summary>
        /// Атрибут с полной настройкой
        /// </summary>
        /// <param name="category">Категория звуков для отображения</param>
        /// <param name="showPreview">Показывать кнопку предпрослушивания</param>
        public SoundIdAttribute(SoundCategory category, bool showPreview)
        {
            FilterCategory = category;
            ShowPreview = showPreview;
        }
    }
}
