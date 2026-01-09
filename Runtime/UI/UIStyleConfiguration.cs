// Packages/com.protosystem.core/Runtime/UI/UIStyleConfiguration.cs
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Настройки стиля UI для генерации элементов интерфейса
    /// </summary>
    [CreateAssetMenu(fileName = "UIStyleConfig", menuName = "ProtoSystem/UI/Style Configuration", order = 100)]
    public class UIStyleConfiguration : ScriptableObject
    {
        [Header("=== Основные цвета ===")]
        [Tooltip("Основной цвет фона (окна, панели)")]
        public Color backgroundColor = new Color(0.098f, 0.098f, 0.137f, 1f); // rgba(25, 25, 35, 1) - непрозрачный
        
        [Tooltip("Цвет акцента (кнопки, слайдеры, прогресс-бары)")]
        public Color accentColor = new Color(0.29f, 0.62f, 1.0f, 1f); // #4A9EFF
        
        [Tooltip("Цвет текста")]
        public Color textColor = Color.white;
        
        [Tooltip("Цвет вторичного текста (labels, placeholders)")]
        public Color secondaryTextColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        
        [Tooltip("Цвет границ (по умолчанию белый полупрозрачный как в HTML)")]
        public Color borderColor = new Color(1f, 1f, 1f, 0.12f); // rgba(255, 255, 255, 0.12)

        [Header("=== Цвета элементов ===")]
        [Tooltip("Цвет фона элементов (input, dropdown)")]
        public Color elementBackgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.08f);
        
        [Tooltip("Цвет фона при наведении")]
        public Color elementHoverColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        
        [Tooltip("Цвет элемента в активном состоянии")]
        public Color elementActiveColor = new Color(0.2f, 0.2f, 0.25f, 1f);

        [Header("=== Размеры ===")]
        [Tooltip("Высота элементов управления (px)")]
        public int elementHeight = 32;
        
        [Tooltip("Размер шрифта (обычный текст)")]
        public int fontSize = 14;
        
        [Tooltip("Размер шрифта (заголовки)")]
        public int headerFontSize = 16;
        
        [Tooltip("Размер шрифта (секции)")]
        public int sectionFontSize = 12;
        
        [Tooltip("Отступ между элементами (px)")]
        public int spacing = 8;
        
        [Tooltip("Внутренние отступы элементов (px)")]
        public int padding = 12;

        [Header("=== Скругление углов ===")]
        [Tooltip("Радиус скругления для окон (px)")]
        public int windowBorderRadius = 16;
        
        [Tooltip("Радиус скругления для кнопок (px)")]
        public int buttonBorderRadius = 8;
        
        [Tooltip("Радиус скругления для input элементов (px)")]
        public int inputBorderRadius = 8;
        
        [Tooltip("Радиус скругления для dropdown (px)")]
        public int dropdownBorderRadius = 8;
        
        [Tooltip("Радиус скругления для checkbox (px)")]
        public int checkboxBorderRadius = 4;
        
        [Tooltip("Радиус скругления для слайдера (px) - 0 для автоматического (половина высоты)")]
        public int sliderBorderRadius = 0;

        [Header("=== Слайдер ===")]
        [Tooltip("Размер ручки слайдера (px) - должен быть квадратным")]
        public int sliderHandleSize = 18;
        
        [Tooltip("Высота трека слайдера (px) - для Modern стиля тоньше")]
        public int sliderTrackHeight = 6;
        
        [Tooltip("Высота трека прогресс-бара (px) - обычно толще слайдера")]
        public int progressBarTrackHeight = 10;
        
        [Tooltip("Цвет ручки слайдера")]
        public Color sliderHandleColor = Color.white;
        
        [Tooltip("Цвет фона трека")]
        public Color sliderTrackBackgroundColor = new Color(1f, 1f, 1f, 0.1f); // rgba(255,255,255,0.1)

        [Header("=== Toggle/Checkbox ===")]
        [Tooltip("Стиль toggle элементов")]
        public ToggleStyle toggleStyle = ToggleStyle.Checkbox;
        
        [Tooltip("Размер checkbox (px)")]
        public int checkboxSize = 24;
        
        [Tooltip("Толщина обводки checkbox (px)")]
        public int checkboxBorderWidth = 2;

        [Header("=== Dropdown ===")]
        [Tooltip("Высота строки dropdown (px)")]
        public int dropdownRowHeight = 32;
        
        [Tooltip("Высота выпадающего списка (px)")]
        public int dropdownListHeight = 120;

        [Header("=== Расположение и отступы ===")]
        [Tooltip("Расстояние между элементами в списках (px)")]
        public int elementSpacing = 6;
        
        [Tooltip("Расстояние между checkbox и текстом (px)")]
        public int checkboxTextGap = 8;

        [Header("=== Иконки ===")]
        [Tooltip("Размер иконок (px)")]
        public int iconSize = 24;
        
        [Tooltip("Толщина линий иконок (px)")]
        public int iconStrokeWidth = 2;

        [Header("=== Стиль ===")]
        public UIStylePreset stylePreset = UIStylePreset.Modern;
        
        [Tooltip("Использовать градиенты")]
        public bool useGradients = true;
        
        [Tooltip("Использовать тени")]
        public bool useShadows = false;
        
        [Tooltip("Толщина границ (px) - 0 = без границ, 0.5 = тонкая (как в HTML)")]
        [Range(0f, 4f)]
        public float borderWidth = 0.5f; // 0.5px - тонкая как в HTML

        /// <summary>
        /// Создаёт конфигурацию со стилем по умолчанию
        /// </summary>
        public static UIStyleConfiguration CreateDefault()
        {
            var config = CreateInstance<UIStyleConfiguration>();
            config.name = "Default UI Style";
            return config;
        }

        /// <summary>
        /// Применяет пресет стиля
        /// </summary>
        public void ApplyPreset(UIStylePreset preset)
        {
            stylePreset = preset;
            
            switch (preset)
            {
                case UIStylePreset.Modern:
                    ApplyModernStyle();
                    break;
                case UIStylePreset.Minimal:
                    ApplyMinimalStyle();
                    break;
                case UIStylePreset.Material:
                    ApplyMaterialStyle();
                    break;
                case UIStylePreset.Classic:
                    ApplyClassicStyle();
                    break;
            }
        }

        private void ApplyModernStyle()
        {
            backgroundColor = new Color(0.098f, 0.098f, 0.137f, 1f); // Непрозрачный
            accentColor = new Color(0.29f, 0.62f, 1.0f, 1f);
            borderColor = new Color(1f, 1f, 1f, 0.12f);
            windowBorderRadius = 16;
            buttonBorderRadius = 8;
            inputBorderRadius = 8;
            dropdownBorderRadius = 8;
            checkboxBorderRadius = 4;
            sliderBorderRadius = 0; // Авто (половина высоты)
            useGradients = true;
            useShadows = false;
            borderWidth = 0.5f;
        }

        private void ApplyMinimalStyle()
        {
            backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            accentColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            textColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            windowBorderRadius = 0;
            buttonBorderRadius = 2;
            inputBorderRadius = 2;
            dropdownBorderRadius = 2;
            checkboxBorderRadius = 2;
            sliderBorderRadius = 0;
            useGradients = false;
            useShadows = false;
        }

        private void ApplyMaterialStyle()
        {
            backgroundColor = Color.white;
            accentColor = new Color(0.25f, 0.46f, 0.85f, 1f); // Material Blue
            textColor = new Color(0.13f, 0.13f, 0.13f, 1f);
            windowBorderRadius = 4;
            buttonBorderRadius = 4;
            inputBorderRadius = 4;
            dropdownBorderRadius = 4;
            checkboxBorderRadius = 2;
            sliderBorderRadius = 0;
            useGradients = false;
            useShadows = true;
        }

        private void ApplyClassicStyle()
        {
            backgroundColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            accentColor = new Color(0.0f, 0.47f, 0.84f, 1f);
            borderColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            windowBorderRadius = 3;
            buttonBorderRadius = 3;
            inputBorderRadius = 3;
            dropdownBorderRadius = 3;
            checkboxBorderRadius = 2;
            sliderBorderRadius = 0;
            useGradients = true;
            useShadows = true;
        }
    }

    /// <summary>
    /// Пресеты стилей UI
    /// </summary>
    public enum UIStylePreset
    {
        Modern,     // Тёмный современный стиль (текущий)
        Minimal,    // Минималистичный светлый
        Material,   // Material Design (Google)
        Classic     // Классический Windows-стиль
    }

    /// <summary>
    /// Стиль toggle элементов
    /// </summary>
    public enum ToggleStyle
    {
        Checkbox,   // Классический чекбокс с галочкой (по умолчанию)
        Switch      // iOS/Android стиль переключатель
    }
}
